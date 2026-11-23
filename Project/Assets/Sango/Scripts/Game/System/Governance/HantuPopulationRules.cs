using System;

namespace Sango.Core
{
    public static class HantuPopulationRules
    {
        const float GrowthCoefficient = 50f / 10000;
        const int ConversionSecurity = 90;
        const int ConversionRatio = 5;
        internal const int MigrationRollCount = 5;

        public static int FacilityPercent(int population, int peoplePerPercent)
        {
            return Math.Max(50, Math.Min(150, population / peoplePerPercent));
        }

        public static int Transfer(int source, int destination, int capacity, int requested)
        {
            return Math.Max(0, Math.Min(requested, Math.Min(source, capacity - destination)));
        }

        public static float CapacityModifier(int population, int growthCapacity)
        {
            int lower = (int)((long)growthCapacity * 2 / 100);
            int upper = (int)((long)growthCapacity * 12 / 100);
            if (population <= 0) return 0;
            if (population < lower)
                return (float)lower / (population + ((lower - population) >> 4));
            if (population > upper)
                return (float)(upper / (2d * population));
            return 1;
        }

        public static int Growth(int population, int capacity, int growthCapacity, int politics,
            int development, int security, int threshold, int harvestBonus, bool bigCity = false)
        {
            if (population <= 0 || population >= capacity || security <= threshold)
                return 0;

            int safety = Math.Min(100, security) - threshold;
            double increase = Math.Sqrt(population) * GrowthCoefficient * CapacityModifier(population, growthCapacity)
                * (politics / 2 + development) * safety;
            // 先按 FISTP 的默认舍入取整，再应用整数百分比。
            int baseIncrease = (int)Math.Round(increase, MidpointRounding.ToEven);
            if (bigCity)
                baseIncrease = Percent(baseIncrease, 120);
            return Math.Min(capacity - population, Percent(baseIncrease, 100 + harvestBonus));
        }

        public static int ConversionRollCount(int security)
        {
            return 1 + (Math.Min(20, Math.Abs(security - ConversionSecurity)) >> 2);
        }

        internal static bool CanConvertUrban(int security) => security <= ConversionSecurity;
        internal static bool CanConvertRural(int security) => security >= ConversionSecurity;

        public static int RuralConversion(int rural, int roll, int grainRate)
        {
            return (int)((long)Percent(rural, roll) * ConversionRatio / Math.Max(1, grainRate));
        }

        public static int Migration(int source, int destination, int capacity, int roll)
        {
            if (source < Percent(destination, 50)) return 0;
            return Transfer(source, destination, capacity, Percent(source, roll));
        }

        // 整数百分比向零截断，先乘后除。
        internal static int Percent(int value, int rate)
        {
            return (int)((long)value * rate / 100);
        }

        // 由季度、据点和流程派生抽样，存读档可复现且不占用战斗随机序列。
        internal static int QuarterRoll(int season, int site, int channel, int choices)
        {
            unchecked
            {
                uint seed = (uint)season * 0x9e3779b9u ^ (uint)site * 0x85ebca6bu ^ (uint)channel * 0xc2b2ae35u;
                seed = (seed ^ (seed >> 16)) * 0x7feb352du;
                seed = (seed ^ (seed >> 15)) * 0x846ca68bu;
                return 1 + (int)((seed ^ (seed >> 16)) % (uint)choices);
            }
        }

        public static int RecruitCost(int troops, bool player)
        {
            return Percent(Math.Max(0, troops), player ? 100 : 50);
        }

        public static int RuralRecruitCost(int totalCost, int grainRate)
        {
            int rate = Math.Max(0, grainRate);
            int ruralShare = (int)(100L * rate / (rate + 10L));
            return Percent(totalCost, ruralShare);
        }

        public static int RecruitLimit(int urban, int rural, bool player)
        {
            long total = (long)Math.Max(0, urban) + Math.Max(0, rural);
            if (total == 0) return 0;
            return (int)Math.Min(int.MaxValue, player ? total : total * 2 + 1);
        }
    }
}
