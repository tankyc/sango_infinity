using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System;
using System.Linq;

namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UIScenarioVariables : UGUIWindow, IVariablesSetting
    {
        public Text title;

        public GameObject titleObj;
        public GameObject bigTitleObj;
        public GameObject integerObj;
        public GameObject floatObj;
        public GameObject integerSliderObj;
        public GameObject floatSliderObj;
        public GameObject dropdownObj;
        public GameObject toggleObj;
        public GameObject toggleGroupObj;

        protected List<GameObject> pool_titleObj = new List<GameObject>();
        protected List<GameObject> pool_bigTitleObj = new List<GameObject>();
        protected List<GameObject> pool_integerObj = new List<GameObject>();
        protected List<GameObject> pool_floatObj = new List<GameObject>();
        protected List<GameObject> pool_integerSliderObj = new List<GameObject>();
        protected List<GameObject> pool_floatSliderObj = new List<GameObject>();
        protected List<GameObject> pool_dropdownObj = new List<GameObject>();
        protected List<GameObject> pool_toggleObj = new List<GameObject>();
        protected List<GameObject> pool_toggleGroupObj = new List<GameObject>();
        /// <summary>按钮行池。模板复用 titleObj（排版一致），但**单独成池**，
        /// 避免被加过 Button/Image 的对象回流到普通标题行里误响应点击。</summary>
        protected List<GameObject> pool_buttonObj = new List<GameObject>();
        /// <summary>输入行池。同样复用 titleObj 做排版，只多挂 Image + InputField。</summary>
        protected List<GameObject> pool_inputObj = new List<GameObject>();
        protected List<GameObject> itemList = new List<GameObject>();

        /// <summary>
        /// 参数预设输入框里的名字。参数界面每次 <see cref="RefreshSetting"/> 都会整页重建，
        /// 输入框对象会被回收，所以名字必须存在字段上，否则刷新一次就被清空。
        /// </summary>
        string presetNameInput;

        public Action onSure;
        public Action onCancel;

        Scenario TargetScenario;
        ShortScenario TargetShortScenario;

        public override void OnOpen()
        {
            
        }

        public override void OnOpen(params object[] objects)
        {
            if(objects.Length == 1)
            {
                onSure = null; onCancel = null;
                for (int i = 0; i < itemList.Count; i++)
                    RemoveItem(itemList[i]);
                itemList.Clear();
                TargetScenario = Scenario.CurSelected;
                TargetShortScenario = objects[0] as ShortScenario; 
                if (TargetScenario.Variables == null)
                    TargetScenario.LoadVariables();
                ShowVariables(TargetScenario);
            }
            else
            {
                onSure = null; onCancel = null;
                for (int i = 0; i < itemList.Count; i++)
                    RemoveItem(itemList[i]);
                itemList.Clear();
                TargetScenario = objects[0] as Scenario;
                if (objects.Length > 1)
                    onSure = objects[1] as System.Action;
                if (objects.Length > 2)
                    onCancel = objects[2] as System.Action;
                if (TargetScenario.Variables == null)
                    TargetScenario.LoadVariables();
                ShowVariables(TargetScenario);
            }
            
        }

        float[] lvlFactor = new float[] { 1f, 1f, 1.2f, 2f };
        float[] player_lvlFactor = new float[] { 1.2f, 1f, 1f, 0.9f };

        /// <summary>
        /// 寿命模式显示名，索引 = <see cref="ScenarioInfo.lifeMode"/> / <see cref="ScenarioVariables.lifeMode"/>：
        /// 0 史实 / 1 长寿 / 2 虚构 / 3 无死亡。
        /// </summary>
        static readonly string[] LifeModeNames = { "史实", "长寿", "虚构", "无死亡" };

        public void ShowVariables(Scenario scenario)
        {
            ScenarioVariables variables = scenario.Variables;
            AddBigTitle(scenario.Name);

            // 参数预设放在最前面：玩家进来先决定"用哪套参数"，再往下微调
            ShowPresetSection(scenario);

            AddTitle("难度选择");
            AddToggleGroupItem("难度", variables.difficulty, new List<string> { "简单", "普通", "困难", "超级" }, (v) =>
            {
                variables.difficulty = v;
                variables.foodFactor = lvlFactor[v];
                variables.goldFactor = lvlFactor[v];
                variables.playerFoodFactor = player_lvlFactor[v];
                variables.playerGoldFactor = player_lvlFactor[v];
                ApplyAIDifficultyBonus(variables, v);
                RefreshSetting();
            });

            AddTitle("AI每回合加成");
            AddNumberItem("AI每回合加钱", variables.aiTurnAddGold, 0, 10000, (v) => { variables.aiTurnAddGold = v; });
            AddNumberItem("AI每回合加粮", variables.aiTurnAddFood, 0, 100000, (v) => { variables.aiTurnAddFood = v; });
            AddNumberItem("AI每回合加士兵", variables.aiTurnAddTroops, 0, 100000, (v) => { variables.aiTurnAddTroops = v; });
            AddNumberItem("AI每回合加兵装", variables.aiTurnAddArms, 0, 100000, (v) => { variables.aiTurnAddArms = v; });

            AddTitle("剧本基础参数");
            AddToggleItem("<color=#ee2222>编辑生效</color>", variables.ApplyEdit, (v) => { variables.ApplyEdit = v; });

            // 名字最大支持字数：所有名字列(SortByName)的显示宽度按 1.05 × 本值 计算
            AddNumberItem("名字最大支持字数", variables.maxNameLength, 1, 20, (v) => { variables.maxNameLength = v; });

            AddNumberItem("电脑粮食倍率", variables.foodFactor, 0, 100, (v) => { variables.foodFactor = v; });
            AddNumberItem("电脑资金倍率", variables.goldFactor, 0, 100, (v) => { variables.goldFactor = v; });
            AddNumberItem("玩家粮食倍率", variables.playerFoodFactor, 0, 100, (v) => { variables.playerFoodFactor = v; });
            AddNumberItem("玩家资金倍率", variables.playerGoldFactor, 0, 100, (v) => { variables.playerGoldFactor = v; });
            AddNumberItem("行动力上限", variables.ActionPointLimit, 0, 10000, (v) => { variables.ActionPointLimit = v; });
            AddNumberItem("行动力获取倍率", variables.ActionPointFactor, 0, 100, (v) => { variables.ActionPointFactor = v; });
            AddToggleItem("年龄生效", variables.AgeEnabled, (v) => { variables.AgeEnabled = v; RefreshSetting(); });
            if (variables.AgeEnabled)
                AddToggleItem("能力随年龄变化", variables.EnableAgeAbilityFactor, (v) => { variables.EnableAgeAbilityFactor = v; });

            // 寿命模式（对应 Open311 的 LifeModeEnum，结算见 PersonLifeCycle.MonthlySettlement）：
            //   0 史实    —— 按武将死亡年推算，会正常老死
            //   1 长寿    —— 整体延后 PersonLifeCycle.LongevityLife 年（默认 20）
            //   2 虚构    —— 不结算寿终，只有战死 / 处决 / 事件会死人
            //   3 无死亡  —— 同虚构，保留给"英雄集结"类剧本
            // 注意：与上面的「年龄生效」不是一回事 —— 那个只控制能力值是否随年龄衰减。
            //
            // 【锁定规则】剧本信息（ScenarioInfo.lifeMode）取值 ≥0 时以剧本信息为准：
            // 这里只显示一行只读说明，玩家改不动 —— 寿命模式是剧本作者的史实设定
            // （"英雄集结"必须无死亡），不该在开局参数里被改成会把人老死的档位。
            if (scenario.Info != null && scenario.Info.HasLifeMode)
            {
                int lockedMode = Mathf.Clamp(scenario.Info.lifeMode, 0, LifeModeNames.Length - 1);
                AddTitle($"寿命模式：{LifeModeNames[lockedMode]}（由剧本信息锁定，不可修改）");
            }
            else
            {
                AddToggleGroupItem("寿命模式", variables.lifeMode,
                    new List<string> { "史实", "长寿", "虚构", "无死亡" },
                    (v) => { variables.lifeMode = v; });
            }

            // 战死频率：全局唯一口径（单挑战死、俘虏处斩的战死系数都读它），
            // 0 无 / 1 普通(2%) / 2 高(5%)
            AddToggleGroupItem("战死频率", variables.battleDeathMode,
                new List<string> { "无", "普通", "高" },
                (v) => { variables.battleDeathMode = v; });

            AddNumberItem("建筑间隔距离", variables.BuildingSpace, 0, 2, (v) => { variables.BuildingSpace = v; });
            AddToggleItem("是否让武将在登场年份到达后登场", variables.allowInvalidPersonValidWhenYearPass, (v) => { variables.allowInvalidPersonValidWhenYearPass = v;});

            //AddNumberItem("能力每级经验", variables.AbilityExpLevelNeed, 0, 10000, (v) => { variables.AbilityExpLevelNeed = (ushort)v; });
            //AddNumberItem("最高能力等级", variables.MaxAbilityLevel, 1, 20, (v) => { variables.MaxAbilityLevel = (byte)v; });
            //AddNumberItem("属性每点经验", variables.AttributeExpLevelNeed, 0, 10000, (v) => { variables.AttributeExpLevelNeed = (ushort)v; });
            //AddNumberItem("属性成长上限", variables.MaxAttributeGet, 0, 100, (v) => { variables.MaxAttributeGet = (byte)v; });
            //AddToggleItem("人口系统开关", variables.populationEnable, (v) => { variables.populationEnable = v; });
            //AddNumberItem("基础人口增长率", variables.populationIncreaseBaseFactor, 0, 1, (v) => { variables.populationIncreaseBaseFactor = v; });
            //AddNumberItem("人口上限基础值", variables.populationLimitBase, 0, 100000, (v) => { variables.populationLimitBase = v; });
            //AddNumberItem("每级城市人口上限增加值", variables.populationLimitPerLevel, 0, 10000, (v) => { variables.populationLimitPerLevel = v; });
            //AddNumberItem("基础兵役比例", variables.baseTroopPopulationRatio, 0, 1, (v) => { variables.baseTroopPopulationRatio = v; });
            //AddNumberItem("最大兵役比例", variables.maxTroopPopulationRatio, 0, 1, (v) => { variables.maxTroopPopulationRatio = v; });
            //AddNumberItem("人口对粮食消耗的影响系数", variables.populationFoodCostFactor, 0, 0.01f, (v) => { variables.populationFoodCostFactor = v; });
            //AddNumberItem("人口对金钱收入的影响系数", variables.populationGoldIncomeFactor, 0, 0.01f, (v) => { variables.populationGoldIncomeFactor = v; });
            AddNumberItem("队伍粮食基础消耗率", variables.baseFoodCostInTroop, 0, 100, (v) => { variables.baseFoodCostInTroop = v; });
            AddNumberItem("城池中粮食基础消耗率", variables.baseFoodCostInCity, 0, 100, (v) => { variables.baseFoodCostInCity = v; });
            AddNumberItem("城池缺粮后每回合逃跑的士兵比例", variables.runawayWhenCityFoodNotEnough, 0, 1, (v) => { variables.runawayWhenCityFoodNotEnough = v; });
            //AddNumberItem("民心对于收入的影响最低值", variables.popularSupportInfluenceMax, 0, 100, (v) => { variables.popularSupportInfluenceMax = v; });
            //AddNumberItem("民心影响的正负范围", variables.popularSupportInfluence, 0, 1, (v) => { variables.popularSupportInfluence = v; });
            AddNumberItem("治安对于收入的影响最低值", variables.securityInfluenceMax, 0, 100, (v) => { variables.securityInfluenceMax = v; });
            AddNumberItem("治安影响的正负范围", variables.securityInfluence, 0, 1, (v) => { variables.securityInfluence = v; });
            AddNumberItem("治安对征兵的影响值比例", variables.securityInfluenceRecruitTroops, 0, 1, (v) => { variables.securityInfluenceRecruitTroops = v; });
            AddNumberItem("每一月治安下降最大数", variables.securityChangeOnMonthStart, -100, 0, (v) => { variables.securityChangeOnMonthStart = v; });
            AddNumberItem("每一季度治安下降最大数", variables.securityChangeOnSeasonStart, -100, 0, (v) => { variables.securityChangeOnSeasonStart = v; });
            AddNumberItem("建筑建造最大回合数", variables.BuildMaxTurn, 0, 100, (v) => { variables.BuildMaxTurn = v; });

            //AddNumberItem("每一点农业带来的粮食收入", variables.agriculture_add_food, 0, 100, (v) => { variables.agriculture_add_food = v; });
            //AddNumberItem("每一点商业点带来的金币收入", variables.commerce_add_gold, 0, 100, (v) => { variables.commerce_add_gold = v; });
            AddNumberItem("每月变化的关系值", variables.relationChangePerMonth, -1000, 1000, (v) => { variables.relationChangePerMonth = v; });
            AddNumberItem("每月的关系变化率(百分比)", variables.relationChangeChance, 0, 100, (v) => { variables.relationChangeChance = v; });
            AddNumberItem("破城时候的抓捕率(百分比)", variables.captureChangceWhenCityFall, 0, 100, (v) => { variables.captureChangceWhenCityFall = v; });
            // 【已移除】"最后一城时候的抓捕率"：原版势力最后一城被攻陷时是全員必俘、不掷骰
            // （s11_sys_event_capture.cpp:452-456 的 `not node.remaining` 分支），
            // 本项目城陷流程的最后一城分支同样无条件入待俘列表（City.OnFall），
            // 该配置从来没有生效点，留在界面上会误导数值调整。
            AddNumberItem("队伍溃败时候的抓捕率(百分比)", variables.captureChangceWhenTroopFall, 0, 100, (v) => { variables.captureChangceWhenTroopFall = v; });
            AddNumberItem("单挑获胜时候的基础抓捕率(百分比)", variables.captureChangceWhenDuelWin, 0, 100, (v) => { variables.captureChangceWhenDuelWin = v; });

            AddTitle("战斗参数");
            AddNumberItem("全局和平回合数", variables.AIAttackProtectedCount, 0, 100, (v) => { variables.AIAttackProtectedCount = v; });
            AddNumberItem("基础伤害", variables.fight_base_damage, 0, 1000, (v) => { variables.fight_base_damage = v; });
            AddNumberItem("基准兵力(攻守兵力差)", variables.fight_base_troops_need, 0, 10000, (v) => { variables.fight_base_troops_need = v; });
            AddNumberItem("每多基准兵力,获得一次兵力系数增益", variables.fight_base_troop_count, 0, 1000, (v) => { variables.fight_base_troop_count = v; });
            AddNumberItem("兵力系数增益", (float)variables.fight_damage_magic_number, 0, 0.01f, (v) => { variables.fight_damage_magic_number = v; });
            AddNumberItem("基础火焰伤害", variables.baseFireDamage, 100, 3000, (v) => { variables.baseFireDamage = v; });
            AddToggleItem("火焰-允许蔓延到相邻格", variables.fireSpreadEnabled, (v) => { variables.fireSpreadEnabled = v; });

            AddTitle("剧本事件");
            AddToggleItem("启用剧本事件（常规剧情）", variables.eventSystemEnabled, (v) => { variables.eventSystemEnabled = v; });

            AddTitle("部队参数");
            AddNumberItem("攻击-武力影响(万分比)", variables.fight_troop_attack_strength_factor, 0, 100000, (v) => { variables.fight_troop_attack_strength_factor = v; });
            AddNumberItem("攻击-智力影响(万分比)", variables.fight_troop_attack_intelligence_factor, 0, 100000, (v) => { variables.fight_troop_attack_intelligence_factor = v; });
            AddNumberItem("攻击-统率影响(万分比)", variables.fight_troop_attack_command_factor, 0, 100000, (v) => { variables.fight_troop_attack_command_factor = v; });
            AddNumberItem("攻击-政治影响(万分比)", variables.fight_troop_attack_politics_factor, 0, 100000, (v) => { variables.fight_troop_attack_politics_factor = v; });
            AddNumberItem("攻击-魅力影响(万分比)", variables.fight_troop_attack_glamour_factor, 0, 100000, (v) => { variables.fight_troop_attack_glamour_factor = v; });

            AddNumberItem("防御-武力影响(万分比)", variables.fight_troop_defence_strength_factor, 0, 100000, (v) => { variables.fight_troop_defence_strength_factor = v; });
            AddNumberItem("防御-智力影响(万分比)", variables.fight_troop_defence_intelligence_factor, 0, 100000, (v) => { variables.fight_troop_defence_intelligence_factor = v; });
            AddNumberItem("防御-统率影响(万分比)", variables.fight_troop_defence_command_factor, 0, 100000, (v) => { variables.fight_troop_defence_command_factor = v; });
            AddNumberItem("防御-政治影响(万分比)", variables.fight_troop_defence_politics_factor, 0, 100000, (v) => { variables.fight_troop_defence_politics_factor = v; });
            AddNumberItem("防御-魅力影响(万分比)", variables.fight_troop_defence_glamour_factor, 0, 100000, (v) => { variables.fight_troop_defence_glamour_factor = v; });

            AddTitle("技能参数");
            AddNumberItem("每级兵种适应力对技能释放成功率的加成(百分比)", variables.skillSuccessRateAddByAbility, 0, 100, (v) => { variables.skillSuccessRateAddByAbility = v; });
            AddNumberItem("每级兵种适应力对技能暴击率的加成(百分比)", variables.skillCriticalRateAddByAbility, 0, 100, (v) => { variables.skillCriticalRateAddByAbility = v; });
            AddNumberItem("基础技能暴击率(百分比)", variables.baseSkillCriticalRate, 0, 100, (v) => { variables.baseSkillCriticalRate = v; });
            AddNumberItem("武力对暴击的加成值", variables.skillCriticalRateAddByStength, 0, 100, (v) => { variables.skillCriticalRateAddByStength = v; });
            AddNumberItem("暴击倍率(百分比)", variables.skillCriticalFactor, 100, 300, (v) => { variables.skillCriticalFactor = v; });
     
            AddTitle("单挑参数");
            AddNumberItem("战法命中后触发单挑的基础概率(百分比)", variables.skillDuelChance, 0, 100, (v) => { variables.skillDuelChance = v; });
            // 【已移除】单挑寿命模式 / 单挑战死频率：改为沿用全局
            //（寿命模式见上方「寿命模式」项，战死频率见上方「战死频率」项），
            // 否则会出现"寿终按史实算、单挑按虚拟算年龄衰减"的分裂。
            AddToggleItem("单挑-禁用一击必杀(一合取胜)", variables.duelDisableFirstTurnKill, (v) => { variables.duelDisableFirstTurnKill = v; });
            AddToggleItem("单挑-禁用捕缚(不会捕获敌将)", variables.duelDisableCapture, (v) => { variables.duelDisableCapture = v; });
            AddToggleItem("单挑-禁用AI退却", variables.duelDisableAIRetreat, (v) => { variables.duelDisableAIRetreat = v; });

            AddTitle("舌战参数");
            AddToggleItem("舌战-禁用会心(不出现追击/留情)", variables.debateDisableCritical, (v) => { variables.debateDisableCritical = v; });
            AddNumberItem("舌战-外交失败触发概率(百分比)", variables.debateChanceWhenDiplomacyFail, 0, 100, (v) => { variables.debateChanceWhenDiplomacyFail = v; });
            AddNumberItem("舌战-招募失败触发概率(百分比)", variables.debateChanceWhenRecruitFail, 0, 100, (v) => { variables.debateChanceWhenRecruitFail = v; });
            AddToggleItem("舌战-AI之间也触发(后台结算不弹界面)", variables.debateAllowAIVsAI, (v) => { variables.debateAllowAIVsAI = v; });
     

            AddTitle("缴获与保留参数");
            AddNumberItem("近战击溃部队缴获的金钱比例(百分比)", variables.defeatTroopCanGainGoldFactor, 0, 100, (v) => { variables.defeatTroopCanGainGoldFactor = v; });
            AddNumberItem("近战击溃部队缴获的粮食比例(百分比)", variables.defeatTroopCanGainFoodFactor, 0, 100, (v) => { variables.defeatTroopCanGainFoodFactor = v; });

           
            AddTitle("越狱参数");
            AddNumberItem("基础越狱概率(万分比)", variables.baseEscapeProbabllity, 0, 10000, (v) => { variables.baseEscapeProbabllity = v; });
            AddNumberItem("越狱概率每回合增长(万分比)", variables.baseEscapeProbablilityAddByTurn, 0, 1000, (v) => { variables.baseEscapeProbablilityAddByTurn = v; });

            AddTitle("AI参数");
            AddNumberItem("进攻时候留守最低的兵力", variables.minTroopsKeepWhenAttack, 0, 100000, (v) => { variables.minTroopsKeepWhenAttack = v; });
            AddNumberItem("进攻时候留守最低的粮食", variables.minFoodKeepWhenAttack, 0, 100000, (v) => { variables.minFoodKeepWhenAttack = v; });
            AddNumberItem("防御时候留守最低的兵力", variables.minTroopsKeepWhenDefence, 0, 100000, (v) => { variables.minTroopsKeepWhenDefence = v; });
            AddNumberItem("防御时候留守最低的粮食", variables.minFoodKeepWhenDefence, 0, 100000, (v) => { variables.minFoodKeepWhenDefence = v; });

            AddTitle("系统参数");
            AddNumberItem("寻路安全次数限制", variables.pathfindingSafeCount, 1000, 10000000, (v) => { variables.pathfindingSafeCount = v; });

            
            AddTitle("招募系统参数");
            AddNumberItem("可招募的忠诚度阈值", variables.recruitableLine, 0, 100, (v) => { variables.recruitableLine = v; });
            AddNumberItem("玩家招募加成", variables.playerRecruitAdd, 0, 100, (v) => { variables.playerRecruitAdd = v; });
            AddNumberItem("挖角敌将关系惩罚(成功)", variables.recruitForeignRelationPenalty, 0, 5000, (v) => { variables.recruitForeignRelationPenalty = v; });
            AddNumberItem("挖角敌将关系惩罚(失败)", variables.recruitForeignRelationPenaltyFailed, 0, 5000, (v) => { variables.recruitForeignRelationPenaltyFailed = v; });

            AddTitle("逃跑系统参数");
            AddNumberItem("城市中逃跑概率减少值", variables.escapeCityReduction, 0, 1000, (v) => { variables.escapeCityReduction = v; });
            AddNumberItem("部队中逃跑概率增加值", variables.escapeTroopIncrease, 0, 1000, (v) => { variables.escapeTroopIncrease = v; });
            AddNumberItem("最大逃跑概率", variables.escapeMaxProbability, 1000, 5000, (v) => { variables.escapeMaxProbability = v; });

            AddTitle("敌方部队发现参数");
            AddNumberItem("发现敌方新建部队的基础概率(万分比)", variables.discoverEnemyTroopBaseProbability, 0, 10000, (v) => { variables.discoverEnemyTroopBaseProbability = v; });
            AddNumberItem("军师智力对发现概率的影响系数(万分比)", variables.discoverEnemyTroopIntelligenceFactor, 0, 1000, (v) => { variables.discoverEnemyTroopIntelligenceFactor = v; });

            // 城市计略（流言）参数：用于遏制 AI 互相刷流言把忠诚打到可登庸线以下
            AddTitle("城市计略参数");
            AddNumberItem("流言忠诚下限闸门", variables.cityStrategyRumorLoyaltyFloor, 0, 100, (v) => { variables.cityStrategyRumorLoyaltyFloor = v; });
            AddNumberItem("同城流言冷却回合", variables.cityStrategyRumorImmunityTurns, 0, 36, (v) => { variables.cityStrategyRumorImmunityTurns = v; });
            AddNumberItem("AI褒奖响应标记回合", variables.cityStrategyRumorVictimRewardTurns, 0, 36, (v) => { variables.cityStrategyRumorVictimRewardTurns = v; });
            AddNumberItem("AI褒奖响应追加人数", variables.cityStrategyRumorVictimRewardBoost, 0, 10, (v) => { variables.cityStrategyRumorVictimRewardBoost = v; });

            AddBigTitle("成长资源获取");
            AddGainTable("功绩", variables.meritGain, GainValueConfig.MeritPlaces);
            AddGainTable("技巧点", variables.techniquePointGain, GainValueConfig.TechniquePointPlaces);
            AddGainTable("武将经验", variables.expGain, GainValueConfig.ExpPlaces);
            AddGainTable("兵种适性经验", variables.abilityExpGain, GainValueConfig.AbilityExpPlaces);
            AddGainTable("能力经验", variables.attributeExpGain, GainValueConfig.AttributeExpPlaces);

            // 宝物与装备：搜索人才的产出概率 / 限流（GainValueConfig 的 ItemDiscover* 段）
            // 与国库容量。节奏口径见 ForceItemDiscover 的类注释。
            AddBigTitle("宝物与装备产出");
            AddGainTable("搜索人才产出", variables.itemDiscoverGain, GainValueConfig.ItemDiscoverPlaces);
            // 国库容量：两项同时为 0 = 不设上限（策划案 4.2 的 v1 默认口径），
            // 任意一项调到大于 0 即恢复「基础容量 + 城数 × 每城增量」的上限计算
            AddNumberItem("国库基础容量(件，0=不限)", variables.treasuryLimitBase, 0, 999, (v) => { variables.treasuryLimitBase = v; });
            AddNumberItem("国库每城容量增量(件，0=不限)", variables.treasuryLimitPerCity, 0, 99, (v) => { variables.treasuryLimitPerCity = v; });

            // 装备变更（授予 / 没收一件珍品）的成本：技巧点，默认 50，0 = 免费。
            // 2026-10 改版后不再有忠诚曲线、不占行动力、不限每回合次数（见 ItemGrantRules）。
            AddNumberItem("装备变更技巧点消耗(0=免费)", variables.itemChangeTechniqueCost, 0, 10000, (v) => { variables.itemChangeTechniqueCost = v; });

            // 能力研究 / 培育：势力级能力研究的金钱消耗、培育的金钱消耗 /
            // 单次提升点数 / 触发概率（GainValueConfig 的 AbilityStudy* 段，见 AbilityStudySystem）。
            AddBigTitle("能力研究与培育");
            AddGainTable("能力研究与培育", variables.abilityStudyGain, GainValueConfig.AbilityStudyPlaces);

            AddBigTitle("Mod相关");
            GameEvent.OnScenarioVariablesSetting?.Invoke(this, scenario);
        }





        /// <summary>
        /// 根据难度预设AI每回合加成。
        /// </summary>
        void ApplyAIDifficultyBonus(ScenarioVariables variables, int difficulty)
        {
            switch (difficulty)
            {
                case 2: // 困难
                    variables.aiTurnAddGold = 200;
                    variables.aiTurnAddFood = 2000;
                    variables.aiTurnAddTroops = 500;
                    variables.aiTurnAddArms = 300;
                    break;
                case 3: // 超级
                    variables.aiTurnAddGold = 500;
                    variables.aiTurnAddFood = 5000;
                    variables.aiTurnAddTroops = 1000;
                    variables.aiTurnAddArms = 800;
                    break;
                default:
                    variables.aiTurnAddGold = 0;
                    variables.aiTurnAddFood = 0;
                    variables.aiTurnAddTroops = 0;
                    variables.aiTurnAddArms = 0;
                    break;
            }
        }

        /// <summary>
        /// 渲染"参数预设"区块：下拉读取 + 按钮保存。
        ///
        /// 预设来源见 <see cref="ScenarioVariablesPresetManager"/>：
        ///   · 模组：{模组}/Data/ScenarioVariables/{名字}.json
        ///   · 自建：{CustomEditRootPath}/Data/ScenarioVariables/{名字}.json（保存目标）
        /// 剧本用 <c>Info.variablesPreset</c> 指定默认读哪个，进界面时已由
        /// <c>Scenario.LoadVariables</c> 自动套用一次，这里只负责"换一套 / 存一套"。
        /// </summary>
        /// <param name="scenario">当前编辑的剧本</param>
        void ShowPresetSection(Scenario scenario)
        {
            if (scenario == null)
                return;

            AddTitle("参数预设");

            string current = scenario.Info != null ? scenario.Info.variablesPreset : null;
            // 输入框里上一次写的名字优先（整页刷新后要保住玩家刚输入的内容）
            if (string.IsNullOrEmpty(presetNameInput))
                presetNameInput = ScenarioVariablesPresetManager.SanitizeName(current);

            // 名字输入：保存 / 删除都以这里为准，回车或失焦时写回字段
            AddInputItem("预设名字（保存 / 删除都用它）", presetNameInput, (v) =>
            {
                presetNameInput = ScenarioVariablesPresetManager.SanitizeName(v);
            });

            List<string> names = ScenarioVariablesPresetManager.GetNames();
            if (names.Count == 0)
            {
                AddTitle("<color=#888888>（未找到预设文件，可在上面填名字后点保存）</color>");
            }
            else
            {
                int curIndex = 0;
                if (!string.IsNullOrEmpty(current))
                {
                    int idx = names.IndexOf(current);
                    if (idx >= 0)
                        curIndex = idx;
                }

                // 选中即套用：套用后整页重建，玩家能立刻看到每一项的新值
                AddDropdownItem("读取配置", curIndex, names, (v) =>
                {
                    if (v < 0 || v >= names.Count)
                        return;
                    if (ScenarioVariablesPresetManager.TryApply(scenario, names[v]))
                    {
                        if (scenario.Info != null)
                            scenario.Info.variablesPreset = names[v];
                        presetNameInput = names[v];
                        RefreshSetting();
                    }
                });
            }

            // 保存：用输入框里的名字写成一份新预设（同名则覆盖），并把它设为剧本默认预设
            string saveName = ScenarioVariablesPresetManager.SanitizeName(presetNameInput);
            AddButtonItem($"保存为新预设「{saveName}」", () =>
            {
                if (!ScenarioVariablesPresetManager.Save(saveName, scenario.Variables))
                    return;

                if (scenario.Info != null)
                    scenario.Info.variablesPreset = saveName;
                presetNameInput = saveName;
                RefreshSetting();
            });

            // 删除：只允许删自建目录里的预设；模组 / 内置预设是只读的，管理器会拒绝并说明原因
            AddButtonItem($"删除预设「{saveName}」", () =>
            {
                if (!ScenarioVariablesPresetManager.Delete(saveName))
                    return;

                if (scenario.Info != null
                    && string.Equals(scenario.Info.variablesPreset, saveName, System.StringComparison.OrdinalIgnoreCase))
                    scenario.Info.variablesPreset = null;

                presetNameInput = null;
                RefreshSetting();
            });
        }

        public virtual void RefreshSetting()
        {
            for (int i = 0; i < itemList.Count; i++)
                RemoveItem(itemList[i]);
            itemList.Clear();
            // 用 OnOpen 记录的目标剧本重建，而不是 Scenario.CurSelected：
            // 开局前的入口传进来的是"准备开局的剧本"，两者可能不是同一个对象。
            ShowVariables(TargetScenario != null ? TargetScenario : Scenario.CurSelected);
        }

        public void RemoveItem(GameObject item)
        {
            string name = item.name;
            switch (name)
            {
                case "titleObj":
                    pool_titleObj.Add(item);
                    break;
                case "bigTitleObj":
                    pool_bigTitleObj.Add(item);
                    break;
                case "integerObj":
                    pool_integerObj.Add(item);
                    break;
                case "floatObj":
                    pool_floatObj.Add(item);
                    break;
                case "integerSliderObj":
                    pool_integerSliderObj.Add(item);
                    break;
                case "floatSliderObj":
                    pool_floatSliderObj.Add(item);
                    break;
                case "dropdownObj":
                    pool_dropdownObj.Add(item);
                    break;
                case "toggleObj":
                    pool_toggleObj.Add(item);
                    break;
                case "toggleGroupObj":
                    pool_toggleGroupObj.Add(item);
                    break;
                case "buttonObj":
                    pool_buttonObj.Add(item);
                    break;
                case "inputObj":
                    pool_inputObj.Add(item);
                    break;
            }
            item.SetActive(false);
        }

        public void SetItemBehindThis(GameObject item, GameObject t)
        {
            item.transform.SetSiblingIndex(t.transform.GetSiblingIndex() + 1);
        }

        public GameObject AddBigTitle(string title)
        {
            GameObject obj;
            if (pool_bigTitleObj.Count > 0)
            {
                obj = pool_bigTitleObj[0];
                pool_bigTitleObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(bigTitleObj, bigTitleObj.transform.parent);
                obj.name = "bigTitleObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UITitleField>().Set(title);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddTitle(string title)
        {
            GameObject obj;
            if (pool_titleObj.Count > 0)
            {
                obj = pool_titleObj[0];
                pool_titleObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(titleObj, titleObj.transform.parent);
                obj.name = "titleObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UITitleField>().Set(title);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }


        /// <summary>
        /// 一行可点击的按钮。
        ///
        /// 【为什么复用 titleObj 而不是新建一套 prefab】参数界面是纯代码动态生成的，
        /// 每种控件都要求美术在 prefab 里摆一个模板对象；为了两个按钮去改 prefab 代价太大。
        /// 这里直接拿标题行当模板（排版 / 高度 / 字体全部继承），只额外挂一层半透明 Image 做底
        /// 和 Button 做热区 —— Image 是父对象，文字是子对象，渲染顺序上不会遮住文字。
        ///
        /// 【为什么单独一个池】按钮对象带 Button 组件，若回收到 pool_titleObj 再当普通标题行复用，
        /// 会残留 onClick 监听，点标题就触发保存。所以 buttonObj 自己一套池，永不回流。
        /// </summary>
        /// <param name="title">按钮文字</param>
        /// <param name="onClick">点击回调</param>
        /// <returns>生成的对象</returns>
        public GameObject AddButtonItem(string title, System.Action onClick)
        {
            GameObject obj;
            if (pool_buttonObj.Count > 0)
            {
                obj = pool_buttonObj[0];
                pool_buttonObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(titleObj, titleObj.transform.parent);
                obj.name = "buttonObj";
            }

            itemList.Add(obj);

            UITitleField field = obj.GetComponent<UITitleField>();
            if (field != null)
                field.Set(title);

            // 底色（半透明蓝）：已有 Image 就复用，避免多次 Instantiate 叠出多层
            UnityEngine.UI.Image image = obj.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = obj.AddComponent<UnityEngine.UI.Image>();
                image.color = new UnityEngine.Color(0.16f, 0.42f, 0.78f, 0.55f);
            }

            UnityEngine.UI.Button button = obj.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                button = obj.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = image;
            }
            // 复用对象时必须先清空旧监听，否则上一次刷新挂的回调还在
            button.onClick.RemoveAllListeners();
            if (onClick != null)
                button.onClick.AddListener(() => onClick.Invoke());

            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        /// <summary>
        /// 一行文本输入：标题单独一行，输入框另起一行（复用 titleObj 的 Text 作为 InputField 的文本组件）。
        ///
        /// 【为什么先 SetActive(false) 再配置】<c>InputField</c> 在 OnEnable 里会立刻
        /// <c>UpdateLabel()</c>，此时若 textComponent 还没赋值就会报空引用。
        /// 因此模板实例化后先失活，属性配齐再激活。
        /// </summary>
        /// <param name="title">行标题</param>
        /// <param name="value">初始文本</param>
        /// <param name="onChange">结束编辑（回车 / 失焦）时回调</param>
        /// <returns>输入行对象</returns>
        public GameObject AddInputItem(string title, string value, System.Action<string> onChange)
        {
            AddTitle(title);

            GameObject obj;
            if (pool_inputObj.Count > 0)
            {
                obj = pool_inputObj[0];
                pool_inputObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(titleObj, titleObj.transform.parent);
                obj.name = "inputObj";
            }

            itemList.Add(obj);
            obj.SetActive(false);

            // 输入框底色（比按钮更深，视觉上区分"可编辑"与"可点击"）
            UnityEngine.UI.Image image = obj.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = obj.AddComponent<UnityEngine.UI.Image>();
                image.color = new UnityEngine.Color(0f, 0f, 0f, 0.45f);
            }

            UnityEngine.UI.InputField input = obj.GetComponent<UnityEngine.UI.InputField>();
            if (input == null)
            {
                input = obj.AddComponent<UnityEngine.UI.InputField>();
                input.lineType = UnityEngine.UI.InputField.LineType.SingleLine;
                input.targetGraphic = image;
            }

            // titleObj 的 Text 直接拿来当输入文本组件。
            // 优先走 UITitleField.title（prefab 里显式绑定的那个），拿不到再退回子节点搜索。
            UITitleField titleField = obj.GetComponent<UITitleField>();
            UnityEngine.UI.Text textComp = (titleField != null && titleField.title != null)
                ? titleField.title
                : obj.GetComponentInChildren<UnityEngine.UI.Text>(true);
            input.textComponent = textComp;

            // 复用对象：上一轮的监听必须清掉，否则保存 / 删除会被触发多次
            input.onEndEdit.RemoveAllListeners();
            input.text = value ?? string.Empty;
            if (onChange != null)
                input.onEndEdit.AddListener((text) => onChange(text));

            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddNumberItem(string title, int number, int min, int max, System.Action<int> onChange)
        {
            GameObject obj;
            if (pool_integerObj.Count > 0)
            {
                obj = pool_integerObj[0];
                pool_integerObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(integerObj, integerObj.transform.parent);
                obj.name = "integerObj";
            }

            itemList.Add(obj);
            obj.GetComponent<UIIntegerField>().Set(title, number, min, max, onChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        /// <summary>
        /// 渲染一张"获取地点 → 数值"的配置表。
        ///
        /// 键来自 GainPlace 枚举（表归属集中维护在 GainValueConfig 里），
        /// 所以以后新增获取地点只需加枚举项 + 填默认值，不需要再手写界面行。
        /// </summary>
        void AddGainTable(string title, GainValueConfig config, GainPlace[] places)
        {
            if (config == null || places == null || places.Length == 0)
            {
                AddTitle(title + "（暂无可配置项）");
                return;
            }

            AddTitle(title);
            for (int i = 0; i < places.Length; i++)
            {
                GainPlace place = places[i];        // 每轮一个副本，避免闭包捕获循环变量

                if (GainValueConfig.KindOf(place) == GainValueKind.Range)
                {
                    // 区间类：最小值 / 最大值两行
                    config.GetRange(place, out int min, out int max);
                    AddNumberItem(GainValueConfig.NameOf(place) + " 最小", min, 0, GainValueConfig.MaxOf(place),
                        v =>
                        {
                            config.GetRange(place, out int curMin, out int curMax);
                            config.SetRange(place, v, curMax);
                        });
                    AddNumberItem(GainValueConfig.NameOf(place) + " 最大", max, 0, GainValueConfig.MaxOf(place),
                        v =>
                        {
                            config.GetRange(place, out int curMin, out int curMax);
                            config.SetRange(place, curMin, v);
                        });
                }
                else
                {
                    AddNumberItem(GainValueConfig.NameOf(place), config.Get(place), 0, GainValueConfig.MaxOf(place),
                        v => config.Set(place, v));
                }
            }
        }

        public GameObject AddNumberItem(string title, float number, float min, float max, System.Action<float> onChange)
        {
            GameObject obj;
            if (pool_floatObj.Count > 0)
            {
                obj = pool_floatObj[0];
                pool_floatObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(floatObj, floatObj.transform.parent);
                obj.name = "floatObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIFloatField>().Set(title, number, min, max, onChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddSliderItem(string title, int value, int min, int max, System.Action<int> onValueChange)
        {
            GameObject obj;
            if (pool_integerSliderObj.Count > 0)
            {
                obj = pool_integerSliderObj[0];
                pool_integerSliderObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(integerSliderObj, integerSliderObj.transform.parent);
                obj.name = "integerSliderObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIIntegerSliderField>().Set(title, value, min, max, onValueChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddSliderItem(string title, float value, float min, float max, System.Action<float> onValueChange)
        {
            GameObject obj;
            if (pool_floatSliderObj.Count > 0)
            {
                obj = pool_floatSliderObj[0];
                pool_floatSliderObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(floatSliderObj, floatSliderObj.transform.parent);
                obj.name = "floatSliderObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIFloatSliderField>().Set(title, value, min, max, onValueChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddToggleItem(string title, bool value, System.Action<bool> onValueChange)
        {
            GameObject obj;
            if (pool_toggleObj.Count > 0)
            {
                obj = pool_toggleObj[0];
                pool_toggleObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(toggleObj, toggleObj.transform.parent);
                obj.name = "toggleObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIToggleField>().Set(title, value, onValueChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddDropdownItem(string title, int value, List<string> values, System.Action<int> onValueChange)
        {
            GameObject obj;
            if (pool_dropdownObj.Count > 0)
            {
                obj = pool_dropdownObj[0];
                pool_dropdownObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(dropdownObj, dropdownObj.transform.parent);
                obj.name = "dropdownObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIDropdownField>().Set(title, value, values, onValueChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public GameObject AddToggleGroupItem(string title, int value, List<string> values, System.Action<int> onValueChange)
        {
            GameObject obj;
            if (pool_toggleGroupObj.Count > 0)
            {
                obj = pool_toggleGroupObj[0];
                pool_toggleGroupObj.RemoveAt(0);
            }
            else
            {
                obj = GameObject.Instantiate(toggleGroupObj, toggleGroupObj.transform.parent);
                obj.name = "toggleGroupObj";
            }
            itemList.Add(obj);
            obj.GetComponent<UIToggleGroupField>().Set(title, value, values, onValueChange);
            obj.SetActive(true);
            obj.transform.SetAsLastSibling();
            return obj;
        }

        public virtual void OnStartGame()
        {
            if (onSure != null)
            {
                onSure.Invoke();
                return;
            }

            // 剧本加载全程是同步的（LoadWorld 内部会一路同步执行到 Scenario.Start），
            // 若在这里直接调用 Scenario.StartScenario，本帧根本来不及把 loading 界面渲染出来，
            // 玩家看到的只会是画面卡死。
            // 因此这里改为协程的方式：先显示 loading 界面，等一帧让它真正渲染出来后再执行后续逻辑。
            // 注意：协程必须挂在常驻对象(App)上启动，因为后续会关闭本界面，
            // GameObject 一旦被 SetActive(false)，其自身的协程会被中断。
            App.Instance.StartCoroutine(StartGameRoutine());
        }

        /// <summary>
        /// 开始游戏协程：显示 loading 界面 -> 等一帧 -> 关闭剧本参数界面 -> 分帧加载剧本。
        /// 加载由 Scenario.StartScenarioRoutine 驱动：五个重阶段之间各让出一帧，
        /// 使 loading 界面上的阶段进度文本能真正渲染出来；
        /// loading 界面的移除由剧本加载成功后的 Scenario.Start() 负责（关闭 window_loading 并打开 window_game）。
        /// </summary>
        /// <returns>协程迭代器</returns>
        IEnumerator StartGameRoutine()
        {
            // 1.先显示 loading 界面
            Window.Instance.Open("window_loading");

            // 2.等待一帧,确保 loading 界面已经显示成功(渲染到屏幕上)之后,再执行后续逻辑
            yield return null;

            // 3.关闭剧本参数界面
            Window.Instance.Close("window_scenario_variables");

            // 4.分帧加载剧本:解析通用数据 → 解析剧本数据 → 构建开局势力 → 读取地图数据 → 初始化世界,
            // 加载成功后由 Scenario.Start() 移除 loading 界面并进入游戏
            yield return Scenario.StartScenarioRoutine(Scenario.CurSelected, TargetShortScenario);
        }

        public virtual void OnCancel()
        {
            if (onCancel != null)
            {
                onCancel.Invoke();
                return;
            }
            Window.Instance.Close("window_scenario_variables");
            Window.Instance.Open("window_scenario_force_select", TargetShortScenario);
        }
    }
}
