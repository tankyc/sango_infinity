using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class TroopType : SangoObject
    {
        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty] public int kind;
        
        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 图标
        /// </summary>
        [JsonProperty] public string icon;

        /// <summary>
        /// 模型
        /// </summary>
        [JsonProperty] public int model;

        /// <summary>
        /// 动画
        /// </summary>
        [JsonProperty] public int [] aniIds;

        /// <summary>
        /// 是否战斗
        /// </summary>
        [JsonProperty] public bool isFight;

        // <summary>
        /// 是否远程
        /// </summary>
        [JsonProperty] public bool isRange;

        /// <summary>
        /// 是否是单体(器械)
        /// </summary>
        [JsonProperty] public bool isSingle;

        /// <summary>
        /// 是否为陆地
        /// </summary>
        [JsonProperty] public bool isLand;

        /// <summary>
        /// 攻击力
        /// </summary>
        [JsonProperty] public int atk;

        /// <summary>
        /// 耐久破坏力
        /// </summary>
        [JsonProperty] public int durabilityDmg;

        /// <summary>
        /// 防御力
        /// </summary>
        [JsonProperty] public int def;

        /// <summary>
        /// 移动力
        /// </summary>
        [JsonProperty] public int move;

        /// <summary>
        /// 护甲类型
        /// </summary>
        [JsonProperty] public int defType;

        /// <summary>
        /// 攻击类型
        /// </summary>
        [JsonProperty] public int atkType;

        /// <summary>
        /// 战斗力
        /// </summary>
        [JsonProperty] public int fightPower;

        /// <summary>
        /// 挂钩能力
        /// </summary>
        [JsonProperty] public int influenceAbility;

        /// <summary>
        /// 初始技能
        /// </summary>
        [JsonProperty] public List<int> skills = new List<int>();

        /// <summary>
        /// 组建消耗的道具(兵器,战马,兵符等)
        /// </summary>
        [JsonProperty] public List<int> costItems;

        /// <summary>
        /// 可组建必须的兵符道具Id
        /// </summary>
        [JsonProperty] public int validItemId;

        /// <summary>
        /// 可组建必须的科技Id
        /// </summary>
        [JsonProperty] public int validTechId;

        /// <summary>
        /// 对于每种地形的移动消耗值
        /// </summary>
        [JsonProperty] public List<byte> terrainCost = new List<byte>();

        /// <summary>
        /// 可组建条件(兵符,特定城市等)
        /// </summary>
        [JsonProperty] public Condition.Condition activeCondition;

        /// <summary>
        /// 粮食消耗倍率
        /// </summary>
        [JsonProperty] public float foodCostFactor = 1;

        /// <summary>
        /// 招募每兵所需额外食物
        /// </summary>
        [JsonProperty] public int costFood = 0;

        /// <summary>
        /// 招募每兵所需额外金币
        /// </summary>
        [JsonProperty] public int costGold = 0;

        /// <summary>
        /// 招募每兵所需额外兵役人口
        /// </summary>
        [JsonProperty] public int costPopulation = 0;

        /// <summary>
        /// 招募每兵所需技巧点数/兵
        /// </summary>
        [JsonProperty] public int costTechPoint = 0;

        /// <summary>
        /// 招募每兵所需对应经验点数/兵
        /// </summary>
        [JsonProperty] public int costExpPoint = 0;

        public int MoveCost(Cell cell)
        {
            int terrainId = cell.TerrainType.Id;
            if (terrainId < terrainCost.Count)
                return terrainCost[terrainId];

            return terrainCost[0];
        }
    }
}
