using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力检查条件
    /// forceId: 势力ID
    /// checkType: 检查类型 (self/ally/enemy/neutral)
    /// </summary>
    public class FactionCheck : Condition
    {
        /// <summary>
        /// 势力ID
        /// </summary>
        int forceId;
        
        /// <summary>
        /// 检查类型 (ally: 盟友, enemy: 敌人)
        /// </summary>
        string checkType;

        /// <summary>
        /// 初始化势力检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            forceId = p.Value<int>("forceId");
            checkType = p.Value<string>("checkType") ?? "self";
        }

        public override bool Check(IConditionDatabase database)
        {
            Force a = database.ActionForce;
            Force b = database.TargetForce;
            switch (checkType)
            {
                case "ally":
                    return IsAlly(a, b);
                case "enemy":
                    return IsEnemy(a, b);
                default:
                    return false;
            }
        }

        /// <summary>
        /// 检查两个部队是否为盟友
        /// </summary>
        /// <param name="troop1">第一个部队</param>
        /// <param name="troop2">第二个部队</param>
        /// <returns>是否为盟友</returns>
        private bool IsAlly(Force troop1, Force troop2)
        {
            if (troop1 == null || troop2 == null)
                return false;

            // 简化实现：同一势力视为盟友
            return troop1.IsAlliance(troop2);
        }

        /// <summary>
        /// 检查两个部队是否为敌人
        /// </summary>
        /// <param name="troop1">第一个部队</param>
        /// <param name="troop2">第二个部队</param>
        /// <returns>是否为敌人</returns>
        private bool IsEnemy(Force troop1, Force troop2)
        {
            if (troop1 == null || troop2 == null)
                return false;
            
            // 简化实现：不同势力视为敌人
            return troop1.IsEnemy(troop2);
        }
    }
}
