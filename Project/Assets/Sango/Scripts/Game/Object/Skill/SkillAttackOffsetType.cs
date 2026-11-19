namespace Sango.Core
{

    public enum SkillAttackOffsetType : int
    {
        Customize = 0,
        Ring = 1,
        DirectionLine = 2,
        SelfRing = 3,
        SpellNeighbors = 4,
        Spiral = 5,
        Fan = 6,      // 扇形
        Rectangle = 7, // 矩形
        Cross = 8,     // 十字形
        Square = 9,    // 方形
        Diamond = 10,  // 菱形
        /// <summary>
        /// 概率递减波及（「连环」用）。
        ///
        /// 与 <see cref="Spiral"/> 的区别：Spiral 是"范围内全部命中"；本类型按圈层由内向外
        /// 逐个目标掷骰，每通过一个目标就按配置规则降低概率，**一旦某个目标判定不通过就整体停止**，
        /// 后续目标与外圈都不再生效。对应原版连环"每次只额外波及极少数邻接目标"的随机性
        /// （s11_sys_anim_unit.cpp:1275-1282 `shuffle(ring, ring_count, 1)` 后取 `ring[0]`）。
        ///
        /// 数据格式：<c>[11, atkRange, 概率初始值, 概率递减类型, 概率递减数值]</c>
        /// <list type="bullet">
        /// <item>atkRange（下标 1）最大圈层半径</item>
        /// <item>概率初始值（下标 2）0~100 的百分数</item>
        /// <item>概率递减类型（下标 3）见 <see cref="SkillDecayType"/></item>
        /// <item>概率递减数值（下标 4）减法 = 每次扣掉的百分点；乘法 = 每次相乘的百分数系数</item>
        /// </list>
        /// 中心格（主目标）恒命中，不参与判定 —— 它本来就是计略的原始目标。
        /// </summary>
        DecaySpread = 11
    }

    /// <summary>
    /// 「概率递减波及」的概率变化规则（<see cref="SkillAttackOffsetType.DecaySpread"/> 的下标 3）。
    /// </summary>
    public enum SkillDecayType : int
    {
        /// <summary>
        /// 减法：每通过一个目标，概率减去「递减数值」个百分点。
        /// 例：初始 70、递减 25 → 70 → 45 → 20 → 0（0 视为无法再通过）。
        /// </summary>
        Subtract = 0,

        /// <summary>
        /// 乘法：每通过一个目标，概率乘以「递减数值 / 100」。
        /// 例：初始 80、递减 50 → 80 → 40 → 20 → 10 …（只剩整数，向下取整）。
        /// </summary>
        Multiply = 1
    }
}
