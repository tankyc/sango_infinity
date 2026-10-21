using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按value修改气力获取  
    /// value： 增加值(万分比) 
    /// modifyType: 0只改变增加值 1只改变减少值
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class TroopModifyMorale : TroopActionBase
    {
        int modifyType;
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            modifyType = p.Value<int>("modifyType");
        }

        public override void Clear()
        {

        }

        public override void Execute(Trigger trigger)
        {
            if(trigger == null) return;
            if (trigger.ActionTroop != Troop) return;
            int srcValue = trigger.ValueOverride.Value;
            switch(modifyType)
            {
                case 0:
                    if(srcValue > 0)
                    {
                        srcValue = srcValue * value / 10000;
                        trigger.ValueOverride.Set(srcValue);
                    }
                    break;
                case 1:
                    if (srcValue < 0)
                    {
                        srcValue = srcValue * value / 10000;
                        trigger.ValueOverride.Set(srcValue);
                    }
                    break;
            }
        }
    }
}
