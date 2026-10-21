using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按万分比缩放火焰伤害（乘法）。
    ///
    /// 接入事件：OnFireAdd（火焰生成时）。
    /// </summary>
    public class TroopModifyFireDamage : TroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnFireAdd += OnFireAdd;
        }

        public override void Clear()
        {
            GameEvent.OnFireAdd -= OnFireAdd;
        }

        void OnFireAdd(SkillInstance skillInstance, Fire fire)
        {
            if (Force != null && skillInstance.master.BelongForce != Force) return;
            if (Troop != null && Troop != skillInstance.master) return;

            fire.damage = Scenario.Cur.Variables.baseFireDamage * value / 10000;
        }
    }
}
