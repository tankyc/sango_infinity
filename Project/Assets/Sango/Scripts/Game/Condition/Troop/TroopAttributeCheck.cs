using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部队属性比较条件
    /// attributeType: 属性类型 (command/strength/intelligence/politics/glamour)
    /// compareTarget: 比较目标 (self/target)
    /// value: 比较值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class TroopAttributeCheck : Condition
    {
        /// <summary>
        /// 属性类型
        /// </summary>
        string attributeType;

        /// <summary>
        /// 比较目标 (self: 自己, target: 目标)
        /// </summary>
        bool compareTarget;

        /// <summary>
        /// 比较值
        /// </summary>
        int value;

        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化武将属性比较条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            attributeType = p.Value<string>("attributeType");
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
            value = p.Value<int>("value");
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 获取武将的属性值
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>属性值</returns>
        private int GetAttributeValue(Troop troop)
        {
            if (troop == null)
                return 0;

            switch (attributeType)
            {
                case "command":
                    return troop.Command;
                case "strength":
                    return troop.Strength;
                case "intelligence":
                    return troop.Intelligence;
                case "politics":
                    return troop.Politics;
                case "glamour":
                    return troop.Glamour;
                case "attack":
                    return troop.Attack;
                case "defence":
                    return troop.Defence;
                case "troops":
                    return troop.troops;
                default:
                    return 0;
            }
        }

        public override bool Check(IConditionDatabase database)
        {
            Troop troop = null;

            if (compareTarget)
            {
                troop = database.ActionTroop;
            }
            else
            {
                troop = database.TargetTroop;
            }

            if (troop == null)
                return false;

            int actualValue = GetAttributeValue(troop);
            return GameUtility.CheckCondition(value, @operator, actualValue);
        }
    }
}
