using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 道具类型排序功能类，提供道具类型对象的各种排序字段定义
    /// </summary>
    public class ItemTypeSortFunction : Singleton<ItemTypeSortFunction>
    {
        /// <summary>
        /// 获取道具类型对象显示字符串的代理
        /// </summary>
        /// <param name="itemType">道具类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string ItemTypeValueStrGet(ItemType itemType);

        /// <summary>
        /// 道具类型对象排序比较的代理
        /// </summary>
        /// <param name="itemType1">道具类型对象1</param>
        /// <param name="itemType2">道具类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int ItemTypeSortFunc(ItemType itemType1, ItemType itemType2);

        /// <summary>
        /// 获取道具类型对象属性值的object类型代理
        /// </summary>
        /// <param name="itemType">道具类型对象</param>
        /// <returns>属性值</returns>
        public delegate object ItemTypeValueObjGet(ItemType itemType);

        /// <summary>
        /// 设置道具类型对象属性值的代理
        /// </summary>
        /// <param name="itemType">道具类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void ItemTypeValueObjSet(ItemType itemType, object value);

        /// <summary>
        /// 道具类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public ItemTypeValueStrGet valueStrGetCall;
            public ItemTypeSortFunc valueSortFunc;
            public ItemTypeValueObjGet valueObjGet;
            public ItemTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((ItemType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((ItemType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((ItemType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((ItemType)a, (ItemType)b);
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
            name = "道具",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按说明排序
        /// </summary>
        public static SortTitle SortByDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            valueStrGetCall = x => x.desc,
            valueSortFunc = (a, b) => string.Compare(a.desc, b.desc, System.StringComparison.Ordinal),
            valueObjGet = x => x.desc,
            valueObjSet = (x, v) => x.desc = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 按主类型排序
        /// </summary>
        public static SortTitle SortByKind = new SortTitle()
        {
            name = "主类型",
            width = 2.00f,
            valueStrGetCall = x => x.kind.ToString(),
            valueSortFunc = (a, b) => a.kind.CompareTo(b.kind),
            valueObjGet = x => x.kind,
            valueObjSet = (x, v) => x.kind = System.Convert.ToByte(v),
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按次类型排序
        /// </summary>
        public static SortTitle SortByStoreKind = new SortTitle()
        {
            name = "次类型",
            width = 2.00f,
            valueStrGetCall = x => x.storeKind.ToString(),
            valueSortFunc = (a, b) => a.storeKind.CompareTo(b.storeKind),
            valueObjGet = x => x.storeKind,
            valueObjSet = (x, v) => x.storeKind = System.Convert.ToByte(v),
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按额外费用排序
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
        /// 按是否可存储排序
        /// </summary>
        public static SortTitle SortByStore = new SortTitle()
        {
            name = "可存储",
            width = 2.00f,
            valueStrGetCall = x => x.store ? "是" : "否",
            valueSortFunc = (a, b) => a.store.CompareTo(b.store),
            valueObjGet = x => x.store,
            valueObjSet = null,
        };

        /// <summary>
        /// 带品级颜色的道具名列（国库预览用）。
        ///
        /// 与 <see cref="SortByName"/> 的区别只在显示文本：这里输出 <see cref="ItemType.ColorName"/>，
        /// 珍品名按品级上色（与授予 / 没收窗口、发现宝物的提示文本同一套口径）。
        /// 排序仍按纯名字，避免富文本标签参与比较。
        /// </summary>
        public static SortTitle SortByColorName = new SortTitle()
        {
            name = "道具",
            width = 8.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x => x.ColorName,
            valueSortFunc = (a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 品级列（★ 星级；无品级的普通道具显示「—」）
        /// </summary>
        public static SortTitle SortByGradeStars = new SortTitle()
        {
            name = "品级",
            width = 3.00f,
            valueStrGetCall = x => x.GradeText,
            valueSortFunc = (a, b) => a.grade.CompareTo(b.grade),
            valueObjGet = x => x.grade,
            valueObjSet = null,
        };

        /// <summary>
        /// 能力加成列（五维加成文本；无加成显示「—」）
        ///
        /// 【排序口径】按五维加成之和排，而不是按文本 —— 文本排序在"统率+5"与"武力+5"之间
        /// 完全由中文偏旁决定，玩家点表头会得到看不出规律的顺序。
        /// </summary>
        public static SortTitle SortByBonus = new SortTitle()
        {
            name = "能力加成",
            width = 12.00f,
            alignment = (int)UnityEngine.TextAnchor.MiddleLeft,
            valueStrGetCall = x =>
            {
                string text = x.GetBonusText();
                return string.IsNullOrEmpty(text) ? "—" : text;
            },
            valueSortFunc = (a, b) => GetBonusTotal(a).CompareTo(GetBonusTotal(b)),
            valueObjGet = x => GetBonusTotal(x),
            valueObjSet = null,
        };

        /// <summary>
        /// 数量列：显示道具在**指定仓库**里的持有量（国库预览用）。
        ///
        /// 【为什么是工厂】持有量不属于道具本身，而是"某势力国库 / 某城道具库"的现算值；
        /// 静态字段拿不到来源仓库，必须用闭包把 <paramref name="store"/> 固定下来
        /// （与 <see cref="ForceSortFunction.GetSortByDistanceDay"/> 同一思路）。
        /// </summary>
        /// <param name="store">来源仓库（国库 / 城道具库），可为 null（此时恒为 0）</param>
        /// <returns>列定义</returns>
        public static SortTitle GetSortByCount(ItemStore store)
        {
            return new SortTitle()
            {
                name = "数量",
                width = 2.50f,
                valueStrGetCall = x => GetCount(store, x).ToString(),
                valueSortFunc = (a, b) => GetCount(store, a).CompareTo(GetCount(store, b)),
                valueObjGet = x => GetCount(store, x),
                valueObjSet = null,
            };
        }

        /// <summary>五维加成之和（仅用于排序）</summary>
        static int GetBonusTotal(ItemType item)
        {
            if (item == null) return 0;
            return item.commandBonus + item.strengthBonus + item.intelligenceBonus
                 + item.politicsBonus + item.glamourBonus;
        }

        /// <summary>取道具在指定仓库里的数量（仓库或道具为空时返回 0）</summary>
        static int GetCount(ItemStore store, ItemType item)
        {
            if (store == null || item == null) return 0;
            return store.GetNumber(item);
        }

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortById,
            SortByName,
            SortByKind,
            SortByCost,
        };
    }
}
