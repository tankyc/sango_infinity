/*
 * 文件名：DuelRules.cs
 * 描述：单挑规则参数 —— 直接读游戏剧本参数（替代 DuelSettings 里的静态钩子）
 *
 * 为什么单独放一个静态类：
 *   单挑里有一部分判定是 static 的（例如 CalcDuelFtkTeam / GetDuelStrength），
 *   拿不到 Duel 实例，也就用不了 system.（DuelGameSystem）。
 *   这些地方统一走本类读剧本参数，保证"实例路径"和"静态路径"用的是同一份规则。
 *
 * 对应关系（原 DuelSettings 成员 → 现在的来源）：
 *   Difficulty        → 剧本参数 difficulty（0简单/1普通/2困难/3超级）映射到单挑 3 档
 *   LifeMode          → 剧本参数 duelLifeMode（0=普通 / 1=虚拟）
 *   BattleDeathMode   → 剧本参数 duelDeathMode（0=无 / 1=普通 / 2=高）
 *   IsFeatDisabled    → 剧本参数 duelDisableFirstTurnKill / duelDisableCapture / duelDisableAIRetreat
 */

namespace Sango.Core.Duel
{
    /// <summary>单挑规则参数（统一从剧本参数读取）</summary>
    public static class DuelRules
    {
        /// <summary>当前剧本参数；未载入剧本时为 null</summary>
        public static ScenarioVariables Variables
        {
            get { return Scenario.Cur != null ? Scenario.Cur.Variables : null; }
        }

        /// <summary>
        /// 难度：剧本参数 difficulty（0简单/1普通/2困难/3超级）→ 单挑的 Easy/Normal/Hard。
        /// 只影响单挑内 AI 的必杀 / 退却倾向，不会反向影响剧本难度。
        /// </summary>
        public static Difficulty GetDifficulty()
        {
            ScenarioVariables v = Variables;
            int d = v != null ? v.difficulty : 1;
            if (d <= 0) return Difficulty.Easy;
            if (d == 1) return Difficulty.Normal;
            return Difficulty.Hard;
        }

        /// <summary>寿命模式：剧本参数 duelLifeMode（0=普通 / 1=虚拟）</summary>
        public static LifeMode GetLifeMode()
        {
            ScenarioVariables v = Variables;
            return (v != null && v.duelLifeMode == 1) ? LifeMode.Virtual : LifeMode.Normal;
        }

        /// <summary>战死频率：剧本参数 duelDeathMode（0=无 / 1=普通 / 2=高）</summary>
        public static BattleDeathMode GetBattleDeathMode()
        {
            ScenarioVariables v = Variables;
            int m = v != null ? v.duelDeathMode : 1;
            if (m <= 0) return BattleDeathMode.None;
            return m >= 2 ? BattleDeathMode.High : BattleDeathMode.Normal;
        }

        /// <summary>
        /// 单挑获胜的基础抓捕率：剧本参数 captureChangceWhenDuelWin（百分比，0~100）。
        /// 未载入剧本时用默认值 70（与 ScenarioVariables 里该字段的默认值保持一致）。
        /// </summary>
        public static int GetDuelWinCaptureChance()
        {
            ScenarioVariables v = Variables;
            return v != null ? v.captureChangceWhenDuelWin : 70;
        }

        /// <summary>功能是否被禁用（一击必杀 / 捕缚 / AI退却，各对应一个剧本参数开关）</summary>
        public static bool IsFeatDisabled(Feature feature)
        {
            ScenarioVariables v = Variables;
            if (v == null) return false;
            switch (feature)
            {
                case Feature.DuelFirstTurnKill: return v.duelDisableFirstTurnKill;
                case Feature.Hobaku: return v.duelDisableCapture;
                case Feature.DuelAIRetreat: return v.duelDisableAIRetreat;
            }
            return false;
        }
    }
}
