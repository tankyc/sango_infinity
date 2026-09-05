using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 义理排序功能类，提供义理对象的各种排序字段定义
    /// </summary>
    public class ArgumentationSortFunction : Singleton<ArgumentationSortFunction>
    {
        /// <summary>
        /// 获取义理对象显示字符串的代理
        /// </summary>
        /// <param name="argumentation">义理对象</param>
        /// <returns>显示字符串</returns>
        public delegate string ArgumentationValueStrGet(Argumentation argumentation);

        /// <summary>
        /// 义理对象排序比较的代理
        /// </summary>
        /// <param name="argumentation1">义理对象1</param>
        /// <param name="argumentation2">义理对象2</param>
        /// <returns>比较结果</returns>
        public delegate int ArgumentationSortFunc(Argumentation argumentation1, Argumentation argumentation2);

        /// <summary>
        /// 获取义理对象属性值的object类型代理
        /// </summary>
        /// <param name="argumentation">义理对象</param>
        /// <returns>属性值</returns>
        public delegate object ArgumentationValueObjGet(Argumentation argumentation);

        /// <summary>
        /// 设置义理对象属性值的代理
        /// </summary>
        /// <param name="argumentation">义理对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void ArgumentationValueObjSet(Argumentation argumentation, object value);

        /// <summary>
        /// 义理排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public ArgumentationValueStrGet valueStrGetCall;
            public ArgumentationSortFunc valueSortFunc;
            public ArgumentationValueObjGet valueObjGet;
            public ArgumentationValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Argumentation)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Argumentation)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Argumentation)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Argumentation)a, (Argumentation)b);
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
            name = "义理",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
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
            valueObjSet = (x, v) => x.kind = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按对忠诚的影响排序
        /// </summary>
        public static SortTitle SortByLoyaltyAdd = new SortTitle()
        {
            name = "忠诚影响",
            width = 2.40f,
            valueStrGetCall = x => x.loyaltyAdd.ToString(),
            valueSortFunc = (a, b) => a.loyaltyAdd.CompareTo(b.loyaltyAdd),
            valueObjGet = x => x.loyaltyAdd,
            valueObjSet = (x, v) => x.loyaltyAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按内讧暴击率加成排序
        /// </summary>
        public static SortTitle SortByInfightingCriticalAdd = new SortTitle()
        {
            name = "内讧暴击",
            width = 2.40f,
            valueStrGetCall = x => x.infightingCriticalAdd.ToString(),
            valueSortFunc = (a, b) => a.infightingCriticalAdd.CompareTo(b.infightingCriticalAdd),
            valueObjGet = x => x.infightingCriticalAdd,
            valueObjSet = (x, v) => x.infightingCriticalAdd = (int)v,
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
            SortByLoyaltyAdd,
        };
    }
}
