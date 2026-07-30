/*
 * 文件名：GameEvent.cs
 * 描述：游戏事件类，定义游戏中所有的事件委托
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core.Tools;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 游戏事件类，定义游戏中所有的事件委托
    /// 包含全局事件、剧本事件、窗口事件、游戏事件、武将事件、城市事件、部队事件等
    /// </summary>
    public class GameEvent : EventBase
    {

        #region Global
        /// <summary>
        /// 游戏状态监听 -Enter
        /// </summary>
        public static EventDelegate<int, int> OnGameStateEnter;

        /// <summary>
        /// 游戏状态监听 -Exit
        /// </summary>
        public static EventDelegate<int, int> OnGameStateExit;

        /// <summary>
        /// 可扩展适应名字
        /// </summary>
        public static EventDelegateReturn<string, int> OnGetAbilityName;

        /// <summary>
        /// 可扩展获取武将属性名字
        /// </summary>
        public static EventDelegateReturn<string, int> OnGetAttributeName;
        public static EventDelegateReturn<string, int> OnGetAttributeNameWithColor;


        public static EventDelegate<Cell, Vector3, bool> OnClick;
        public static EventDelegate<Cell, Vector3, bool> OnRClick;
        public static EventDelegate<Cell, Vector3, bool> OnRClickObject;
        public static EventDelegate<Cell, Vector3, bool> OnCancel;
        /// <summary>
        /// 在最后一个系统出栈时候
        /// </summary>
        public static EventDelegate OnSystemEnd;

        /// <summary>
        /// 在第一个系统入栈时
        /// </summary>
        public static EventDelegate OnSystemStart;

        /// <summary>
        /// 在Mod更新的时候
        /// </summary>
        public static EventDelegate<Mod.Mod> OnModUpdate;

        #endregion Global

        #region Scenario
        /// <summary>
        /// 剧本加载开始
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioLoadStart;

        /// <summary>
        /// 剧本加载结束
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioLoadEnd;

        /// <summary>
        /// 地图场景加载开始
        /// </summary>
        public static EventDelegate<Scenario> OnWorldLoadStart;

        /// <summary>
        /// 地图场景加载结束
        /// </summary>
        public static EventDelegate<Scenario> OnWorldLoadEnd;

        /// <summary>
        /// 剧本开始
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioStart;

        /// <summary>
        /// 剧本准备
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioPrepare;

        /// <summary>
        /// 剧本准备
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioInit;

        /// <summary>
        /// 剧本结束
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioEnd;

        /// <summary>
        /// 剧本tick
        /// </summary>
        public static EventDelegate<Scenario, float> OnScenarioTick;
        #endregion Scenario

        #region Window
        /// <summary>
        /// 在window创建之后
        /// </summary>
        public static EventDelegate<string, Window.WindowInterface> OnWindowCreate;

        /// <summary>
        ///  临时-城市扩展信息
        /// </summary>
        public static EventDelegate OnCityHeadbarShowInfoChange;

        /// <summary>
        /// ContextMenu激活的时候,可以监听来添加自定义按钮
        /// </summary>
        public static EventDelegate<IContextMenuData> OnContextMenuShow;

        public static EventDelegate<IContextMenuData, BuildingBase> OnBuildingContextMenuShow;
        public static EventDelegate<IContextMenuData, Cell> OnCellContextMenuShow;
        public static EventDelegate<IContextMenuData, City> OnCityContextMenuShow;
        public static EventDelegate<IContextMenuData, Troop> OnTroopContextMenuShow;
        public static EventDelegate<IContextMenuData, Troop, Cell> OnTroopActionContextMenuShow;
        public static EventDelegate<IContextMenuData, Troop, Cell> OnInteractiveContextMenuShow;
        public static EventDelegate<IContextMenuData> OnRightMouseButtonContextMenuShow;
        public static EventDelegate<IContextMenuData, City> OnCityRightMouseButtonContextMenuShow;
        public static EventDelegate<IContextMenuData, Building> OnBuildingRightMouseButtonContextMenuShow;
        public static EventDelegate<IContextMenuData, Troop> OnTroopRightMouseButtonContextMenuShow;
        public static EventDelegate<IContextMenuData, Port> OnPortRightMouseButtonContextMenuShow;
        public static EventDelegate<IContextMenuData, Gate> OnGateRightMouseButtonContextMenuShow;

        public static EventDelegate<IContextMenuData> OnGameSettingContextMenuShow;

        public static EventDelegate<ITroopInteractiveDialogData, Troop, Cell> OnTroopInteractiveContextDialogShow;
        public static EventDelegate<IVariablesSetting, Scenario> OnScenarioVariablesSetting;


        /// <summary>
        /// 城池点开的详细Tips信息板,监听可以增减信息展示条目
        /// customData 为 ObjectSortTitle 则是横向两条信息(参考枪+冲车)
        /// customData 为 0 则接着创建一个line
        /// customData 为 1 则是横向两条信息但是只设置第一条,第二条隐藏
        /// </summary>
        public static EventDelegate<City, List<ObjectSortTitle>> OnInitCityInfoPanel;

        /// <summary>
        /// 城池tips信息板,监听可以增减信息展示条目
        /// </summary>
        public static EventDelegate<City, List<ObjectSortTitle>> OnInitCityMiniPanel;

        /// <summary>
        /// 部队tips信息板,监听可以增减信息展示条目
        /// </summary>
        public static EventDelegate<Troop, List<ObjectSortTitle>> OnInitTroopMiniPanel;

        /// <summary>
        /// 港口tips信息板,监听可以增减信息展示条目
        /// </summary>
        public static EventDelegate<Port, List<ObjectSortTitle>> OnInitPortMiniPanel;

        /// <summary>
        /// 关卡信息板,监听可以增减信息展示条目
        /// </summary>
        public static EventDelegate<Gate, List<ObjectSortTitle>> OnInitGateMiniPanel;

        /// <summary>
        /// 建筑tips信息板,监听可以增减信息展示条目
        /// </summary>
        public static EventDelegate<Building, List<ObjectSortTitle>> OnInitBuildingMiniPanel;

        #endregion Window

        #region Game
        /// <summary>
        /// 游戏设置界面展示
        /// </summary>
        public static EventDelegate<IVariablesSetting> OnGameSetting;
        public static EventDelegate OnGameSettingApply;
        public static EventDelegate OnGameSettingCancel;

        /// <summary>
        /// 后处理开关变化
        /// 参数：bool enabled - true=启用后处理，false=禁用
        /// 由监听者（如 PostProcessVolume 控制器）负责实际启用/禁用场景中的后处理组件
        /// </summary>
        public static EventDelegate<bool> OnPostProcessingChanged;

        /// <summary>
        /// 游戏保存
        /// </summary>
        public static EventDelegate<Scenario, int, bool> OnGameSave;

        /// <summary>
        /// 游戏加载
        /// </summary>
        public static EventDelegate<Scenario> OnGameLoad;

        /// <summary>
        /// 新天开始
        /// </summary>
        public static EventDelegate<Scenario> OnDayUpdate;

        /// <summary>
        /// 新月开始
        /// </summary>
        public static EventDelegate<Scenario> OnMonthUpdate;

        /// <summary>
        /// 新月开始
        /// </summary>
        public static EventDelegate<Scenario> OnMonthStart;

        /// <summary>
        /// 新月结束
        /// </summary>
        public static EventDelegate<Scenario> OnMonthEnd;

        /// <summary>
        /// 新年开始
        /// </summary>
        public static EventDelegate<Scenario> OnYearUpdate;

        /// <summary>
        /// 新季节开始
        /// </summary>
        public static EventDelegate<Scenario> OnSeasonUpdate;

        /// <summary>
        /// 回合开始
        /// </summary>
        public static EventDelegate<Scenario> OnTurnStart;

        /// <summary>
        /// 回合结束
        /// </summary>
        public static EventDelegate<Scenario> OnTurnEnd;




        #region Force
        /// <summary>
        /// 势力逻辑开始
        /// </summary>
        public static EventDelegate<Force, Scenario> OnForceTurnStart;

        /// <summary>
        /// 势力逻辑结束
        /// </summary>
        public static EventDelegate<Force, Scenario> OnForceTurnEnd;

        /// <summary>
        /// 玩家控制势力
        /// </summary>
        public static EventDelegate<Corps, Scenario> OnPlayerControl;

        /// <summary>
        /// 势力AI
        /// </summary>
        public static EventDelegate<Force, Scenario> OnForceAIPrepare;
        public static EventDelegate<Force, Scenario> OnForceAIStart;
        public static EventDelegate<Force, Scenario> OnForceAIEnd;

        /// <summary>
        /// 当势力灭亡的时候
        /// </summary>
        public static EventDelegate<Force, City, Troop> OnForceFall;
        public static EventDelegate<Force, int> OnForceGainTechniquePoint;
        public static EventDelegate<Force, int> OnForceGainHegemonyPoint;
        public static EventDelegate<Force, Technique> OnForceResearchComplete;

        /// <summary>
        /// 势力更换军师
        /// </summary>
        public static EventDelegate<Force, Person> OnForceChangeCounsellor;

        /// <summary>
        /// 发现敌方部队
        /// </summary>
        public static EventDelegate<Force, City, Troop, Person> OnDiscoverEnemyTroop;

        /// <summary>
        /// 建筑建造完成
        /// </summary>
        public static EventDelegate<Building, SangoObjectList<Person>> OnBuildingComplete;

        /// <summary>
        /// 建筑升级完成
        /// </summary>
        public static EventDelegate<Building, SangoObjectList<Person>> OnBuildingUpgradeComplete;

        #endregion Force

        #region Corps
        public static EventDelegate<Corps, Scenario> OnCorpsCreate;
        public static EventDelegate<Corps, Scenario> OnCorpsDelete;

        #endregion Corps

        /// <summary>
        /// 城池AI
        /// </summary>
        public static EventDelegate<City, Scenario> OnCityAIPrepare;
        public static EventDelegate<City, Scenario> OnCityAIStart;
        public static EventDelegate<City, Scenario> OnCityAIEnd;


        public static EventDelegate<City, Scenario> OnCityTurnStart;
        public static EventDelegate<City, Scenario> OnCityTurnEnd;
        public static EventDelegate<City, Scenario> OnCityMonthStart;
        public static EventDelegate<City, Scenario> OnCitySeasonStart;


        /// <summary>
        /// 部队AI
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopAIStart;
        public static EventDelegate<Troop, Scenario> OnTroopAIPrepare;
        public static EventDelegate<Troop, Scenario> OnTroopAIEnd;


        public static EventDelegate<Troop, Cell, Cell> OnTroopLeaveCell;
        public static EventDelegate<Troop, Cell, Cell> OnTroopEnterCell;

        public static EventDelegate<Troop, Scenario> OnTroopTurnStart;
        public static EventDelegate<Troop, Scenario> OnTroopTurnEnd;

        public static EventDelegate<Building, Scenario> OnBuildingTurnStart;
        public static EventDelegate<Building, Scenario> OnBuildingTurnEnd;

        /// <summary>
        /// 部队组建的时候
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopCreated;

        /// <summary>
        /// 部队溃灭的时候
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopDestroyed;

        /// <summary>
        /// 部队计算属性的时候
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopCalculateAttribute;

        /// <summary>
        /// 部队计算反击的时候
        /// </summary>
        public static EventDelegate<Troop, Troop, SkillInstance, Scenario, OverrideData<int>> OnTroopCalculateAttackBack;

        /// <summary>
        /// 当城池沦陷的时候
        /// </summary>
        public static EventDelegate<City, Force, Troop> OnCityFall;

        /// <summary>
        /// 可监听改写工作花费
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person[], OverrideData<int>> OnCityCheckJobCost;

        /// <summary>
        /// 可监听改写工作成果
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person[], OverrideData<int>> OnCityJobResult;

        /// <summary>
        /// 可监听改写工作回合
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person[], OverrideData<int>> OnCityJobCounterResult;

        /// <summary>
        /// 可监听改写工作获取的技巧
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person[], OverrideData<int>> OnCityJobGainTechniquePoint;

        /// <summary>
        /// 可监听改写工作获取的经验
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person[], OverrideData<int>> OnCityJobGainMerit;


        /// <summary>
        /// 可监听改写发现人才的几率
        /// City, JobType, PersonList, OverrideData
        /// </summary>
        public static EventDelegate<City, int, Person, OverrideData<int>> OnCityJobSearchingWild;

        /// <summary>
        /// 可监听改计算城池最大士气
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxMorale;

        /// <summary>
        /// 可监听改计算城池最大资金
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxGold;

        /// <summary>
        /// 可监听改计算城池最大兵粮
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxFood;

        /// <summary>
        /// 可监听改计算城池最大仓库数量
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxItemStoreSize;

        /// <summary>
        /// 可监听改计算城池最大士兵数
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxTroops;

        /// <summary>
        /// 可监听改计算城池最大耐久
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateMaxDurability;

        /// <summary>
        /// 计算城池收入
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City> OnCityCalculateHarvest;


        /// <summary>
        /// 可监听改计算部队最大兵力
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, Troop, OverrideData<int>> OnTroopCalculateMaxTroops;

        /// <summary>
        /// 可监听改计算城池每季度治安下降值
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCitySecurityChangeOnSeasonStart;

        /// <summary>
        /// 可监听改计算城池每月治安下降值
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCitySecurityChangeOnMonthStart;

        /// <summary>
        /// 可监听改写研究特定技巧的花费和时间
        /// 城市, 执行者, 科技, 技巧点花费, 资金花费, 时间
        /// </summary>
        public static EventDelegate<City, Person[], Technique, OverrideData<int>, OverrideData<int>, OverrideData<int>> OnCityResearchCost;

        /// <summary>
        /// 可监听改计算建筑兵粮产出
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<BuildingBase, OverrideData<int>> OnBuildingCalculateFoodGain;

        /// <summary>
        /// 可监听改计算建筑资金产出
        /// BuildingBase, OverrideData
        /// </summary>
        public static EventDelegate<BuildingBase, OverrideData<int>> OnBuildingCalculateGoldGain;

        /// <summary>
        /// 可监听改计算建筑人口增长率
        /// BuildingBase, OverrideData
        /// </summary>
        public static EventDelegate<BuildingBase, OverrideData<int>> OnBuildingCalculatePopulationGain;

        /// <summary>
        /// 可监听改计算建筑人口增长率
        /// BuildingBase, OverrideData
        /// </summary>
        public static EventDelegate<BuildingBase, OverrideData<int>> OnBuildingCalculateProduct;


        /// <summary>
        /// 可监听改计算建筑反击攻击力
        /// Troop, Cell, BuildingBase, Skill, OverrideData
        /// </summary>
        public static EventDelegate<Troop, Cell, BuildingBase, SkillInstance, OverrideData<int>> OnBuildCalculateAttackBack;

        /// <summary>
        /// 武将升级事件
        /// </summary>
        public static EventDelegate<Person> OnPersonLevelUp;

        /// <summary>
        /// 武将装备变更事件
        /// </summary>
        public static EventDelegate<Person> OnPersonEquipmentChanged;

        /// <summary>
        /// 势力忠诚换季衰减概率
        /// </summary>
        public static EventDelegate<Force, OverrideData<int>> OnForcePersonLoyaltyChangeProbability;

        /// <summary>
        /// 可监听改计算战法成功率(百分比) 必爆, 设置100则为必中, 设置为0则必不中
        /// City, Skill, spellCell, OverrideFunc
        /// </summary>
        public static EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnTroopBeforeCalculateSkillSuccess;
        /// <summary>
        /// 
        /// 可监听改计算战法成功率(百分比)
        /// City, Skill, spellCell, OverrideFunc
        /// </summary>
        public static EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnTroopAfterCalculateSkillSuccess;


        /// <summary>
        /// 可监听改计算战法暴击率(百分比) 必爆, 设置100则为必爆, 设置为0则必不爆
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public static EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnTroopBeforeCalculateSkillCritical;


        /// <summary>
        /// 可监听改计算战法暴击率(百分比)
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public static EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnTroopAfterCalculateSkillCritical;

        /// <summary>
        /// 可监听改计算战法暴击时的伤害倍率(百分比)
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public static EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnTroopCalculateSkillCriticalFactor;

        /// <summary>
        /// 当部队兵力变化时
        /// </summary>
        public static EventDelegate<Troop, SangoObject, SkillInstance, int, OverrideData<int>> OnTroopChangeTroops;

        /// <summary>
        /// 当部队气力变化时
        /// </summary>
        public static EventDelegate<Troop, int, OverrideData<int>> OnTroopChangeMorale;

        /// <summary>
        /// 当部队兵力变化时
        /// </summary>
        public static EventDelegate<Corps> OnCorpsActionPointChange;

        /// <summary>
        /// 当部队结束行动时
        /// </summary>
        public static EventDelegate<Troop> OnTroopActionOver;

        /// <summary>
        /// 技能实例计算属性时
        /// </summary>
        public static EventDelegate<Troop, SkillInstance> OnSkillCalculateAttribute;

        /// <summary>
        /// 技能实例命中敌人时候
        /// </summary>
        public static EventDelegate<SkillInstance, Troop, OverrideData<int>> OnSkillDamageTroop;

        /// <summary>
        /// 技能实例命中敌人之后
        /// </summary>
        public static EventDelegate<SkillInstance, Troop, OverrideData<int>> OnSkillDamageTroopAfter;

        /// <summary>
        /// 技能实例命中建筑士兵
        /// </summary>
        public static EventDelegate<SkillInstance, BuildingBase, OverrideData<int>> OnSkillDamageBuildingTroops;

        /// <summary>
        /// 技能实例命中建筑耐久
        /// </summary>
        public static EventDelegate<SkillInstance, BuildingBase, OverrideData<int>> OnSkillDamageBuildingDurability;

        /// <summary>
        /// 技能实例效果触发结束
        /// </summary>
        public static EventDelegate<SkillInstance> OnSkillActionOver;

        /// <summary>
        /// 技能实例效果触发结束
        /// </summary>
        public static EventDelegate<SkillInstance, Cell, Troop, BuildingBase> OnSkillActionEnd;

        /// <summary>
        /// 放火
        /// </summary>
        public static EventDelegate<SkillInstance, Fire> OnFireAdd;

        /// <summary>
        /// 当武将逃跑时
        /// </summary>
        public static EventDelegate<Person, SangoObject> OnPersonEscape;

        /// <summary>
        /// 当俘虏被释放时
        /// </summary>
        public static EventDelegate<Person, Force> OnPersonRelease;

        /// <summary>
        /// 当俘虏被斩杀时
        /// </summary>
        public static EventDelegate<Person, Force> OnPersonExecute;

        /// <summary>
        /// 当武将被俘虏时
        /// </summary>
        public static EventDelegate<Person, Troop> OnPersonCaptured;

        /// <summary>
        /// 武将转移成功
        /// </summary>
        public static EventDelegate<Person, City, City> OnPersonChangeBelongCity;

        /// <summary>
        /// 武将改变所在城市
        /// </summary>
        public static EventDelegate<Person, City, City> OnPersonChangCurrentCity;

        /// <summary>
        /// 武将官职晋升
        /// </summary>
        public static EventDelegate<Person, Official> OnPersonUpgradeOfficial;

        /// <summary>
        /// 当单挑开始时
        /// </summary>
        public static EventDelegate<DuelSystem> OnDuelStart;

        /// <summary>
        /// 当单挑结束时
        /// </summary>
        public static EventDelegate<DuelSystem, DuelResult> OnDuelEnd;

        /// <summary>
        /// 当需要选择决策时
        /// </summary>
        public static EventDelegate<DuelSystem> OnDuelDecisionRequired;

        /// <summary>
        /// 当单挑决策已做出时
        /// </summary>
        public static EventDelegate<DuelSystem> OnDuelDecisionMade;

        /// <summary>
        /// 当单挑行动发生时
        /// </summary>
        public static EventDelegate<DuelSystem, Person, Person, AttackResult> OnDuelAction;

        /// <summary>
        /// 当获取工作AP消耗的时候
        /// </summary>
        public static EventDelegate<JobType, int, OverrideData<int>> OnGetJobCostAP;

        /// <summary>
        /// 当获取工作花费的时候
        /// </summary>
        public static EventDelegate<JobType, int, OverrideData<int>> OnGetJobCost;

        /// <summary>
        /// 当获取工作功绩的时候
        /// </summary>
        public static EventDelegate<JobType, int, OverrideData<int>> OnGetJobMeritGain;

        /// <summary>
        /// 当获取工作技巧的时候
        /// </summary>
        public static EventDelegate<JobType, int, OverrideData<int>> OnGetJobTPGain;


        #region Diplomacy
        /// <summary>
        /// 执行结盟
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyAlliance;

        /// <summary>
        /// 执行停战
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyTruce;

        /// <summary>
        /// 执行宣战
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyDeclareWar;

        /// <summary>
        /// 执行送礼
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacySendGift;

        /// <summary>
        /// 执行请求技术
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacyRequestTechnique;

        /// <summary>
        /// 执行请求兵力
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacyRequestTroops;

        /// <summary>
        /// 执行通商
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyTrade;

        /// <summary>
        /// 执行和亲
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyMarriage;

        /// <summary>
        /// 执行请求结盟
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyAllianceRequest;

        /// <summary>
        /// 执行请求停战
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyTruceRequest;

        /// <summary>
        /// 撕毁条约
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyBreakAlliance;

        /// <summary>
        /// 执行赎回俘虏
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacyRansom;
        #endregion Diplomacy

        #region bridge
        public static EventDelegate DialogClose;
        public static EventDelegate<Person, string, System.Action, System.Action> DialogShow;
        #endregion

        #endregion Game

    }
}
