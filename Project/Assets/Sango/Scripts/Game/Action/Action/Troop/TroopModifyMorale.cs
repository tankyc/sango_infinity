using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种类型战法的增减伤害  
    /// value： 增加值(百分比) , Execute为绝对值
    /// </summary>
    public class TroopModifyMorale : TroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger, params object[] sangoObjects)
        {
            if(trigger == null) return;
            if (trigger.ActionTroop != Troop) return;
            int srcValue = trigger.DamageOverride.Value;
            srcValue = srcValue * value / 10000;
            trigger.DamageOverride.Set(srcValue);
        }
    }
}
