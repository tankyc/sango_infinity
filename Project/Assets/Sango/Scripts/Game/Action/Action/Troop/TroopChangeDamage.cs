using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按百分比增减本次兵力变化量（value 可为负）。
    ///
    /// 接入事件：OnTroopChangeTroops（兵力增减结算时），这是"改伤害"的正确挂点。
    /// </summary>
    public class TroopChangeDamage : TroopTroopActionBase
    {
        /// <summary>
        /// 反击过滤：-1 不限（默认）/ 0 仅非反击 / 1 仅反击。
        ///
        /// <para>
        /// 【为什么用 -1 作默认】`p.Value<int>("atkBack")` 在键缺失时返回 0，
        /// 而 0 的含义是"仅非反击"，会让所有既有配置（如破军 +30%）突然对反击失效。
        /// 因此这里必须用 JToken 判空后再取值，保持"未配置 = 不限"的向后兼容。
        /// </para>
        /// </summary>
        int atkBackFilter;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            Newtonsoft.Json.Linq.JToken token = p["atkBack"];
            atkBackFilter = token != null ? token.ToObject<int>() : -1;

            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        void OnTroopChangeTroops(Troop defencer, SangoObject atker,int atkBack, OverrideData<int> overrideData)
        {
            // 反击过滤必须在 CheckTroop 之前：CheckTroop 会用 atker 反推"施法部队"，
            // 而反击场景下 atker 也是发起方部队，两者并不冲突，提前过滤更省。
            if (atkBackFilter == 0 && atkBack == 1) return;
            if (atkBackFilter == 1 && atkBack != 1) return;

            if (!CheckTroop(defencer, atker)) return;
            overrideData.Value = overrideData.Value * (100 + value) / 100;
        }

        public override void Execute(Trigger trigger)
        {

        }

    }
}
