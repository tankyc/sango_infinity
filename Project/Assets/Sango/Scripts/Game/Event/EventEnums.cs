/*
 * 文件名：EventEnums.cs
 * 描述：剧本事件系统的基础枚举定义（触发时机、角色槽来源、演出指令、等级、历史性强度等）
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件触发时机。
    /// 每一项都由 <see cref="ScenarioEventManager"/> 统一订阅对应的 GameEvent 委托，
    /// 命名于 GameEvent 中的 hook 一一对应（不存在的 hook 不得臆造）。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventTriggerKind
    {
        /// <summary>剧本开始（OnScenarioStart），用于开场剧情（如桃园结义）</summary>
        ScenarioStart = 0,

        /// <summary>回合开始（OnTurnStart），每回合检查一次</summary>
        TurnStart = 1,

        /// <summary>回合结束（OnTurnEnd）</summary>
        TurnEnd = 2,

        /// <summary>某个势力回合开始（OnForceTurnStart）</summary>
        ForceTurnStart = 3,

        /// <summary>月更替（OnMonthStart）</summary>
        MonthStart = 4,

        /// <summary>季节更替（OnSeasonUpdate）</summary>
        SeasonStart = 5,

        /// <summary>年更替（OnYearUpdate）</summary>
        YearStart = 6,

        /// <summary>部队进入格子（OnTroopEnterCell）——三英战吕布的"接敌瞬间"</summary>
        TroopEnterCell = 7,

        /// <summary>部队离开格子（OnTroopLeaveCell）</summary>
        TroopLeaveCell = 8,

        /// <summary>城池陷落（OnCityFall）</summary>
        CityFall = 9,

        /// <summary>武将登场（PersonStateType 由 Invalid 迁移为在野）</summary>
        PersonAppear = 10,

        /// <summary>武将加入势力（OnPersonChangeBelongCity + 势力归属校验）</summary>
        PersonJoinForce = 11,

        /// <summary>武将沦为俘虏（OnPersonCaptured）</summary>
        PersonCaptured = 12,

        /// <summary>武将被处决（OnPersonExecute）</summary>
        PersonExecute = 13,

        /// <summary>武将逃脱（OnPersonEscape）</summary>
        PersonEscape = 14,

        /// <summary>势力灭亡（OnForceFall）</summary>
        ForceFall = 15,

        /// <summary>部队创建（OnTroopCreated）</summary>
        TroopCreated = 16,

        /// <summary>部队被消灭（OnTroopDestroyed）</summary>
        TroopDestroyed = 17,

        /// <summary>建筑完工（OnBuildingComplete）</summary>
        BuildingComplete = 18,

        /// <summary>缔结同盟（OnDiplomacyAlliance）</summary>
        DiplomacyAlliance = 19,

        /// <summary>宣战（OnDiplomacyDeclareWar）</summary>
        DiplomacyDeclareWar = 20,

        /// <summary>赠送礼物（OnDiplomacySendGift）</summary>
        DiplomacySendGift = 21,

        /// <summary>缔结停战（OnDiplomacyTruce）</summary>
        DiplomacyTruce = 22,

        /// <summary>缔结婚姻（OnDiplomacyMarriage）</summary>
        DiplomacyMarriage = 23,

        /// <summary>单挑结束（OnDuelFinished）</summary>
        DuelFinished = 24,

        /// <summary>舌战结束（OnDebateEnd）</summary>
        DebateEnd = 25,

        /// <summary>
        /// 武将工作结算完成（OnPersonActionOver）。
        /// 注意：该 hook 只带 Person 一个参数，无法区分是搜索、内政还是登用，
        /// 因此必须用 TriggerDef.JobTypes 做二次判断（读 person.missionType）。
        /// </summary>
        JobFinished = 26,

        /// <summary>到达指定日期（OnDayUpdate，配合条件树里的 TimeRange / DateCheck）</summary>
        DateReach = 27,

        /// <summary>仅由其它事件或剧本脚本调用（无 hook，靠 CallEvent 或 Schedule 驱动）</summary>
        ManualCall = 28,

        /// <summary>
        /// 工作**结算完成**（OnCityJobSearchingSettled），**旧演出彻底结束之后**才抛，并带上结果码。
        ///
        /// 与 <see cref="JobFinished"/> 的区别：
        ///   · JobFinished 挂的是 OnPersonActionOver —— 通用信号、无结果、在旧演出**执行期间**抛出；
        ///   · JobSettled  挂的是 OnCityJobSearchingSettled —— 带结果码、在旧演出**结束之后**抛出。
        /// 凡是要区分"探索成功 / 一无所获"、或不想与旧演出抢 GameDialog 的事件，都该用本时机。
        /// 结果码过滤见 <see cref="EventTriggerDef.JobResults"/>。
        /// </summary>
        JobSettled = 29,
    }

    /// <summary>
    /// 角色槽的对象类型。
    /// 决定槽位绑定结果用什么类型去解析与校验。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventSlotType
    {
        /// <summary>势力</summary>
        Force = 0,

        /// <summary>军团</summary>
        Corps = 1,

        /// <summary>城池（含关、港）</summary>
        City = 2,

        /// <summary>武将</summary>
        Person = 3,

        /// <summary>
        /// 部队。
        /// 【重要】部队是运行时创建 / 销毁的对象，其 Id 每局游戏都可能不同，
        /// 因此事件 JSON 里**禁止**用 Fixed + Id 绑定部队槽，
        /// 必须使用 TroopOfPerson / ForceBestTroop / NeighborTroop / EventEntity 这类描述性来源。
        /// </summary>
        Troop = 4,

        /// <summary>战法</summary>
        Skill = 5,

        /// <summary>地图格子</summary>
        Cell = 6,

        /// <summary>建筑</summary>
        Building = 7,
    }

    /// <summary>
    /// 角色槽的取值来源。决定"这个槽的人 / 城 / 势力从哪来"。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventSlotSource
    {
        /// <summary>指定对象 Id（编辑器中下拉选择）。城市 / 势力 / 武将 / 建筑可安全使用；部队禁用</summary>
        Fixed = 0,

        /// <summary>本次 hook 注入的事件实体（如被俘的武将、陷落的城池）</summary>
        EventEntity = 1,

        /// <summary>玩家操作的势力</summary>
        PlayerForce = 2,

        /// <summary>势力的君主</summary>
        ForceGovernor = 3,

        /// <summary>势力的军师</summary>
        ForceCounsellor = 4,

        /// <summary>势力内某属性最高的武将（SourceArg 指定属性名，如 Intelligence）</summary>
        ForceBestPerson = 5,

        /// <summary>势力内的随机武将</summary>
        ForceRandomPerson = 6,

        /// <summary>城市的太守</summary>
        CityGovernor = 7,

        /// <summary>城内的随机武将</summary>
        CityRandomPerson = 8,

        /// <summary>玩家势力的主城</summary>
        PlayerMainCity = 9,

        /// <summary>事件实体的邻接势力</summary>
        NeighborForce = 10,

        /// <summary>事件实体的邻接城市</summary>
        NeighborCity = 11,

        /// <summary>指定武将所在的部队（SourceArg 指定武将槽的 Key）</summary>
        TroopOfPerson = 12,

        /// <summary>势力内兵力最多的部队</summary>
        ForceBestTroop = 13,

        /// <summary>指定槽位部队的相邻敌方部队（SourceArg 指定部队槽的 Key）</summary>
        NeighborTroop = 14,

        /// <summary>由另一槽位派生（SourceArg 形如 "SlotKey.BelongForce"）</summary>
        DerivedOfSlot = 15,

        /// <summary>由剧本变量指定（SourceArg 为变量名）</summary>
        ScenarioVariable = 16,
    }

    /// <summary>
    /// 触发作用域。决定"这个事件对谁检查"。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventTriggerScope
    {
        /// <summary>任意势力（全局检查一次）</summary>
        AnyForce = 0,

        /// <summary>仅玩家势力</summary>
        PlayerForce = 1,

        /// <summary>指定势力的回合（配合 ScopeId 使用）</summary>
        SpecificForce = 2,

        /// <summary>指定城池相关（配合 ScopeId 使用）</summary>
        SpecificCity = 3,
    }

    /// <summary>
    /// 演出指令类型。EventStage 的线性指令流由这些指令组成，
    /// 配合 Label / Jump 实现分支。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventOpType
    {
        /// <summary>台词（支持多句链路，映射 GameDialog.OpenTalk）</summary>
        Talk = 0,

        /// <summary>纯文本框（旁白）</summary>
        TalkWindow = 1,

        /// <summary>玩家选择肢（映射 PlayerChoice）</summary>
        Choice = 2,

        /// <summary>世界状态结算（效果组）</summary>
        Effect = 3,

        /// <summary>强制发起事件单挑（三英战吕布）</summary>
        Duel = 4,

        /// <summary>
        /// 伪舌战：台词链 + 直接结算。
        /// 真实舌战界面未完成（DebateChallengeFlow.ForceNoView == true），
        /// 因此一期用本指令做"舌战群儒""骂死王朗"等演出，等界面完成后再换成真实舌战。
        /// </summary>
        DebateFake = 5,

        /// <summary>强制发起部队交战</summary>
        Battle = 6,

        /// <summary>镜头移动</summary>
        Camera = 7,

        /// <summary>背景音乐</summary>
        Bgm = 8,

        /// <summary>音效</summary>
        Se = 9,

        /// <summary>语音</summary>
        Voice = 10,

        /// <summary>打开任意 UI 窗口（如势力情报、图片）</summary>
        Window = 11,

        /// <summary>玩家消息（结果情报逐条弹）</summary>
        Message = 12,

        /// <summary>等待（计时或等待点击）</summary>
        Wait = 13,

        /// <summary>标签（分支跳转的目标）</summary>
        Label = 14,

        /// <summary>无条件跳转</summary>
        Jump = 15,

        /// <summary>条件跳转（条件成立则跳到 Target，否则继续下一条）</summary>
        Branch = 16,

        /// <summary>事件私有变量运算</summary>
        SetVar = 17,

        /// <summary>事件旗标设置（跨事件、进存档）</summary>
        SetFlag = 18,

        /// <summary>嵌套调用另一事件</summary>
        CallEvent = 19,

        /// <summary>登记定时待办（跨回合事件链）</summary>
        Schedule = 20,

        /// <summary>跳过剩余演出，只结算效果</summary>
        SkipAll = 21,

        /// <summary>并行执行一组指令（如"镜头推近 + 台词同时"）</summary>
        Parallel = 22,

        /// <summary>情报面板（势力对比 / 武将一览 / 地图形势）</summary>
        InfoPanel = 23,

        /// <summary>场景切换（城郭 / 战场 / 室内 / 山野 / 水滨）</summary>
        SceneChange = 24,

        /// <summary>天气与风向演出（借东风、水淹七军）</summary>
        WeatherEffect = 25,

        /// <summary>火攻演出与蔓延（赤壁、夷陵）</summary>
        FireEffect = 26,

        /// <summary>人物特写</summary>
        CloseUp = 27,

        /// <summary>结束（缺省则跑完自动结束）</summary>
        End = 28,

        /// <summary>
        /// 图文演出：铺满屏幕展示一张 CG（或序列帧动图）+ 一段文字，等玩家点击后才继续。
        ///
        /// 窗口骨架来自 `window_force_destroy`（势力灭亡的演出窗口），它本身就是
        /// "事件演出 UI"的范例：全屏遮罩 + 入场 Animation + UIImageAnimation 序列帧
        /// + 音效 + 演完才允许关闭 + 关闭回调。
        /// </summary>
        Picture = 29,

        /// <summary>提前收掉图文窗口（正常情况玩家点击会自行关闭，这条用于分支里强制收场）</summary>
        PictureClose = 30,
    }

    /// <summary>
    /// 事件等级。决定事件发生时的打断强度，用于控制事件爆发密度。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventLevel
    {
        /// <summary>历史节点，必须中断玩家操作，独占演出</summary>
        Critical = 0,

        /// <summary>正常弹出对话框（默认）</summary>
        Normal = 1,

        /// <summary>不弹窗，只写入"事件一览"窗口与玩家消息</summary>
        Ambient = 2,

        /// <summary>完全静默，只结算效果</summary>
        Silent = 3,
    }

    /// <summary>
    /// 历史性强度。决定"局势不符历史时，这个事件还触不触发"。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventMode
    {
        /// <summary>强行按演义走：条件宽松 + 关键人物保护（适合"演义模式"剧本）</summary>
        Historical = 0,

        /// <summary>条件满足即触发，结果可偏移（默认）</summary>
        Semihistorical = 1,

        /// <summary>条件严格，局势不符就不触发（适合"自由模式"剧本）</summary>
        Flexible = 2,
    }

    /// <summary>
    /// 事件分类。用于编辑器左侧列表分组。
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EventCategory
    {
        /// <summary>史实历史事件</summary>
        History = 0,

        /// <summary>通用事件（不限于某个剧本）</summary>
        Common = 1,

        /// <summary>人物相关</summary>
        Person = 2,

        /// <summary>部队相关</summary>
        Troop = 3,

        /// <summary>外交相关</summary>
        Diplomacy = 4,

        /// <summary>教学</summary>
        Tutorial = 5,

        /// <summary>Mod 自定义</summary>
        Mod = 6,
    }

    /// <summary>
    /// 条件求值开销分级。
    /// 求值器会把条件树展平后按此升序排序，实现短路求值——
    /// 廉价条件先跑，昂贵的（要遍历集合 / 算距离）放后面。
    /// </summary>
    public enum ConditionCost
    {
        /// <summary>极廉价：纯内存比较（旗标、事件计数、难度）</summary>
        Trivial = 0,

        /// <summary>廉价：单对象属性读取（武将属性、忠诚）</summary>
        Cheap = 1,

        /// <summary>中等：需要遍历某势力的集合（城池数、武将数）</summary>
        Normal = 2,

        /// <summary>昂贵：全场景遍历（距离计算、寻路）</summary>
        Expensive = 3,
    }
}
