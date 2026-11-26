using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;

namespace Sango.Core
{
    /// <summary>
    /// 特技权限查询（"作用于城市"的解锁类特技专用）。
    ///
    /// <para>
    /// 【为什么需要这一层】<see cref="CityAllowCreateItem"/> / <see cref="CityAllowTroopType"/>
    /// 属于**权限声明**而不是事件效果：它们的判定点是"打开生产列表 / 打开出征界面 / 提交生产"，
    /// 这些时机并没有稳定的 GameEvent 可订阅（而且生产列表要在选人之前就算出来）。
    /// 因此这里按需扫描武将的**生效特技**（自带 + 装备 / 官职附加 + 混合组展开），
    /// 直接读 <c>actionEntities</c> 里的参数做判断，不依赖 Action 的生命周期。
    /// </para>
    ///
    /// <para>
    /// 【取数口径】统一走 <see cref="Person.CollectEffectiveFeatures"/>：
    ///   · 只取前 <see cref="Person.ActiveFeatureCount"/> 个（剧本特技名额），未解锁槽位不生效；
    ///   · 装备 / 官职附加特技全量生效、不占名额；
    ///   · 混合组按子特技递归展开。
    /// 因此本类不会出现"界面显示了、实际不生效"的两套口径。
    /// </para>
    ///
    /// <para>
    /// 【性能】只在打开生产 / 出征界面、提交生产任务时调用，属低频路径；
    /// 内部复用静态 List，不用 LINQ、不产生闭包。科技已满足的道具 / 兵种不会进入特技扫描。
    /// </para>
    /// </summary>
    public static class FeaturePermission
    {
        /// <summary>
        /// 权限查询接收的 kind 全量掩码 = "除混合组外的全部位"。
        ///
        /// 【为什么不按单个 kind 过滤】权限类特技的 kind 由策划在 Features.json 里填，
        /// 填错（例如把权限写成行军类）不应该导致"完全没有效果"这种难查的问题；
        /// 这里放开所有生效场景，只靠 actionEntities 的 class 精确匹配。
        ///
        /// 【为什么用 ~KindMask(Mixed) 而不是枚举 1..9】新增 FeatureKindType 时
        /// 权限查询会自动覆盖新场景，不需要回来改这一行（否则新 kind 的特技会静默失效）。
        /// 混合组（bit 0）由 <see cref="Feature.CollectEffective"/> 递归展开子特技，本身不参与匹配。
        /// </summary>
        static readonly int AllKindMask = ~Feature.KindMask(FeatureKindType.Mixed);

        /// <summary>
        /// <see cref="Person.CollectEffectiveFeatures"/> 的复用缓冲（单线程、不重入）。
        /// 容量按"剧本特技名额（默认 3~5）+ 装备 / 官职附加（4 件）"取 12，首次装填后不再扩容。
        /// </summary>
        static readonly List<Feature> tempFeatures = new List<Feature>(12);

        /// <summary>已提示过"兵种配置分组写反"的特技 + 兵种组合，避免每次开界面刷屏</summary>
        static readonly HashSet<string> warnedTroopGroupMismatch = new HashSet<string>();

        /// <summary><see cref="CityCanCreateItem"/> 的单次判定复用容器（避免每次分配集合）</summary>
        static readonly CreateItemPermissionSet tempCreateItemSet = new CreateItemPermissionSet();

        /// <summary>
        /// 兵种权限批量收集的共享复用容器（见 <see cref="SharedTroopTypePermissionSet"/>）。
        /// </summary>
        static readonly TroopTypePermissionSet sharedTroopTypeSet = new TroopTypePermissionSet();

        /// <summary>
        /// 兵种权限批量收集的**共享复用容器**。
        ///
        /// 【为什么共享】<see cref="TroopType.CheckActivTroopTypeList"/> 在 AI 组队 / 出征界面刷新时
        /// 每次都会构建候选列表；若每次 new 一个集合（6 个 List），会在回合结算处累积 GC。
        /// 共享容器 + <c>Clear()</c> 保留容量，首次之后零分配。
        ///
        /// 【使用约定】只允许在**同步、不重入**的遍历里临时借用：
        /// 借用前必须 <c>Clear()</c>，用完不要保存引用（下一次借用会覆写内容）。
        /// 当前唯一借用方是 <see cref="TroopType.CheckActivTroopTypeList"/>，
        /// 其循环体内（IsValid / HasItem）不会回调到自身或本容器。
        /// </summary>
        public static TroopTypePermissionSet SharedTroopTypePermissionSet
        {
            get { return sharedTroopTypeSet; }
        }

        // ==================== 剧本级能力探测（未配置时零成本短路） ====================
        //
        // 【为什么需要】这两个权限 Action 属于"可选玩法"：绝大多数剧本 / 存档根本不会配置。
        // 如果不做短路，生产菜单与出征界面每次刷新都要遍历一遍城内武将的特技，
        // 而 AI 每回合组队会反复走 CheckActivTroopTypeList —— 白付一笔与玩法无关的开销。
        // 这里在首次查询时扫一遍 Features 表（一次性），记下"本剧本是否配了这两类 Action"，
        // 没配就直接返回，性能与改动前完全一致。

        /// <summary>最近一次能力探测对应的剧本（换剧本 / 读档后重新扫描）</summary>
        static Scenario capabilityScenario;

        /// <summary>是否已对 <see cref="capabilityScenario"/> 做过探测</summary>
        static bool capabilityScanned;

        /// <summary>当前剧本是否存在 <see cref="CityAllowCreateItem"/> 配置</summary>
        static bool hasCreateItemPermission;

        /// <summary>当前剧本是否存在 <see cref="CityAllowTroopType"/> 配置</summary>
        static bool hasTroopTypePermission;

        /// <summary>
        /// 当前剧本是否配置了任意"道具制造权限"特技。
        /// 为 false 时所有道具权限查询可直接短路（未使用该玩法的剧本零额外开销）。
        /// </summary>
        public static bool HasAnyCreateItemPermission
        {
            get { EnsureCapabilities(); return hasCreateItemPermission; }
        }

        /// <summary>
        /// 当前剧本是否配置了任意"兵种组建权限"特技。
        /// 为 false 时候选兵种构建 / 单兵种校验都可直接短路。
        /// </summary>
        public static bool HasAnyTroopTypePermission
        {
            get { EnsureCapabilities(); return hasTroopTypePermission; }
        }

        /// <summary>
        /// 按当前剧本的 Features 表刷新"是否配置了权限 Action"。
        ///
        /// 【失效时机】只在 <see cref="Scenario.Cur"/> 引用变化时重扫（开局 / 读档 / 换剧本）。
        /// Features 是剧本静态数据，运行期不会热改；若将来的 Mod 支持运行期增删 actionEntities，
        /// 需要在这里补一个显式失效入口。
        /// </summary>
        static void EnsureCapabilities()
        {
            Scenario scenario = Scenario.Cur;
            if (capabilityScanned && ReferenceEquals(scenario, capabilityScenario))
                return;

            capabilityScenario = scenario;
            capabilityScanned = true;
            hasCreateItemPermission = false;
            hasTroopTypePermission = false;

            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Features == null)
                return;

            foreach (KeyValuePair<int, Feature> kv in scenario.CommonData.Features.objects)
            {
                Feature feature = kv.Value;
                JArray entities = feature != null ? feature.actionEntities : null;
                if (entities == null)
                    continue;

                for (int i = 0; i < entities.Count; i++)
                {
                    if (!(entities[i] is JObject node))
                        continue;

                    string cls = node.Value<string>("class");
                    if (cls == CityAllowCreateItem.ClassName)
                        hasCreateItemPermission = true;
                    else if (cls == CityAllowTroopType.ClassName)
                        hasTroopTypePermission = true;

                    // 两类都找到了就不必继续扫
                    if (hasCreateItemPermission && hasTroopTypePermission)
                        return;
                }
            }
        }

        // ==================== 道具制造权限 ====================

        /// <summary>
        /// 该城市当前能否生产指定道具（科技满足 **或** 城内武将携带解锁特技）。
        ///
        /// 【与 <see cref="ItemType.IsValid"/> 的区别】本方法对空势力做了保护
        /// （无主据点的 <c>BelongForce</c> 为 null，原方法会直接解引用崩溃），
        /// 并且额外叠加特技解锁路径。生产任务入口与生产列表共用本方法，保证口径一致。
        /// </summary>
        /// <param name="city">生产所在城市</param>
        /// <param name="itemType">待生产的道具类型</param>
        /// <returns>可生产返回 true</returns>
        public static bool CityCanCreateItem(City city, ItemType itemType)
        {
            if (city == null || itemType == null)
                return false;

            if (TechAllowsCreateItem(city, itemType))
                return true;

            // 剧本里没配 CityAllowCreateItem → 直接按科技结果返回（零额外开销）
            if (!HasAnyCreateItemPermission)
                return false;

            tempCreateItemSet.Clear();
            CollectCityCreateItemPermissions(city, tempCreateItemSet);
            return tempCreateItemSet.Allows(itemType);
        }

        /// <summary>
        /// 一次性收集该城市当前放开的所有道具权限（生产列表用）。
        ///
        /// 【为什么单独提供】<see cref="CityCanCreateItem"/> 是"单个道具"判断，
        /// 每条道具都会重扫一遍城内武将；生产列表要对整张兵装表逐条判断，
        /// 用本方法只扫一次武将，再由 <paramref name="set"/> 线性比对，避免 N × M 次特技展开。
        /// </summary>
        /// <param name="city">生产所在城市</param>
        /// <param name="set">输出容器（调用方负责复用与清空）</param>
        public static void CollectCityCreateItemPermissions(City city, CreateItemPermissionSet set)
        {
            if (city == null || set == null || city.allPersons == null)
                return;

            // 剧本里没配该 Action → 不必遍历武将（集合保持调用方 Clear 后的空状态）
            if (!HasAnyCreateItemPermission)
                return;

            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person person = city.allPersons[i];
                if (person == null || !person.HasAnyFeature)
                    continue;

                tempFeatures.Clear();
                person.CollectEffectiveFeatures(AllKindMask, tempFeatures);

                for (int j = 0; j < tempFeatures.Count; j++)
                {
                    Feature feature = tempFeatures[j];
                    JArray entities = feature != null ? feature.actionEntities : null;
                    if (entities == null)
                        continue;

                    for (int k = 0; k < entities.Count; k++)
                    {
                        if (!(entities[k] is JObject node))
                            continue;

                        if (node.Value<string>("class") != CityAllowCreateItem.ClassName)
                            continue;

                        CityAllowCreateItem.CollectAllowed(
                            node, set.itemIds, set.itemKinds, set.storeKinds);
                    }
                }
            }
        }

        /// <summary>
        /// 原有科技条件是否满足（空势力按"不满足"处理，不再抛空引用）。
        /// </summary>
        /// <param name="city">生产所在城市</param>
        /// <param name="itemType">道具类型</param>
        /// <returns>科技满足返回 true</returns>
        public static bool TechAllowsCreateItem(City city, ItemType itemType)
        {
            if (itemType == null)
                return false;

            // 无科技要求：走原有逻辑（剑兵兵装这类 cost = 0 的项也在此列）
            if (itemType.validTechId <= 0)
                return true;

            Force force = city != null ? city.BelongForce : null;
            return force != null && force.HasTechnique(itemType.validTechId);
        }

        // 【单一判定路径】道具权限只保留"批量收集 + 集合比对"一条路径
        //（<see cref="CollectCityCreateItemPermissions"/> + <see cref="CreateItemPermissionSet"/>）。
        // 不再提供"单个道具早退扫描"的重载：那会与批量路径形成两套口径，
        // 一旦参数的"或"关系调整就得改两处，属于可维护性负担。

        // ==================== 兵种组建权限 ====================

        /// <summary>
        /// 这批武将里是否有人携带"允许组建该兵种"的特技（**单个兵种**判定）。
        ///
        /// 【调用方】只有 <see cref="TroopTypeResolver.IsUsable"/>（出征界面的最终校验）。
        /// 构建候选列表请用 <see cref="CollectTroopTypePermissions"/>（批量，避免逐兵种重复展开）。
        /// 两处都传入"上阵武将 / 空闲武将"列表，与特殊兵道具 <c>validItemId</c> 的判定口径一致。
        /// </summary>
        /// <param name="persons">判定用武将列表</param>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>有武将提供权限返回 true</returns>
        public static bool AnyPersonAllowsTroopType(IList<Person> persons, TroopType troopType)
        {
            if (persons == null || troopType == null)
                return false;

            // 剧本里没配 CityAllowTroopType → 直接 false。
            // 这是出征界面每次换将、AI 每次组队都会走的分支，短路后与改动前完全同价。
            if (!HasAnyTroopTypePermission)
                return false;

            for (int i = 0; i < persons.Count; i++)
            {
                if (PersonAllowsTroopType(persons[i], troopType))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 一次性收集这批武将提供的全部兵种权限（构建候选兵种列表用）。
        ///
        /// 【为什么需要批量版本】<see cref="TroopType.CheckActivTroopTypeList"/> 要遍历整张兵种表
        /// （约 20 条）。若每条都调 <see cref="AnyPersonAllowsTroopType"/>，复杂度是
        /// "兵种数 × 武将数 × 每人特技数" —— AI 每回合组队 / 出征界面反复打开都会走这条路径。
        /// 改为先收集一次、再逐个线性比对；配合调用方的"原条件优先"，
        /// 科技与兵符都满足时（绝大多数兵种）**一次特技展开都不做**。
        /// </summary>
        /// <param name="persons">判定用武将列表</param>
        /// <param name="set">输出容器（调用方负责复用与清空）</param>
        public static void CollectTroopTypePermissions(IList<Person> persons, TroopTypePermissionSet set)
        {
            if (persons == null || set == null)
                return;

            // 剧本里没配该 Action → 不必遍历武将（集合保持调用方 Clear 后的空状态）
            if (!HasAnyTroopTypePermission)
                return;

            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                if (person == null || !person.HasAnyFeature)
                    continue;

                tempFeatures.Clear();
                person.CollectEffectiveFeatures(AllKindMask, tempFeatures);

                for (int j = 0; j < tempFeatures.Count; j++)
                {
                    Feature feature = tempFeatures[j];
                    JArray entities = feature != null ? feature.actionEntities : null;
                    if (entities == null)
                        continue;

                    for (int k = 0; k < entities.Count; k++)
                    {
                        if (!(entities[k] is JObject node))
                            continue;

                        if (node.Value<string>("class") != CityAllowTroopType.ClassName)
                            continue;

                        CityAllowTroopType.CollectAllowed(
                            node, feature.Id,
                            set.anyIds, set.landIds, set.waterIds,
                            set.anyFeatureIds, set.landFeatureIds, set.waterFeatureIds);
                    }
                }
            }
        }

        /// <summary>
        /// 单个武将是否携带"允许组建该兵种"的特技。
        ///
        /// 【使用场景】只用于"单个兵种最终校验"（<see cref="TroopTypeResolver.IsUsable"/>），
        /// 命中即返回，不遍历完整张兵种表；批量场景请用 <see cref="CollectTroopTypePermissions"/>。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="troopType">兵种</param>
        /// <returns>提供权限返回 true</returns>
        static bool PersonAllowsTroopType(Person person, TroopType troopType)
        {
            if (person == null || !person.HasAnyFeature)
                return false;

            tempFeatures.Clear();
            person.CollectEffectiveFeatures(AllKindMask, tempFeatures);

            for (int i = 0; i < tempFeatures.Count; i++)
            {
                Feature feature = tempFeatures[i];
                JArray entities = feature != null ? feature.actionEntities : null;
                if (entities == null)
                    continue;

                for (int j = 0; j < entities.Count; j++)
                {
                    if (!(entities[j] is JObject node))
                        continue;

                    if (node.Value<string>("class") != CityAllowTroopType.ClassName)
                        continue;

                    if (CityAllowTroopType.Allows(node, troopType))
                        return true;

                    // 配置把兵种写进了与自身 isLand 相反的分组：只提示一次，避免玩家以为特技没做
                    if (CityAllowTroopType.IsGroupMismatch(node, troopType))
                        WarnTroopGroupMismatch(feature, troopType);
                }
            }
            return false;
        }

        /// <summary>
        /// 提示"兵种被配到相反水陆分组"，同一（特技，兵种）只提示一次。
        /// </summary>
        /// <param name="feature">来源特技</param>
        /// <param name="troopType">兵种</param>
        static void WarnTroopGroupMismatch(Feature feature, TroopType troopType)
        {
            if (feature == null)
                return;

            WarnTroopGroupMismatch(feature.Id, feature.Name, troopType);
        }

        /// <summary>
        /// 批量路径的错组提示：只拿到特技 Id 时调用（名字从剧本公共数据取）。
        ///
        /// 【为什么需要这个重载】批量集合按"兵种分组"存 Id，查询时只能拿到命中的特技 Id，
        /// 拿不到 Feature 对象；错组属于低频异常配置，这里再按 Id 反查一次名字即可。
        /// </summary>
        /// <param name="featureId">来源特技 Id</param>
        /// <param name="troopType">兵种</param>
        public static void WarnTroopGroupMismatchOnce(int featureId, TroopType troopType)
        {
            if (featureId <= 0)
                return;

            Scenario scenario = Scenario.Cur;
            Feature feature = scenario != null ? scenario.GetObject<Feature>(featureId) : null;
            WarnTroopGroupMismatch(featureId,
                feature != null ? feature.Name : ("Id=" + featureId), troopType);
        }

        /// <summary>
        /// 错组提示的实际实现（按"特技 Id + 兵种 Id"去重，只打一次）。
        /// </summary>
        /// <param name="featureId">来源特技 Id</param>
        /// <param name="featureName">来源特技名（取不到时用 Id 兜底）</param>
        /// <param name="troopType">兵种</param>
        static void WarnTroopGroupMismatch(int featureId, string featureName, TroopType troopType)
        {
            if (troopType == null)
                return;

            string key = featureId + ":" + troopType.Id;
            if (!warnedTroopGroupMismatch.Add(key))
                return;

            string expectGroup = troopType.isLand ? "landTroopTypeIds" : "waterTroopTypeIds";
            Sango.Log.Warning(
                $"*[特技权限]「{featureName}」把兵种「{troopType.Name}」(Id={troopType.Id}) "
                + $"配到了相反的水陆分组，应配在 {expectGroup} 里，本次不生效");
        }
    }

    /// <summary>
    /// 城市"可制造道具"权限集合（<see cref="CityAllowCreateItem"/> 的展开结果）。
    ///
    /// <para>
    /// 用 <see cref="List{T}"/> 而不是 <see cref="HashSet{T}"/>：权限项通常只有个位数，
    /// 线性查找比哈希更快，而且 <see cref="Clear"/> 保留容量，反复打开生产列表不产生额外分配。
    /// </para>
    /// </summary>
    public sealed class CreateItemPermissionSet
    {
        /// <summary>放开的道具 Id</summary>
        public readonly List<int> itemIds = new List<int>(8);

        /// <summary>放开的道具大类（<see cref="ItemKindType"/>）</summary>
        public readonly List<int> itemKinds = new List<int>(4);

        /// <summary>放开的仓储槽位（<see cref="ItemStoreKindType"/>）</summary>
        public readonly List<int> storeKinds = new List<int>(4);

        /// <summary>
        /// 清空集合（保留容量）。
        /// </summary>
        public void Clear()
        {
            itemIds.Clear();
            itemKinds.Clear();
            storeKinds.Clear();
        }

        /// <summary>
        /// 集合是否为空（没有收集到任何权限）。
        /// </summary>
        public bool IsEmpty
        {
            get { return itemIds.Count == 0 && itemKinds.Count == 0 && storeKinds.Count == 0; }
        }

        /// <summary>
        /// 判定某个道具是否在集合内（三个维度为**或**关系）。
        /// </summary>
        /// <param name="itemType">待判定的道具</param>
        /// <returns>放开返回 true</returns>
        public bool Allows(ItemType itemType)
        {
            if (itemType == null)
                return false;

            return itemIds.Contains(itemType.Id)
                || itemKinds.Contains(itemType.kind)
                || storeKinds.Contains(itemType.storeKind);
        }
    }

    /// <summary>
    /// 兵种组建权限集合（<see cref="CityAllowTroopType"/> 的展开结果）。
    ///
    /// <para>
    /// 按水陆分组存 Id：<see cref="anyIds"/>（不限水陆）/ <see cref="landIds"/>（陆上）/
    /// <see cref="waterIds"/>（水上）。水陆两组与 <see cref="TroopType.isLand"/> 严格比对，
    /// 因此"把陆上兵种配进 waterTroopTypeIds"不会误生效。
    /// </para>
    /// <para>
    /// <c>xxxFeatureIds</c> 是与 <c>xxxIds</c> 一一对应的平行数组，只用于"分组写反"时
    /// 定位来源特技；命中判定不读它。
    /// </para>
    /// </summary>
    public sealed class TroopTypePermissionSet
    {
        /// <summary>不限水陆的兵种 Id</summary>
        public readonly List<int> anyIds = new List<int>(4);

        /// <summary>陆上兵种 Id</summary>
        public readonly List<int> landIds = new List<int>(8);

        /// <summary>水上兵种 Id</summary>
        public readonly List<int> waterIds = new List<int>(4);

        /// <summary>与 <see cref="anyIds"/> 对应的来源特技 Id</summary>
        public readonly List<int> anyFeatureIds = new List<int>(4);

        /// <summary>与 <see cref="landIds"/> 对应的来源特技 Id</summary>
        public readonly List<int> landFeatureIds = new List<int>(8);

        /// <summary>与 <see cref="waterIds"/> 对应的来源特技 Id</summary>
        public readonly List<int> waterFeatureIds = new List<int>(4);

        /// <summary>
        /// 清空集合（保留容量）。
        /// </summary>
        public void Clear()
        {
            anyIds.Clear();
            landIds.Clear();
            waterIds.Clear();
            anyFeatureIds.Clear();
            landFeatureIds.Clear();
            waterFeatureIds.Clear();
        }

        /// <summary>
        /// 集合是否为空（没有收集到任何兵种权限）。
        /// </summary>
        public bool IsEmpty
        {
            get { return anyIds.Count == 0 && landIds.Count == 0 && waterIds.Count == 0; }
        }

        /// <summary>
        /// 判定某个兵种是否在集合内（按 <see cref="TroopType.isLand"/> 取对应分组）。
        /// </summary>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>放开返回 true</returns>
        public bool Allows(TroopType troopType)
        {
            if (troopType == null)
                return false;

            if (anyIds.Contains(troopType.Id))
                return true;

            return troopType.isLand
                ? landIds.Contains(troopType.Id)
                : waterIds.Contains(troopType.Id);
        }

        /// <summary>
        /// 该兵种 Id 是否被配到了与自身水陆属性**相反**的分组里。
        /// </summary>
        /// <param name="troopType">待判定的兵种</param>
        /// <returns>命中的来源特技 Id；没有错组返回 -1</returns>
        public int GetMismatchFeatureId(TroopType troopType)
        {
            if (troopType == null)
                return -1;

            // 陆上兵种却配在 water 组 / 水上兵种却配在 land 组
            List<int> ids = troopType.isLand ? waterIds : landIds;
            List<int> featureIds = troopType.isLand ? waterFeatureIds : landFeatureIds;

            int index = ids.IndexOf(troopType.Id);
            return index >= 0 && index < featureIds.Count ? featureIds[index] : -1;
        }
    }
}
