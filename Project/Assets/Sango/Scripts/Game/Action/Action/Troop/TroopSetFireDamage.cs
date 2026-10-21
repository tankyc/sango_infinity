using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 设定火焰伤害数值（覆盖）。
    ///
    /// 接入事件：OnFireAdd（火焰生成时），实际伤害 = 剧本 baseFireDamage × value / 10000，
    /// 即 value = 10000 表示一倍基础火焰伤害，20000 表示两倍。
    /// </summary>
    public class TroopSetFireDamage : TroopActionBase
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
