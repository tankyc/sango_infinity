namespace Sango.Core
{
    public class TroopSortFunction : Singleton<TroopSortFunction>
    {
        public delegate string TroopValueStrGet(Troop troop);
        public delegate T TroopValueGet<T>(Troop troop);
        public delegate int TroopSortFunc(Troop troop1, Troop troop2);

        /// <summary>
        /// 设置Troop对象属性值的代理
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TroopValueObjSet(Troop troop, object value);

        public class SortTitle<T> : ObjectSortTitle
        {
            public TroopValueStrGet valueStrGetCall;
            public TroopSortFunc valueSortFunc;
            public TroopValueGet<T> valueGetCall;
            public TroopValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueGetCall.Invoke((Troop)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Troop)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Troop)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Troop)a, (Troop)b);
            }

            public SortTitle<T> Copy()
            {
                return new SortTitle<T>
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueStrGetCall = valueStrGetCall,
                    valueGetCall = valueGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        public static SortTitle<string> SortByName = new SortTitle<string>()
        {
            name = "武将",
            width = 3.2f,
            valueGetCall = x => x.Name,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle<int> SortByDefence = new SortTitle<int>()
        {
            name = "防御",
            width = 2.0f,
            valueStrGetCall = x => x.Defence.ToString(),
            valueGetCall = x => x.Defence,
            valueSortFunc = (a, b) => a.Defence.CompareTo(b.Defence),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByAttack = new SortTitle<int>()
        {
            name = "攻击",
            width = 2.0f,
            valueStrGetCall = x => x.Attack.ToString(),
            valueGetCall = x => x.Attack,
            valueSortFunc = (a, b) => a.Attack.CompareTo(b.Attack),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByMoveability = new SortTitle<int>()
        {
            name = "移动",
            width = 2.0f,
            valueStrGetCall = x => x.MoveAbility.ToString(),
            valueGetCall = x => x.MoveAbility,
            valueSortFunc = (a, b) => a.MoveAbility.CompareTo(b.MoveAbility),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByBuild = new SortTitle<int>()
        {
            name = "建设",
            width = 2.0f,
            valueStrGetCall = x => x.BuildPower.ToString(),
            valueGetCall = x => x.BuildPower,
            valueSortFunc = (a, b) => a.BuildPower.CompareTo(b.BuildPower),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByIntelligence = new SortTitle<int>()
        {
            name = "智力",
            width = 2.0f,
            valueStrGetCall = x => x.Intelligence.ToString(),
            valueGetCall = x => x.Intelligence,
            valueSortFunc = (a, b) => a.Intelligence.CompareTo(b.Intelligence),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByGold = new SortTitle<int>()
        {
            name = "资金",
            width = 2.0f,
            valueStrGetCall = x => x.gold.ToString(),
            valueGetCall = x => x.gold,
            valueSortFunc = (a, b) => a.gold.CompareTo(b.gold),
            valueObjSet = (x, v) => x.gold = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByFood = new SortTitle<int>()
        {
            name = "兵粮",
            width = 2.0f,
            valueStrGetCall = x => x.food.ToString(),
            valueGetCall = x => x.food,
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjSet = (x, v) => x.food = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMorale = new SortTitle<int>()
        {
            name = "气力",
            width = 2.0f,
            valueStrGetCall = x => x.morale.ToString(),
            valueGetCall = x => x.morale,
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMoraleByMax = new SortTitle<int>()
        {
            name = "气力",
            width = 2.0f,
            valueStrGetCall = x => $"{x.morale}/{x.MaxMorale}",
            valueGetCall = x => x.morale,
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByTroops = new SortTitle<int>()
        {
            name = "士兵",
            width = 2.0f,
            valueStrGetCall = x => x.troops.ToString(),
            valueGetCall = x => x.troops,
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjSet = (x, v) => x.troops = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMember1 = new SortTitle<int>()
        {
            name = "副将",
            width = 2.0f,
            valueStrGetCall = x => x.Member1 != null ? x.Member1.Name : "",
            valueGetCall = x => x.Member1?.Id ?? 0,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Member1, b.Member1),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByMember2 = new SortTitle<int>()
        {
            name = "副将",
            width = 2.0f,
            valueStrGetCall = x => x.Member2 != null ? x.Member2.Name : "",
            valueGetCall = x => x.Member2?.Id ?? 0,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Member2, b.Member2),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongForce = new SortTitle<int>()
        {
            name = "势力",
            width = 2.4f,
            valueStrGetCall = x => x.mBelongForce?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongForce, b.mBelongForce),
            valueGetCall = x => x.mBelongForce?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongCorps = new SortTitle<int>()
        {
            name = "军团",
            width = 4.0f,
            valueStrGetCall = x => x.mBelongCorps?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCorps, b.mBelongCorps),
            valueGetCall = x => x.mBelongCorps?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongCity = new SortTitle<int>()
        {
            name = "所属",
            width = 2.4f,
            valueStrGetCall = x => x.mBelongCity?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCity, b.mBelongCity),
            valueGetCall = x => x.mBelongCity?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<SangoObjectList<Person>> SortByCaptiveCount = new SortTitle<SangoObjectList<Person>>()
        {
            name = "俘虏",
            width = 2.4f,
            valueStrGetCall = x => x.captiveList.Count.ToString(),
            valueSortFunc = (a, b) => a.captiveList.Count.CompareTo(b.captiveList.Count),
            valueGetCall = x => x.captiveList,
            valueObjSet = null,
        };

        public static SortTitle<bool> SortByActionOver = new SortTitle<bool>()
        {
            name = "行动",
            width = 2.4f,
            valueStrGetCall = x => x.ActionOver ? "未行动" : "行动完",
            valueSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueGetCall = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        public static SortTitle<bool> SortByState = new SortTitle<bool>()
        {
            name = "状态",
            width = 2.4f,
            valueStrGetCall = x => x.ActionOver ? "行动完" : "未行动",
            valueSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueGetCall = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        public static SortTitle<int> SortByTroopType = new SortTitle<int>()
        {
            name = "兵种",
            width = 2.4f,
            valueStrGetCall = x => x.TroopType.Name,
            valueSortFunc = (a, b) => a.TroopType.Id.CompareTo(b.TroopType.Id),
            valueGetCall = x => x.TroopType.Id,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByAbility = new SortTitle<int>()
        {
            name = "适应",
            width = 2.4f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.TroopTypeLv),
            valueSortFunc = (a, b) => a.TroopTypeLv.CompareTo(b.TroopTypeLv),
            valueGetCall = x => x.TroopTypeLv,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByWaterAbility = new SortTitle<int>()
        {
            name = "水军",
            width = 2.4f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.WaterTroopTypeLv),
            valueSortFunc = (a, b) => a.WaterTroopTypeLv.CompareTo(b.WaterTroopTypeLv),
            valueGetCall = x => x.WaterTroopTypeLv,
            valueObjSet = null,
        };
    }

}
