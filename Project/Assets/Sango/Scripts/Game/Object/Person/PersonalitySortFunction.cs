using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 性格排序功能类，提供性格对象的各种排序字段定义
    /// </summary>
    public class PersonalitySortFunction : Singleton<PersonalitySortFunction>
    {
        /// <summary>
        /// 获取性格对象显示字符串的代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <returns>显示字符串</returns>
        public delegate string PersonalityValueStrGet(Personality personality);

        /// <summary>
        /// 性格对象排序比较的代理
        /// </summary>
        /// <param name="personality1">性格对象1</param>
        /// <param name="personality2">性格对象2</param>
        /// <returns>比较结果</returns>
        public delegate int PersonalitySortFunc(Personality personality1, Personality personality2);

        /// <summary>
        /// 获取性格对象属性值的object类型代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonalityValueObjGet(Personality personality);

        /// <summary>
        /// 设置性格对象属性值的代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonalityValueObjSet(Personality personality, object value);

        /// <summary>
        /// 性格排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public PersonalityValueStrGet valueStrGetCall;
            public PersonalitySortFunc valueSortFunc;
            public PersonalityValueObjGet valueObjGet;
            public PersonalityValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Personality)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Personality)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Personality)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Personality)a, (Personality)b);
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
            name = "性格",
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
        /// 按战争倾向加成排序
        /// </summary>
        public static SortTitle SortByWarTendencyAdd = new SortTitle()
        {
            name = "战争倾向",
            width = 2.40f,
            valueStrGetCall = x => x.warTendencyAdd.ToString(),
            valueSortFunc = (a, b) => a.warTendencyAdd.CompareTo(b.warTendencyAdd),
            valueObjGet = x => x.warTendencyAdd,
            valueObjSet = (x, v) => x.warTendencyAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按防御倾向加成排序
        /// </summary>
        public static SortTitle SortByDefenseTendencyAdd = new SortTitle()
        {
            name = "防御倾向",
            width = 2.40f,
            valueStrGetCall = x => x.defenseTendencyAdd.ToString(),
            valueSortFunc = (a, b) => a.defenseTendencyAdd.CompareTo(b.defenseTendencyAdd),
            valueObjGet = x => x.defenseTendencyAdd,
            valueObjSet = (x, v) => x.defenseTendencyAdd = (int)v,
            editType = DataEditType.IntCalculator,
        };

        /// <summary>
        /// 按外交倾向加成排序
        /// </summary>
        public static SortTitle SortByDiplomacyTendencyAdd = new SortTitle()
        {
            name = "外交倾向",
            width = 2.40f,
            valueStrGetCall = x => x.diplomacyTendencyAdd.ToString(),
            valueSortFunc = (a, b) => a.diplomacyTendencyAdd.CompareTo(b.diplomacyTendencyAdd),
            valueObjGet = x => x.diplomacyTendencyAdd,
            valueObjSet = (x, v) => x.diplomacyTendencyAdd = (int)v,
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
        };
    }
}
