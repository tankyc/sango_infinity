using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 被围减伤（汉土特技 0.4.22 —— 55416 无畏）。
    ///
    /// <para>
    /// 【设计原文】部队回合开始时统计三格内敌军，每支使本部队受到的物理伤害降低 5%，
    /// 最多降低 30%，下次本部队回合开始时重算。
    /// </para>
    /// <para>
    /// 【为什么不新开事件】统计挂在官方 <c>OnTroopTurnStart</c>（部队回合开始），
    /// 减伤挂在官方 <c>OnTroopChangeTroops</c>（兵力变化前改数值），
    /// 本类只在两者之间维护一个快照字段（<see cref="Troop.nearEnemySnapshot"/>），不新增结算路径。
    /// </para>
    /// <para>
    /// 【口径】
    /// · 快照时机：本队回合开始（<c>OnTroopTurnStart</c> 且 <c>troop == Troop</c>）；
    /// · 统计范围：以本队所在格为中心的 <c>Spiral(radius)</c>（默认 3 格，含中心格），
    ///   只数 <c>IsEnemy</c> 的部队，排除自己；
    /// · 减伤只作用于"本队被物理攻击"的掉兵（<c>defencer == Troop</c>，计略不算），
    ///   反击被打同样算；每支敌军 <c>perEnemy</c>%（默认 5%），封顶 <c>maxBonus</c>%（默认 30%）；
    /// · 快照持久化（存读档保留），下次本队回合开始才刷新。
    /// </para>
    /// <para>
    /// 【性能】统计每次部队回合只跑一次，37 格回调（radius = 3）；减伤每次受击只做一次
    /// 乘法与截断。两条路径都不分配堆内存：Spiral 回调委托在 Init 时缓存。
    /// </para>
    /// JSON 参数：<c>radius</c>（默认 3）、<c>perEnemy</c>（默认 5）、<c>maxBonus</c>（默认 30）。
    /// </summary>
    public class TroopDamageReduceByNearEnemy : TroopTroopActionBase
    {
        /// <summary>统计半径（默认 3 格）</summary>
        int radius = 3;
        /// <summary>每支敌军提供的减伤百分点（默认 5）</summary>
        int perEnemy = 5;
        /// <summary>减伤上限（百分点，默认 30）</summary>
        int maxBonus = 30;

        /// <summary>统计过程中的临时计数（避免每次回合开始新建容器）</summary>
        int scanCount;
        /// <summary>Spiral 回调（缓存，避免每次统计新建委托）</summary>
        System.Action<Cell> scanAction;

        /// <summary>初始化并订阅回合开始 / 兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 必须是部队）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int r = p.Value<int>("radius");
            radius = r > 0 ? r : 3;

            int pe = p.Value<int>("perEnemy");
            perEnemy = pe > 0 ? pe : 5;

            // 缺键不能用 Value<int>（返回 0 会被误读成"不封顶"）
            JToken mbToken = p["maxBonus"];
            if (mbToken == null)
                maxBonus = 30;
            else
            {
                int mb = mbToken.ToObject<int>();
                maxBonus = mb > 0 ? mb : 30;
            }

            if (Troop == null)
            {
                Sango.Log.Error("TroopDamageReduceByNearEnemy 必须装配在部队上（快照存在部队身上），该配置不会生效");
                return;
            }

            if (scanAction == null)
                scanAction = ScanCell;

            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>
        /// 本队回合开始时重算三格内敌军数量。
        /// </summary>
        /// <param name="troop">当前回合开始的部队</param>
        /// <param name="scenario">当前剧本</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            Troop self = this.Troop;
            if (self == null || !ReferenceEquals(troop, self) || self.cell == null)
                return;

            scanCount = 0;
            self.cell.Spiral(radius, scanAction);
            self.nearEnemySnapshot = scanCount;
        }

        /// <summary>
        /// 兵力变化前按"回合开始快照"减伤。
        /// </summary>
        /// <param name="defencer">兵力变化的一方（这里是本队）</param>
        /// <param name="atker">施加方</param>
        /// <param name="atkBack">反击强度因子（本类不据此过滤：反击被打同样算受击）</param>
        /// <param name="overrideData">待修正的兵力变化量（负数为掉兵）</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (overrideData == null || overrideData.Value >= 0) return;

            Troop self = this.Troop;
            if (self == null || !ReferenceEquals(defencer, self)) return;
            if (self.nearEnemySnapshot <= 0) return;

            // 只对物理攻击生效（计略 / 火焰 / 建筑反击不算）
            SkillInstance skill = atker as SkillInstance;
            if (skill == null || skill.IsStrategy()) return;

            if (!CheckTroop(defencer, atker)) return;

            int bonus = self.nearEnemySnapshot * perEnemy;
            if (bonus > maxBonus) bonus = maxBonus;
            // 兜底：配置写得过大时不允许把掉兵量反向（100 - bonus 必须为正）
            if (bonus > 90) bonus = 90;

            overrideData.Value = overrideData.Value * (100 - bonus) / 100;
        }

        /// <summary>Spiral 回调：累计周围敌军数量</summary>
        /// <param name="cell">当前格</param>
        void ScanCell(Cell cell)
        {
            if (cell == null) return;
            Troop t = cell.troop;
            if (t == null) return;

            Troop self = this.Troop;
            if (self == null || ReferenceEquals(t, self)) return;
            if (!t.IsEnemy(self)) return;

            scanCount++;
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
