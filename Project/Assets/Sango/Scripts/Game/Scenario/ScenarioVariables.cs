using System.IO;
using Newtonsoft.Json;
using UnityEngine;


namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioVariables : SangoObjectExtensionData
    {
        /// <summary>
        /// 应用基础武将库修改
        /// </summary>
        [JsonProperty] public bool ApplyEdit = false;

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
        /// 能力每级经验（兵种适性每一级所需经验）。
        /// 节奏参考：一部队一年约 15 次战斗结算，默认每次攻击结算 3 点 → 约 45 点/年，
        /// 一级 100 点 ≈ 2.2 年，10 级满级 ≈ 22 年。
        /// </summary>
        [JsonProperty] public ushort AbilityExpLevelNeed = 100;

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
        /// 功绩获取配置（键 = GainPlace，缺省回落到内置默认值）
        /// </summary>
        [JsonProperty] public GainValueConfig meritGain = new GainValueConfig();

        /// <summary>
        /// 技巧点获取配置（键 = GainPlace）
        /// </summary>
        [JsonProperty] public GainValueConfig techniquePointGain = new GainValueConfig();

        /// <summary>
        /// 武将等级经验获取配置（键 = GainPlace）
        /// </summary>
        [JsonProperty] public GainValueConfig expGain = new GainValueConfig();

        /// <summary>
        /// 兵种适性经验获取配置（键 = GainPlace，目前尚无产出点）
        /// </summary>
        [JsonProperty] public GainValueConfig abilityExpGain = new GainValueConfig();

        /// <summary>
        /// 能力(属性)经验获取配置（键 = GainPlace）
        /// </summary>
        [JsonProperty] public GainValueConfig attributeExpGain = new GainValueConfig();

        /// <summary>
        /// 剧本事件奖励配置（键 = GainPlace 的 Event* 段）。
        /// 事件效果用 { "GainPlace": "键名" } 引用本表，因此事件 JSON 里不出现硬编码数值，
        /// 调参统一在本表进行（剧本设置界面的"剧本事件奖励"分区）。
        /// </summary>
        [JsonProperty] public GainValueConfig eventGain = new GainValueConfig();

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
        /// 单挑获胜时候的**基础**抓捕率(百分比)。
        ///
        /// 单挑的抓捕率 = 本值 + 胜方部队的抓捕率(即上面那个 captureChangceWhenTroopFall，
        ///                以及挂在部队上的各种修改型 Action) − 败方武将的逃跑系数，夹在 0~100。
        /// 这一项原先在 Duel.CalcCaptureChance 里是硬编码的 80；因为现在把部队抓捕率也加了进来，
        /// 基础值下调到 70 —— 普通部队 + 无逃跑系数时总概率 75%，比原来的平铺 80% 略低，
        /// 同时给"捕缚类抬抓捕率、马术类抬逃跑系数"留出上下调节余地。
        /// 设为 0 就退化成"只看部队抓捕率 − 逃跑系数"（与部队溃灭抓捕完全同口径）。
        /// </summary>
        [JsonProperty] public int captureChangceWhenDuelWin = 70;

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
        /// 战法(kind=2)命中后触发单挑的基础概率(百分比)，0 = 关闭该触发。
        /// 实际概率取发起部队属性 Troop.duelChance（本值 + Action 的修正），夹在 0~100 之间；
        /// 另外还要求该战法自身的 Skill.canTriggerDuel 为 true（战法级别的开关）。
        /// </summary>
        [JsonProperty] public int skillDuelChance = 5;

        /// <summary>
        /// 单挑寿命模式：0 = 普通，1 = 虚拟（影响老将的武力衰减判定，对应单挑内部的 LifeMode）
        /// </summary>
        [JsonProperty] public int duelLifeMode = 0;

        /// <summary>
        /// 单挑战死频率：0 = 无，1 = 普通(2%)，2 = 高(5%)，对应单挑内部的 BattleDeathMode
        /// </summary>
        [JsonProperty] public int duelDeathMode = 1;

        /// <summary>单挑：禁用"一合取胜(一击必杀)"判定</summary>
        [JsonProperty] public bool duelDisableFirstTurnKill = false;

        /// <summary>单挑：禁用"捕缚"（禁用后单挑结束不会捕获敌将）</summary>
        [JsonProperty] public bool duelDisableCapture = false;

        /// <summary>单挑：禁用 AI 的"退却"必杀</summary>
        [JsonProperty] public bool duelDisableAIRetreat = false;

        /// <summary>
        /// 舌战：禁用"会心"（禁用后舌战不再进入会心阶段，
        /// 胜方不会做出追击 / 留情的抉择，胜利方式固定为普通）。
        /// 对应 C++ 的 Feature_DebateCritical。
        /// </summary>
        [JsonProperty] public bool debateDisableCritical = false;

        /// <summary>
        /// 外交交涉失败后按概率强制进入舌战的概率(百分比)。0 = 不触发。
        /// 触发双方：使者 = 执行外交的武将，对方代表 = 接收方势力君主。
        /// </summary>
        [JsonProperty] public int debateChanceWhenDiplomacyFail = 5;

        /// <summary>
        /// 招募(登用)人才失败后按概率强制进入舌战的概率(百分比)。0 = 不触发。
        /// 触发双方：招募者 = 执行登用的武将，被招募者 = 目标人才。
        /// </summary>
        [JsonProperty] public int debateChanceWhenRecruitFail = 10;

        /// <summary>
        /// 是否允许 AI 之间也触发舌战。false = 只有玩家参与时才触发。
        /// AI 之间的舌战不带表现层，只在后台结算（不弹界面）。
        /// </summary>
        [JsonProperty] public bool debateAllowAIVsAI = true;

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

        /// <summary>
        /// 是否允许火焰向相邻格蔓延（剧本开关，玩家可在剧本参数界面自行选择）。
        /// 关掉后火焰照常点燃 / 灼烧 / 熄灭，只是不再向外扩散，见 Fire.SpreadFire。
        /// </summary>
        [JsonProperty] public bool fireSpreadEnabled = true;

        /// <summary>
        /// 火焰每回合向相邻格蔓延的**最大**概率（%），对应可燃性最高的地形（森林 / 荒地）。
        /// 实际概率只取决于**目标格**的地形可燃性，按比例折算，见 Fire.SpreadFire。
        /// </summary>
        [JsonProperty] public int fireSpreadMaxChance = 50;

        /// <summary>
        /// 地形可燃性（TerrainTypes.fireRate）的基准上限，用于归一化蔓延概率。
        /// 数据表中森林 / 荒地约 20~25，湿地 5，水域 / 山地为 0。
        /// </summary>
        [JsonProperty] public int fireSpreadTerrainRateMax = 25;

        /// <summary>
        /// 单团火焰每回合最多蔓延的格子数（0 表示不限制）。
        /// 用于防止火势呈指数式失控。
        /// </summary>
        [JsonProperty] public int fireSpreadMaxPerTurn = 2;

        /// <summary>
        /// 蔓延生成的新火焰存活回合数的**随机上限**。
        /// 新火焰的寿命在 [1, 该值] 之间随机取值，使火线各团的存续时间参差不齐，
        /// 蔓延节奏更自然（不会整片火同时熄灭）。
        /// 设为 0 表示不随机，改为完全继承父火的剩余回合数。
        /// </summary>
        [JsonProperty] public int fireSpreadMaxLifespan = 3;

        /// <summary>
        /// 火焰蔓延的最大代数（0 表示不限制）。
        /// 由技能 / 计略直接点燃的火焰为第 0 代，每向外蔓延一次代数 +1；
        /// 达到该代数后不再继续蔓延，从根本上杜绝火势无限扩散。
        /// </summary>
        [JsonProperty] public int fireSpreadMaxGeneration = 4;

        /// <summary>
        /// 火焰每增加一代，蔓延概率与寿命上限的衰减百分比（%）。
        /// 第 N 代火焰保留 (100 - N × 该值)% 的蔓延概率与寿命上限。
        /// 默认 20 时：第 1 代 80%、第 2 代 60%、第 3 代 40%、第 4 代 20%，
        /// 第 5 代衰减至 0（不再蔓延）。
        /// </summary>
        [JsonProperty] public int fireSpreadDecayPerGeneration = 20;

        /// <summary>
        /// **剧本事件系统总开关**（剧本级，玩家可在剧本参数界面自行开关，随存档持久化）。
        ///
        /// 关掉后的行为：
        ///   · 不再评估任何触发 —— 连条件树都不求值，零开销；
        ///   · 不再推进定时事件队列；
        ///   · 不再重试"因演出占用而暂缓"的触发。
        ///
        /// 两点刻意的取舍：
        ///   · **已经在播的那一个事件照常播完** —— 中途掐断会留下半开的窗口与对话框，
        ///     反而要写一堆清理逻辑；而且玩家关开关时通常正是被某个事件打断。
        ///   · **定时待办项保留** —— 重新打开后按回合逐条补播（每回合只成功播一条），
        ///     不会一次性连播二十个事件把玩家埋掉。
        ///
        /// 调试面板的"强制触发"**不受本开关影响**（那是显式意图，不是自动触发）。
        /// </summary>
        [JsonProperty] public bool eventSystemEnabled = false;

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

        #region 城市计略系统参数

        /// <summary>
        /// 城市计略成功率主项系数（百分比）。
        /// 成功率 = 本系数 × 使者智力² / (使者智力² + 抵抗者智力²) + 智力差修正，再钳到上下限。
        /// 形状取自原版脚本 206 计略成功率.cpp 的平方和归一化（其原值为 35，对应"对等智力约 17%"，
        /// 那是部队伪报的强度）；城市计略按"对等智力 50%"标定，故取 100。数值自定，待平衡。
        /// </summary>
        [JsonProperty] public int cityStrategyRateCoefficient = 100;

        /// <summary>
        /// 城市计略智力差修正系数：领先时按 (差值/本系数) 加分，落后时按 (差值×本系数) 减分。
        /// 原版为"领先 10 点以上 +(差/2)、否则 +(差-10)*2"，城市计略照抄会形成"落后 20 点即 1%"的悬崖，
        /// 故统一为线性修正，仍保留"落后比领先变化更快"的非对称方向。
        /// </summary>
        [JsonProperty] public int cityStrategyIntelligenceFactor = 2;

        /// <summary>
        /// 城市计略最低成功率（百分比）。对齐原版脚本 206 的"计略成功率下限 = 1"
        /// </summary>
        [JsonProperty] public int cityStrategyRateMin = 1;

        /// <summary>
        /// 城市计略最高成功率（百分比）。原版脚本 206 的上限是 99，这里抬到 100 是为了让
        /// "军师智力即承诺门槛"能真正兑现：推荐口径只推荐预估成功率 ≥ 军师智力的人，
        /// 上限留在 99 会让智力 100 的军师永远无人可荐。`GameRandom.Chance` 对 chance >= 100 直接返回 true，
        /// 所以 100 就是必成。注意本值随存档序列化，改默认值只对新开局生效，老存档仍沿用各自的旧值
        /// </summary>
        [JsonProperty] public int cityStrategyRateMax = 100;

        /// <summary>
        /// 计略"暴露线"（百分比）：成功率低于此值仍成功的，视为险胜露馅，按 detectedPenalty 反噬关系。
        /// 依据原版流言结果码 721:46 的 wisdom_diff &lt; 60 → "成功被发现"，此处用成功率作等价判据。
        /// 配成 0 表示成功一律不露馅。
        /// </summary>
        [JsonProperty] public int cityStrategyDetectedRateLine = 50;

        /// <summary>
        /// 计略成功但露馅（险胜）时，对"发起方与各目标方"关系的反噬值。
        /// 比彻底失败的代价轻，用于表达"事未成但人已露头"。
        /// </summary>
        [JsonProperty] public int cityStrategyDetectedPenalty = 100;

        /// <summary>
        /// 二虎竞食成功后两个目标势力的交情恶化值（固定值，不随成功率缩放）。
        /// 原版关系区间 0..100，破盟/停战破裂的事件量级是 -30 ~ -50；本工程关系区间为 ±5000，
        /// 换算后 3000 ≈ 原版 -30。原值 500 换算只有原版 -5（=1 个月的自然漂移），玩家无感，故上调。
        /// </summary>
        [JsonProperty] public int cityStrategyTwoTigersRelationDecrease = 3000;

        /// <summary>
        /// 势力关系恶化到该值（含）以下时，AI 会放弃与对方的同盟并转向敌对。
        /// 对应原版脚本 808 新外交战争.cpp 的"relations &lt;= 0 即破盟"，按本工程刻度换算为 -3000；
        /// 原版关系下限映射在本工程刻度上恰好是钳制边界 -5000，永远达不到，故必须显式设线。
        /// </summary>
        [JsonProperty] public int cityStrategyWarTriggerRelation = -3000;

        /// <summary>
        /// 城市计略彻底失败（被识破）时对"发起方与各目标方"关系的反噬值（0 表示不反噬）
        /// </summary>
        [JsonProperty] public int cityStrategyFailedPenalty = 200;

        /// <summary>
        /// 流言成功后武将忠诚下降区间下限（整城掷同一个基准值，再按义理逐人加权）。
        /// 原版量级参照：每月自然掉忠 0~5（155）、事件级一次性掉忠 2~30（851 s11全事件解析.cpp），
        /// 原值 10~25 相对偏低端事件过强，收到 8~15。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorLoyaltyDropMin = 8;

        /// <summary>
        /// 流言成功后武将忠诚下降区间上限（闭区间上界，结算时按 max + 1 传给 GameRandom.Range）
        /// </summary>
        [JsonProperty] public int cityStrategyRumorLoyaltyDropMax = 15;

        /// <summary>
        /// 流言忠诚下降的义理加权上限。
        /// 逐人降幅 = 基准值 + (义理档 - 最低档) / 本系数，义理越低越容易被谣言动摇（对齐 155 的 (义理_高 - giri)/2）。
        /// 设为 0 表示不加权。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorGiriDivisor = 2;

        /// <summary>
        /// 流言对都市成功后治安下降区间下限（港/关不受治安影响）。
        /// 原版量级参照：每季自然下降 0~5（154）、瘟疫单次 2~4（160）、巡查单次约 +3~11（104）。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorSecurityDropMin = 6;

        /// <summary>
        /// 流言对都市成功后治安下降区间上限（闭区间上界，结算时按 max+1 传给 GameRandom.Range）
        /// </summary>
        [JsonProperty] public int cityStrategyRumorSecurityDropMax = 12;

        /// <summary>
        /// 流言失败时使者被捕的基准概率（百分比）。原版的"失败被捕"判据方向自相矛盾
        /// （721:49 是抵抗值越高越易被捕），故本实现改为"按使者自身智力做只降不增的减免"。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorCaptureBaseRate = 30;

        /// <summary>
        /// 被捕概率的智力减免起算线：使者智力超过该值才开始减免，低于该值不额外加罚
        /// </summary>
        [JsonProperty] public int cityStrategyRumorCaptureReliefLine = 70;

        /// <summary>
        /// 使者智力每超过减免线 1 点，被捕概率下降的百分点数
        /// </summary>
        [JsonProperty] public int cityStrategyRumorCaptureReliefPerPoint = 1;

        /// <summary>
        /// 被捕概率下限：再足智多谋也留有一丝风险，避免流言变成零成本指令
        /// </summary>
        [JsonProperty] public int cityStrategyRumorCaptureMin = 5;

        /// <summary>
        /// AI 主动施计的智力优势门：使者智力需高出抵抗者该值以上才出手
        /// （对应原版脚本 #军师之战2.cpp 的"智力差 &lt; 5 不应战"）
        /// </summary>
        [JsonProperty] public int cityStrategyAIMinIntelligenceEdge = 5;

        /// <summary>
        /// AI 每回合愿意主动发动一次城市计略的基准概率（百分比）。
        /// 原版 255 号脚本对 AI 施计另有资金门槛（gold &gt;= 消耗 × rand(10,20)/10），
        /// 本工程把"概率 + 资金倍数"两道门都保留，只是概率放在这里调
        /// </summary>
        [JsonProperty] public int cityStrategyAIAttemptChance = 25;

        /// <summary>
        /// 记仇窗口内 AI 报复性施计的概率（百分比）。
        /// 对应原版 721 AI优化-流言.cpp 的"被流言后 bind 记仇 → 反施流言"，明显高于主动门。
        /// 注意：报复凭据是一次性的（Force.TakeCityStrategyGrudge），同一笔账只会驱动一次报复；
        /// 默认值由 60 下调到 35，配合一次性凭据掐断"AI 互相无限刷流言"的正反馈。
        /// </summary>
        [JsonProperty] public int cityStrategyAIRevengeChance = 35;

        /// <summary>
        /// 计略仇与二虎竞食挑拨凭据的有效期（回合，1 回合 = 1 旬）。
        /// 超窗的记仇不再驱动报复，过期的挑拨凭据也不能再触发破盟，避免长局里累积出连锁反应
        /// </summary>
        [JsonProperty] public int cityStrategyGrudgeKeepTurns = 6;

        /// <summary>
        /// AI 报复流言时的行军距离上限，单位与 City.Distance 一致（最短路径的城数，1 城 = 1 回合行程）。
        /// 对应原版 721 AI优化-流言.cpp 的"报复范围 = 2 座城"；本工程把港/关也算一跳，故放宽到 3 作容差。
        /// 配成 0 表示不限制距离。
        /// </summary>
        [JsonProperty] public int cityStrategyAIRevengeMaxDays = 3;

        /// <summary>
        /// 流言忠诚下限闸门：流言无论如何都不能把一名武将的忠诚压到该值以下。
        /// 配 0 表示不设闸门（回到"流言可以把整城忠诚打到底"的旧口径）。
        /// 用途：遏制 AI 互相施放流言把整城忠诚打到可登庸线以下，让玩家"看戏等低再登庸"。
        /// 只约束流言这一条链路，不干预换季掉忠与事件掉忠（那些仍可把忠诚压得更低）。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorLoyaltyFloor = 45;

        /// <summary>
        /// 同一座据点被流言命中后的免疫回合数：免疫窗内再对该据点施放流言，效果不落地。
        /// 配 0 表示无冷却。记录落在 City.lastRumorTurn 上，按绝对回合数比较判定，玩家与 AI 同口径，
        /// 防止同一座城被连续每回合反复刷忠诚。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorImmunityTurns = 5;

        /// <summary>
        /// AI 势力级流言配额：同一 AI 势力两次流言派遣之间至少间隔的回合数（0 表示不限制）。
        /// 把"每个 AI 每回合都可能喷一次"降为"偶尔为之"，是掐断 AI 互相刷流言节奏的核心闸门之一。
        /// 只约束流言，不影响二虎竞食。
        /// </summary>
        [JsonProperty] public int cityStrategyAIMaxRumorPerTurns = 3;

        /// <summary>
        /// "被流言动摇"标记的有效期（回合）：CityAI 据此识别需要优先褒奖的武将，把被喷掉的忠诚补回来。
        /// 配 0 表示不启用 AI 褒奖响应。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorVictimRewardTurns = 6;

        /// <summary>
        /// 本城出现"被流言动摇"的武将时，AI 单回合褒奖人数在 AIConfig.rewardMaxPersonPerTurn 之上额外增加的人数。
        /// 配 0 表示不追加；配额有效期见 cityStrategyRumorVictimRewardTurns。
        /// </summary>
        [JsonProperty] public int cityStrategyRumorVictimRewardBoost = 1;

        #endregion 城市计略系统参数

        #region 招募系统参数

        /// <summary>
        /// 挖角外交代价 - 成功：登庸"在职于其它势力"的武将得手时，对"我方与该势力"关系造成的下降值。
        /// 配 0 表示不扣。用途：让"看戏等 AI 互喷把忠诚打低再去登庸"付出真实的外交代价，而不是零成本白捡。
        /// 只对在职敌将生效：在野武将、无势力俘虏、破城/俘虏招降一律不受影响。
        /// </summary>
        [JsonProperty] public int recruitForeignRelationPenalty = 300;

        /// <summary>
        /// 挖角外交代价 - 失败被察觉：登庸"在职于其它势力"的武将失败时，对双方关系造成的下降值。
        /// 比成功轻，配 0 表示不扣。用于表达"挖角意图暴露"的分量。
        /// </summary>
        [JsonProperty] public int recruitForeignRelationPenaltyFailed = 100;

        /// <summary>
        /// 招募系统 - 基础相性值
        /// </summary>
        [JsonProperty] public int recruitBaseCompatibility = 25;

        /// <summary>
        /// 招募系统 - 在野武将（及无势力俘虏）忠诚度基础值。
        /// 公式里它们用的是"合成忠诚" = 本值 + difficulty × recruitLoyaltyDifficultyFactor
        ///（默认难度 1 → 30 + 5 = 35），而不是武将的真实忠诚。
        /// </summary>
        [JsonProperty] public int recruitWildLoyaltyBase = 30;

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
        /// 运输比例【已停止生效】
        ///
        /// 旧城池运输（CityAI.AITransfrom / AITransfromToBelongCity）按"本城库存 × 该比例"发货，
        /// 该命令已被资源调度（ResourceDispatcher）取代：现在按**圈层水位 + 目标缺口 + 在途量**决定发货量，
        /// 不再用固定比例。字段保留只为兼容老存档（反序列化时不会报错），改它没有任何效果。
        /// </summary>
        [JsonProperty] public int TransportPercent = 80;

        /// <summary>
        /// 是否让未登场的武将在登场年份到达后登场
        /// </summary>
        [JsonProperty] public bool allowInvalidPersonValidWhenYearPass = true;

        /// <summary>
        /// 默认计略设置
        /// </summary>
        [JsonProperty] public int[] defaultStrategySkills = new int[] { 22, 23, 24, 25, 26, 27, 28 };



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
