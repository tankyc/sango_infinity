using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 阻止本城市所属武将在换季结算时降低忠诚。
    /// </summary>
    public class CityPreventPersonLoyaltyLoss : CityActionBase
    {
        /// <summary>
        /// 订阅势力换季忠诚结算事件。
        /// </summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnForcePersonLoyaltyChange += OnForcePersonLoyaltyChange;
        }

        /// <summary>
        /// 解除势力换季忠诚结算事件订阅。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnForcePersonLoyaltyChange -= OnForcePersonLoyaltyChange;
        }

        /// <summary>
        /// 仅拦截本城且属于当前势力的武将的本次掉忠。
        /// </summary>
        private void OnForcePersonLoyaltyChange(Force force, Person person, OverrideData<bool> shouldLoseLoyalty)
        {
            if (City != person.BelongCity || City.BelongForce != force)
            {
                return;
            }
            shouldLoseLoyalty.Value = false;
        }
    }
}
