using Newtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CityLevelType : SangoObject
    {
        /// <summary>
        /// 升级所需金钱
        /// </summary>
        [JsonProperty] public int costGold;

        /// <summary>
        /// 升级所需技巧点
        /// </summary>
        [JsonProperty] public int costTechPoint;

        /// <summary>
        /// 升级需要达到民心
        /// </summary>
        [JsonProperty] public int needPopularSupport;

        /// <summary>
        /// 增益范围
        /// </summary>
        [JsonProperty] public int buffRange;

        /// <summary>
        /// 可容纳兵力
        /// </summary>
        [JsonProperty] public int troopsLimitAdd;

        /// <summary>
        /// 仓库大小
        /// </summary>
        [JsonProperty] public int storeLimitAdd;

        /// <summary>
        /// 金库大小
        /// </summary>
        [JsonProperty] public int goldLimitAdd;

        /// <summary>
        /// 粮仓大小
        /// </summary>
        [JsonProperty] public int foodLimitAdd;

        /// <summary>
        /// 城内建筑槽位
        /// </summary>
        [JsonProperty] public int insideSlotAdd;

        /// <summary>
        /// 城外建筑槽位
        /// </summary>
        [JsonProperty] public int outsideSlotAdd;

        /// <summary>
        /// 村庄槽位
        /// </summary>
        [JsonProperty] public int villageSlotAdd;

        /// <summary>
        /// 基础金钱收入 基础收入 = 基础收入 * 当前商业值 / 最大商业值
        /// </summary>
        [JsonProperty] public int baseGainGoldAdd;

        /// <summary>
        /// 基础粮食收入 基础收入 = 基础粮食收入 * 当前农业值 / 最大农业值
        /// </summary>
        [JsonProperty] public int baseGainFoodAdd;

        /// <summary>
        /// 最大商业值
        /// </summary>
        [JsonProperty] public int commerceLimitAdd;

        /// <summary>
        /// 最大农业值
        /// </summary>
        [JsonProperty] public int agricultureLimitAdd;

        /// <summary>
        /// 最大耐久
        /// </summary>
        [JsonProperty] public int durabilityLimitAdd;

        //TODO:升级所需的额外条件
        //[JsonProperty] public Condition.Condition levelUpCondition;

    }
}
