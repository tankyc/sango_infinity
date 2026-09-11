using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 工作类型排序功能类，提供工作类型对象的各种排序字段定义
    /// </summary>
    public class JobTypeSortFunction : Singleton<JobTypeSortFunction>
    {
        /// <summary>
        /// 获取工作类型对象显示字符串的代理
        /// </summary>
        /// <param name="jobType">工作类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string JobTypeValueStrGet(JobType jobType);

        /// <summary>
        /// 工作类型对象排序比较的代理
        /// </summary>
        /// <param name="jobType1">工作类型对象1</param>
        /// <param name="jobType2">工作类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int JobTypeSortFunc(JobType jobType1, JobType jobType2);

        /// <summary>
        /// 获取工作类型对象属性值的object类型代理
        /// </summary>
        /// <param name="jobType">工作类型对象</param>
        /// <returns>属性值</returns>
        public delegate object JobTypeValueObjGet(JobType jobType);

        /// <summary>
        /// 设置工作类型对象属性值的代理
        /// </summary>
        /// <param name="jobType">工作类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void JobTypeValueObjSet(JobType jobType, object value);

        /// <summary>
        /// 工作类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public JobTypeValueStrGet valueStrGetCall;
            public JobTypeSortFunc valueSortFunc;
            public JobTypeValueObjGet valueObjGet;
            public JobTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((JobType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((JobType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((JobType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((JobType)a, (JobType)b);
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
            name = "工作",
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
        /// 按行动力消耗排序
        /// </summary>
        public static SortTitle SortByCostAP = new SortTitle()
        {
            name = "行动力消耗",
            width = 2.40f,
            valueStrGetCall = x => x.costAP.ToString(),
            valueSortFunc = (a, b) => a.costAP.CompareTo(b.costAP),
            valueObjGet = x => x.costAP,
            valueObjSet = (x, v) => x.costAP = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按费用排序
        /// </summary>
        public static SortTitle SortByCost = new SortTitle()
        {
            name = "费用",
            width = 2.40f,
            valueStrGetCall = x => x.cost.ToString(),
            valueSortFunc = (a, b) => a.cost.CompareTo(b.cost),
            valueObjGet = x => x.cost,
            valueObjSet = (x, v) => x.cost = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按功绩收益排序
        /// </summary>
        public static SortTitle SortByMeritGain = new SortTitle()
        {
            name = "功绩收益",
            width = 2.40f,
            valueStrGetCall = x => x.meritGain.ToString(),
            valueSortFunc = (a, b) => a.meritGain.CompareTo(b.meritGain),
            valueObjGet = x => x.meritGain,
            valueObjSet = (x, v) => x.meritGain = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按技巧点收益排序
        /// </summary>
        public static SortTitle SortByTpGain = new SortTitle()
        {
            name = "技巧点收益",
            width = 2.40f,
            valueStrGetCall = x => x.tpGain.ToString(),
            valueSortFunc = (a, b) => a.tpGain.CompareTo(b.tpGain),
            valueObjGet = x => x.tpGain,
            valueObjSet = (x, v) => x.tpGain = (int)v,
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
            SortByCostAP,
            SortByCost,
        };
    }
}
