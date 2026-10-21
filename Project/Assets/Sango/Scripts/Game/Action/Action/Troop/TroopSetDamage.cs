using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 把本次兵力变化量设为固定值（value 为绝对值，正负号跟随原值）。
    ///
    /// 接入事件：OnTroopChangeTroops（兵力增减结算时）。
    /// belowDamage > 0 时，只有原变化量不超过该阈值才会生效。
    /// </summary>
    public class TroopSetDamage : TroopTroopActionBase
    {
        int belowDamage = 0;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            belowDamage = p.Value<int>("belowDamage");
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        public override void Clear()
        {
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        void OnTroopChangeTroops(Troop defencer, SangoObject atker, int atkBack, OverrideData<int> overrideData)
        {
            if (belowDamage > 0 && System.Math.Abs(overrideData.Value) > belowDamage)
                return;
            if (!CheckTroop(defencer, atker)) return;

            // 【符号修复】兵力变化量的正负号代表"增兵 / 掉兵"，不能直接赋正数：
            // 原先写 overrideData.Value = value，配置 value=800 时会把"掉 800 兵"改写成"加 800 兵"。
            // 这里跟随原值符号，只替换绝对值；原值为 0（无变化）时按掉兵处理，保持"设定伤害"的直觉。
            int absValue = System.Math.Abs(value);
            overrideData.Value = overrideData.Value > 0 ? absValue : -absValue;
        }

        public override void Execute(Trigger trigger)
        {

        }

    }
}
