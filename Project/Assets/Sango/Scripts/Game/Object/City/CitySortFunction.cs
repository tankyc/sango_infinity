using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    public enum CitySortTileType : int
    {
        Name = 0,

    }

    public enum CitySortGroupType : int
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

    public class CitySortFunction : Singleton<CitySortFunction>
    {
        public delegate string CityValueStrGet(City city);
        public delegate int CityValueGet(City city);
        public delegate int CitySortFunc(City city1, City city2);

        /// <summary>
        /// 获取City对象属性值的object类型代理
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>属性值</returns>
        public delegate object CityValueObjGet(City city);

        /// <summary>
        /// 设置City对象属性值的代理
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void CityValueObjSet(City city, object value);

        public City CurCity;

        public class SortTitle : ObjectSortTitle
        {
            public CityValueStrGet valueStrGetCall;
            public CitySortFunc valueSortFunc;
            public CityValueObjGet valueObjGet;
            public CityValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((City)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((City)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((City)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((City)a, (City)b);
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

        public void GetSortTitleGroup(CitySortGroupType citySortTileGroupType, List<ObjectSortTitle> titleList)
        {
            switch (citySortTileGroupType)
            {
                case CitySortGroupType.State:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CitySortGroupType.FightPower:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByTroopsLimit);
                        break;
                    }
                case CitySortGroupType.Item:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CitySortGroupType.Gold:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CitySortGroupType.Food:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
                case CitySortGroupType.Disaster:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
            }
        }

        public string GetSortTitleGroupName(CitySortGroupType citySortTileGroupType)
        {
            switch (citySortTileGroupType)
            {
                case CitySortGroupType.State: return "状态";
                case CitySortGroupType.FightPower: return "战力";
                case CitySortGroupType.Item: return "兵装";
                case CitySortGroupType.Gold: return "资金";
                case CitySortGroupType.Food: return "兵粮";
                case CitySortGroupType.Disaster: return "灾害";
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
            name = "城池",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByLeader = new SortTitle()
        {
            name = "太守",
            width = 4.00f,
            valueStrGetCall = x => x.Leader?.Name ?? "---",
            valueSortFunc = (a, b) => SangoObject.Compare(a.Leader, b.Leader),
            valueObjGet = x => x.Leader,
            valueObjSet = (x, v) => x.Leader = (Person)v,
        };

        public static SortTitle SortByPersonCount = new SortTitle()
        {
            name = "现役",
            width = 2.40f,
            valueStrGetCall = x => x.allPersons.Count.ToString(),
            valueSortFunc = (a, b) => a.allPersons.Count.CompareTo(b.allPersons.Count),
            valueObjGet = x => x.allPersons.Count,
            valueObjSet = null,
        };

        /// <summary>
        /// 人口排序标题（显示并修改城池人口基础字段population）
        /// 人口上限受PopulationLimit(人口上限基础值+等级*每级人口上限)限制,此处仅编辑当前人口值
        /// </summary>
        public static SortTitle SortByPopulation = new SortTitle()
        {
            name = "人口",
            width = 4.00f,
            valueStrGetCall = x => x.population.ToString(),
            valueSortFunc = (a, b) => a.population.CompareTo(b.population),
            valueObjGet = x => x.population,
            valueObjSet = (x, v) => x.population = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 兵役人口排序标题（显示并修改城池兵役人口基础字段troopPopulation）
        /// 兵役人口不超过MaxTroopPopulation(人口*最大兵役人口比例),此处仅编辑当前值
        /// </summary>
        public static SortTitle SortByTroopPopulation = new SortTitle()
        {
            name = "兵役人口",
            width = 4.00f,
            valueStrGetCall = x => x.troopPopulation.ToString(),
            valueSortFunc = (a, b) => a.troopPopulation.CompareTo(b.troopPopulation),
            valueObjGet = x => x.troopPopulation,
            valueObjSet = (x, v) => x.troopPopulation = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByTroops = new SortTitle()
        {
            name = "士兵",
            width = 4.00f,
            valueStrGetCall = x => x.troops.ToString(),
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjGet = x => x.troops,
            valueObjSet = (x, v) => x.troops = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByBaseTroopsLimit = new SortTitle()
        {
            name = "士兵上限",
            width = 4.00f,
            valueStrGetCall = x => x.troopsLimit.ToString(),
            valueSortFunc = (a, b) => a.troopsLimit.CompareTo(b.troopsLimit),
            valueObjGet = x => x.troopsLimit,
            valueObjSet = (x, v) => x.troopsLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 10000,
        };

        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "士兵上限",
            width = 4.00f,
            valueStrGetCall = x => x.TroopsLimit.ToString(),
            valueSortFunc = (a, b) => a.TroopsLimit.CompareTo(b.TroopsLimit),
            valueObjGet = x => x.TroopsLimit,
            valueObjSet = null,
        };

        public static SortTitle SortByGold = new SortTitle()
        {
            name = "资金",
            width = 4.00f,
            valueStrGetCall = x => x.gold.ToString(),
            valueSortFunc = (a, b) => a.gold.CompareTo(b.gold),
            valueObjGet = x => x.gold,
            valueObjSet = (x, v) => x.gold = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByGoldLimit = new SortTitle()
        {
            name = "资金上限",
            width = 4.00f,
            valueStrGetCall = x => x.GoldLimit.ToString(),
            valueSortFunc = (a, b) => a.GoldLimit.CompareTo(b.GoldLimit),
            valueObjGet = x => x.GoldLimit,
            valueObjSet = null,
        };

        /// <summary>
        /// 资金上限基础值排序标题（显示并修改金库大小的基础字段goldLimit）
        /// GoldLimit为只读计算属性(基础值+等级加成+额外加成)，无法直接写入，
        /// 需要修改基础值时使用本列，写入后持久化字段goldLimit同步生效
        /// </summary>
        public static SortTitle SortByBaseGoldLimit = new SortTitle()
        {
            name = "资金上限",
            width = 4.00f,
            valueStrGetCall = x => x.goldLimit.ToString(),
            valueSortFunc = (a, b) => a.goldLimit.CompareTo(b.goldLimit),
            valueObjGet = x => x.goldLimit,
            valueObjSet = (x, v) => x.goldLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByFood = new SortTitle()
        {
            name = "兵粮",
            width = 4.00f,
            valueStrGetCall = x => x.food.ToString(),
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjGet = x => x.food,
            valueObjSet = (x, v) => x.food = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByFoodLimit = new SortTitle()
        {
            name = "兵粮上限",
            width = 4.00f,
            valueStrGetCall = x => x.FoodLimit.ToString(),
            valueSortFunc = (a, b) => a.FoodLimit.CompareTo(b.FoodLimit),
            valueObjGet = x => x.FoodLimit,
            valueObjSet = null,
        };

        /// <summary>
        /// 兵粮上限基础值排序标题（显示并修改粮仓大小的基础字段foodLimit）
        /// FoodLimit为只读计算属性(基础值+等级加成+额外加成)，无法直接写入，
        /// 需要修改基础值时使用本列，写入后持久化字段foodLimit同步生效
        /// </summary>
        public static SortTitle SortByBaseFoodLimit = new SortTitle()
        {
            name = "兵粮上限",
            width = 4.00f,
            valueStrGetCall = x => x.foodLimit.ToString(),
            valueSortFunc = (a, b) => a.foodLimit.CompareTo(b.foodLimit),
            valueObjGet = x => x.foodLimit,
            valueObjSet = (x, v) => x.foodLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 仓库上限排序标题（显示城池仓库大小总值StoreLimit,只读）
        /// StoreLimit为只读计算属性(基础值+等级加成+额外加成),需要修改基础值时请使用SortByBaseStoreLimit
        /// </summary>
        public static SortTitle SortByStoreLimit = new SortTitle()
        {
            name = "仓库上限",
            width = 4.00f,
            valueStrGetCall = x => x.StoreLimit.ToString(),
            valueSortFunc = (a, b) => a.StoreLimit.CompareTo(b.StoreLimit),
            valueObjGet = x => x.StoreLimit,
            valueObjSet = null,
        };

        /// <summary>
        /// 仓库上限基础值排序标题（显示并修改仓库大小的基础字段storeLimit）
        /// StoreLimit为只读计算属性(基础值+等级加成+额外加成)，无法直接写入,
        /// 需要修改基础值时使用本列,写入后持久化字段storeLimit同步生效
        /// </summary>
        public static SortTitle SortByBaseStoreLimit = new SortTitle()
        {
            name = "仓库上限",
            width = 4.00f,
            valueStrGetCall = x => x.storeLimit.ToString(),
            valueSortFunc = (a, b) => a.storeLimit.CompareTo(b.storeLimit),
            valueObjGet = x => x.storeLimit,
            valueObjSet = (x, v) => x.storeLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByLevel = new SortTitle()
        {
            name = "等级",
            width = 2.40f,
            valueStrGetCall = x => x.CityLevelType.Name,
            valueSortFunc = (a, b) => a.CityLevelType.Id.CompareTo(b.CityLevelType.Id),
            valueObjGet = x => x.CityLevelType,
            valueObjSet = null,
        };

        public static SortTitle SortByIsFree = new SortTitle()
        {
            name = "空闲",
            width = 2.40f,
            valueStrGetCall = x => x.freePersons.Count.ToString(),
            valueSortFunc = (a, b) => a.freePersons.Count.CompareTo(b.freePersons.Count),
            valueObjGet = x => x.freePersons.Count,
            valueObjSet = null,
        };

        public static SortTitle SortByCaptiveCount = new SortTitle()
        {
            name = "俘虏",
            width = 2.40f,
            valueStrGetCall = x => x.captiveList.Count.ToString(),
            valueSortFunc = (a, b) => a.captiveList.Count.CompareTo(b.captiveList.Count),
            valueObjGet = x => x.captiveList.Count,
            valueObjSet = null,
        };

        public static SortTitle SortByWildCount = new SortTitle()
        {
            name = "在野",
            width = 2.40f,
            valueStrGetCall = x => x.wildPersons.Count.ToString(),
            valueSortFunc = (a, b) => a.wildPersons.Count.CompareTo(b.wildPersons.Count),
            valueObjGet = x => x.wildPersons.Count,
            valueObjSet = null,
        };

        public static SortTitle SortByBelongForce = new SortTitle()
        {
            name = "势力",
            width = 2.40f,
            valueStrGetCall = x => x.mBelongForce?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongForce, b.mBelongForce),
            valueObjGet = x => x.mBelongForce,
            valueObjSet = (x, v) => x.mBelongForce = (Force)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Force,
        };

        public static SortTitle SortByBelongCorps = new SortTitle()
        {
            name = "军团",
            width = 4.00f,
            valueStrGetCall = x => x.mBelongCorps?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCorps, b.mBelongCorps),
            valueObjGet = x => x.mBelongCorps,
            valueObjSet = (x, v) => x.mBelongCorps = (Corps)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Corps,
        };

        public static SortTitle SortByBelongCity = new SortTitle()
        {
            name = "所属",
            width = 2.40f,
            valueStrGetCall = x => x.mBelongCity?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCity, b.mBelongCity),
            valueObjGet = x => x.mBelongCity,
            valueObjSet = (x, v) => x.mBelongCity = (City)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.City,
        };

        /// <summary>
        /// 州排序标题（显示并修改城池所属州province）
        /// province为Province对象引用,通过下拉数据集DataSetType.Province选择
        /// </summary>
        public static SortTitle SortByProvince = new SortTitle()
        {
            name = "州",
            width = 2.40f,
            valueStrGetCall = x => x.province?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.province, b.province),
            valueObjGet = x => x.province,
            valueObjSet = (x, v) => x.province = (Province)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Province,
        };

        /// <summary>
        /// 邻接城市数量排序标题（显示并排序城池相邻城市数量）
        /// NeighborList为邻接城市列表,由地图道路连通关系决定,内容只读
        /// </summary>
        public static SortTitle SortByNeighborCount = new SortTitle()
        {
            name = "邻接",
            width = 2.40f,
            valueStrGetCall = x => x.NeighborList.Count.ToString(),
            valueSortFunc = (a, b) => a.NeighborList.Count.CompareTo(b.NeighborList.Count),
            valueObjGet = x => x.NeighborList.Count,
            valueObjSet = null,
        };

        /// <summary>
        /// 民心排序标题（显示并修改城池民心基础字段popularSupport）
        /// popularSupport为byte类型字段,民心范围0~100,参考地图编辑器FieldIntRanges设置
        /// </summary>
        public static SortTitle SortByPopularSupport = new SortTitle()
        {
            name = "民心",
            width = 2.40f,
            valueStrGetCall = x => x.popularSupport.ToString(),
            valueSortFunc = (a, b) => a.popularSupport.CompareTo(b.popularSupport),
            valueObjGet = x => x.popularSupport,
            valueObjSet = (x, v) => x.popularSupport = (byte)(int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 100,
        };

        public static SortTitle SortBySecurity = new SortTitle()
        {
            name = "治安",
            width = 2.40f,
            valueStrGetCall = x => x.security.ToString(),
            valueSortFunc = (a, b) => a.security.CompareTo(b.security),
            valueObjGet = x => x.security,
            valueObjSet = (x, v) => x.security = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 100,
        };

        public static SortTitle SortBySecurity_SecurityLimit = new SortTitle()
        {
            name = "治安",
            width = 2.40f,
            valueStrGetCall = x => $"{x.security}/100",
            valueSortFunc = (a, b) => a.security.CompareTo(b.security),
            valueObjGet = x => $"{x.security}/100",
            valueObjSet = null,
        };


        public static SortTitle SortByDurability = new SortTitle()
        {
            name = "耐久",
            width = 2.40f,
            valueStrGetCall = x => x.durability.ToString(),
            valueSortFunc = (a, b) => a.durability.CompareTo(b.durability),
            valueObjGet = x => x.durability,
            valueObjSet = (x, v) => x.durability = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByDurability_DurabilityLimit = new SortTitle()
        {
            name = "耐久",
            width = 2.40f,
            valueStrGetCall = x => $"{x.durability}/{x.DurabilityLimit}",
            valueSortFunc = (a, b) => a.durability.CompareTo(b.durability),
            valueObjGet = x => $"{x.durability}/{x.DurabilityLimit}",
            valueObjSet = null,
        };

        /// <summary>
        /// 耐久上限基础值排序标题（显示并修改最大耐久的基础字段durabilityLimit）
        /// DurabilityLimit为只读计算属性(基础值+等级加成+额外加成)，无法直接写入，
        /// 需要修改上限基础值时使用本列，写入后持久化字段durabilityLimit同步生效
        /// </summary>
        public static SortTitle SortByBaseDurabilityLimit = new SortTitle()
        {
            name = "耐久上限",
            width = 4.00f,
            valueStrGetCall = x => x.durabilityLimit.ToString(),
            valueSortFunc = (a, b) => a.durabilityLimit.CompareTo(b.durabilityLimit),
            valueObjGet = x => x.durabilityLimit,
            valueObjSet = (x, v) => x.durabilityLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByAllPersonCountInfo = new SortTitle()
        {
            name = "现役",
            width = 2.40f,
            valueStrGetCall = x => $"{x.freePersons.Count}/{x.allPersons.Count}",
            valueSortFunc = (a, b) => a.allPersons.Count.CompareTo(b.allPersons.Count),
            valueObjGet = x => $"{x.freePersons.Count}/{x.allPersons.Count}",
            valueObjSet = null,
        };

        public static SortTitle SortByBuildingBuildCount_TotalCount = new SortTitle()
        {
            name = "设施",
            width = 4.00f,
            valueStrGetCall = x => $"{x.GetInteriorCellUsedCount()}/{x.InteriorCellCount}",
            valueSortFunc = (a, b) => a.InteriorCellCount.CompareTo(b.InteriorCellCount),
            valueObjGet = x => $"{x.GetInteriorCellUsedCount()}/{x.InteriorCellCount}",
            valueObjSet = null,
        };

        public static SortTitle SortByMorale_MoraleLimit = new SortTitle()
        {
            name = "气力",
            width = 2.40f,
            valueStrGetCall = x => $"{x.morale}/{x.MaxMorale}",
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjGet = x => $"{x.morale}/{x.MaxMorale}",
            valueObjSet = null,
        };

        public static SortTitle SortByMorale = new SortTitle()
        {
            name = "气力",
            width = 2.40f,
            valueStrGetCall = x => x.morale.ToString(),
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjGet = x => x.morale,
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 100,
        };

        public static SortTitle SortByItemStroe = new SortTitle()
        {
            name = "库存",
            width = 5.40f,
            valueStrGetCall = x => x.morale.ToString(),
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjGet = x => x.morale,
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.Object,
        };

        public static SortTitle GetSortByItemId(int id)
        {
            ItemType itemType = Scenario.Cur.GetObject<ItemType>(id);
            return new SortTitle()
            {
                name = itemType.Name,
                width = 2.00f,
                valueStrGetCall = x => x.itemStore.GetNumber(itemType).ToString(),
                valueSortFunc = (a, b) => a.itemStore.GetNumber(itemType).CompareTo(b.itemStore.GetNumber(itemType)),
                valueObjGet = x => x.itemStore.GetNumber(itemType),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortByItemId(int id, City city)
        {
            ItemType itemType = Scenario.Cur.GetObject<ItemType>(id);
            if (itemType.kind == 3 || itemType.kind == 4)
            {
                ItemType tempType = Scenario.Cur.GetObject<ItemType>(id + 1);
                if (tempType.storeKind == itemType.storeKind && tempType.IsValid(city.mBelongForce))
                {
                    itemType = tempType;
                }
            }
            return new SortTitle()
            {
                name = itemType.Name,
                width = 2.00f,
                valueStrGetCall = x => x.itemStore.GetNumber(itemType).ToString(),
                valueSortFunc = (a, b) => a.itemStore.GetNumber(itemType).CompareTo(b.itemStore.GetNumber(itemType)),
                valueObjGet = x => x.itemStore.GetNumber(itemType),
                valueObjSet = null,
            };
        }

        public static SortTitle SortByTotalGainGold = new SortTitle()
        {
            name = "资金收入",
            width = 2.40f,
            valueStrGetCall = x => x.totalGainGold.ToString(),
            valueSortFunc = (a, b) => a.totalGainGold.CompareTo(b.totalGainGold),
            valueObjGet = x => x.totalGainGold,
            valueObjSet = (x, v) => x.totalGainGold = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByTotalGainFood = new SortTitle()
        {
            name = "兵粮收入",
            width = 2.40f,
            valueStrGetCall = x => x.totalGainFood.ToString(),
            valueSortFunc = (a, b) => a.totalGainFood.CompareTo(b.totalGainFood),
            valueObjGet = x => x.totalGainFood,
            valueObjSet = (x, v) => x.totalGainFood = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByHasBusiness = new SortTitle()
        {
            name = "市价",
            width = 2.40f,
            valueStrGetCall = x => x.hasBusiness > 0 ? $"兵粮{x.hasBusiness}=资金1" : "无商人",
            valueSortFunc = (a, b) => a.hasBusiness.CompareTo(b.hasBusiness),
            valueObjGet = x => x.hasBusiness,
            valueObjSet = (x, v) => x.hasBusiness = (byte)(int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 255,
        };


        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortByName,
            SortByPersonCount,
            SortByBelongCity,
            SortByTroops,
            SortByGold,
            SortByFood,
            SortByLevel,

        };
    }
}
