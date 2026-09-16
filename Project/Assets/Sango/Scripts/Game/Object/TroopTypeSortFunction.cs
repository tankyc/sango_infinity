using Sango.Core.Player;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 兵种类型排序功能类，提供兵种类型对象的各种排序字段定义
    /// </summary>
    public class TroopTypeSortFunction : Singleton<TroopTypeSortFunction>
    {
        /// <summary>
        /// 获取兵种类型对象显示字符串的代理
        /// </summary>
        /// <param name="troopType">兵种类型对象</param>
        /// <returns>显示字符串</returns>
        public delegate string TroopTypeValueStrGet(TroopType troopType);

        /// <summary>
        /// 兵种类型对象排序比较的代理
        /// </summary>
        /// <param name="troopType1">兵种类型对象1</param>
        /// <param name="troopType2">兵种类型对象2</param>
        /// <returns>比较结果</returns>
        public delegate int TroopTypeSortFunc(TroopType troopType1, TroopType troopType2);

        /// <summary>
        /// 获取兵种类型对象属性值的object类型代理
        /// </summary>
        /// <param name="troopType">兵种类型对象</param>
        /// <returns>属性值</returns>
        public delegate object TroopTypeValueObjGet(TroopType troopType);

        /// <summary>
        /// 设置兵种类型对象属性值的代理
        /// </summary>
        /// <param name="troopType">兵种类型对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TroopTypeValueObjSet(TroopType troopType, object value);

        /// <summary>
        /// 兵种类型排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public TroopTypeValueStrGet valueStrGetCall;
            public TroopTypeSortFunc valueSortFunc;
            public TroopTypeValueObjGet valueObjGet;
            public TroopTypeValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((TroopType)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((TroopType)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((TroopType)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((TroopType)a, (TroopType)b);
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
            name = "兵种",
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
        /// 按攻击力排序
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
        /// 按防御力排序
        /// </summary>
        public static SortTitle SortByDef = new SortTitle()
        {
            name = "防御力",
            width = 2.40f,
            valueStrGetCall = x => x.def.ToString(),
            valueSortFunc = (a, b) => a.def.CompareTo(b.def),
            valueObjGet = x => x.def,
            valueObjSet = (x, v) => x.def = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按移动力排序
        /// </summary>
        public static SortTitle SortByMove = new SortTitle()
        {
            name = "移动力",
            width = 2.40f,
            valueStrGetCall = x => x.move.ToString(),
            valueSortFunc = (a, b) => a.move.CompareTo(b.move),
            valueObjGet = x => x.move,
            valueObjSet = (x, v) => x.move = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        /// <summary>
        /// 按战斗力排序
        /// </summary>
        public static SortTitle SortByFightPower = new SortTitle()
        {
            name = "战斗力",
            width = 2.40f,
            valueStrGetCall = x => x.fightPower.ToString(),
            valueSortFunc = (a, b) => a.fightPower.CompareTo(b.fightPower),
            valueObjGet = x => x.fightPower,
            valueObjSet = (x, v) => x.fightPower = (int)v,
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
            SortByAtk,
            SortByDef,
            SortByMove,
            SortByFightPower,
        };
    }
}
