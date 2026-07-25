using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System;

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
        protected List<GameObject> itemList = new List<GameObject>();

        public Action onSure;
        public Action onCancel;

        Scenario TargetScenario;

        public override void OnOpen()
        {
            onSure = null; onCancel = null;
            for (int i = 0; i < itemList.Count; i++)
                RemoveItem(itemList[i]);
            itemList.Clear();
            TargetScenario = Scenario.CurSelected;
            if (TargetScenario.Variables == null)
                TargetScenario.LoadVariables();
            ShowVariables(TargetScenario);
        }

        public override void OnOpen(params object[] objects)
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

        float[] lvlFactor = new float[] { 1f, 1f, 1.2f, 2f };
        float[] player_lvlFactor = new float[] { 1.2f, 1f, 1f, 0.9f };

        public void ShowVariables(Scenario scenario)
        {
            ScenarioVariables variables = scenario.Variables;
            AddBigTitle(scenario.Name);
            AddTitle("难度选择");
            AddToggleGroupItem("难度", variables.difficulty, new List<string> { "简单", "普通", "困难", "超级" }, (v) =>
            {
                variables.difficulty = v;
                variables.foodFactor = lvlFactor[v];
                variables.goldFactor = lvlFactor[v];
                variables.playerFoodFactor = player_lvlFactor[v];
                variables.playerGoldFactor = player_lvlFactor[v];
                RefreshSetting();
            });
            AddTitle("剧本基础参数");
            AddNumberItem("电脑粮食倍率", variables.foodFactor, 0, 100, (v) => { variables.foodFactor = v; });
            AddNumberItem("电脑资金倍率", variables.goldFactor, 0, 100, (v) => { variables.goldFactor = v; });
            AddNumberItem("玩家粮食倍率", variables.playerFoodFactor, 0, 100, (v) => { variables.playerFoodFactor = v; });
            AddNumberItem("玩家资金倍率", variables.playerGoldFactor, 0, 100, (v) => { variables.playerGoldFactor = v; });
            AddNumberItem("行动力上限", variables.ActionPointLimit, 0, 10000, (v) => { variables.ActionPointLimit = v; });
            AddNumberItem("行动力获取倍率", variables.ActionPointFactor, 0, 100, (v) => { variables.ActionPointFactor = v; });
            AddToggleItem("年龄生效", variables.AgeEnabled, (v) => { variables.AgeEnabled = v; RefreshSetting(); });
            if (variables.AgeEnabled)
                AddToggleItem("能力随年龄变化", variables.EnableAgeAbilityFactor, (v) => { variables.EnableAgeAbilityFactor = v; });

            AddNumberItem("建筑间隔距离", variables.BuildingSpace, 0, 2, (v) => { variables.BuildingSpace = v; });

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
            AddNumberItem("最后一城时候的抓捕率(百分比)", variables.captureChangceWhenLastCityFall, 0, 100, (v) => { variables.captureChangceWhenLastCityFall = v; });
            AddNumberItem("队伍溃败时候的抓捕率(百分比)", variables.captureChangceWhenTroopFall, 0, 100, (v) => { variables.captureChangceWhenTroopFall = v; });

            AddTitle("战斗参数");
            AddNumberItem("新游戏禁战回合", variables.AIAttackProtectedCount, 0, 100, (v) => { variables.AIAttackProtectedCount = v; });
            AddNumberItem("基础伤害", variables.fight_base_damage, 0, 1000, (v) => { variables.fight_base_damage = v; });
            AddNumberItem("基准兵力(攻守兵力差)", variables.fight_base_troops_need, 0, 10000, (v) => { variables.fight_base_troops_need = v; });
            AddNumberItem("每多基准兵力,获得一次兵力系数增益", variables.fight_base_troop_count, 0, 1000, (v) => { variables.fight_base_troop_count = v; });
            AddNumberItem("兵力系数增益", (float)variables.fight_damage_magic_number, 0, 0.01f, (v) => { variables.fight_damage_magic_number = v; });

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
            AddNumberItem("基础火焰伤害", variables.baseFireDamage, 100, 3000, (v) => { variables.baseFireDamage = v; });


            AddTitle("缴获与保留参数");
            AddNumberItem("近战击溃部队缴获的金钱比例(百分比)", variables.defeatTroopCanGainGoldFactor, 0, 100, (v) => { variables.defeatTroopCanGainGoldFactor = v; });
            AddNumberItem("近战击溃部队缴获的粮食比例(百分比)", variables.defeatTroopCanGainFoodFactor, 0, 100, (v) => { variables.defeatTroopCanGainFoodFactor = v; });

            //AddTitle("城市沦陷保留参数");
            //AddNumberItem("城市沦陷可以保留金钱比例(等级1)", variables.cityFallCanKeepGoldFactor[0], 0, 100, (v) => { variables.cityFallCanKeepGoldFactor[0] = v; });
            //AddNumberItem("城市沦陷可以保留金钱比例(等级2)", variables.cityFallCanKeepGoldFactor[1], 0, 100, (v) => { variables.cityFallCanKeepGoldFactor[1] = v; });
            //AddNumberItem("城市沦陷可以保留金钱比例(等级3)", variables.cityFallCanKeepGoldFactor[2], 0, 100, (v) => { variables.cityFallCanKeepGoldFactor[2] = v; });
            //AddNumberItem("城市沦陷可以保留金钱比例(等级4)", variables.cityFallCanKeepGoldFactor[3], 0, 100, (v) => { variables.cityFallCanKeepGoldFactor[3] = v; });

            //AddNumberItem("城市沦陷可以保留粮食比例(等级1)", variables.cityFallCanKeepFoodFactor[0], 0, 100, (v) => { variables.cityFallCanKeepFoodFactor[0] = v; });
            //AddNumberItem("城市沦陷可以保留粮食比例(等级2)", variables.cityFallCanKeepFoodFactor[1], 0, 100, (v) => { variables.cityFallCanKeepFoodFactor[1] = v; });
            //AddNumberItem("城市沦陷可以保留粮食比例(等级3)", variables.cityFallCanKeepFoodFactor[2], 0, 100, (v) => { variables.cityFallCanKeepFoodFactor[2] = v; });
            //AddNumberItem("城市沦陷可以保留粮食比例(等级4)", variables.cityFallCanKeepFoodFactor[3], 0, 100, (v) => { variables.cityFallCanKeepFoodFactor[3] = v; });

            //AddNumberItem("城市沦陷可以保留士兵比例(等级1)", variables.cityFallCanKeepTroopsFactor[0], 0, 100, (v) => { variables.cityFallCanKeepTroopsFactor[0] = v; });
            //AddNumberItem("城市沦陷可以保留士兵比例(等级2)", variables.cityFallCanKeepTroopsFactor[1], 0, 100, (v) => { variables.cityFallCanKeepTroopsFactor[1] = v; });
            //AddNumberItem("城市沦陷可以保留士兵比例(等级3)", variables.cityFallCanKeepTroopsFactor[2], 0, 100, (v) => { variables.cityFallCanKeepTroopsFactor[2] = v; });
            //AddNumberItem("城市沦陷可以保留士兵比例(等级4)", variables.cityFallCanKeepTroopsFactor[3], 0, 100, (v) => { variables.cityFallCanKeepTroopsFactor[3] = v; });

            //AddNumberItem("城市沦陷可以保留库存比例(等级1)", variables.cityFallCanKeepItemFactor[0], 0, 100, (v) => { variables.cityFallCanKeepItemFactor[0] = v; });
            //AddNumberItem("城市沦陷可以保留库存比例(等级2)", variables.cityFallCanKeepItemFactor[1], 0, 100, (v) => { variables.cityFallCanKeepItemFactor[1] = v; });
            //AddNumberItem("城市沦陷可以保留库存比例(等级3)", variables.cityFallCanKeepItemFactor[2], 0, 100, (v) => { variables.cityFallCanKeepItemFactor[2] = v; });
            //AddNumberItem("城市沦陷可以保留库存比例(等级4)", variables.cityFallCanKeepItemFactor[3], 0, 100, (v) => { variables.cityFallCanKeepItemFactor[3] = v; });

            //AddNumberItem("城市沦陷可以保留农业比例(等级1)", variables.cityFallCanKeepAgriculture[0], 0, 100, (v) => { variables.cityFallCanKeepAgriculture[0] = v; });
            //AddNumberItem("城市沦陷可以保留农业比例(等级2)", variables.cityFallCanKeepAgriculture[1], 0, 100, (v) => { variables.cityFallCanKeepAgriculture[1] = v; });
            //AddNumberItem("城市沦陷可以保留农业比例(等级3)", variables.cityFallCanKeepAgriculture[2], 0, 100, (v) => { variables.cityFallCanKeepAgriculture[2] = v; });
            //AddNumberItem("城市沦陷可以保留农业比例(等级4)", variables.cityFallCanKeepAgriculture[3], 0, 100, (v) => { variables.cityFallCanKeepAgriculture[3] = v; });

            //AddNumberItem("城市沦陷可以保留开发比例(等级1)", variables.cityFallCanKeepCommerce[0], 0, 100, (v) => { variables.cityFallCanKeepCommerce[0] = v; });
            //AddNumberItem("城市沦陷可以保留开发比例(等级2)", variables.cityFallCanKeepCommerce[1], 0, 100, (v) => { variables.cityFallCanKeepCommerce[1] = v; });
            //AddNumberItem("城市沦陷可以保留开发比例(等级3)", variables.cityFallCanKeepCommerce[2], 0, 100, (v) => { variables.cityFallCanKeepCommerce[2] = v; });
            //AddNumberItem("城市沦陷可以保留开发比例(等级4)", variables.cityFallCanKeepCommerce[3], 0, 100, (v) => { variables.cityFallCanKeepCommerce[3] = v; });

            AddTitle("越狱参数");
            AddNumberItem("基础越狱概率(万分比)", variables.baseEscapeProbabllity, 0, 10000, (v) => { variables.baseEscapeProbabllity = v; });
            AddNumberItem("越狱概率每回合增长(万分比)", variables.baseEscapeProbablilityAddByTurn, 0, 1000, (v) => { variables.baseEscapeProbablilityAddByTurn = v; });

            AddTitle("AI参数");
            AddNumberItem("进攻时候留守最低的兵力", variables.minTroopsKeepWhenAttack, 0, 100000, (v) => { variables.minTroopsKeepWhenAttack = v; });
            AddNumberItem("进攻时候留守最低的粮食", variables.minFoodKeepWhenAttack, 0, 100000, (v) => { variables.minFoodKeepWhenAttack = v; });
            AddNumberItem("防御时候留守最低的兵力", variables.minTroopsKeepWhenDefence, 0, 100000, (v) => { variables.minTroopsKeepWhenDefence = v; });
            AddNumberItem("防御时候留守最低的粮食", variables.minFoodKeepWhenDefence, 0, 100000, (v) => { variables.minFoodKeepWhenDefence = v; });

            AddTitle("系统参数");
            AddNumberItem("寻路安全次数限制", variables.pathfindingSafeCount, 1000, 1000000, (v) => { variables.pathfindingSafeCount = v; });

            //AddTitle("外交系统参数");
            //AddNumberItem("外交关系阈值 - 结盟", variables.diplomacyAllianceRelationThreshold, -5000, 5000, (v) => { variables.diplomacyAllianceRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 停战", variables.diplomacyTruceRelationThreshold, -5000, 5000, (v) => { variables.diplomacyTruceRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 请求技术", variables.diplomacyRequestTechniqueRelationThreshold, -5000, 5000, (v) => { variables.diplomacyRequestTechniqueRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 请求兵力", variables.diplomacyRequestTroopsRelationThreshold, -5000, 5000, (v) => { variables.diplomacyRequestTroopsRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 通商", variables.diplomacyTradeRelationThreshold, -5000, 5000, (v) => { variables.diplomacyTradeRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 和亲", variables.diplomacyMarriageRelationThreshold, -5000, 5000, (v) => { variables.diplomacyMarriageRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 请求结盟", variables.diplomacyAllianceRequestRelationThreshold, -5000, 5000, (v) => { variables.diplomacyAllianceRequestRelationThreshold = v; });
            //AddNumberItem("外交关系阈值 - 请求停战", variables.diplomacyTruceRequestRelationThreshold, -5000, 5000, (v) => { variables.diplomacyTruceRequestRelationThreshold = v; });
            //AddNumberItem("使者能力加成上限", variables.diplomacyAbilityBonusMax, 0, 100, (v) => { variables.diplomacyAbilityBonusMax = v; });
            //AddNumberItem("资源价值加成上限", variables.diplomacyResourceBonusMax, 0, 100, (v) => { variables.diplomacyResourceBonusMax = v; });
            //AddNumberItem("资源价值加成系数", variables.diplomacyResourceBonusFactor, 1, 1000, (v) => { variables.diplomacyResourceBonusFactor = v; });
            //AddNumberItem("结盟关系增加", variables.diplomacyAllianceRelationIncrease, -1000, 1000, (v) => { variables.diplomacyAllianceRelationIncrease = v; });
            //AddNumberItem("停战关系增加", variables.diplomacyTruceRelationIncrease, -1000, 1000, (v) => { variables.diplomacyTruceRelationIncrease = v; });
            //AddNumberItem("宣战关系减少", variables.diplomacyDeclareWarRelationDecrease, -1000, 1000, (v) => { variables.diplomacyDeclareWarRelationDecrease = v; });
            //AddNumberItem("送礼金额", variables.diplomacySendGiftAmount, 100, 10000, (v) => { variables.diplomacySendGiftAmount = v; });
            //AddNumberItem("送礼关系增加比例", variables.diplomacySendGiftRelationFactor, 1, 100, (v) => { variables.diplomacySendGiftRelationFactor = v; });
            //AddNumberItem("请求技术关系减少", variables.diplomacyRequestTechniqueRelationDecrease, -1000, 1000, (v) => { variables.diplomacyRequestTechniqueRelationDecrease = v; });
            //AddNumberItem("请求兵力关系减少", variables.diplomacyRequestTroopsRelationDecrease, -1000, 1000, (v) => { variables.diplomacyRequestTroopsRelationDecrease = v; });
            //AddNumberItem("通商关系增加", variables.diplomacyTradeRelationIncrease, -1000, 1000, (v) => { variables.diplomacyTradeRelationIncrease = v; });
            //AddNumberItem("通商黄金收入增加", variables.diplomacyTradeGoldIncrease, 0, 1000, (v) => { variables.diplomacyTradeGoldIncrease = v; });
            //AddNumberItem("和亲关系增加", variables.diplomacyMarriageRelationIncrease, -1000, 1000, (v) => { variables.diplomacyMarriageRelationIncrease = v; });
            //AddNumberItem("和亲额外关系增加", variables.diplomacyMarriageExtraRelationIncrease, -1000, 1000, (v) => { variables.diplomacyMarriageExtraRelationIncrease = v; });
            //AddNumberItem("赎回俘虏关系增加比例", variables.diplomacyRansomRelationFactor, 1, 100, (v) => { variables.diplomacyRansomRelationFactor = v; });
            //AddNumberItem("请求结盟关系增加", variables.diplomacyAllianceRequestRelationIncrease, -1000, 1000, (v) => { variables.diplomacyAllianceRequestRelationIncrease = v; });
            //AddNumberItem("请求停战关系增加", variables.diplomacyTruceRequestRelationIncrease, -1000, 1000, (v) => { variables.diplomacyTruceRequestRelationIncrease = v; });
            //AddNumberItem("撕毁同盟关系减少", variables.diplomacyBreakAllianceRelationDecrease, -1000, 1000, (v) => { variables.diplomacyBreakAllianceRelationDecrease = v; });
            //AddNumberItem("撕毁停战关系减少", variables.diplomacyBreakTruceRelationDecrease, -1000, 1000, (v) => { variables.diplomacyBreakTruceRelationDecrease = v; });
            //AddNumberItem("撕毁通商关系减少", variables.diplomacyBreakTradeRelationDecrease, -1000, 1000, (v) => { variables.diplomacyBreakTradeRelationDecrease = v; });
            //AddNumberItem("同盟持续时间", variables.diplomacyAllianceDuration, 1, 100, (v) => { variables.diplomacyAllianceDuration = v; });
            //AddNumberItem("停战持续时间", variables.diplomacyTruceDuration, 1, 100, (v) => { variables.diplomacyTruceDuration = v; });
            //AddNumberItem("通商持续时间", variables.diplomacyTradeDuration, 1, 100, (v) => { variables.diplomacyTradeDuration = v; });
            //AddNumberItem("每月同盟关系增加", variables.diplomacyMonthlyAllianceRelationIncrease, -1000, 1000, (v) => { variables.diplomacyMonthlyAllianceRelationIncrease = v; });
            //AddNumberItem("每月普通关系减少", variables.diplomacyMonthlyNormalRelationDecrease, -1000, 1000, (v) => { variables.diplomacyMonthlyNormalRelationDecrease = v; });

            //AddTitle("外交成功率参数");
            //// 结盟成功率参数
            //AddNumberItem("结盟基础成功率", variables.diplomacyAllianceBaseSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceBaseSuccessRate = v; });
            //AddNumberItem("结盟关系系数", variables.diplomacyAllianceRelationFactor, 1, 100, (v) => { variables.diplomacyAllianceRelationFactor = v; });
            //AddNumberItem("结盟最大成功率", variables.diplomacyAllianceMaxSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceMaxSuccessRate = v; });
            //AddNumberItem("结盟最低成功率", variables.diplomacyAllianceMinSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceMinSuccessRate = v; });

            //// 停战成功率参数
            //AddNumberItem("停战基础成功率", variables.diplomacyTruceBaseSuccessRate, 0, 100, (v) => { variables.diplomacyTruceBaseSuccessRate = v; });
            //AddNumberItem("停战关系系数", variables.diplomacyTruceRelationFactor, 1, 100, (v) => { variables.diplomacyTruceRelationFactor = v; });
            //AddNumberItem("停战最大成功率", variables.diplomacyTruceMaxSuccessRate, 0, 100, (v) => { variables.diplomacyTruceMaxSuccessRate = v; });
            //AddNumberItem("停战最低成功率", variables.diplomacyTruceMinSuccessRate, 0, 100, (v) => { variables.diplomacyTruceMinSuccessRate = v; });

            //// 请求技术成功率参数
            //AddNumberItem("请求技术基础成功率", variables.diplomacyRequestTechniqueBaseSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTechniqueBaseSuccessRate = v; });
            //AddNumberItem("请求技术关系系数", variables.diplomacyRequestTechniqueRelationFactor, 1, 100, (v) => { variables.diplomacyRequestTechniqueRelationFactor = v; });
            //AddNumberItem("请求技术最大成功率", variables.diplomacyRequestTechniqueMaxSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTechniqueMaxSuccessRate = v; });
            //AddNumberItem("请求技术最低成功率", variables.diplomacyRequestTechniqueMinSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTechniqueMinSuccessRate = v; });

            //// 请求兵力成功率参数
            //AddNumberItem("请求兵力基础成功率", variables.diplomacyRequestTroopsBaseSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTroopsBaseSuccessRate = v; });
            //AddNumberItem("请求兵力关系系数", variables.diplomacyRequestTroopsRelationFactor, 1, 100, (v) => { variables.diplomacyRequestTroopsRelationFactor = v; });
            //AddNumberItem("请求兵力最大成功率", variables.diplomacyRequestTroopsMaxSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTroopsMaxSuccessRate = v; });
            //AddNumberItem("请求兵力最低成功率", variables.diplomacyRequestTroopsMinSuccessRate, 0, 100, (v) => { variables.diplomacyRequestTroopsMinSuccessRate = v; });

            //// 通商成功率参数
            //AddNumberItem("通商基础成功率", variables.diplomacyTradeBaseSuccessRate, 0, 100, (v) => { variables.diplomacyTradeBaseSuccessRate = v; });
            //AddNumberItem("通商关系系数", variables.diplomacyTradeRelationFactor, 1, 100, (v) => { variables.diplomacyTradeRelationFactor = v; });
            //AddNumberItem("通商最大成功率", variables.diplomacyTradeMaxSuccessRate, 0, 100, (v) => { variables.diplomacyTradeMaxSuccessRate = v; });
            //AddNumberItem("通商最低成功率", variables.diplomacyTradeMinSuccessRate, 0, 100, (v) => { variables.diplomacyTradeMinSuccessRate = v; });

            //// 和亲成功率参数
            //AddNumberItem("和亲基础成功率", variables.diplomacyMarriageBaseSuccessRate, 0, 100, (v) => { variables.diplomacyMarriageBaseSuccessRate = v; });
            //AddNumberItem("和亲关系系数", variables.diplomacyMarriageRelationFactor, 1, 100, (v) => { variables.diplomacyMarriageRelationFactor = v; });
            //AddNumberItem("和亲最大成功率", variables.diplomacyMarriageMaxSuccessRate, 0, 100, (v) => { variables.diplomacyMarriageMaxSuccessRate = v; });
            //AddNumberItem("和亲最低成功率", variables.diplomacyMarriageMinSuccessRate, 0, 100, (v) => { variables.diplomacyMarriageMinSuccessRate = v; });

            //// 请求结盟成功率参数
            //AddNumberItem("请求结盟基础成功率", variables.diplomacyAllianceRequestBaseSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceRequestBaseSuccessRate = v; });
            //AddNumberItem("请求结盟关系系数", variables.diplomacyAllianceRequestRelationFactor, 1, 100, (v) => { variables.diplomacyAllianceRequestRelationFactor = v; });
            //AddNumberItem("请求结盟最大成功率", variables.diplomacyAllianceRequestMaxSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceRequestMaxSuccessRate = v; });
            //AddNumberItem("请求结盟最低成功率", variables.diplomacyAllianceRequestMinSuccessRate, 0, 100, (v) => { variables.diplomacyAllianceRequestMinSuccessRate = v; });
            //AddNumberItem("请求结盟关系阈值", variables.diplomacyAllianceRequestSuccessRelationThreshold, -5000, 5000, (v) => { variables.diplomacyAllianceRequestSuccessRelationThreshold = v; });
            //AddNumberItem("请求结盟随机概率分母", variables.diplomacyAllianceRequestChanceDenominator, 1000, 5000, (v) => { variables.diplomacyAllianceRequestChanceDenominator = v; });

            //// 请求停战成功率参数
            //AddNumberItem("请求停战基础成功率", variables.diplomacyTruceRequestBaseSuccessRate, 0, 100, (v) => { variables.diplomacyTruceRequestBaseSuccessRate = v; });
            //AddNumberItem("请求停战关系系数", variables.diplomacyTruceRequestRelationFactor, 1, 100, (v) => { variables.diplomacyTruceRequestRelationFactor = v; });
            //AddNumberItem("请求停战最大成功率", variables.diplomacyTruceRequestMaxSuccessRate, 0, 100, (v) => { variables.diplomacyTruceRequestMaxSuccessRate = v; });
            //AddNumberItem("请求停战最低成功率", variables.diplomacyTruceRequestMinSuccessRate, 0, 100, (v) => { variables.diplomacyTruceRequestMinSuccessRate = v; });
            //AddNumberItem("请求停战关系阈值", variables.diplomacyTruceRequestSuccessRelationThreshold, -5000, 5000, (v) => { variables.diplomacyTruceRequestSuccessRelationThreshold = v; });
            //AddNumberItem("请求停战随机概率分母", variables.diplomacyTruceRequestChanceDenominator, 1000, 5000, (v) => { variables.diplomacyTruceRequestChanceDenominator = v; });
            //AddNumberItem("请求停战随机概率偏移", variables.diplomacyTruceRequestChanceOffset, 0, 2000, (v) => { variables.diplomacyTruceRequestChanceOffset = v; });

            //// 赎回俘虏成功率参数
            //AddNumberItem("赎回俘虏基础成功率", variables.diplomacyRansomBaseSuccessRate, 0, 100, (v) => { variables.diplomacyRansomBaseSuccessRate = v; });
            //AddNumberItem("赎回俘虏关系系数", variables.diplomacyRansomSuccessRelationFactor, 1, 100, (v) => { variables.diplomacyRansomSuccessRelationFactor = v; });
            //AddNumberItem("赎回俘虏最大成功率", variables.diplomacyRansomMaxSuccessRate, 0, 100, (v) => { variables.diplomacyRansomMaxSuccessRate = v; });
            //AddNumberItem("赎回俘虏最低成功率", variables.diplomacyRansomMinSuccessRate, 0, 100, (v) => { variables.diplomacyRansomMinSuccessRate = v; });

            AddTitle("招募系统参数");
            AddNumberItem("可招募的忠诚度阈值", variables.recruitableLine, 0, 100, (v) => { variables.recruitableLine = v; });
            AddNumberItem("玩家招募加成", variables.playerRecruitAdd, 0, 100, (v) => { variables.playerRecruitAdd = v; });
            //AddNumberItem("基础相性值", variables.recruitBaseCompatibility, 0, 100, (v) => { variables.recruitBaseCompatibility = v; });
            //AddNumberItem("在野武将忠诚度基础值", variables.recruitWildLoyaltyBase, 0, 100, (v) => { variables.recruitWildLoyaltyBase = v; });
            //AddNumberItem("忠诚度难度系数", variables.recruitLoyaltyDifficultyFactor, 1, 20, (v) => { variables.recruitLoyaltyDifficultyFactor = v; });
            //AddNumberItem("默认义理ID", variables.recruitDefaultArgumentationId, 1, 10, (v) => { variables.recruitDefaultArgumentationId = v; });
            //AddNumberItem("基础成功率", variables.recruitBaseSuccessRate, 0, 100, (v) => { variables.recruitBaseSuccessRate = v; });
            //AddNumberItem("相性影响系数分子", variables.recruitCompatibilityFactorNumerator, 1, 10, (v) => { variables.recruitCompatibilityFactorNumerator = v; });
            //AddNumberItem("相性影响系数分母", variables.recruitCompatibilityFactorDenominator, 1, 10, (v) => { variables.recruitCompatibilityFactorDenominator = v; });
            //AddNumberItem("忠诚度影响基础值", variables.recruitLoyaltyInfluenceBase, 0, 50, (v) => { variables.recruitLoyaltyInfluenceBase = v; });
            //AddNumberItem("忠诚度影响系数分子", variables.recruitLoyaltyInfluenceNumerator, 1, 100, (v) => { variables.recruitLoyaltyInfluenceNumerator = v; });
            //AddNumberItem("忠诚度影响系数分母", variables.recruitLoyaltyInfluenceDenominator, 1, 100, (v) => { variables.recruitLoyaltyInfluenceDenominator = v; });
            //AddNumberItem("魅力最低值", variables.recruitMinGlamour, 0, 100, (v) => { variables.recruitMinGlamour = v; });
            //AddNumberItem("魅力影响系数分子", variables.recruitGlamourFactorNumerator, 1, 10, (v) => { variables.recruitGlamourFactorNumerator = v; });
            //AddNumberItem("魅力影响系数分母", variables.recruitGlamourFactorDenominator, 1, 10, (v) => { variables.recruitGlamourFactorDenominator = v; });
            //AddNumberItem("亲爱武将影响值", variables.recruitLikePersonInfluence, 0, 50, (v) => { variables.recruitLikePersonInfluence = v; });
            //AddNumberItem("亲子关系影响值", variables.recruitParentChildInfluence, 0, 50, (v) => { variables.recruitParentChildInfluence = v; });
            //AddNumberItem("厌恶武将影响值", variables.recruitHatePersonInfluence, 0, 50, (v) => { variables.recruitHatePersonInfluence = v; });
            //AddNumberItem("俘虏影响值", variables.recruitPrisonerInfluence, 0, 50, (v) => { variables.recruitPrisonerInfluence = v; });
            //AddNumberItem("随机影响最大值", variables.recruitRandomMax, 0, 20, (v) => { variables.recruitRandomMax = v; });
            //AddNumberItem("君主魅力影响系数", variables.recruitGovernorGlamourFactor, 1, 10, (v) => { variables.recruitGovernorGlamourFactor = v; });
            //AddNumberItem("第一次发现加成", variables.recruitFirstDiscoveryBonus, 0, 50, (v) => { variables.recruitFirstDiscoveryBonus = v; });
            //AddNumberItem("基础义理值", variables.recruitBaseGiri, 1, 20, (v) => { variables.recruitBaseGiri = v; });
            //AddNumberItem("义理最大值", variables.recruitMaxGiri, 1, 20, (v) => { variables.recruitMaxGiri = v; });
            //AddNumberItem("义理忠诚度影响系数", variables.recruitGiriLoyaltyFactor, 1, 5, (v) => { variables.recruitGiriLoyaltyFactor = v; });

            AddTitle("逃跑系统参数");
            AddNumberItem("城市中逃跑概率减少值", variables.escapeCityReduction, 0, 1000, (v) => { variables.escapeCityReduction = v; });
            AddNumberItem("部队中逃跑概率增加值", variables.escapeTroopIncrease, 0, 1000, (v) => { variables.escapeTroopIncrease = v; });
            AddNumberItem("最大逃跑概率", variables.escapeMaxProbability, 1000, 5000, (v) => { variables.escapeMaxProbability = v; });

            AddTitle("敌方部队发现参数");
            AddNumberItem("发现敌方新建部队的基础概率(万分比)", variables.discoverEnemyTroopBaseProbability, 0, 10000, (v) => { variables.discoverEnemyTroopBaseProbability = v; });
            AddNumberItem("军师智力对发现概率的影响系数(万分比)", variables.discoverEnemyTroopIntelligenceFactor, 0, 1000, (v) => { variables.discoverEnemyTroopIntelligenceFactor = v; });

            AddBigTitle("Mod相关");
            GameEvent.OnScenarioVariablesSetting?.Invoke(this, scenario);
        }


        public virtual void RefreshSetting()
        {
            for (int i = 0; i < itemList.Count; i++)
                RemoveItem(itemList[i]);
            itemList.Clear();
            ShowVariables(Scenario.CurSelected);
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
            Window.Instance.Open("window_loading");
            Window.Instance.Close("window_scenario_variables");
            Scenario.StartScenario(Scenario.CurSelected);
        }

        public virtual void OnCancel()
        {
            if (onCancel != null)
            {
                onCancel.Invoke();
                return;
            }
            Window.Instance.Close("window_scenario_variables");
            Window.Instance.Open("window_scenario_force_select");
        }
    }
}
