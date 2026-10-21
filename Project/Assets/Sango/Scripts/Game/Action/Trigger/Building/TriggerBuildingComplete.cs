namespace Sango.Core
{
    /// <summary>
    /// 建筑建造完成：接入 GameEvent.OnBuildingComplete（建筑, 参与建造的武将列表）。
    ///
    /// 适合做"建成奖励"：给所属城加资源、给参与武将加功绩 / 经验、解锁后续建筑等。
    /// </summary>
    public class TriggerBuildingComplete : BuildingTrigger
    {
        /// <summary>参与建造的武将列表</summary>
        public SangoObjectList<Person> Builders;

        /// <summary>目标对象：参与建造的武将列表</summary>
        public override object TargetObject => Builders;

        /// <summary>订阅事件</summary>
        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            GameEvent.OnBuildingComplete += OnBuildingComplete;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingComplete -= OnBuildingComplete;
        }

        void OnBuildingComplete(Building building, SangoObjectList<Person> builders)
        {
            if (!CheckBuilding(building))
                return;

            Builders = builders;
            triggerCall?.Invoke(this);
        }
    }
}
