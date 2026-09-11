using System;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class ObjectSortTitle
    {
        public string name;
        public float width;
        public int alignment = 4;
        public object customData;
        public float ContentMaxWidth { get { return width * 25f; } }

        /// <summary>
        /// 值修改类型
        /// 用于定义该属性的编辑方式，UIDataEdit根据此字段决定使用哪种控件
        /// </summary>
        public DataEditType editType = DataEditType.None;

        /// <summary>
        /// 候选数据集类型
        /// 用于定义编辑时获取可选项的数据集来源（人物状态/势力集合/城市集合等）
        /// </summary>
        public DataSetType dataSetType = DataSetType.None;

        /// <summary>
        /// 数值编辑下限（IntInput与IntCalculator使用）
        /// </summary>
        public int minValue = 0;

        /// <summary>
        /// 数值编辑上限（IntInput与IntCalculator使用）
        /// </summary>
        public int maxValue = int.MaxValue;

        /// <summary>
        /// 是否允许被UIDataEdit编辑
        /// </summary>
        public bool CanEdit { get { return editType != DataEditType.None; } }

        /// <summary>
        /// 判断指定目标对象是否允许修改该属性（编辑前拦截用）
        /// 默认全部允许，特殊属性（如君主身份）可在子类中重写限制
        /// </summary>
        /// <param name="obj">目标对象</param>
        /// <returns>是否允许修改</returns>
        public virtual bool CanSetValue(SangoObject obj) { return true; }

        public abstract string GetValueStr(SangoObject obj);
        public abstract int Sort(SangoObject a, SangoObject b);

        /// <summary>
        /// 获取属性值（通用object返回类型）
        /// 子类通过代理闭包实现具体逻辑
        /// </summary>
        /// <param name="obj">目标SangoObject</param>
        /// <returns>属性值</returns>
        public abstract object GetValue(SangoObject obj);

        /// <summary>
        /// 设置属性值（通用object参数类型）
        /// 子类通过代理闭包实现具体逻辑
        /// </summary>
        /// <param name="obj">目标SangoObject</param>
        /// <param name="value">新的属性值</param>
        public abstract void SetValue(SangoObject obj, object value);

        public ObjectSortTitle SetAlignment(int a) {  this.alignment = a; return this; }
        public ObjectSortTitle SetName(string a) {  this.name = a; return this; }
        public ObjectSortTitle SetCustomData(object a) {  this.customData = a; return this; }

        /// <summary>
        /// 设置值修改类型（链式调用）
        /// </summary>
        /// <param name="t">值修改类型</param>
        /// <returns>当前ObjectSortTitle</returns>
        public ObjectSortTitle SetEditType(DataEditType t) { this.editType = t; return this; }

        /// <summary>
        /// 设置候选数据集类型（链式调用）
        /// </summary>
        /// <param name="t">数据集类型</param>
        /// <returns>当前ObjectSortTitle</returns>
        public ObjectSortTitle SetDataSetType(DataSetType t) { this.dataSetType = t; return this; }

        /// <summary>
        /// 设置数值编辑范围（IntInput与IntCalculator使用）
        /// </summary>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns>当前ObjectSortTitle</returns>
        public ObjectSortTitle SetRange(int min, int max) { this.minValue = min; this.maxValue = max; return this; }
    }
}
