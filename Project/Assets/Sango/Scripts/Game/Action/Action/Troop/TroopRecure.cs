using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的增减伤害  
    /// value： 增加值(百分比) , Execute为绝对值
    /// </summary>
    public class TroopRecure : TroopActionBase
    {
        int percent = 0;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            percent = p.Value<int>("percent");
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger, params object[] sangoObjects)
        {
            if(trigger == null) return;
            if (trigger.ActionTroop != Troop) return;
            if (trigger.DamageOverride != null && trigger.ActionTroop.IsAlive)
            {
                int hp = trigger.DamageOverride.Value * percent / 100;
                if (hp > 0)
                {
                    trigger.ActionTroop.ChangeTroops(hp, trigger.ActionTroop, trigger.ActionSkill, 0);
                }
            }
        }
    }
}
