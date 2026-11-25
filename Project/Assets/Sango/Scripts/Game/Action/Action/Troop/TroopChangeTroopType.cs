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
    /// 【触发时机】挂 <see cref="GameEvent.OnTroopBeforeCalculateAttribute"/>：
    /// 部队出征建队、进城、装卸装备、读档后重算属性时都会走到，天然覆盖"出场"这一刻，
    /// 不需要额外加一个"出场事件"。
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

            bool hasLand = landFrom != null && landTo != null && landFrom.Length == landTo.Length;
            bool hasWater = waterFrom != null && waterTo != null && waterFrom.Length == waterTo.Length;

            if (!hasLand && !hasWater)
            {
                Sango.Log.Error("TroopChangeTroopType 的 landFrom/landTo 或 waterFrom/waterTo 未成对配置，将不会生效");
                return;
            }

            GameEvent.OnTroopBeforeCalculateAttribute += OnTroopBeforeCalculateAttribute;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopBeforeCalculateAttribute -= OnTroopBeforeCalculateAttribute;
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
        /// 属性重算前做一次兵种替换（水陆各判一次）。
        /// </summary>
        /// <param name="troop">正在重算的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopBeforeCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (troop == null || scenario == null)
                return;

            if (Force != null && troop.BelongForce != Force) return;
            if (Troop != null && Troop != troop) return;

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
                    troop.WaterTroopType = target;
                else
                    troop.LandTroopType = target;
                return;
            }
        }
    }
}
