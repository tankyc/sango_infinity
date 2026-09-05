using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 区域排序功能类，提供区域对象的各种排序字段定义
    /// </summary>
    public class RegionSortFunction : Singleton<RegionSortFunction>
    {
        /// <summary>
        /// 获取区域对象显示字符串的代理
        /// </summary>
        /// <param name="region">区域对象</param>
        /// <returns>显示字符串</returns>
        public delegate string RegionValueStrGet(Region region);

        /// <summary>
        /// 区域对象排序比较的代理
        /// </summary>
        /// <param name="region1">区域对象1</param>
        /// <param name="region2">区域对象2</param>
        /// <returns>比较结果</returns>
        public delegate int RegionSortFunc(Region region1, Region region2);

        /// <summary>
        /// 获取区域对象属性值的object类型代理
        /// </summary>
        /// <param name="region">区域对象</param>
        /// <returns>属性值</returns>
        public delegate object RegionValueObjGet(Region region);

        /// <summary>
        /// 设置区域对象属性值的代理
        /// </summary>
        /// <param name="region">区域对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void RegionValueObjSet(Region region, object value);

        /// <summary>
        /// 区域排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public RegionValueStrGet valueStrGetCall;
            public RegionSortFunc valueSortFunc;
            public RegionValueObjGet valueObjGet;
            public RegionValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Region)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Region)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Region)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Region)a, (Region)b);
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
            name = "区域",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
        };
    }
}
