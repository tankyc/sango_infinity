using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 复合条件伤害：按"属性差 / 目标受伤 / 相邻敌军数"派生的百分比加成或减伤。
    ///
    /// <para>
    /// 【对应设计条目】乘险（按双方统率差增伤）、久御（对已受伤目标增伤）、
    /// 叠锋（按相邻敌军数量增伤）一类"不是固定 +X%，而是随局势变化的公式"。
    /// </para>
    /// <para>
    /// 【为什么不新开事件】官方已有的 <c>OnTroopChangeTroops</c> 就是"兵力变化前的改数值"挂点
    /// （<see cref="TroopChangeDamage"/> 用的就是它），本类只是把固定的 <c>value</c>
    /// 换成按条件算出来的值，事件、时序、过滤全部复用，不新增运行时开销。
    /// </para>
    /// <para>
    /// 【宿主方向】装配在部队上时以该部队为"我方"；装配在势力（技巧）上时按 <c>isDefender</c>
    /// 定方向（0 攻击方 / 1 受击方），与基类 <c>CheckTroop</c> 选取的部队保持一致。
    /// </para>
    /// <para>
    /// 【只修正掉兵】三种条件都是"增伤"公式，因此仅当 <c>overrideData.Value &lt; 0</c> 时介入；
    /// 回血 / 补给（Value &gt; 0）不套百分比，避免变成"治疗加成"。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>kind</term><description>0 属性差 / 1 目标已负伤 / 2 相邻敌军数</description></item>
    /// <item><term>statType</term><description>kind = 0 时比较的属性：0 统率 / 1 武力，默认 0</description></item>
    /// <item><term>perUnit</term><description>每 1 点属性差 / 每 1 支相邻敌军的加成（百分点），默认 5</description></item>
    /// <item><term>value</term><description>kind = 1 时的固定加成（百分点）</description></item>
    /// <item><term>maxBonus</term><description>加成上限（百分点）：缺省 100，显式配 0 或负数表示不封顶（内部仍按 ±10000 兜底防溢出）</description></item>
    /// <item><term>atkBack</term><description>反击过滤配置：-1 不限（默认）/ 0 仅非反击 / 1 仅反击（注意这是过滤器选项，运行时事件参数是 28~90 的强度因子）</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 【atkBack 过滤】配置项 <c>atkBack</c> 是过滤器（-1 不限 / 0 仅非反击 / 1 仅反击）；
    /// 事件第三个参数已换成 <see cref="AttackBackInfo"/>，判定统一走
    /// <see cref="TroopActionBase.IsAttackBack"/>，不再从 int 里猜。
    /// </para>
    /// </summary>
    public class TroopDamageByCondition : TroopTroopActionBase
    {
        /// <summary>条件类型：0 属性差 / 1 目标已负伤 / 2 相邻敌军数</summary>
        int kind;
        /// <summary>比较属性：0 统率 / 1 武力</summary>
        int statType;
        /// <summary>每单位加成（百分点）</summary>
        int perUnit = 5;
        /// <summary>加成上限（百分点）</summary>
        int maxBonus = 100;
        /// <summary>是否启用上限：配置 maxBonus 为 0 / 负数时为 false（不封顶）</summary>
        bool hasMaxBonus = true;
        /// <summary>
        /// 加成硬上限（百分点）：即使配"不封顶"也不允许越过，
        /// 防止把配置写成天文数字后 `(100 + bonus) * overrideData.Value` 溢出 int 造成伤害反向。
        /// </summary>
        const int MaxBonusHardLimit = 10000;
        /// <summary>反击过滤：-1 不限 / 0 仅非反击 / 1 仅反击</summary>
        int atkBackFilter = -1;
        /// <summary>宿主 / 配置非法（直接不生效，避免"对所有部队生效"）</summary>
        bool invalidHost;

        /// <summary>相邻敌军计数缓冲（复用，避免每次伤害结算都分配容器）</summary>
        readonly List<Troop> neighborBuffer = new List<Troop>(8);
        /// <summary>Spiral 回调（缓存，避免每次伤害结算都新建委托）</summary>
        System.Action<Cell> collectAction;

        /// <summary>初始化并订阅兵力变化事件</summary>
        /// <param name="p">JSON 参数</param>
        /// <param name="sangoObjects">宿主对象（[0] 应为部队或势力）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            kind = p.Value<int>("kind");
            statType = p.Value<int>("statType");

            // kind 越界属于配置错误：静默返回 0 会让"配了却不生效"极难排查，直接停用并报错。
            if (kind < 0 || kind > 2)
            {
                invalidHost = true;
                Sango.Log.Error($"TroopDamageByCondition 的 kind={kind} 非法（应为 0/1/2），该配置不会生效");
                return;
            }

            int pu = p.Value<int>("perUnit");
            perUnit = pu != 0 ? pu : 5;

            // maxBonus：缺键 = 默认 100；显式配 0 / 负数 = 不封顶。
            // 不能直接 p.Value<int>("maxBonus")，缺键返回 0 会被误读成"不封顶"。
            JToken mbToken = p["maxBonus"];
            if (mbToken == null)
            {
                maxBonus = 100;
                hasMaxBonus = true;
            }
            else
            {
                int mb = mbToken.ToObject<int>();
                hasMaxBonus = mb > 0;
                maxBonus = hasMaxBonus ? mb : 0;
            }

            // 键缺失时必须视为"不限"：Value<int> 缺失返回 0，而 0 的含义是"仅非反击"
            JToken token = p["atkBack"];
            atkBackFilter = token != null ? token.ToObject<int>() : -1;

            // kind = 1 靠 value 给固定加成，漏配 value 会"配了却没效果"，这里提前提示
            if (kind == 1 && value == 0)
                Sango.Log.Warning("TroopDamageByCondition（kind=1）的 value 未配置或为 0，该特技不会产生任何伤害加成");

            // 宿主校验：基类 Init 只从 sangoObjects[0] 取部队 / 势力。
            // 若两者都为空（例如误配在 Person / City 类特技上），CheckTroop 的
            // `Force != null` 与 `Troop != null` 两道门都会跳过，等于对所有部队生效，
            // 这里必须显式停用并报错，避免伤害数值莫名其妙全局翻倍。
            if (Troop == null && Force == null)
            {
                invalidHost = true;
                Sango.Log.Error($"TroopDamageByCondition 的宿主既不是部队也不是势力（kind={kind}），该配置不会生效");
                return;
            }

            if (collectAction == null)
                collectAction = CollectCell;

            // 重复 Init 时先退订再订阅，避免同一个实例挂两次导致加成被平方
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件并清空缓冲</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
            neighborBuffer.Clear();
        }

        /// <summary>
        /// 兵力变化前的伤害修正。
        /// </summary>
        /// <param name="defencer">兵力变化的一方</param>
        /// <param name="atker">施加方（部队 / 战法 / 建筑）</param>
        /// <param name="atkBack">反击强度 / 因子，0 = 非反击（本项目不传 0/1 布尔）</param>
        /// <param name="overrideData">待修正的兵力变化量</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atker, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (invalidHost) return;
            if (overrideData == null) return;

            // 只修正掉兵（Value < 0）。本类三种条件都是"增伤"公式，
            // 若对回血 / 补给（Value > 0）也套百分比，会变成"治疗加成"，
            // 与 TroopChangeDamageByDirection 的"只修正掉兵"口径保持一致。
            if (overrideData.Value >= 0) return;

            // 反击过滤放在最前：提前过滤更省。判定口径统一走基类 IsAttackBack
            // （atkBack 是"反击强度因子"，0 = 非反击；不能写 == 1，理由见基类注释）。
            bool isBackAttack = IsAttackBack(atkBack);
            if (atkBackFilter == 0 && isBackAttack) return;
            if (atkBackFilter == 1 && !isBackAttack) return;

            // 先解析"发起攻击的部队"：解析不出（火焰 / 建筑 / 陷阱）就没有敌我方向可言，
            // 直接退出，比进 CheckTroop 走一圈更省；后续计算也全都依赖它。
            Troop attacker = atker != null ? atker.AsTroop() : null;
            if (attacker == null) return;

            // 快速拒绝：部队宿主时，本次变化的两方都不是自己，就没必要走 CheckTroop 全套过滤。
            // CheckTroop 内部的 `Troop != troop` 本来也会拒绝，这里只是把它提前到最便宜的位置；
            // SangoObject 未重载 == / !=，此处就是引用比较。
            if (this.Troop != null && this.Troop != attacker && this.Troop != defencer) return;

            if (!CheckTroop(defencer, atker)) return;

            // 持有方：装配在部队上时以该部队为锚；装配在势力上时按 isDefender 定方向，
            // 与 CheckTroop 内部 "isDefender == 0 取 skill.master，否则取 defencer" 完全一致。
            Troop self = this.Troop != null ? this.Troop : (isDefender == 0 ? attacker : defencer);
            Troop enemy = ReferenceEquals(self, attacker) ? defencer : attacker;
            if (self == null || enemy == null || ReferenceEquals(self, enemy)) return;

            int bonus = Compute(self, enemy);
            if (bonus == 0) return;

            overrideData.Value = overrideData.Value * (100 + bonus) / 100;
        }

        /// <summary>
        /// 按条件计算加成百分比。
        /// </summary>
        /// <param name="self">持有特技的一方（我方）</param>
        /// <param name="enemy">另一方（敌方）</param>
        /// <returns>百分点加成，可为负</returns>
        int Compute(Troop self, Troop enemy)
        {
            int bonus = 0;
            switch (kind)
            {
                case 0:
                    {
                        // 属性差：己方高于对方的部分 × perUnit。
                        // Person.Command / Strength 已按伤病衰减，所以负伤会自然削弱差值。
                        if (self.Leader == null || enemy.Leader == null)
                            return 0;
                        int diff = GetStat(self.Leader, statType) - GetStat(enemy.Leader, statType);
                        if (diff > 0)
                            bonus = diff * perUnit;
                        break;
                    }
                case 1:
                    // 目标已负伤：固定 value
                    if (enemy.Leader != null && enemy.Leader.injury > 0)
                        bonus = value;
                    break;
                case 2:
                    {
                        // 相邻敌军数：围着"我方"数敌军（不含目标自己，避免目标被重复计入）。
                        // 【口径说明】原版 GetAttackBackFactor 的 CountAdjacentEnemies（Troop.cs:2400）
                        // 统计 6 邻居、含被攻击目标本身；本类按"叠锋"的设计口径排除目标，
                        // 即"被 2 支敌军贴身"= +2 × perUnit。若要改成与原版包围数完全一致，
                        // 去掉下面 `ReferenceEquals(t, enemy)` 这一项即可。
                        if (self.cell == null)
                            return 0;
                        neighborBuffer.Clear();
                        self.cell.Spiral(1, collectAction);
                        int count = 0;
                        for (int i = 0; i < neighborBuffer.Count; i++)
                        {
                            Troop t = neighborBuffer[i];
                            if (t == null || ReferenceEquals(t, self) || ReferenceEquals(t, enemy))
                                continue;
                            if (!t.IsEnemy(self))
                                continue;
                            count++;
                        }
                        bonus = count * perUnit;
                        break;
                    }
                default:
                    return 0;
            }

            // 配置上限（maxBonus 配 0 / 负数时不封顶）
            if (hasMaxBonus)
            {
                if (bonus > maxBonus) bonus = maxBonus;
                else if (bonus < -maxBonus) bonus = -maxBonus;
            }

            // 兜底硬上限：见 MaxBonusHardLimit 注释，避免 (100 + bonus) 乘法溢出 int
            if (bonus > MaxBonusHardLimit) bonus = MaxBonusHardLimit;
            else if (bonus < -MaxBonusHardLimit) bonus = -MaxBonusHardLimit;

            // 不允许把伤害压到 0 或反向：调用方按 (100 + bonus) 做乘算，
            // bonus <= -100 会让兵力变化量变成 0 / 反号（掉兵变回兵），这里保留至少 1% 伤害。
            if (bonus < -99) bonus = -99;
            return bonus;
        }

        /// <summary>取指定属性值</summary>
        /// <param name="person">武将</param>
        /// <param name="type">0 统率 / 1 武力</param>
        static int GetStat(Person person, int type)
        {
            return type == 1 ? person.Strength : person.Command;
        }

        /// <summary>Spiral 回调：收集周围部队</summary>
        /// <param name="cell">当前格</param>
        void CollectCell(Cell cell)
        {
            if (cell == null || cell.troop == null)
                return;
            neighborBuffer.Add(cell.troop);
        }

        /// <summary>执行（本类只做伤害修正，不参与触发器链）</summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
