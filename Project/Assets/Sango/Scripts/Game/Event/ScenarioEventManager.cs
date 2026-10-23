/*
 * 文件名：ScenarioEventManager.cs
 * 描述：剧本事件调度器——负责事件的触发判定、定时队列推进、运行时状态的读写
 * 创建日期：2026-09-25
 * 最后修改：2026-09-26
 */

using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 剧本事件调度器。
    ///
    /// 职责边界：
    ///   · 本类只回答"**要不要发生、什么时候发生、谁参与**"，不做演出（演出交给 EventRunner）。
    ///   · 事件的驱动一律**订阅 GameEvent 的既有 hook**，不新增游戏主循环。
    ///
    /// 【订阅策略 · 重要】本类是应用级单例（由 GameSystemManager.Init 反射创建），
    /// 在 Init() 里订阅 GameEvent 并**终身不解绑**。
    /// 这样它会被 GameEventBaseline 的基线快照覆盖，读档收尾时不会被误清；
    /// Clear() 只负责清"数据"（队列、计数、缓存），不碰订阅。
    ///
    /// 【读档生命周期】本类的数据清理挂在 ScenarioLifecycle.BeginShutdown() 的 5.5 步，
    /// 见 Game/System/ScenarioLifecycle.cs。
    /// </summary>
    [GameSystem(order = 900)]
    public class ScenarioEventManager : GameSystem
    {
        /// <summary>
        /// 全局旗标 / 全局变量使用的伪事件 Id。
        ///
        /// 为什么复用 EventRunState 而不给 Scenario 加新字段：
        /// 全局旗标（如 "SwornBrothersDone"）需要跨事件读取且进存档，
        /// 而 EventRunState 已经是"随剧本持久化的键值袋"，
        /// 用一个保留 Id 承载它，可零新字段、零新增序列化路径。
        /// </summary>
        public const int GlobalStateEventId = 0;

        /// <summary>
        /// 单例访问入口。
        /// 本类是 [GameSystem]，实例由 GameSystemManager 反射创建并注册，
        /// 因此不能用 Singleton&lt;T&gt;，必须走系统表。
        /// </summary>
        public static ScenarioEventManager Instance
        {
            get { return GameSystem.GetSystem<ScenarioEventManager>(); }
        }

        /// <summary>是否已完成初始化（防止 Game.Init 多次调用造成重复订阅）</summary>
        bool inited;

        /// <summary>事件库（三源加载 + 按触发时机的索引）</summary>
        readonly EventDatabase database = new EventDatabase();

        /// <summary>事件库（只读访问，供调试面板与编辑器查看）</summary>
        public EventDatabase Database { get { return database; } }

        /// <summary>剧本开局年份。用于 ScenarioFilter 的 YearRange 过滤（Info.year 会随游戏推进变化）</summary>
        int scenarioStartYear;

        /// <summary>本回合已求值的事件数（用于 MaxEventEvalPerTurn 预算控制）</summary>
        int evaluatedThisTurn;

        /// <summary>本回合已播放的事件数（用于 MaxEventFirePerTurn 密度控制）</summary>
        int firedThisTurn;

        /// <summary>当前正在播放的事件 Id，0 表示空闲</summary>
        int runningEventId;

        /// <summary>
        /// 每回合求值预算上限。防止"数百个事件 × 每回合多次 hook"把帧率吃穿。
        /// 超预算后本回合不再评估剩余候选，下回合继续。
        /// </summary>
        const int MaxEventEvalPerTurn = 200;

        /// <summary>
        /// 每回合最多播放的事件数（密度控制）。
        /// Critical 级事件不受此限——历史节点必须发生。
        /// </summary>
        const int MaxEventFirePerTurn = 3;

        /// <summary>候选排序用的临时缓冲（避免每次求值都分配）</summary>
        readonly List<EventDefinition> sortedBuffer = new List<EventDefinition>();

        #region 让路与暂缓

        /// <summary>
        /// 因"别人正在演出"而暂缓的触发。结构里连**结果码**一起存 ——
        /// JobSettled 类事件的触发过滤依赖它，丢了就退化成"任意结果都触发"。
        /// </summary>
        struct DeferredTrigger
        {
            public EventTriggerKind kind;

            /// <summary>
            /// 注入的实体数组（索引 0 与旧字段 entity 等价）。
            /// 暂缓后重试时必须原样还原，否则"多参与方"事件在让路一轮后会丢掉第 2/3 个实体，
            /// 表现为"第一次能触发、被暂缓一次后再也绑不上槽位"。
            /// </summary>
            public SangoObject[] entities;

            /// <summary>注入的格子（可为 null）</summary>
            public Cell cell;

            public int jobResult;
        }

        /// <summary>暂缓上限。旧演出迟迟不结束时不无限堆积，超了明确报错并丢弃。</summary>
        const int MaxDeferredTriggers = 32;

        /// <summary>
        /// 本回合新增的暂缓项数。
        /// 只计数不逐条打日志：TroopLeaveCell 按格触发，AI 大军行军一回合几十上百次，
        /// 逐条输出会把控制台刷爆（见 <see cref="ReportDeferredStats"/>）。
        /// </summary>
        int deferredAddCount;

        /// <summary>本回合因队列满被丢弃的触发数，与 <see cref="deferredAddCount"/> 一起在回合开始时汇总</summary>
        int deferredDropCount;

        /// <summary>暂缓队列（下一回合开始时重试）</summary>
        readonly List<DeferredTrigger> deferredTriggers = new List<DeferredTrigger>();

        /// <summary>排空暂缓队列时的快照缓冲（复用，避免每次分配）</summary>
        readonly List<DeferredTrigger> deferBuffer = new List<DeferredTrigger>();

        /// <summary>排空重入锁。排空过程中又产生新的暂缓项时，本轮不再处理，留到下一回合。</summary>
        bool drainingDeferred;

        /// <summary>
        /// 本次 hook 携带的工作结果码（仅 <see cref="EventTriggerKind.JobSettled"/> 有意义）。
        /// <see cref="int.MinValue"/> 表示"本次不是工作结算触发的"。
        ///
        /// 为什么用一个字段而不是传参：<see cref="TryFireEvent"/> 是公开方法（调试面板会单独调），
        /// 它的签名不该被结果码污染。单线程 + 一次只处理一个 hook，字段是安全的。
        /// </summary>
        int currentJobResult = int.MinValue;

        /// <summary>当前暂缓中的触发数（调试面板用）</summary>
        public int DeferredCount { get { return deferredTriggers.Count; } }

        #endregion

        /// <summary>本回合已求值的事件数（调试面板用）</summary>
        public int EvaluatedThisTurn { get { return evaluatedThisTurn; } }

        /// <summary>本回合已播放的事件数（调试面板用）</summary>
        public int FiredThisTurn { get { return firedThisTurn; } }

        /// <summary>当前是否正在播放事件（播放期间必须阻塞回合推进与存档）</summary>
        public bool IsPlaying { get { return runningEventId > 0; } }

        /// <summary>
        /// 是否正在播放事件（静态便捷判断）。
        ///
        /// 供 <c>Scenario.Run()</c> 这类外部调用点使用 —— 它和旧体系的 RenderEvent 一样，
        /// 需要"演出期间挡住整条回合推进"，否则事件在等玩家点对话框时，
        /// AI 势力会在后台把整个回合跑完。
        /// </summary>
        public static bool IsPlayingNow
        {
            get
            {
                ScenarioEventManager manager = Instance;
                return manager != null && manager.IsPlaying;
            }
        }

        /// <summary>上一次已知的总开关状态，仅用于"开关变化时"打一条日志（避免每帧刷屏）</summary>
        bool lastEnabledState = true;

        /// <summary>
        /// 剧本事件系统总开关（剧本参数：<see cref="ScenarioVariables.eventSystemEnabled"/>）。
        ///
        /// 只管控**常规剧本事件**：关掉后常规事件不再触发、定时队列不推进、暂缓项不重试。
        ///
        /// **固定游戏事件**（<see cref="EventDefinition.AlwaysEnabled"/>）是例外：
        /// 它们承担"游戏流程完整性"（结局演出、退出出口），无论开关如何都照常触发。
        /// 因此本属性为 false **不等于**"事件系统完全停摆"。
        ///
        /// 另外：**正在播的那一个事件会播完**（中途掐断会留下半开窗口）。
        /// 详见 <see cref="ScenarioVariables.eventSystemEnabled"/> 的注释。
        ///
        /// 剧本未加载 / 变量缺失时按"启用"处理：宁可多播，也不要因为数据缺失让整个系统静默失效。
        /// </summary>
        public bool Enabled
        {
            get
            {
                Scenario scenario = Scenario.Cur;
                bool enabled = scenario == null || scenario.Variables == null
                    ? true
                    : scenario.Variables.eventSystemEnabled;

                // 在 getter 里打日志有点非常规，但这里只在**状态真的变化**时才写一条，
                // 而开关变化正是排查"事件怎么突然不弹了"时最需要的那条线索。
                if (enabled != lastEnabledState)
                {
                    lastEnabledState = enabled;
                    Log.Info(enabled
                        ? "剧本事件系统：已启用"
                        : "剧本事件系统：已关闭（常规剧本事件不再触发、定时队列暂停；固定游戏事件仍会触发，正在播的事件会播完）");
                }

                return enabled;
            }
        }

        /// <summary>当前正在播放的事件 Id，空闲时为 0</summary>
        public int RunningEventId { get { return runningEventId; } }

        /// <summary>剧本开局年份（调试面板用）</summary>
        public int ScenarioStartYear { get { return scenarioStartYear; } }

        #region 生命周期

        /// <summary>
        /// 初始化。由 GameSystemManager.Init() 在 Game.Init() 阶段反射调用一次。
        /// 这里订阅 GameEvent 属于"应用级订阅"，由 GameEventBaseline 保住，终身不解绑。
        /// </summary>
        public override void Init()
        {
            // 防重入：Game.Init 若被再次调用，接口会重复订阅（GameEventBaseline 只兜底、不阻止）
            if (inited) return;
            inited = true;

            Name = "ScenarioEventManager";

            // 注册事件专用的条件与效果。
            // 时机：本方法由 GameSystemManager.Init() 调用（Game.cs:83），
            // 而 Condition.Init() 更早（Game.cs:71），因此核心条件先注册、事件条件后追加，
            // 二者不会互相覆盖；且都早于任何剧本加载，使用前注册表一定是完整的。
            EventConditionFactory.InitAll();
            EventEffectBase.InitAll();

            // 事件库是应用级静态数据，只加载一次（三源合并，Mod 覆盖内置）
            database.Load();

            // —— 回合开始：推进定时队列 + 评估本回合候选事件 ——
            // 注意：Scenario.TurnStart() 是先让各对象跑 OnTurnStart，最后才广播 GameEvent.OnTurnStart，
            // 因此在这里处理时，所有系统都已完成本回合初始化，无需再等一帧。
            GameEvent.OnTurnStart += OnTurnStart;

            // —— 势力回合开始：给 ForceTurnStart 类事件用 ——
            GameEvent.OnForceTurnStart += OnForceTurnStart;

            // —— 剧本开始：清掉上一局的残留缓存，并捕获开局年份 ——
            GameEvent.OnScenarioStart += OnScenarioStart;

            // —— 读档：同上，缓存不能跨局存活 ——
            GameEvent.OnGameLoad += OnGameLoad;

            // —— 城池陷落：CityFall 类事件 ——
            GameEvent.OnCityFall += OnCityFall;

            // —— 武将工作完成：JobFinished 类事件 ——
            // 注意这是**通用**信号，无结果码，且在旧演出执行期间抛出；能用 JobSettled 就别用它。
            GameEvent.OnPersonActionOver += OnPersonActionOver;

            // —— 探索人才结算完成：JobSettled 类事件（带结果码，旧演出结束后才抛）——
            GameEvent.OnCityJobSearchingSettled += OnCityJobSearchingSettled;

            // —— 部队进入格子：TroopEnterCell 类事件（三英战吕布）——
            GameEvent.OnTroopEnterCell += OnTroopEnterCell;
            GameEvent.OnTroopLeaveCell += OnTroopLeaveCell;

            // —— 武将沦为俘虏 / 被处决 / 逃脱：用于人事类事件 ——
            GameEvent.OnPersonCaptured += OnPersonCaptured;
            GameEvent.OnPersonExecute += OnPersonExecute;
            GameEvent.OnPersonEscape += OnPersonEscape;

            // —— 武将归属变化：PersonJoinForce 类事件 ——
            GameEvent.OnPersonChangeBelongCity += OnPersonChangeBelongCity;

            // —— 势力灭亡 ——
            GameEvent.OnForceFall += OnForceFall;

            // —— 统一天下（天下一统）：剧本终局事件（胜利结局）——
            GameEvent.OnWorldUnified += OnWorldUnified;

            // —— 部队创建 / 消灭 ——
            GameEvent.OnTroopCreated += OnTroopCreated;
            GameEvent.OnTroopDestroyed += OnTroopDestroyed;

            // —— 时间推进 ——
            // 注意 DateReach 挂的是 OnDayUpdate（每天一次）。看起来高频，但 EvaluateKind
            // 的第一件事就是取 Kind 索引，索引为空时立刻返回，几乎没有开销；
            // 只有真的写了 DateReach 事件时才会进入条件求值。
            GameEvent.OnDayUpdate += OnDayUpdate;
            GameEvent.OnMonthStart += OnMonthStart;
            GameEvent.OnSeasonUpdate += OnSeasonUpdate;
            GameEvent.OnYearUpdate += OnYearUpdate;
            GameEvent.OnTurnEnd += OnTurnEnd;

            // —— 单挑 / 舌战结束 ——
            GameEvent.OnDuelEnd += OnDuelEnd;
            GameEvent.OnDebateEnd += OnDebateEnd;

            // —— 外交 ——
            GameEvent.OnDiplomacyAlliance += OnDiplomacyAlliance;
            GameEvent.OnDiplomacyTruce += OnDiplomacyTruce;
            GameEvent.OnDiplomacyDeclareWar += OnDiplomacyDeclareWar;
            GameEvent.OnDiplomacySendGift += OnDiplomacySendGift;
            GameEvent.OnDiplomacyMarriage += OnDiplomacyMarriage;

            // ================================================================
            // 第二批订阅（对应 EventTriggerKind 31~67）。
            //
            // 【为什么整批放在 Init 里】GameEvent 的订阅属于"应用级订阅"，
            // 由 GameEventBaseline 兜底、终身不解绑（见类注释），因此这里只增不减。
            // 新增触发时机时，只需要在此加一行订阅 + 下方区域加一个三行回调。
            // ================================================================

            // —— 回合节奏（势力 / 城市 / 部队） ——
            GameEvent.OnForceTurnEnd += OnForceTurnEnd;
            GameEvent.OnPlayerEndTurn += OnPlayerEndTurn;
            GameEvent.OnCityTurnStart += OnCityTurnStart;
            GameEvent.OnCityMonthStart += OnCityMonthStart;
            GameEvent.OnCitySeasonStart += OnCitySeasonStart;
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
            GameEvent.OnTroopActionOver += OnTroopActionOver;
            GameEvent.OnTroopClear += OnTroopClear;

            // —— 军团 ——
            GameEvent.OnCorpsCreate += OnCorpsCreate;
            GameEvent.OnCorpsDelete += OnCorpsDelete;

            // —— 势力成长 ——
            GameEvent.OnForceResearchComplete += OnForceResearchComplete;
            GameEvent.OnForceChangeCounsellor += OnForceChangeCounsellor;

            // —— 建筑（补齐：BuildingComplete 早已在枚举里定义，此前一直漏订阅，属于死枚举） ——
            GameEvent.OnBuildingComplete += OnBuildingComplete;
            GameEvent.OnBuildingUpgradeComplete += OnBuildingUpgradeComplete;

            // —— 武将成长 / 人事 ——
            GameEvent.OnPersonLevelUp += OnPersonLevelUp;
            GameEvent.OnPersonInjury += OnPersonInjury;
            GameEvent.OnPersonRelease += OnPersonRelease;
            GameEvent.OnPersonChangCurrentCity += OnPersonChangCurrentCity;
            GameEvent.OnPersonUpgradeOfficial += OnPersonUpgradeOfficial;

            // —— 战场 ——
            GameEvent.OnTroopChangeForce += OnTroopChangeForce;
            GameEvent.OnDiscoverEnemyTroop += OnDiscoverEnemyTroop;
            GameEvent.OnFireAdd += OnFireAdd;
            GameEvent.OnSkillActionEnd += OnSkillActionEnd;
            GameEvent.OnDuelStart += OnDuelStart;
            GameEvent.OnDuelChallengeRequest += OnDuelChallengeRequest;
            GameEvent.OnDebateStart += OnDebateStart;
            GameEvent.OnDebateChallengeRequest += OnDebateChallengeRequest;

            // 单挑结算：枚举注释写的是 OnDuelFinished，此前只订阅了 OnDuelEnd。
            // 两个 hook 的时序不同（一个是"演出结束"，一个是"结算完成"），
            // 因此两个都订上：先到的那个赢，后到的会被 EvaluateKind 的 IsPlaying 挡掉，
            // 不会重复播放；若前一个条件不满足，后一个仍有机会，避免漏触发。
            GameEvent.OnDuelFinished += OnDuelFinished;

            // —— 城市工作 / 收获 ——
            GameEvent.OnCityJobResult += OnCityJobResult;
            GameEvent.OnCityJobCounterResult += OnCityJobCounterResult;
            GameEvent.OnCityGainGoldHarvest += OnCityGainGoldHarvest;
            GameEvent.OnCityGainFoodHarvest += OnCityGainFoodHarvest;

            // —— 外交细分 ——
            GameEvent.OnDiplomacyBreakAlliance += OnDiplomacyBreakAlliance;
            GameEvent.OnDiplomacyAllianceRequest += OnDiplomacyAllianceRequest;
            GameEvent.OnDiplomacyTruceRequest += OnDiplomacyTruceRequest;
            GameEvent.OnDiplomacyRequestTechnique += OnDiplomacyRequestTechnique;
            GameEvent.OnDiplomacyRequestTroops += OnDiplomacyRequestTroops;
            GameEvent.OnDiplomacyTrade += OnDiplomacyTrade;
            GameEvent.OnDiplomacyRansom += OnDiplomacyRansom;

            Log.Info("剧本事件系统初始化完成");
        }

        /// <summary>
        /// 清理运行期数据（不解除 GameEvent 订阅，理由见类注释）。
        /// 调用点：ScenarioLifecycle.BeginShutdown() 的 5.5 步。
        /// </summary>
        public override void Clear()
        {
            evaluatedThisTurn = 0;
            firedThisTurn = 0;
            runningEventId = 0;
            scenarioStartYear = 0;
            sortedBuffer.Clear();
            ClearDeferredTriggers();
            lastEnabledState = true;    // 换局后重新按"启用"起算，开关变化时才会再打一次日志

            // 正在播放的事件必须一并收尾：档已经没了，它的对话框/选择肢回调都失效了
            EventRunner runner = EventRunner.Instance;
            if (runner != null) runner.Clear();

            // 定时队列挂在 Scenario 上，剧本对象被替换时自然失效；
            // 但如果同一个 Scenario 被反复读档复用，这里要显式清掉，避免"旧待办在新局里冒出来"。
            Scenario scenario = Scenario.Cur;
            if (scenario != null && scenario.ScheduledEvents != null)
            {
                int count = scenario.ScheduledEvents.Count;
                scenario.ScheduledEvents.Clear();
                if (count > 0)
                    Log.Info($"剧本事件系统清理：丢弃 {count} 项定时待办");
            }

            Log.Info("剧本事件系统已清理");
        }

        /// <summary>清空暂缓队列（换局 / 读档时用；注意**不要**在每回合清，那正是要重试它们的时候）</summary>
        void ClearDeferredTriggers()
        {
            deferredTriggers.Clear();
            deferBuffer.Clear();
            drainingDeferred = false;
            currentJobResult = int.MinValue;

            // 换局 / 读档时统计也要归零，否则上一局的丢弃数会算到新局头上
            deferredAddCount = 0;
            deferredDropCount = 0;
        }

        void OnScenarioStart(Scenario scenario)
        {
            ResetTurnCounters();
            ClearDeferredTriggers();
            CaptureStartYear(scenario);

            // 评估"剧本开始"类事件（桃园结义等开场剧情）。
            // 必须在 CaptureStartYear 之后：ScenarioFilter 的 YearRange 过滤要用开局年份。
            EvaluateKind(EventTriggerKind.ScenarioStart, null);
        }

        void OnGameLoad(Scenario scenario)
        {
            ResetTurnCounters();
            ClearDeferredTriggers();

            // 读档后不重新捕获开局年份：ScenarioInfo.year 已经是"当前年份"，
            // 用它当开局年份会让"200 年开局"的剧本在游戏进行到 210 年后误判。
            // 因此从存档里的既有状态恢复——若没存过就保持 0（不过滤）。
            if (scenarioStartYear <= 0) CaptureStartYear(scenario);
        }

        /// <summary>复位每回合计数器</summary>
        void ResetTurnCounters()
        {
            evaluatedThisTurn = 0;
            firedThisTurn = 0;
            runningEventId = 0;
        }

        /// <summary>捕获剧本开局年份（只在剧本开始时调一次）</summary>
        /// <param name="scenario">剧本</param>
        void CaptureStartYear(Scenario scenario)
        {
            scenarioStartYear = scenario != null && scenario.Info != null ? scenario.Info.year : 0;
            Log.Info($"剧本事件系统：开局年份 {scenarioStartYear}");
        }

        #endregion

        #region GameEvent hook 入口

        void OnTurnStart(Scenario scenario)
        {
            // 先把上一回合的暂缓 / 丢弃情况汇总成一条日志（每回合最多一条），再复位计数。
            // 必须排在 DrainDeferred 之前：那份统计属于"上一回合"，本轮重试的结果要算到下一回合。
            ReportDeferredStats();
            ResetTurnCounters();

            // 总开关关掉时：不推进定时队列、不重试暂缓项。
            // 定时队列里的待办项**保留**（不丢），重新打开后按回合逐条补播。
            //
            // 但**不能在这里早返回** —— 第三步的 EvaluateKind 会按事件自身的
            // AlwaysEnabled 过滤，让"固定游戏事件"（统一天下等结局事件）在开关关闭后照常触发。
            // 早返回会把这些事件一起静默掉，恰好抹掉了这条分类存在的意义。
            if (Enabled)
            {
                // 第一步：推进定时队列（跨回合事件链在此接上）
                ProcessScheduledQueue(scenario);

                // 第二步：重试上一回合因"演出占用"而暂缓的触发。
                // 放在这里是因为回合开始时上一回合的所有演出（含 AI 行军）都已结束，是天然的安静点。
                DrainDeferred();
            }

            // 第三步：评估本回合的常驻时机事件（常规事件由 EvaluateKind 内部按开关过滤）
            EvaluateKind(EventTriggerKind.TurnStart, null);
            EvaluateKind(EventTriggerKind.DateReach, null);
        }

        void OnForceTurnStart(Force force, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.ForceTurnStart, force);
        }

        void OnCityFall(City city, Force force, Troop troop)
        {
            EvaluateKind(EventTriggerKind.CityFall, city);
        }

        void OnPersonActionOver(Person person)
        {
            EvaluateKind(EventTriggerKind.JobFinished, person);
        }

        void OnTroopEnterCell(Troop troop, Cell from, Cell to)
        {
            EvaluateKind(EventTriggerKind.TroopEnterCell, troop);
        }

        void OnPersonCaptured(Person person, Troop captor)
        {
            EvaluateKind(EventTriggerKind.PersonCaptured, person);
        }

        /// <summary>
        /// 探索人才结算完成（旧演出结束后才抛，带结果码）。
        /// </summary>
        /// <param name="person">执行者</param>
        /// <param name="city">所在城市</param>
        /// <param name="result">结果码：0=发现人才 / &gt;0=发现资金 / -1=一无所获</param>
        /// <param name="found">发现的武将（结果码为 0 时有效）</param>
        void OnCityJobSearchingSettled(Person person, City city, int result, Person found)
        {
            currentJobResult = result;
            try
            {
                EvaluateKind(EventTriggerKind.JobSettled, person);
            }
            finally
            {
                currentJobResult = int.MinValue;
            }
        }

        void OnPersonExecute(Person person, Force force)
        {
            EvaluateKind(EventTriggerKind.PersonExecute, person);
        }

        void OnPersonEscape(Person person, SangoObject captor)
        {
            EvaluateKind(EventTriggerKind.PersonEscape, person);
        }

        void OnPersonChangeBelongCity(Person person, City from, City to)
        {
            EvaluateKind(EventTriggerKind.PersonJoinForce, person);

            // 同一个 hook 兼作"武将登场"：登场的定义是"武将首次有了所在城市"，
            // 在此之前 from 为 null（未登场 / 在野且无归属）。
            // 引擎没有独立的 OnPersonAppear，这里是唯一能观察到这次状态迁移的公共信号，
            // 因此靠 from == null 做一次判别 —— 早先 PersonAppear 枚举一直没人调用，属于死枚举。
            if (from == null && to != null)
                EvaluateKind(EventTriggerKind.PersonAppear, person);
        }

        void OnForceFall(Force force, City city, Troop troop)
        {
            EvaluateKind(EventTriggerKind.ForceFall, force);
        }

        /// <summary>
        /// 统一天下（天下一统）。
        ///
        /// 【为什么走 urgent 模式，绕过让路闸门】
        /// 抛出这一 hook 的调用点（City 陷落处理的 allInOne 分支）紧接着就调了
        /// <c>Scenario.OnGamePause()</c>，主循环（<c>Scenario.Run</c> 的后台线程）当场停住。
        /// 而常规的让路策略是"暂缓到下一回合开始重试"——那个回合永远不会到来，
        /// 结局事件就会被永久吞掉。因此这里跳过让路闸门，立刻试播。
        ///
        /// 【安全边界】<see cref="EvaluateKind"/> 仍会挡住"IsPlaying"，即已有事件在播时不抢；
        /// 而此刻玩家刚攻陷最后一座城，对话框与旧演出队列都是干净的，抢窗口的风险极低。
        /// </summary>
        /// <param name="force">统一天下的势力（通常即玩家势力）</param>
        void OnWorldUnified(Force force)
        {
            Log.Info($"剧本事件：检测到【{force?.Name}】统一天下，立即尝试播放结局事件");

            // urgent = true：跳过"让路暂缓"，否则主循环已停，暂缓项永远等不到下一回合
            EvaluateKind(EventTriggerKind.WorldUnified, force, true);
        }

        void OnTroopCreated(Troop troop, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.TroopCreated, troop);
        }

        void OnTroopDestroyed(Troop troop, SangoObject killer, int reason, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.TroopDestroyed, troop);
        }

        void OnTroopLeaveCell(Troop troop, Cell from, Cell to)
        {
            EvaluateKind(EventTriggerKind.TroopLeaveCell, troop);
        }

        void OnDayUpdate(Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.DateReach, null);
        }

        void OnMonthStart(Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.MonthStart, null);
        }

        void OnSeasonUpdate(Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.SeasonStart, null);
        }

        void OnYearUpdate(Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.YearStart, null);
        }

        void OnTurnEnd(Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.TurnEnd, null);
        }

        // 单挑 / 舌战的结论实体类型藏在 Duel / Debate 子系统里，
        // 事件侧目前只需要"知道发生了"，参与方由事件自己的槽位声明决定，因此不注入实体。
        void OnDuelEnd(Sango.Core.Duel.Duel duel)
        {
            EvaluateKind(EventTriggerKind.DuelFinished, null);
        }

        void OnDebateEnd(Sango.Core.Debate.Debate debate)
        {
            EvaluateKind(EventTriggerKind.DebateEnd, null);
        }

        void OnDiplomacyAlliance(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyAlliance, a);
        }

        void OnDiplomacyTruce(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyTruce, a);
        }

        void OnDiplomacyDeclareWar(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyDeclareWar, a);
        }

        void OnDiplomacySendGift(Force a, Force b, int amount, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacySendGift, a);
        }

        void OnDiplomacyMarriage(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyMarriage, a);
        }

        // ====================================================================
        // 第二批 hook 入口（EventTriggerKind 31~67）
        //
        // 【统一写法】每个回调只做一件事：把 hook 里"最有代表性的那个对象"
        // 作为事件实体交给 EvaluateKind，剩下的参与方由事件自己的槽位声明去解析。
        // 为什么不做多实体注入：现有的 EventSlotSource.EventEntity 是单实体模型，
        // 多实体需要改 EventContext / EventSlotResolver / 槽位命名规则三处，
        // 属于独立一轮改造；本批先保证"事件能按时机触发"，
        // 需要第二个参与方时用 ForceSlot / TargetForce 等固定槽位表达即可。
        // ====================================================================

        void OnForceTurnEnd(Force force, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.ForceTurnEnd, force);
        }

        void OnPlayerEndTurn(Force force, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.PlayerEndTurn, force);
        }

        void OnCityTurnStart(City city, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.CityTurnStart, city);
        }

        void OnCityMonthStart(City city, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.CityMonthStart, city);
        }

        void OnCitySeasonStart(City city, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.CitySeasonStart, city);
        }

        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.TroopTurnStart, troop);
        }

        /// <summary>部队行动结束：注入部队，供"行军途中"类事件判断</summary>
        /// <param name="troop">行动结束的部队</param>
        void OnTroopActionOver(Troop troop)
        {
            EvaluateKind(EventTriggerKind.TroopActionOver, troop);
        }

        void OnTroopClear(Troop troop, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.TroopClear, troop);
        }

        /// <summary>
        /// 部队易主：注入部队本体（新旧势力在 hook 参数里，事件侧用条件读当前归属即可，
        /// 因为调用此回调时归属已经改完，oldForce 参数只对"变化量"有意义）。
        /// </summary>
        /// <param name="troop">易主的部队</param>
        /// <param name="oldForce">原势力</param>
        /// <param name="newForce">新势力</param>
        void OnTroopChangeForce(Troop troop, Force oldForce, Force newForce)
        {
            EvaluateKind(EventTriggerKind.TroopChangeForce, troop);
        }

        void OnCorpsCreate(Corps corps, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.CorpsCreated, corps);
        }

        void OnCorpsDelete(Corps corps, Scenario scenario)
        {
            EvaluateKind(EventTriggerKind.CorpsDeleted, corps);
        }

        void OnForceResearchComplete(Force force, Technique technique)
        {
            EvaluateKind(EventTriggerKind.ForceResearchComplete, force);
        }

        void OnForceChangeCounsellor(Force force, Person person)
        {
            EvaluateKind(EventTriggerKind.ForceChangeCounsellor, force);
        }

        /// <summary>建筑完工：注入建筑（合流时 building 必然非空）</summary>
        /// <param name="building">完工的建筑</param>
        /// <param name="workers">参与建造的武将</param>
        void OnBuildingComplete(Building building, SangoObjectList<Person> workers)
        {
            EvaluateKind(EventTriggerKind.BuildingComplete, building);
        }

        void OnBuildingUpgradeComplete(Building building, SangoObjectList<Person> workers)
        {
            EvaluateKind(EventTriggerKind.BuildingUpgradeComplete, building);
        }

        void OnPersonLevelUp(Person person)
        {
            EvaluateKind(EventTriggerKind.PersonLevelUp, person);
        }

        void OnPersonInjury(Person person, Troop troop)
        {
            EvaluateKind(EventTriggerKind.PersonInjury, person);
        }

        void OnPersonRelease(Person person, Force force)
        {
            EvaluateKind(EventTriggerKind.PersonRelease, person);
        }

        void OnPersonChangCurrentCity(Person person, City from, City to)
        {
            EvaluateKind(EventTriggerKind.PersonChangeCurrentCity, person);
        }

        void OnPersonUpgradeOfficial(Person person, Official official)
        {
            EvaluateKind(EventTriggerKind.PersonUpgradeOfficial, person);
        }

        void OnDiscoverEnemyTroop(Force force, City city, Troop troop, Person person)
        {
            EvaluateKind(EventTriggerKind.DiscoverEnemyTroop, force);
        }

        /// <summary>
        /// 火势蔓延：hook 参数是战法实例与 Fire 对象，都不是 SangoObject，
        /// 因此不注入实体，参与方由事件槽位自己声明。
        /// </summary>
        /// <param name="skill">引发火势的战法</param>
        /// <param name="fire">火对象</param>
        void OnFireAdd(SkillInstance skill, Fire fire)
        {
            EvaluateKind(EventTriggerKind.FireAdd, null);
        }

        void OnSkillActionEnd(SkillInstance skill, Cell cell, Troop troop, BuildingBase building)
        {
            EvaluateKind(EventTriggerKind.SkillActionEnd, troop);
        }

        void OnDuelStart(Troop a, Troop b)
        {
            EvaluateKind(EventTriggerKind.DuelStart, a);
        }

        void OnDuelChallengeRequest(Troop a, Troop b)
        {
            EvaluateKind(EventTriggerKind.DuelChallenge, a);
        }

        void OnDebateStart(Person a, Person b)
        {
            EvaluateKind(EventTriggerKind.DebateStart, a);
        }

        void OnDebateChallengeRequest(Person a, Person b)
        {
            EvaluateKind(EventTriggerKind.DebateChallenge, a);
        }

        /// <summary>
        /// 单挑结算完成（OnDuelFinished）。
        /// 与 OnDuelEnd 的区别见 Init 里的订阅说明：两个都订，靠 IsPlaying 去重。
        /// </summary>
        /// <param name="duel">单挑对象</param>
        void OnDuelFinished(Sango.Core.Duel.Duel duel)
        {
            EvaluateKind(EventTriggerKind.DuelFinished, null);
        }

        void OnCityJobResult(City city, int result, Person[] persons, Sango.Core.Tools.OverrideData<int> data)
        {
            EvaluateKind(EventTriggerKind.CityJobResult, city);
        }

        void OnCityJobCounterResult(City city, int result, Person[] persons, Sango.Core.Tools.OverrideData<int> data)
        {
            EvaluateKind(EventTriggerKind.CityJobCounterResult, city);
        }

        void OnCityGainGoldHarvest(City city, Sango.Core.Tools.OverrideData<int> data)
        {
            EvaluateKind(EventTriggerKind.CityGainGoldHarvest, city);
        }

        void OnCityGainFoodHarvest(City city, Sango.Core.Tools.OverrideData<int> data)
        {
            EvaluateKind(EventTriggerKind.CityGainFoodHarvest, city);
        }

        void OnDiplomacyBreakAlliance(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyBreakAlliance, a);
        }

        void OnDiplomacyAllianceRequest(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyAllianceRequest, a);
        }

        void OnDiplomacyTruceRequest(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyTruceRequest, a);
        }

        void OnDiplomacyRequestTechnique(Force a, Force b, int amount, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyRequestTechnique, a);
        }

        void OnDiplomacyRequestTroops(Force a, Force b, int amount, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyRequestTroops, a);
        }

        void OnDiplomacyTrade(Force a, Force b, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyTrade, a);
        }

        void OnDiplomacyRansom(Force a, Force b, int amount, bool success)
        {
            EvaluateKind(EventTriggerKind.DiplomacyRansom, a);
        }

        #endregion

        #region 候选评估与播放

        /// <summary>
        /// 评估某个触发时机的全部候选事件，命中即播放。
        ///
        /// 【一次只播一个】hook 触发时若同时命中多个事件，只播优先级最高的那个，
        /// 其余留到下个回合。原因是演出独占输入与对话框，并行播放会互相覆盖窗口；
        /// 且一回合连播 5 个事件对玩家是灾难。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <param name="entity">hook 注入的事件实体（可为 null）</param>
        /// <returns>是否成功播放了一个事件</returns>
        public bool EvaluateKind(EventTriggerKind kind, SangoObject entity)
        {
            return EvaluateKind(kind, entity, false);
        }

        /// <summary>
        /// 多实体 + 格子注入版本（供单挑双方、外交双方、建造选址这类 hook 使用）。
        ///
        /// 【约定】entities 的第 0 个等价于旧的 entity；
        /// 事件里的 <see cref="EventSlotSource.EventEntity"/> 取第 1 个，
        /// EventEntity2/3/4 依次取后续元素；cell 供 Type = Cell 的槽位使用。
        /// 数组里允许出现 null（hook 的某些参数天然可空），槽位解析会按"取不到"处理。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <param name="entities">hook 注入的实体数组（可含 null）</param>
        /// <param name="cell">hook 注入的格子（可为 null）</param>
        /// <returns>是否成功播放了一个事件</returns>
        public bool EvaluateKind(EventTriggerKind kind, SangoObject[] entities, Cell cell)
        {
            return EvaluateKindInternal(kind, entities, cell, false);
        }

        /// <summary>
        /// 评估某个触发时机的全部候选事件，命中即播放（带紧急模式）。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <param name="entity">hook 注入的事件实体（可为 null）</param>
        /// <param name="urgent">true = 跳过"让路暂缓"，立刻试播。
        /// 仅用于"主循环马上就要停住、暂缓等于永久丢失"的场合（统一天下）。</param>
        /// <returns>是否成功播放了一个事件</returns>
        public bool EvaluateKind(EventTriggerKind kind, SangoObject entity, bool urgent)
        {
            SangoObject[] entities = entity != null ? new SangoObject[] { entity } : null;
            return EvaluateKindInternal(kind, entities, null, urgent);
        }

        /// <summary>
        /// 评估的统一实现（多实体 + 格子）。
        ///
        /// 【为什么单开一个 Internal】旧的单实体入口有两个（带 / 不带 urgent），
        /// 若各自复制一份实现，日后加一道闸门就得改两处、必漏一处。
        /// 因此全部收敛到这里，旧入口只做参数打包。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <param name="entities">注入的实体数组（可为 null）</param>
        /// <param name="cell">注入的格子（可为 null）</param>
        /// <param name="urgent">true = 跳过让路暂缓</param>
        /// <returns>是否成功播放了一个事件</returns>
        bool EvaluateKindInternal(EventTriggerKind kind, SangoObject[] entities, Cell cell, bool urgent)
        {
            // 作用域过滤 / 工作类型过滤仍按"第一个实体"判定，与旧行为一致
            SangoObject entity = (entities != null && entities.Length > 0) ? entities[0] : null;

            if (!database.Loaded || IsPlaying) return false;

            // 总开关：所有 hook 驱动的自动触发都从这里进来，闸门放这一处即可覆盖全部触发时机。
            //
            // 但**不能直接 return** —— 关掉后仍要放行"固定游戏事件"
            // （EventDefinition.AlwaysEnabled，如统一天下结局）。
            // 因此这里只取一次开关值，真正的过滤放到候选循环里按事件自己的标记做。
            bool switchOn = Enabled;

            // 让路：旧体系（RenderEvent）或对话框正在占用时，本轮不启动新演出。
            //
            // 两套体系共用 GameDialog 的同一个对话队列，同时投递会让窗口互相覆盖、
            // GameController.Enabled 被反复开关，退 Play 模式时队列积压还会触发窗口泄漏。
            // 最典型的撞车：City.DoJobSearching 在旧演出执行期间抛出 OnPersonActionOver，
            // 事件若在此刻启动，就会和旧演出抢对话框。
            //
            // 刻意**不丢弃而是暂缓** —— 与"一次 hook 只播一个事件、其余留到下回合"是同一策略。
            //
            // urgent 时**不暂缓**：调用方（统一天下）抛出 hook 之后主循环就停了，
            // 暂缓到"下一回合"等于这件事永远不会发生。此时宁可冒抢窗口的风险也要立刻试播。
            if (!urgent && IsBlockedByOtherPresentation())
            {
                Defer(kind, entities, cell, currentJobResult);
                return false;
            }

            if (evaluatedThisTurn >= MaxEventEvalPerTurn)
            {
                Log.Info($"剧本事件：本回合求值预算已用尽（{MaxEventEvalPerTurn}），剩余候选延后到下回合");
                return false;
            }

            List<EventDefinition> candidates = database.GetByKind(kind);
            if (candidates.Count == 0) return false;

            EventDatabase.SortByPriority(candidates, sortedBuffer);

            for (int i = 0; i < sortedBuffer.Count; i++)
            {
                if (evaluatedThisTurn >= MaxEventEvalPerTurn) return false;

                // 总开关关闭时跳过常规剧本事件：连条件树都不求值（"关掉就等于零开销"）。
                // 只留下 AlwaysEnabled 的固定游戏事件继续往下过闸。
                EventDefinition candidate = sortedBuffer[i];
                if (!switchOn && (candidate == null || !candidate.AlwaysEnabled)) continue;

                evaluatedThisTurn++;
                if (TryFireEvent(candidate, kind, entities, cell)) return true;
            }

            return false;
        }

        /// <summary>
        /// 是否被"别人的演出"占着，轮不到我们开窗口。
        ///
        /// 两个判据：
        ///   1. <c>GameDialog.HasCurrent</c> —— 已经有对话窗口在占位。这是**真正的冲突面**：
        ///      两套体系最终都落到同一个 GameDialog 队列上。
        ///   2. <c>RenderEvent.HasPendingAfterCurrent</c> —— 旧体系当前这项演完之后**后面还有戏**。
        ///      只看"旧体系有没有在播"是不行的：移动演出是"路径每格排一个事件"，
        ///      长距离行军期间它一直非空闲，会把所有事件都推到下一回合。
        ///      而当前项正在播本身并不冲突（它没开对话框），冲突的是接下来还要开。
        /// </summary>
        /// <returns>true = 需要让路</returns>
        bool IsBlockedByOtherPresentation()
        {
            GameDialog dialog = GameDialog.Instance;
            if (dialog != null && dialog.HasCurrent) return true;

            Sango.Render.RenderEvent renderEvent = Sango.Render.RenderEvent.Instance;
            if (renderEvent != null && renderEvent.HasPendingAfterCurrent) return true;

            return false;
        }

        /// <summary>
        /// 把一次触发放进暂缓队列，等下一回合开始重试。
        /// </summary>
        /// <param name="kind">触发时机</param>
        /// <param name="entities">hook 注入的实体数组（可为 null）</param>
        /// <param name="cell">hook 注入的格子（可为 null）</param>
        /// <param name="jobResult">工作结果码（非 JobSettled 时传 int.MinValue）</param>
        void Defer(EventTriggerKind kind, SangoObject[] entities, Cell cell, int jobResult)
        {
            if (deferredTriggers.Count >= MaxDeferredTriggers)
            {
                // 只计数，不在这里打日志：TroopLeaveCell 每移一格就触发一次，
                // 队列一满就会逐条 Warning 刷屏，真正的线索反而被淹没。
                // 汇总出口见 ReportDeferredStats（每回合最多一条）。
                deferredDropCount++;
                return;
            }

            deferredTriggers.Add(new DeferredTrigger { kind = kind, entities = entities, cell = cell, jobResult = jobResult });
            deferredAddCount++;
        }

        /// <summary>
        /// 重试暂缓队列。在 OnTurnStart 调用 —— 那是一个天然的"安静点"：
        /// 上一回合的所有演出（含 AI 行军）都已经结束。
        /// </summary>
        void DrainDeferred()
        {
            if (drainingDeferred || deferredTriggers.Count == 0) return;

            drainingDeferred = true;
            try
            {
                // 先做快照再清空：重试过程中若又产生暂缓项，它们是"新的"，
                // 不能被本轮循环吃掉，留到下一回合。
                deferBuffer.Clear();
                deferBuffer.AddRange(deferredTriggers);
                deferredTriggers.Clear();

                for (int i = 0; i < deferBuffer.Count; i++)
                {
                    if (IsPlaying)
                    {
                        Defer(deferBuffer[i].kind, deferBuffer[i].entities, deferBuffer[i].cell, deferBuffer[i].jobResult);
                        continue;
                    }

                    if (IsBlockedByOtherPresentation())
                    {
                        // 仍然被占着：整批留在暂缓里，本轮不再尝试
                        for (int j = i; j < deferBuffer.Count; j++)
                            Defer(deferBuffer[j].kind, deferBuffer[j].entities, deferBuffer[j].cell, deferBuffer[j].jobResult);
                        break;
                    }

                    currentJobResult = deferBuffer[i].jobResult;
                    EvaluateKindInternal(deferBuffer[i].kind, deferBuffer[i].entities, deferBuffer[i].cell, false);
                }
            }
            finally
            {
                currentJobResult = int.MinValue;
                drainingDeferred = false;
            }
        }

        /// <summary>
        /// 汇总上一回合的暂缓 / 丢弃情况（**每回合最多一条日志**）。
        ///
        /// 为什么汇总而不是逐条：
        ///   · TroopLeaveCell / TroopEnterCell 是**按格**触发的，AI 大军行军一回合几十上百次；
        ///   · 旧体系的移动演出又是"路径每格排一个事件"，一旦演出长时间不结束
        ///     （<see cref="IsBlockedByOtherPresentation"/> 持续为 true），暂缓队列会瞬间填满；
        ///   · 逐条 Warning 会把控制台刷爆，"为什么被挡"这条关键信息反而看不见。
        /// 因此 Defer 只累加计数，由本方法在回合开始时统一输出一次。
        /// </summary>
        void ReportDeferredStats()
        {
            if (deferredDropCount > 0)
            {
                Log.Warning($"剧本事件：上回合暂缓队列已满（上限 {MaxDeferredTriggers} 项），" +
                            $"新增 {deferredAddCount} 项、丢弃 {deferredDropCount} 次触发，" +
                            $"当前队列 {deferredTriggers.Count} 项。" +
                            "持续出现说明旧体系的演出长时间不结束（移动演出按格排队）。");
            }
            else if (deferredAddCount > 0)
            {
                Log.Info($"剧本事件：上回合暂缓 {deferredAddCount} 项，当前队列 {deferredTriggers.Count} 项");
            }

            deferredAddCount = 0;
            deferredDropCount = 0;
        }

        /// <summary>
        /// 尝试触发一个事件：逐层过闸（启用 → 触发次数 → 冷却 → 年份 → 密度 → 剧本过滤
        /// → 槽位绑定 → 条件树 → 概率），全通过才播。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="kind">触发时机</param>
        /// <param name="entity">hook 注入的实体</param>
        /// <returns>是否已开始播放</returns>
        public bool TryFireEvent(EventDefinition def, EventTriggerKind kind, SangoObject entity)
        {
            SangoObject[] entities = entity != null ? new SangoObject[] { entity } : null;
            return TryFireEvent(def, kind, entities, null);
        }

        /// <summary>
        /// 尝试触发一个事件（多实体 + 格子版本）。
        /// 具体的闸门顺序见旧重载上方注释，两者共用同一份实现。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="kind">触发时机</param>
        /// <param name="entities">注入的实体数组（可为 null）</param>
        /// <param name="cell">注入的格子（可为 null）</param>
        /// <returns>是否已开始播放</returns>
        public bool TryFireEvent(EventDefinition def, EventTriggerKind kind, SangoObject[] entities, Cell cell)
        {
            SangoObject entity = (entities != null && entities.Length > 0) ? entities[0] : null;

            if (def == null) return false;

            // 闸门未通过的统一出口：记录 LastFailReason；打开了 DebugLog 的事件额外打一行"卡在哪一步"。
            // 用局部函数是为了不把十几处 return 各写一遍日志，也避免有人改漏。
            bool Fail(EventRunState st, string step, string reason)
            {
                if (st != null) st.LastFailReason = reason;
                if (def.DebugLog) Log.Info($"[事件调试] {def} 未触发 @{step}：{reason}");
                return false;
            }

            if (!def.Enabled)
            {
                if (def.DebugLog) Log.Info($"[事件调试] {def} 未触发 @事件开关：Enabled = false");
                return false;
            }

            // 总开关。EvaluateKind 已挡过一道，这里再挡是为了覆盖"绕过 EvaluateKind 直接调本方法"
            // 的调用方（例如以后调试面板想单独重试某个事件）。
            //
            // 固定游戏事件（AlwaysEnabled）例外：它们承担结局 / 流程完整性，不受玩家开关影响。
            // 判断必须放在 def 判空之后 —— 先取 def.AlwaysEnabled 就是空引用。
            if (!Enabled && !def.AlwaysEnabled)
            {
                if (def.DebugLog) Log.Info($"[事件调试] {def} 未触发 @总开关：剧本事件总开关已关闭");
                return false;
            }

            if (IsPlaying)
            {
                if (def.DebugLog) Log.Info($"[事件调试] {def} 本次跳过：已有别的事件在播放");
                return false;
            }

            EventTriggerDef trigger = def.Trigger;
            if (trigger == null) return false;

            EventRunState state = GetState(def.Id, true);
            if (state == null) return false;

            // ① 触发次数上限（Once / MaxFireCount）
            if (trigger.IsExhausted(state.FireCount))
                return Fail(state, "①触发次数", "触发次数已用尽");

            // ② 冷却
            int curTurn = Scenario.Cur != null && Scenario.Cur.Info != null ? Scenario.Cur.Info.turnCount : 0;
            if (trigger.CooldownTurns > 0 && state.LastFireTurn >= 0
                && curTurn - state.LastFireTurn < trigger.CooldownTurns)
            {
                return Fail(state, "②冷却",
                    $"冷却中（还需 {trigger.CooldownTurns - (curTurn - state.LastFireTurn)} 回合）");
            }

            // ②-2 工作类型 / 结果过滤。
            // 调试面板的强制触发走 FireEventNow（它不经过本方法），因此不需要额外的跳过开关。
            if (!MatchJobFilter(def, entity, out string jobReason))
                return Fail(state, "②工作类型/结果过滤", jobReason);

            // ③ 年份索引（二级裁剪：把大部分历史事件在本年份直接排除）
            int curYear = Scenario.Cur != null && Scenario.Cur.Info != null ? Scenario.Cur.Info.year : 0;
            if (!trigger.MatchYear(curYear))
                return Fail(state, "③年份索引", $"当前年份 {curYear} 不在 {trigger.EarliestYear}~{trigger.LatestYear} 区间");

            // ④ 作用域
            if (!MatchScope(def, entity))
                return Fail(state, "④作用域", $"触发作用域不匹配（要求 {def.Trigger.Scope}）");

            // ⑤ 剧本绑定过滤
            int scenarioId = Scenario.Cur != null && Scenario.Cur.Info != null ? Scenario.Cur.Info.id : 0;
            if (!EventDatabase.MatchScenarioFilter(def, scenarioStartYear, scenarioId))
                return Fail(state, "⑤剧本过滤", "当前剧本不适用该事件");

            // ⑥ 密度控制（Critical 级不受限）
            if (def.Level != EventLevel.Critical && firedThisTurn >= MaxEventFirePerTurn)
                return Fail(state, "⑥密度控制", $"本回合事件密度已达上限（{MaxEventFirePerTurn}）");

            // ⑦ 槽位绑定（失败即放弃，且不改任何世界状态）
            EventContext context = new EventContext
            {
                definition = def,
                state = state,
                fireCount = state.FireCount + 1,
                fireKind = kind,
                eventEntity = entity,
                // 多实体与格子：单实体场景下 entities 只有一个元素，行为与改造前完全一致
                eventEntities = entities != null ? new List<SangoObject>(entities) : null,
                eventCell = cell,
            };

            if (!EventSlotResolver.ResolveAll(def, context))
            {
                LogSlotBindings(def, context);
                return Fail(state, "⑦槽位绑定", context.slotFailReason ?? "槽位绑定失败");
            }
            // 绑定成功也打一份，方便确认"条件里的 Slot 到底绑到了谁"
            LogSlotBindings(def, context);

            // ⑧ 条件树
            context.database.fireCount = context.fireCount;
            context.database.eventId = def.Id;

            // DebugLog 打开时收集逐条判定过程；此时求值不短路，能把所有不满足项一次列全
            List<string> condTrace = def.DebugLog ? new List<string>() : null;
            if (!EventConditionEvaluator.Evaluate(def.Conditions, context.database, out string reason, condTrace))
            {
                LogConditionTrace(def, condTrace, reason);
                return Fail(state, "⑧条件树", reason);
            }
            if (def.DebugLog) Log.Info($"[事件调试] {def} 条件树全部满足（检查了 {condTrace.Count} 个节点）");

            // ⑨ 触发概率（放在最后：先过廉价闸门再掷骰，避免白白消耗随机数）
            if (trigger.Chance < 100)
            {
                if (trigger.Chance <= 0 || !GameRandom.Chance(trigger.Chance))
                    return Fail(state, "⑨触发概率", $"触发概率未命中（{trigger.Chance}%）");
            }

            // ⑩ 延迟播放。前置条件**全部满足之后**才登记，因为 DelayTurns 的语义是
            // "条件满足了，但先憋 N 回合再演"（用于事件余波、伏笔回收）。
            //
            // 复用定时队列（ScheduledEventItems）而不是自己记一个倒计时：
            // 那样它会随存档持久化、跨读档不丢，也不必再写一套推进逻辑。
            if (trigger.DelayTurns > 0)
            {
                if (HasPendingDelay(def.Id))
                {
                    // 条件持续满足时每回合都会走到这里，必须靠这个标记防重复登记
                    state.LastFailReason = "已登记延迟播放，等待到达触发回合";
                    return false;
                }

                ScheduleDelayed(def, context, trigger.DelayTurns);
                state.LastFailReason = $"已登记延迟播放（{trigger.DelayTurns} 回合后）";
                return false;
            }

            state.LastFailReason = null;
            return PlayEvent(def, context, state);
        }

        #region 调试日志（事件 DebugLog）

        /// <summary>
        /// 把条件树的逐条判定过程打进日志。
        /// 只在事件打开了 DebugLog 时输出；trace 为空（例如条件树本身就是空）时静默。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="trace">逐条判定过程</param>
        /// <param name="failReason">整体结论</param>
        static void LogConditionTrace(EventDefinition def, List<string> trace, string failReason)
        {
            if (def == null || !def.DebugLog) return;

            if (trace == null || trace.Count == 0)
            {
                Log.Info($"[事件调试] {def} 条件树为空（或没有可追踪的节点），按恒成立处理");
                return;
            }

            Log.Info($"[事件调试] {def} 条件判定过程（共 {trace.Count} 行，√ 成立 / × 不成立）：");
            for (int i = 0; i < trace.Count; i++)
                Log.Info($"    {trace[i]}");

            if (!string.IsNullOrEmpty(failReason))
                Log.Info($"[事件调试] {def} 条件结论：{failReason}");
        }

        /// <summary>
        /// 把每个角色槽的绑定结果打进日志（绑到了谁 / 未绑定）。
        /// 条件里写错槽位 Key、或者人物已死亡导致绑定失败时，这是最快能看出来的地方。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="context">事件上下文</param>
        static void LogSlotBindings(EventDefinition def, EventContext context)
        {
            if (def == null || !def.DebugLog || def.Slots == null || def.Slots.Count == 0) return;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < def.Slots.Count; i++)
            {
                EventActorSlot slot = def.Slots[i];
                if (slot == null || string.IsNullOrEmpty(slot.Key)) continue;

                SangoObject obj = context != null ? context.GetSlot(slot.Key) : null;
                sb.Append("    ");
                sb.Append(slot.Key);
                sb.Append("（");
                sb.Append(slot.Type);
                sb.Append("）= ");
                sb.Append(obj != null ? obj.Name : "未绑定");
                if (slot.Optional) sb.Append(" [可选]");
                sb.Append('\n');
            }

            Log.Info($"[事件调试] {def} 槽位绑定：\n{sb}");
        }

        #endregion

        /// <summary>
        /// Trigger.DelayTurns 登记的待办用**负数** SourceEventId 做标记，
        /// 与事件里 Op:Schedule（正数）区分开 —— 两者语义不同（一个是"延迟自己"，一个是"安排别人"）。
        /// </summary>
        /// <param name="eventId">事件 Id</param>
        /// <returns>标记值</returns>
        static int DelaySourceMarker(int eventId) { return -eventId; }

        /// <summary>该事件是否已经登记了延迟播放（防止条件持续满足时反复登记）</summary>
        /// <param name="eventId">事件 Id</param>
        /// <returns>是否已有待办</returns>
        bool HasPendingDelay(int eventId)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.ScheduledEvents == null) return false;

            int marker = DelaySourceMarker(eventId);
            List<ScheduledEventItem> items = scenario.ScheduledEvents;
            for (int i = 0; i < items.Count; i++)
            {
                ScheduledEventItem item = items[i];
                if (item != null && item.TargetEventId == eventId && item.SourceEventId == marker)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 登记延迟播放。
        ///
        /// 【为什么 cancelJson 传 null，而不是把事件自己的条件树塞进去】
        /// 条件树里可能有依赖"事件上下文"的条件（FireCountCompare / VarCompare 要读
        /// database.fireCount 与 database.eventId），而取消条件的求值路径
        /// <c>EventConditionEvaluator.IsCancelConditionMet</c> **不会设置这两个字段**，
        /// 于是 fireCount 会取默认 1、eventId 取 0，算出来的结果可能与真实情况不符。
        ///
        /// 不传取消条件反而更准确：待办到期后由 <c>FireScheduled</c> 走**正常求值路径**
        /// （它会把 fireCount / eventId 都设好）复核一遍条件，不满足就不播。
        /// 语义上与"余波"的直觉一致：到时局势已变，就不演了。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="context">已解析好槽位的上下文</param>
        /// <param name="delayTurns">延迟回合数</param>
        void ScheduleDelayed(EventDefinition def, EventContext context, int delayTurns)
        {
            int chainId = def.Chain != null ? def.Chain.ChainId : 0;
            int stage = def.Chain != null ? def.Chain.Stage : 0;

            Schedule(def.Id, delayTurns, chainId, stage, context.boundSlots, null,
                DelaySourceMarker(def.Id));
        }

        /// <summary>
        /// 工作类型 / 结果过滤。
        ///
        /// 两条独立判据：
        ///   · <c>JobResults</c> —— 工作**结果码**，只有 JobSettled 时机带得出来。**优先用这条**。
        ///   · <c>JobTypes</c>   —— 工作**类型**，读 person.missionType。
        ///
        /// 【为什么 JobTypes 是容错而不是严格过滤】
        /// 项目 MissionType 里**没有"探索"这一项**：探索人才只记
        /// <c>CityJobType.Searching</c>（城池级的工作开销枚举），**从不写 person.missionType**。
        /// 所以对"探索完成"类事件，JobTypes 无论填什么都不可能命中。
        /// 早期版本甚至完全没实现这个字段，于是像 "Searching" 这种不存在的枚举名
        /// 会被静默忽略 —— 事件照常触发，但没人知道过滤其实没生效。
        /// 这里改为：**名称无法识别时按"不过滤"处理并告警**，避免一个笔误就让事件永久静默且毫无线索。
        /// 要精确判定"探索成功 / 一无所获"，请改用 JobSettled 时机 + JobResults。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="entity">hook 注入的实体</param>
        /// <param name="reason">不通过时的中文原因</param>
        /// <returns>是否通过</returns>
        bool MatchJobFilter(EventDefinition def, SangoObject entity, out string reason)
        {
            reason = null;
            EventTriggerDef trigger = def.Trigger;

            // ① 结果码
            if (trigger.JobResults != null && trigger.JobResults.Count > 0)
            {
                if (currentJobResult == int.MinValue)
                {
                    reason = "本事件要求按工作结果码过滤，但本次触发不携带结果码（只有 JobSettled 时机才有）";
                    return false;
                }

                if (!trigger.JobResults.Contains(currentJobResult))
                {
                    reason = $"工作结果码 {currentJobResult} 不在允许列表内";
                    return false;
                }
            }

            // ② 工作类型
            if (trigger.JobTypes == null || trigger.JobTypes.Count == 0) return true;

            Person person = entity as Person;
            if (person == null)
            {
                reason = "本事件配了 JobTypes 过滤，但本次 hook 没有注入武将实体";
                return false;
            }

            bool anyValid = false;
            for (int i = 0; i < trigger.JobTypes.Count; i++)
            {
                string name = trigger.JobTypes[i];
                if (!System.Enum.TryParse(name, out MissionType mission))
                {
                    Log.Warning($"事件 {def} 的 JobTypes 含无法识别的任务名 \"{name}\"，该项已忽略（请检查拼写；探索类事件应改用 JobResults）");
                    continue;
                }

                anyValid = true;
                if (person.missionType == (int)mission) return true;
            }

            if (anyValid)
            {
                reason = $"武将当前任务 {person.missionType} 不在 JobTypes 列表内";
                return false;
            }

            // 所有名称都无法识别 → 视为没配过滤，宁可多播也不要静默失效
            Log.Warning($"事件 {def} 的 JobTypes 没有任何一项能识别，已按\"不过滤\"处理");
            return true;
        }

        /// <summary>作用域匹配</summary>
        /// <param name="def">事件定义</param>
        /// <param name="entity">hook 注入的实体</param>
        /// <returns>是否匹配</returns>
        bool MatchScope(EventDefinition def, SangoObject entity)
        {
            EventTriggerScope scope = def.Trigger.Scope;

            switch (scope)
            {
                case EventTriggerScope.AnyForce:
                    return true;

                case EventTriggerScope.PlayerForce:
                    {
                        // 按 hook 注入的实体类型逐级判断"这件事是不是玩家引起的"。
                        // 只判"场上有玩家势力"是不够的：那会让敌人的行为也触发玩家事件
                        // （三顾茅庐要求"玩家的人做完探索"，用敌将做探索不该触发）。
                        Force force = entity as Force;
                        if (force != null) return force.IsPlayer;

                        Person person = entity as Person;
                        if (person != null) return person.BelongForce != null && person.BelongForce.IsPlayer;

                        Troop troop = entity as Troop;
                        if (troop != null) return troop.BelongForce != null && troop.BelongForce.IsPlayer;

                        City city = entity as City;
                        if (city != null) return city.BelongForce != null && city.BelongForce.IsPlayer;

                        // hook 没带实体（TurnStart / MonthStart 等）时，退化为"场上存在玩家势力"
                        return HasPlayerForce();
                    }

                case EventTriggerScope.SpecificForce:
                    {
                        Force force = entity as Force;
                        if (force != null) return force.Id == def.Trigger.ScopeId;
                        return true;
                    }

                case EventTriggerScope.SpecificCity:
                    {
                        City city = entity as City;
                        if (city != null) return city.Id == def.Trigger.ScopeId;
                        return true;
                    }

                default:
                    return true;
            }
        }

        /// <summary>是否已有玩家势力（按作用域的 PlayerForce 判定用）</summary>
        /// <returns>是否存在玩家势力</returns>
        bool HasPlayerForce()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.forceSet == null) return false;

            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force force = scenario.forceSet[i];
                if (force != null && force.IsPlayer) return true;
            }
            return false;
        }

        /// <summary>
        /// 播放事件：把上下文交给 EventRunner，并在结束时回写触发次数。
        /// </summary>
        /// <param name="def">事件定义</param>
        /// <param name="context">运行时上下文</param>
        /// <param name="state">运行时状态</param>
        /// <returns>是否成功开始播放</returns>
        bool PlayEvent(EventDefinition def, EventContext context, EventRunState state)
        {
            EventRunner runner = EventRunner.Instance;
            if (runner == null)
            {
                Log.Error($"剧本事件 {def} 无法播放：EventRunner 系统不可用");
                return false;
            }

            int eventId = def.Id;
            int cooldown = def.Trigger != null ? def.Trigger.CooldownTurns : 0;

            // 必须先置位再 Play：runner.Play 会立即执行第一条指令，
            // 若整个事件只有效果（Silent 级），会在 Play 返回**之前**就结束并回调，
            // 那时 runningEventId 还是旧值，密度计数就会错。
            runningEventId = eventId;
            firedThisTurn++;

            bool started = runner.Play(def, context, () => OnEventFinished(eventId, cooldown));
            if (!started)
            {
                if (runningEventId == eventId) runningEventId = 0;
                firedThisTurn--;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 事件播放结束的回调。
        /// 注意：只有正常播放完毕才计触发次数——
        /// 中止（槽位失效、异常）时不计，否则等价于"事件已发生但什么都没做"，还占掉了 Once 名额。
        /// </summary>
        /// <param name="eventId">事件 Id</param>
        /// <param name="cooldown">冷却回合数</param>
        void OnEventFinished(int eventId, int cooldown)
        {
            MarkFired(eventId, cooldown);

            if (runningEventId == eventId)
                runningEventId = 0;
        }

        /// <summary>
        /// 手动触发一个事件（调试面板"强制触发" / Op: CallEvent）。
        /// 会跳过触发次数、冷却、年份、密度这些闸门，但**仍求值条件树**。
        /// </summary>
        /// <param name="eventId">事件 Id</param>
        /// <returns>是否成功开始播放</returns>
        public bool FireEventNow(int eventId)
        {
            EventDefinition def = database.Get(eventId);
            if (def == null)
            {
                Log.Warning($"强制触发失败：找不到事件 {eventId}");
                return false;
            }

            if (IsPlaying)
            {
                Log.Warning($"强制触发失败：事件 {runningEventId} 正在播放");
                return false;
            }

            EventRunState state = GetState(eventId, true);
            EventContext context = new EventContext
            {
                definition = def,
                state = state,
                fireCount = state.FireCount + 1,
                fireKind = EventTriggerKind.ManualCall,
            };

            if (!EventSlotResolver.ResolveAll(def, context))
            {
                state.LastFailReason = context.slotFailReason ?? "槽位绑定失败";
                Log.Warning($"强制触发 {def} 失败：{state.LastFailReason}");
                return false;
            }

            context.database.fireCount = context.fireCount;
            context.database.eventId = def.Id;

            if (!EventConditionEvaluator.Evaluate(def.Conditions, context.database, out string reason))
            {
                state.LastFailReason = reason;
                Log.Warning($"强制触发 {def} 的条件未满足：{reason}");
                return false;
            }

            return PlayEvent(def, context, state);
        }

        /// <summary>
        /// 为 Op: CallEvent 构建子事件上下文（不启动新的播放，由 EventRunner 内联执行）。
        ///
        /// 与 <see cref="FireEventNow"/> 的区别：
        ///   · 不检查 IsPlaying —— 调用方本身就是正在播放的事件；
        ///   · 不占用 runningEventId、不计本回合事件密度（子事件属于同一场演出）；
        ///   · 仍然做槽位绑定与条件求值：子事件条件不满足时视为调用失败。
        /// </summary>
        /// <param name="eventId">子事件 Id</param>
        /// <param name="def">输出：事件定义</param>
        /// <param name="context">输出：已绑定槽位的上下文</param>
        /// <returns>是否可以内联执行</returns>
        public bool TryBuildCallContext(int eventId, out EventDefinition def, out EventContext context)
        {
            def = null;
            context = null;

            EventDefinition found = database.Get(eventId);
            if (found == null)
            {
                Log.Warning($"CallEvent 失败：找不到事件 {eventId}");
                return false;
            }

            EventRunState state = GetState(eventId, true);
            EventContext ctx = new EventContext
            {
                definition = found,
                state = state,
                fireCount = state != null ? state.FireCount + 1 : 1,
                fireKind = EventTriggerKind.ManualCall,
            };

            if (!EventSlotResolver.ResolveAll(found, ctx))
            {
                string reason = ctx.slotFailReason ?? "槽位绑定失败";
                if (state != null) state.LastFailReason = reason;
                Log.Warning($"CallEvent {found} 失败：{reason}");
                return false;
            }

            ctx.database.fireCount = ctx.fireCount;
            ctx.database.eventId = found.Id;

            if (!EventConditionEvaluator.Evaluate(found.Conditions, ctx.database, out string failReason))
            {
                if (state != null) state.LastFailReason = failReason;
                Log.Warning($"CallEvent {found} 的条件未满足：{failReason}");
                return false;
            }

            def = found;
            context = ctx;
            return true;
        }

        /// <summary>
        /// 子事件（Op: CallEvent）正常播完：计入触发次数。
        /// 刻意不碰 runningEventId —— 外层事件还在播。
        /// </summary>
        /// <param name="eventId">子事件 Id</param>
        public void NotifyCalledEventFinished(int eventId)
        {
            if (eventId <= 0) return;
            MarkFired(eventId, 0);
        }

        #endregion

        #region 运行时状态

        /// <summary>
        /// 取某个事件的运行时状态。
        /// </summary>
        /// <param name="eventId">事件 Id</param>
        /// <param name="createIfMissing">不存在时是否创建</param>
        /// <returns>运行时状态；不存在且不创建时返回 null</returns>
        public EventRunState GetState(int eventId, bool createIfMissing)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return null;

            if (scenario.EventStates == null)
            {
                if (!createIfMissing) return null;
                scenario.EventStates = new List<EventRunState>();
            }

            List<EventRunState> states = scenario.EventStates;
            for (int i = 0; i < states.Count; i++)
            {
                if (states[i] != null && states[i].EventId == eventId)
                    return states[i];
            }

            if (!createIfMissing) return null;

            EventRunState state = new EventRunState { EventId = eventId };
            states.Add(state);
            return state;
        }

        /// <summary>读取某事件已触发的次数，从未触发返回 0</summary>
        /// <param name="eventId">事件 Id</param>
        /// <returns>触发次数</returns>
        public int GetFireCount(int eventId)
        {
            EventRunState state = GetState(eventId, false);
            return state != null ? state.FireCount : 0;
        }

        /// <summary>读取某事件的私有变量</summary>
        /// <param name="eventId">事件 Id</param>
        /// <param name="key">变量名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>变量值</returns>
        public int GetEventVar(int eventId, string key, int defaultValue = 0)
        {
            EventRunState state = GetState(eventId, false);
            return state != null ? state.GetVar(key, defaultValue) : defaultValue;
        }

        /// <summary>写入某事件的私有变量</summary>
        /// <param name="eventId">事件 Id</param>
        /// <param name="key">变量名</param>
        /// <param name="value">变量值</param>
        public void SetEventVar(int eventId, string key, int value)
        {
            EventRunState state = GetState(eventId, true);
            if (state != null) state.SetVar(key, value);
        }

        /// <summary>
        /// 读取全局旗标（跨事件共享，随存档持久化）。
        /// </summary>
        /// <param name="key">旗标名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>旗标值</returns>
        public bool GetGlobalFlag(string key, bool defaultValue = false)
        {
            EventRunState state = GetState(GlobalStateEventId, false);
            return state != null ? state.GetFlag(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 写入全局旗标（跨事件共享，随存档持久化）。
        /// </summary>
        /// <param name="key">旗标名</param>
        /// <param name="value">旗标值</param>
        public void SetGlobalFlag(string key, bool value)
        {
            EventRunState state = GetState(GlobalStateEventId, true);
            if (state == null)
            {
                // 没有剧本时静默丢弃会让写旗标的效果"看起来成功了其实没生效"，
                // 这里明确报错，避免排查时毫无线索
                Log.Warning($"写入全局旗标失败（当前没有加载中的剧本）：{key} = {value}");
                return;
            }
            state.SetFlag(key, value);
        }

        /// <summary>
        /// 标记某事件已触发一次（写回触发次数与上次触发回合）。
        /// 注意：只应在事件**成功播放完毕**后调用；播放中途异常时不得调用，
        /// 否则会等价于"事件已发生但什么都没做"。
        /// </summary>
        /// <param name="eventId">事件 Id</param>
        /// <param name="cooldownTurns">触发后记录的冷却回合数（仅用于调试展示）</param>
        public void MarkFired(int eventId, int cooldownTurns)
        {
            EventRunState state = GetState(eventId, true);
            if (state == null) return;

            state.FireCount++;
            state.LastFireTurn = Scenario.Cur != null && Scenario.Cur.Info != null ? Scenario.Cur.Info.turnCount : -1;
        }

        #endregion

        #region 定时队列

        /// <summary>
        /// 登记一项定时待办：在 delayTurns 个回合后触发目标事件。
        /// 对应演出指令 Op: Schedule。
        /// </summary>
        /// <param name="targetEventId">目标事件 Id</param>
        /// <param name="delayTurns">延迟回合数（0 表示下一回合立即检查）</param>
        /// <param name="chainId">所属事件链 Id</param>
        /// <param name="stage">所属事件链阶段号</param>
        /// <param name="boundSlots">要继承的槽位绑定（只存类型 + Id，跨回合后重新解析）</param>
        /// <param name="cancelConditionJson">取消条件（序列化的条件树 JSON，可为 null）</param>
        /// <param name="sourceEventId">安排本项的事件 Id（日志追溯用）</param>
        public void Schedule(int targetEventId, int delayTurns, int chainId, int stage,
            List<BoundSlotRef> boundSlots, string cancelConditionJson, int sourceEventId)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
            {
                Log.Error("登记定时事件失败：当前没有加载中的剧本");
                return;
            }

            if (scenario.ScheduledEvents == null)
                scenario.ScheduledEvents = new List<ScheduledEventItem>();

            int curTurn = scenario.Info != null ? scenario.Info.turnCount : 0;
            ScheduledEventItem item = new ScheduledEventItem
            {
                TargetEventId = targetEventId,
                FireTurn = curTurn + (delayTurns < 0 ? 0 : delayTurns),
                ChainId = chainId,
                Stage = stage,
                BoundSlots = boundSlots,
                CancelConditionJson = cancelConditionJson,
                SourceEventId = sourceEventId,
            };

            scenario.ScheduledEvents.Add(item);
            Log.Info($"登记定时事件：目标事件 {targetEventId}，将在第 {item.FireTurn} 回合触发（当前第 {curTurn} 回合，链 {chainId} 阶段 {stage}）");
        }

        /// <summary>
        /// 推进定时队列：把到达触发回合的待办项取出来执行。
        /// 由 OnTurnStart 调用，每回合一次。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void ProcessScheduledQueue(Scenario scenario)
        {
            if (scenario == null || scenario.ScheduledEvents == null || scenario.ScheduledEvents.Count == 0)
                return;

            // 演出占用中就让路。注意**必须在这里 return、不能继续往下走**：
            // 下面的流程是"先 RemoveAt 再尝试播放"，一旦播放失败待办项就永久丢了。
            // 在这里拦住，待办项留在队列里，下回合再试。
            if (IsPlaying) return;
            if (IsBlockedByOtherPresentation()) return;

            List<ScheduledEventItem> items = scenario.ScheduledEvents;
            int curTurn = scenario.Info != null ? scenario.Info.turnCount : 0;

            // 倒序遍历：边遍历边移除，避免索引错位
            for (int i = items.Count - 1; i >= 0; i--)
            {
                ScheduledEventItem item = items[i];
                if (item == null)
                {
                    items.RemoveAt(i);
                    continue;
                }

                // 还没到触发回合，留着
                if (curTurn < item.FireTurn)
                    continue;

                items.RemoveAt(i);

                // 取消条件成立则丢弃（如"袁绍已灭亡，官渡余波不再发生"）
                if (EventConditionEvaluator.IsCancelConditionMet(item, out string cancelReason))
                {
                    Log.Info($"定时事件被取消：目标事件 {item.TargetEventId}（{cancelReason}）");
                    continue;
                }

                if (FireScheduled(item)) return;    // 一回合只推进一项，其余留到下回合
            }
        }

        /// <summary>
        /// 执行一个到期的定时待办：按"类型 + Id"重建槽位绑定后播放目标事件。
        /// </summary>
        /// <param name="item">待办项</param>
        /// <returns>是否成功开始播放</returns>
        bool FireScheduled(ScheduledEventItem item)
        {
            EventDefinition def = database.Get(item.TargetEventId);
            if (def == null)
            {
                Log.Warning($"定时事件触发失败：找不到事件 {item.TargetEventId}（{item.Describe()}）");
                return false;
            }

            EventRunState state = GetState(def.Id, true);

            EventContext context = new EventContext
            {
                definition = def,
                state = state,
                fireCount = state.FireCount + 1,
                fireKind = EventTriggerKind.ManualCall,
            };

            if (!EventSlotResolver.ResolveAll(def, context))
            {
                state.LastFailReason = context.slotFailReason ?? "槽位绑定失败";
                Log.Warning($"定时事件 {def} 因槽位失效放弃：{state.LastFailReason}");
                return false;
            }

            // 定时待办继承上一阶段的对象：覆盖到手动解析的结果上，
            // 保证"官渡之战阶段 7"用的还是阶段 1 选定的那些人。
            // 按 item.BoundSlots 里的原始类型逐个重建，而不是笼统当成 Person——
            // 势力 / 城池槽若按 Person 解析会全部失效（这是很容易埋进去的坑）。
            InheritBoundSlots(item, context);

            context.database.fireCount = context.fireCount;
            context.database.eventId = def.Id;

            if (!EventConditionEvaluator.Evaluate(def.Conditions, context.database, out string reason))
            {
                state.LastFailReason = reason;
                Log.Info($"定时事件 {def} 的条件未满足：{reason}");
                return false;
            }

            Log.Info($"定时事件到达触发点并开始播放：{item.Describe()}");
            return PlayEvent(def, context, state);
        }

        /// <summary>
        /// 把定时待办携带的槽位绑定重新解析并覆盖到上下文上。
        ///
        /// 逐条按 BoundSlotRef 里记录的**原始类型**重建——不能笼统当成 Person，
        /// 否则势力 / 城池 / 军团槽会全部解析失败（只需一次写错就很难查）。
        /// 已经失效的对象（武将在等待期间死了、部队解散了）会被跳过，
        /// 该槽位保持为空，由后续条件 / 效果自己判断。
        /// </summary>
        /// <param name="item">待办项</param>
        /// <param name="context">运行时上下文</param>
        void InheritBoundSlots(ScheduledEventItem item, EventContext context)
        {
            if (item.BoundSlots == null || item.BoundSlots.Count == 0) return;

            Scenario scenario = Scenario.Cur;
            int failed = 0;

            for (int i = 0; i < item.BoundSlots.Count; i++)
            {
                BoundSlotRef slotRef = item.BoundSlots[i];
                if (string.IsNullOrEmpty(slotRef.Key)) continue;

                SangoObject obj = EventConditionEvaluator.ResolveObject(scenario, slotRef.Type, slotRef.Id);
                if (obj == null)
                {
                    failed++;
                    continue;
                }

                context.BindSlot(slotRef.Key, slotRef.Type, obj);
            }

            if (failed > 0)
                Log.Warning($"定时事件继承槽位时 {failed} 个对象已失效（{item.Describe()}）");
        }

        /// <summary>当前待办的定时事件数量（调试面板用）</summary>
        public int ScheduledCount
        {
            get
            {
                Scenario scenario = Scenario.Cur;
                return scenario != null && scenario.ScheduledEvents != null ? scenario.ScheduledEvents.Count : 0;
            }
        }

        #endregion
    }
}
