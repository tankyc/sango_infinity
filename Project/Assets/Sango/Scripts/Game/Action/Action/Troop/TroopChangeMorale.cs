using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的增减伤害  
    /// value： 增加值(百分比) , Execute为绝对值
    /// </summary>
    public class TroopChangeMorale : TroopActionBase
    {
        int targetType = 0;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            targetType = p.Value<int>("targetType");
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if(trigger == null) return;

            if (trigger.ActionTroop != Troop) return;

            if(targetType == 0)
            {
                if (trigger.ActionTroop == null) return;

                trigger.ActionTroop.ChangeMorale(value);
            }
            else if (targetType == 1)
            {
                if (trigger.TargetTroop == null) return;

                trigger.TargetTroop.ChangeMorale(value);
            }

            
        }
    }
}
