using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城市属性检查条件
    /// attributeType: 属性类型 (commerce/agriculture/popularSupport/security/energy/morale/troops/food/gold)
    /// checkTarget: 检查目标 (self/target)
    /// value: 比较值
    /// operator: 比较运算符 (eq/gt/lt/gte/lte)
    /// </summary>
    public class CityAttributeCheck : Condition
    {
        /// <summary>
        /// 属性类型
        /// </summary>
        string attributeType;

        /// <summary>
        /// 检查目标 (self: 自己, target: 目标)
        /// </summary>
        string checkTarget;

        /// <summary>
        /// 比较值
        /// </summary>
        int value;

        /// <summary>
        /// 比较运算符 (eq: 等于, gt: 大于, lt: 小于, gte: 大于等于, lte: 小于等于)
        /// </summary>
        string @operator;

        /// <summary>
        /// 初始化城市属性检查条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            attributeType = p.Value<string>("attributeType");
            checkTarget = p.Value<string>("checkTarget") ?? "self";
            value = p.Value<int>("value");
            @operator = p.Value<string>("operator") ?? "gte";
        }

        /// <summary>
        /// 获取城市的属性值
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <returns>属性值</returns>
        private int GetAttributeValue(City city)
        {
            if (city == null)
                return 0;

            switch (attributeType)
            {
                case "commerce":
                    return city.commerce;
                case "agriculture":
                    return city.agriculture;
                case "popularSupport":
                    return city.popularSupport;
                case "security":
                    return city.security;
                case "energy":
                    return city.energy;
                case "morale":
                    return city.morale;
                case "troops":
                    return city.troops;
                case "food":
                    return city.food;
                case "gold":
                    return city.gold;
                case "population":
                    return city.population;
                case "troopPopulation":
                    return city.troopPopulation;
                default:
                    return 0;
            }
        }

        public override bool Check(IConditionDatabase database)
        {
            City city = null;

            if (checkTarget == "self")
            {
                city = database.ActionCity;
            }
            else if (checkTarget == "target")
            {
                city = database.TargetCity;
            }

            if (city == null)
                return false;

            int actualValue = GetAttributeValue(city);
            return GameUtility.CheckCondition(value, @operator, actualValue);
        }
    }
}
