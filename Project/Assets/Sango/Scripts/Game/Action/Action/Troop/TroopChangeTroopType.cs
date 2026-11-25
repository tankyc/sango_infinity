using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 兵种替换：把部队的兵种按配置映射换成另一个（特殊兵 / 兵圣）。
    ///
    /// <para>
    /// 【水陆分开配】一支部队同时拥有陆战兵种（<see cref="Troop.LandTroopType"/>）与
    /// 水战兵种（<see cref="Troop.WaterTroopType"/>），两者互相独立。
    /// 因此参数拆成 <c>landFrom/landTo</c> 与 <c>waterFrom/waterTo</c> 两组，各自一一对应。
    /// </para>
    /// <para>
    /// 【触发时机】挂 <see cref="GameEvent.OnTroopBeforeInitSkill"/>（属性重算的**最早期**）：
    /// 部队出征建队、进城、装卸装备、读档后重算属性时都会走到，天然覆盖"出场"这一刻，
    /// 不需要额外加一个"出场事件"。
    /// </para>
    /// <para>
    /// 【为什么不用 OnTroopBeforeCalculateAttribute】那个事件的派发位置在
    /// <see cref="Troop.CalculateAttribute"/> 末尾——兵种适性、战法列表、攻防移动力都已算完，
    /// 在那时换兵种只能等**下一次**重算才生效；而部队出征通常只重算一次，
    /// 表现为"特技完全不生效"（剑兵出征仍是剑兵）。改用重算开头的事件后，
    /// 替换结果会参与本次计算：适性按骑兵取、战法列表按骑兵重建、攻防与移动力按骑兵算。
    /// </para>
    /// <para>
    /// 【不会递归】替换后 <c>LandTroopTypeId</c> 已经等于目标 Id，不会再命中 <c>from</c> 列表，
    /// 所以下一次重算不会反复替换。替换本身用属性 setter 写，
    /// 会同时更新 Id 字段与 <c>mLandTroopType</c> 缓存，避免"Id 改了但缓存还是旧兵种"。
    /// </para>
    /// <para>
    /// 【配置示例】
    /// <code>
    /// { "class":"TroopChangeTroopType",
    ///   "landFrom":[1,2,3], "landTo":[21,22,23],
    ///   "waterFrom":[11],  "waterTo":[31] }
    /// </code>
    /// </para>
    /// </summary>
    public class TroopChangeTroopType : TroopActionBase
    {
        /// <summary>陆战：源兵种 Id 列表</summary>
        int[] landFrom;

        /// <summary>陆战：目标兵种 Id 列表（与 landFrom 一一对应）</summary>
        int[] landTo;

        /// <summary>水战：源兵种 Id 列表</summary>
        int[] waterFrom;

        /// <summary>水战：目标兵种 Id 列表（与 waterFrom 一一对应）</summary>
        int[] waterTo;

        /// <summary>已报过"目标兵种不存在"的 Id，避免每次重算刷屏</summary>
        readonly HashSet<int> reportedInvalid = new HashSet<int>();

        /// <summary>是否仅主将生效（1 = 副将不触发）</summary>
        int leaderOnly;

        /// <summary>装配来源武将（Feature.InitActions 传入，用于主将判定）</summary>
        Person sourcePerson;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：landFrom/landTo、waterFrom/waterTo（均为 int 数组，成对使用）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            landFrom = ParseArray(p, "landFrom");
            landTo = ParseArray(p, "landTo");
            waterFrom = ParseArray(p, "waterFrom");
            waterTo = ParseArray(p, "waterTo");
            leaderOnly = p.Value<int>("leaderOnly");

            // Feature.InitActions 传入的 sangoObjects 含装配武将（[1]），末尾追加 Feature
            for (int i = 0; i < sangoObjects.Length; i++)
            {
                if (sangoObjects[i] is Person person)
                {
                    sourcePerson = person;
                    break;
                }
            }

            bool hasLand = landFrom != null && landTo != null && landFrom.Length == landTo.Length;
            bool hasWater = waterFrom != null && waterTo != null && waterFrom.Length == waterTo.Length;

            if (!hasLand && !hasWater)
            {
                Sango.Log.Error("TroopChangeTroopType 的 landFrom/landTo 或 waterFrom/waterTo 未成对配置，将不会生效");
                return;
            }

            GameEvent.OnTroopBeforeInitSkill += OnTroopBeforeInitSkill;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopBeforeInitSkill -= OnTroopBeforeInitSkill;

            // 【清除兵装覆盖】Action 退订说明"这支部队已经不再拥有该特技"
            // （换将 / 拆特技 / 部队销毁）。不清的话，失去特技的骑兵部队会一直沿用
            // 剑兵的"不耗军马"口径。
            // 原始兵种 Id（originLandTroopTypeId）**不清**：InitActionList 重建后若特技仍在，
            // 重算时会靠它恢复覆盖值。
            if (Troop != null)
            {
                Troop.landCostItemsOverride = null;
                Troop.landCostItemsOverrideTargetId = 0;
                Troop.waterCostItemsOverride = null;
                Troop.waterCostItemsOverrideTargetId = 0;
            }
        }

        /// <summary>读数组参数</summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="key">键名</param>
        /// <returns>数组；未配置返回 null</returns>
        static int[] ParseArray(JObject p, string key)
        {
            JArray array = p.Value<JArray>(key);
            if (array == null || array.Count == 0)
                return null;
            int[] result = new int[array.Count];
            for (int i = 0; i < array.Count; i++)
                result[i] = array[i].ToObject<int>();
            return result;
        }

        /// <summary>
        /// 属性重算开始前做一次兵种替换（水陆各判一次）。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopBeforeInitSkill(Troop troop, Scenario scenario)
        {
            if (troop == null || scenario == null)
                return;

            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

            // 主将限制：副将携带的同名特技不生效（势力技法 sourcePerson 为空时不过滤）
            if (leaderOnly == 1 && sourcePerson != null && troop.Leader != sourcePerson)
                return;

            TryReplace(troop, scenario, landFrom, landTo, false);
            TryReplace(troop, scenario, waterFrom, waterTo, true);
        }

        /// <summary>
        /// 按映射表替换一侧兵种。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <param name="scenario">场景对象</param>
        /// <param name="from">源 Id 列表</param>
        /// <param name="to">目标 Id 列表</param>
        /// <param name="water">true 处理水战兵种</param>
        void TryReplace(Troop troop, Scenario scenario, int[] from, int[] to, bool water)
        {
            if (from == null || to == null || from.Length != to.Length)
                return;

            int current = water ? troop.WaterTroopTypeId : troop.LandTroopTypeId;
            if (current <= 0)
                return;

            // 【恢复分支】当前已经是替换后的目标兵种（读档 / 第二次及以后的重算）：
            // 此时 from 不再命中，若不做处理，覆盖值会丢失、退回按目标兵种扣军马。
            // 用入档的原始兵种 Id 反查消耗表并重建覆盖。
            int originId = water ? troop.originWaterTroopTypeId : troop.originLandTroopTypeId;
            if (originId > 0)
            {
                for (int i = 0; i < to.Length; i++)
                {
                    if (to[i] != current || from[i] != originId)
                        continue;

                    TroopType origin = scenario.GetObject<TroopType>(originId);
                    if (water)
                    {
                        troop.waterCostItemsOverride = origin != null ? origin.costItems : null;
                        troop.waterCostItemsOverrideTargetId = current;
                    }
                    else
                    {
                        troop.landCostItemsOverride = origin != null ? origin.costItems : null;
                        troop.landCostItemsOverrideTargetId = current;
                    }
                    return;
                }
            }

            for (int i = 0; i < from.Length; i++)
            {
                if (from[i] != current)
                    continue;

                TroopType target = scenario.GetObject<TroopType>(to[i]);
                if (target == null)
                {
                    // 同一个错误 Id 只报一次：属性重算在回合内会被多次触发
                    if (reportedInvalid.Add(to[i]))
                        Sango.Log.Error($"TroopChangeTroopType：目标兵种 Id={to[i]} 不存在，已跳过该条映射");
                    return;
                }

                if (water)
                {
                    // 【兵装口径】保存替换前的消耗表：出征扣装 / 入城归还 / 界面显示统一读
                    // Troop.WaterCostItems，保证"扣的和还的是同一份"，也避免目标兵种
                    // 需要兵装而城里没有时把可出兵数夹成 0（特技等于不可用）。
                    // 同时记下目标兵种 Id 与原始兵种 Id：前者决定覆盖值是否仍然有效，
                    // 后者让读档后的重算能反推"原本是哪个兵种"。
                    troop.waterCostItemsOverride = troop.WaterTroopType != null ? troop.WaterTroopType.costItems : null;
                    troop.waterCostItemsOverrideTargetId = target.Id;
                    troop.originWaterTroopTypeId = current;
                    troop.WaterTroopType = target;
                }
                else
                {
                    troop.landCostItemsOverride = troop.LandTroopType != null ? troop.LandTroopType.costItems : null;
                    troop.landCostItemsOverrideTargetId = target.Id;
                    troop.originLandTroopTypeId = current;
                    troop.LandTroopType = target;
                }
                return;
            }
        }
    }
}
