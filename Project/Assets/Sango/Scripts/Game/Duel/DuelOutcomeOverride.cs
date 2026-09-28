/*
 * 文件名：DuelOutcomeOverride.cs
 * 描述：事件单挑的结果强制覆盖——让剧本事件能指定确定的胜负与结局
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

namespace Sango.Core.Duel
{
    /// <summary>
    /// 事件单挑的结果强制覆盖。
    ///
    /// 为什么需要它：史实桥段需要**确定**的结果，而单挑的自然结算是概率性的——
    ///   · "关羽斩颜良"：必须颜良死，不能有概率被逃掉；
    ///   · "三英战吕布"：吕布必须败走但**不能死**；
    ///   · "赵云长坂坡"：赵云不能败。
    /// 概率结算下这些事件会时对时错，破坏剧本叙事。
    ///
    /// 覆盖点（见 <see cref="Duel"/>）：
    ///   · ResultHandler 入口先调 ApplyOutcomeOverride，把强制胜负写进 param；
    ///   · ResultNormal 里按 loserResult 覆盖败方结局；
    ///   · ignoreGovernorImmunity 绕过"君主不可被俘/战死"的暂时规则
    ///     （白门楼斩吕布需要这个）。
    ///
    /// 不指定（默认值）的项一律保持自然结算，因此传入 null 等价于"不干预"。
    /// </summary>
    public class DuelOutcomeOverride
    {
        /// <summary>
        /// 强制胜方队伍：0 = 挑战方，1 = 应战方，-1 = 不指定（由单挑逻辑自然决出）。
        /// </summary>
        public int winnerTeam = -1;

        /// <summary>
        /// 强制败方成员的结局。
        /// DuelCharaResult_Max 表示不指定（保持自然结算）。
        /// </summary>
        public DuelCharaResult loserResult = DuelCharaResult.DuelCharaResult_Max;

        /// <summary>
        /// 是否绕过"君主不可被俘虏、也不会战死"的暂时规则。
        /// 默认 false；仅在"斩吕布""弑曹髦"这类必须致君主于死地的史实桥段才置 true。
        /// </summary>
        public bool ignoreGovernorImmunity;

        /// <summary>
        /// 是否绕过"一方不是普通势力时不发生俘虏或死亡"的降级规则。
        /// 默认 false；用于"处斩非普通势力（如异族、黄巾）武将"的场景。
        /// </summary>
        public bool ignoreForceKind;

        /// <summary>
        /// 是否指定了任何一项强制结果。全为默认值时可供调用方跳过覆盖逻辑。
        /// </summary>
        public bool IsSpecified
        {
            get { return winnerTeam >= 0 || loserResult != DuelCharaResult.DuelCharaResult_Max; }
        }
    }
}
