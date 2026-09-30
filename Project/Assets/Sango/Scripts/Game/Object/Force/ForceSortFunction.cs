using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
   
    public enum ForceSortGroupType : int
    {
        //自定义,功能独有
        Custom = 0,
        //状态
        State,
        //战力
        FightPower,
        //兵装
        Item,
        //资金
        Gold,
        //兵粮
        Food,
        //灾害
        Disaster,

        Max
    }

    public class ForceSortFunction : Singleton<ForceSortFunction>
    {
        public delegate string ForceValueStrGet(Force force);
        public delegate int ForceValueGet(Force force);
        public delegate int ForceSortFunc(Force force1, Force force2);

        /// <summary>
        /// 获取Force对象属性值的object类型代理
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>属性值</returns>
        public delegate object ForceValueObjGet(Force force);

        /// <summary>
        /// 设置Force对象属性值的代理
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void ForceValueObjSet(Force force, object value);

        public Force CurForce;

        public class SortTitle : ObjectSortTitle
        {
            public ForceValueStrGet valueStrGetCall;
            public ForceSortFunc valueSortFunc;
            public ForceValueObjGet valueObjGet;
            public ForceValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Force)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Force)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Force)obj);
            } 

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Force)a, (Force)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueStrGetCall = valueStrGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        /// <summary>
        /// 生成"表卡"（分组标签页）对应的列集合。
        ///
        /// 每组第一列固定是势力名：列表控件把第 0 列当作行名（见 UIObjectDisplay.UpdateSortContent
        /// 里 i==0 走 listItem.textItem），其余列才是数据列，所以每组都必须以 SortByName 开头。
        ///
        /// 数值列一律走"按归属现算"的聚合口径，理由见下方 Force 级聚合标题的说明
        /// （不读 Force.CityList / CityCount / FightPower / PersonCount 这些回合缓存）。
        /// </summary>
        /// <param name="forceSortTileGroupType">表卡类型</param>
        /// <param name="titleList">输出：该表的列</param>
        public void GetSortTitleGroup(ForceSortGroupType forceSortTileGroupType, List<ObjectSortTitle> titleList)
        {
            switch (forceSortTileGroupType)
            {
                case ForceSortGroupType.State:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByLeader);
                        titleList.Add(SortByTitle);
                        titleList.Add(SortByCapitalCity);
                        titleList.Add(SortByFlag);
                        titleList.Add(SortByAlliance);
                        break;
                    }
                case ForceSortGroupType.FightPower:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByFightPower);
                        titleList.Add(SortByCityCount);
                        titleList.Add(SortByCityBaseCount);
                        titleList.Add(SortByPersonCount);
                        titleList.Add(SortByTroops);
                        titleList.Add(SortByTroopsLimit);
                        titleList.Add(SortByTechniquePoint);
                        titleList.Add(SortByHegemonyPoint);
                        break;
                    }
                case ForceSortGroupType.Item:
                    {
                        titleList.Add(SortByName);
                        // 兵装按剧本的道具类型逐类开列（与城池信息面板 UICityInfoPanel 的兵装列同一套列口径），
                        // 每列数值是该势力名下所有城池该库类的库存合计，详见 CreateItemStoreColumns。
                        foreach (SortTitle itemTitle in CreateItemStoreColumns())
                            titleList.Add(itemTitle);
                        break;
                    }
                case ForceSortGroupType.Gold:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByGold);
                        titleList.Add(SortByGoldLimit);
                        titleList.Add(SortByGoldGain);
                        break;
                    }
                case ForceSortGroupType.Food:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByFood);
                        titleList.Add(SortByFoodLimit);
                        titleList.Add(SortByFoodGain);
                        break;
                    }
                case ForceSortGroupType.Disaster:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByFireCount);
                        titleList.Add(SortByBurningCityCount);
                        titleList.Add(SortByAverageSecurity);
                        titleList.Add(SortByAverageDurability);
                        break;
                    }
            }
        }

        public string GetSortTitleGroupName(ForceSortGroupType forceSortTileGroupType)
        {
            switch (forceSortTileGroupType)
            {
                case ForceSortGroupType.State: return "状态";
                case ForceSortGroupType.FightPower: return "战力";
                case ForceSortGroupType.Item: return "兵装";
                case ForceSortGroupType.Gold: return "资金";
                case ForceSortGroupType.Food: return "兵粮";
                case ForceSortGroupType.Disaster: return "灾害";
            }

            return "";
        }
        public static SortTitle SortById = new SortTitle()
        {
            name = "编号",
            width = 2.5f,
            valueStrGetCall = x => x.Id.ToString(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = x => x.Id,
            valueObjSet = (x, v) => x.Id = (int)v,
        };

        public static SortTitle SortByName = new SortTitle()
        {
            name = "势力",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByLeader = new SortTitle()
        {
            name = "主公",
            width = 4.00f,
            valueStrGetCall = x => x.mGovernor?.Name ?? "---",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mGovernor, b.mGovernor),
            valueObjGet = x => x.mGovernor,
            valueObjSet = (x, v) => x.mGovernor = (Person)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Person,
        };


        public static SortTitle GetSortByDistanceDay(City where)
        {
            return new SortTitle()
            {
                name = "期间",
                width = 2.00f,
                valueStrGetCall = x => $"{x.mGovernor.DistanceDays(where)}0日",
                valueSortFunc = (a, b) => a.mGovernor.DistanceDays(where).CompareTo(b.mGovernor.DistanceDays(where)),
                valueObjGet = x => x.mGovernor.DistanceDays(where),
                valueObjSet = null,
            };
        }

        /// <summary>
        /// 首都排序标题（首都由君主所在城市决定，为只读派生值，不支持直接编辑）
        /// </summary>
        public static SortTitle SortByCapitalCity = new SortTitle()
        {
            name = "首都",
            width = 3.00f,
            valueStrGetCall = x => x == null || x.CapitalCity == null ? "—" : x.CapitalCity.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.CapitalCity, b.CapitalCity),
            valueObjGet = x => x == null ? null : x.CapitalCity,
            valueObjSet = null,
        };

        /// <summary>
        /// 旗帜排序标题（显示势力旗帜名，下拉从剧本旗帜集合中选值修改）
        /// </summary>
        public static SortTitle SortByFlag = new SortTitle()
        {
            name = "旗帜",
            width = 2.60f,
            valueStrGetCall = x => x == null || x.mFlag == null ? "—" : x.mFlag.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.mFlag, b.mFlag),
            valueObjGet = x => x == null ? null : x.mFlag,
            valueObjSet = (x, v) =>
            {
                if (x == null) return;
                Flag flag = v as Flag;
                x.mFlag = flag;
                // 旗帜运行时对象引用与持久化字段需同步更新，否则保存剧本时会丢失修改
                x.Flag = flag == null ? 0 : flag.Id;
            },
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Flag,
        };

        /// <summary>
        /// 爵位排序标题（显示势力爵位名，下拉从剧本爵位集合中选值修改）
        /// </summary>
        public static SortTitle SortByTitle = new SortTitle()
        {
            name = "爵位",
            width = 3.00f,
            valueStrGetCall = x => x == null || x.Title == null ? "—" : x.Title.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Title, b.Title),
            valueObjGet = x => x == null ? null : x.Title,
            valueObjSet = (x, v) =>
            {
                if (x == null) return;
                x.Title = v as Title;
            },
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Title,
        };

        /// <summary>
        /// 联盟排序标题（显示与各势力的同盟/停战/通商关系文本，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByAlliance = new SortTitle()
        {
            name = "联盟",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetAllianceText(x),
            valueSortFunc = (a, b) =>
            {
                int aCount = a == null || a.AllianceList == null ? 0 : a.AllianceList.Count;
                int bCount = b == null || b.AllianceList == null ? 0 : b.AllianceList.Count;
                return aCount.CompareTo(bCount);
            },
            valueObjGet = x => x == null ? null : x.AllianceList,
            valueObjSet = null,
        };

        /// <summary>
        /// 科技树排序标题（显示势力初始开放的科技树列表，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortBInitTechniques = new SortTitle()
        {
            name = "科技树",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetTechniqueListText(x == null ? null : x.InitTechniques),
            valueSortFunc = (a, b) => CompareTechniqueCount(a == null ? null : a.InitTechniques, b == null ? null : b.InitTechniques),
            valueObjGet = x => x == null ? null : x.InitTechniques,
            valueObjSet = null,
        };

        /// <summary>
        /// 科技排序标题（显示势力已掌握的科技列表，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByTechniques = new SortTitle()
        {
            name = "科技",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetTechniqueListText(x == null ? null : x.Techniques),
            valueSortFunc = (a, b) => CompareTechniqueCount(a == null ? null : a.Techniques, b == null ? null : b.Techniques),
            valueObjGet = x => x == null ? null : x.Techniques,
            valueObjSet = null,
        };

        /// <summary>
        /// 技巧点排序标题（技巧点只能通过游戏逻辑增减，不支持直接编辑，仅展示）
        /// </summary>
        public static SortTitle SortByTechniquePoint = new SortTitle()
        {
            name = "技巧点",
            width = 2.00f,
            valueStrGetCall = x => x.TechniquePoint.ToString(),
            valueSortFunc = (a, b) => a.TechniquePoint.CompareTo(b.TechniquePoint),
            valueObjGet = x => x.TechniquePoint,
            valueObjSet = null,
        };

        /// <summary>
        /// 霸业点排序标题（按霸业点数值显示与排序，点击后通过数字计算器修改）
        /// </summary>
        public static SortTitle SortByHegemonyPoint = new SortTitle()
        {
            name = "霸业点",
            width = 2.00f,
            valueStrGetCall = x => x.HegemonyPoint.ToString(),
            valueSortFunc = (a, b) => a.HegemonyPoint.CompareTo(b.HegemonyPoint),
            valueObjGet = x => x.HegemonyPoint,
            valueObjSet = (x, v) => x.HegemonyPoint = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 方针排序标题（按方针数值显示与排序，方针选项语义未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByPolicyType = new SortTitle()
        {
            name = "方针",
            width = 2.00f,
            valueStrGetCall = x => x.PolicyType.ToString(),
            valueSortFunc = (a, b) => a.PolicyType.CompareTo(b.PolicyType),
            valueObjGet = x => x.PolicyType,
            valueObjSet = null,
        };

        /// <summary>
        /// 国库排序标题（显示国库兵装等道具内容，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByStroe = new SortTitle()
        {
            name = "国库",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetItemStoreText(x == null ? null : x.Stroe),
            valueSortFunc = (a, b) =>
            {
                int aCount = a == null || a.Stroe == null ? 0 : a.Stroe.TotalNumber;
                int bCount = b == null || b.Stroe == null ? 0 : b.Stroe.TotalNumber;
                return aCount.CompareTo(bCount);
            },
            valueObjGet = x => x == null ? null : x.Stroe,
            valueObjSet = null,
        };

        // ==================== 表卡数值列（势力级聚合） ====================
        //
        // 下面这一组列都是"按归属现算"：当场遍历 scenario.citySet / personSet / fireSet 聚合。
        // 刻意不读 Force.CityList / CityCount / CityBaseCount / FightPower / PersonCount ——
        // 那几个是 Force.UpdateTurnInfo 在**该势力自己回合开始**时才刷新的缓存（见 Force.cs 的 UpdateTurnInfo），
        // 中途参加时目标势力可能整轮还没轮到，剧本编辑流程里更是根本不推进回合，
        // 直接读缓存会显示成"0 城 / 国力 0"这种误导信息。
        // 聚合口径与势力详情面板 UIForceInformation.Show 一致（它同样当场遍历 citySet / personSet 聚合），
        // 所以列表里的数字和玩家点开的详情页对得上。
        // 国力列沿用 City.FightPower：城池战斗力公式只有 City.UpdateFightPower 一份，这里只做求和，不另算一份。

        /// <summary>
        /// 国力排序标题（该势力所有城池战斗力之和，与 Force.UpdateTurnInfo 里 FightPower += c.FightPower 同一口径）
        /// </summary>
        public static SortTitle SortByFightPower = new SortTitle()
        {
            name = "国力",
            width = 2.40f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.FightPower).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.FightPower).CompareTo(SumCityValue(b, city => city.FightPower)),
            valueObjGet = x => SumCityValue(x, city => city.FightPower),
            valueObjSet = null,
        };

        /// <summary>
        /// 都市数量排序标题（只统计都市，不含城寨与关卡）
        /// </summary>
        public static SortTitle SortByCityCount = new SortTitle()
        {
            name = "都市",
            width = 2.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => CountCity(x, true).ToString(),
            valueSortFunc = (a, b) => CountCity(a, true).CompareTo(CountCity(b, true)),
            valueObjGet = x => CountCity(x, true),
            valueObjSet = null,
        };

        /// <summary>
        /// 城寨数量排序标题（都市以外的城池：城寨 / 关卡，口径同 Force.CityBaseCount - Force.CityCount）
        /// </summary>
        public static SortTitle SortByCityBaseCount = new SortTitle()
        {
            name = "城寨",
            width = 2.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => CountCity(x, false).ToString(),
            valueSortFunc = (a, b) => CountCity(a, false).CompareTo(CountCity(b, false)),
            valueObjGet = x => CountCity(x, false),
            valueObjSet = null,
        };

        /// <summary>
        /// 武将数量排序标题（按归属现算，不读 Force.PersonCount 回合缓存）
        /// </summary>
        public static SortTitle SortByPersonCount = new SortTitle()
        {
            name = "武将",
            width = 2.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => CountPerson(x).ToString(),
            valueSortFunc = (a, b) => CountPerson(a).CompareTo(CountPerson(b)),
            valueObjGet = x => CountPerson(x),
            valueObjSet = null,
        };

        /// <summary>
        /// 兵力排序标题（该势力所有城池的驻军合计）
        /// </summary>
        public static SortTitle SortByTroops = new SortTitle()
        {
            name = "兵力",
            width = 2.40f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.troops).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.troops).CompareTo(SumCityValue(b, city => city.troops)),
            valueObjGet = x => SumCityValue(x, city => city.troops),
            valueObjSet = null,
        };

        /// <summary>
        /// 兵力上限排序标题（该势力所有城池可容纳兵力合计，读只读计算属性 City.TroopsLimit）
        /// </summary>
        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "兵力上限",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.TroopsLimit).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.TroopsLimit).CompareTo(SumCityValue(b, city => city.TroopsLimit)),
            valueObjGet = x => SumCityValue(x, city => city.TroopsLimit),
            valueObjSet = null,
        };

        /// <summary>
        /// 资金排序标题（该势力所有城池的资金合计）
        /// </summary>
        public static SortTitle SortByGold = new SortTitle()
        {
            name = "资金",
            width = 2.40f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.gold).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.gold).CompareTo(SumCityValue(b, city => city.gold)),
            valueObjGet = x => SumCityValue(x, city => city.gold),
            valueObjSet = null,
        };

        /// <summary>
        /// 资金上限排序标题（该势力所有城池金库容量合计，读只读计算属性 City.GoldLimit）
        /// </summary>
        public static SortTitle SortByGoldLimit = new SortTitle()
        {
            name = "资金上限",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.GoldLimit).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.GoldLimit).CompareTo(SumCityValue(b, city => city.GoldLimit)),
            valueObjGet = x => SumCityValue(x, city => city.GoldLimit),
            valueObjSet = null,
        };

        /// <summary>
        /// 资金收入排序标题（该势力所有城池每回合产出合计，与势力详情面板的"资金收入"同源）
        /// </summary>
        public static SortTitle SortByGoldGain = new SortTitle()
        {
            name = "资金收入",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.totalGainGold).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.totalGainGold).CompareTo(SumCityValue(b, city => city.totalGainGold)),
            valueObjGet = x => SumCityValue(x, city => city.totalGainGold),
            valueObjSet = null,
        };

        /// <summary>
        /// 兵粮排序标题（该势力所有城池的兵粮合计）
        /// </summary>
        public static SortTitle SortByFood = new SortTitle()
        {
            name = "兵粮",
            width = 2.40f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.food).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.food).CompareTo(SumCityValue(b, city => city.food)),
            valueObjGet = x => SumCityValue(x, city => city.food),
            valueObjSet = null,
        };

        /// <summary>
        /// 兵粮上限排序标题（该势力所有城池粮仓容量合计，读只读计算属性 City.FoodLimit）
        /// </summary>
        public static SortTitle SortByFoodLimit = new SortTitle()
        {
            name = "兵粮上限",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.FoodLimit).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.FoodLimit).CompareTo(SumCityValue(b, city => city.FoodLimit)),
            valueObjGet = x => SumCityValue(x, city => city.FoodLimit),
            valueObjSet = null,
        };

        /// <summary>
        /// 兵粮收入排序标题（该势力所有城池每回合产出合计，与势力详情面板的"兵粮收入"同源）
        /// </summary>
        public static SortTitle SortByFoodGain = new SortTitle()
        {
            name = "兵粮收入",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => SumCityValue(x, city => city.totalGainFood).ToString(),
            valueSortFunc = (a, b) => SumCityValue(a, city => city.totalGainFood).CompareTo(SumCityValue(b, city => city.totalGainFood)),
            valueObjGet = x => SumCityValue(x, city => city.totalGainFood),
            valueObjSet = null,
        };

        /// <summary>
        /// 火场排序标题（落在该势力城池地格上的火焰数量，一格一团火计一个）
        /// </summary>
        public static SortTitle SortByFireCount = new SortTitle()
        {
            name = "火场",
            width = 2.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => CountFires(x, false).ToString(),
            valueSortFunc = (a, b) => CountFires(a, false).CompareTo(CountFires(b, false)),
            valueObjGet = x => CountFires(x, false),
            valueObjSet = null,
        };

        /// <summary>
        /// 受灾城排序标题（有火焰的城池数量，同一座城的多团火只算一座）
        /// </summary>
        public static SortTitle SortByBurningCityCount = new SortTitle()
        {
            name = "受灾城",
            width = 2.40f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => CountFires(x, true).ToString(),
            valueSortFunc = (a, b) => CountFires(a, true).CompareTo(CountFires(b, true)),
            valueObjGet = x => CountFires(x, true),
            valueObjSet = null,
        };

        /// <summary>
        /// 平均治安排序标题（各城池治安均值，无城时显示—；治安越低越容易出事，是"灾害"表的先验指标）
        /// </summary>
        public static SortTitle SortByAverageSecurity = new SortTitle()
        {
            name = "平均治安",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => FormatAverageCityValue(x, city => city.security),
            valueSortFunc = (a, b) =>
            {
                int aCount;
                int bCount;
                // 均值口径与显示一致（无城时按 0 参与比较），out 只用来判空、这里不需要
                return AverageCityValue(a, city => city.security, out aCount).CompareTo(AverageCityValue(b, city => city.security, out bCount));
            },
            valueObjGet = x =>
            {
                int count;
                return AverageCityValue(x, city => city.security, out count);
            },
            valueObjSet = null,
        };

        /// <summary>
        /// 平均耐久排序标题（各城池耐久均值，无城时显示—；耐久低说明城防已被拆，是"灾害"表的受损指标）
        /// </summary>
        public static SortTitle SortByAverageDurability = new SortTitle()
        {
            name = "平均耐久",
            width = 4.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleRight,
            valueStrGetCall = x => FormatAverageCityValue(x, city => city.durability),
            valueSortFunc = (a, b) =>
            {
                int aCount;
                int bCount;
                return AverageCityValue(a, city => city.durability, out aCount).CompareTo(AverageCityValue(b, city => city.durability, out bCount));
            },
            valueObjGet = x =>
            {
                int count;
                return AverageCityValue(x, city => city.durability, out count);
            },
            valueObjSet = null,
        };

        // ==================== 排序标题辅助方法 ====================

        /// <summary>
        /// 汇总该势力名下所有存活城池的某个数值（表卡数值列的统一聚合入口）。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="valueOf">取单城数值的委托</param>
        /// <returns>合计值；势力、剧本或城池集合为空时返回 0</returns>
        private static int SumCityValue(Force force, System.Func<City, int> valueOf)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.citySet == null) return 0;

            int total = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force) continue;
                total += valueOf(city);
            }
            return total;
        }

        /// <summary>
        /// 统计该势力名下的城池数量。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="cityOnly">true = 只数都市；false = 只数城寨 / 关卡</param>
        /// <returns>数量</returns>
        private static int CountCity(Force force, bool cityOnly)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.citySet == null) return 0;

            int total = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force) continue;
                if (city.IsCity() != cityOnly) continue;
                total++;
            }
            return total;
        }

        /// <summary>
        /// 统计该势力名下的武将数量。
        /// 口径同势力详情面板 UIForceInformation.Show 里的 personList（按 Person.BelongForce 归属统计）。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <returns>武将数；势力、剧本或武将集合为空时返回 0</returns>
        private static int CountPerson(Force force)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.personSet == null) return 0;

            int total = 0;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.BelongForce != force) continue;
                total++;
            }
            return total;
        }

        /// <summary>
        /// 求该势力城池某个数值的平均值（四舍五入到整数）。
        /// 一次遍历同时累加总和与城池数：均值列要"总和"和"个数"两个量，
        /// 拆成 SumCityValue + CountCityTotal 会把城池集合走两遍，这里合成一遍。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="valueOf">取单城数值的委托</param>
        /// <param name="count">输出：参与平均的城池数，0 表示无城、此时平均值没有意义</param>
        /// <returns>平均值；无城时为 0</returns>
        private static int AverageCityValue(Force force, System.Func<City, int> valueOf, out int count)
        {
            count = 0;
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.citySet == null) return 0;

            int total = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force) continue;
                total += valueOf(city);
                count++;
            }

            if (count <= 0) return 0;
            return (int)System.Math.Round((double)total / count);
        }

        /// <summary>
        /// 平均值列的显示文本：无城时显示"—"，避免把"没有城池"读成"数值为 0"。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="valueOf">取单城数值的委托</param>
        /// <returns>显示文本</returns>
        private static string FormatAverageCityValue(Force force, System.Func<City, int> valueOf)
        {
            int count;
            int average = AverageCityValue(force, valueOf, out count);
            return count <= 0 ? "—" : average.ToString();
        }

        /// <summary>
        /// 汇总该势力名下所有城池在某一库类（ItemType.storeKind）上的兵装库存。
        /// 兵装实际存放在各城的 itemStore 里（建筑生产、部队补给、城池间运输都是直接读写它），
        /// 所以势力级兵装按城池聚合，口径同 UIForceInformation.Show 里的 itemStore.Add(city.itemStore)。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="storeKind">道具库类</param>
        /// <returns>库存数量</returns>
        private static int SumStoreItemCount(Force force, int storeKind)
        {
            return SumCityValue(force, city => city.itemStore == null ? 0 : city.itemStore.GetNumber(storeKind));
        }

        /// <summary>
        /// 生成"兵装"表的逐库类列（一类一列）。
        ///
        /// 列取自剧本 CommonData.ItemTypeList（ScenarioCommonData.Init 已按 store 且 validTechId&lt;=0 过滤、
        /// 按 Id 升序排好），再按 storeKind 去重：同一 storeKind 的多个道具（如冲车 / 木兽同属 6 号库）
        /// 共用一格库存，只开一列，列名用最小 Id 的那个道具名。
        /// 当前道具表口径下正好是 枪 / 戟 / 弓 / 军马 / 冲车 / 井阑 / 楼船 七列，
        /// 与城池信息面板 UICityInfoPanel 手写的兵装列（道具 Id 2,3,4,5,6,8,11）一致，
        /// 差别是这里跟着剧本数据走，不再硬编码 Id。
        /// </summary>
        /// <returns>兵装列列表；剧本或道具数据缺失时返回空列表（该表只剩势力名列）</returns>
        private static List<SortTitle> CreateItemStoreColumns()
        {
            List<SortTitle> columns = new List<SortTitle>();
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.ItemTypeList == null)
                return columns;

            List<ItemType> itemTypes = scenario.CommonData.ItemTypeList;
            List<byte> usedStoreKinds = new List<byte>();
            for (int i = 0; i < itemTypes.Count; i++)
            {
                ItemType itemType = itemTypes[i];
                if (itemType == null) continue;
                // ItemTypeList 已按 Id 升序，第一个遇到某个 storeKind 的就是本列的列名代表
                if (usedStoreKinds.Contains(itemType.storeKind)) continue;
                usedStoreKinds.Add(itemType.storeKind);

                int storeKind = itemType.storeKind;     // 闭包要捕获每轮新建的局部副本，直接用循环体内的 itemType.storeKind 会让所有列读到同一值
                columns.Add(new SortTitle()
                {
                    name = itemType.Name,
                    width = 2.00f,
                    alignment = (int)UnityEngine.TextAnchor.MiddleRight,
                    valueStrGetCall = x => SumStoreItemCount(x, storeKind).ToString(),
                    valueSortFunc = (a, b) => SumStoreItemCount(a, storeKind).CompareTo(SumStoreItemCount(b, storeKind)),
                    valueObjGet = x => SumStoreItemCount(x, storeKind),
                    valueObjSet = null,
                });
            }
            return columns;
        }

        /// <summary>
        /// 统计落在该势力城池上的火焰数量。
        /// 火焰（Fire）是当前工程里唯一的灾害对象：由火计 / 特技在地图格上点燃（见 SetFire、Fire.SpreadFire），
        /// 熄灭时自己从 fireSet 摘除（见 Fire.Clear），所以这里只需要按 IsAlive 过滤，再经
        /// fire.cell.BelongCity 回溯受灾城池、按归属归到势力。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <param name="distinctCity">true = 统计"受灾城池数"（同一座城多团火只算一座）；false = 统计着火格数</param>
        /// <returns>数量</returns>
        private static int CountFires(Force force, bool distinctCity)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.fireSet == null) return 0;

            int total = 0;
            List<City> countedCities = distinctCity ? new List<City>() : null;
            for (int i = 0; i < scenario.fireSet.Count; i++)
            {
                Fire fire = scenario.fireSet[i];
                if (fire == null || !fire.IsAlive) continue;

                Cell cell = fire.cell;
                City city = cell == null ? null : cell.BelongCity;
                if (city == null || !city.IsAlive || city.BelongForce != force) continue;

                if (distinctCity)
                {
                    // 同一座城的多团火只计一次
                    if (countedCities.Contains(city)) continue;
                    countedCities.Add(city);
                }
                total++;
            }
            return total;
        }

        /// <summary>
        /// 生成势力结盟关系显示文本（同盟/停战/通商:对方势力名，多条以顿号分隔）
        /// </summary>
        /// <param name="force">势力对象</param>
        /// <returns>结盟关系文本，无结盟时返回—</returns>
        private static string GetAllianceText(Force force)
        {
            if (force == null || force.AllianceList == null || force.AllianceList.Count == 0) return "—";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < force.AllianceList.Count; i++)
            {
                Alliance alliance = force.AllianceList[i];
                if (alliance == null || alliance.ForceList == null) continue;
                string typeName;
                switch (alliance.allianceType)
                {
                    case AllianceType.Truce: typeName = "停战"; break;
                    case AllianceType.Trade: typeName = "通商"; break;
                    default: typeName = "同盟"; break;
                }
                for (int j = 0; j < alliance.ForceList.Count; j++)
                {
                    Force other = alliance.ForceList[j];
                    if (other == null || other == force) continue;
                    if (sb.Length > 0) sb.Append("，");
                    sb.Append(typeName).Append(":").Append(other.Name);
                }
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 生成科技/科技树列表的显示文本（科技名以顿号分隔）
        /// </summary>
        /// <param name="list">科技列表</param>
        /// <returns>显示文本，空列表返回—</returns>
        private static string GetTechniqueListText(SangoObjectList<Technique> list)
        {
            if (list == null || list.Count == 0) return "—";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                Technique technique = list[i];
                if (technique == null) continue;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(technique.Name);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 比较两个科技列表的长度（用于列表列排序）
        /// </summary>
        /// <param name="a">科技列表a</param>
        /// <param name="b">科技列表b</param>
        /// <returns>比较结果</returns>
        private static int CompareTechniqueCount(SangoObjectList<Technique> a, SangoObjectList<Technique> b)
        {
            int aCount = a == null ? 0 : a.Count;
            int bCount = b == null ? 0 : b.Count;
            return aCount.CompareTo(bCount);
        }

        /// <summary>
        /// 生成道具栏显示文本（每类道具名x数量，顿号分隔）
        /// 道具栏按道具类型的storeKind存储数量，名称需到当前剧本CommonData的道具类型集中按storeKind匹配
        /// </summary>
        /// <param name="itemStore">道具栏（国库）</param>
        /// <returns>显示文本，空道具栏返回—</returns>
        private static string GetItemStoreText(ItemStore itemStore)
        {
            if (itemStore == null || itemStore.Items == null || itemStore.Items.Count == 0)
                return "—";
            Scenario scenario = Scenario.Cur;
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, int> kv in itemStore.Items)
            {
                int number = kv.Value;
                if (number <= 0) continue;
                string itemName = null;
                if (scenario != null && scenario.CommonData != null)
                {
                    ItemType itemType = scenario.CommonData.ItemTypes.Find(t => t != null && t.storeKind == kv.Key);
                    if (itemType != null) itemName = itemType.Name;
                }
                if (string.IsNullOrEmpty(itemName)) itemName = kv.Key.ToString();
                if (sb.Length > 0) sb.Append("，");
                sb.Append(itemName);
                if (number > 1) sb.Append("x").Append(number);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

    }
}
