/*
 * 文件名：DebateRules.cs
 * 描述：舌战规则参数 —— 直接读游戏剧本参数（对应单挑的 Game/Duel/DuelRules.cs）
 *
 * 为什么单独放一个静态类：与单挑同理，舌战里也有一部分判定拿不到 Debate 实例，
 * 这些地方统一走本类读剧本参数，保证"实例路径"和"静态路径"用的是同一份规则。
 *
 * 对应关系（原 C++ → 现在的来源）：
 *   System::is_feat_disabled(Feature_DebateCritical)
 *       → 剧本参数 ScenarioVariables.debateDisableCritical
 *         （禁用后舌战不再进入会心阶段，胜负方式固定为 Normal）
 */

namespace Sango.Core.Debate
{
    /// <summary>舌战规则参数（统一从剧本参数读取）</summary>
    public static class DebateRules
    {
        /// <summary>当前剧本参数；未载入剧本时为 null</summary>
        public static ScenarioVariables Variables
        {
            get { return Scenario.Cur != null ? Scenario.Cur.Variables : null; }
        }

        /// <summary>
        /// 功能是否被禁用。对应 C++ System::is_feat_disabled。
        /// 目前只有"禁止会心"一个开关（DebateEnum 的 Feature 枚举里也只登记了这一项）。
        /// </summary>
        public static bool IsFeatDisabled(Feature feature)
        {
            ScenarioVariables v = Variables;
            if (v == null) return false;
            switch (feature)
            {
                case Feature.Feature_DebateCritical: return v.debateDisableCritical;
            }
            return false;
        }

        /// <summary>外交交涉失败后强制进入舌战的概率（百分比，0 = 不触发）</summary>
        public static int GetDiplomacyFailChance()
        {
            ScenarioVariables v = Variables;
            return v != null ? v.debateChanceWhenDiplomacyFail : 20;
        }

        /// <summary>招募(登用)失败后强制进入舌战的概率（百分比，0 = 不触发）</summary>
        public static int GetRecruitFailChance()
        {
            ScenarioVariables v = Variables;
            return v != null ? v.debateChanceWhenRecruitFail : 30;
        }

        /// <summary>是否允许 AI 之间触发舌战（AI 之间的舌战只在后台结算，不弹界面）</summary>
        public static bool AllowAIVsAI()
        {
            ScenarioVariables v = Variables;
            return v == null || v.debateAllowAIVsAI;
        }
    }
}
