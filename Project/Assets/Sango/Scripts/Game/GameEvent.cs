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
        public static EventDelegate<Cell, Vector3, bool, Cell> OnRClick;
        //public static EventDelegate<Cell, Vector3, bool> OnRClickObject;
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

        /// <summary>
        /// 模组下载失败时抛出
        /// 参数1: 下载失败的模组;参数2: 失败原因(已翻译成中文,可直接展示给玩家)
        /// 失败不能只写日志:正式包会把 Info/Warning 编译剔除,界面若不提示,玩家看到的就是"点了下载没反应"
        /// </summary>
        public static EventDelegate<Mod.Mod, string> OnModDownloadFailed;

        /// <summary>
        /// 版本检查成功获取到远端版本信息时抛出
        /// 参数：VersionInfo 远端版本信息，监听者可依据 VersionInfo.hasNewVersion 决定是否弹出更新提示
        /// 获取失败时不会抛出该事件
        /// </summary>
        public static EventDelegate<VersionInfo> OnVersionInfoUpdate;

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
        public static EventDelegate<IContextMenuData> OnGameInformationContextMenuShow;

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

        public static EventDelegate<Force, Scenario> OnPlayerEndTurn;

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

        /// <summary>
        /// 当某个势力统一天下的时候（占领了全地图所有城池）。
        /// 由 <see cref="Sango.Core.City"/> 在"攻陷敌势力最后一座城池、且全图城池已尽归进攻方"时抛出。
        /// 剧本事件系统据此播放终局剧本事件（见 EventTriggerKind.WorldUnified），
        /// 与势力灭亡（OnForceFall）不同：这里是**胜利结局**，全图再无第二个势力。
        /// </summary>
        public static EventDelegate<Force> OnWorldUnified;

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
        /// 城市状态标记位发生变化。
        /// 参数：城市、变化前的位域、变化后的位域（取值见 <see cref="Sango.Core.CityStateFlag"/>）。
        ///
        /// 【派发点】唯一入口是 <see cref="Sango.Core.City.SetStateFlag"/> 与
        /// <see cref="Sango.Core.City.RefreshStateFlags"/>，且只在值**真正变化**时派发
        /// （重复置位 / 重复清位不会刷屏）。
        ///
        /// 【用途】订阅方据此刷新界面或联动其它系统；不要在这里反向修改标记位，
        /// 否则会与"Action 持有位、城市重算位"的单一来源打架。
        /// </summary>
        public static EventDelegate<City, int, int> OnCityStateFlagsChanged;


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
        /// 部队溃灭的时候。
        ///
        /// 第三个参数是 <see cref="AttackBackInfo"/>（与 <see cref="OnTroopChangeTroops"/> 同源）：
        /// 用 <c>IsAttackBack</c> 判是否反击，不要再从 int 里猜。
        /// </summary>
        public static EventDelegate<Troop, SangoObject, AttackBackInfo, Scenario> OnTroopDestroyed;

        public static EventDelegate<Troop, Scenario> OnTroopClear;

        /// <summary>
        /// 部队更换所属势力的时候(登庸主将 / 招降等)。
        ///
        /// 派发该事件的原因:部队的"归属"是 <c>Leader.BelongForce</c> 的实时派生,数据层改完就生效,
        /// 但表现层(旗帜材质色、头顶血条底色、小地图圆点色)都是**只在模型加载时写入一次**的,
        /// 不派事件就会出现"登庸成功、部队却还顶着敌国颜色"的当回合不一致。
        /// 表现层监听方统一在这里按新势力色重刷(参考城市侧的 CityFall -> MapCityNodeData.UpdateImage)。
        /// 参数:部队、换旗前的势力(可为 null)、换旗后的势力(可为 null)
        /// </summary>
        public static EventDelegate<Troop, Force, Force> OnTroopChangeForce;


        /// <summary>
        /// 部队属性重算的**最早期**：在兵种适性、战法列表、攻防移动力计算之前。
        ///
        /// 【为什么不复用 <see cref="OnTroopBeforeCalculateAttribute"/>】那个事件虽然名字叫
        /// "计算前"，实际派发位置在 <see cref="Troop.CalculateAttribute"/> 的末尾
        /// （兵种适性 / 战法 / 攻防都已算完）。它适合"在算好的结果上再追加战法"
        /// （<see cref="Action.TroopAddSkill"/>），不适合"换兵种"这类**必须参与计算**的修改。
        /// 本事件专供"要在计算开始前改写兵种 / 基础值"的 Action（如
        /// <see cref="Action.TroopChangeTroopType"/>）使用。
        ///
        /// 参数：部队、场景对象
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopBeforeInitSkill;

        /// <summary>
        /// 部队计算属性的时候
        /// </summary>
        public static EventDelegate<Troop, Scenario> OnTroopBeforeCalculateAttribute;
        public static EventDelegate<Troop, Scenario> OnTroopCalculateAttribute;
        public static EventDelegate<Troop, Scenario> OnTroopAfterCalculateAttribute;
        
        /// <summary>
        /// 部队计算反击的时候
        /// </summary>
        public static EventDelegate<Troop, Troop, SkillInstance, Scenario, OverrideData<int>> OnTroopCalculateAttackBack;

        /// <summary>
        /// 当城池沦陷的时候
        /// </summary>
        public static EventDelegate<City, Force, Troop> OnCityFall;

        /// <summary>
        /// 据点更换所属势力的时候（非战斗攻陷的易主）。
        ///
        /// 【为什么需要】城市的势力色是**只在节点创建时写一次**的（小地图城市节点
        /// MapCityNodeData 的构造 / 战略地图贴图），原先只有 OnCityFall 一条刷新入口。
        /// 于是劝降合并（DiplomacyActionAnnex → Force.SurrenderAllBuildings）、
        /// 事件改属、编辑器改属这些"没打仗就换旗"的路径，数据层已经改完，
        /// 小地图却仍然显示旧势力的颜色。
        ///
        /// 【与 OnCityFall 的区别】OnCityFall 带"被谁攻陷"的语义，
        /// 订阅方包含剧本事件触发器（城池陷落类事件）、玩家消息、势力灭亡提示等
        /// 一整套**战斗语义**的消费者，非战斗易主绝不能借用它 —— 会误触发剧本事件。
        /// 本事件只表达"这个据点现在归谁了"，订阅方仅限表现层刷新。
        ///
        /// 【派发点】统一在 <see cref="Sango.Core.CityStateSystem.SetCorps"/> 里派发，
        /// 那是本工程唯一的据点改旗入口（治理层约定，禁止直接写 BelongCorps/BelongForce）。
        /// 参数：据点、换旗前的势力（可为 null）、换旗后的势力（可为 null = 无主）
        /// </summary>
        public static EventDelegate<City, Force, Force> OnCityChangeForce;

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
        /// 「探索人才」**结算完成**（演出结束后才抛）。
        ///
        /// 参数：执行者 Person、所在 City、结果码、发现的武将。
        /// 结果码与 <c>City.DoJobSearching</c> 的返回值一致：
        ///   0 = 发现人才（末位参数有效）／ &gt;0 = 发现资金，数值即金额 ／
        ///   -2 = 发现宝物 / 装备（道具已进势力国库，本事件不带出道具对象）／ -1 = 一无所获。
        ///
        /// 【为什么需要它，而不是用 OnPersonActionOver】
        /// OnPersonActionOver 是"武将行动结束"这个**通用**信号，它在 City.DoJobSearching
        /// **执行期间**就被抛出来，于是：
        ///   · 无法判断探索是成功还是失败（调用方只知道"某人的动作结束了"）；
        ///   · 抛出时旧演出（CityPersonSearchingEvent）还停在 Enter 里等对话框回调，
        ///     此刻若启动另一套演出，两边会同时往 GameDialog 投递，窗口互相覆盖。
        /// 本 hook 在演出**彻底结束**（RenderEvent 调用 Exit）之后才抛，并带上结果码，
        /// 因此既能区分成败，也不会与旧演出撞车。
        /// </summary>
        public static EventDelegate<Person, City, int, Person> OnCityJobSearchingSettled;

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
        /// 每回合城池耐久自然回复量修正（技巧「培训工兵」用）
        /// City, OverrideData
        /// 【原版出处】s11_game_week.cpp:943-953：持有 TechId_Kouheiikusei 时，
        /// 据点周回复量由 60~180 提升为 150~450（同一公式 ×2.5），受攻击中不回复。
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateDurabilityRecover;

        /// <summary>
        /// 计算城池收入
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City> OnCityCalculateHarvest;
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateGoldHarvest;
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateGoldHarvestAfter;
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateFoodHarvest;
        public static EventDelegate<City, OverrideData<int>> OnCityCalculateFoodHarvestAfter;

        public static EventDelegate<City, Scenario, OverrideData<int>> OnCityCalculateFoodCost;


        /// <summary>
        /// 城池获取资金收入
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityGainGoldHarvest;

        /// <summary>
        /// 城池获取粮食收入
        /// City, Troop, OverrideData
        /// </summary>
        public static EventDelegate<City, OverrideData<int>> OnCityGainFoodHarvest;

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
        /// 武将负伤事件：伤病等级被加重之后触发（受伤武将, 造成伤害的部队，可能为空）
        /// </summary>
        public static EventDelegate<Person, Troop> OnPersonInjury;

        /// <summary>
        /// 询问某武将能否负伤：免伤效果在这里拦截（如特技 #53 护卫），把 canInjure 设为 false 即免疫本次伤病。
        /// 由 PersonInjury.Injure 在**写入伤病之前**派发，所以拦截方不需要关心与伤害来源的先后顺序。
        /// 受伤武将, 伤害来源(部队/战法/单挑等，可为空), 是否允许受伤
        /// </summary>
        public static EventDelegate<Person, SangoObject, OverrideData<bool>> OnPersonCheckInjury;

        /// <summary>
        /// 询问某武将能否战死：免死效果在这里拦截（如特技 #53 护卫），把 canDie 设为 false 即免于本次战死。
        /// 由 Person.DeadInBattle 在**任何死亡处理之前**派发，与 OnPersonCheckInjury 同一套顺序约定。
        /// 只覆盖"战死"（单挑落败身死等战场死亡）；处决、病逝、剧本事件死亡走 Person.Dead，不派发本事件。
        /// 战死武将, 致死来源(部队/决斗对手等，可为空), 是否允许战死
        /// </summary>
        public static EventDelegate<Person, SangoObject, OverrideData<bool>> OnPersonCheckBattleDeath;

        /// <summary>
        /// 武将死亡事件：<c>Person.Dead</c> 完成全部死亡处理（摘名册 / 清任务 / 君主继承）之后派发。
        ///
        /// 【与 OnPersonCheckBattleDeath 的区别】
        ///   · 那个在**死亡之前**问"能不能死"（护卫等免死效果在这里拦截），只覆盖战死；
        ///   · 本事件在**死亡之后**通知"已经死了"，覆盖所有死法（战死 / 病逝 / 处决 / 自刎 / 遇刺）。
        ///
        /// 【参数】死亡武将, 死亡时的所属势力(可能为空：在野 / 已脱离势力), 死因
        ///（取值见 <see cref="PersonDeathCause"/>，只用于播报文案，不参与任何结算）。
        ///
        /// 【为什么带上势力】死亡处理会把 BelongForce 清空，监听者事后从武将身上读不到
        /// "他死前属于谁"，无法判断该不该给玩家播报 —— 因此在派发前缓存并随事件传出。
        /// </summary>
        public static EventDelegate<Person, Force, string> OnPersonDied;

        /// <summary>
        /// 武将装备变更事件
        /// </summary>
        public static EventDelegate<Person> OnPersonEquipmentChanged;

        /// <summary>
        /// 当武将行动完
        /// </summary>
        public static EventDelegate<Person> OnPersonActionOver;

        /// <summary>
        /// 势力忠诚换季衰减概率
        /// </summary>
        public static EventDelegate<Force, OverrideData<int>> OnForcePersonLoyaltyChangeProbability;

        /// <summary>
        /// 势力武将换季忠诚变化结算。监听者可将 shouldLoseLoyalty 设为 false 阻止本次扣忠。
        /// </summary>
        public static EventDelegate<Force, Person, OverrideData<bool>> OnForcePersonLoyaltyChange;

        /// <summary>
        /// 部队计算本次获得的技巧点。仅击破敌方部队时 isDestroyEnemyTroop 为 true。
        /// </summary>
        public static EventDelegate<Troop, bool, OverrideData<int>> OnTroopCalculateTechniquePoint;

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
        /// 询问"是否允许给这支部队施加状态"。被施加方把 Value 置为 false 即免疫本次 Buff。
        ///
        /// <para>参数：被施加方部队、Buff Id、是否允许（默认 true）。</para>
        /// <para>
        /// 【性能约定】派发点必须先判 null；无订阅者时不要创建 OverrideData，
        /// 否则每次上状态都会多一次池化对象往返。
        /// </para>
        /// </summary>
        public static EventDelegate<Troop, int, OverrideData<bool>> OnTroopCheckBuff;

        /// <summary>
        /// 部队**已经**获得某个状态之后的派发点。
        ///
        /// <para>参数：目标部队、状态 Id、施加者（可能是部队 / 建筑 / null）、持续回合数。</para>
        /// <para>
        /// 【与 OnTroopCheckBuff 的分工】那个是"施加前的否决"（免疫 / 拒绝），
        /// 这个是"已经生效后的追加"（扩散、转嫁）。汉土的
        /// 煽惑 / 疑机 / 佯装 一类"异常状态向邻军扩散"就跑在这里。
        /// </para>
        /// <para>
        /// 【防递归】扩散出去的追加状态会再次触发本事件，订阅方必须用
        /// <c>spreadDepth</c> 之类的深度标记截断，否则相邻两军会互相弹射。
        /// </para>
        /// <para>【性能约定】派发点先判 null；无订阅者时不要构造任何对象。</para>
        /// </summary>
        public static EventDelegate<Troop, int, SangoObject, int> OnTroopBuffApplied;

        /// <summary>
        /// 询问"是否允许这支部队被战法位移"。被位移方把 Value 置为 false 即免疫本次位移。
        ///
        /// <para>参数：被位移方部队、施法方部队、是否允许（默认 true）。</para>
        /// <para>
        /// 【与 OnTroopSkillOffset 的区别】那个是"位移已经发生"之后的通知（猛者负伤等），
        /// 这个是位移结算之前的否决点（铁壁：不会被敌方战法位移）。
        /// </para>
        /// </summary>
        public static EventDelegate<Troop, Troop, OverrideData<bool>> OnTroopCheckSkillOffset;

        /// <summary>
        /// 询问"这栋建筑本次受到的耐久伤害是否要修正"（坚城 / 固守：城池耐久伤害降低）。
        ///
        /// <para>参数：建筑本体、攻击来源（可能是 Troop 或 SkillInstance）、耐久变化量（负数=受伤）。</para>
        /// <para>
        /// 【性能约定】派发点先判 <c>num &lt; 0</c> 且事件非 null：回复耐久不派发，
        /// 无订阅者时也不创建 OverrideData。
        /// </para>
        /// </summary>
        public static EventDelegate<BuildingBase, SangoObject, OverrideData<int>> OnBuildingCheckDurabilityDamage;

        /// <summary>
        /// 道具授予完成后派发（一次性消耗品在这里生效并移除自身，如万寿果）。
        ///
        /// <para>参数：执行操作的城市、目标武将、被授予的道具。</para>
        /// <para>
        /// 【派发时机】<c>ItemGrantRules.Grant</c> 里在 <c>person.InitPersonActions()</c> **之后**、
        /// 写日志之前。放在重建之后是为了让道具自带的 Action 已经装配好，能收到本次通知。
        /// </para>
        /// </summary>
        public static EventDelegate<City, Person, ItemType> OnItemGranted;

        /// <summary>
        /// 询问"登用武将的最终成功率"（伯乐 / 蜀相 / 论客等人事类特技）。
        ///
        /// <para>
        /// 参数：执行者、目标武将、登用类型（<c>PersonRecruitType</c>）、成功率（0-100）。
        /// 监听者可以加减百分点或按百分比缩放；派发点会做 0-100 截断。
        /// </para>
        /// <para>
        /// 【与 RecruitPersonProbabilityOverride 的区别】那是整段替换算式（单委托，会互相覆盖）；
        /// 本事件是"在既有算式结果上做增量修正"，可以被多个特技同时订阅。
        /// </para>
        /// </summary>
        public static EventDelegate<Person, Person, int, OverrideData<int>> OnPersonRecruitProbability;

        /// <summary>
        /// 在野武将求仕成功并入仕。
        ///
        /// <para>参数：求仕武将、目标势力、接收城。战报播报（PlayerMessage）与外部统计订阅这里。</para>
        /// </summary>
        public static EventDelegate<Person, Force, City> OnPersonSeekOfficeAccepted;

        /// <summary>
        /// 在野武将求仕被答复（true = 接纳，false = 婉拒）。
        ///
        /// <para>
        /// 与 <see cref="OnPersonSeekOfficeAccepted"/> 分开：婉拒没有入仕，但统计需要成对记录；
        /// 两者都不负责播报，播报统一由 PlayerMessage 订阅 Accepted 完成。
        /// </para>
        /// </summary>
        public static EventDelegate<Person, Force, bool> OnPersonSeekOfficeDecided;

        /// <summary>
        /// 询问"城陷时攻方可保留多少座内政设施"（安民：占领城池保留建筑）。
        ///
        /// <para>
        /// 参数：被攻陷的城池、攻方部队、可保留座数（初始 = 攻方主将魅力 / 20）。
        /// 监听者可以追加座数；置为 <c>int.MaxValue</c> 表示全部保留。
        /// </para>
        /// <para>
        /// 【为什么不做成"是否全保留"的布尔】原版的存留本来就是"按名额"的连续量，
        /// 用座数语义可以同时表达"多保留 3 座"和"全部保留"，也不破坏既有的魅力公式。
        /// </para>
        /// </summary>
        public static EventDelegate<City, Troop, OverrideData<int>> OnCityCalculateBuildingKeepCount;

        /// <summary>
        /// 当部队兵力变化时（数值修正入口）。
        ///
        /// 参数：(受影响的部队, 施加方, 反击信息, 待修正的兵力变化量)。
        /// 第三个参数是 <see cref="AttackBackInfo"/>：<c>IsAttackBack</c> 判是否反击、
        /// <c>Factor</c> 取强度（部队 28~90 / 据点 20~50）、<c>Source</c> 取来源。
        /// 编译期已无法写 <c>atkBack == 1</c>，历史误判不会再出现。
        /// </summary>
        public static EventDelegate<Troop, SangoObject, AttackBackInfo, OverrideData<int>> OnTroopChangeTroops;

        /// <summary>
        /// 部队受到伤害时收集"范围减伤"（多个设施/光环取最大值，不连乘）。
        ///
        /// 与 <see cref="OnTroopChangeTroops"/> 分开：后者是逐项修正（同类可能连乘），
        /// 这里只收集百分点，由 Troop.ChangeTroops 统一按最大值应用一次。
        /// </summary>
        public static EventDelegate<Troop, SangoObject, AttackBackInfo, OverrideData<int>> OnTroopCalculateAuraDamageReduce;

        /// <summary>
        /// 单挑结算完成（统一上下文：胜方/败方/主将/结果/败方坐标）。
        /// 旧事件 <see cref="OnDuelEnd"/> 保留，本事件只做"特技效果统一入口"。
        /// </summary>
        public static EventDelegate<CombatContext> OnCombatDuelFinished;

        /// <summary>部队被击破（统一上下文：施术者/目标/坐标/伤害前后）</summary>
        public static EventDelegate<CombatContext> OnCombatTroopDestroyed;

        /// <summary>战法伤害结算完成（统一上下文）</summary>
        public static EventDelegate<CombatContext> OnCombatDamageCalculated;

        /// <summary>命中判定成功（伤害结算前，统一上下文；damageBefore/After 尚未填充）</summary>
        public static EventDelegate<CombatContext> OnCombatHit;

        /// <summary>
        /// 据点反击**已经结算完伤害**之后的通知。
        ///
        /// <para>参数：被反击的攻击部队、发起反击的建筑 / 城池、本次反击伤害。</para>
        /// <para>
        /// 【与 OnBuildCalculateAttackBack 的分工】那个是"伤害计算前改数值"，
        /// 这个是"伤害落地后追加状态"（拒攻使敌军混乱 / 蛮弩使敌将负伤 / 弩炬起火）。
        /// 放在结算之后，才不会出现"伤害还没扣就先上状态"的时序错位。
        /// </para>
        /// <para>【性能约定】调用方先判 null；只有伤害 &gt; 0 才派发。</para>
        /// </summary>
        public static EventDelegate<Troop, BuildingBase, int> OnBuildingAttackBackSettled;

        /// <summary>
        /// 内政工作速度计算（预览 / 提交共用）。
        /// 特技通过修改 <see cref="CityWorkContext.speedFactor"/> 加成，保证显示与实扣一致。
        /// </summary>
        public static EventDelegate<CityWorkContext> OnCityCalculateWorkSpeed;

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
        /// 战法的位移效果把敌方部队推走之后（施法部队, 被位移部队, 位移格数）。
        ///
        /// 只在"目标真的换了格子"时派发：被挡住 / 无处可去（没推动）不派发，
        /// 因为依赖它的特技（特技 #52 猛者）语义就是"以战法让敌方部队移动区格"。
        /// 施法者自己位移（Master 系列）不算，只有目标部队被推走才派发。
        /// </summary>
        public static EventDelegate<Troop, Troop, int> OnTroopSkillOffset;

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
        /// 当有人提出单挑请求时（参数为挑战方与应战方部队）。
        /// 由 DuelChallengeFlow 统一接管：判定是否应战、是否观看、是否带表现层。
        /// 剧本事件想做"武将之间的单挑"，广播本事件即可，不必自己复制判定流程。
        /// </summary>
        public static EventDelegate<Troop, Troop> OnDuelChallengeRequest;

        /// <summary>
        /// 当单挑开始时（参数为参与单挑的挑战方与应战方部队）
        /// </summary>
        public static EventDelegate<Troop, Troop> OnDuelStart;

        /// <summary>
        /// 当单挑进行中，结果已确定、开始应用结果时（参数为单挑本体）
        /// </summary>
        public static EventDelegate<Duel.Duel> OnDuelFinished;

        /// <summary>
        /// 当单挑整体结束时（参数为单挑本体，附带胜负队伍与结果）
        /// </summary>
        public static EventDelegate<Duel.Duel> OnDuelEnd;

        /// <summary>
        /// 当有人请求发起舌战时（参数为挑战方与应战方武将）。
        /// 与单挑同理，由 DebateChallengeFlow 统一接管，剧本事件想做"武将之间的舌战"广播本事件即可。
        /// </summary>
        public static EventDelegate<Person, Person> OnDebateChallengeRequest;

        /// <summary>
        /// 当舌战开始时（参数为挑战方与应战方武将）
        /// </summary>
        public static EventDelegate<Person, Person> OnDebateStart;

        /// <summary>
        /// 当舌战整体结束时（参数为舌战本体，胜负与胜利方式在 Param 里）
        /// </summary>
        public static EventDelegate<Debate.Debate> OnDebateEnd;

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

        /// <summary>
        /// 执行劝降（原版 Annex）
        /// </summary>
        public static EventDelegate<Force, Force, bool> OnDiplomacyAnnex;

        /// <summary>
        /// 执行俘虏交换（原版 Exchange）
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacyExchange;

        /// <summary>
        /// 执行请求援军（原版 Reinforce）
        /// </summary>
        public static EventDelegate<Force, Force, int, bool> OnDiplomacyReinforce;
        #endregion Diplomacy

        #region bridge
        public static EventDelegate DialogClose;
        public static EventDelegate<Person, string, System.Action, System.Action> DialogShow;
        #endregion

        #endregion Game

    }

}
