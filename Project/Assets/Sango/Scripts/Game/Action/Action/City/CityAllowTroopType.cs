using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 兵种组建权限（作用于城市）。
    ///
    /// <para>
    /// 【玩法】武将携带本特技时，出征界面（<c>CityExpedition</c>）允许组建配置指定的兵种，
    /// 即使该兵种要求的科技（<see cref="TroopType.validTechId"/>）尚未研究、
    /// 或要求的兵符道具（<see cref="TroopType.validItemId"/>）不在上阵武将身上。
    /// 原有科技 / 兵符路径依旧有效，本特技只是**额外**开一条解锁路径，
    /// 不绕过兵装消耗、粮食 / 资金 / 技巧点等既有消耗。
    /// </para>
    ///
    /// <para>
    /// 【水陆配置】兵种按 <see cref="TroopType.isLand"/> 分两类，可分别配置：
    /// <code>
    /// {
    ///   "class": "CityAllowTroopType",
    ///   "landTroopTypeIds": [4, 5],     // 陆上兵种（弩兵、骑兵…）
    ///   "waterTroopTypeIds": [9, 10],   // 水上兵种（楼船、斗舰…）
    ///   "troopTypeIds": [15]            // 不限水陆（两者都可命中）
    /// }
    /// </code>
    /// 三组为**或**关系；<c>landTroopTypeIds</c> / <c>waterTroopTypeIds</c> 会与兵种的
    /// <c>isLand</c> 严格比对 —— 把陆上兵种配进 <c>waterTroopTypeIds</c> 不会生效，
    /// 查询侧会打一次警告，便于排查配置写错分组。
    /// </para>
    ///
    /// <para>
    /// 【生效场景】特技 <c>kind</c> 建议填 6（<see cref="FeatureKindType.CityProduce"/>）。
    /// 实际判定由 <see cref="FeaturePermission"/> 主动扫描武将生效特技完成，不依赖事件订阅，
    /// 因此本类只负责"参数解析 + 命中判定"，不订阅任何 GameEvent。
    /// </para>
    /// </summary>
    public class CityAllowTroopType : ActionBase
    {
        /// <summary>配置里的类名（查询侧按此匹配，避免拼写漂移）</summary>
        public const string ClassName = "CityAllowTroopType";

        /// <summary>陆上兵种 Id（仅对 isLand == true 生效）</summary>
        int[] landTroopTypeIds;

        /// <summary>水上兵种 Id（仅对 isLand == false 生效）</summary>
        int[] waterTroopTypeIds;

        /// <summary>不限水陆的兵种 Id</summary>
        int[] troopTypeIds;

        /// <summary>
        /// 解析配置参数。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">宿主对象（本类不使用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            landTroopTypeIds = ReadIntArray(p, "landTroopTypeIds");
            waterTroopTypeIds = ReadIntArray(p, "waterTroopTypeIds");
            troopTypeIds = ReadIntArray(p, "troopTypeIds");
        }

        /// <summary>
        /// 清理参数（Action 被移除时调用）。
        /// </summary>
        public override void Clear()
        {
            landTroopTypeIds = null;
            waterTroopTypeIds = null;
            troopTypeIds = null;
        }

        /// <summary>
        /// 事件驱动效果：本类无触发式效果，只有被动查询。
        /// </summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 判定某个兵种是否被本权限放开（实例口径）。
        /// </summary>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>放开返回 true</returns>
        public bool Allows(TroopType troopType)
        {
            if (troopType == null)
                return false;

            if (Contains(troopTypeIds, troopType.Id))
                return true;

            return troopType.isLand
                ? Contains(landTroopTypeIds, troopType.Id)
                : Contains(waterTroopTypeIds, troopType.Id);
        }

        /// <summary>
        /// 判定某个兵种是否被本权限放开（配置口径，查询侧直接读 JSON，不必实例化 Action）。
        /// </summary>
        /// <param name="node">actionEntities 里的一项</param>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>命中返回 true</returns>
        public static bool Allows(JObject node, TroopType troopType)
        {
            if (node == null || troopType == null)
                return false;

            if (Contains(ReadIntArray(node, "troopTypeIds"), troopType.Id))
                return true;

            return troopType.isLand
                ? Contains(ReadIntArray(node, "landTroopTypeIds"), troopType.Id)
                : Contains(ReadIntArray(node, "waterTroopTypeIds"), troopType.Id);
        }

        /// <summary>
        /// 该兵种 Id 是否被**配到了与自身水陆属性相反的分组**里。
        ///
        /// 【用途】只用于查询侧打一次配置警告。典型场景：把陆上兵种写进了
        /// <c>waterTroopTypeIds</c>，此时 <see cref="Allows(JObject, TroopType)"/> 恒为 false，
        /// 玩家只会看到"特技没效果"，没有这层提示很难定位。
        /// </summary>
        /// <param name="node">actionEntities 里的一项</param>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>配置与水陆属性相反返回 true</returns>
        public static bool IsGroupMismatch(JObject node, TroopType troopType)
        {
            if (node == null || troopType == null)
                return false;

            return troopType.isLand
                ? Contains(ReadIntArray(node, "waterTroopTypeIds"), troopType.Id)
                : Contains(ReadIntArray(node, "landTroopTypeIds"), troopType.Id);
        }

        /// <summary>
        /// 把配置里放开的兵种 Id 按水陆分组追加到目标容器（批量收集用）。
        ///
        /// 【平行数组】<paramref name="xxxFeatureIds"/> 与 <paramref name="xxxIds"/> 一一对应，
        /// 只用于"配错水陆分组"时定位来源特技；命中判定不读它。
        /// </summary>
        /// <param name="node">actionEntities 里的一项</param>
        /// <param name="featureId">来源特技 Id</param>
        /// <param name="anyIds">不限水陆 Id 目标容器</param>
        /// <param name="landIds">陆上 Id 目标容器</param>
        /// <param name="waterIds">水上 Id 目标容器</param>
        /// <param name="anyFeatureIds">与 <paramref name="anyIds"/> 对应的特技 Id 容器</param>
        /// <param name="landFeatureIds">与 <paramref name="landIds"/> 对应的特技 Id 容器</param>
        /// <param name="waterFeatureIds">与 <paramref name="waterIds"/> 对应的特技 Id 容器</param>
        public static void CollectAllowed(
            JObject node, int featureId,
            List<int> anyIds, List<int> landIds, List<int> waterIds,
            List<int> anyFeatureIds, List<int> landFeatureIds, List<int> waterFeatureIds)
        {
            if (node == null)
                return;

            AppendGroup(node.Value<JArray>("troopTypeIds"), featureId, anyIds, anyFeatureIds);
            AppendGroup(node.Value<JArray>("landTroopTypeIds"), featureId, landIds, landFeatureIds);
            AppendGroup(node.Value<JArray>("waterTroopTypeIds"), featureId, waterIds, waterFeatureIds);
        }

        /// <summary>
        /// 把 JSON 数组里尚未存在的 Id 追加到列表，并同步写入对应的来源特技 Id。
        ///
        /// 【零分配】直接遍历 JArray 取值，不用 <c>ToObject&lt;int[]&gt;()</c> ——
        /// 本方法在每次构建候选兵种列表（AI 每回合多次）时都会走到。
        /// </summary>
        /// <param name="array">来源 JSON 数组（可为 null）</param>
        /// <param name="featureId">来源特技 Id</param>
        /// <param name="ids">Id 目标列表（可为 null）</param>
        /// <param name="featureIds">特技 Id 目标列表（可为 null）</param>
        static void AppendGroup(JArray array, int featureId, List<int> ids, List<int> featureIds)
        {
            if (array == null || ids == null)
                return;

            for (int i = 0; i < array.Count; i++)
            {
                int id = array[i].Value<int>();
                if (ids.Contains(id))
                    continue;

                ids.Add(id);
                featureIds?.Add(featureId);
            }
        }

        /// <summary>
        /// 从 JSON 里读取 int 数组；缺失或格式不对时返回 null（调用方按"未配置"处理）。
        /// </summary>
        /// <param name="p">JSON 对象</param>
        /// <param name="key">键名</param>
        /// <returns>整数数组或 null</returns>
        static int[] ReadIntArray(JObject p, string key)
        {
            if (p == null)
                return null;

            JArray array = p.Value<JArray>(key);
            return array == null ? null : array.ToObject<int[]>();
        }

        /// <summary>
        /// 数组中是否包含目标值（null / 空数组一律视为不包含）。
        /// </summary>
        /// <param name="values">目标数组</param>
        /// <param name="target">目标值</param>
        /// <returns>包含返回 true</returns>
        static bool Contains(int[] values, int target)
        {
            if (values == null || values.Length == 0)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == target)
                    return true;
            }
            return false;
        }
    }
}
