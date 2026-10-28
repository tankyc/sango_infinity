namespace Sango.Core
{
    /// <summary>
    /// 异族 / 贼「根城」（原版 nejiro）系统。
    ///
    /// 【三个时机，分别对应原版三处】
    ///   · <c>OnScenarioStart</c>：清候选位缓存 + 补正势力类型。**不生成根城**
    ///     （原版开局没有根城，前 6 个月也不生成，见 s11_game_month.cpp:1505）。
    ///   · <c>OnMonthUpdate</c>：月度生成根城，对齐 <c>Game::bandit_appearance</c>
    ///     （s11_game_month.cpp:1503-1583）。
    ///   · <c>OnTurnStart</c>：每回合（原版每旬）遍历所有根城增援，对齐 <c>Game::on_week</c>
    ///     （s11_game_week.cpp:636-653）。
    ///
    /// 【时间粒度依据】Scenario.cs:1869 的注释已确认：本项目一回合 = 一旬 = 原版一周；
    /// 36 回合/年 ÷ 12 月 = 3 回合/月（原版 <c>elapsed_months = turn / 3</c>）。
    /// </summary>
    [GameSystem(order = 100)]
    public class BanditCampSystem : GameSystem
    {
        /// <summary>
        /// 系统初始化：订阅剧本开始 / 月度 / 回合开始。
        /// </summary>
        public override void Init()
        {
            Name = "根城";
            // 先退订再订阅：GameSystem 是单例，重复 Init 不能把同一回调挂两次
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnScenarioStart += OnScenarioStart;
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
            GameEvent.OnMonthUpdate += OnMonthUpdate;
            GameEvent.OnTurnStart -= OnTurnStart;
            GameEvent.OnTurnStart += OnTurnStart;
        }

        /// <summary>
        /// 系统清理：退订事件，避免回主菜单后残留回调。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
            GameEvent.OnTurnStart -= OnTurnStart;
            BanditCampPlacer.ResetCache();
        }

        /// <summary>
        /// 剧本开始：清上局残留的候选位缓存，并补正剧本缺失的势力类型。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnScenarioStart(Scenario scenario)
        {
            BanditCampPlacer.ResetCache();

            if (scenario == null || scenario.forceSet == null)
                return;

            for (int i = 0; i < scenario.forceSet.Count; i++)
                BanditCampPlacer.EnsureForceType(scenario.forceSet[i]);
        }

        /// <summary>
        /// 月度：尝试长出新根城（原版 <c>bandit_appearance</c>）。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnMonthUpdate(Scenario scenario)
        {
            BanditCampPlacer.TrySpawnMonthly(scenario);
        }

        /// <summary>
        /// 每回合：所有根城按概率增援（原版 <c>on_week</c>）。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnTurnStart(Scenario scenario)
        {
            BanditTroopFactory.TrySpawnAll(scenario);
        }
    }
}
