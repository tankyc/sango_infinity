using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class CityLevelType : SangoObject
    {
        /// <summary>
        /// 增益范围
        /// </summary>
        [JsonProperty] public int buffRange;

        /// <summary>
        /// 可容纳兵力
        /// </summary>
        [JsonProperty] public int maxTroops;

        /// <summary>
        /// 仓库大小
        /// </summary>
        [JsonProperty] public int storeMax;

        /// <summary>
        /// 金库大小
        /// </summary>
        [JsonProperty] public int maxGold;

        /// <summary>
        /// 粮仓大小
        /// </summary>
        [JsonProperty] public int maxFood;

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
        /// 城内建筑槽位
        /// </summary>
        [JsonProperty] public int insideSlot;

        /// <summary>
        /// 城外建筑槽位
        /// </summary>
        [JsonProperty] public int outsideSlot;

        /// <summary>
        /// 村庄槽位
        /// </summary>
        [JsonProperty] public int villageSlot;

        /// <summary>
        /// 基础金钱收入 基础收入 = 基础收入 * 当前商业值 / 最大商业值
        /// </summary>
        [JsonProperty] public int baseGainGold;

        /// <summary>
        /// 基础粮食收入 基础收入 = 基础粮食收入 * 当前农业值 / 最大农业值
        /// </summary>
        [JsonProperty] public int baseGainFood;

        /// <summary>
        /// 最大商业值
        /// </summary>
        [JsonProperty] public int maxCommerce;

        /// <summary>
        /// 最大农业值
        /// </summary>
        [JsonProperty] public int maxAgriculture;

        /// <summary>
        /// 最大耐久
        /// </summary>
        [JsonProperty] public int maxDurability;

        //TODO:升级所需的额外条件
        [JsonProperty] public Condition.Condition levelUpCondition;

    }
}
