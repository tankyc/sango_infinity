using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 科技排序功能类，提供科技对象的各种排序字段定义
    /// </summary>
    public class TechniqueSortFunction : Singleton<TechniqueSortFunction>
    {
        /// <summary>
        /// 获取科技对象显示字符串的代理
        /// </summary>
        /// <param name="technique">科技对象</param>
        /// <returns>显示字符串</returns>
        public delegate string TechniqueValueStrGet(Technique technique);

        /// <summary>
        /// 科技对象排序比较的代理
        /// </summary>
        /// <param name="technique1">科技对象1</param>
        /// <param name="technique2">科技对象2</param>
        /// <returns>比较结果</returns>
        public delegate int TechniqueSortFunc(Technique technique1, Technique technique2);

        /// <summary>
        /// 获取科技对象属性值的object类型代理
        /// </summary>
        /// <param name="technique">科技对象</param>
        /// <returns>属性值</returns>
        public delegate object TechniqueValueObjGet(Technique technique);

        /// <summary>
        /// 设置科技对象属性值的代理
        /// </summary>
        /// <param name="technique">科技对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TechniqueValueObjSet(Technique technique, object value);

        /// <summary>
        /// 科技排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public TechniqueValueStrGet valueStrGetCall;
            public TechniqueSortFunc valueSortFunc;
            public TechniqueValueObjGet valueObjGet;
            public TechniqueValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Technique)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Technique)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Technique)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Technique)a, (Technique)b);
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
            name = "科技",
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
            width = 2.40f,
            valueStrGetCall = x => x.kind,
            valueSortFunc = (a, b) => string.Compare(a.kind, b.kind, System.StringComparison.Ordinal),
            valueObjGet = x => x.kind,
            valueObjSet = (x, v) => x.kind = (string)v,
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
        /// 按金钱消耗排序
        /// </summary>
        public static SortTitle SortByGoldCost = new SortTitle()
        {
            name = "金钱消耗",
            width = 2.40f,
            valueStrGetCall = x => x.goldCost.ToString(),
            valueSortFunc = (a, b) => a.goldCost.CompareTo(b.goldCost),
            valueObjGet = x => x.goldCost,
            valueObjSet = (x, v) => x.goldCost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按技巧点消耗排序
        /// </summary>
        public static SortTitle SortByTechPointCost = new SortTitle()
        {
            name = "技巧点消耗",
            width = 2.40f,
            valueStrGetCall = x => x.techPointCost.ToString(),
            valueSortFunc = (a, b) => a.techPointCost.CompareTo(b.techPointCost),
            valueObjGet = x => x.techPointCost,
            valueObjSet = (x, v) => x.techPointCost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按研究回合排序
        /// </summary>
        public static SortTitle SortByCounter = new SortTitle()
        {
            name = "研究回合",
            width = 2.40f,
            valueStrGetCall = x => x.counter.ToString(),
            valueSortFunc = (a, b) => a.counter.CompareTo(b.counter),
            valueObjGet = x => x.counter,
            valueObjSet = (x, v) => x.counter = (int)v,
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
            SortByKind,
            SortByLevel,
        };
    }
}
