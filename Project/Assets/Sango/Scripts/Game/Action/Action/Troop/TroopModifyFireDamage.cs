using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队忽略ZOC
    /// land: 0 忽略水上 1 忽略陆地 2都忽略
    /// value 万分比
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
            if (Force != null && skillInstance.master.mBelongForce != Force) return;
            if (Troop != null && Troop != skillInstance.master) return;

            fire.damage = Scenario.Cur.Variables.baseFireDamage * value / 10000;
        }
    }
}
