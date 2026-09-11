using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 能力等级排序功能类，提供能力等级（AbilityLevelType）对象的各种排序字段定义
    /// 能力等级为武将兵种适性/能力成长的等级配置，主要字段为升级所需经验
    /// </summary>
    public class AbilityLevelTypeSortFunction : Singleton<AbilityLevelTypeSortFunction>
    {
        /// <summary>
        /// 获取能力等级对象显示字符串的代理
        /// </summary>
        /// <param name="abilityLevelType">能力等级对象</param>
        /// <returns>显示字符串</returns>
        public delegate string AbilityLevelTypeValueStrGet(AbilityLevelType abilityLevelType);

        /// <summary>
        /// 能力等级对象排序比较的代理
        /// </summary>
        /// <param name="abilityLevelType1">能力等级对象1</param>
        /// <param name="abilityLevelType2">能力等级对象2</param>
        /// <returns>比较结果</returns>
        public delegate int AbilityLevelTypeSortFunc(AbilityLevelType abilityLevelType1, AbilityLevelType abilityLevelType2);

        /// <summary>
        /// 获取能力等级对象属性值的object类型代理
        /// </summary>
        /// <param name="abilityLevelType">能力等级对象</param>
        /// <returns>属性值</returns>
        public delegate object AbilityLevelTypeValueObjGet(AbilityLevelType abilityLevelType);

        /// <summary>
        /// 设置能力等级对象属性值的代理
        /// </summary>
        /// <param name="abilityLevelType">能力等级对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void AbilityLevelTypeValueObjSet(AbilityLevelType abilityLevelType, object value);

        /// <summary>
        /// 能力等级排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public AbilityLevelTypeValueStrGet valueStrGetCall;
            public AbilityLevelTypeSortFunc valueSortFunc;
            public AbilityLevelTypeValueObjGet valueObjGet;
            public AbilityLevelTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((AbilityLevelType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((AbilityLevelType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((AbilityLevelType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((AbilityLevelType)a, (AbilityLevelType)b);
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
            name = "能力等级",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
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
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByExp,
        };
    }
}
