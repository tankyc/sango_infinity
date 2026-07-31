using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{

    public enum CorpsSortGroupType : int
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

    public class CorpsSortFunction : Singleton<CorpsSortFunction>
    {
        public delegate string CorpsValueStrGet(Corps corps);
        public delegate int CorpsValueGet(Corps corps);
        public delegate int CorpsSortFunc(Corps corps1, Corps corps2);

        /// <summary>
        /// 获取Corps对象属性值的object类型代理
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>属性值</returns>
        public delegate object CorpsValueObjGet(Corps corps);

        /// <summary>
        /// 设置Corps对象属性值的代理
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void CorpsValueObjSet(Corps corps, object value);

        public Corps CurCorps;

        public class SortTitle : ObjectSortTitle
        {
            public CorpsValueStrGet valueStrGetCall;
            public CorpsSortFunc valueSortFunc;
            public CorpsValueObjGet valueObjGet;
            public CorpsValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Corps)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Corps)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Corps)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Corps)a, (Corps)b);
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

        public void GetSortTitleGroup(CorpsSortGroupType CorpsSortTileGroupType, List<ObjectSortTitle> titleList)
        {
            switch (CorpsSortTileGroupType)
            {
                case CorpsSortGroupType.State:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CorpsSortGroupType.FightPower:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CorpsSortGroupType.Item:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CorpsSortGroupType.Gold:
                    {
                        titleList.Add(SortByName);
                        break;
                    }
                case CorpsSortGroupType.Food:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
                case CorpsSortGroupType.Disaster:
                    {

                        titleList.Add(SortByName);
                        break;
                    }
            }
        }

        public string GetSortTitleGroupName(CorpsSortGroupType CorpsSortTileGroupType)
        {
            switch (CorpsSortTileGroupType)
            {
                case CorpsSortGroupType.State: return "状态";
                case CorpsSortGroupType.FightPower: return "战力";
                case CorpsSortGroupType.Item: return "兵装";
                case CorpsSortGroupType.Gold: return "资金";
                case CorpsSortGroupType.Food: return "兵粮";
                case CorpsSortGroupType.Disaster: return "灾害";
            }

            return "";
        }

        public static SortTitle SortByName = new SortTitle()
        {
            name = "势力",
            width = 4.00f,
            valueStrGetCall = x => x.BelongForce.Name,
            valueSortFunc = (a, b) => a.BelongForce.Name.CompareTo(b.BelongForce.Name),
            valueObjGet = x => x.BelongForce,
            valueObjSet = (x, v) => x.BelongForce = (Force)v,
        };

        public static SortTitle SortByNumber = new SortTitle()
        {
            name = "军团",
            width = 4.00f,
            valueStrGetCall = x => $"第{x.number}军团",
            valueSortFunc = (a, b) => a.number.CompareTo(b.number),
            valueObjGet = x => x.number,
            valueObjSet = (x, v) => x.number = (int)v,
        };

        public static SortTitle SortByLeader = new SortTitle()
        {
            name = "都督",
            width = 2.80f,
            valueStrGetCall = x => x.Comander?.Name ?? "---",
            valueSortFunc = (a, b) => SangoObject.Compare(a.Comander, b.Comander),
            valueObjGet = x => x.Comander,
            valueObjSet = (x, v) => x.Comander = (Person)v,
        };

        public static SortTitle SortByCityCount = new SortTitle()
        {
            name = "都市",
            width = 2.00f,
            valueStrGetCall = x => x.cityCount.ToString(),
            valueSortFunc = (a, b) => a.cityCount.CompareTo(b.cityCount),
            valueObjGet = x => x.cityCount,
            valueObjSet = null,
        };

        public static SortTitle SortByPersonCount = new SortTitle()
        {
            name = "武将",
            width = 2.00f,
            valueStrGetCall = x => x.personCount.ToString(),
            valueSortFunc = (a, b) => a.personCount.CompareTo(b.personCount),
            valueObjGet = x => x.personCount,
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

        public static SortTitle SortByFood = new SortTitle()
        {
            name = "粮食",
            width = 4.00f,
            valueStrGetCall = x => x.food.ToString(),
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjGet = x => x.food,
            valueObjSet = (x, v) => x.food = (int)v,
        };

        public static SortTitle SortByTroop = new SortTitle()
        {
            name = "士兵",
            width = 4.00f,
            valueStrGetCall = x => x.troops.ToString(),
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjGet = x => x.troops,
            valueObjSet = (x, v) => x.troops = (int)v,
        };

        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>()
        {
            SortByNumber,
            SortByLeader,
            SortByCityCount,
            SortByPersonCount,
            SortByTroop,
            SortByGold,
            SortByFood,
        };
    }
}
