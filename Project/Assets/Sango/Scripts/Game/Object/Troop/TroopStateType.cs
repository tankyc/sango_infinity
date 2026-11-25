namespace Sango.Core
{
    /// <summary>
    /// 【历史遗留】旧的状态枚举（无任何引用），仅保留以免外部 Mod / 旧序列化数据解析失败。
    ///
    /// 新代码请使用 <see cref="TroopStateFlag"/>：它是位域，能同时表达多种状态，
    /// 且直接对应 <see cref="Troop.stateFlags"/> 的存储格式。
    /// </summary>
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
    ///
    /// 【写入 / 移除为什么是"重算"而不是逐个 set / clear】
    /// 位域里的每一位都是**派生值**，唯一真相是 <see cref="BuffManager"/> 里的 Buff 列表。
    /// <see cref="BuffManager.AddBuff"/> / <see cref="BuffManager.RemoveBuff"/> /
    /// <see cref="BuffManager.RemoveBuffByKind"/> / <see cref="BuffManager.OnForceTurnStart"/>
    /// （到期 RemoveAll）/ <see cref="BuffManager.Clear"/> / <see cref="BuffManager.Init"/>（读档）
    /// 六条路径全部走 <see cref="BuffManager.RefreshMasterStateFlags"/> 重算，
    /// 因此"加 Buff 置位、掉 Buff 清位"天然成对：
    ///   · 不会因为某条移除路径漏调 clear 而残留状态；
    ///   · 同一部队叠两个混乱 Buff，移除其中一个时不会被误清（引用计数问题不存在）；
    ///   · 读档后按存档里的 Buff 列表重算，不会出现"存档有混乱、标记却是 0"。
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

        /// <summary>
        /// 扰乱 / 眩晕：回合开始被迫结束行动（<c>Troop.ActionOver = true</c>）。
        /// 由 <see cref="Stun"/> 效果写入；冰冻（<see cref="Freeze"/>）同样置这一位。
        /// </summary>
        Stun = 1 << 1,

        /// <summary>
        /// 伪报（原版 <c>UnitState_Gihou</c>）：回合开始被强制送回自己的据点。
        /// 由 <see cref="Escape"/> 效果写入。
        /// </summary>
        Escape = 1 << 2,

        /// <summary>
        /// 足止（原版 <c>UnitState_Ashidome</c>）：移动力系数归零，能打不能走。
        /// 行为由 <see cref="Buff.troopMoveCoef"/> 表达，这一位只作为统一的状态查询入口。
        /// </summary>
        MoveSeal = 1 << 3,

        /// <summary>
        /// 暴走（原版 <c>UnitState_Bousou</c>）：回合开始被强制改派为"攻击最近敌军"。
        /// 由 <see cref="Berserk"/> 效果写入。
        /// </summary>
        Berserk = 1 << 4,

        /// <summary>
        /// 沉默：不能施放战法与计略（普攻不受影响）。
        /// 拦截点在 <see cref="SkillInstance.CanBeSpell"/>（菜单 / AI 统一入口）
        /// 与 <see cref="Troop.SpellSkill"/>（执行兜底）。
        /// </summary>
        Silence = 1 << 5,

        /// <summary>中毒：每回合按 Buff 配置掉兵（<see cref="Poison"/>）</summary>
        Poison = 1 << 6,

        /// <summary>燃烧：每回合按 Buff 配置掉兵（<see cref="Burn"/>）</summary>
        Burn = 1 << 7,

        /// <summary>冰冻：无法行动（<see cref="Freeze"/>），与 <see cref="Stun"/> 叠加出现</summary>
        Freeze = 1 << 8,

        /// <summary>无敌：免疫一切兵力损失（<see cref="Invincible"/>）</summary>
        Invincible = 1 << 9,

        /// <summary>护盾：按点数吸收伤害（<see cref="Shield"/>）</summary>
        Shield = 1 << 10,
    }

    /// <summary>
    /// <see cref="TroopStateFlag"/> 的判定掩码集合。
    ///
    /// 【为什么单独抽出来】同一个"位"在不同业务里的语义不同，写成散落的魔法数字
    /// 迟早会漏改一处（例如反击判定忘了把 Freeze 算进"被控"）。
    /// </summary>
    public static class TroopStateFlags
    {
        /// <summary>
        /// **被控**状态的并集：处于这些状态时不能行动、不能反击。
        ///
        /// 只包含"完全无法行动"的状态：
        ///   · <see cref="TroopStateFlag.Stun"/>   扰乱 / 冰冻 → 回合开始置 ActionOver；
        ///   · <see cref="TroopStateFlag.Escape"/> 伪报 → 回合开始被送回城。
        /// 注意**不含** <see cref="TroopStateFlag.MoveSeal"/>（足止还能原地攻击）、
        /// <see cref="TroopStateFlag.Berserk"/>（暴走仍会主动攻击）与
        /// <see cref="TroopStateFlag.Silence"/>（沉默只是不能放技能）。
        /// </summary>
        public const int ControlMask =
            (int)(TroopStateFlag.Stun | TroopStateFlag.Escape | TroopStateFlag.Freeze);

        /// <summary>
        /// 妨碍行动的状态并集：被控 + 足止 + 暴走。
        /// 供 AI 评分类逻辑"目标是否已被干扰"使用（与"不能反击"的 <see cref="ControlMask"/> 区分）。
        /// </summary>
        public const int DisableMask =
            (int)(TroopStateFlag.Stun | TroopStateFlag.Escape | TroopStateFlag.Freeze
                | TroopStateFlag.MoveSeal | TroopStateFlag.Berserk);
    }
}
