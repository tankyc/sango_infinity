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

        /// <summary>
        /// 是否为"名字"列。
        /// 名字列不使用固定的 <see cref="width"/>，而是随剧本参数"名字最大支持字数"
        /// (<see cref="ScenarioVariables.maxNameLength"/>) 动态计算宽度，见 <see cref="ContentMaxWidth"/>。
        /// 所有 SortByName 筛选项都应置为 true。
        /// </summary>
        public bool isNameColumn;

        /// <summary>
        /// 名字列额外预留的字符数（仅 <see cref="isNameColumn"/> 为 true 时生效）。
        /// 例如军团名显示为"第N军团"，需要在名字最大支持字数的基础上额外增加 4 个字的宽度。
        /// </summary>
        public int nameExtraChars;

        /// <summary>
        /// 内容最大宽度（像素）。
        /// 普通列 = width × 25；
        /// 名字列 = 1.05 × (名字最大支持字数 + 额外字符数) × 25，其中单字像素沿用 25 的口径。
        /// </summary>
        public float ContentMaxWidth
        {
            get
            {
                if (isNameColumn)
                    return ScenarioVariables.GetNameColumnWidth(nameExtraChars) * 25f;
                return width * 25f;
            }
        }

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

        /// <summary>默认颜色（白）的富文本颜色值</summary>
        public const string ColorHexWhite = "#FFFFFF";
        /// <summary>1级（仅适性使用）：绿</summary>
        public const string ColorHexGreen = "#00FF7F";
        /// <summary>2级（仅适性使用）：蓝</summary>
        public const string ColorHexBlue = "#00BFFF";
        /// <summary>3级 / 属性80~89：紫</summary>
        public const string ColorHexPurple = "#BF5FFF";
        /// <summary>4级 / 属性90~99：金</summary>
        public const string ColorHexGold = "#FFD700";
        /// <summary>5级及以上 / 属性>=100：红</summary>
        public const string ColorHexRed = "#FF3333";

        /// <summary>
        /// 按等级取颜色：1 绿，2 蓝，3 紫，4 金，5 及以上 红，其余 白。
        /// </summary>
        /// <param name="grade">等级</param>
        /// <returns>富文本颜色值（如 #61E28F）</returns>
        public static string GetGradeColorHex(int grade)
        {
            if (grade <= 0) return ColorHexWhite;
            if (grade >= 5) return ColorHexRed;
            switch (grade)
            {
                case 1: return ColorHexGreen;
                case 2: return ColorHexBlue;
                case 3: return ColorHexPurple;
                default: return ColorHexGold;
            }
        }

        /// <summary>
        /// 给文本套上富文本颜色标签
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <param name="colorHex">富文本颜色值（如 #FFFFFF）</param>
        /// <returns>带颜色标签的文本</returns>
        public static string Colorize(string text, string colorHex)
        {
            return "<color=" + colorHex + ">" + text + "</color>";
        }

        /// <summary>
        /// 武将五维属性文本着色：>=100 红，>=90 金，>=80 紫；60~79 及以下使用默认颜色。
        /// </summary>
        /// <param name="value">属性数值</param>
        /// <returns>带颜色标签的文本</returns>
        public static string GetAttributeColorText(int value)
        {
            int grade = value / 10 - 5;
            // 属性只保留紫(80~89)、金(90~99)、红(>=100)三档颜色，60~79 不再显示绿/蓝
            if (grade <= 2)
                return Colorize(value.ToString(), ColorHexWhite);
            return Colorize(value.ToString(), GetGradeColorHex(grade));
        }

        /// <summary>
        /// 兵种适性文本着色：等级 1 绿，2 蓝，3 紫，4 金，5 及以上 红，其余 白。
        /// </summary>
        /// <param name="text">适性名称文本</param>
        /// <param name="level">适性等级</param>
        /// <returns>带颜色标签的文本</returns>
        public static string GetAbilityColorText(string text, int level)
        {
            return Colorize(text, GetGradeColorHex(level));
        }

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
