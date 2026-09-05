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
