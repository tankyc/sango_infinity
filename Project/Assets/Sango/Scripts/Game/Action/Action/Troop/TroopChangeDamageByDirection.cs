using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按受击方位修正伤害（正合：正面 -30% / 侧面 -15%；奇袭：背面 +30% / 侧面 +15%）。
    ///
    /// <para>
    /// 【方位怎么定】以被攻击部队的正面朝向（<see cref="Troop.faceDx"/> / <see cref="Troop.faceDy"/>，
    /// 由 <c>Troop.UpdateCell</c> 按移动方向维护）为基准：
    /// · 攻击者相对方位与朝向相同 → 正面；
    /// · 相同但反号 → 背面；
    /// · 其余 → 侧面。
    /// </para>
    /// <para>
    /// 【朝向未知时不生效】刚创建、从未移动过的部队 <c>faceDx/faceDy</c> 均为 0，
    /// 此时一律放行（不做修正）。否则"刚出城"的部队会被默认当成正面朝敌吃满减伤或加伤。
    /// </para>
    /// <para>
    /// 【无法判定的攻击来源】火焰、建筑反击、陷阱等 <c>atk.AsTroop()</c> 取不到部队，
    /// 没有空间方位可言，直接跳过。
    /// </para>
    /// <para>
    /// 【配置】<c>isDefender</c> 决定本效果是"我打别人"还是"别人打我"：
    /// 奇袭配 0（默认，作为攻击方），正合配 1（作为防守方）。
    /// 正合要同时表达正面 -30 与侧面 -15，配两条 Action 即可（position 分别 0 / 1）。
    /// </para>
    /// </summary>
    public class TroopChangeDamageByDirection : TroopTroopActionBase
    {
        /// <summary>生效方位：0 正面 / 1 侧面 / 2 背面</summary>
        int position;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：position（0/1/2）、value（百分点，正=增伤，负=减伤）、
        /// isDefender（0 攻击方 / 1 防守方）、condition。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            position = p.Value<int>("position");
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>
        /// 兵力变化结算时按方位修正伤害。
        /// </summary>
        /// <param name="defencer">受伤部队（方位判定的基准）</param>
        /// <param name="atk">攻击来源</param>
        /// <param name="atkBack">反击强度因子，0 = 非反击（见 TroopActionBase.IsAttackBack）</param>
        /// <param name="overrideData">兵力变化量</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atk, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (defencer == null || overrideData == null)
                return;

            // 只修正掉兵；回血 / 补给没有"方位"概念
            if (overrideData.Value <= 0)
                return;

            // 攻击来源必须是部队：火焰 / 建筑反击没有空间方位
            Troop atkTroop = atk?.AsTroop();
            if (atkTroop == null || atkTroop.cell == null || defencer.cell == null)
                return;

            // 归属过滤：确定"我是攻击方还是防守方"
            if (Troop != null)
            {
                if (isDefender == 0)
                {
                    if (atkTroop != Troop) return;
                }
                else
                {
                    if (defencer != Troop) return;
                }
            }
            else if (Force != null)
            {
                Troop self = isDefender == 0 ? atkTroop : defencer;
                if (self.BelongForce != Force) return;
            }
            else
            {
                return;
            }

            int fx = defencer.faceDx;
            int fy = defencer.faceDy;
            if (fx == 0 && fy == 0)
                return;

            int dx = atkTroop.cell.x - defencer.cell.x;
            int dy = atkTroop.cell.y - defencer.cell.y;
            int ax = dx > 0 ? 1 : (dx < 0 ? -1 : 0);
            int ay = dy > 0 ? 1 : (dy < 0 ? -1 : 0);

            // 同格（理论上不会发生）视为无法判定
            if (ax == 0 && ay == 0)
                return;

            bool isFront = ax == fx && ay == fy;
            bool isBack = ax == -fx && ay == -fy;
            int actual = isFront ? 0 : (isBack ? 2 : 1);
            if (actual != position)
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(defencer)))
                return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }
    }
}
