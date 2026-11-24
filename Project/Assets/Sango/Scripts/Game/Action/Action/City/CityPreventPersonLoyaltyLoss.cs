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

            // 【只有真正绑定到城市的实例才订阅】
            // CityActionBase.Init 允许两种"City 为空"的合法情形：
            //   ① sangoObjects 为空数组 → 直接 return，City 保持 null；
            //   ② sangoObjects[0] 不是 City（例如绑到 Force）→ City = null、Force 有值。
            // 这两种情况下若仍然订阅，就留下一个"City == null"的僵尸监听者：
            // 它永远否决不了任何人（拿不到自己的城），却会在换季结算里抛
            // NullReferenceException —— 即本次线上崩溃的根因，见方法内注释。
            if (City == null)
            {
                Sango.Log.Error($"*仁政(CityPreventPersonLoyaltyLoss) 装配时未绑定到城市，已跳过事件订阅"
                    + $"（sangoObjects={(sangoObjects == null ? "null" : sangoObjects.Length.ToString())}）");
                return;
            }

            GameEvent.OnForcePersonLoyaltyChange += OnForcePersonLoyaltyChange;

            // 【标记位】换季结算的主判定走位域，装配即置位。
            City.SetStateFlag(CityStateFlag.RenZheng, true);
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
            // 【空值防御 —— 曾经的崩溃点】
            // 原写法是 `if (City != person.BelongCity || City.BelongForce != force)`，
            // 只有两个引用都为空时才会出事，而这一组合恰恰是真会发生的：
            //   · City 为空：Action 被绑到 Force / 装配时传了空数组（见 Init 注释）；
            //   · person.BelongCity 为空：无户口的武将（剧本初始化中间态、城陷撤离途中、
            //     异族 / 事件武将）—— PersonLoyaltyRules.CanLoseLoyalty 原先不拦这类对象。
            // 此时 `null != null` 为 false，短路失效，紧接着求值 `City.BelongForce`
            // 就抛 NullReferenceException。
            //
            // 语义上：没有所属城的人谈不上"本城仁政保护"，直接放行（照常扣忠）即可。
            if (City == null || person == null || person.BelongCity == null)
                return;

            if (City != person.BelongCity || City.BelongForce != force)
            {
                return;
            }
            shouldLoseLoyalty.Value = false;
        }
    }
}
