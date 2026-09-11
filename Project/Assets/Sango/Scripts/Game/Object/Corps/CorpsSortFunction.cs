using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using Unity.VisualScripting;

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
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
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
            name = "名字",
            width = 7.00f,
            valueStrGetCall = x => x.ForceNumberName,
            valueSortFunc = (a, b) => a.ForceNumberName.CompareTo(b.ForceNumberName),
            valueObjGet = x => x.ForceNumberName,
        };

        /// <summary>
        /// 军团番号排序标题（显示并修改军团编号number）
        /// 军团编号范围为1~50,与Corps.numberTxt/Corps.colors支持上限保持一致
        /// </summary>
        public static SortTitle SortByNumber = new SortTitle()
        {
            name = "军团",
            width = 5.00f,
            valueStrGetCall = x => $"{x.mBelongForce.ColorName}第{x.number}军团",
            valueSortFunc = (a, b) => a.number.CompareTo(b.number),
            valueObjGet = x => x.number,
            valueObjSet = (x, v) => x.number = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 50,
        };

        public static SortTitle SortByLeader = new SortTitle()
        {
            name = "都督",
            width = 2.80f,
            valueStrGetCall = x => x.mComander?.Name ?? "---",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mComander, b.mComander),
            valueObjGet = x => x.mComander,
            valueObjSet = (x, v) => x.mComander = (Person)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Person,
        };

        /// <summary>
        /// 军团委任方针下拉选项(与Corps.AppointType枚举语义一一对应:0无方针/1攻略势力/2攻略都市/3委任)
        /// </summary>
        static readonly List<DataEditOption> CorpsAppointOptions = new List<DataEditOption>
        {
            new DataEditOption("无方针", (int)Corps.AppointType.None),
            new DataEditOption("攻略势力", (int)Corps.AppointType.DestroyForce),
            new DataEditOption("攻略都市", (int)Corps.AppointType.OccupyCity),
            new DataEditOption("委任", (int)Corps.AppointType.Auto),
        };

        /// <summary>
        /// 委任方针排序标题（显示并修改军团委任方针appoint）
        /// appoint取值对应Corps.AppointType:None=0无方针/DestroyForce=1攻略势力/OccupyCity=2攻略都市/Auto=3委任
        /// </summary>
        public static SortTitle SortByAppoint = new SortTitle()
        {
            name = "方针",
            width = 4.00f,
            valueStrGetCall = GetAppointName,
            valueSortFunc = (a, b) => a.appoint.CompareTo(b.appoint),
            valueObjGet = x => x.appoint,
            valueObjSet = (x, v) => x.appoint = (int)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Custom,
            customData = CorpsAppointOptions,
        };

        /// <summary>
        /// 方针目标排序标题（攻略势力显示势力名,攻略都市显示城池名,只读展示）
        /// 方针目标类型随appoint变化(攻略势力为势力ID/攻占城池为城池ID),无法用固定下拉数据集表达,
        /// 需要修改目标时请先通过方针列切换方针,再在军团方针窗口(UICorpsSetting)中选择目标
        /// </summary>
        public static SortTitle SortByAppointTarget = new SortTitle()
        {
            name = "方针目标",
            width = 6.00f,
            valueStrGetCall = GetAppointTargetName,
            valueSortFunc = (a, b) => SangoObject.Compare(GetAppointTargetObj(a), GetAppointTargetObj(b)),
            valueObjGet = GetAppointTargetObj,
            valueObjSet = null,
        };

        // ==================== 排序标题辅助方法 ====================

        /// <summary>
        /// 获取军团委任方针显示文本(与Corps.AppointType语义一致)
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>方针文本</returns>
        static string GetAppointName(Corps corps)
        {
            switch (corps.appoint)
            {
                case (int)Corps.AppointType.DestroyForce: return "攻略势力";
                case (int)Corps.AppointType.OccupyCity: return "攻略都市";
                case (int)Corps.AppointType.Auto: return "委任";
                default: return "无方针";
            }
        }

        /// <summary>
        /// 获取军团方针目标对象(攻略势力为势力对象,攻略都市为城池对象,其余方针返回null)
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>目标对象,无目标时返回null</returns>
        static SangoObject GetAppointTargetObj(Corps corps)
        {
            if (corps == null || corps.appoint_target <= 0) return null;
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return null;
            switch (corps.appoint)
            {
                case (int)Corps.AppointType.DestroyForce:
                    return scenario.forceSet.Get(corps.appoint_target);
                case (int)Corps.AppointType.OccupyCity:
                    return scenario.citySet.Get(corps.appoint_target);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 获取军团方针目标显示文本(攻略势力显示势力名,攻略都市显示城池名)
        /// 无方针或目标ID未指定时显示"—",指定了ID但取不到对象(如目标势力已消亡)时显示"未指定"
        /// </summary>
        /// <param name="corps">军团对象</param>
        /// <returns>目标显示文本</returns>
        static string GetAppointTargetName(Corps corps)
        {
            SangoObject target = GetAppointTargetObj(corps);
            if (target != null) return target.Name;
            if (corps == null || corps.appoint_target <= 0) return "—";
            return "未指定";
        }

        public static SortTitle SortByBelongForce = new SortTitle()
        {
            name = "势力",
            width = 4.20f,
            valueStrGetCall = x => x.mBelongForce?.Name ?? "",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongForce, b.mBelongForce),
            valueObjGet = x => x.mBelongForce,
            valueObjSet = (x, v) => x.mBelongForce = (Force)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Force,
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
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByFood = new SortTitle()
        {
            name = "粮食",
            width = 4.00f,
            valueStrGetCall = x => x.food.ToString(),
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjGet = x => x.food,
            valueObjSet = (x, v) => x.food = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByTroop = new SortTitle()
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
