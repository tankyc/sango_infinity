using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 道具制造权限（作用于城市）。
    ///
    /// <para>
    /// 【玩法】武将携带本特技时，其所在城市可以在锻造厂（铁匠铺）、工坊、造船厂中
    /// 制造配置指定的道具 —— 即使势力尚未研究该道具要求的科技
    /// （<see cref="ItemType.validTechId"/>）。原有科技路径依旧有效，本特技只是**额外**开一条解锁路径，
    /// 不会绕过库存上限、资金消耗、生产工期等其他限制。
    /// </para>
    ///
    /// <para>
    /// 【配置】<c>Features.json</c> 的 <c>actionEntities</c>：
    /// <code>
    /// {
    ///   "class": "CityAllowCreateItem",
    ///   "itemIds": [11, 12],      // 精确放开某几个道具 Id（ItemTypes.json 的 Id）
    ///   "itemKinds": [4],         // 放开某个大类（ItemKindType：1 兵器 / 2 马 / 3 器械 / 4 船）
    ///   "storeKinds": [8]         // 放开某个仓储槽位（ItemStoreKindType）
    /// }
    /// </code>
    /// 三个条件为**或**关系，任一命中即算放开；全部不填等于没有效果。
    /// </para>
    ///
    /// <para>
    /// 【生效场景】特技 <c>kind</c> 建议填 6（<see cref="FeatureKindType.CityProduce"/>）。
    /// 实际判定由 <see cref="FeaturePermission"/> 主动扫描武将生效特技完成，不依赖事件订阅，
    /// 因此本类只负责"参数解析 + 命中判定"，不订阅任何 GameEvent。
    /// </para>
    /// </summary>
    public class CityAllowCreateItem : ActionBase
    {
        /// <summary>配置里的类名（查询侧按此匹配，避免拼写漂移）</summary>
        public const string ClassName = "CityAllowCreateItem";

        /// <summary>精确放开的道具 Id</summary>
        int[] itemIds;

        /// <summary>放开的大类（<see cref="ItemKindType"/>）</summary>
        int[] itemKinds;

        /// <summary>放开的仓储槽位（<see cref="ItemStoreKindType"/>）</summary>
        int[] storeKinds;

        /// <summary>
        /// 解析配置参数。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">宿主对象（本类不使用）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            itemIds = ReadIntArray(p, "itemIds");
            itemKinds = ReadIntArray(p, "itemKinds");
            storeKinds = ReadIntArray(p, "storeKinds");
        }

        /// <summary>
        /// 清理参数（Action 被移除时调用）。
        /// </summary>
        public override void Clear()
        {
            itemIds = null;
            itemKinds = null;
            storeKinds = null;
        }

        /// <summary>
        /// 事件驱动效果：本类无触发式效果，只有被动查询。
        /// </summary>
        /// <param name="trigger">触发器</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 判定某个道具是否被本权限放开（实例口径）。
        /// </summary>
        /// <param name="itemType">待判定的道具类型</param>
        /// <returns>放开返回 true</returns>
        public bool Allows(ItemType itemType)
        {
            if (itemType == null)
                return false;

            return Contains(itemIds, itemType.Id)
                || Contains(itemKinds, itemType.kind)
                || Contains(storeKinds, itemType.storeKind);
        }

        /// <summary>
        /// 判定某个道具是否被本权限放开（配置口径，查询侧直接读 JSON，不必实例化 Action）。
        /// </summary>
        /// <param name="node">actionEntities 里的一项</param>
        /// <param name="itemType">待判定的道具类型</param>
        /// <returns>命中返回 true</returns>
        public static bool Allows(JObject node, ItemType itemType)
        {
            if (node == null || itemType == null)
                return false;

            return Contains(ReadIntArray(node, "itemIds"), itemType.Id)
                || Contains(ReadIntArray(node, "itemKinds"), itemType.kind)
                || Contains(ReadIntArray(node, "storeKinds"), itemType.storeKind);
        }

        /// <summary>
        /// 把配置里放开的道具 Id / 大类 / 仓储槽位追加到目标容器（查询侧一次性收集用）。
        ///
        /// 【去重】同一特技被多个武将携带、或武将同时持有自带与装备两份时，
        /// 同一组权限会被重复读入；这里做去重，避免集合无限膨胀。
        ///
        /// 【零分配】直接遍历 JArray 取值，不用 <c>ToObject&lt;int[]&gt;()</c> ——
        /// 后者每次调用都会 new 一个临时数组，而本方法在每次打开生产列表时都会被调用。
        /// </summary>
        /// <param name="node">actionEntities 里的一项</param>
        /// <param name="itemIds">道具 Id 目标容器</param>
        /// <param name="itemKinds">大类目标容器</param>
        /// <param name="storeKinds">仓储槽位目标容器</param>
        public static void CollectAllowed(JObject node, List<int> itemIds, List<int> itemKinds, List<int> storeKinds)
        {
            if (node == null)
                return;

            AppendUnique(node.Value<JArray>("itemIds"), itemIds);
            AppendUnique(node.Value<JArray>("itemKinds"), itemKinds);
            AppendUnique(node.Value<JArray>("storeKinds"), storeKinds);
        }

        /// <summary>
        /// 把 JSON 数组里尚未存在的值追加到列表（遍历取值，不产生中间数组）。
        /// </summary>
        /// <param name="array">来源 JSON 数组（可为 null）</param>
        /// <param name="target">目标列表（可为 null）</param>
        static void AppendUnique(JArray array, List<int> target)
        {
            if (array == null || target == null)
                return;

            for (int i = 0; i < array.Count; i++)
            {
                int value = array[i].Value<int>();
                if (!target.Contains(value))
                    target.Add(value);
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
