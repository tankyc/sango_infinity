using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 部队兵种适性检查条件
    /// abilityType: 兵种适性类型 (spear/halberd/crossbow/ride/machine/water/current)
    ///   current (或留空) 表示取该部队当前所处水陆环境对应兵种的适性(Troop.TroopTypeLv)
    /// compareTarget: 检查目标 (self: 自己, target: 目标)
    /// value: 比较的适性等级(数值越大适性越好)
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class TroopAbilityCheck : Condition
    {
        /// <summary>
        /// 兵种适性类型,对应 AbilityType 的小写名称,current 表示当前地形所属兵种
        /// </summary>
        string abilityType;

        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        bool compareTarget;

        /// <summary>
        /// 比较的适性等级
        /// </summary>
        int value;

        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化部队兵种适性检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 未配置时默认取当前地形所属兵种的适性
            abilityType = (p.Value<string>("abilityType") ?? "current").ToLowerInvariant();
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
            value = p.Value<int>("value");
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="database">检查条件所需的对象</param>
        /// <returns>条件是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            // 按 compareTarget 决定检查哪一支部队
            Troop troop = compareTarget ? database.ActionTroop : database.TargetTroop;
            if (troop == null)
                return false;

            int actualValue = GetAbilityLevel(troop);
            return GameUtility.CheckCondition(value, @operator, actualValue);
        }

        /// <summary>
        /// 获取部队的兵种适性等级
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>适性等级(数值越大越好)</returns>
        private int GetAbilityLevel(Troop troop)
        {
            // 当前地形所属兵种:部队已经按水陆环境缓存了对应适性
            if (string.IsNullOrEmpty(abilityType) || abilityType == "current")
                return troop.TroopTypeLv;

            // 指定兵种适性:与 LandTroopTypeLv/WaterTroopTypeLv 的计算口径一致,取部队全体武将的最大适性
            AbilityType type = ParseAbilityType(abilityType);
            if (type == AbilityType.None)
                return 0;

            int level = -1;
            troop.ForEachPerson(person =>
            {
                if (person != null)
                    level = System.Math.Max(level, GetPersonAbilityLevel(person, type));
            });

            return level < 0 ? 0 : level;
        }

        /// <summary>
        /// 兵种适性名称解析为枚举
        /// </summary>
        /// <param name="name">适性名称(spear/halberd/crossbow/ride/machine/water)</param>
        /// <returns>适性类型,无法识别时返回 None</returns>
        private static AbilityType ParseAbilityType(string name)
        {
            switch (name)
            {
                case "spear": return AbilityType.Spear;
                case "halberd": return AbilityType.Halberd;
                case "crossbow": return AbilityType.Crossbow;
                case "ride": return AbilityType.Ride;
                case "machine": return AbilityType.Machine;
                case "water": return AbilityType.Water;
                default: return AbilityType.None;
            }
        }

        /// <summary>
        /// 获取武将指定兵种的适性等级
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <param name="type">适性类型</param>
        /// <returns>适性等级</returns>
        private static int GetPersonAbilityLevel(Person person, AbilityType type)
        {
            switch (type)
            {
                case AbilityType.Spear: return person.SpearLv;
                case AbilityType.Halberd: return person.HalberdLv;
                case AbilityType.Crossbow: return person.CrossbowLv;
                case AbilityType.Ride: return person.RideLv;
                case AbilityType.Machine: return person.MachineLv;
                case AbilityType.Water: return person.WaterLv;
                default: return 0;
            }
        }
    }
}
