using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 地形类型排序功能类，提供地形类型对象的各种排序字段定义
    /// </summary>
    public class TerrainTypeSortFunction : Singleton<TerrainTypeSortFunction>
    {
        /// <summary>
        /// 获取地形类型对象显示字符串的代理
        /// </summary>
        /// <param name="terrainType">地形类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string TerrainTypeValueStrGet(TerrainType terrainType);

        /// <summary>
        /// 地形类型对象排序比较的代理
        /// </summary>
        /// <param name="terrainType1">地形类型对象1</param>
        /// <param name="terrainType2">地形类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int TerrainTypeSortFunc(TerrainType terrainType1, TerrainType terrainType2);

        /// <summary>
        /// 获取地形类型对象属性值的object类型代理
        /// </summary>
        /// <param name="terrainType">地形类型对象</param>
        /// <returns>属性值</returns>
        public delegate object TerrainTypeValueObjGet(TerrainType terrainType);

        /// <summary>
        /// 设置地形类型对象属性值的代理
        /// </summary>
        /// <param name="terrainType">地形类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TerrainTypeValueObjSet(TerrainType terrainType, object value);

        /// <summary>
        /// 地形类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public TerrainTypeValueStrGet valueStrGetCall;
            public TerrainTypeSortFunc valueSortFunc;
            public TerrainTypeValueObjGet valueObjGet;
            public TerrainTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((TerrainType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((TerrainType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((TerrainType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((TerrainType)a, (TerrainType)b);
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
            name = "地形",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按粮食产量排序
        /// </summary>
        public static SortTitle SortByFoodDeposit = new SortTitle()
        {
            name = "粮产",
            width = 2.40f,
            valueStrGetCall = x => x.foodDeposit.ToString(),
            valueSortFunc = (a, b) => a.foodDeposit.CompareTo(b.foodDeposit),
            valueObjGet = x => x.foodDeposit,
            valueObjSet = (x, v) => x.foodDeposit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按金钱产量排序
        /// </summary>
        public static SortTitle SortByGoldDeposit = new SortTitle()
        {
            name = "金产",
            width = 2.40f,
            valueStrGetCall = x => x.goldDeposit.ToString(),
            valueSortFunc = (a, b) => a.goldDeposit.CompareTo(b.goldDeposit),
            valueObjGet = x => x.goldDeposit,
            valueObjSet = (x, v) => x.goldDeposit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按基础移动消耗排序
        /// </summary>
        public static SortTitle SortByBaseCost = new SortTitle()
        {
            name = "移动消耗",
            width = 2.40f,
            valueStrGetCall = x => x.baseCost.ToString(),
            valueSortFunc = (a, b) => a.baseCost.CompareTo(b.baseCost),
            valueObjGet = x => x.baseCost,
            valueObjSet = (x, v) => x.baseCost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按是否为水域排序
        /// </summary>
        public static SortTitle SortByIsWater = new SortTitle()
        {
            name = "水域",
            width = 2.00f,
            valueStrGetCall = x => x.isWater ? "是" : "否",
            valueSortFunc = (a, b) => a.isWater.CompareTo(b.isWater),
            valueObjGet = x => x.isWater,
            valueObjSet = null,
        };

        /// <summary>
        /// 按是否可建设排序
        /// </summary>
        public static SortTitle SortByCanBuild = new SortTitle()
        {
            name = "可建设",
            width = 2.00f,
            valueStrGetCall = x => x.canBuild ? "是" : "否",
            valueSortFunc = (a, b) => a.canBuild.CompareTo(b.canBuild),
            valueObjGet = x => x.canBuild,
            valueObjSet = null,
        };

        /// <summary>
        /// 按是否可通行排序
        /// </summary>
        public static SortTitle SortByMoveable = new SortTitle()
        {
            name = "可通行",
            width = 2.00f,
            valueStrGetCall = x => x.moveable ? "是" : "否",
            valueSortFunc = (a, b) => a.moveable.CompareTo(b.moveable),
            valueObjGet = x => x.moveable,
            valueObjSet = null,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByFoodDeposit,
            SortByGoldDeposit,
            SortByBaseCost,
            SortByIsWater,
        };
    }
}
