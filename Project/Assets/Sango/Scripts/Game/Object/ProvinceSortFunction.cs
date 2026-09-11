using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 州排序功能类，提供州对象的各种排序字段定义
    /// </summary>
    public class ProvinceSortFunction : Singleton<ProvinceSortFunction>
    {
        /// <summary>
        /// 获取州对象显示字符串的代理
        /// </summary>
        /// <param name="province">州对象</param>
        /// <returns>显示字符串</returns>
        public delegate string ProvinceValueStrGet(Province province);

        /// <summary>
        /// 州对象排序比较的代理
        /// </summary>
        /// <param name="province1">州对象1</param>
        /// <param name="province2">州对象2</param>
        /// <returns>比较结果</returns>
        public delegate int ProvinceSortFunc(Province province1, Province province2);

        /// <summary>
        /// 获取州对象属性值的object类型代理
        /// </summary>
        /// <param name="province">州对象</param>
        /// <returns>属性值</returns>
        public delegate object ProvinceValueObjGet(Province province);

        /// <summary>
        /// 设置州对象属性值的代理
        /// </summary>
        /// <param name="province">州对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void ProvinceValueObjSet(Province province, object value);

        /// <summary>
        /// 州排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public ProvinceValueStrGet valueStrGetCall;
            public ProvinceSortFunc valueSortFunc;
            public ProvinceValueObjGet valueObjGet;
            public ProvinceValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Province)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Province)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Province)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Province)a, (Province)b);
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
            name = "州",
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
        /// 按所属区域排序（仅显示）
        /// </summary>
        public static SortTitle SortByRegion = new SortTitle()
        {
            name = "区域",
            width = 4.00f,
            valueStrGetCall = x => x.Region?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.Region, b.Region),
            valueObjGet = x => x.Region,
            valueObjSet = null,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByRegion,
        };
    }
}
