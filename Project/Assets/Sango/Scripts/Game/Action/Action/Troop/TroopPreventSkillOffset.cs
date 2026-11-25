using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 战法位移免疫：否决本方部队被敌方战法推走（铁壁"不会被敌方战法位移"）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnTroopCheckSkillOffset"/>，由 <c>SkillInstance.DoOffset</c>
    /// 在真正改写格子之前询问。否决后不会产生任何位移、撞击伤害与后续的
    /// <see cref="GameEvent.OnTroopSkillOffset"/> 通知，也不会有"先推一格再推回来"的表现抖动。
    /// </para>
    /// <para>
    /// 【为什么不复用 OnTroopSkillOffset】那个事件在位移**发生之后**才派发
    /// （SkillInstance 里用"前后格子是否不同"判定），事后补一次位移等于交了双份路径，
    /// 且途经格的地雷 / 陷阱会被踩两次。
    /// </para>
    /// </summary>
    public class TroopPreventSkillOffset : TroopTroopActionBase
    {
        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：condition（可选，用于"仅在森林中""仅对骑兵"等限定）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCheckSkillOffset += OnTroopCheckSkillOffset;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCheckSkillOffset -= OnTroopCheckSkillOffset;
        }

        /// <summary>
        /// 被战法位移前决定是否否决。
        /// </summary>
        /// <param name="defencer">被位移方部队</param>
        /// <param name="caster">施法方部队</param>
        /// <param name="allowData">是否允许（默认 true，置 false 即免疫）</param>
        void OnTroopCheckSkillOffset(Troop defencer, Troop caster, OverrideData<bool> allowData)
        {
            if (defencer == null || allowData == null)
                return;

            // 归属过滤：只保护自己 / 自己势力的部队
            if (Troop != null && Troop != defencer) return;
            if (Force != null && defencer.BelongForce != Force) return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(defencer)))
                return;

            allowData.Value = false;
        }
    }
}
