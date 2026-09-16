using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 兵种动画排序功能类，提供兵种动画对象的各种排序字段定义
    /// </summary>
    public class TroopAnimationSortFunction : Singleton<TroopAnimationSortFunction>
    {
        /// <summary>
        /// 获取兵种动画对象显示字符串的代理
        /// </summary>
        /// <param name="troopAnimation">兵种动画对象</param>
        /// <returns>显示字符串</returns>
        public delegate string TroopAnimationValueStrGet(TroopAnimation troopAnimation);

        /// <summary>
        /// 兵种动画对象排序比较的代理
        /// </summary>
        /// <param name="troopAnimation1">兵种动画对象1</param>
        /// <param name="troopAnimation2">兵种动画对象2</param>
        /// <returns>比较结果</returns>
        public delegate int TroopAnimationSortFunc(TroopAnimation troopAnimation1, TroopAnimation troopAnimation2);

        /// <summary>
        /// 获取兵种动画对象属性值的object类型代理
        /// </summary>
        /// <param name="troopAnimation">兵种动画对象</param>
        /// <returns>属性值</returns>
        public delegate object TroopAnimationValueObjGet(TroopAnimation troopAnimation);

        /// <summary>
        /// 设置兵种动画对象属性值的代理
        /// </summary>
        /// <param name="troopAnimation">兵种动画对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TroopAnimationValueObjSet(TroopAnimation troopAnimation, object value);

        /// <summary>
        /// 兵种动画排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public TroopAnimationValueStrGet valueStrGetCall;
            public TroopAnimationSortFunc valueSortFunc;
            public TroopAnimationValueObjGet valueObjGet;
            public TroopAnimationValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((TroopAnimation)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((TroopAnimation)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((TroopAnimation)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((TroopAnimation)a, (TroopAnimation)b);
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
            name = "动画",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按动画资源名排序
        /// </summary>
        public static SortTitle SortByAniName = new SortTitle()
        {
            name = "动画名",
            width = 4.00f,
            valueStrGetCall = x => x.aniName,
            valueSortFunc = (a, b) => string.Compare(a.aniName, b.aniName, System.StringComparison.Ordinal),
            valueObjGet = x => x.aniName,
            valueObjSet = (x, v) => x.aniName = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按帧数排序
        /// </summary>
        public static SortTitle SortByCelCount = new SortTitle()
        {
            name = "帧数",
            width = 2.00f,
            valueStrGetCall = x => x.celCount.ToString(),
            valueSortFunc = (a, b) => a.celCount.CompareTo(b.celCount),
            valueObjGet = x => x.celCount,
            valueObjSet = (x, v) => x.celCount = System.Convert.ToByte(v),
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按缩放排序
        /// </summary>
        public static SortTitle SortByScale = new SortTitle()
        {
            name = "缩放",
            width = 2.00f,
            valueStrGetCall = x => x.scale.ToString(),
            valueSortFunc = (a, b) => a.scale.CompareTo(b.scale),
            valueObjGet = x => x.scale,
            valueObjSet = null,
        };

        /// <summary>
        /// 按是否循环排序
        /// </summary>
        public static SortTitle SortByIsLoop = new SortTitle()
        {
            name = "循环",
            width = 2.00f,
            valueStrGetCall = x => x.isLoop ? "是" : "否",
            valueSortFunc = (a, b) => a.isLoop.CompareTo(b.isLoop),
            valueObjGet = x => x.isLoop,
            valueObjSet = null,
        };

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByAniName,
            SortByCelCount,
        };
    }
}
