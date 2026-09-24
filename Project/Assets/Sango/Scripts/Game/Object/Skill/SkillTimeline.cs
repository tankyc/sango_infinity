using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能时间轴
    /// 只存储初始化后的参数，由管理器管理
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class SkillTimeline
    {
        /// <summary>
        /// 时间轴事件列表
        /// </summary>
        [JsonProperty]
        public List<SkillTimelineEvent> events = new List<SkillTimelineEvent>();

        /// <summary>
        /// 时间轴总长度
        /// </summary>
        [JsonProperty]
        public float duration = 2.5f;

        /// <summary>
        /// 初始化时间轴
        /// </summary>
        /// <param name="timelineData"></param>
        public void Init(JObject timelineData)
        {
            if (timelineData == null) return;

            if (timelineData.TryGetValue("duration", out JToken durationToken))
            {
                duration = durationToken.Value<float>();
            }

            if (timelineData.TryGetValue("events", out JToken eventsToken))
            {
                JArray eventsArray = eventsToken as JArray;
                if (eventsArray != null)
                {
                    foreach (JToken eventToken in eventsArray)
                    {
                        SkillTimelineEvent timelineEvent = SkillTimelineEvent.Create(eventToken as JObject);
                        if (timelineEvent != null)
                        {
                            events.Add(timelineEvent);
                        }
                    }
                }
            }

            // 按时间顺序排序事件
            SortEvents();
        }

        /// <summary>
        /// 按时间顺序排序事件
        /// </summary>
        public void SortEvents()
        {
            events.Sort((a, b) => a.time.CompareTo(b.time));
        }
    }
}