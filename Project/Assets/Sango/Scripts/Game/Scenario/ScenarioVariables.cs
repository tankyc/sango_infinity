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
        [JsonProperty] public float fight_base_damage = 64;

        /// <summary>
        /// 基准兵力(攻守兵力差)
        /// </summary>
        [JsonProperty] public float fight_base_troops_need = 2000;

        /// <summary>
        /// 每多基准兵力,获得一次兵力系数增益
        /// </summary>
        [JsonProperty] public float fight_base_troop_count = 200;

        /// <summary>
        /// 兵力系数增益
        /// </summary>
        [JsonProperty] public double fight_damage_magic_number = 0.000476190455;

        /// <summary>
        /// 伤害难度系数
        /// </summary>
        [JsonProperty] public float[] fight_damage_difficulty_factor = new float[] { 1.3f, 1, 0.7f };

        /// <summary>
        /// 难度
        /// </summary>
        [JsonProperty] public int difficulty = 1;


        ///// <summary>
        ///// 士气最多影响比例
        ///// </summary>
        //[JsonProperty] public float fight_morale_decay_percent = 0.5f;

        ///// <summary>
        ///// 士气基准值
        ///// </summary>
        //[JsonProperty] public float fight_morale_decay_below = 80;

        ///// <summary>
        ///// 每多基准值多少,获得一次士气加成
        ///// </summary>
        //[JsonProperty] public float fight_base_morale_increase_count = 20;

        ///// <summary>
        ///// 士气矫正加成
        ///// </summary>
        //[JsonProperty] public float fight_morale_add = 0.15f;

        ///// <summary>
        ///// 最大减伤比例
        ///// </summary>
        //[JsonProperty] public float fight_base_reduce_percent = 0.3f;

        /// <summary>
        /// 部队攻击武力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_attack_strength_factor = 7000;
        /// <summary>
        /// 部队攻击智力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_attack_intelligence_factor = 1000;
        /// <summary>
        /// 部队攻击统率影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_attack_command_factor = 2000;
        /// <summary>
        /// 部队攻击政治影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_attack_politics_factor = 0;
        /// <summary>
        /// 部队攻击魅力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_attack_glamour_factor = 0;

        /// <summary>
        /// 部队防御武力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_defence_strength_factor = 1000;
        /// <summary>
        /// 部队防御智力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_defence_intelligence_factor = 2000;
        /// <summary>
        /// 部队防御统率影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_defence_command_factor = 7000;
        /// <summary>
        /// 部队防御政治影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_defence_politics_factor = 0;
        /// <summary>
        /// 部队防御魅力影响比例(万分比)
        /// </summary>
        [JsonProperty] public int fight_troop_defence_glamour_factor = 0;

        /// <summary>
        /// 适应能力加成(百分比)
        /// </summary>
        [JsonProperty]
        public int[] troops_adaptation_level_boost = new int[]
         // C    B        A       S        SS
           {80,   90,     100,   110,    120, };

        /// <summary>
        /// 兵种克制(小数)
        /// </summary>
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
        /// 工作花费 {农业,商业,巡视,训练,搜索,招募士兵,招募武将,生产兵装, 建造, 生产器具, 生产船, 生产马}
        /// </summary>
        [JsonProperty]
        public int[] jobCost = new int[] { 200, 200, 100, 0, 0, 300, 0, 200, 0, 0, 0, 0 };
        /// <summary>
        /// 工作人数限制 {农业,商业,巡视,训练,搜索,招募士兵,招募武将,生产兵装, 建造, 生产器具, 生产船, 生产马}
        /// </summary>
        [JsonProperty]
        public int[] jobMaxPersonCount = new int[] { 3, 3, 3, 3, 1, 3, 1, 3, 3, 3, 3, 3 };
        /// <summary>
        /// 工作获取的功绩 {农业,商业,巡视,训练,搜索,招募士兵,招募武将,生产兵装, 建造, 生产器具, 生产船, 生产马}
        /// </summary>
        [JsonProperty]
        public int[] jobMeritGain = new int[] { 5, 5, 5, 5, 5, 10, 20, 20, 20, 20, 20, 20 };
        /// <summary>
        /// 工作获取的技巧点 {农业,商业,巡视,训练,搜索,招募士兵,招募武将,生产兵装, 建造, 生产器具, 生产船, 生产马}
        /// </summary>
        [JsonProperty]
        public int[] jobTechniquePoint = new int[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };

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

        public float DifficultyDamageFactor
        {
            get
            {
                if (difficulty >= 0 && difficulty < fight_damage_difficulty_factor.Length)
                {
                    return fight_damage_difficulty_factor[difficulty];
                }
                return 1;
            }
        }
    }
}
