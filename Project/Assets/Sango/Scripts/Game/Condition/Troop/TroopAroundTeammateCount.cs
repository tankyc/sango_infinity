using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部队状态检查条件
    /// statusType: 状态类型
    /// checkTarget: 检查目标 (self/target)
    /// hasStatus: 是否拥有该状态 (true/false)
    /// </summary>
    public class TroopAroundTeammateCount : Condition
    {
        /// <summary>
        /// 状态类型
        /// </summary>
        int count;
        
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        bool compareTarget;

        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化部队状态检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            count = p.Value<int>("count");
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
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

            int num = 0;
            troop.cell.GetNeighbors(x =>
            {
                if (x.troop != null && x.troop.IsSameForce(database.ActionTroop))
                    num++;
            });

            return GameUtility.CheckCondition(count, @operator, num);
        }
    }
}
