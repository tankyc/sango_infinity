using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑特色：每 N 回合额外增加一次资源收入。
    ///
    /// JSON 参数（actionEntities 内）：
    ///   resourceType : 0 金 / 1 粮 / 2 技巧点
    ///   value        : 每次增加量
    ///   intervalTurns: 间隔回合数（默认 1 = 每回合）
    ///
    /// 【为什么挂在 OnBuildingTurnStart】建筑回合开始由 Building.OnForceTurnStart 派发，
    /// 每座建筑每回合只触发一次；资源直接进所属城市，不需要改 BuildingWorking 的收入公式。
    /// 【性能】未配置特色动作的建筑不会创建本 Action；每个 Action 只在自身建筑回合回调时
    /// 做一次引用比较，满间隔时才写资源，无列表分配、无字符串拼接。
    /// </summary>
    public class BuildingFeatureResourceIncome : BuildingActionBase
    {
        int resourceType;
        int value;
        int intervalTurns;
        int durationTurns;
        int remainingTurns;
        int turnCounter;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            if (Building == null)
                return;

            resourceType = p != null ? p.Value<int>("resourceType") : 0;
            value = p != null ? p.Value<int>("value") : 0;
            intervalTurns = p != null ? p.Value<int>("intervalTurns") : 1;
            if (intervalTurns < 1)
                intervalTurns = 1;
            durationTurns = p != null ? p.Value<int>("durationTurns") : 0;
            remainingTurns = durationTurns;
            turnCounter = 0;

            GameEvent.OnBuildingTurnStart += OnBuildingTurnStart;
        }

        public override void Clear()
        {
            GameEvent.OnBuildingTurnStart -= OnBuildingTurnStart;
        }

        public override void Execute(Trigger trigger)
        {
            // 本 Action 由建筑回合事件驱动，不参与通用 Trigger 派发。
        }

        void OnBuildingTurnStart(Building building, Scenario scenario)
        {
            if (Building == null || building != Building || value == 0)
                return;
            // 未完工 / 已失效的建筑不结算；IsComplate 在 Building 子类上。
            Building concrete = Building as Building;
            if (concrete == null || !concrete.isComplate || !Building.IsAlive)
                return;

            // durationTurns > 0 表示限时特色：按建筑回合递减；0 = 永久。
            if (durationTurns > 0)
            {
                if (remainingTurns <= 0)
                    return;
                remainingTurns--;
            }

            turnCounter++;
            if (turnCounter < intervalTurns)
                return;
            turnCounter = 0;

            City city = Building.BelongCity;
            if (city == null)
                return;

            switch (resourceType)
            {
                case 0:
                    city.AddGold(value);
                    break;
                case 1:
                    city.AddFood(value);
                    break;
                case 2:
                    city.BelongForce?.GainTechniquePoint(value);
                    break;
            }
        }
    }
}
