using System;

namespace Sango.Core
{
    public static class HantuPopulationRules
    {
        public static int FacilityPercent(int population, int peoplePerPercent)
        {
            return Math.Max(50, Math.Min(150, population / peoplePerPercent));
        }

        public static int Transfer(int source, int destination, int capacity, int requested)
        {
            return Math.Max(0, Math.Min(requested, Math.Min(source, capacity - destination)));
        }

        public static int Growth(int population, int capacity, int politics, int development,
            int security, int threshold, double coefficient, double harvestFactor)
        {
            if (population <= 0 || population >= capacity || security <= threshold)
                return 0;

            // 汉土按剩余容量放缓增长。治安、发展和人口规模决定季度增长量。
            double availableCapacity = 1d - (double)population / capacity;
            int safety = Math.Min(100, security) - threshold;
            double increase = (politics / 2 + development) * safety * coefficient
                * Math.Sqrt(population) * availableCapacity * harvestFactor;
            return (int)Math.Min(capacity - population, Math.Max(0d, increase));
        }

        public static int RecruitCost(int troops, bool player)
        {
            if (troops <= 0) return 0;
            return player ? troops : troops / 2 + troops % 2;
        }

        public static int RecruitLimit(int urban, int rural, bool player)
        {
            long total = (long)Math.Max(0, urban) + Math.Max(0, rural);
            return (int)Math.Min(int.MaxValue, player ? total : total * 2);
        }
    }
}
