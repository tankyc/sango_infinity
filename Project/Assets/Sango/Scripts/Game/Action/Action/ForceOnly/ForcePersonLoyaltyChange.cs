using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 势力忠诚减少概率  value:修改值(百分比)
    /// </summary>
    public class ForcePersonLoyaltyChange : ForceActionBase
    {
        protected int value;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            GameEvent.OnForcePersonLoyaltyChangeProbability += OnForcePersonLoyaltyChangeProbability;
        }

        public override void Clear()
        {
            GameEvent.OnForcePersonLoyaltyChangeProbability -= OnForcePersonLoyaltyChangeProbability;
        }

        void OnForcePersonLoyaltyChangeProbability(Force force, OverrideData<int> overrideData)
        {
            if (Force != force) return;
            overrideData.Value = (int)System.Math.Ceiling(overrideData.Value * value / 100f);
        }
    }
}
