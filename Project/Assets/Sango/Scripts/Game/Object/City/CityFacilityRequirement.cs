/*
 * 文件名：CityFacilityRequirement.cs
 * 描述：城池"最低内政设施配置"表与达标判定。
 *
 * 对齐出处：Open311（D:\open311\s11）
 *   · 最低配置表构造 —— s11_sys_scen_ai.cpp:21-44  ai_table_update
 *   · 单项取数       —— s11_sys_scen_ai.cpp:212-240 ai_table_get_gov_facility_count
 *   · Lv2/Lv3 合并   —— s11_sys_scen_ai.cpp:270-287 ai_table_sum_gov_facility_count
 *   · 势力级工坊     —— s11_sys_scen_ai.cpp:670-693 ai_table_get_min_gov_facility_count
 *   · 达标判定       —— s11_sys_scen_ai.cpp:717-767 ai_table_meets_min_gov_facility_count
 *
 * 语义：这座城是否已经"建设到位"。没到位的城会被 AI 判为尚未发展起来，
 * 从而禁止对外用兵、对外运粮、对外调人（见各命令体的调用注释）。
 *
 * 数据来源：Build/Content/Data/Common/BuildingTypes.json 中 majorType = 4 的条目，
 * 同 kind 的 Lv1/Lv2/Lv3 共享一个 kind 值（例如市场 = 32，Lv2 = 51、Lv3 = 52 都写 kind 32）。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 内政设施种类。数值与 <c>BuildingTypes.json</c> 的 <c>kind</c> 一致，
    /// 也与 Open311 的 <c>FacilityId</c> 语义一一对应（Lv2/Lv3 不单列，靠 level 区分）。
    /// </summary>
    public enum InteriorFacilityKind : int
    {
        /// <summary>市场（Lv1/2/3）—— 原版 FacilityId_Market</summary>
        Market = 32,
        /// <summary>农场（Lv1/2/3）—— 原版 FacilityId_Farm</summary>
        Farm = 33,
        /// <summary>兵舍（Lv1/2/3）—— 原版 FacilityId_Barracks</summary>
        Barracks = 34,
        /// <summary>锻冶厂（Lv1/2/3）—— 原版 FacilityId_Smith</summary>
        Smith = 35,
        /// <summary>厩舍（Lv1/2/3）—— 原版 FacilityId_Stable</summary>
        Stable = 36,
        /// <summary>工房（Lv1/2/3）—— 原版 FacilityId_Workshop</summary>
        Workshop = 37,
        /// <summary>造船厂（Lv1/2/3）—— 原版 FacilityId_Shipyard</summary>
        Shipyard = 38,
        /// <summary>造币厂 —— 原版 FacilityId_Mint</summary>
        Mint = 39,
        /// <summary>谷仓 —— 原版 FacilityId_Garner</summary>
        Garner = 40,
        /// <summary>符节台 —— 原版 FacilityId_TallyOffice</summary>
        TallyOffice = 41,
        /// <summary>军事府 —— 原版 FacilityId_MilitaryOffice</summary>
        MilitaryOffice = 42,
        /// <summary>人材府 —— 原版 FacilityId_PersonnelOffice</summary>
        PersonnelOffice = 43,
        /// <summary>外交府 —— 原版 FacilityId_DiplomaticOffice</summary>
        DiplomaticOffice = 44,
        /// <summary>计略府 —— 原版 FacilityId_StrategemOffice</summary>
        StrategemOffice = 45,
        /// <summary>炼兵所 —— 原版 FacilityId_TrainingSchool</summary>
        TrainingSchool = 46,
        /// <summary>大市场 —— 原版 FacilityId_CentralMarket</summary>
        CentralMarket = 47,
        /// <summary>鱼市 —— 原版 FacilityId_FishMarket</summary>
        FishMarket = 48,
        /// <summary>黑市 —— 原版 FacilityId_BlackMarket</summary>
        BlackMarket = 49,
        /// <summary>军屯农 —— 原版 FacilityId_MilitaryFarm</summary>
        MilitaryFarm = 50,
    }

    /// <summary>
    /// 城池最低内政设施配置的取数与判定。全部为静态方法，不持有状态。
    /// </summary>
    public static class CityFacilityRequirement
    {
        /// <summary>kind 数组长度（BuildingTypes 里内政类的最大 kind 是 50，取 64 留余量）</summary>
        public const int KindArraySize = 64;

        /// <summary>
        /// 取本城的"最低内政设施配置"。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:21-44（固定 4 项 + 特产 1 项）
        /// 与 :670-693（势力级至少 1 座工房）。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="min">输出数组（长度 <see cref="KindArraySize"/>，调用方负责清零）</param>
        public static void GetMinGovFacilityCount(City city, int[] min)
        {
            if (city == null || min == null)
                return;

            // 固定 4 项 —— :28-31
            min[(int)InteriorFacilityKind.Market] = 3;
            min[(int)InteriorFacilityKind.Farm] = 3;
            min[(int)InteriorFacilityKind.Barracks] = 1;
            min[(int)InteriorFacilityKind.Smith] = 1;

            // 按城特产追加 1 项 —— :32-43
            // 0 枪 / 1 戟 / 2 弩 / 6 大都市 不追加（原版同样不追加）
            switch (city.specialty)
            {
                case 3:     // 马
                    min[(int)InteriorFacilityKind.Stable] = 1;
                    break;
                case 4:     // 兵器
                    min[(int)InteriorFacilityKind.Workshop] = 1;
                    break;
                case 5:     // 水军
                    min[(int)InteriorFacilityKind.Shipyard] = 1;
                    break;
            }

            // 势力级：全势力一座工房都没有 → 本城必须补一座 —— :670-693
            if (city.BelongForce != null && !ForceHasFacility(city.BelongForce, (int)InteriorFacilityKind.Workshop))
                min[(int)InteriorFacilityKind.Workshop] = 1;
        }

        /// <summary>
        /// 统计本城各类内政设施数量（Lv1/Lv2/Lv3 自动合并到同一个 kind）。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:227-240（逐格取 kind 累加）
        /// 与 :270-287（把 Lv2/Lv3 的数量并回 Lv1 项）。
        /// 本项目同一 kind 的 Lv2/Lv3 共用 kind 值，因此天然合并，无需额外归并。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="count">输出数组（长度 <see cref="KindArraySize"/>，调用方负责清零）</param>
        public static void CountGovFacility(City city, int[] count)
        {
            if (city == null || count == null || city.interiorCellList == null)
                return;

            for (int i = 0; i < city.interiorCellList.Count; i++)
            {
                Cell cell = city.interiorCellList[i];
                if (cell == null || cell.building == null)
                    continue;
                Building building = cell.building as Building;
                if (building == null || !building.IsAlive || building.BuildingType == null)
                    continue;
                int kind = building.BuildingType.kind;
                if (kind < 0 || kind >= count.Length)
                    continue;
                count[kind]++;
            }
        }

        /// <summary>
        /// 本城是否已有某类内政设施（Lv 不限）。
        ///
        /// 供各类命令判断"有无训练所 / 锻冶厂"之类的加成条件。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="kind">设施种类</param>
        /// <returns>有至少 1 座返回 true</returns>
        public static bool HasGovFacility(City city, InteriorFacilityKind kind)
        {
            if (city == null || city.interiorCellList == null)
                return false;

            int[] count = new int[KindArraySize];
            CountGovFacility(city, count);
            return count[(int)kind] > 0;
        }

        /// <summary>
        /// 本城内政设施是否达到最低配置。
        ///
        /// 对齐出处：s11_sys_scen_ai.cpp:717-767 ai_table_meets_min_gov_facility_count。
        /// <para>
        /// 玩家势力走宽松两段式：非 1 号军团只查市场 / 农场，以及与本军团委任相关的
        /// 兵舍（设了"兵充实"）与锻冶（设了枪 / 戟 / 弩生产）；
        /// 最后再判一次"设施总数 ≥ 最低配置总数"。
        /// AI 势力逐项硬比对。
        /// </para>
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>是否达标</returns>
        public static bool MeetsMinGovFacilityCount(City city)
        {
            if (city == null || city.BelongForce == null)
                return false;

            int[] count = new int[KindArraySize];
            int[] min = new int[KindArraySize];
            CountGovFacility(city, count);
            GetMinGovFacilityCount(city, min);

            if (!city.BelongForce.IsPlayer)
            {
                // AI：逐项硬比对 —— :731-739
                for (int i = 0; i < count.Length; i++)
                {
                    if (count[i] < min[i])
                        return false;
                }
                return true;
            }

            // 玩家：支军团按委任项宽松判定 —— :740-756
            Corps corps = city.BelongCorps;
            if (corps != null && corps.number != 1)
            {
                if (count[(int)InteriorFacilityKind.Market] < min[(int)InteriorFacilityKind.Market])
                    return false;
                if (count[(int)InteriorFacilityKind.Farm] < min[(int)InteriorFacilityKind.Farm])
                    return false;
                // 兵充实（军团委任 Store_Troops = 1）→ 查兵舍 —— :746-750
                if (corps.GetAppointValue(Corps.AppointContentType.Store_Troops) == 1
                    && count[(int)InteriorFacilityKind.Barracks] < min[(int)InteriorFacilityKind.Barracks])
                    return false;
                // 枪 / 戟 / 弩 生产（任一被勾选）→ 查锻冶 —— :751-755
                bool needSmith =
                    corps.GetAppointValue(Corps.AppointContentType.MakeItem_Spear) == 1
                    || corps.GetAppointValue(Corps.AppointContentType.MakeItem_Halberd) == 1
                    || corps.GetAppointValue(Corps.AppointContentType.MakeItem_Crossbow) == 1;
                if (needSmith && count[(int)InteriorFacilityKind.Smith] < min[(int)InteriorFacilityKind.Smith])
                    return false;
            }

            // 总数兜底 —— :757-765
            int countSum = 0;
            int minCountSum = 0;
            for (int i = 0; i < count.Length; i++)
            {
                countSum += count[i];
                minCountSum += min[i];
            }
            return countSum >= minCountSum;
        }

        /// <summary>
        /// 势力内是否存在指定种类的内政设施。
        /// 对齐出处：s11_sys_scen_ai.cpp:674-687（遍历势力所有城找工房）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="kind">设施种类</param>
        /// <returns>是否存在</returns>
        static bool ForceHasFacility(Force force, int kind)
        {
            List<City> cityList = force.CityList;
            if (cityList == null)
                return false;

            for (int i = 0; i < cityList.Count; i++)
            {
                City city = cityList[i];
                if (city == null || !city.IsAlive || city.interiorCellList == null)
                    continue;
                for (int j = 0; j < city.interiorCellList.Count; j++)
                {
                    Cell cell = city.interiorCellList[j];
                    if (cell == null || cell.building == null)
                        continue;
                    Building building = cell.building as Building;
                    if (building == null || !building.IsAlive || building.BuildingType == null)
                        continue;
                    if (building.BuildingType.kind == kind)
                        return true;
                }
            }
            return false;
        }
    }
}
