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
        /// 名字最大支持字数。
        /// 用于约束武将 / 势力 / 城市 / 军团等名字的录入长度，默认 4 字。
        /// 所有"名字列"(SortByName)的显示宽度均按本值动态计算，见 <see cref="GetNameColumnWidth"/>。
        /// </summary>
        [JsonProperty] public int maxNameLength = DefaultMaxNameLength;

        /// <summary>
        /// 最大可存储的行动力上限
        /// </summary>
        [JsonProperty] public int ActionPointLimit = 255;

        /// <summary>
        /// 行动力获取倍率
        /// </summary>
        [JsonProperty] public float ActionPointFactor = 1;

        /// <summary>AI 行动力基础值的缺省值（原版写死 40）</summary>
        public const int DefaultAIActionPointBase = 40;

        /// <summary>
        /// AI 行动力基础值 —— AI 军团每回合行动力兜底的**每城基准量**。
        ///
        /// 语义：AI 军团每回合起始行动力 = max(当前行动力, 本军团城数 × 本值)，
        /// 见 <see cref="CorpsAI.AIDistrictPrepare"/>。
        /// 对齐 Open311 s11_game_ai_district.cpp:52-62 的 `city_count * 2 * 20`（原版写死 40 / 城）；
        /// 本项目提到剧本配置（剧本参数 → AI参数 → AI行动力基础值），便于按剧本规模调 AI 节奏：
        /// 值越小，AI 每回合能做的事越少。
        /// 配 0 或负数时回落到 <see cref="DefaultAIActionPointBase"/>。
        /// </summary>
        [JsonProperty] public int aiActionPointBase = DefaultAIActionPointBase;

        /// <summary>
        /// 真实年龄开关
        /// </summary>
        [JsonProperty] public bool AgeEnabled = true;

        /// <summary>
        /// 能力随年龄变化
        /// </summary>
        [JsonProperty] public bool EnableAgeAbilityFactor = true;

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

        /// <summary>最大生效特技数的缺省值</summary>
        public const int DefaultMaxActiveFeatureCount = 3;

        /// <summary>
        /// 最大生效特技数量（默认 3，剧本可在"剧本基础参数"里覆盖）。
        ///
        /// 【语义】每名武将的 <see cref="Person.FeatureList"/> **只有前 N 个生效**。
        /// 超出部分不装配动作、不参与条件判定、不在界面展示、也不能再通过培育学习；
        /// 但**数据不删除**（存档仍完整保留全部特技 Id），把上限调大后立即恢复生效，
        /// 不需要重新录入。配 0 或负数表示不限制，退回"全部生效"的旧行为。
        ///
        /// 【影响的三条链路】
        ///   1. 生效：<see cref="Person.ActiveFeatureCount"/> 控制所有装配点
        ///      （城市 / 部队 / 内政）与 <see cref="Person.HasFeatrue(int)"/> 的判定范围；
        ///   2. 展示：武将详情、招募信息与列表的"特技"列只显示前 N 个；
        ///   3. 学习：<see cref="AbilityStudySystem"/> 的特技培育在已满 N 个时直接失败。
        ///
        /// 【为什么要做成剧本参数而不是常量】不同剧本的武将特技密度差异很大，
        /// 历史剧本动辄四五个特技，演义剧本两三个就够；统一写死会让某一类剧本失衡。
        /// </summary>
        [JsonProperty] public int maxActiveFeatureCount = DefaultMaxActiveFeatureCount;

        // ==================== AI 据点登用阈值 ====================
        //
        // 【为什么要配置化】原版把"人少才登用"的阈值写死成 `3 + 难度 * 2`（3..7），
        // 本项目沿用同一公式（CityAIOutpostCommand.AIEmployMax，对齐出处
        // s11_game_ai_kyoten_cmd_personnel.cpp:529-536）。但不同剧本的武将密度差别很大：
        // 大势力剧本一座城动辄七八人，永远触发不了登用；小势力剧本三人才刚够守城。
        // 因此把公式里的每个常量都提到剧本参数，**默认值等于原版公式**，不改默认行为。
        //
        // 判定链（改动只会调节"要不要去登用"，不改变登用本身的成功率判定）：
        //   AIEmployMax     → 据点人数 < 基数 + 难度×增量
        //   AIEmployEnemy   → 据点人数 < minPerson + max(minPerson, 上限基数)
        //   AIEmployEnemy   → 目标忠诚 < 忠诚上限基数 + 难度×增量
        //   AIEmployEnemyMax→ 据点人数 < 登用敌方保底触发人数

        /// <summary>
        /// AI 据点触发登用的人数基数（原版公式 <c>3 + 难度×2</c> 里的 3，缺省 3）。
        /// 玩家势力固定用本值，AI 势力再按难度叠加 <see cref="aiEmployTriggerPerDifficulty"/>。
        /// </summary>
        [JsonProperty] public int aiEmployTriggerBase = 3;

        /// <summary>
        /// AI 据点触发登用的人数随难度递增（原版公式里的 2，缺省 2）。
        /// 配 0 = 阈值不随难度变化。
        /// </summary>
        [JsonProperty] public int aiEmployTriggerPerDifficulty = 2;

        /// <summary>
        /// 登用敌方保底（AIEmployEnemyMax）的触发人数（原版写死 3，缺省 3）。
        /// 注意原版这一条**不吃难度加成**，与 <see cref="aiEmployTriggerBase"/> 是两条独立阈值。
        /// </summary>
        [JsonProperty] public int aiEmployEnemyMaxTrigger = 3;

        /// <summary>
        /// 挖玩家武将时的忠诚上限基数（原版 <c>92 + 难度×2</c> 里的 92，缺省 92）。
        /// 目标忠诚 ≥ 本值（+ 难度增量）时 AI 直接放弃，不去试探。
        /// </summary>
        [JsonProperty] public int aiEmployMinLoyalty = 92;

        /// <summary>
        /// 挖玩家武将的忠诚上限随难度递增（原版公式里的 2，缺省 2）。
        /// </summary>
        [JsonProperty] public int aiEmployLoyaltyPerDifficulty = 2;

        /// <summary>
        /// "人已够多就不再登用"的上限基数（原版 <c>minPerson + max(minPerson, 10)</c> 里的 10，缺省 10）。
        /// 据点人数 ≥ <c>minPerson + max(minPerson, 本值)</c> 时直接放弃登用。
        /// 调大 = 允许人多的据点继续登用；调成 0 = 退回 <c>minPerson * 2</c> 的更严口径。
        /// </summary>
        [JsonProperty] public int aiEmployPersonLimitBase = 10;

        /// <summary>
        /// AI 据点单回合登用人数的覆盖值（缺省 0 = 沿用 <c>AIConfig.recruitPersonMaxPerTurn</c> 的全局值）。
        ///
        /// 配大于 0 时覆盖全局值，供剧本按自己的武将密度调节"一回合最多挖几个人"。
        /// 注意这只是**基数**：在野人数 ≥ 4 时仍会按"在野人数的一半"上调，
        /// 开局前 N 回合还会再 +1（见 CityAI.AIRecruitPerson），这两条原版节奏不变。
        /// </summary>
        [JsonProperty] public int aiEmployMaxPerTurnOverride = 0;

        // ==================== AI 据点探索阈值 ====================

        /// <summary>
        /// AI 据点触发探索的人数基数（原版公式 <c>3 + 难度×2</c> 里的 3，缺省 3）。
        /// 判定见 <c>CityAIOutpostCommand.AISearchMax</c>，对齐出处
        /// s11_game_ai_kyoten_cmd_personnel.cpp:425-432。
        /// </summary>
        [JsonProperty] public int aiSearchTriggerBase = 3;

        /// <summary>
        /// AI 据点触发探索的人数随难度递增（原版公式里的 2，缺省 2）。
        /// 配 0 = 阈值不随难度变化。
        /// </summary>
        [JsonProperty] public int aiSearchTriggerPerDifficulty = 2;

        /// <summary>
        /// 每名**未发现武将**给探索阈值增加的权重（缺省 1）。
        ///
        /// 【为什么需要这一项】原版只看"据点人数 &lt; 3 + 难度×2"，不看城里还藏着几个人。
        /// 结果是人多但没人发现的据点永远不会去搜 —— 那些未发现武将等于永久沉底。
        /// 本项让阈值随未发现武将数上浮：藏的人越多，越允许多几个已有人手时仍去探索。
        ///
        /// 与 <c>AIConfig.searchInvisibleChancePerPerson</c> 是同一思路的两个面：
        /// 那个管"搜的时候成功率/概率"，这个管"要不要派人去搜"。
        /// 配 0 = 关闭加成，退回原版"只看人数"的口径。
        /// </summary>
        [JsonProperty] public int aiSearchInvisibleBonusPerPerson = 1;

        /// <summary>
        /// 未发现武将对探索阈值的加成封顶（缺省 3，配 0 = 不封顶）。
        /// 防止大城里几十个未发现武将把阈值抬到永远触发、把行动力全砸在搜索上。
        /// </summary>
        [JsonProperty] public int aiSearchInvisibleBonusMax = 3;

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
        /// 宝物 / 装备产出配置（键 = GainPlace 的 ItemDiscover* 段）。
        /// 探索人才时的珍品产出概率、势力与城市冷却、开局保护、累计上限都在本表调参，
        /// 代码侧不硬编码（见 <see cref="Sango.Core.ForceItemDiscover"/>）。
        /// </summary>
        [JsonProperty] public GainValueConfig itemDiscoverGain = new GainValueConfig();

        /// <summary>
        /// 能力研究 / 培育配置（键 = GainPlace 的 AbilityStudy* 段）。
        /// 势力级能力研究的金钱消耗、培育的金钱消耗 / 单次提升点数 / 触发概率都在本表调参，
        /// 代码侧不硬编码（见 <see cref="Sango.Core.AbilityStudySystem"/>）。
        /// </summary>
        [JsonProperty] public GainValueConfig abilityStudyGain = new GainValueConfig();

        /// <summary>
        /// 国库基础容量（宝物与装备合计件数）。
        /// 国库上限 = 本值 + 己方城数 × <see cref="treasuryLimitPerCity"/>，
        /// 口径见 <see cref="Sango.Core.Force.TreasuryLimit"/>。
        ///
        /// **默认 0 = 不设上限**（策划案 4.2：v1 暂不限制国库容量，先把产出与授予链路跑通）。
        /// 本值与 <see cref="treasuryLimitPerCity"/> **同时**为 0 时表示完全关闭容量限制；
        /// 任意一项配成大于 0 即恢复上限计算。
        /// </summary>
        [JsonProperty] public int treasuryLimitBase = 0;

        /// <summary>
        /// 国库每城容量增量（每多一座城可多存几件珍品）。
        /// 默认 0 = 不设上限（与 <see cref="treasuryLimitBase"/> 同时为 0 时生效）。
        /// </summary>
        [JsonProperty] public int treasuryLimitPerCity = 0;

        /// <summary>
        /// 装备变更技巧点消耗：授予或没收**一件**珍品（宝物 / 装备）扣多少技巧点。
        ///
        /// 定位说明（2026-10 改版）：授予 / 没收不再走"人事赏罚"（不占行动力、不限每回合次数、
        /// 不影响忠诚），而是当作**装备管理**操作 —— 成本只有技巧点，调参口径见
        /// <see cref="Sango.Core.ItemGrantRules.GetTechniqueCost"/>。
        /// 默认 50；配 0 = 免费（纯沙盒调试用）。
        /// </summary>
        [JsonProperty] public int itemChangeTechniqueCost = 50;

        /// <summary>
        /// 势力技巧点上限。对齐原版 <c>Database::max_force_tech_point_</c>
        /// （s11_db.h:173 默认 10000，读取处 s11_sys_scen_force.cpp:674）。
        /// 所有技巧点增减都会 clamp 到 [0, 该值]；配 0 表示不设上限。
        /// </summary>
        [JsonProperty] public int maxForceTechniquePoint = 10000;

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
        /// 部队→部队 兵力伤害公式（逻辑库名，见 TroopDamageMethod）。
        /// 默认 Open311 = 逐行对齐原版 calc_troops_damage；
        /// 留空或写错都会自动回落到 Open311，不会导致战斗中断。
        /// </summary>
        [JsonProperty] public string troopDamageMethod = "Open311";

        /// <summary>
        /// 部队→据点守军 兵力伤害公式（逻辑库名，见 GarrisonDamageMethod）。默认 Open311。
        /// </summary>
        [JsonProperty] public string garrisonDamageMethod = "Open311";

        /// <summary>
        /// 部队→建筑 耐久伤害公式（逻辑库名，见 DurabilityDamageMethod）。默认 Open311。
        /// </summary>
        [JsonProperty] public string durabilityDamageMethod = "Open311";

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
        /// 全局和平回合数。
        ///
        /// 开局前若干回合内，所有 AI 势力都不会主动发起攻击行为：
        ///   · 出城攻打敌方城池 / 夺回被占港关；
        ///   · 跨城支援友军、向前线派补给队与工程队；
        ///   · 部队自主改派为进攻任务；
        ///   · 由计略引起的破盟宣战。
        /// 玩家（含玩家委任的部队）不受此限制；AI 的防守与驱逐入侵敌军照常执行。
        ///
        /// 字段名沿用旧存档的 AIAttackProtectedCount 以保证读档兼容，语义已扩展为"全局和平"。
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
        /// 破城时候的抓捕率(百分比)
        /// </summary>
        [JsonProperty] public int captureChangceWhenCityFall = 10;

        /// <summary>
        /// 【空置】最后一城时候的抓捕率(百分比)。
        ///
        /// 保留字段只为兼容旧剧本/旧存档（JSON 里可能已有这个键，删字段会让反序列化报警），
        /// **没有任何生效点**：原版势力最后一城被攻陷时是全员必俘、不掷骰
        /// （s11_sys_event_capture.cpp:452-456 的 `not node.remaining` 分支），
        /// 本项目城陷流程的最后一城分支同样无条件进待俘列表（见 City.OnFall）。
        /// 界面上对应的调节项已移除，不要再接入新逻辑。
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
        /// 进攻时候留守最低的兵力。
        ///
        /// 对齐原版 s11_game_ai_kyoten_cmd_mil_attack.cpp:68
        ///   min_kyoten_troops = min(min_kyoten_troops, 15000 + rand_int(5000))
        /// 原版留守兵力上限约 15000~20000，这里取固定下限 15000。
        ///
        /// 【注意】此值必须与 <c>CityAI.AICanAttack</c> 的兵力门槛（按个性 8000~12000）配合。
        /// 旧值 30000 远高于 AICanAttack 门槛，会卡住"兵力够决定进攻、却组不成部队"的城市
        /// （典型：初始 28000 兵的许昌反复"决定进攻"但从未真正出城）。
        /// </summary>
        [JsonProperty] public int minTroopsKeepWhenAttack = 15000;

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

        // ────────────────────────────────────────────────────────────────
        // 【空置】cityFallCanKeep* 这一组"城市沦陷保留比例"已不再生效。
        //
        // 城陷掠夺已改为照搬原版公式（s11_sys_event_kanraku.cpp:264-277）：
        //   rate = 5；攻方主将存活时 rate = max(rate, 主将魅力 / 10)；
        //   金 / 粮 / 兵力 / 兵装 全部 × rate / 100；农业与商业开发值不参与。
        // 这组数组当初是"按城市等级掷骰保留 10%~N%"的旧设计，没有原版出处。
        //
        // 字段保留只为兼容旧剧本 / 旧存档里已经写入的键（删字段会让反序列化报警），
        // 逻辑侧已无任何读取点，界面上对应的调节项也一直是注释状态。
        // ────────────────────────────────────────────────────────────────

        /// <summary>[空置] 城市沦陷可以保留金钱比例(百分比)，见上方说明</summary>
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
        /// 暴击倍率(百分比)。
        ///
        /// 【原版】attack_critical_result 命中后统一 <c>damage *= 1.15f</c>
        /// （s11_sys_damage.cpp:359-363），即 115。
        /// 本项目旧默认值是 150（×1.5），已按原版改为 115。
        /// </summary>
        [JsonProperty] public int skillCriticalFactor = 115;

        /// <summary>
        /// 战法(kind=2)命中后触发单挑的基础概率(百分比)，0 = 关闭该触发。
        /// 实际概率取发起部队属性 Troop.duelChance（本值 + Action 的修正），夹在 0~100 之间；
        /// 另外还要求该战法自身的 Skill.canTriggerDuel 为 true（战法级别的开关）。
        /// </summary>
        [JsonProperty] public int skillDuelChance = 5;

        /// <summary>
        /// 【空置】单挑寿命模式。
        ///
        /// 已改为沿用全局寿命模式（剧本信息锁定优先，其次本类的 <see cref="lifeMode"/>），
        /// 单挑内部 LifeMode 由 <see cref="Sango.Core.Duel.DuelRules.GetLifeMode"/> 统一解析：
        /// 史实 / 长寿 → Normal（按年龄衰减），虚构 / 无死亡 → Virtual（不衰减）。
        ///
        /// 字段保留只为兼容旧剧本 / 旧存档里已写入的键，逻辑侧已无读取点，界面上对应的开关也已移除。
        /// </summary>
        [JsonProperty] public int duelLifeMode = 0;

        /// <summary>
        /// 【空置】单挑战死频率。已并入全局 <see cref="battleDeathMode"/>，见该字段说明。
        /// 字段保留只为兼容旧剧本 / 旧存档，逻辑侧已无读取点。
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
        [JsonProperty] public int pathfindingSafeCount = 10000000;

        /// <summary>
        /// 寿命模式（对应 Open311 的 LifeModeEnum）。
        ///
        /// 0 = 史实：按武将的死亡年推算，能力值越高越晚寿终，会正常老死；
        /// 1 = 长寿：在史实基础上整体延后 <see cref="PersonLifeCycle.LongevityLife"/> 年，
        ///    相当于"人人都活得久"，势力不会因老将凋零而断层；
        /// 2 = 虚构：不结算寿终（只有战死/处决/事件会死人）；
        /// 3 = 虚构无死亡：与虚构相同，保留给"英雄集结"类剧本。
        ///
        /// 默认 **虚构无死亡（3）**：本项目当前以"武将不因年龄退场"为默认体验；
        /// 需要史实寿终时，在剧本参数界面把「寿命模式」改成"史实"即可（老存档保留自己存档里的值）。
        ///
        /// 【会被剧本信息覆盖】剧本信息（<see cref="ScenarioInfo.lifeMode"/>）取值 ≥0 时，
        /// 以剧本信息为准，本字段在该剧本内不生效、界面上只显示不可改 —— 寿命模式属于
        /// 剧本作者的史实设定（"英雄集结"必须无死亡），不该由玩家在开局参数里改。
        /// 解析入口：<see cref="PersonLifeCycle.GetLifeMode"/>。
        /// </summary>
        [JsonProperty] public int lifeMode = 3;

        /// <summary>
        /// 战死频率：0 = 无，1 = 普通(2%)，2 = 高(5%)。
        ///
        /// 【全局唯一口径】单挑的落败战死、俘虏处斩判定（原版 shodan2 的战死系数）
        /// 都读这一个值，对应 Open311 的 <c>get_battle_death_mode()</c>。
        /// 单挑不再有独立开关（原 duelDeathMode 已空置）。
        /// 单挑侧读取入口：<see cref="Sango.Core.Duel.DuelRules.GetBattleDeathMode"/>。
        /// </summary>
        [JsonProperty] public int battleDeathMode = 1;

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
        /// 只管控**常规剧本事件**。关掉后的行为：
        ///   · 常规事件不再评估任何触发 —— 连条件树都不求值，零开销；
        ///   · 不再推进定时事件队列；
        ///   · 不再重试"因演出占用而暂缓"的触发。
        ///
        /// **固定游戏事件不受影响**：标记了
        /// <see cref="Sango.Core.Event.EventDefinition.AlwaysEnabled"/> 的事件
        /// （结局演出、必须给玩家的退出出口等）照常触发 —— 它们是"游戏流程完整性"的一部分，
        /// 被关掉会让剧本断在半路（例如统一天下后既没有结局、也回不到开始页面）。
        ///
        /// 两点刻意的取舍：
        ///   · **已经在播的那一个事件照常播完** —— 中途掐断会留下半开的窗口与对话框，
        ///     反而要写一堆清理逻辑；而且玩家关开关时通常正是被某个事件打断。
        ///   · **定时待办项保留** —— 重新打开后按回合逐条补播（每回合只成功播一条），
        ///     不会一次性连播二十个事件把玩家埋掉。
        ///
        /// 调试面板的"强制触发"**不受本开关影响**（那是显式意图，不是自动触发）。
        /// </summary>
        [JsonProperty] public bool eventSystemEnabled = true;

        #region 外交系统参数

        // 【已删除】历史遗留的、在 C# 侧没有任何读取点的一批外交参数已清理，包括：
        //   · raw 尺度的关系阈值 / 关系增减（diplomacy*RelationThreshold / Increase / Decrease）
        //   · 旧版外交成功率模型（diplomacy*BaseSuccessRate / *RelationFactor / *Max|MinSuccessRate
        //     以及 diplomacy*ChanceDenominator / ChanceOffset）
        //   · 使者能力加成、资源价值加成、赎回费用因子（diplomacyAbilityBonusMax、diplomacyRansom*CostFactor）
        //   · 月度关系漂移参数（relationChangePerMonth / relationChangeChance）
        // 外交判定统一走 DiplomacyRules 的 like(0~100) 公式；需要新的可调参数时请加在那里。

        /// <summary>
        /// 送礼金额
        /// </summary>
        [JsonProperty] public int diplomacySendGiftAmount = 1000;

        /// <summary>
        /// 通商黄金收入增加
        /// </summary>
        [JsonProperty] public int diplomacyTradeGoldIncrease = 50;

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

        /// <summary>外交失败基础惩罚值（like 尺度 0~100；旧档存的是 raw 量级，读取时会 /100 折算）</summary>
        [JsonProperty] public int diplomacyFailedBasePenalty = 3;

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
        /// 原版量级：离间 / 流言 / 教唆"被察觉"分支 -4（`s11_sys_person_order_strategy.cpp:365-369`）。
        /// 关系值已改为 0~100 尺度，故直接取 4。
        /// </summary>
        [JsonProperty] public int cityStrategyDetectedPenalty = 4;

        /// <summary>
        /// 二虎竞食成功后两个目标势力的交情恶化值（固定值，不随成功率缩放）。
        /// 原版关系区间 0..100，破盟 / 停战破裂的事件量级是 -30 ~ -50。
        /// 本工程关系值已改为与 Open311 同尺度的 0~100，故直接取 30
        ///（旧值 3000 是 ±5000 时代的 raw 刻度，迁移时会由值域探测自动折算）。
        /// </summary>
        [JsonProperty] public int cityStrategyTwoTigersRelationDecrease = 30;

        /// <summary>
        /// 势力关系恶化到该值（含）以下时，AI 会放弃与对方的同盟并转向敌对。
        /// 对应原版脚本 808 新外交战争.cpp 的"relations &lt;= 0 即破盟"。
        ///
        /// 【尺度】关系表现已与 Open311 同为 0~100（50 中立、钳制下限 0）。
        /// 原版脚本的 0 在 raw(±5000) 时代被写成 -3000，换算回 like 恰为 20
        ///（`DiplomacyRules.GetYookouLevel` 的"险恶"分界），故取 20。
        /// 若配成负数，判定会永不成立，二虎竞食后的破盟宣战将彻底失效。
        /// </summary>
        [JsonProperty] public int cityStrategyWarTriggerRelation = 20;

        /// <summary>
        /// 城市计略彻底失败（被识破）时对"发起方与各目标方"关系的反噬值（0 表示不反噬）。
        /// 原版量级：离间 / 流言 / 教唆"失败"分支 -8 ~ -10
        ///（`s11_sys_person_order_strategy.cpp:395-396`、`:427-428`、`:1268`、`:1294`、`:1325`）。
        /// 关系值已改为 0~100 尺度，故取 8。
        /// </summary>
        [JsonProperty] public int cityStrategyFailedPenalty = 8;

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
        /// 二虎竞食挑拨凭据的有效期（回合，1 回合 = 1 旬）。
        /// 过期的凭据不能再触发破盟，避免长局里累积出连锁反应。
        /// （势力级主动施计与"计略记仇"已随 CityStrategyAI 精简一并移除，
        ///   这里只服务于仍在使用的 <c>Force.TakeStrategyWarSeed</c>。）
        /// </summary>
        [JsonProperty] public int cityStrategyGrudgeKeepTurns = 6;

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
        /// <remarks>like 尺度 0~100（关系表已改为与 Open311 同尺度）；旧档的 300 会在运行时按 /100 折算。</remarks>
        [JsonProperty] public int recruitForeignRelationPenalty = 3;

        /// <summary>
        /// 挖角外交代价 - 失败被察觉：登庸"在职于其它势力"的武将失败时，对双方关系造成的下降值。
        /// 比成功轻，配 0 表示不扣。用于表达"挖角意图暴露"的分量。
        /// </summary>
        /// <remarks>like 尺度；旧档的 100 会在运行时按 /100 折算。</remarks>
        [JsonProperty] public int recruitForeignRelationPenaltyFailed = 1;

        /// <summary>
        /// 招募系统 - 基础相性值
        /// </summary>
        [JsonProperty] public int recruitBaseCompatibility = 25;

        // ==================== 运输队 / 单队上限 ====================
        //
        // 对齐出处：Open311 `Database`（s11_db.h:200-206）的剧本级默认值。
        // 这些是"单支部队"的容量上限，与据点存储上限、势力库存都不同：
        // 运输命令据此对装载量做 clamp，兵装裁剪也据此判断目标还能收多少。
        //
        // 【调参入口】剧本配置界面 →"运输部队携带上限"分区（UIScenarioVariables）。
        // 【0 的语义】配 0（或负数）表示**不设上限**，由下面的 Transport*Limit 属性折算成 int.MaxValue，
        // 调用方直接 Math.Min 即可，不要用原始字段做上限判断。

        /// <summary>单支运输队携带兵力上限的缺省值（原版 60000，剧本配置里的"最大带兵数量"）</summary>
        public const int DefaultMaxUnitTransportTroops = 60000;

        /// <summary>单支运输队携带粮食上限的缺省值（原版 500000，剧本配置里的"最大粮食数量"）</summary>
        public const int DefaultMaxUnitTransportFood = 500000;

        /// <summary>单支运输队携带资金上限的缺省值（原版 100000，剧本配置里的"最大金钱数量"）</summary>
        public const int DefaultMaxUnitTransportGold = 100000;

        /// <summary>单支运输队每种兵装上限的缺省值（原版 100000，剧本配置里的"最大兵装数量"）</summary>
        public const int DefaultMaxUnitEquipmentQuantity = 100000;

        /// <summary>单支运输队最多携带的兵力 —— 原版 60000</summary>
        [JsonProperty] public int maxUnitTransportTroops = DefaultMaxUnitTransportTroops;

        /// <summary>单支运输队最多携带的粮食 —— 原版 500000</summary>
        [JsonProperty] public int maxUnitTransportFood = DefaultMaxUnitTransportFood;

        /// <summary>单支运输队最多携带的资金 —— 原版 100000</summary>
        [JsonProperty] public int maxUnitTransportGold = DefaultMaxUnitTransportGold;

        /// <summary>
        /// 单支运输队单类"兵装"上限 —— 原版 100000。
        /// 注意：这是**转换前的基准值**，实际可携带量必须再走
        /// <see cref="GetTransportItemLimit"/>（内部调用 <see cref="ItemType.TransformLimit"/>）。
        /// </summary>
        [JsonProperty] public int maxUnitEquipmentQuantity = DefaultMaxUnitEquipmentQuantity;

        /// <summary>单支部队攻城器械 / 舰船上限 —— 原版 100</summary>
        [JsonProperty] public int maxUnitCrewEquipmentQuantity = 100;

        /// <summary>运输携带上限：兵力（配置 &lt;= 0 视为不限，返回 int.MaxValue）</summary>
        public int TransportTroopLimit => maxUnitTransportTroops > 0 ? maxUnitTransportTroops : int.MaxValue;

        /// <summary>运输携带上限：粮食（配置 &lt;= 0 视为不限，返回 int.MaxValue）</summary>
        public int TransportFoodLimit => maxUnitTransportFood > 0 ? maxUnitTransportFood : int.MaxValue;

        /// <summary>运输携带上限：资金（配置 &lt;= 0 视为不限，返回 int.MaxValue）</summary>
        public int TransportGoldLimit => maxUnitTransportGold > 0 ? maxUnitTransportGold : int.MaxValue;

        /// <summary>
        /// 运输队某类物品（兵装 / 攻城器械 / 舰船）的携带上限 —— **必须走转换上限**。
        ///
        /// 本项目的兵装类库存上限一律由 <see cref="ItemType.TransformLimit"/> 换算得出
        ///（与城内仓库上限同口径，见 City.cs:3694 / BuildingPlanner.cs:882）：
        ///   · p1 == 0（枪 / 戟 / 弩 / 军马）→ 原值，即剧本配置的"最大兵装数量"；
        ///   · p1 &gt; 0（冲车 / 井阑 / 楼船等，数据里为 10）→ 配置值 × p1 / 1000。
        /// 直接把 maxUnitEquipmentQuantity 当上限是错的：器械 / 舰船这类 p1=10 的物品
        /// 会因此放大 100 倍。任何"运输能带多少兵装"的计算都必须经过这里。
        /// 配置 &lt;= 0 视为不限，返回 int.MaxValue。
        /// </summary>
        /// <param name="itemType">物品类型（null 返回 0）</param>
        public int GetTransportItemLimit(ItemType itemType)
        {
            if (itemType == null) return 0;
            if (maxUnitEquipmentQuantity <= 0) return int.MaxValue;
            return itemType.TransformLimit(maxUnitEquipmentQuantity);
        }

        /// <summary>
        /// 招募系统 - 在野 / 灭国俘虏的等效忠诚基准。
        ///
        /// 对齐原版（s11_sys_scen_person.cpp:2209-2210）：在野或已灭势力的俘虏，
        /// 忠诚按固定值参与计算 —— 简单难度 60，其它难度 70。
        /// </summary>
        [JsonProperty] public int recruitWildLoyaltyBase = 70;

        /// <summary>
        /// 招募系统 - 默认义理ID
        /// </summary>
        [JsonProperty] public int recruitDefaultArgumentationId = 3;

        /// <summary>
        /// 招募系统 - 基础成功率
        /// </summary>
        [JsonProperty] public int recruitBaseSuccessRate = 45;

        /// <summary>
        /// 招募系统 - 相性影响系数分子。
        ///
        /// 对齐原版登用公式的 `(相性差) / 5`，因此取 1/5。
        /// 原默认值是 3/2（相性影响被放大 7.5 倍），与 Open311 不符，已修正。
        /// </summary>
        [JsonProperty] public int recruitCompatibilityFactorNumerator = 1;

        /// <summary>
        /// 招募系统 - 相性影响系数分母
        /// </summary>
        [JsonProperty] public int recruitCompatibilityFactorDenominator = 5;

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
        /// 招募系统 - 随机影响最大值。
        ///
        /// 对齐原版 `rand_int(Giri_Max - giri)`（s11_sys_scen_person.cpp:2222）：
        /// 上限是义理最大值 5，减去目标的义理档位，所以实际范围是 0..(5−giri)。
        /// 公式侧已写成 `Range(0, recruitRandomMax - 义理)`，因此这里取 5。
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

        /// <summary>
        /// 取适应等级名称。
        ///
        /// 【取值顺序】
        /// <list type="number">
        /// <item>优先查剧本通用数据 <c>AbilityLevelTypes.json</c>（见 <see cref="PersonAbilityValue.GetLevelName"/>）：
        /// 这是当前唯一的适应等级配置源，级数不限于 4 级，可支持 Ｓ１/Ｓ２… 等扩展等级；</item>
        /// <item>数据未就绪或等级下标超出配置时，回退到剧本参数 <see cref="personAbilityName"/> 数组（旧数据兜底）；</item>
        /// <item>仍然超范围时交给可扩展钩子 <see cref="GameEvent.OnGetAbilityName"/>，最后取数组末位。</item>
        /// </list>
        /// </summary>
        /// <param name="lvl">适应等级下标（负数按 0 处理）</param>
        /// <returns>适应等级名称</returns>
        public string GetAbilityName(int lvl)
        {
            if (lvl < 0) lvl = 0;

            // 1.优先 AbilityLevelTypes
            string name = PersonAbilityValue.GetLevelName(lvl);
            if (!string.IsNullOrEmpty(name))
                return name;

            // 2.回退到剧本参数数组
            if (personAbilityName != null && personAbilityName.Length > 0)
            {
                if (lvl < personAbilityName.Length)
                    return personAbilityName[lvl];

                // 3.超出旧数组范围：走可扩展钩子，再不行取末位
                if (GameEvent.OnGetAbilityName != null)
                {
                    string ext = GameEvent.OnGetAbilityName(lvl);
                    if (!string.IsNullOrEmpty(ext))
                        return ext;
                }
                return personAbilityName[personAbilityName.Length - 1];
            }

            return string.Empty;
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

        #region 名字列宽度计算（供所有 SortByName 使用）

        /// <summary>
        /// 内置默认的名字最大支持字数。
        /// 没有任何剧本（纯逻辑推演 / 编辑器内）时使用，与字段 <see cref="maxNameLength"/> 的默认值一致。
        /// </summary>
        public const int DefaultMaxNameLength = 4;

        /// <summary>
        /// 名字列每个"字"的宽度系数。
        /// 中文名字按 1.05 个字符宽度估算（略放宽，避免边界字被截断）。
        /// </summary>
        public const float NameWidthPerChar = 1.05f;

        /// <summary>
        /// 计算名字列的内容宽度（单位：字符宽度）。
        ///
        /// 宽度 = 1.05 × (名字最大支持字数 + extraChars)。
        /// 例如默认 4 字：普通名字列 = 1.05 × 4 = 4.2 字宽；军团名（"第N军团"额外 4 字） = 1.05 × (4 + 4) = 8.4 字宽。
        ///
        /// 取值优先级：正在运行的剧本(<see cref="Scenario.Cur"/>) → 当前选中的剧本(<see cref="Scenario.CurSelected"/>) → 内置默认变量。
        /// 这样无论在游戏内、剧本选择预览、还是编辑器里都能得到一致的宽度。
        /// </summary>
        /// <param name="extraChars">名字列额外预留的字符数（例如军团名"第N军团"额外 4 字），默认 0</param>
        /// <returns>名字列内容宽度（单位：字符宽度，实际像素请再乘单字像素）</returns>
        public static float GetNameColumnWidth(int extraChars = 0)
        {
            ScenarioVariables v = GetActive();
            int len = (v != null && v.maxNameLength > 0) ? v.maxNameLength : DefaultMaxNameLength;
            return (len + extraChars) * NameWidthPerChar;
        }

        /// <summary>
        /// 获取当前生效的剧本变量（运行时优先取正在运行的剧本，其次取当前选中的剧本，最后回落到内置默认变量）。
        /// </summary>
        /// <returns>当前生效的剧本变量，永不为空</returns>
        static ScenarioVariables GetActive()
        {
            Scenario cur = Scenario.Cur;
            if (cur != null && cur.Variables != null)
                return cur.Variables;

            Scenario selected = Scenario.CurSelected;
            if (selected != null && selected.Variables != null)
                return selected.Variables;

            return Scenario.DefaultVariables;
        }

        #endregion
    }
}
