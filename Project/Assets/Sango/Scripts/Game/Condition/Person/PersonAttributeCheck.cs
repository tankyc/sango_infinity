using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将属性比较条件
    /// attributeType: 属性类型 (command/strength/intelligence/politics/glamour)
    /// compareTarget: 比较目标 (self/target)
    /// value: 比较值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class PersonAttributeCheck : Condition
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
        private int GetAttributeValue(Person person)
        {
            if (person == null)
                return 0;
            
            switch (attributeType)
            {
                case "command":
                    return person.Command;
                case "strength":
                    return person.Strength;
                case "intelligence":
                    return person.Intelligence;
                case "politics":
                    return person.Politics;
                case "glamour":
                    return person.Glamour;
                default:
                    return 0;
            }
        }

        public override bool Check(IConditionDatabase database)
        {
            Person person = null;

            if (compareTarget)
            {
                person = database.ActionPerson;
            }
            else
            {
                person = database.TargetPerson;
            }

            if (person == null)
                return false;

            int actualValue = GetAttributeValue(person);
            return GameUtility.CheckCondition(value, @operator, actualValue);
        }
    }
}
