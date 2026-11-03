namespace Sango.Core
{
    public enum TroopStateType : int
    {
        None = 0,

        // 眩晕
        Stun,

        // 混乱
        Chaos,

        // 伪报
        Escape
    }

    /// <summary>
    /// 部队**状态标记位**（位域，存在 <see cref="Troop.stateFlags"/>）。
    ///
    /// 【为什么需要】状态本身挂在 Buff 上（<see cref="Buff"/>），但有些判定在**极热的
    /// 路径**上（例如 <see cref="Map.IsZOC"/> 每展开一格就要看 6 个邻格），
    /// 每次都遍历 Buff 列表代价太高。这里把"状态是否成立"折叠成一个整数位域，
    /// 由 BuffManager 在增删 Buff 时重算一次，热路径只读一个 bit。
    ///
    /// 【与原版对应】Open311 的 <c>Unit::state_</c>（s11_enum_scen.h:926-938），
    /// 原版同样是一个整数，不是逐帧遍历状态表。
    /// </summary>
    [System.Flags]
    public enum TroopStateFlag : int
    {
        /// <summary>无状态</summary>
        None = 0,

        /// <summary>
        /// 混乱（原版 <c>UnitState_Konran</c>）。
        /// 副作用：该部队**不向周围施加 ZOC** —— 原版 <c>is_zoc_pos</c> 的部队分支
        /// 要求 <c>unit->get_state() != UnitState_Konran</c>（s11_sys_unit_path.cpp:361-363）。
        /// </summary>
        Chaos = 1 << 0,
    }
}
