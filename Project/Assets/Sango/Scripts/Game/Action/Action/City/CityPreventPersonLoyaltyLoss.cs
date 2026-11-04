using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技「仁政」：本城所属武将换季结算时不掉忠。
    ///
    /// 【实现方式】装配时点亮所属城市的 <see cref="CityStateFlag.RenZheng"/> 标记位，
    /// 销毁时清位。换季结算（<see cref="PersonLoyaltyRules.HasRenZhengInBelongCity"/>）
    /// 只读这个 bit，不再遍历武将列表找特技 —— 原写法是"每个武将都全量扫一遍 personSet"，
    /// 整体 O(N²)，1500 名武将时单季要 2.4 秒。
    ///
    /// 【保留事件订阅】<see cref="GameEvent.OnForcePersonLoyaltyChange"/> 这一路仍保留：
    /// 它与标记位是两道独立的闸门（原版也是两条路），
    /// 且本 Action 只在城市装配 Action 时存在，标记位则由城市统一重算兜底。
    /// </summary>
    public class CityPreventPersonLoyaltyLoss : CityActionBase
    {
        /// <summary>
        /// 订阅势力换季忠诚结算事件，并点亮本城的「仁政」标记位。
        /// </summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnForcePersonLoyaltyChange += OnForcePersonLoyaltyChange;

            // 【标记位】换季结算的主判定走位域，装配即置位。
            City?.SetStateFlag(CityStateFlag.RenZheng, true);
        }

        /// <summary>
        /// 解除订阅，并清掉本城的「仁政」标记位。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnForcePersonLoyaltyChange -= OnForcePersonLoyaltyChange;

            // 标记位跟随 Action 生命周期：Action 销毁 = 本城不再有仁政。
            // 重建时 Init 会重新置位，因此"清 → 建"的顺序不会留下错误状态。
            City?.SetStateFlag(CityStateFlag.RenZheng, false);
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
