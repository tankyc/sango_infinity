using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 爵位称号排序功能类，提供爵位称号对象的各种排序字段定义
    /// </summary>
    public class TitleSortFunction : Singleton<TitleSortFunction>
    {
        /// <summary>
        /// 获取爵位称号对象显示字符串的代理
        /// </summary>
        /// <param name="title">爵位称号对象</param>
        /// <returns>显示字符串</returns>
        public delegate string TitleValueStrGet(Title title);

        /// <summary>
        /// 爵位称号对象排序比较的代理
        /// </summary>
        /// <param name="title1">爵位称号对象1</param>
        /// <param name="title2">爵位称号对象2</param>
        /// <returns>比较结果</returns>
        public delegate int TitleSortFunc(Title title1, Title title2);

        /// <summary>
        /// 获取爵位称号对象属性值的object类型代理
        /// </summary>
        /// <param name="title">爵位称号对象</param>
        /// <returns>属性值</returns>
        public delegate object TitleValueObjGet(Title title);

        /// <summary>
        /// 设置爵位称号对象属性值的代理
        /// </summary>
        /// <param name="title">爵位称号对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TitleValueObjSet(Title title, object value);

        /// <summary>
        /// 爵位称号排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public TitleValueStrGet valueStrGetCall;
            public TitleSortFunc valueSortFunc;
            public TitleValueObjGet valueObjGet;
            public TitleValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Title)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Title)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Title)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Title)a, (Title)b);
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
            name = "爵位",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
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
        /// 按指挥兵力排序
        /// </summary>
        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "指挥",
            width = 2.40f,
            valueStrGetCall = x => x.troopsLimit.ToString(),
            valueSortFunc = (a, b) => a.troopsLimit.CompareTo(b.troopsLimit),
            valueObjGet = x => x.troopsLimit,
            valueObjSet = (x, v) => x.troopsLimit = (int)v,
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
            SortByLevel,
            SortByTroopsLimit,
        };
    }
}
