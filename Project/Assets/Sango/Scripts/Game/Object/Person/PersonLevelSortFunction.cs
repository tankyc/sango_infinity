using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将等级排序功能类，提供武将等级对象的各种排序字段定义
    /// </summary>
    public class PersonLevelSortFunction : Singleton<PersonLevelSortFunction>
    {
        /// <summary>
        /// 获取武将等级对象显示字符串的代理
        /// </summary>
        /// <param name="personLevel">武将等级对象</param>
        /// <returns>显示字符串</returns>
        public delegate string PersonLevelValueStrGet(PersonLevel personLevel);

        /// <summary>
        /// 武将等级对象排序比较的代理
        /// </summary>
        /// <param name="personLevel1">武将等级对象1</param>
        /// <param name="personLevel2">武将等级对象2</param>
        /// <returns>比较结果</returns>
        public delegate int PersonLevelSortFunc(PersonLevel personLevel1, PersonLevel personLevel2);

        /// <summary>
        /// 获取武将等级对象属性值的object类型代理
        /// </summary>
        /// <param name="personLevel">武将等级对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonLevelValueObjGet(PersonLevel personLevel);

        /// <summary>
        /// 设置武将等级对象属性值的代理
        /// </summary>
        /// <param name="personLevel">武将等级对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonLevelValueObjSet(PersonLevel personLevel, object value);

        /// <summary>
        /// 武将等级排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public PersonLevelValueStrGet valueStrGetCall;
            public PersonLevelSortFunc valueSortFunc;
            public PersonLevelValueObjGet valueObjGet;
            public PersonLevelValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((PersonLevel)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((PersonLevel)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((PersonLevel)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((PersonLevel)a, (PersonLevel)b);
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
        /// 按名称排序（名称由ID生成，不可编辑）
        /// </summary>
        public static SortTitle SortByName = new SortTitle()
        {
            name = "等级",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = null,
        };

        /// <summary>
        /// 按升级所需经验排序
        /// </summary>
        public static SortTitle SortByExp = new SortTitle()
        {
            name = "所需经验",
            width = 2.40f,
            valueStrGetCall = x => x.exp.ToString(),
            valueSortFunc = (a, b) => a.exp.CompareTo(b.exp),
            valueObjGet = x => x.exp,
            valueObjSet = (x, v) => x.exp = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按带兵数排序
        /// </summary>
        public static SortTitle SortByTroops = new SortTitle()
        {
            name = "带兵数",
            width = 2.40f,
            valueStrGetCall = x => x.troops.ToString(),
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjGet = x => x.troops,
            valueObjSet = (x, v) => x.troops = (int)v,
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
            SortByExp,
            SortByTroops,
        };
    }
}
