using System.IO;
using Newtonsoft.Json;
using UnityEngine;


namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioVariables
    {
        /// <summary>
        /// 真实年龄开关
        /// </summary>
        [JsonProperty] public bool AgeEnabled = true;

        /// <summary>
        /// 能力随年龄变化
        /// </summary>
        [JsonProperty] public bool EnableAgeAbilityFactor = true;

        /// <summary>
        /// 能力每级经验
        /// </summary>
        [JsonProperty] public ushort AbilityExpLevelNeed = 1000;

        /// <summary>
        /// 最高能力等级
        /// </summary>
        [JsonProperty] public byte MaxAbilityLevel = 10;

        /// <summary>
        /// 属性每点经验
        /// </summary>
        [JsonProperty] public ushort AttributeExpLevelNeed = 250;

        /// <summary>
        /// 属性成长不超过这个点数
        /// </summary>
        [JsonProperty] public byte MaxAttributeGet = 30;

        /// <summary>
        /// 基础伤害
        /// </summary>
        [JsonProperty] public float fight_base_durability_damage = 4;

        /// <summary>
        /// 基础伤害
        /// </summary>
        [JsonProperty] public float fight_base_damage = 8;

        /// <summary>
        /// 基准兵力
        /// </summary>
        [JsonProperty] public float fight_base_troops_need = 3000;

        /// <summary>
        /// 每多基准兵力,获得一次兵力系数增益
        /// </summary>
        [JsonProperty] public float fight_base_troop_count = 200;

        /// <summary>
        /// 兵力系数增益
        /// </summary>
        [JsonProperty] public float fight_base_troop_factor_per_count = 0.05f;

        /// <summary>
        /// 伤害由基准武力,影响比例
        /// </summary>
        [JsonProperty] public float fight_base_strength_damage_factor = 0.8f;

        /// <summary>
        /// 伤害由基准智力,影响比例
        /// </summary>
        [JsonProperty] public float fight_base_intelligence_damage_factor = 0.2f;

        /// <summary>
        /// 士气最多影响比例
        /// </summary>
        [JsonProperty] public float fight_morale_decay_percent = 0.5f;

        /// <summary>
        /// 士气基准值
        /// </summary>
        [JsonProperty] public float fight_morale_decay_below = 80;

        /// <summary>
        /// 每多基准值多少,获得一次士气加成
        /// </summary>
        [JsonProperty] public float fight_base_morale_increase_count = 20;

        /// <summary>
        /// 士气矫正加成
        /// </summary>
        [JsonProperty] public float fight_morale_add = 0.15f;

        /// <summary>
        /// 最大减伤比例
        /// </summary>
        [JsonProperty] public float fight_base_reduce_percent = 0.5f;

        /// <summary>
        /// 攻城伤害由基准武力,影响比例
        /// </summary>
        [JsonProperty] public float fight_durability_base_strength_damage_factor = 0.2f;

        /// <summary>
        /// 攻城伤害由基准智力,影响比例
        /// </summary>
        [JsonProperty] public float fight_durability_base_intelligence_damage_factor = 0.2f;


        [JsonProperty]
        public float[] troops_adaptation_level_boost = new float[]
         // C    B        A       S        SS
           {0.8f,   0.9f,     1f,   1.1f,    1.2f, };
        [JsonProperty]
        public float[][] troops_type_restraint = new float[][]{

        //////////1   2   3   4   5   6   7   8   9   10  11  12  13  14
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        new float[] {1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1,  1},
        };
        /// <summary>
        /// 人口系统开关
        /// </summary>
        [JsonProperty] public bool populationEnable = false;

        /// <summary>
        /// 基础人口增长率
        /// </summary>
        [JsonProperty] public float populationIncreaseBaseFactor = 0.0113f;

        /// <summary>
        /// 队伍粮食基础消耗率
        /// </summary>
        [JsonProperty] public float baseFoodCostInTroop = 0.1f;

        /// <summary>
        /// 城池中粮食基础消耗率(每回合)
        /// </summary>
        [JsonProperty] public float baseFoodCostInCity = 0.025f;
        /// <summary>
        /// 城池缺粮后每回合逃跑的士兵比例
        /// </summary>
        [JsonProperty] public float runawayWhenCityFoodNotEnough = 0.1f;

        ///// <summary>
        ///// 每点商业对应的金币收入(月)
        ///// </summary>
        //[JsonProperty] public float eachCommercePointToGold = 0.3f;

        ///// <summary>
        ///// 每点开发对应的粮食收入(秋季)
        ///// </summary>
        //[JsonProperty] public float eachAgriculturePointToFood = 15.6f;

        /// <summary>
        /// 民心对于收入的影响最低值
        /// </summary>
        [JsonProperty] public float popularSupportInfluenceMax = 60;

        /// <summary>
        /// 民心影响的正负范围
        /// </summary>
        [JsonProperty] public float popularSupportInfluence = 0.2f;

        /// <summary>
        /// 治安对于收入的影响最低值
        /// </summary>
        [JsonProperty] public float securityInfluenceMax = 70;

        /// <summary>
        /// 治安影响的正负范围
        /// </summary>
        [JsonProperty] public float securityInfluence = 0.1f;

        /// <summary>
        /// 开发花费
        /// </summary>
        [JsonProperty] public int developCost = 200;

        /// <summary>
        /// 开发最大人数/回合
        /// </summary>
        [JsonProperty] public int developMaxPersonCount = 3;

        /// <summary>
        /// 农业花费/人
        /// </summary>
        [JsonProperty] public int farmingCost = 200;

        /// <summary>
        /// 农业最大人数/回合
        /// </summary>
        [JsonProperty] public int farmingMaxPersonCount = 3;

        /// <summary>
        /// 巡视花费/人
        /// </summary>
        [JsonProperty] public int inspectionCost = 200;

        /// <summary>
        /// 巡视最大武将数/回合
        /// </summary>
        [JsonProperty] public int inspectionMaxPersonCount = 3;

        /// <summary>
        /// 招募花费/人
        /// </summary>
        [JsonProperty] public int recuritTroopCost = 200;

        /// <summary>
        /// 招募最大武将数/回合
        /// </summary>
        [JsonProperty] public int recuritMaxPersonCount = 3;

        /// <summary>
        /// 粮食倍率
        /// </summary>
        [JsonProperty] public float foodFactor = 2f;

        /// <summary>
        /// 金币倍率
        /// </summary>
        [JsonProperty] public float goldFactor = 1f;

        /// <summary>
        /// 每月变化的关系值
        /// </summary>
        [JsonProperty] public short relationChangePerMonth = -200;

        /// <summary>
        /// 每月的关系变化率
        /// </summary>
        [JsonProperty] public ushort relationChangeChangce = 50;

        /// <summary>
        /// 破城时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenCityFall = 30;

        /// <summary>
        /// 最后一城时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenLastCityFall = 80;

        /// <summary>
        /// 队伍溃败时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenTroopFall = 5;

    }
}
