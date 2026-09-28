using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 距离检查条件
    /// distance: 距离值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// distanceType: 距离类型 (manhattan/euclidean)
    /// </summary>
    public class DistanceCheck : Condition
    {
        /// <summary>
        /// 距离值
        /// </summary>
        int distance;
        
        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;
        
        /// <summary>
        /// 初始化距离检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            distance = p.Value<int>("distance");
            @operator = p.Value<string>("operator") ?? "eq";
        }

        public override bool Check(IConditionDatabase database)
        {
            if(database.ActionCell == null || database.TargetCell == null)
                return false;
            int dis = database.ActionCell.Distance(database.TargetCell);
            return GameUtility.CheckCondition(distance, @operator, dis);
        }

        /// <summary>
        /// 根据运算符检查距离是否满足条件
        /// </summary>
        /// <param name="actualDistance">实际距离</param>
        /// <returns>条件是否满足</returns>
        private bool CheckDistance(int actualDistance)
        {
            if (actualDistance < 0)
                return false;
            
            switch (@operator)
            {
                case "eq":
                    return actualDistance == distance;
                case "gt":
                    return actualDistance > distance;
                case "lt":
                    return actualDistance < distance;
                case "gte":
                    return actualDistance >= distance;
                case "lte":
                    return actualDistance <= distance;
                default:
                    return false;
            }
        }
    }
}
