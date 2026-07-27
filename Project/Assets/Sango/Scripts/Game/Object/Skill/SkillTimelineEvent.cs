using Sango.Manager;
using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能时间轴事件类型
    /// </summary>
    public enum SkillTimelineEventType
    {
        /// <summary>
        /// 播放动画
        /// </summary>
        PlayAnimation,
        /// <summary>
        /// 播放特效
        /// </summary>
        PlayEffect,
        /// <summary>
        /// 播放音效
        /// </summary>
        PlaySound,
        /// <summary>
        /// 执行伤害
        /// </summary>
        ExecuteDamage,
        /// <summary>
        /// 执行位移
        /// </summary>
        ExecuteOffset,
        /// <summary>
        /// 执行效果
        /// </summary>
        ExecuteEffect,
        /// <summary>
        /// 显示文本
        /// </summary>
        ShowText,
        /// <summary>
        /// 相机抖动
        /// </summary>
        CameraShake
    }

    /// <summary>
    /// 技能时间轴事件基类
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class SkillTimelineEvent
    {
        /// <summary>
        /// 事件时间点
        /// </summary>
        [JsonProperty]
        public float time;

        /// <summary>
        /// 事件类型
        /// </summary>
        [JsonProperty]
        public string type;

        /// <summary>
        /// 初始化事件
        /// </summary>
        /// <param name="eventData"></param>
        public virtual void Init(JObject eventData)
        {
            if (eventData == null) return;

            if (eventData.TryGetValue("time", out JToken timeToken))
            {
                time = timeToken.Value<float>();
            }

            if (eventData.TryGetValue("type", out JToken typeToken))
            {
                type = typeToken.Value<string>();
            }
        }

        /// <summary>
        /// 执行事件
        /// </summary>
        /// <param name="skillInstance">技能实例</param>
        /// <param name="troop">释放技能的部队</param>
        /// <param name="spellCell">施法目标单元格</param>
        public abstract void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell);

        /// <summary>
        /// 获取事件类型枚举
        /// </summary>
        /// <returns></returns>
        public SkillTimelineEventType GetEventType()
        {
            SkillTimelineEventType eventType;
            if (Enum.TryParse<SkillTimelineEventType>(type, true, out eventType))
            {
                return eventType;
            }
            return SkillTimelineEventType.PlayEffect;
        }

        /// <summary>
        /// 创建事件实例
        /// </summary>
        /// <param name="eventData">事件数据</param>
        /// <returns>事件实例</returns>
        public static SkillTimelineEvent Create(JObject eventData)
        {
            if (eventData == null) return null;

            string type = eventData.Value<string>("type");
            if (string.IsNullOrEmpty(type)) return null;

            SkillTimelineEvent timelineEvent = null;
            switch (type)
            {
                case "PlayAnimation":
                    timelineEvent = new SkillTimelineEvent_PlayAnimation();
                    break;
                case "PlayEffect":
                    timelineEvent = new SkillTimelineEvent_PlayEffect();
                    break;
                case "PlaySound":
                    timelineEvent = new SkillTimelineEvent_PlaySound();
                    break;
                case "ExecuteDamage":
                    timelineEvent = new SkillTimelineEvent_ExecuteDamage();
                    break;
                case "ExecuteOffset":
                    timelineEvent = new SkillTimelineEvent_ExecuteOffset();
                    break;
                case "ExecuteEffect":
                    timelineEvent = new SkillTimelineEvent_ExecuteEffect();
                    break;
                case "ShowText":
                    timelineEvent = new SkillTimelineEvent_ShowText();
                    break;
                case "CameraShake":
                    timelineEvent = new SkillTimelineEvent_CameraShake();
                    break;
            }

            if (timelineEvent != null)
            {
                timelineEvent.Init(eventData);
            }

            return timelineEvent;
        }
    }

    /// <summary>
    /// 播放动画事件
    /// </summary>
    public class SkillTimelineEvent_PlayAnimation : SkillTimelineEvent
    {
        public string animationName;

        public override void Init(JObject eventData)
        {
            base.Init(eventData);
            if (eventData.TryGetValue("parameters", out JToken parametersToken))
            {
                JObject parameters = parametersToken as JObject;
                if (parameters != null && parameters.TryGetValue("animationName", out JToken animationNameToken))
                {
                    animationName = animationNameToken.Value<string>();
                }
            }
        }

        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 播放动画逻辑
            // troop.Render.PlayAnimation(animationName);
        }
    }

    /// <summary>
    /// 播放特效事件
    /// </summary>
    public class SkillTimelineEvent_PlayEffect : SkillTimelineEvent
    {
        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 播放特效逻辑
            List<Cell> atkCellList = new List<Cell>();
            skillInstance.GetAttackCells(troop, spellCell, atkCellList);
            skillInstance.PlaySkillVisual(troop, spellCell, atkCellList);
        }
    }

    /// <summary>
    /// 播放音效事件
    /// </summary>
    public class SkillTimelineEvent_PlaySound : SkillTimelineEvent
    {
        public int soundId;

        public override void Init(JObject eventData)
        {
            base.Init(eventData);
            if (eventData.TryGetValue("parameters", out JToken parametersToken))
            {
                JObject parameters = parametersToken as JObject;
                if (parameters != null && parameters.TryGetValue("soundId", out JToken soundNameToken))
                {
                    soundId = soundNameToken.Value<int>();
                }
            }
        }

        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 播放音效逻辑
           GameMedia.Instance.PlaySfx(soundId);
        }
    }

    /// <summary>
    /// 执行伤害事件
    /// </summary>
    public class SkillTimelineEvent_ExecuteDamage : SkillTimelineEvent
    {
        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 执行伤害逻辑
            int criticalFactor = skillInstance.CheckCritical(spellCell);
            skillInstance.Action(spellCell, criticalFactor);
        }
    }

    /// <summary>
    /// 执行位移事件
    /// </summary>
    public class SkillTimelineEvent_ExecuteOffset : SkillTimelineEvent
    {
        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 执行位移逻辑
            Troop targetTroop = spellCell.troop;
            skillInstance.DoOffset(troop, targetTroop, 0);
        }
    }

    /// <summary>
    /// 执行效果事件
    /// </summary>
    public class SkillTimelineEvent_ExecuteEffect : SkillTimelineEvent
    {
        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 执行效果逻辑
            List<Cell> atkCellList = new List<Cell>();
            skillInstance.GetAttackCells(troop, spellCell, atkCellList);
            skillInstance.DoEffect(troop, spellCell, atkCellList);
        }
    }

    /// <summary>
    /// 显示文本事件
    /// </summary>
    public class SkillTimelineEvent_ShowText : SkillTimelineEvent
    {
        public string text;

        public override void Init(JObject eventData)
        {
            base.Init(eventData);
            if (eventData.TryGetValue("parameters", out JToken parametersToken))
            {
                JObject parameters = parametersToken as JObject;
                if (parameters != null && parameters.TryGetValue("text", out JToken textToken))
                {
                    text = textToken.Value<string>();
                }
            }
        }

        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 显示文本逻辑
            // UIManager.Instance.ShowSkillText(text, spellCell.Position);
        }
    }

    /// <summary>
    /// 相机抖动事件
    /// </summary>
    public class SkillTimelineEvent_CameraShake : SkillTimelineEvent
    {
        public float intensity = 1.0f;
        public float duration = 0.5f;

        public override void Init(JObject eventData)
        {
            base.Init(eventData);
            if (eventData.TryGetValue("parameters", out JToken parametersToken))
            {
                JObject parameters = parametersToken as JObject;
                if (parameters != null)
                {
                    if (parameters.TryGetValue("intensity", out JToken intensityToken))
                    {
                        intensity = intensityToken.Value<float>();
                    }
                    if (parameters.TryGetValue("duration", out JToken durationToken))
                    {
                        duration = durationToken.Value<float>();
                    }
                }
            }
        }

        public override void Execute(SkillInstance skillInstance, Troop troop, Cell spellCell)
        {
            // 相机抖动逻辑
            // CameraManager.Instance.ShakeCamera(intensity, duration);
        }
    }
}