using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 技能排序功能类，提供技能对象的各种排序字段定义
    /// </summary>
    public class SkillSortFunction : Singleton<SkillSortFunction>
    {
        /// <summary>
        /// 获取技能对象显示字符串的代理
        /// </summary>
        /// <param name="skill">技能对象</param>
        /// <returns>显示字符串</returns>
        public delegate string SkillValueStrGet(Skill skill);

        /// <summary>
        /// 技能对象排序比较的代理
        /// </summary>
        /// <param name="skill1">技能对象1</param>
        /// <param name="skill2">技能对象2</param>
        /// <returns>比较结果</returns>
        public delegate int SkillSortFunc(Skill skill1, Skill skill2);

        /// <summary>
        /// 获取技能对象属性值的object类型代理
        /// </summary>
        /// <param name="skill">技能对象</param>
        /// <returns>属性值</returns>
        public delegate object SkillValueObjGet(Skill skill);

        /// <summary>
        /// 设置技能对象属性值的代理
        /// </summary>
        /// <param name="skill">技能对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void SkillValueObjSet(Skill skill, object value);

        /// <summary>
        /// 技能排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public SkillValueStrGet valueStrGetCall;
            public SkillSortFunc valueSortFunc;
            public SkillValueObjGet valueObjGet;
            public SkillValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Skill)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Skill)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Skill)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Skill)a, (Skill)b);
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
            name = "技能",
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
        /// 按基础攻击力排序
        /// </summary>
        public static SortTitle SortByAtk = new SortTitle()
        {
            name = "攻击力",
            width = 2.40f,
            valueStrGetCall = x => x.atk.ToString(),
            valueSortFunc = (a, b) => a.atk.CompareTo(b.atk),
            valueObjGet = x => x.atk,
            valueObjSet = (x, v) => x.atk = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按气力消耗排序
        /// </summary>
        public static SortTitle SortByCostEnergy = new SortTitle()
        {
            name = "气力消耗",
            width = 2.40f,
            valueStrGetCall = x => x.costEnergy.ToString(),
            valueSortFunc = (a, b) => a.costEnergy.CompareTo(b.costEnergy),
            valueObjGet = x => x.costEnergy,
            valueObjSet = (x, v) => x.costEnergy = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按基础成功率排序
        /// </summary>
        public static SortTitle SortBySuccessRate = new SortTitle()
        {
            name = "成功率",
            width = 2.40f,
            valueStrGetCall = x => x.successRate.ToString(),
            valueSortFunc = (a, b) => a.successRate.CompareTo(b.successRate),
            valueObjGet = x => x.successRate,
            valueObjSet = (x, v) => x.successRate = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按是否远程排序
        /// </summary>
        public static SortTitle SortByIsRange = new SortTitle()
        {
            name = "远程",
            width = 2.00f,
            valueStrGetCall = x => x.isRange ? "是" : "否",
            valueSortFunc = (a, b) => a.isRange.CompareTo(b.isRange),
            valueObjGet = x => x.isRange,
            valueObjSet = null,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByKind,
            SortByCostEnergy,
            SortBySuccessRate,
        };
    }
}
