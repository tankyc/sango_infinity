using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 流言计略指令：选一个敌方据点（都市/港口/关隘均可）与一名使者，
    /// 使者抵达该据点后结算忠诚下降，目标为都市时同时结算治安下降。
    /// 交互为送礼式：进入指令即常驻窗口，只用一个"目标城池"框（窗口第二框由基类按 TargetSlotCount 收起），
    /// 选定目标城池后军师举荐框才弹出，点确认才把推荐人选落为默认使者，使者可另点按钮换人，最后点"决定"派遣。
    /// </summary>
    [GameSystem]
    public class CityStrategyRumor : CityStrategySystemBase
    {
        /// <summary>
        /// 玩家选定的被施法据点，同时是使者目的地
        /// </summary>
        City targetCity;

        public CityStrategyRumor()
        {
            customTitleName = "流言";
            customMenuName = "计略/流言";
            // 整组"计略"排在外交之后、君主之前；组内次序：二虎竞食 → 流言 →（将来）驱虎吞狼
            customMenuOrder = 3960;
            // 流言独占一个窗口 prefab：布局与送礼窗口逐行一致（单目标行），不随二虎的第二目标行被拉高
            windowName = "window_city_strategy_rumor";
        }

        /// <summary>
        /// 本指令对应的计略类型
        /// </summary>
        protected override CityStrategyType StrategyType => CityStrategyType.Rumor;

        /// <summary>
        /// 本指令对应的城市工作类型，消耗从 JobTypes.json 的 27 号行读取
        /// </summary>
        protected override CityJobType StrategyJob => CityJobType.Rumor;

        /// <summary>
        /// 流言只有一个目标框
        /// </summary>
        public override int TargetSlotCount => 1;

        /// <summary>
        /// 目标行标题：流言的目标是一座敌方城池
        /// </summary>
        /// <param name="slot">目标框序号，流言只有 0</param>
        /// <returns>字段标题</returns>
        public override string GetTargetTitle(int slot)
        {
            return "目标城池";
        }

        /// <summary>
        /// 目标行：被施法据点名（含势力色包边）
        /// </summary>
        /// <param name="slot">目标框序号，流言只有 0</param>
        /// <returns>据点名，未选时为空串</returns>
        public override string GetTargetDescription(int slot)
        {
            return targetCity != null ? targetCity.ColorName : "";
        }

        /// <summary>
        /// 流言的唯一目标框始终可选
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
        /// 抵达日数：出发城到被施法据点的行军日数，两者任一未定则返回 0。
        /// 用出发城而非使者所在位置起算，保证玩家还没换使者时这一行也已经给出可比较的数值
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
        /// 流言只需一个目标，选定即算就绪
        /// </summary>
        public override bool TargetReady => targetCity != null;

        /// <summary>
        /// 打开据点选择器，单选点选即确认
        /// </summary>
        /// <param name="slot">目标框序号，流言只有 0</param>
        public override void OpenTargetSelector(int slot)
        {
            List<City> candidates = BuildTargetCities();
            if (candidates.Count <= 0)
                return;

            // 把当前已选据点作为初始勾选集传入，玩家二次进入选择器时能看到自己上一次的选择
            List<City> preChecked = targetCity != null ? new List<City> { targetCity } : new List<City>();
            GameSystem.GetSystem<CitySelectSystem>().Start(
                candidates, preChecked, 1, OnTargetCitySelected, null, "流言目标");
        }

        /// <summary>
        /// 是否存在至少一个可施法的敌方据点
        /// </summary>
        /// <returns>满足返回 true</returns>
        protected override bool HasEnoughTargets()
        {
            return BuildTargetCities().Count > 0;
        }

        /// <summary>
        /// 据点选择器的确认回调：记录目标并交给基类收尾（目标齐了才弹军师举荐框）
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
        /// 进入指令时清掉上一次的目标选择，避免复用窗口实例时残留
        /// </summary>
        public override void OnEnter()
        {
            targetCity = null;
            base.OnEnter();
        }

        /// <summary>
        /// 拼出本次流言行为
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
        /// 生成可施法的据点候选：存活的其他势力所属、且是都市/港口/关隘这类有驻军的据点
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
                // 野城与特殊建筑没有忠诚与治安的攻心对象，只对三方据点生效
                if (!city.IsCityBase())
                    return;
                Force owner = city.BelongForce;
                if (owner == null || owner == sender || !owner.IsAlive)
                    return;
                // 处于流言免疫窗内的据点暂不可选：避免玩家选完才在抵达时发现效果不落地
                if (city.IsRumorImmune())
                    return;
                candidates.Add(city);
            }));
            return candidates;
        }
    }
}
