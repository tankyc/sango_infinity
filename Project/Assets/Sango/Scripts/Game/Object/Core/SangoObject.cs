/*
 * 文件名：SangoObject.cs
 * 描述：游戏对象基类，所有游戏对象的基类，包含ID、名称、存活状态等基础属性
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Sango.Render;

namespace Sango.Core
{
    /// <summary>
    /// 游戏对象类型枚举，定义游戏中所有对象的类型
    /// </summary>
    public enum SangoObjectType : int
    {
        /// <summary>
        /// 未知类型
        /// </summary>
        Unknown = 0,
        /// <summary>
        /// 剧本
        /// </summary>
        Scenario,
        /// <summary>
        /// 武将
        /// </summary>
        Person,
        /// <summary>
        /// 势力
        /// </summary>
        Force,
        /// <summary>
        /// 部队
        /// </summary>
        Troops,
        /// <summary>
        /// 城市
        /// </summary>
        City,
        /// <summary>
        /// 建筑
        /// </summary>
        Building,
        /// <summary>
        /// 军团
        /// </summary>
        Corps,
        /// <summary>
        /// 技能
        /// </summary>
        Skill,
        /// <summary>
        /// 技能
        /// </summary>
        SkillInstance,
        /// <summary>
        /// 内部建筑
        /// </summary>
        InnerBuilding,
        /// <summary>
        /// 计略
        /// </summary>
        Strategy,
        /// <summary>
        /// 火焰
        /// </summary>
        Fire

    }

    /// <summary>
    /// 游戏对象基类，所有游戏对象（武将、城市、势力、部队等）的基类
    /// 提供ID、名称、存活状态、行动状态等基础属性和方法
    /// </summary>
    public class SangoObject
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public virtual SangoObjectType ObjectType { get { return SangoObjectType.Unknown; } }

        /// <summary>
        /// 对象ID
        /// </summary>
        private int _Id = -1;
        
        /// <summary>
        /// 对象ID，用于唯一标识游戏对象
        /// </summary>
        [JsonProperty(Order = -99)]
        public virtual int Id { get { return _Id; } set { _Id = value; } }

        /// <summary>
        /// 对象名称
        /// </summary>
        [JsonProperty(Order = -98)]
        public virtual string Name { get; set; }
        
        /// <summary>
        /// 对象标签
        /// </summary>
        public virtual string Tag { get; set; }

        /// <summary>
        /// 对象是否存活
        /// </summary>
        [JsonProperty]
        public virtual bool IsAlive { get; set; }

        /// <summary>
        /// 对象是否已行动完毕
        /// </summary>
        [JsonProperty]
        public virtual bool ActionOver { get; set; }

            /// <summary>
            /// 获取对象的渲染器
            /// </summary>
            /// <returns>对象渲染器</returns>
        public virtual ObjectRender GetRender() { return null; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SangoObject()
        {
            IsAlive = true;
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns>对象名称</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// 执行AI逻辑
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool DoAI(Scenario scenario) { return true; }

        /// <summary>
        /// 运行对象逻辑
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否运行成功</returns>
        public virtual bool Run(Scenario scenario) { return true; }

        /// <summary>
        /// 剧本准备时调用,在Init之前
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public virtual void OnScenarioPrepare(Scenario scenario) {; }

        /// <summary>
        /// 在保存的时候同步数据
        /// </summary>
        /// <param name="scenario"></param>
        public virtual void OnScenarioSave(Scenario scenario) {; }

        /// <summary>
        /// 剧本开始时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public virtual void OnScenarioStart(Scenario scenario) {; }

        /// <summary>
        /// 在势力开始时候调用
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual bool OnForceTurnStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在势力结束时候调用
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual bool OnForceTurnEnd(Scenario scenario) { return true; }

        /// <summary>
        /// 在回合开始时候调用
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual bool OnTurnStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在回合结束时候调用
        /// </summary>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public virtual bool OnTurnEnd(Scenario scenario) { return true; }


        /// <summary>
        /// 在天开始时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnDayStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在天结束时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnDayEnd(Scenario scenario) { return true; }

        /// <summary>
        /// 在月开始时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnMonthStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在月结束时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnMonthEnd(Scenario scenario) { return true; }

        /// <summary>
        /// 在年开始时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnYearStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在年结束时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnYearEnd(Scenario scenario) { return true; }

        /// <summary>
        /// 在季节开始时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnSeasonStart(Scenario scenario) { return true; }

        /// <summary>
        /// 在季节结束时调用
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否执行成功</returns>
        public virtual bool OnSeasonEnd(Scenario scenario) { return true; }

        /// <summary>
        /// 初始化对象,在prepare之后
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public virtual void Init(Scenario scenario) { }

        /// <summary>
        /// 清理对象
        /// </summary>
        public virtual void Clear() { }

        /// <summary>
        /// 判断两个势力是否为敌对关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>是否为敌对关系</returns>
        public bool IsEnemy(Force forceA, Force forceB)
        {
            if (forceA == null) return true;
            if (forceB == null) return true;
            return forceA != forceB && !forceA.IsAlliance(forceB);
        }

        /// <summary>
        /// 判断两个势力是否为同一势力
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>是否为同一势力</returns>
        public bool IsSameForce(Force forceA, Force forceB)
        {
            if (forceA == null) return false;
            if (forceB == null) return false;
            return forceA == forceB;
        }

        /// <summary>
        /// 判断两个势力是否为同盟关系
        /// </summary>
        /// <param name="forceA">势力A</param>
        /// <param name="forceB">势力B</param>
        /// <returns>是否为同盟关系</returns>
        public bool IsAlliance(Force forceA, Force forceB)
        {
            if (forceA == null) return false;
            if (forceB == null) return false;
            return forceA.IsAlliance(forceB);
        }

        /// <summary>
        /// 比较两个游戏对象的ID
        /// </summary>
        /// <param name="a">对象A</param>
        /// <param name="b">对象B</param>
        /// <returns>比较结果</returns>
        public static int Compare(SangoObject a, SangoObject b)
        {
            if (a != null && b != null)
            {
                return a.Id.CompareTo(b.Id);
            }

            if (a != null)
                return 1;

            if (b != null)
                return -1;

            return 0;
        }

    }


    /// <summary>
    /// 带扩展数据的游戏对象基类
    /// </summary>
    public class SangoObjectExtensionData : SangoObject
    {
        //[JsonExtensionData]
        //public Dictionary<string, JToken> AdditionalData { get; set; }
        /// <summary>
        /// 扩展数据字典，用于存储额外的数据
        /// </summary>
        [JsonExtensionData]
        private Dictionary<string, JToken> ExtensionData;



        /// <summary>
        /// 获取扩展数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">键名</param>
        /// <returns>数据值</returns>
        public T GetExtensionData<T>(string key) where T : struct
        {
            if (ExtensionData == null) return default(T);

            if (ExtensionData.TryGetValue(key, out var value))
            {
                return value.Value<T>();
            }
            return default(T);
        }

        /// <summary>
        /// 设置扩展数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="key">键名</param>
        /// <param name="value">数据值</param>
        public void SetExtensionData<T>(string key, T value) where T : struct
        {
            if (ExtensionData == null)
                ExtensionData = new Dictionary<string, JToken>();
            ExtensionData[key] = new JValue(value);
        }

        /// <summary>
        /// 获取扩展数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>JToken数据</returns>
        public JToken GetExtensionData(string key)
        {
            if (ExtensionData == null) return null;
            if (ExtensionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// 设置扩展数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <param name="value">JToken数据</param>
        public void SetExtensionData<T>(string key, JToken value)
        {
            if (ExtensionData == null)
                ExtensionData = new Dictionary<string, JToken>();
            ExtensionData[key] = value;
        }

        /// <summary>
        /// 检查是否包含指定键的扩展数据
        /// </summary>
        /// <param name="key">键名</param>
        /// <returns>是否包含</returns>
        public bool HasExtensionData(string key)
        {
            if (ExtensionData == null) return false;
            return ExtensionData.ContainsKey(key);
        }


    }
}
