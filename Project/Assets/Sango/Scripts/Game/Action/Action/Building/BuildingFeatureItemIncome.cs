using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑特色：每 N 回合额外增加一次道具收入。
    ///
    /// JSON 参数（actionEntities 内）：
    ///   itemTypeId   : 道具类型 Id
    ///   value        : 每次增加数量
    ///   intervalTurns: 间隔回合数（默认 1）
    ///
    /// 道具直接进所属城市的 itemStore；不触发生产流程、不占工作位。
    /// </summary>
    public class BuildingFeatureItemIncome : BuildingActionBase
    {
        int itemTypeId;
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

            itemTypeId = p != null ? p.Value<int>("itemTypeId") : 0;
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
            // 由建筑回合事件驱动。
        }

        void OnBuildingTurnStart(Building building, Scenario scenario)
        {
            if (Building == null || building != Building || itemTypeId <= 0 || value == 0)
                return;
            // 未完工 / 已失效的建筑不结算；IsComplate 在 Building 子类上。
            Building concrete = Building as Building;
            if (concrete == null || !concrete.isComplate || !Building.IsAlive)
                return;

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
            city.AddItem(itemTypeId, value);
        }
    }
}
