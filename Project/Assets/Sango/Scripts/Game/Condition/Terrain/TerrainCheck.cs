using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 地形检查条件
    /// terrainType: 地形类型
    /// checkTarget: 检查目标 (self/target)
    /// </summary>
    public class TerrainCheck : Condition
    {
        /// <summary>
        /// 地形类型
        /// </summary>
        int terrainType;
        
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        string checkTarget;

        /// <summary>
        /// 初始化地形检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            terrainType = p.Value<int>("terrainType");
            checkTarget = p.Value<string>("checkTarget") ?? "self";
        }

        public override bool Check(IConditionDatabase database)
        {
            if (checkTarget == "self" && database.ActionCell != null)
            {
                return database.ActionCell.TerrainType.Id == terrainType;
            }
            else if (checkTarget == "target" && database.TargetCell != null)
            {
                return database.TargetCell.TerrainType.Id == terrainType;
            }
            return false;
        }
    }
}
