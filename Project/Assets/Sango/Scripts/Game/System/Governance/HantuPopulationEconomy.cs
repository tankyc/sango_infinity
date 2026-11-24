using System;

namespace Sango.Core
{
    public static class HantuPopulationEconomy
    {
        public static int PatrolFee(int urban, int rural)
        {
            // 6.0 按城乡总人口整除十万，再加一档。每档扣 50 金。
            long population = (long)Math.Max(0, urban) + Math.Max(0, rural);
            return (int)((population / 100000 + 1) * 50);
        }

        public static int SecurityBaseline(int governorPolitics, int highestPolitics, bool hasIntimidation)
        {
            // 6.0 分别截断太守和最高政治的 10%，威压额外增加 10。
            int baseline = 75 + Math.Max(0, governorPolitics) / 10 + Math.Max(0, highestPolitics) / 10;
            return Math.Min(100, baseline + (hasIntimidation ? 10 : 0));
        }

        public static int LimitPatrolGain(int security, int baseline, int gain)
        {
            return Math.Max(0, Math.Min(gain, baseline - security));
        }
    }
}
