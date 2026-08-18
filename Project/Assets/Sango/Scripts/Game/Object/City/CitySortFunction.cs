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

       


        public static SortTitle SortByName = new SortTitle()
        {
            name = "城池",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
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

        public static SortTitle SortByTroops = new SortTitle()
        {
            name = "士兵",
            width = 4.00f,
            valueStrGetCall = x => x.troops.ToString(),
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjGet = x => x.troops,
            valueObjSet = (x, v) => x.troops = (int)v,
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

        public static SortTitle SortByFood = new SortTitle()
        {
            name = "兵粮",
            width = 4.00f,
            valueStrGetCall = x => x.food.ToString(),
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjGet = x => x.food,
            valueObjSet = (x, v) => x.food = (int)v,
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
        };

        public static SortTitle SortByBelongCorps = new SortTitle()
        {
            name = "军团",
            width = 4.00f,
            valueStrGetCall = x => x.mBelongCorps?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCorps, b.mBelongCorps),
            valueObjGet = x => x.mBelongCorps,
            valueObjSet = (x, v) => x.mBelongCorps = (Corps)v,
        };

        public static SortTitle SortByBelongCity = new SortTitle()
        {
            name = "所属",
            width = 2.40f,
            valueStrGetCall = x => x.mBelongCity?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCity, b.mBelongCity),
            valueObjGet = x => x.mBelongCity,
            valueObjSet = (x, v) => x.mBelongCity = (City)v,
        };

        public static SortTitle SortBySecurity = new SortTitle()
        {
            name = "治安",
            width = 2.40f,
            valueStrGetCall = x => x.security.ToString(),
            valueSortFunc = (a, b) => a.security.CompareTo(b.security),
            valueObjGet = x => x.security,
            valueObjSet = (x, v) => x.security = (int)v,
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
        };

        public static SortTitle SortByTotalGainFood = new SortTitle()
        {
            name = "兵粮收入",
            width = 2.40f,
            valueStrGetCall = x => x.totalGainFood.ToString(),
            valueSortFunc = (a, b) => a.totalGainFood.CompareTo(b.totalGainFood),
            valueObjGet = x => x.totalGainFood,
            valueObjSet = (x, v) => x.totalGainFood = (int)v,
        };

        public static SortTitle SortByHasBusiness = new SortTitle()
        {
            name = "市价",
            width = 2.40f,
            valueStrGetCall = x => x.hasBusiness > 0 ? $"兵粮{x.hasBusiness}=资金1" : "无商人",
            valueSortFunc = (a, b) => a.hasBusiness.CompareTo(b.hasBusiness),
            valueObjGet = x => x.hasBusiness,
            valueObjSet = (x, v) => x.hasBusiness = (byte)(int)v,
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
