using Sango.Core.Tools;
using System.Collections.Generic;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队忽略ZOC
    /// land: 0 忽略水上 1 忽略陆地 2都忽略
    /// </summary>
    public class TroopTriggerAction : TroopActionBase
    {
        List<Trigger> triggerList;
        List<ActionBase> actionList;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            // 初始化触发器
            JArray triggerArray = p.Value<JArray>("triggerList");
            if (triggerArray != null)
            {
                triggerList = new List<Trigger>(triggerArray.Count);
                for (int i = 0; i < triggerArray.Count; i++)
                {
                    JObject valus = triggerArray[i] as JObject;
                    Trigger action = Trigger.Create(valus.Value<string>("class"));
                    if (action != null)
                    {
                        action.Init(OnTrigger, sangoObjects);
                        triggerList.Add(action);
                    }
                }
            }

            // 初始化效果
            JArray actionArray = p.Value<JArray>("actionList");
            if (actionArray != null)
            {
                actionList = new List<ActionBase>(actionArray.Count);
                for (int i = 0; i < actionArray.Count; i++)
                {
                    JObject valus = actionArray[i] as JObject;
                    ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                    if (action != null)
                    {
                        action.Init(valus, sangoObjects);
                        actionList.Add(action);
                    }
                }
            }
        }

        public override void Clear()
        {
            if (triggerList != null)
            {
                foreach (var obj in triggerList)
                {
                    obj.Clear();
                }
            }

            if (actionList != null)
            {
                foreach (var obj in actionList)
                {
                    obj.Clear();
                }
            }
        }

        public virtual void OnTrigger(Trigger trigger, params object[] sangoObjects)
        {
            if (actionList != null)
            {
                foreach (var obj in actionList)
                {
                    obj.Execute(trigger, sangoObjects);
                }
            }
        }


    }
}
