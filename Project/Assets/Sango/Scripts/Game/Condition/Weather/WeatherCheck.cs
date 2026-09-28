using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 天气检查条件
    /// weatherType: 天气类型
    /// checkTarget: 检查目标 (self/target)
    /// </summary>
    public class WeatherCheck : Condition
    {
        /// <summary>
        /// 天气类型
        /// </summary>
        int weatherType;
        
        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        string checkTarget;

        /// <summary>
        /// 初始化天气检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            weatherType = p.Value<int>("weatherType");
            checkTarget = p.Value<string>("checkTarget") ?? "self";
        }

        public override bool Check(IConditionDatabase database)
        {
            if (checkTarget == "self" && database.ActionCell != null )
            {
                return database.ActionCell.weatherType == weatherType;
            }
            else if (checkTarget == "target" && database.TargetCell != null)
            {
                return database.TargetCell.weatherType == weatherType;
            }
            return false;
        }

    }
}
