using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 武将特技槽位的解锁条件（一条配置 = 一个槽位 + 一组条件）。
    ///
    /// 【配置位置】武将 JSON 的 <c>FeatureUnlockConditions</c> 数组，例：
    /// <code>
    /// "FeatureUnlockConditions": [
    ///   { "slot": 0, "desc": "出仕即拥有" },
    ///   { "slot": 1, "desc": "达到 5 级",
    ///     "conditions": { "class": "PersonLevelCheck", "level": 5, "operator": "gte" } },
    ///   { "slot": 2, "desc": "统率 80 且 5 级",
    ///     "conditions": { "class": "andList", "list": [
    ///         { "class": "PersonAttributeCheck", "attType": "command", "level": 80, "operator": "gte" },
    ///         { "class": "PersonLevelCheck", "level": 5, "operator": "gte" } ] } }
    /// ]
    /// </code>
    ///
    /// 【语义】
    ///   · 只要武将为某个槽位配了这一条，该槽就**只看配置**，不再参考剧本参数
    ///     <see cref="ScenarioVariables.skillSlotUnlockLevels"/> 的等级门槛；
    ///   · 武将未给该槽位配条件时，回落剧本参数（按武将等级判定）；
    ///   · 配了 <c>slot</c> 但没配 <c>conditions</c> = 该槽无条件解锁（常用于"第 1 槽生来就有"）。
    ///
    /// 【为什么条件不直接标 [JsonProperty] 反序列化】项目里 <see cref="Condition"/> 一律走
    /// <see cref="Condition.Create(string)"/> 工厂 + <see cref="Condition.Init(JObject, SangoObject[])"/>，
    /// 直接反序列化拿不到注册表里的实现类（TroopType / CityLevelType 都因此注释掉了同名字段）。
    /// </summary>
    public class FeatureUnlockCondition
    {
        /// <summary>槽位下标（0 起，对应 <see cref="Person.FeatureList"/> 的下标）</summary>
        public int slot;

        /// <summary>条件对象，可为 null（表示该槽无条件解锁）</summary>
        public Condition condition;

        /// <summary>
        /// 条件说明文本（可选，配置里的 <c>desc</c>）。
        /// 用于界面展示"如何解锁"；不配时界面会回落到"达到 N 级解锁"。
        /// </summary>
        public string desc;

        /// <summary>
        /// 配置无效（写错了条件类名）。按"永不满足"处理，避免写错反而白送解锁。
        /// </summary>
        public bool invalid;

        /// <summary>
        /// 解析一条配置。
        /// </summary>
        /// <param name="obj">配置对象（含 slot / desc / conditions）</param>
        /// <param name="sangoObjects">初始化条件时透传的相关对象</param>
        /// <returns>解析成功返回 true；对象为空或 slot 非法返回 false</returns>
        public bool Init(JObject obj, params SangoObject[] sangoObjects)
        {
            if (obj == null)
                return false;

            slot = obj.Value<int?>("slot") ?? 0;
            if (slot < 0)
                return false;

            desc = obj.Value<string>("desc");

            JObject conditionObject = obj.Value<JObject>("conditions");
            if (conditionObject == null)
                return true;   // 只配了槽位 = 无条件解锁

            string className = conditionObject.Value<string>("class");
            condition = Condition.Create(className);
            if (condition == null)
            {
                invalid = true;
                Sango.Log.Error($"特技解锁条件中存在未注册的条件类：{className}（槽位 {slot}），该槽位将始终不可解锁");
                return true;
            }

            condition.Init(conditionObject, sangoObjects);
            return true;
        }

        /// <summary>
        /// 判定该槽位对指定武将是否已解锁。
        /// </summary>
        /// <param name="person">待判定的武将</param>
        /// <returns>已解锁返回 true</returns>
        public bool Check(Person person)
        {
            if (person == null || invalid)
                return false;

            // 没配条件 = 无条件解锁
            if (condition == null)
                return true;

            DefaultConditionDatabase database = new DefaultConditionDatabase();
            database.ActionPerson = person;
            return condition.Check(database);
        }
    }
}
