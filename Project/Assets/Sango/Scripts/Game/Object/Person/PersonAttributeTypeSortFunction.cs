using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将属性类型排序功能类，提供武将属性类型对象的各种排序字段定义
    /// </summary>
    public class PersonAttributeTypeSortFunction : Singleton<PersonAttributeTypeSortFunction>
    {
        /// <summary>
        /// 获取武将属性类型对象显示字符串的代理
        /// </summary>
        /// <param name="personAttributeType">武将属性类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string PersonAttributeTypeValueStrGet(PersonAttributeType personAttributeType);

        /// <summary>
        /// 武将属性类型对象排序比较的代理
        /// </summary>
        /// <param name="personAttributeType1">武将属性类型对象1</param>
        /// <param name="personAttributeType2">武将属性类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int PersonAttributeTypeSortFunc(PersonAttributeType personAttributeType1, PersonAttributeType personAttributeType2);

        /// <summary>
        /// 获取武将属性类型对象属性值的object类型代理
        /// </summary>
        /// <param name="personAttributeType">武将属性类型对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonAttributeTypeValueObjGet(PersonAttributeType personAttributeType);

        /// <summary>
        /// 设置武将属性类型对象属性值的代理
        /// </summary>
        /// <param name="personAttributeType">武将属性类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonAttributeTypeValueObjSet(PersonAttributeType personAttributeType, object value);

        /// <summary>
        /// 武将属性类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public PersonAttributeTypeValueStrGet valueStrGetCall;
            public PersonAttributeTypeSortFunc valueSortFunc;
            public PersonAttributeTypeValueObjGet valueObjGet;
            public PersonAttributeTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((PersonAttributeType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((PersonAttributeType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((PersonAttributeType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((PersonAttributeType)a, (PersonAttributeType)b);
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
            name = "属性",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按名称ID排序
        /// </summary>
        public static SortTitle SortByNameID = new SortTitle()
        {
            name = "名称ID",
            width = 2.40f,
            valueStrGetCall = x => x.nameID.ToString(),
            valueSortFunc = (a, b) => a.nameID.CompareTo(b.nameID),
            valueObjGet = x => x.nameID,
            valueObjSet = (x, v) => x.nameID = (int)v,
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
        };
    }
}
