/*
 * 文件名：EventEffectBase.cs
 * 描述：事件效果基类 + 效果注册工厂 + 编辑器元数据特性
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件效果的编辑器元数据。
    /// 事件编辑器靠反射读它自动生成表单，因此**新增一个效果不需要改编辑器代码**。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class EventEffectMetaAttribute : Attribute
    {
        /// <summary>中文名（编辑器下拉列表里显示）</summary>
        public string Name;

        /// <summary>说明（编辑器 tooltip）</summary>
        public string Description;

        /// <summary>参数名列表（编辑器据此生成对应输入框）</summary>
        public string[] Params;

        /// <summary>构造</summary>
        /// <param name="name">中文名</param>
        /// <param name="description">说明</param>
        /// <param name="params">参数名列表</param>
        public EventEffectMetaAttribute(string name, string description, params string[] @params)
        {
            Name = name;
            Description = description;
            Params = @params;
        }
    }

    /// <summary>
    /// 事件效果基类。
    ///
    /// 与既有的 <see cref="Sango.Core.Action.ActionBase"/> 的区别：
    /// ActionBase 是"战法触发链"的一部分，Execute 签名带 Trigger，语义是"战斗中被动生效"；
    /// 事件效果是"结算时主动改世界"，参数来自事件 JSON、目标是角色槽绑定对象。
    /// 两者职责不同，因此不强行复用（避免把事件语义塞进战法链路）。
    ///
    /// 【失败不中断】单个效果失败只记日志并返回 false，由调用方决定是否继续执行后续效果；
    /// 绝不抛异常打断整个事件——玩家不该因为一个配置笔误而卡死。
    /// </summary>
    public abstract class EventEffectBase
    {
        /// <summary>本次效果的参数（来自事件 JSON 的 Params）</summary>
        protected JObject parameters;

        /// <summary>
        /// 编辑器显示用的中文名。默认取 <see cref="EventEffectMetaAttribute"/>，没有则用类名。
        /// </summary>
        public virtual string DisplayName
        {
            get
            {
                EventEffectMetaAttribute meta = GetMeta();
                return meta != null && !string.IsNullOrEmpty(meta.Name) ? meta.Name : GetType().Name;
            }
        }

        /// <summary>取本效果类上的元数据特性</summary>
        /// <returns>元数据；没有返回 null</returns>
        public EventEffectMetaAttribute GetMeta()
        {
            object[] attrs = GetType().GetCustomAttributes(typeof(EventEffectMetaAttribute), false);
            return attrs != null && attrs.Length > 0 ? attrs[0] as EventEffectMetaAttribute : null;
        }

        /// <summary>
        /// 用事件 JSON 里的参数初始化效果。
        /// 这里**只做解析与缓存**，不做表达式求值——求值放到 <see cref="Execute"/>，
        /// 这样 "#变量" 读到的是执行那一刻的值，而不是初始化时的快照。
        /// </summary>
        /// <param name="p">参数对象（即 EventEffectNode.Params）</param>
        public virtual void Init(JObject p)
        {
            parameters = p;
        }

        /// <summary>
        /// 执行效果。
        /// </summary>
        /// <param name="context">事件运行时上下文（角色槽绑定在这里）</param>
        /// <returns>是否执行成功</returns>
        public abstract bool Execute(EventContext context);

        #region 参数读取 helper

        /// <summary>读字符串参数</summary>
        /// <param name="name">参数名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>参数值</returns>
        protected string GetString(string name, string defaultValue = null)
        {
            if (parameters == null) return defaultValue;
            JToken token = parameters[name];
            if (token == null || token.Type == JTokenType.Null) return defaultValue;
            return token.Value<string>();
        }

        /// <summary>读布尔参数</summary>
        /// <param name="name">参数名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>参数值</returns>
        protected bool GetBool(string name, bool defaultValue = false)
        {
            if (parameters == null) return defaultValue;
            JToken token = parameters[name];
            if (token == null || token.Type == JTokenType.Null) return defaultValue;
            try
            {
                return token.Value<bool>();
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>求值一个数值参数（支持 GainPlace / #变量 / $槽位.属性）</summary>
        /// <param name="name">参数名</param>
        /// <param name="context">事件上下文</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>求值结果</returns>
        protected int EvalInt(string name, EventContext context, int defaultValue = 0)
        {
            if (parameters == null) return defaultValue;
            return EventValue.EvalInt(parameters[name], context, defaultValue);
        }

        /// <summary>读并替换文本参数里的占位符</summary>
        /// <param name="name">参数名</param>
        /// <param name="context">事件上下文</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>替换后的文本</returns>
        protected string EvalText(string name, EventContext context, string defaultValue = null)
        {
            string raw = GetString(name, defaultValue);
            return raw != null ? EventValue.EvalText(raw, context) : null;
        }

        /// <summary>取槽位对象</summary>
        /// <param name="name">参数名（该参数的值是槽位 Key）</param>
        /// <param name="context">事件上下文</param>
        /// <returns>绑定对象；无返回 null</returns>
        protected SangoObject GetSlot(string name, EventContext context)
        {
            return context != null ? context.GetSlot(GetString(name)) : null;
        }

        /// <summary>取指定类型的槽位对象</summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="name">参数名</param>
        /// <param name="context">事件上下文</param>
        /// <returns>绑定对象；无或类型不符返回 null</returns>
        protected T GetSlot<T>(string name, EventContext context) where T : SangoObject
        {
            return context != null ? context.GetSlot<T>(GetString(name)) : null;
        }

        /// <summary>记录一条"槽位为空导致效果跳过"的中文警告（统一格式，便于排查）</summary>
        /// <param name="name">参数名</param>
        /// <param name="slotKey">槽位 Key</param>
        protected void WarnSlotMissing(string name, string slotKey)
        {
            Log.Warning($"事件效果 {DisplayName} 跳过了：参数 {name} 指向的槽位 {slotKey} 未绑定");
        }

        #endregion

        #region 工厂

        /// <summary>效果创建委托</summary>
        public delegate EventEffectBase EffectCreator();

        /// <summary>效果创建映射表（名字 → 创建器）</summary>
        public static Dictionary<string, EffectCreator> CreateMap = new Dictionary<string, EffectCreator>();

        /// <summary>注册一个效果类型</summary>
        /// <param name="name">效果名（事件 JSON 里的 Type）</param>
        /// <param name="creator">创建器</param>
        public static void Register(string name, EffectCreator creator)
        {
            CreateMap[name] = creator;
        }

        /// <summary>创建效果实例的通用辅助（供 Register 时传方法组）</summary>
        /// <typeparam name="T">效果类型</typeparam>
        /// <returns>新实例</returns>
        public static EventEffectBase CreateHandle<T>() where T : EventEffectBase, new()
        {
            return new T();
        }

        /// <summary>按名字创建效果实例</summary>
        /// <param name="name">效果名</param>
        /// <returns>效果实例；未注册返回 null</returns>
        public static EventEffectBase Create(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return CreateMap.TryGetValue(name, out EffectCreator creator) ? creator() : null;
        }

        /// <summary>
        /// 按效果节点创建并初始化效果实例。
        /// </summary>
        /// <param name="node">效果节点</param>
        /// <returns>效果实例；类型未注册返回 null</returns>
        public static EventEffectBase Create(EventEffectNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Type)) return null;

            EventEffectBase effect = Create(node.Type);
            if (effect == null)
            {
                Log.Warning($"事件效果类型未注册：{node.Type}");
                return null;
            }

            effect.Init(node.Params);
            return effect;
        }

        /// <summary>
        /// 注册所有内置效果。
        /// 由 ScenarioEventManager.Init() 调用（在 GameSystemManager.Init 阶段，
        /// 早于任何剧本加载，因此注册表在使用前一定是完整的）。
        ///
        /// 命名刻意与实例方法 <see cref="Init"/> 区分（InitAll 而不是 Init），
        /// 避免静态 / 实例同名带来的可读性问题。
        /// </summary>
        public static void InitAll()
        {
            EventEffectsFlow.RegisterAll();
            EventEffectsPerson.RegisterAll();
            EventEffectsCity.RegisterAll();
            EventEffectsWorld.RegisterAll();
        }

        #endregion
    }
}
