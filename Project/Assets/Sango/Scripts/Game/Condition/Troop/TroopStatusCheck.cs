using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 部队状态检查条件
    /// statusType: 状态类型
    /// checkTarget: 检查目标 (self/target)
    /// hasStatus: 是否拥有该状态 (true/false)
    /// </summary>
    public class TroopStatusCheck : Condition
    {
        /// <summary>
        /// 状态类型
        /// </summary>
        int statusType;
        
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        bool compareTarget;
        
        /// <summary>
        /// 是否拥有该状态
        /// </summary>
        bool hasStatus;

        /// <summary>
        /// 初始化部队状态检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            statusType = p.Value<int>("statusType");
            compareTarget = (p.Value<string>("compareTarget") ?? "self") == "self";
            hasStatus = p.Value<bool>("hasStatus");
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

            bool has = HasBuffByKind(troop, statusType);
            return has == hasStatus;
        }

        /// <summary>
        /// 检查部队是否拥有指定类型的buff
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="kind">buff类型</param>
        /// <returns>是否拥有该类型的buff</returns>
        private bool HasBuffByKind(Troop troop, int kind)
        {
            if (troop == null || troop.buffManager == null)
                return false;
            
            foreach (var buff in troop.buffManager._buffs)
            {
                if (buff.Buff != null && buff.Buff.kind == kind)
                    return true;
            }
            return false;
        }
    }
}
