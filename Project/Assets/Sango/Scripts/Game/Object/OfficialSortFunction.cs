using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 官职排序功能类，提供官职对象的各种排序字段定义
    /// </summary>
    public class OfficialSortFunction : Singleton<OfficialSortFunction>
    {
        /// <summary>
        /// 获取官职对象显示字符串的代理
        /// </summary>
        /// <param name="official">官职对象</param>
        /// <returns>显示字符串</returns>
        public delegate string OfficialValueStrGet(Official official);

        /// <summary>
        /// 官职对象排序比较的代理
        /// </summary>
        /// <param name="official1">官职对象1</param>
        /// <param name="official2">官职对象2</param>
        /// <returns>比较结果</returns>
        public delegate int OfficialSortFunc(Official official1, Official official2);

        /// <summary>
        /// 获取官职对象属性值的object类型代理
        /// </summary>
        /// <param name="official">官职对象</param>
        /// <returns>属性值</returns>
        public delegate object OfficialValueObjGet(Official official);

        /// <summary>
        /// 设置官职对象属性值的代理
        /// </summary>
        /// <param name="official">官职对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void OfficialValueObjSet(Official official, object value);

        /// <summary>
        /// 官职排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public OfficialValueStrGet valueStrGetCall;
            public OfficialSortFunc valueSortFunc;
            public OfficialValueObjGet valueObjGet;
            public OfficialValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Official)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Official)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Official)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Official)a, (Official)b);
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
        /// 按ID排序
        /// </summary>
        public static SortTitle SortById = new SortTitle()
        {
            name = "ID",
            width = 2.00f,
            valueStrGetCall = x => x.Id.ToString(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = x => x.Id,
            valueObjSet = null,
        };

        /// <summary>
        /// 按名称排序
        /// </summary>
        public static SortTitle SortByName = new SortTitle()
        {
            name = "官职",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按等级排序
        /// </summary>
        public static SortTitle SortByLevel = new SortTitle()
        {
            name = "等级",
            width = 2.00f,
            valueStrGetCall = x => x.level.ToString(),
            valueSortFunc = (a, b) => a.level.CompareTo(b.level),
            valueObjGet = x => x.level,
            valueObjSet = (x, v) => x.level = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按所需功绩排序
        /// </summary>
        public static SortTitle SortByMeritNeeds = new SortTitle()
        {
            name = "所需功绩",
            width = 2.40f,
            valueStrGetCall = x => x.meritNeeds.ToString(),
            valueSortFunc = (a, b) => a.meritNeeds.CompareTo(b.meritNeeds),
            valueObjGet = x => x.meritNeeds,
            valueObjSet = (x, v) => x.meritNeeds = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按指挥兵力排序
        /// </summary>
        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "指挥",
            width = 2.40f,
            valueStrGetCall = x => x.troopsLimit.ToString(),
            valueSortFunc = (a, b) => a.troopsLimit.CompareTo(b.troopsLimit),
            valueObjGet = x => x.troopsLimit,
            valueObjSet = (x, v) => x.troopsLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按费用排序
        /// </summary>
        public static SortTitle SortByCost = new SortTitle()
        {
            name = "费用",
            width = 2.40f,
            valueStrGetCall = x => x.cost.ToString(),
            valueSortFunc = (a, b) => a.cost.CompareTo(b.cost),
            valueObjGet = x => x.cost,
            valueObjSet = (x, v) => x.cost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按统率加成排序
        /// </summary>
        public static SortTitle SortByCommandAdd = new SortTitle()
        {
            name = "统率加成",
            width = 2.40f,
            valueStrGetCall = x => x.commandAdd.ToString(),
            valueSortFunc = (a, b) => a.commandAdd.CompareTo(b.commandAdd),
            valueObjGet = x => x.commandAdd,
            valueObjSet = (x, v) => x.commandAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按武力加成排序
        /// </summary>
        public static SortTitle SortByStrengthAdd = new SortTitle()
        {
            name = "武力加成",
            width = 2.40f,
            valueStrGetCall = x => x.strengthAdd.ToString(),
            valueSortFunc = (a, b) => a.strengthAdd.CompareTo(b.strengthAdd),
            valueObjGet = x => x.strengthAdd,
            valueObjSet = (x, v) => x.strengthAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按智力加成排序
        /// </summary>
        public static SortTitle SortByIntelligenceAdd = new SortTitle()
        {
            name = "智力加成",
            width = 2.40f,
            valueStrGetCall = x => x.intelligenceAdd.ToString(),
            valueSortFunc = (a, b) => a.intelligenceAdd.CompareTo(b.intelligenceAdd),
            valueObjGet = x => x.intelligenceAdd,
            valueObjSet = (x, v) => x.intelligenceAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按政治加成排序
        /// </summary>
        public static SortTitle SortByPoliticsAdd = new SortTitle()
        {
            name = "政治加成",
            width = 2.40f,
            valueStrGetCall = x => x.politicsAdd.ToString(),
            valueSortFunc = (a, b) => a.politicsAdd.CompareTo(b.politicsAdd),
            valueObjGet = x => x.politicsAdd,
            valueObjSet = (x, v) => x.politicsAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按魅力加成排序
        /// </summary>
        public static SortTitle SortByGlamourAdd = new SortTitle()
        {
            name = "魅力加成",
            width = 2.40f,
            valueStrGetCall = x => x.glamourAdd.ToString(),
            valueSortFunc = (a, b) => a.glamourAdd.CompareTo(b.glamourAdd),
            valueObjGet = x => x.glamourAdd,
            valueObjSet = (x, v) => x.glamourAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByLevel,
            SortByMeritNeeds,
            SortByTroopsLimit,
        };
    }
}
