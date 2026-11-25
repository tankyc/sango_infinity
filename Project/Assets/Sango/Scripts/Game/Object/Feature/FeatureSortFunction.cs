using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
   
    public class FeatureSortFunction : Singleton<FeatureSortFunction>
    {
        public delegate string FeatureValueStrGet(Feature Feature);
        public delegate int FeatureValueGet(Feature Feature);
        public delegate int FeatureSortFunc(Feature Feature1, Feature Feature2);

        /// <summary>
        /// 获取Feature对象属性值的object类型代理
        /// </summary>
        /// <param name="Feature">武将对象</param>
        /// <returns>属性值</returns>
        public delegate object FeatureValueObjGet(Feature Feature);

        /// <summary>
        /// 设置Feature对象属性值的代理
        /// </summary>
        /// <param name="Feature">武将对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void FeatureValueObjSet(Feature Feature, object value);

        public class SortTitle : ObjectSortTitle
        {
            public FeatureValueStrGet valueGetCall;
            public FeatureSortFunc FeatureSortFunc;
            public FeatureValueObjGet valueObjGet;
            public FeatureValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Feature)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Feature)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueGetCall.Invoke((Feature)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return FeatureSortFunc.Invoke((Feature)a, (Feature)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueGetCall = valueGetCall,
                    FeatureSortFunc = FeatureSortFunc,
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

        public static SortTitle SortByName = new SortTitle()
        {
            name = "特技",
            // 【列宽】支持 5 个中文字：5 字 × 1.05 余量 = 5.25 个字符宽（131 px）。
            // 原值 3.20f 只够 3 个字；设计要求里存在 5 字特技（如「八门金锁阵」），
            // 因此按 5 字预留。1.05 的余量系数沿用 <see cref="ScenarioVariables.NameWidthPerChar"/>，
            // 与所有"名字列"（<see cref="ObjectSortTitle.ContentMaxWidth"/> 的 isNameColumn 分支）同一口径。
            width = 5f * ScenarioVariables.NameWidthPerChar,
            // 【空值保护】Features.json 漏填 Name 时，属性返回 null；
            // 排序比较器若直接 CompareTo 会抛 NullReferenceException，把整个列表拖垮。
            valueGetCall = x => x.Name ?? string.Empty,
            FeatureSortFunc = (a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };


        /// <summary>
        /// 特技类型（<see cref="Feature.kind"/>）的中文名，下标即 <see cref="FeatureKindType"/> 的整数值。
        ///
        /// <para>
        /// 【与 Features.json 的对应关系】kind 字段直接存枚举整数：
        /// 0 混合 / 1 行军 / 2 攻击 / 3 防御 / 4 计略 / 5 补助 /
        /// 6 生产（内政·产出）/ 7 收获（内政·收获）/ 8 灾害 / 9 关系。
        /// 6 与 7 都属"内政"，这里按枚举名（Produce / Harvest）分别译成"生产 / 收获"，
        /// 便于配置时区分"内政设施产出"与"季节收获加成"两类效果。
        /// </para>
        /// </summary>
        static readonly string[] KindNames =
        {
            "混合",   // 0 Mixed
            "行军",   // 1 TroopMove
            "攻击",   // 2 TroopAttack
            "防御",   // 3 TroopDefence
            "计略",   // 4 TroopStrategy
            "补助",   // 5 TroopSupport
            "生产",   // 6 CityProduce
            "收获",   // 7 CityHarvest
            "灾害",   // 8 CityDisaster
            "关系",   // 9 PersonRelationship
        };

        /// <summary>
        /// 取特技类型的中文名（供"类型"列显示）。
        /// 越界或未填时返回"未知"，不抛异常 —— 数据被手工改错时界面仍能打开。
        /// </summary>
        /// <param name="kind">Features.json 的 kind 值</param>
        /// <returns>中文名</returns>
        public static string GetKindName(int kind)
        {
            if (kind < 0 || kind >= KindNames.Length)
                return "未知";
            return KindNames[kind];
        }

        /// <summary>
        /// 等级列：Features.json 的 <c>level</c>（原版特技分级，1~4）。
        ///
        /// <para>
        /// 【为什么要单列】特技表原先只有"名字 + 说明"两列，<c>level</c> 与 <c>kind</c>
        /// 虽然能配置（<see cref="Feature.level"/> / <see cref="Feature.kind"/>），
        /// 但在界面上完全看不到 —— 配置完无法核对是否填对。
        /// 本列只读展示，不通过表格改数据（editType 保持 None）。
        /// </para>
        /// </summary>
        public static SortTitle SortByLevel = new SortTitle()
        {
            name = "等级",
            width = 2.00f,
            valueGetCall = x => x.level.ToString(),
            FeatureSortFunc = (a, b) => a.level.CompareTo(b.level),
            valueObjGet = x => x.level,
            valueObjSet = null,
        };

        /// <summary>
        /// 类型列：Features.json 的 <c>kind</c>，显示为中文名（见 <see cref="GetKindName"/>）。
        /// 宽度按 3 个中文字预留（最长的是"生产 / 收获 / 混合"，均 2 字），排序按 kind 整数值。
        /// </summary>
        public static SortTitle SortByKind = new SortTitle()
        {
            name = "类型",
            width = 3.20f,
            valueGetCall = x => GetKindName(x.kind),
            FeatureSortFunc = (a, b) => a.kind.CompareTo(b.kind),
            valueObjGet = x => x.kind,
            valueObjSet = null,
        };

        public static SortTitle SortByDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            // 【空值保护】新增特技常常先占位、后补说明；漏填 desc 时
            // 原先的 desc.ToString() / desc.CompareTo 都会直接抛空引用。
            valueGetCall = x => x.desc ?? string.Empty,
            FeatureSortFunc = (a, b) => string.Compare(a.desc, b.desc, System.StringComparison.Ordinal),
            valueObjGet = x => x.desc,
            valueObjSet = null,
        };

        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortByName,
            SortByLevel,
            SortByKind,
            SortByDesc,
        };

    }

}
