using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 驱虎吞狼计略指令：选一个敌方据点（太守所在）与一名使者，
    /// 使者抵达后策反该城太守，使其带着这座城独立成一个新势力。
    ///
    /// 交互与流言一致（单目标城 + 军师举荐使者），因此复用流言的窗口 prefab。
    /// 效果侧见 <see cref="CityStrategyActionTigerDevour"/> 与 <see cref="CityStateSystem.CreateIndependence"/>。
    /// </summary>
    [GameSystem]
    public class CityStrategyTigerDevour : CityStrategySystemBase
    {
        /// <summary>
        /// 玩家选定的被策反据点，同时是使者目的地
        /// </summary>
        City targetCity;

        public CityStrategyTigerDevour()
        {
            customTitleName = "驱虎吞狼";
            customMenuName = "计略/驱虎吞狼";
            // 整组"计略"排在外交之后、君主之前；组内次序：二虎竞食 → 流言 → 驱虎吞狼
            customMenuOrder = 3970;
            // 与流言同布局（单目标行），直接复用其窗口 prefab
            windowName = "window_city_strategy_rumor";
        }

        /// <summary>
        /// 本指令对应的计略类型
        /// </summary>
        protected override CityStrategyType StrategyType => CityStrategyType.TigerDevour;

        /// <summary>
        /// 本指令对应的城市工作类型，消耗从 JobTypes.json 的 26 号行读取
        /// </summary>
        protected override CityJobType StrategyJob => CityJobType.TigerDevour;

        /// <summary>
        /// 驱虎吞狼只有一个目标框
        /// </summary>
        public override int TargetSlotCount => 1;

        /// <summary>
        /// 目标行标题：目标是一座敌方城池
        /// </summary>
        /// <param name="slot">目标框序号，只有 0</param>
        /// <returns>字段标题</returns>
        public override string GetTargetTitle(int slot)
        {
            return "目标城池";
        }

        /// <summary>
        /// 目标行：被策反据点名（含势力色包边）
        /// </summary>
        /// <param name="slot">目标框序号，只有 0</param>
        /// <returns>据点名，未选时为空串</returns>
        public override string GetTargetDescription(int slot)
        {
            return targetCity != null ? targetCity.ColorName : "";
        }

        /// <summary>
        /// 唯一目标框始终可选
        /// </summary>
        /// <param name="slot">目标框序号</param>
        /// <returns>固定 true</returns>
        public override bool CanSelectTargetSlot(int slot)
        {
            return true;
        }

        /// <summary>
        /// 关系行：发起方与据点所属势力的交情，让玩家先看到"这次会得罪谁"
        /// </summary>
        public override string RelationshipDescription
        {
            get
            {
                Force owner = targetCity != null ? targetCity.BelongForce : null;
                if (owner == null || TargetCity == null)
                    return "";
                return Scenario.Cur.GetRelation(TargetCity.BelongForce, owner).ToString();
            }
        }

        /// <summary>
        /// 抵达日数：出发城到被策反据点的行军日数
        /// </summary>
        public override int EnvoyDistanceDays
        {
            get
            {
                if (targetCity == null || TargetCity == null)
                    return 0;
                return ToDays(TargetCity.Distance(targetCity));
            }
        }

        /// <summary>
        /// 选定目标即算就绪
        /// </summary>
        public override bool TargetReady => targetCity != null;

        /// <summary>
        /// 打开据点选择器，单选点选即确认
        /// </summary>
        /// <param name="slot">目标框序号，只有 0</param>
        public override void OpenTargetSelector(int slot)
        {
            List<City> candidates = BuildTargetCities();
            if (candidates.Count <= 0)
                return;

            List<City> preChecked = targetCity != null ? new List<City> { targetCity } : new List<City>();
            GameSystem.GetSystem<CitySelectSystem>().Start(
                candidates, preChecked, 1, OnTargetCitySelected, null, "驱虎吞狼目标");
        }

        /// <summary>
        /// 是否存在至少一个可策反的敌方据点
        /// </summary>
        /// <returns>满足返回 true</returns>
        protected override bool HasEnoughTargets()
        {
            return BuildTargetCities().Count > 0;
        }

        /// <summary>
        /// 据点选择器的确认回调
        /// </summary>
        /// <param name="cities">选中的据点，单选时长度为 1</param>
        void OnTargetCitySelected(List<City> cities)
        {
            if (cities == null || cities.Count <= 0)
                return;

            targetCity = cities[0];
            OnTargetsChanged();
        }

        /// <summary>
        /// 进入指令时清掉上一次的目标选择
        /// </summary>
        public override void OnEnter()
        {
            targetCity = null;
            base.OnEnter();
        }

        /// <summary>
        /// 拼出本次驱虎吞狼行为
        /// </summary>
        /// <param name="selectedEnvoy">使者武将</param>
        /// <returns>计略行为；玩家尚未选定据点时返回 null</returns>
        protected override CityStrategyActionBase BuildAction(Person selectedEnvoy)
        {
            if (targetCity == null)
                return null;

            return GameSystem.GetSystem<CityStrategyManager>().CreateAction(
                StrategyType, TargetCity.BelongForce, TargetCity, selectedEnvoy, targetCity, targetCity.BelongForce, null);
        }

        /// <summary>
        /// 生成可策反的据点候选。
        ///
        /// 与流言的差别：必须是**都市**（港关没有"太守带城独立"的语义），
        /// 且城中要有太守、太守不能是该势力君主（君主不会被策反）。
        /// </summary>
        /// <returns>候选据点列表</returns>
        List<City> BuildTargetCities()
        {
            List<City> candidates = new List<City>();
            if (TargetCity == null)
                return candidates;

            Force sender = TargetCity.BelongForce;
            Scenario.Cur.citySet.ForEach((System.Action<City>)(city =>
            {
                if (city == null || !city.IsAlive)
                    return;
                // 只有都市能"带城独立"
                if (!city.IsCity())
                    return;
                Force owner = city.BelongForce;
                if (owner == null || owner == sender || !owner.IsAlive)
                    return;
                // 没有太守就没有策反对象
                Person taishu = city.Leader;
                if (taishu == null || !taishu.IsAlive)
                    return;
                // 君主不会被策反
                if (taishu.IsGovernor)
                    return;
                candidates.Add(city);
            }));
            return candidates;
        }
    }
}
