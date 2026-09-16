using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 建筑类型排序功能类，提供建筑类型对象的各种排序字段定义
    /// </summary>
    public class BuildingTypeSortFunction : Singleton<BuildingTypeSortFunction>
    {
        /// <summary>
        /// 获取建筑类型对象显示字符串的代理
        /// </summary>
        /// <param name="buildingType">建筑类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string BuildingTypeValueStrGet(BuildingType buildingType);

        /// <summary>
        /// 建筑类型对象排序比较的代理
        /// </summary>
        /// <param name="buildingType1">建筑类型对象1</param>
        /// <param name="buildingType2">建筑类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int BuildingTypeSortFunc(BuildingType buildingType1, BuildingType buildingType2);

        /// <summary>
        /// 获取建筑类型对象属性值的object类型代理
        /// </summary>
        /// <param name="buildingType">建筑类型对象</param>
        /// <returns>属性值</returns>
        public delegate object BuildingTypeValueObjGet(BuildingType buildingType);

        /// <summary>
        /// 设置建筑类型对象属性值的代理
        /// </summary>
        /// <param name="buildingType">建筑类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void BuildingTypeValueObjSet(BuildingType buildingType, object value);

        /// <summary>
        /// 建筑类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public BuildingTypeValueStrGet valueStrGetCall;
            public BuildingTypeSortFunc valueSortFunc;
            public BuildingTypeValueObjGet valueObjGet;
            public BuildingTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((BuildingType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((BuildingType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((BuildingType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((BuildingType)a, (BuildingType)b);
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
            name = "建筑",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按说明排序
        /// </summary>
        public static SortTitle SortByDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            valueStrGetCall = x => x.desc,
            valueSortFunc = (a, b) => string.Compare(a.desc, b.desc, System.StringComparison.Ordinal),
            valueObjGet = x => x.desc,
            valueObjSet = (x, v) => x.desc = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按类型排序
        /// </summary>
        public static SortTitle SortByKind = new SortTitle()
        {
            name = "类型",
            width = 2.00f,
            valueStrGetCall = x => x.kind.ToString(),
            valueSortFunc = (a, b) => a.kind.CompareTo(b.kind),
            valueObjGet = x => x.kind,
            valueObjSet = (x, v) => x.kind = System.Convert.ToByte(v),
            editType = DataEditType.IntCalculator,
            minValue = 0,
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
        /// 按耐久上限排序
        /// </summary>
        public static SortTitle SortByDurabilityLimit = new SortTitle()
        {
            name = "耐久上限",
            width = 2.40f,
            valueStrGetCall = x => x.durabilityLimit.ToString(),
            valueSortFunc = (a, b) => a.durabilityLimit.CompareTo(b.durabilityLimit),
            valueObjGet = x => x.durabilityLimit,
            valueObjSet = (x, v) => x.durabilityLimit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按造价排序
        /// </summary>
        public static SortTitle SortByCost = new SortTitle()
        {
            name = "造价",
            width = 2.40f,
            valueStrGetCall = x => x.cost.ToString(),
            valueSortFunc = (a, b) => a.cost.CompareTo(b.cost),
            valueObjGet = x => x.cost,
            valueObjSet = (x, v) => x.cost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按金钱收益排序
        /// </summary>
        public static SortTitle SortByGoldGain = new SortTitle()
        {
            name = "金钱收益",
            width = 2.40f,
            valueStrGetCall = x => x.goldGain.ToString(),
            valueSortFunc = (a, b) => a.goldGain.CompareTo(b.goldGain),
            valueObjGet = x => x.goldGain,
            valueObjSet = (x, v) => x.goldGain = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按粮食收益排序
        /// </summary>
        public static SortTitle SortByFoodGain = new SortTitle()
        {
            name = "粮食收益",
            width = 2.40f,
            valueStrGetCall = x => x.foodGain.ToString(),
            valueSortFunc = (a, b) => a.foodGain.CompareTo(b.foodGain),
            valueObjGet = x => x.foodGain,
            valueObjSet = (x, v) => x.foodGain = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByKind,
            SortByLevel,
            SortByCost,
        };
    }
}
