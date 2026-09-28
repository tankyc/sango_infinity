using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部队士气检查条件
    /// checkTarget: 检查目标 (self/target)
    /// value: 士气值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class TroopMoraleCheck : Condition
    {
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        bool compareTarget;
        
        /// <summary>
        /// 士气值
        /// </summary>
        int value;
        
        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化部队士气检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
            value = p.Value<int>("value");
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="objects">检查条件所需的对象</param>
        /// <returns>条件是否满足</returns>
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
            
            return GameUtility.CheckCondition(value, @operator, troop.morale);
        }
    }
}
