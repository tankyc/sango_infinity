using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城市等级类型排序功能类，提供城市等级类型对象的各种排序字段定义
    /// </summary>
    public class CityLevelTypeSortFunction : Singleton<CityLevelTypeSortFunction>
    {
        /// <summary>
        /// 获取城市等级类型对象显示字符串的代理
        /// </summary>
        /// <param name="cityLevelType">城市等级类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string CityLevelTypeValueStrGet(CityLevelType cityLevelType);

        /// <summary>
        /// 城市等级类型对象排序比较的代理
        /// </summary>
        /// <param name="cityLevelType1">城市等级类型对象1</param>
        /// <param name="cityLevelType2">城市等级类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int CityLevelTypeSortFunc(CityLevelType cityLevelType1, CityLevelType cityLevelType2);

        /// <summary>
        /// 获取城市等级类型对象属性值的object类型代理
        /// </summary>
        /// <param name="cityLevelType">城市等级类型对象</param>
        /// <returns>属性值</returns>
        public delegate object CityLevelTypeValueObjGet(CityLevelType cityLevelType);

        /// <summary>
        /// 设置城市等级类型对象属性值的代理
        /// </summary>
        /// <param name="cityLevelType">城市等级类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void CityLevelTypeValueObjSet(CityLevelType cityLevelType, object value);

        /// <summary>
        /// 城市等级类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public CityLevelTypeValueStrGet valueStrGetCall;
            public CityLevelTypeSortFunc valueSortFunc;
            public CityLevelTypeValueObjGet valueObjGet;
            public CityLevelTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((CityLevelType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((CityLevelType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((CityLevelType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((CityLevelType)a, (CityLevelType)b);
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
            name = "城市等级",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按升级所需金钱排序
        /// </summary>
        public static SortTitle SortByCostGold = new SortTitle()
        {
            name = "升级金钱",
            width = 2.40f,
            valueStrGetCall = x => x.costGold.ToString(),
            valueSortFunc = (a, b) => a.costGold.CompareTo(b.costGold),
            valueObjGet = x => x.costGold,
            valueObjSet = (x, v) => x.costGold = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按升级所需技巧点排序
        /// </summary>
        public static SortTitle SortByCostTechPoint = new SortTitle()
        {
            name = "升级技巧点",
            width = 2.40f,
            valueStrGetCall = x => x.costTechPoint.ToString(),
            valueSortFunc = (a, b) => a.costTechPoint.CompareTo(b.costTechPoint),
            valueObjGet = x => x.costTechPoint,
            valueObjSet = (x, v) => x.costTechPoint = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按可容纳兵力加成排序
        /// </summary>
        public static SortTitle SortByTroopsLimitAdd = new SortTitle()
        {
            name = "兵力加成",
            width = 2.40f,
            valueStrGetCall = x => x.troopsLimitAdd.ToString(),
            valueSortFunc = (a, b) => a.troopsLimitAdd.CompareTo(b.troopsLimitAdd),
            valueObjGet = x => x.troopsLimitAdd,
            valueObjSet = (x, v) => x.troopsLimitAdd = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按金库大小加成排序
        /// </summary>
        public static SortTitle SortByGoldLimitAdd = new SortTitle()
        {
            name = "金库加成",
            width = 2.40f,
            valueStrGetCall = x => x.goldLimitAdd.ToString(),
            valueSortFunc = (a, b) => a.goldLimitAdd.CompareTo(b.goldLimitAdd),
            valueObjGet = x => x.goldLimitAdd,
            valueObjSet = (x, v) => x.goldLimitAdd = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按粮仓大小加成排序
        /// </summary>
        public static SortTitle SortByFoodLimitAdd = new SortTitle()
        {
            name = "粮仓加成",
            width = 2.40f,
            valueStrGetCall = x => x.foodLimitAdd.ToString(),
            valueSortFunc = (a, b) => a.foodLimitAdd.CompareTo(b.foodLimitAdd),
            valueObjGet = x => x.foodLimitAdd,
            valueObjSet = (x, v) => x.foodLimitAdd = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByCostGold,
            SortByCostTechPoint,
        };
    }
}
