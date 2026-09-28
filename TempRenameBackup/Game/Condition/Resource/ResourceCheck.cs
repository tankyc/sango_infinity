//using TKNewtonsoft.Json.Linq;
//using System.Collections.Generic;

//namespace Sango.Core
//{
//    /// <summary>
//    /// 资源检查条件
//    /// resourceType: 资源类型 (gold/food/wood/stone/iron)
//    /// amount: 资源数量
//    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
//    /// </summary>
//    public class ResourceCheck : Condition
//    {
//        /// <summary>
//        /// 资源类型
//        /// </summary>
//        string resourceType;
        
//        /// <summary>
//        /// 资源数量
//        /// </summary>
//        int amount;
        
//        /// <summary>
//        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
//        /// </summary>
//        string @operator;

//        /// <summary>
//        /// 初始化资源检查条件
//        /// </summary>
//        /// <param name="p">JSON参数对象</param>
//        /// <param name="sangoObjects">相关的游戏对象</param>
//        public override void Init(JObject p, params SangoObject[] sangoObjects)
//        {
//            resourceType = p.Value<string>("resourceType");
//            amount = p.Value<int>("amount");
//            @operator = p.Value<string>("operator") ?? "gte";
//        }

//        /// <summary>
//        /// 检查条件是否满足
//        /// </summary>
//        /// <param name="objects">检查条件所需的对象</param>
//        /// <returns>条件是否满足</returns>
//        public override bool Check(params object[] objects)
//        {
//            if (objects.Length < 1)
//                return false;
            
//            Troop troop = objects[0] as Troop;
//            if (troop == null)
//                return false;
            
//            int currentAmount = GetResourceAmount(troop);
//            return CheckResource(currentAmount);
//        }

//        /// <summary>
//        /// 检查部队、目标和技能相关的条件
//        /// </summary>
//        /// <param name="troop">部队对象</param>
//        /// <param name="target">目标部队</param>
//        /// <param name="skill">技能实例</param>
//        /// <returns>条件是否满足</returns>
//        public override bool Check(Troop troop, Troop target, SkillInstance skill)
//        {
//            if (troop == null)
//                return false;
            
//            int currentAmount = GetResourceAmount(troop);
//            return CheckResource(currentAmount);
//        }

//        /// <summary>
//        /// 检查技能实例、部队、法术单元格和攻击单元格列表相关的条件
//        /// </summary>
//        /// <param name="skillInstance">技能实例</param>
//        /// <param name="troop">部队对象</param>
//        /// <param name="spellCell">法术单元格</param>
//        /// <param name="atkCellList">攻击单元格列表</param>
//        /// <returns>条件是否满足</returns>
//        public override bool Check(SkillInstance skillInstance, Troop troop, Cell spellCell, List<Cell> atkCellList)
//        {
//            if (troop == null)
//                return false;
            
//            int currentAmount = GetResourceAmount(troop);
//            return CheckResource(currentAmount);
//        }

//        /// <summary>
//        /// 获取部队所属势力的资源数量
//        /// </summary>
//        /// <param name="troop">部队对象</param>
//        /// <returns>资源数量</returns>
//        private int GetResourceAmount(Troop troop)
//        {
//            if (troop == null || troop.BelongForce == null)
//                return 0;
            
//            // 这里简化实现，实际应该从势力的资源管理器中获取
//            // 假设每个部队都有一个资源管理器的引用
//            // 或者通过全局的资源管理系统获取
            
//            // 示例实现：返回0，实际项目中需要根据具体架构实现
//            return 0;
//        }

//        /// <summary>
//        /// 根据运算符检查资源是否满足条件
//        /// </summary>
//        /// <param name="currentAmount">当前资源数量</param>
//        /// <returns>条件是否满足</returns>
//        private bool CheckResource(int currentAmount)
//        {
//            switch (@operator)
//            {
//                case "eq":
//                    return currentAmount == amount;
//                case "gt":
//                    return currentAmount > amount;
//                case "lt":
//                    return currentAmount < amount;
//                case "gte":
//                    return currentAmount >= amount;
//                case "lte":
//                    return currentAmount <= amount;
//                default:
//                    return false;
//            }
//        }
//    }
//}
