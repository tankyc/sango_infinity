using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 旗帜排序功能类，提供旗帜对象的各种排序字段定义
    /// </summary>
    public class FlagSortFunction : Singleton<FlagSortFunction>
    {
        /// <summary>
        /// 获取旗帜对象显示字符串的代理
        /// </summary>
        /// <param name="flag">旗帜对象</param>
        /// <returns>显示字符串</returns>
        public delegate string FlagValueStrGet(Flag flag);

        /// <summary>
        /// 旗帜对象排序比较的代理
        /// </summary>
        /// <param name="flag1">旗帜对象1</param>
        /// <param name="flag2">旗帜对象2</param>
        /// <returns>比较结果</returns>
        public delegate int FlagSortFunc(Flag flag1, Flag flag2);

        /// <summary>
        /// 获取旗帜对象属性值的object类型代理
        /// </summary>
        /// <param name="flag">旗帜对象</param>
        /// <returns>属性值</returns>
        public delegate object FlagValueObjGet(Flag flag);

        /// <summary>
        /// 设置旗帜对象属性值的代理
        /// </summary>
        /// <param name="flag">旗帜对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void FlagValueObjSet(Flag flag, object value);

        /// <summary>
        /// 旗帜排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public FlagValueStrGet valueStrGetCall;
            public FlagSortFunc valueSortFunc;
            public FlagValueObjGet valueObjGet;
            public FlagValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Flag)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Flag)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Flag)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Flag)a, (Flag)b);
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
            name = "旗帜",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按颜色排序（仅显示）
        /// </summary>
        public static SortTitle SortByColor = new SortTitle()
        {
            name = "颜色",
            width = 4.00f,
            valueStrGetCall = x => x.color.ToString(),
            valueSortFunc = (a, b) => string.Compare(a.color.ToString(), b.color.ToString(), System.StringComparison.Ordinal),
            valueObjGet = x => x.color,
            valueObjSet = null,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByColor,
        };
    }
}
