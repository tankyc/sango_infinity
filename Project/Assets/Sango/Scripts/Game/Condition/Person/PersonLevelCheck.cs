using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将等级检查条件
    /// checkTarget: 检查目标 (self/target)
    /// level: 等级值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class PersonLevelCheck : Condition
    {
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        string checkTarget;
        
        /// <summary>
        /// 等级值
        /// </summary>
        int level;
        
        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化武将等级检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            checkTarget = p.Value<string>("checkTarget") ?? "self";
            level = p.Value<int>("level");
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="objects">检查条件所需的对象</param>
        /// <returns>条件是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = null;

            if (checkTarget == "self")
            {
                person = database.ActionPerson;
            }
            else if (checkTarget == "target")
            {
                person = database.TargetPerson;
            }

            if (person == null || person.Level == null)
                return false;

            return GameUtility.CheckCondition(level, @operator, person.Level.Id);
        }
    }
}
