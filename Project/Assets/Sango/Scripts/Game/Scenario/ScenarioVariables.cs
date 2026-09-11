using System.IO;
using TKNewtonsoft.Json;
using UnityEngine;


namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioVariables : SangoObjectExtensionData
    {
        /// <summary>
        /// 最大可存储的行动力上限
        /// </summary>
        [JsonProperty] public int ActionPointLimit = 255;

        /// <summary>
        /// 行动力获取倍率
        /// </summary>
        [JsonProperty] public float ActionPointFactor = 1;

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
        [JsonProperty] public float[] fight_damage_difficulty_factor = new float[] { 1.3f, 1, 0.85f, 0.7f };

        /// <summary>
        /// 难度
        /// </summary>
        [JsonProperty] public int difficulty = 1;

        /// <summary>
        /// AI每回合获得的钱
        /// </summary>
        [JsonProperty] public int aiTurnAddGold = 0;

        /// <summary>
        /// AI每回合获得的粮
        /// </summary>
        [JsonProperty] public int aiTurnAddFood = 0;

        /// <summary>
        /// AI每回合获得的士兵
        /// </summary>
        [JsonProperty] public int aiTurnAddTroops = 0;

        /// <summary>
        /// AI每回合获得的兵装（非器械、非船）
        /// </summary>
        [JsonProperty] public int aiTurnAddArms = 0;

        /// <summary>
        /// 可招募的忠诚度,高于或等于此数值不可招募
        /// </summary>
        [JsonProperty] public float recruitableLine = 95;

        /// <summary>
        /// 玩家招募加成
        /// </summary>
        [JsonProperty] public int playerRecruitAdd = 0;

        /// <summary>
        /// 每一点农业带来的粮食收入
        /// </summary>
        [JsonProperty] public int agriculture_add_food = 10;

        /// <summary>
        /// 每一点商业点带来的金币收入
        /// </summary>
        [JsonProperty] public int commerce_add_gold = 1;

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
           {80,   90,    100,   110,    120, };

        /// <summary>
        /// 兵种克制(小数)
        /// </summary>
        [JsonProperty]
        public float[][] troops_type_restraint = new float[][]{

        //////////   0,   1杂     2枪    3戟    4弩      5骑         6运     7水    8冲      9井
        new float[] {0,   0,     0,       0,      0,       0,        0,      0,      0,       0  },      //0
        new float[] {0,   1,     1,       1,      1,       1,        1,      1,      1,       1  },      //1杂
        new float[] {0,   1,     1,       1,      1,       1.25f,    1,      1,      1,       1  },      //2枪
        new float[] {0,   1,     1,       1,      1.25f,   1,        1,      1,      1,       1  },      //3戟
        new float[] {0,   1,     1.25f,   1,      1,       1,        1,      1,      1,       1  },      //4弩
        new float[] {0,   1,     1,       1.25f,  1,       1,        1,      1,      1,       1  },      //5骑
        new float[] {0,   1,     1,       1,      1,       1,        1,      1,      1,       1  },      //6运
        new float[] {0,   1,     1,       1,      1,       1,        1,      1,      1,       1  },      //7水
        new float[] {0,   1,     1,       1,      1,       1,        1,      1,      1,       1  },      //8冲
        new float[] {0,   1,     1,       1,      1,       1,        1,      1,      1,       1  },      //9井
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
        /// 人口上限基础值
        /// </summary>
        [JsonProperty] public int populationLimitBase = 10000;

        /// <summary>
        /// 每级城市人口上限增加值
        /// </summary>
        [JsonProperty] public int populationLimitPerLevel = 5000;

        /// <summary>
        /// 基础兵役比例
        /// </summary>
        [JsonProperty] public float baseTroopPopulationRatio = 0.3f;

        /// <summary>
        /// 最大兵役比例
        /// </summary>
        [JsonProperty] public float maxTroopPopulationRatio = 0.6f;

        /// <summary>
        /// 人口对粮食消耗的影响系数
        /// </summary>
        [JsonProperty] public float populationFoodCostFactor = 0.001f;

        /// <summary>
        /// 人口对金钱收入的影响系数
        /// </summary>
        [JsonProperty] public float populationGoldIncomeFactor = 0.0005f;

        /// <summary>
        /// 队伍粮食基础消耗率 1粮养10兵每回合
        /// </summary>
        [JsonProperty] public float baseFoodCostInTroop = 0.1f;

        /// <summary>
        /// 城池中粮食基础消耗率(每回合) 1粮养40兵每回合
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
        /// 治安每一点对征兵的影响值比例
        /// </summary>
        [JsonProperty] public float securityInfluenceRecruitTroops = 0.005f;

        /// <summary>
        /// 建筑最大回合数
        /// </summary>
        [JsonProperty] public int BuildMaxTurn = 10;

        /// <summary>
        /// 玩家保护回合(不会受电脑攻击)
        /// </summary>
        [JsonProperty] public int AIAttackProtectedCount = 12;

        /// <summary>
        /// 电脑粮食倍率
        /// </summary>
        [JsonProperty] public float foodFactor = 1f;

        /// <summary>
        /// 电脑资金倍率
        /// </summary>
        [JsonProperty] public float goldFactor = 1f;

        /// <summary>
        /// 玩家粮食倍率
        /// </summary>
        [JsonProperty] public float playerFoodFactor = 1f;

        /// <summary>
        /// 玩家资金倍率
        /// </summary>
        [JsonProperty] public float playerGoldFactor = 1f;

        /// <summary>
        /// 每月变化的关系值
        /// </summary>
        [JsonProperty] public int relationChangePerMonth = -200;

        /// <summary>
        /// 每月的关系变化率
        /// </summary>
        [JsonProperty] public int relationChangeChance = 50;

        /// <summary>
        /// 破城时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenCityFall = 10;

        /// <summary>
        /// 最后一城时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenLastCityFall = 40;

        /// <summary>
        /// 队伍溃败时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenTroopFall = 5;

        /// <summary>
        /// 进攻时候留守最低的兵力
        /// </summary>
        [JsonProperty] public int minTroopsKeepWhenAttack = 30000;

        /// <summary>
        /// 进攻时候留守最低的粮食
        /// </summary>
        [JsonProperty] public int minFoodKeepWhenAttack = 10000;

        /// <summary>
        /// 防御时候留守最低的兵力
        /// </summary>
        [JsonProperty] public int minTroopsKeepWhenDefence = 6000;

        /// <summary>
        /// 防御时候留守最低的粮食
        /// </summary>
        [JsonProperty] public int minFoodKeepWhenDefence = 1000;

        /// <summary>
        /// 近战击溃部队缴获的金钱比例(百分比)
        /// </summary>
        [JsonProperty] public int defeatTroopCanGainGoldFactor = 60;

        /// <summary>
        /// 近战击溃部队缴获的粮食比例(百分比)
        /// </summary>
        [JsonProperty] public int defeatTroopCanGainFoodFactor = 30;

        /// <summary>
        /// 城市沦陷可以保留金钱比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepGoldFactor = new int[4] { 1, 2, 4, 1 };

        /// <summary>
        /// 城市沦陷可以保留粮食比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepFoodFactor = new int[4] { 1, 2, 4, 1 };

        /// <summary>
        /// 城市沦陷可以保留士兵比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepTroopsFactor = new int[4] { 1, 2, 4, 1 };

        /// <summary>
        /// 城市沦陷可以保留库存比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepItemFactor = new int[4] { 1, 2, 4, 1 };

        /// <summary>
        /// 城市沦陷可以保留农业比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepAgriculture = new int[4] { 1, 2, 4, 1 };

        /// <summary>
        /// 城市沦陷可以保留开发比例(百分比)
        /// </summary>
        [JsonProperty] public int[] cityFallCanKeepCommerce = new int[4] { 1, 2, 4, 1 };


        /// <summary>
        /// 每级兵种适应力对技能释放成功率的加成(百分比) 需要>=A级适应力(2)
        /// </summary>
        [JsonProperty] public int skillSuccessRateAddByAbility = 5;

        /// <summary>
        /// 每级兵种适应力对技能暴击率的加成(百分比) 需要>C级适应力(1)
        /// </summary>
        [JsonProperty] public int skillCriticalRateAddByAbility = 1;

        /// <summary>
        /// 每级兵种适应力对技能暴击率的加成(百分比) 需要>C级适应力(1)
        /// </summary>
        [JsonProperty] public int baseSkillCriticalRate = 5;

        /// <summary>
        /// 武力对暴击的加成值x (武力-60) * x / 10  大于60武力,每10点武力能加成x, 只取整数部分
        /// </summary>
        [JsonProperty] public int skillCriticalRateAddByStength = 1;

        /// <summary>
        /// 暴击倍率(百分比)
        /// </summary>
        [JsonProperty] public int skillCriticalFactor = 150;

        /// <summary>
        /// 每一季度治安下降最大数
        /// </summary>
        [JsonProperty] public int securityChangeOnSeasonStart = -5;

        /// <summary>
        /// 每一月治安下降最大数
        /// </summary>
        [JsonProperty] public int securityChangeOnMonthStart = 0;

        /// <summary>
        /// 适应名称
        /// </summary>
        [JsonProperty] public string[] personAbilityName = new string[] { "Ｃ", "Ｂ", "Ａ", "Ｓ" };

        /// <summary>
        /// 属性名称
        /// </summary>
        [JsonProperty] public string[] attributeName = new string[] { "统率", "武力", "智力", "政治", "魅力" };

        /// <summary>
        /// 属性名称-带颜色
        /// </summary>
        [JsonProperty]
        public string[] attributeNameWithColor = new string[] {
            "<color=#F58080>统率</color>",
            "<color=#9866DB>武力</color>",
            "<color=#E5B462>智力</color>",
            "<color=#61E28F>政治</color>",
            "<color=#78CBF3>魅力</color>" };

        /// <summary>
        /// 基础越狱概率(万分比)
        /// </summary>
        [JsonProperty] public int baseEscapeProbabllity = 100;

        /// <summary>
        /// 越狱概率每回合增长(万分比)
        /// </summary>
        [JsonProperty] public int baseEscapeProbablilityAddByTurn = 50;

        /// <summary>
        /// 寻路安全次数限制
        /// </summary>
        [JsonProperty] public int pathfindingSafeCount = 100000;

        /// <summary>
        /// 基础火焰伤害
        /// </summary>
        [JsonProperty] public int baseFireDamage = 320;
        #region 外交系统参数

        /// <summary>
        /// 外交关系阈值 - 结盟
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRelationThreshold = 2000;

        /// <summary>
        /// 外交关系阈值 - 停战
        /// </summary>
        [JsonProperty] public int diplomacyTruceRelationThreshold = -1000;

        /// <summary>
        /// 外交关系阈值 - 请求技术
        /// </summary>
        [JsonProperty] public int diplomacyRequestTechniqueRelationThreshold = 1000;

        /// <summary>
        /// 外交关系阈值 - 请求兵力
        /// </summary>
        [JsonProperty] public int diplomacyRequestTroopsRelationThreshold = 1500;

        /// <summary>
        /// 外交关系阈值 - 通商
        /// </summary>
        [JsonProperty] public int diplomacyTradeRelationThreshold = -500;

        /// <summary>
        /// 外交关系阈值 - 和亲
        /// </summary>
        [JsonProperty] public int diplomacyMarriageRelationThreshold = 1500;

        /// <summary>
        /// 外交关系阈值 - 请求结盟
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestRelationThreshold = 1500;

        /// <summary>
        /// 外交关系阈值 - 请求停战
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestRelationThreshold = -1500;

        /// <summary>
        /// 使者能力加成上限
        /// </summary>
        [JsonProperty] public int diplomacyAbilityBonusMax = 20;

        /// <summary>
        /// 资源价值加成上限
        /// </summary>
        [JsonProperty] public int diplomacyResourceBonusMax = 30;

        /// <summary>
        /// 资源价值加成系数
        /// </summary>
        [JsonProperty] public int diplomacyResourceBonusFactor = 100;

        /// <summary>
        /// 结盟关系增加
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRelationIncrease = 500;

        /// <summary>
        /// 停战关系增加
        /// </summary>
        [JsonProperty] public int diplomacyTruceRelationIncrease = 300;

        /// <summary>
        /// 宣战关系减少
        /// </summary>
        [JsonProperty] public int diplomacyDeclareWarRelationDecrease = 1000;

        /// <summary>
        /// 送礼金额
        /// </summary>
        [JsonProperty] public int diplomacySendGiftAmount = 1000;

        /// <summary>
        /// 送礼关系增加比例
        /// </summary>
        [JsonProperty] public int diplomacySendGiftRelationFactor = 10;

        /// <summary>
        /// 请求技术关系减少
        /// </summary>
        [JsonProperty] public int diplomacyRequestTechniqueRelationDecrease = 200;

        /// <summary>
        /// 请求兵力关系减少
        /// </summary>
        [JsonProperty] public int diplomacyRequestTroopsRelationDecrease = 300;

        /// <summary>
        /// 通商关系增加
        /// </summary>
        [JsonProperty] public int diplomacyTradeRelationIncrease = 100;

        /// <summary>
        /// 通商黄金收入增加
        /// </summary>
        [JsonProperty] public int diplomacyTradeGoldIncrease = 50;

        /// <summary>
        /// 和亲关系增加
        /// </summary>
        [JsonProperty] public int diplomacyMarriageRelationIncrease = 500;

        /// <summary>
        /// 和亲额外关系增加
        /// </summary>
        [JsonProperty] public int diplomacyMarriageExtraRelationIncrease = 200;

        /// <summary>
        /// 赎回俘虏关系增加比例
        /// </summary>
        [JsonProperty] public int diplomacyRansomRelationFactor = 20;

        /// <summary>
        /// 请求结盟关系增加
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestRelationIncrease = 50;

        /// <summary>
        /// 请求停战关系增加
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestRelationIncrease = 30;

        /// <summary>
        /// 撕毁同盟关系减少
        /// </summary>
        [JsonProperty] public int diplomacyBreakAllianceRelationDecrease = 500;

        /// <summary>
        /// 撕毁停战关系减少
        /// </summary>
        [JsonProperty] public int diplomacyBreakTruceRelationDecrease = 300;

        /// <summary>
        /// 撕毁通商关系减少
        /// </summary>
        [JsonProperty] public int diplomacyBreakTradeRelationDecrease = 200;

        /// <summary>
        /// 同盟持续时间
        /// </summary>
        [JsonProperty] public int diplomacyAllianceDuration = 36;

        /// <summary>
        /// 停战持续时间
        /// </summary>
        [JsonProperty] public int diplomacyTruceDuration = 18;

        /// <summary>
        /// 通商持续时间
        /// </summary>
        [JsonProperty] public int diplomacyTradeDuration = 24;

        /// <summary>
        /// 每月同盟关系增加
        /// </summary>
        [JsonProperty] public int diplomacyMonthlyAllianceRelationIncrease = 50;

        /// <summary>
        /// 每月普通关系减少
        /// </summary>
        [JsonProperty] public int diplomacyMonthlyNormalRelationDecrease = 100;

        /// <summary>
        /// 外交成功率 - 结盟基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceBaseSuccessRate = 50;

        /// <summary>
        /// 外交成功率 - 结盟关系系数
        /// </summary>
        [JsonProperty] public float diplomacyAllianceRelationFactor = 50;

        /// <summary>
        /// 外交成功率 - 结盟最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceMaxSuccessRate = 90;

        /// <summary>
        /// 外交成功率 - 结盟最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 停战基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceBaseSuccessRate = 40;

        /// <summary>
        /// 外交成功率 - 停战关系系数
        /// </summary>
        [JsonProperty] public float diplomacyTruceRelationFactor = 37.5f;

        /// <summary>
        /// 外交成功率 - 停战最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceMaxSuccessRate = 80;

        /// <summary>
        /// 外交成功率 - 停战最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 请求技术基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTechniqueBaseSuccessRate = 30;

        /// <summary>
        /// 外交成功率 - 请求技术关系系数
        /// </summary>
        [JsonProperty] public float diplomacyRequestTechniqueRelationFactor = 47.06f;

        /// <summary>
        /// 外交成功率 - 请求技术最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTechniqueMaxSuccessRate = 85;

        /// <summary>
        /// 外交成功率 - 请求技术最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTechniqueMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 请求兵力基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTroopsBaseSuccessRate = 20;

        /// <summary>
        /// 外交成功率 - 请求兵力关系系数
        /// </summary>
        [JsonProperty] public float diplomacyRequestTroopsRelationFactor = 43.75f;

        /// <summary>
        /// 外交成功率 - 请求兵力最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTroopsMaxSuccessRate = 80;

        /// <summary>
        /// 外交成功率 - 请求兵力最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyRequestTroopsMinSuccessRate = 5;

        /// <summary>
        /// 外交成功率 - 通商基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyTradeBaseSuccessRate = 50;

        /// <summary>
        /// 外交成功率 - 通商关系系数
        /// </summary>
        [JsonProperty] public float diplomacyTradeRelationFactor = 55.56f;

        /// <summary>
        /// 外交成功率 - 通商最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyTradeMaxSuccessRate = 95;

        /// <summary>
        /// 外交成功率 - 通商最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyTradeMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 和亲基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyMarriageBaseSuccessRate = 40;

        /// <summary>
        /// 外交成功率 - 和亲关系系数
        /// </summary>
        [JsonProperty] public float diplomacyMarriageRelationFactor = 36.84f;

        /// <summary>
        /// 外交成功率 - 和亲最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyMarriageMaxSuccessRate = 95;

        /// <summary>
        /// 外交成功率 - 和亲最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyMarriageMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 请求结盟基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestBaseSuccessRate = 30;

        /// <summary>
        /// 外交成功率 - 请求结盟关系系数
        /// </summary>
        [JsonProperty] public float diplomacyAllianceRequestRelationFactor = 41.18f;

        /// <summary>
        /// 外交成功率 - 请求结盟最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestMaxSuccessRate = 85;

        /// <summary>
        /// 外交成功率 - 请求结盟最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestMinSuccessRate = 10;

        /// <summary>
        /// 外交成功率 - 请求停战基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestBaseSuccessRate = 20;

        /// <summary>
        /// 外交成功率 - 请求停战关系系数
        /// </summary>
        [JsonProperty] public float diplomacyTruceRequestRelationFactor = 40f;

        /// <summary>
        /// 外交成功率 - 请求停战最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestMaxSuccessRate = 75;

        /// <summary>
        /// 外交成功率 - 请求停战最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestMinSuccessRate = 5;

        /// <summary>
        /// 外交成功率 - 赎回俘虏基础成功率
        /// </summary>
        [JsonProperty] public int diplomacyRansomBaseSuccessRate = 30;

        /// <summary>
        /// 外交成功率 - 赎回俘虏关系系数
        /// </summary>
        [JsonProperty] public float diplomacyRansomSuccessRelationFactor = 40f;

        /// <summary>
        /// 外交成功率 - 赎回俘虏最大成功率
        /// </summary>
        [JsonProperty] public int diplomacyRansomMaxSuccessRate = 90;

        /// <summary>
        /// 外交成功率 - 赎回俘虏最低成功率
        /// </summary>
        [JsonProperty] public int diplomacyRansomMinSuccessRate = 10;

        /// <summary>
        /// 赎回俘虏费用 - 等级费用因子
        /// </summary>
        [JsonProperty] public int diplomacyRansomLevelCostFactor = 100;

        /// <summary>
        /// 赎回俘虏费用 - 功绩费用因子
        /// </summary>
        [JsonProperty] public int diplomacyRansomMeritCostFactor = 10;

        /// <summary>
        /// 赎回俘虏费用 - 官职费用因子
        /// </summary>
        [JsonProperty] public int diplomacyRansomOfficialCostFactor = 500;

        /// <summary>
        /// 赎回俘虏费用 - 属性费用因子
        /// </summary>
        [JsonProperty] public int diplomacyRansomAttributeCostFactor = 20;

        /// <summary>
        /// 外交成功率 - 请求结盟关系阈值
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestSuccessRelationThreshold = 800;

        /// <summary>
        /// 外交成功率 - 请求停战关系阈值
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestSuccessRelationThreshold = -800;

        /// <summary>
        /// 外交成功率 - 请求结盟随机概率分母
        /// </summary>
        [JsonProperty] public int diplomacyAllianceRequestChanceDenominator = 2000;

        /// <summary>
        /// 外交成功率 - 请求停战随机概率分母
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestChanceDenominator = 1500;

        /// <summary>
        /// 外交成功率 - 请求停战随机概率偏移
        /// </summary>
        [JsonProperty] public int diplomacyTruceRequestChanceOffset = 1000;

        /// <summary>
        /// 外交失败基础惩罚值
        /// </summary>
        [JsonProperty] public int diplomacyFailedBasePenalty = 300;

        #endregion 外交系统参数

        #region 招募系统参数

        /// <summary>
        /// 招募系统 - 基础相性值
        /// </summary>
        [JsonProperty] public int recruitBaseCompatibility = 25;

        /// <summary>
        /// 招募系统 - 在野武将忠诚度基础值
        /// </summary>
        [JsonProperty] public int recruitWildLoyaltyBase = 60;

        /// <summary>
        /// 招募系统 - 忠诚度难度系数
        /// </summary>
        [JsonProperty] public int recruitLoyaltyDifficultyFactor = 5;

        /// <summary>
        /// 招募系统 - 默认义理ID
        /// </summary>
        [JsonProperty] public int recruitDefaultArgumentationId = 3;

        /// <summary>
        /// 招募系统 - 基础成功率
        /// </summary>
        [JsonProperty] public int recruitBaseSuccessRate = 45;

        /// <summary>
        /// 招募系统 - 相性影响系数分子
        /// </summary>
        [JsonProperty] public int recruitCompatibilityFactorNumerator = 3;

        /// <summary>
        /// 招募系统 - 相性影响系数分母
        /// </summary>
        [JsonProperty] public int recruitCompatibilityFactorDenominator = 2;

        /// <summary>
        /// 招募系统 - 忠诚度影响基础值
        /// </summary>
        [JsonProperty] public int recruitLoyaltyInfluenceBase = 18;

        /// <summary>
        /// 招募系统 - 忠诚度影响系数分子
        /// </summary>
        [JsonProperty] public int recruitLoyaltyInfluenceNumerator = 5;

        /// <summary>
        /// 招募系统 - 忠诚度影响系数分母
        /// </summary>
        [JsonProperty] public int recruitLoyaltyInfluenceDenominator = 100;

        /// <summary>
        /// 招募系统 - 魅力最低值
        /// </summary>
        [JsonProperty] public int recruitMinGlamour = 30;

        /// <summary>
        /// 招募系统 - 魅力影响系数分子
        /// </summary>
        [JsonProperty] public int recruitGlamourFactorNumerator = 3;

        /// <summary>
        /// 招募系统 - 魅力影响系数分母
        /// </summary>
        [JsonProperty] public int recruitGlamourFactorDenominator = 5;

        /// <summary>
        /// 招募系统 - 亲爱武将影响值
        /// </summary>
        [JsonProperty] public int recruitLikePersonInfluence = 15;

        /// <summary>
        /// 招募系统 - 亲子关系影响值
        /// </summary>
        [JsonProperty] public int recruitParentChildInfluence = 15;

        /// <summary>
        /// 招募系统 - 厌恶武将影响值
        /// </summary>
        [JsonProperty] public int recruitHatePersonInfluence = 15;

        /// <summary>
        /// 招募系统 - 俘虏影响值
        /// </summary>
        [JsonProperty] public int recruitPrisonerInfluence = 15;

        /// <summary>
        /// 招募系统 - 随机影响最大值
        /// </summary>
        [JsonProperty] public int recruitRandomMax = 5;

        /// <summary>
        /// 招募系统 - 君主魅力影响系数
        /// </summary>
        [JsonProperty] public int recruitGovernorGlamourFactor = 5;

        /// <summary>
        /// 招募系统 - 第一次发现加成
        /// </summary>
        [JsonProperty] public int recruitFirstDiscoveryBonus = 15;

        /// <summary>
        /// 招募系统 - 基础义理值
        /// </summary>
        [JsonProperty] public int recruitBaseGiri = 10;

        /// <summary>
        /// 招募系统 - 义理最大值
        /// </summary>
        [JsonProperty] public int recruitMaxGiri = 15;

        /// <summary>
        /// 招募系统 - 义理忠诚度影响系数
        /// </summary>
        [JsonProperty] public int recruitGiriLoyaltyFactor = 2;

        #endregion 招募系统参数

        #region 逃跑系统参数

        /// <summary>
        /// 逃跑系统 - 城市中逃跑概率减少值
        /// </summary>
        [JsonProperty] public int escapeCityReduction = 200;

        /// <summary>
        /// 逃跑系统 - 部队中逃跑概率增加值
        /// </summary>
        [JsonProperty] public int escapeTroopIncrease = 500;

        /// <summary>
        /// 逃跑系统 - 最大逃跑概率
        /// </summary>
        [JsonProperty] public int escapeMaxProbability = 3000;

        #endregion 逃跑系统参数

        #region 敌方部队发现参数

        /// <summary>
        /// 发现敌方新建部队的基础概率(万分比)
        /// </summary>
        [JsonProperty] public int discoverEnemyTroopBaseProbability = 3000;

        /// <summary>
        /// 军师智力对发现概率的影响系数(万分比)
        /// </summary>
        [JsonProperty] public int discoverEnemyTroopIntelligenceFactor = 70;

        #endregion 敌方部队发现参数

        /// <summary>
        /// 建造间隔
        /// </summary>
        [JsonProperty] public int BuildingSpace = 2;

        /// <summary>
        /// 运输比例
        /// </summary>
        [JsonProperty] public int TransportPercent = 80;

        /// <summary>
        /// 是否让未登场的武将在登场年份到达后登场
        /// </summary>
        [JsonProperty] public bool allowInvalidPersonValidWhenYearPass = true;


        public float DifficultyDamageFactor
        {
            get
            {
                if (difficulty >= 0 && difficulty < fight_damage_difficulty_factor.Length)
                {
                    return fight_damage_difficulty_factor[difficulty];
                }
                return fight_damage_difficulty_factor[fight_damage_difficulty_factor.Length - 1];
            }
        }

        /// <summary>
        /// 获取AI每回合实际增加的资源数量。
        /// </summary>
        /// <param name="gold">钱</param>
        /// <param name="food">粮</param>
        /// <param name="troops">士兵</param>
        /// <param name="arms">兵装</param>
        public void GetAIAddValues(out int gold, out int food, out int troops, out int arms)
        {
            gold = aiTurnAddGold;
            food = aiTurnAddFood;
            troops = aiTurnAddTroops;
            arms = aiTurnAddArms;
        }

        public string GetAbilityName(int lvl)
        {
            if (lvl < personAbilityName.Length)
                return personAbilityName[lvl];
            else
            {
                if (GameEvent.OnGetAbilityName != null)
                    return GameEvent.OnGetAbilityName(lvl);
                return personAbilityName[personAbilityName.Length - 1];
            }
        }

        public string GetAttributeName(int lvl)
        {
            if (lvl < attributeName.Length)
                return attributeName[lvl];
            else
            {
                if (GameEvent.OnGetAttributeName != null)
                    return GameEvent.OnGetAttributeName(lvl);
                return attributeName[attributeName.Length - 1];
            }
        }

        public string GetAttributeNameWithColor(int lvl)
        {
            if (lvl < attributeNameWithColor.Length)
                return attributeNameWithColor[lvl];
            else
            {
                if (GameEvent.OnGetAttributeNameWithColor != null)
                    return GameEvent.OnGetAttributeNameWithColor(lvl);
                return attributeNameWithColor[attributeNameWithColor.Length - 1];
            }
        }
    }
}
