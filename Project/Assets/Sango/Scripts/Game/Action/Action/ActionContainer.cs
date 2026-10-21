using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 触发器容器内容（宿主无关）。
    ///
    /// 负责：解析 triggerList / actionList、把触发器接到派发回调上、清理、以及触发时依次执行效果。
    /// TroopTriggerAction / BuildingTriggerAction / CityTriggerAction 都复用本类，
    /// 这样"某事件 → 执行一串效果"的玩法可以同时用在部队、建筑、城市上。
    ///
    /// JSON 结构：
    ///   triggerList : [ { "class": "TriggerXxx", ... }, ... ]
    ///   actionList  : [ { "class": "TroopRecure", ... }, ... ]
    /// </summary>
    public class ActionContainer
    {
        /// <summary>容器内的触发器</summary>
        List<Trigger> triggerList;
        /// <summary>容器内的效果</summary>
        List<ActionBase> actionList;

        /// <summary>
        /// 解析并装配容器内容
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">宿主对象（部队 / 建筑 / 城市…）</param>
        public void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (p == null)
                return;

            // 初始化触发器
            JArray triggerArray = p.Value<JArray>("triggerList");
            if (triggerArray != null)
            {
                triggerList = new List<Trigger>(triggerArray.Count);
                for (int i = 0; i < triggerArray.Count; i++)
                {
                    JObject node = triggerArray[i] as JObject;
                    if (node == null)
                        continue;

                    string className = node.Value<string>("class");
                    Trigger trigger = Trigger.Create(className);
                    if (trigger == null)
                    {
                        Sango.Log.Warning($"[ActionContainer] 未注册的触发器 class：{className}");
                        continue;
                    }

                    trigger.Init(Dispatch, sangoObjects);
                    triggerList.Add(trigger);
                }
            }

            // 初始化效果
            JArray actionArray = p.Value<JArray>("actionList");
            if (actionArray != null)
            {
                actionList = new List<ActionBase>(actionArray.Count);
                for (int i = 0; i < actionArray.Count; i++)
                {
                    JObject node = actionArray[i] as JObject;
                    if (node == null)
                        continue;

                    string className = node.Value<string>("class");
                    ActionBase action = ActionBase.Create(className);
                    if (action == null)
                    {
                        Sango.Log.Warning($"[ActionContainer] 未注册的效果 class：{className}");
                        continue;
                    }

                    action.Init(node, sangoObjects);
                    actionList.Add(action);
                }
            }
        }

        /// <summary>
        /// 清理容器：先退订所有触发器，再清理子效果
        /// </summary>
        public void Clear()
        {
            if (triggerList != null)
            {
                for (int i = 0; i < triggerList.Count; i++)
                    triggerList[i].Clear();
                triggerList.Clear();
            }

            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();
                actionList.Clear();
            }
        }

        /// <summary>
        /// 触发器回调：依次执行容器内的效果
        /// </summary>
        /// <param name="trigger">触发事件</param>
        public void Dispatch(Trigger trigger)
        {
            if (trigger == null || actionList == null)
                return;

            for (int i = 0; i < actionList.Count; i++)
                actionList[i].Execute(trigger);
        }
    }
}
