using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 技能释放格子检查
    /// targetType: 格子情况
    /// result: 结果 0不等 1 相等
    /// hasStatus: 是否拥有该状态 (true/false)
    /// </summary>
    public class SkillTargetCellCheck : Condition
    {
        /// <summary>
        /// 目标类型
        /// </summary>
        string targetType;

        /// <summary>
        /// 结果 0不等 1相等
        /// </summary>
        int result;

        /// <summary>
        /// 初始化部队状态检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            targetType = p.Value<string>("targetType");
            result = p.Value<int>("result");
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="objects">检查条件所需的对象</param>
        /// <returns>条件是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            bool rs = false;
            if (targetType == "troop")
            {
                rs = database.TargetTroop != null;
            }
            else if (targetType == "buildingBase")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null;
            }
            else if (targetType == "cityBase")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null && database.TargetCell.building.IsCityBase();
            }
            else if (targetType == "building")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null && !database.TargetCell.building.IsCityBase();
            }
            else if (targetType == "city")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null && !database.TargetCell.building.IsCity();
            }
            else if (targetType == "port")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null && !database.TargetCell.building.IsPort();
            }
            else if (targetType == "gate")
            {
                rs = database.TargetCell != null && database.TargetCell.building != null && !database.TargetCell.building.IsGate();
            }
            return rs == (result == 1);
        }
    }
}
