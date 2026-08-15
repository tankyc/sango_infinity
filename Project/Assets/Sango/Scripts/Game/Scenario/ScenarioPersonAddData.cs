using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 新武将登场数据类。
    /// 存储管理新武将登场过程中产生的所有数据，
    /// 包括已登场武将、已建立新势力、已配属武将等信息。
    /// 这些数据将传递给后续功能界面使用。
    /// </summary>
    public class ScenarioPersonAddData
    {
        /// <summary>
        /// 已登场的新武将列表（从自建武将库中选择）
        /// </summary>
        public List<PersonLib> AppearedPersonLibs = new List<PersonLib>();

        /// <summary>
        /// 已建立的新势力列表
        /// </summary>
        public List<NewForceData> NewForces = new List<NewForceData>();

        /// <summary>
        /// 已配属的新武将列表（key: 武将ID, value: 所属城池）
        /// </summary>
        public Dictionary<PersonLib, int> AssignedPersons = new Dictionary<PersonLib, int>();

        /// <summary>
        /// 未配属的新武将列表
        /// </summary>
        public List<PersonLib> UnassignedPersonLibs = new List<PersonLib>();

        /// <summary>
        /// 待机新武将列表（已登场但未配属也未建立势力的武将）
        /// </summary>
        public List<PersonLib> StandbyPersonLibs = new List<PersonLib>();

        /// <summary>
        /// 
        /// </summary>
        public List<int> forceIndexList = new List<int>();

        /// <summary>
        /// 获取已登场新武将数量
        /// </summary>
        public int AppearedCount => AppearedPersonLibs.Count;

        /// <summary>
        /// 获取已建立新势力数量
        /// </summary>
        public int NewForceCount => NewForces.Count;

        /// <summary>
        /// 获取已配属新武将数量
        /// </summary>
        public int AssignedCount => AssignedPersons.Count;

        /// <summary>
        /// 获取未配属新武将数量(已登场武将数 - 已配属武将数)
        /// </summary>
        public int UnassignedCount => AppearedCount - AssignedCount;

        /// <summary>
        /// 获取待机新武将数量
        /// </summary>
        public int StandbyCount => StandbyPersonLibs.Count;

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            AppearedPersonLibs.Clear();
            NewForces.Clear();
            AssignedPersons.Clear();
            UnassignedPersonLibs.Clear();
            StandbyPersonLibs.Clear();
            forceIndexList.Clear();
        }

        /// <summary>
        /// 添加已登场武将
        /// </summary>
        public void AddAppearedPerson(PersonLib person)
        {
            if (person != null && !AppearedPersonLibs.Contains(person))
            {
                AppearedPersonLibs.Add(person);
                StandbyPersonLibs.Add(person);
            }
        }

        /// <summary>
        /// 移除已登场武将
        /// </summary>
        public void RemoveAppearedPerson(PersonLib person)
        {
            if (person != null)
            {
                AppearedPersonLibs.Remove(person);
                StandbyPersonLibs.Remove(person);
                UnassignedPersonLibs.Remove(person);
            }
        }

        /// <summary>
        /// 移除新势力
        /// </summary>
        public void RemoveNewForce(NewForceData forceData)
        {
            if (forceData != null)
            {
                NewForces.Remove(forceData);
            }
        }

        /// <summary>
        /// 配属武将到势力
        /// </summary>
        public void AssignPersonToForce(PersonLib person, int forceId)
        {
            if (person == null) return;
            if (!UnassignedPersonLibs.Contains(person))
            {
                UnassignedPersonLibs.Add(person);
            }
            StandbyPersonLibs.Remove(person);
        }

        /// <summary>
        /// 获取指定城市所属的势力（ShortForce）。
        /// 查找范围：
        /// 1. 剧本中已有势力（ShortScenario.CurSelected 或 Cur 的 forceSet，通过 city.BelongForce 匹配）；
        /// 2. 已建立的新势力（NewForces，通过主城/所属城市匹配，即使尚未写入 forceSet 也能命中）。
        /// 城市为空、无归属或找不到对应势力时返回 null。
        /// </summary>
        public ShortForce CityInForce(ShortCity city)
        {
            if (city == null) return null;

            ShortScenario scenario = ShortScenario.CurSelected != null ? ShortScenario.CurSelected : ShortScenario.Cur;

            // 1. 优先通过城市归属势力ID在剧本 forceSet 中查找
            if (scenario != null && scenario.forceSet != null && city.BelongForce != 0)
            {
                ShortForce force = scenario.forceSet.Get(city.BelongForce);
                return force;
            }

            // 2. 在已建立的新势力中按城市匹配（新势力可能尚未写入 forceSet）
            for (int i = 0; i < NewForces.Count; i++)
            {
                NewForceData newForce = NewForces[i];
                if (newForce == null) continue;

                if (newForce.CapitalCity != null && newForce.CapitalCity.Id == city.Id)
                    return GetNewForceShortForce(scenario, newForce);

                if (newForce.allCities != null)
                {
                    for (int j = 0; j < newForce.allCities.Count; j++)
                    {
                        ShortCity c = newForce.allCities[j];
                        if (c != null && c.Id == city.Id)
                            return GetNewForceShortForce(scenario, newForce);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 将 NewForceData 转为可返回的 ShortForce。
        /// 已写入剧本 forceSet 时直接取原对象，否则临时构造一个供上层读取。
        /// </summary>
        static ShortForce GetNewForceShortForce(ShortScenario scenario, NewForceData newForce)
        {
            if (scenario != null && scenario.forceSet != null && newForce.ForceId > 0)
            {
                ShortForce force = scenario.forceSet.Get(newForce.ForceId);
                return force;
            }

            ShortForce temp = new ShortForce();
            temp.Id = newForce.ForceId;
            temp.Name = newForce.ForceName;
            temp.Governor = newForce.Governor != null ? newForce.Governor.Id : 0;
            temp.Counsellor = 0;
            temp.Flag = newForce.Flag != null ? newForce.Flag.Id : 0;
            temp.desc = "新武将势力";
            return temp;
        }

        /// <summary>
        /// 查找一个未被占用的旗帜。
        /// 排除范围：
        /// 1. 已建新势力（NewForces）占用的旗帜；
        /// 2. ShortScenario.CurSelected（或 Cur）中已有势力（forceSet）占用的旗帜。
        /// 从剧本通用数据 Flags 中返回第一个空闲旗帜，无空闲则返回 null。
        /// </summary>
        public Flag FindEmptyFlag()
        {
            // 已占用旗帜 ID 集合
            HashSet<int> usedFlagIds = new HashSet<int>();

            // 1. 已建新势力占用的旗帜
            for (int i = 0; i < NewForces.Count; i++)
            {
                NewForceData force = NewForces[i];
                if (force != null && force.Flag != null)
                    usedFlagIds.Add(force.Flag.Id);
            }

            // 2. 当前选中（或当前加载）剧本中已有势力占用的旗帜
            ShortScenario scenario = ShortScenario.CurSelected != null ? ShortScenario.CurSelected : ShortScenario.Cur;
            if (scenario != null && scenario.forceSet != null)
            {
                foreach (ShortForce force in scenario.forceSet)
                {
                    if (force != null && force.Flag != 0)
                        usedFlagIds.Add(force.Flag);
                }
            }

            // 3. 从剧本通用数据的旗帜列表中查找第一个未被占用的
            ScenarioCommonData commonData = scenario != null && scenario.CommonData != null
                ? scenario.CommonData
                : GameData.Instance.ScenarioCommonData;
            if (commonData == null || commonData.Flags == null || commonData.Flags.objects == null)
                return null;

            for (int i = commonData.Flags.Count; i > 0; i--)
            {
                Flag flag = commonData.Flags[i];
                if (flag != null && !usedFlagIds.Contains(flag.Id))
                {
                    return flag;
                }
            }
            return null;
        }
    }

    /// <summary>
    /// 新势力数据类。
    /// 存储新建势力的相关信息。
    /// </summary>
    public class NewForceData
    {
        /// <summary>
        /// 新势力ID
        /// </summary>
        public int ForceId;

        /// <summary>
        /// 新势力名称
        /// </summary>
        public string ForceName;

        /// <summary>
        /// 君主武将
        /// </summary>
        public PersonLib Governor;

        /// <summary>
        /// 主城
        /// </summary>
        public ShortCity CapitalCity;

        /// <summary>
        /// 所有城池
        /// </summary>
        public List<ShortCity> allCities = new List<ShortCity>();

        /// <summary>
        /// 旗帜
        /// </summary>
        public Flag Flag;

        /// <summary>
        /// 爵位
        /// </summary>
        public Title Title;

        /// <summary>
        /// 势力颜色
        /// </summary>
        public UnityEngine.Color ForceColor;

        /// <summary>
        /// 所属武将列表
        /// </summary>
        public List<PersonLib> Persons = new List<PersonLib>();

        /// <summary>
        /// 旗帜
        /// </summary>
        public Force targetForce;

        /// <summary>
        /// 旗帜
        /// </summary>
        public Corps targetCorps;

        /// <summary>
        /// 目标对象
        /// </summary>
        public Person targetGovernor;

        public void MakeData()
        {
            if (Scenario.Cur == null) return;
            targetForce = new Force();
            targetForce.Id = ForceId;
            targetForce.Flag = Flag.Id;
            targetForce.Governor = Governor.Id;

            targetCorps = new Corps();
            targetCorps.number = 1;
            targetCorps.BelongForce = ForceId;
            targetCorps.Comander = Governor.Id;
        }
    }
}
