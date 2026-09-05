using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// Buff排序功能类，提供Buff对象的各种排序字段定义
    /// </summary>
    public class BuffSortFunction : Singleton<BuffSortFunction>
    {
        /// <summary>
        /// 获取Buff对象显示字符串的代理
        /// </summary>
        /// <param name="buff">Buff对象</param>
        /// <returns>显示字符串</returns>
        public delegate string BuffValueStrGet(Buff buff);

        /// <summary>
        /// Buff对象排序比较的代理
        /// </summary>
        /// <param name="buff1">Buff对象1</param>
        /// <param name="buff2">Buff对象2</param>
        /// <returns>比较结果</returns>
        public delegate int BuffSortFunc(Buff buff1, Buff buff2);

        /// <summary>
        /// 获取Buff对象属性值的object类型代理
        /// </summary>
        /// <param name="buff">Buff对象</param>
        /// <returns>属性值</returns>
        public delegate object BuffValueObjGet(Buff buff);

        /// <summary>
        /// 设置Buff对象属性值的代理
        /// </summary>
        /// <param name="buff">Buff对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void BuffValueObjSet(Buff buff, object value);

        /// <summary>
        /// Buff排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public BuffValueStrGet valueStrGetCall;
            public BuffSortFunc valueSortFunc;
            public BuffValueObjGet valueObjGet;
            public BuffValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Buff)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Buff)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Buff)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Buff)a, (Buff)b);
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
            name = "状态",
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
        /// 按子类型排序
        /// </summary>
        public static SortTitle SortBySubKind = new SortTitle()
        {
            name = "子类型",
            width = 2.00f,
            valueStrGetCall = x => x.subKind.ToString(),
            valueSortFunc = (a, b) => a.subKind.CompareTo(b.subKind),
            valueObjGet = x => x.subKind,
            valueObjSet = (x, v) => x.subKind = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按持续回合限制排序
        /// </summary>
        public static SortTitle SortByLimit = new SortTitle()
        {
            name = "持续回合",
            width = 2.40f,
            valueStrGetCall = x => x.limit.ToString(),
            valueSortFunc = (a, b) => a.limit.CompareTo(b.limit),
            valueObjGet = x => x.limit,
            valueObjSet = (x, v) => x.limit = (int)v,
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
            SortByLimit,
        };
    }
}
