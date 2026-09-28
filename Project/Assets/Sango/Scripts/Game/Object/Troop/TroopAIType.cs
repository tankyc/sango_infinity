/*
 * 文件名：TroopAIType.cs
 * 描述：部队 AI 类型。
 *
 *       用于把"这支部队该由哪套 AI 逻辑驱动"显式类型化，
 *       替代原先散落在 AIPrepare 里的 `if (IsPlayerControl) return;` 之类的隐式判断。
 *
 *       两类 AI 共用同一个任务执行层（TroopMissionBehaviour），
 *       区别只在"行动前准备"阶段：
 *         · Faction —— 势力 AI，执行完整启发式；
 *         · Appoint —— 委任 AI，不做任何判断，单纯执行被指派的任务。
 */

namespace Sango.Core
{
    /// <summary>
    /// 部队 AI 类型：决定该部队的行动前准备走哪一套逻辑。
    /// </summary>
    public enum TroopAIType : int
    {
        /// <summary>
        /// 未启用 AI：玩家第一军团的部队尚未被指派任务，完全由玩家手动操控。
        /// 不做任何准备，也不会自动生成任务（不会"擅自返城"）。
        /// </summary>
        None = 0,

        /// <summary>
        /// 委任 AI：玩家第一军团中**已被指派任务**的部队。
        ///
        /// 语义就是"照玩家指派的任务单纯执行"：
        ///   · 不判断战场态势
        ///   · 不做主动撤退 / 求援 / 随军增筑
        ///   · 不改派或追加任务
        /// 只是把 missionType / missionTarget 原样交给任务执行层。
        /// </summary>
        Appoint = 1,

        /// <summary>
        /// 势力 AI：AI 势力的部队，以及玩家势力中非主军团（被委任军团）的部队。
        ///
        /// 走完整的启发式流程：态势分档、主动撤退、就近补给、求援、
        /// 随军增筑、角色推导等。
        /// </summary>
        Faction = 2,
    }
}
