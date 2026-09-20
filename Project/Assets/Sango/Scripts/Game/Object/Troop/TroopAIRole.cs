/*
 * 文件名：TroopAIRole.cs
 * 描述：部队角色（Role）与角色权重。
 *       角色描述"怎么打"，与 MissionType（去哪）正交：
 *       同一个进攻任务，攻坚型会强打城池，骚扰型会绕后打运输队，游击型会保存实力择机而动。
 */

namespace Sango.Core
{
    /// <summary>
    /// 部队角色：描述部队的作战定位。
    ///
    /// 与 <see cref="MissionType"/>（去哪）正交 —— 角色决定"怎么打"，
    /// 由该角色的权重集合 <see cref="TroopRoleWeights"/> 影响技能评分与任务专注度。
    /// </summary>
    public enum TroopRole : int
    {
        /// <summary>未指定：由 <c>Troop.ResolveRole</c> 依据任务与自身属性自动推导</summary>
        Auto = -1,
        /// <summary>攻坚：高攻低守，优先攻打城池 / 大部队，敢吃反击</summary>
        Assault = 0,
        /// <summary>防守：守城 / 守要道，重视地形与规避反击</summary>
        Defender = 1,
        /// <summary>骚扰：专打运输队 / 落单部队，打完就跑</summary>
        Harasser = 2,
        /// <summary>游击：兵力偏低也敢打，靠机动寻找机会</summary>
        Skirmisher = 3,
        /// <summary>护卫：贴身保护补给队 / 主将，尽量避免接战</summary>
        Escort = 4,
        /// <summary>守备：驻守据点，不主动出击</summary>
        Guard = 5,
    }

    /// <summary>
    /// 单个部队角色的权重集合。
    ///
    /// 所有倍率均为百分数，<c>100</c> 表示"不修正"。
    /// 可通过 <c>Data/Common/AIConfig.json</c> 的 <c>roleAssault</c> /
    /// <c>roleDefender</c> / <c>roleHarasser</c> / <c>roleSkirmisher</c> /
    /// <c>roleEscort</c> / <c>roleGuard</c> 节点做部分覆盖。
    /// </summary>
    public class TroopRoleWeights
    {
        /// <summary>攻击收益倍率（%）：越高越倾向主动出手</summary>
        public int attackScale = 100;

        /// <summary>反击惩罚倍率（%）：越高越规避"贴脸打高反击单位"</summary>
        public int counterPenaltyScale = 100;

        /// <summary>
        /// 任务专注度（%）：
        /// 乘在"任务主目标权重"上，越高越执着于既定任务目标，
        /// 越低越容易被眼前的其它目标吸引（例如游击型）。
        /// </summary>
        public int missionFocusScale = 100;
    }
}
