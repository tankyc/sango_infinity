using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 求解层：把"域内各城的资源水位"翻译成"调拨单"。
    ///
    /// 【调度宗旨的落地】按**圈层水位**把资源从纵深推向边境：
    ///   前线（圈层 0）防御 / 进攻 → 次前线（圈层 1）支援 → 后方（≥2）储备 + 产出。
    /// 只允许**顺梯度**调运（源城圈层 ≥ 目标城圈层；同圈层互济、港关↔归属都市例外），
    /// 因此不会出现"把前线的东西搬去后方"这种零和倒腾。
    ///
    /// 【为什么要按域整体求解】旧的城池运输（<c>CityAI.AITransfrom</c>）只看得到**一环邻城**，
    /// 且按"本城库存的固定比例"发货 —— 没有全局缺口视图，也没有"前线优先"的概念。
    /// 这里改为：先算全域每城的缺口 / 富余，再按优先级配对，最后才落成调拨单。
    ///
    /// 【调什么】六类：金 / 粮 / 兵 / 兵装（枪戟弩马）/ 器械（冲车投石）/ 船。
    /// 六类**都有上限**：金 / 粮 / 兵是城池库容，兵装 / 器械 / 船是道具容器的换算上限
    /// （<see cref="GetItemGroupLimit"/>，按 <see cref="ItemType.TransformLimit"/> 求和）。
    /// 水位一律按**圈层的绝对数量**（<see cref="ResourceDispatchWeights"/> 里 water* 那六条线），
    /// 不按仓容百分比 —— 剧本改过金 / 粮 / 兵 / 兵装上限时，按比例算会让后方城永远达不到基本线、
    /// 永远在缺货，调度直接空转。仓容只用于"安全线"（不超过 仓容 × safeMargin）与"收货容量"。
    ///
    /// 【三趟分配】
    ///   ① 按水位补缺：把富余（自身水位以上的部分）推给缺口最急的城，接收方只收到自己的水位；
    ///   ② 仓位安抚：还缺货的城放宽到**安全线**（水位线到安全线之间的空余仓容也用起来），
    ///      源城仍然只出"自身水位以上"的部分 —— 专治"产出 / 部队回城把上限顶爆、
    ///      超出部分凭空消失（金粮兵）/ 再也造不出来（兵装器械船）"；
    ///   ③ 紧急抽调：前线仍缺货时，允许圈层 ≥1 的城把自身水位降到"自身水位线 × emergencyFloorRatio"（绝不动前线库存）。
    ///
    /// 全程**不超上限**：接收方容量按 <c>上限 × safeMargin − 现有 − 在途</c> 计算，只在容量内发货。
    /// </summary>
    public static class ResourceBalance
    {
        /// <summary>资源种类数（<see cref="ResourceKind"/> 的枚举项数）</summary>
        const int ResCount = 6;

        /// <summary>兵临城下的判定距离（与 <see cref="DeploymentExecutor"/> / <see cref="CitySituation.isUnderSiege"/> 同口径）</summary>
        const int BesiegeRange = 6;

        /// <summary>分配趟次（见类注释）</summary>
        enum AllocMode
        {
            /// <summary>① 按圈层水位补缺</summary>
            WaterLevel = 0,
            /// <summary>② 溢出安抚</summary>
            OverflowRelief = 1,
            /// <summary>③ 紧急抽调</summary>
            Emergency = 2,
        }

        /// <summary>
        /// 单城的资源账本（求解过程中的工作副本，<c>now</c> 会被分配过程改写，代表"已承诺的在途量"）。
        /// </summary>
        class Ledger
        {
            /// <summary>城池</summary>
            public City city;
            /// <summary>圈层（港关按归属都市，见 CityEstablishment.ResolveRing）</summary>
            public int ring;
            /// <summary>是否港 / 关</summary>
            public bool portGate;
            /// <summary>
            /// 港关是否有**军情**（被围 / 境内有敌军 / 附近有敌军 / 邻接外势力城）。
            /// 只有有军情的港关才提资源需求；平时的港关只是主城伸出去的仓储点（见 portGateQuiet*）。
            /// </summary>
            public bool military;
            /// <summary>军情的依据（报告用：被围 / 境内有敌军 / 接壤XX[某势力]）</summary>
            public string militaryReason;
            /// <summary>是否处于进攻姿态（本城有部队出征 / 在打城）</summary>
            public bool attacking;
            /// <summary>是否兵临城下</summary>
            public bool underSiege;
            /// <summary>是否"即将失守"（被围且守军明显不敌，见 <see cref="IsTargetDoomed"/>）</summary>
            public bool doomed;
            /// <summary>战火是否就在旁边（邻接友城被围 / 附近有敌方部队）</summary>
            public bool threatNearby;
            /// <summary>是否彻底太平（非前线、无被围、无邻城被围、附近无敌军）</summary>
            public bool peaceful;
            /// <summary>
            /// 优先级档（目标排序与执行排序的主序）：平时 = 圈层；
            /// 缺口达到"紧急插队"阈值时按上一档（<c>max(0, ring - 1)</c>）对待 —— 快断粮的城可以越级抢配额。
            /// </summary>
            public int priorityTier;
            /// <summary>是否有水路（港口 / 附属港口）：内陆城不需要船</summary>
            public bool hasWater;
            /// <summary>是否是产地（有锻冶 / 马厩 / 工坊 / 船厂）：要给它留生产资金</summary>
            public bool isProducer;
            /// <summary>
            /// 该资源在本城是否参与调度（同时满足：参数允许 + 上限取得到）。
            /// 上限取不到（脏数据 / 港关的等级链异常）时置 false —— 这种城**既不能发货也不能收货**，
            /// 绝不能当成"上限 0"：那会让水位目标变成 0，把全城库存一次搬空。
            /// </summary>
            public bool[] active = new bool[ResCount];
            /// <summary>各资源现有量</summary>
            public int[] now = new int[ResCount];
            /// <summary>各资源在途量（已出发的运输队）</summary>
            public int[] inbound = new int[ResCount];
            /// <summary>各资源的上限</summary>
            public int[] limit = new int[ResCount];
            /// <summary>各资源的水位目标量</summary>
            public int[] target = new int[ResCount];
            /// <summary>各资源缺口（目标 − 现有 − 在途，负值归零）</summary>
            public int[] need = new int[ResCount];
            /// <summary>
            /// 本趟求解里已经"承诺收下"的量。
            /// 用于 <see cref="SourceAvail"/>：刚在**本趟**收到的货不该再被当成自有库存往外发
            /// （否则同一趟里 A→B、B→C 会排出注定落空的单，浪费运输主将与配额）。
            /// </summary>
            public int[] receivedThisRun = new int[ResCount];
            /// <summary>域内跳数（本城 → 其它城，用于挑"最近的源"）</summary>
            public Dictionary<int, int> hops;
            /// <summary>缺口权重（排序用：缺失比例 × 资源权重）</summary>
            public int deficitWeight;
        }

        /// <summary>源城候选（选出"给谁发货最合适"）</summary>
        struct SourceCandidate
        {
            /// <summary>账本下标</summary>
            public int index;
            /// <summary>到目标城的跳数（港关↔父城 / 军团委任已减去优惠，用于"挑最近/更后方"）</summary>
            public int hops;
            /// <summary>到目标城的**原始**跳数（不含任何优惠），用于"中转接力"的距离判定</summary>
            public int rawHops;
            /// <summary>源城圈层</summary>
            public int ring;
            /// <summary>该资源可出口量</summary>
            public int avail;
            /// <summary>源城 id（排序兜底，保证结果可复现）</summary>
            public int cityId;
        }

        /// <summary>
        /// 调拨单账本：同一对（源城 → 目标城）的多类资源合并成一张单（共用一个运输主将）。
        /// </summary>
        class OrderBook
        {
            public List<ResourceShipment> orders = new List<ResourceShipment>();
            readonly Dictionary<long, int> index = new Dictionary<long, int>();
            readonly Dictionary<int, int> fromCounts = new Dictionary<int, int>();

            /// <summary>该源城当前占用了多少张单（用于"每城每回合最多发几车"限流）。</summary>
            public int FromCount(int cityId)
            {
                int c;
                return fromCounts.TryGetValue(cityId, out c) ? c : 0;
            }

            /// <summary>
            /// 这张（源 → 目标）单是否已经存在。
            /// 限流必须看"**新开**几张单"而不是"发了几批货"：同一对城池的多种资源会合并成一张单、
            /// 共用一个运输主将，所以给同一目标续运粮草 / 兵装时不该被自己的第一张单挡住。
            /// </summary>
            public bool HasOrder(int fromCityId, int toCityId)
            {
                return index.ContainsKey((long)fromCityId * 1000000L + toCityId);
            }

            /// <summary>追加货量（同一对城池自动合并）。</summary>
            /// <param name="from">源城</param>
            /// <param name="to">**实际收货城**（中转接力时是中转城，不是最终目标）</param>
            /// <param name="toRing">实际收货城的圈层（报告 / 调试用）</param>
            /// <param name="toTier">这张单**服务的最终目标**的优先级档（执行排序主序）</param>
            /// <param name="toUrgency">最终目标的缺口紧急度（同档内先发最急的）</param>
            /// <param name="reason">调度理由</param>
            /// <param name="kind">资源种类</param>
            /// <param name="amount">货量</param>
            /// <param name="escortFood">本批兵力随车带的护送粮（仅运兵时有意义；须同时按粮草再记一笔）</param>
            /// <param name="escortArms">本批兵力随车带的兵装（仅运兵时有意义；须同时按兵装再记一笔）</param>
            /// <param name="hops">本条发货的行程跳数（执行层据此算**车队自备口粮**：路耗 + 10 天）</param>
            public void Add(City from, City to, int toRing, int toTier, int toUrgency,
                string reason, ResourceKind kind, int amount, int escortFood = 0, int escortArms = 0,
                int hops = 0)
            {
                if (from == null || to == null || amount <= 0)
                    return;

                long key = (long)from.Id * 1000000L + to.Id;
                int i;
                if (index.TryGetValue(key, out i))
                {
                    ResourceShipment s = orders[i];
                    Accumulate(ref s, kind, amount);
                    if (escortFood > 0)
                        s.escortFood += escortFood;
                    if (escortArms > 0)
                        s.escortArms += escortArms;
                    if (hops > s.hops)
                        s.hops = hops;
                    orders[i] = s;
                    return;
                }

                ResourceShipment fresh = new ResourceShipment();
                fresh.fromCityId = from.Id;
                fresh.fromCityName = from.Name;
                fresh.toCityId = to.Id;
                fresh.toCityName = to.Name;
                fresh.toRing = toRing;
                fresh.toTier = toTier;
                fresh.toUrgency = toUrgency;
                fresh.reason = reason;
                fresh.escortFood = escortFood > 0 ? escortFood : 0;
                fresh.escortArms = escortArms > 0 ? escortArms : 0;
                fresh.hops = hops > 0 ? hops : 0;
                Accumulate(ref fresh, kind, amount);
                index[key] = orders.Count;
                orders.Add(fresh);

                fromCounts[from.Id] = FromCount(from.Id) + 1;
            }

            static void Accumulate(ref ResourceShipment s, ResourceKind kind, int amount)
            {
                switch (kind)
                {
                    case ResourceKind.Gold: s.gold += amount; break;
                    case ResourceKind.Food: s.food += amount; break;
                    case ResourceKind.Troop: s.troops += amount; break;
                    case ResourceKind.Arms: s.arms += amount; break;
                    case ResourceKind.Machine: s.machines += amount; break;
                    default: s.boats += amount; break;
                }
            }
        }

        // ==================== 域采集 ====================

        /// <summary>
        /// 采集一个调度作用域的城池：势力级（<paramref name="scope"/> 为 null）= 全势力城池；
        /// 军团级 = 本军团的城池。**含港 / 关**（它们的 BelongCorps 跟随归属都市）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scope">军团作用域（null = 势力级）</param>
        /// <param name="scenario">剧本</param>
        /// <returns>域内城池（存活且有归属）</returns>
        public static List<City> CollectDomain(Force force, Corps scope, Scenario scenario)
        {
            List<City> result = new List<City>();
            if (force == null || scenario == null)
                return result;

            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || city.BelongForce != force)
                    continue;
                if (scope != null)
                {
                    Corps cityCorps = city.BelongCorps;
                    if (cityCorps == null)
                    {
                        // 归属缺失（刚易手的瞬间）→ 归第一军团兜底，避免被所有作用域漏掉
                        if (force.CapitalCorps != scope)
                            continue;
                    }
                    else if (cityCorps != scope)
                        continue;
                }
                result.Add(city);
            }
            return result;
        }

        // ==================== 求解入口 ====================

        /// <summary>
        /// 求解一个作用域的资源调拨计划。**只产出计划，不派车**（派车在 <see cref="ResourceDispatcher"/>）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scope">军团作用域（null = 势力级）</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        /// <param name="dryRun">是否"只算不运"（影子模式 / 玩家直辖军团）</param>
        /// <returns>调拨计划（永不为 null）</returns>
        public static ResourcePlan Solve(Force force, Corps scope, Scenario scenario, ResourceDispatchWeights w,
            bool dryRun)
        {
            ResourcePlan plan = new ResourcePlan();
            if (force == null || scenario == null || w == null)
                return plan;

            plan.forceId = force.Id;
            plan.forceName = force.Name;
            plan.corpsId = scope != null ? scope.Id : 0;
            plan.corpsName = scope != null ? ("第" + scope.number + "军团") : null;
            plan.adviceOnly = dryRun;

            List<City> domain = CollectDomain(force, scope, scenario);
            if (domain.Count == 0)
                return plan;

            // 【军团委任 · 运输禁止】勾了这个开关的军团，城池既不收货也不发货
            // —— 与旧的城池运输命令（AITransfrom / AITransfromToBelongCity 开头都判这一条）保持同一口径。
            for (int i = domain.Count - 1; i >= 0; i--)
            {
                if (IsTransportDisabled(domain[i]))
                {
                    plan.unmet.Add(string.Format("{0}: 军团委任已勾选\"运输禁止\"，本回合不参与调度", domain[i].Name));
                    domain.RemoveAt(i);
                }
            }
            if (domain.Count == 0)
                return plan;

            HashSet<int> domainIds = new HashSet<int>();
            for (int i = 0; i < domain.Count; i++)
                domainIds.Add(domain[i].Id);

            // ---------- 1) 采集账本 ----------
            // 域内跳数表：整域一次算完（带缓存），避免每城一次 BFS 的 O(N²) 重复开销
            Dictionary<int, Dictionary<int, int>> hopsTable = GetDomainHops(domain, domainIds, scenario);

            List<Ledger> ledgers = new List<Ledger>(domain.Count);
            for (int i = 0; i < domain.Count; i++)
            {
                Ledger L = null;
                try
                {
                    L = BuildLedger(domain[i], scenario, w, hopsTable);
                }
                catch (Exception)
                {
                    // 城况采集本身也可能被脏数据打断（港关的等级 / 上限链等）。
                    // 一个据点的问题不该让整回合的 AI 停摆：跳过它并在报告里点名。
                    L = null;
                }

                if (L != null)
                    ledgers.Add(L);
                else
                    plan.unmet.Add(string.Format("{0}: 城况 / 上限数据异常，本回合跳过该城的资源调度", domain[i].Name));
            }
            if (ledgers.Count == 0)
                return plan;

            for (int i = 0; i < ledgers.Count; i++)
            {
                plan.cityState.Add(CityStateText(ledgers[i], w));
                plan.cities.Add(ledgers[i].city);
            }

            // ---------- 1.5) 即将失守的城：本回合不接收补给 ----------
            // 把缺口直接清零（而不是留给"缺口未补齐"去刷屏），并在报告里点名，
            // 同时让第二趟（仓位安抚）也收不了货 —— 见 Allocate 里的同一判定。
            if (w.skipDoomedTarget)
            {
                for (int i = 0; i < ledgers.Count; i++)
                {
                    Ledger L = ledgers[i];
                    if (!L.doomed)
                        continue;
                    for (int r = 0; r < ResCount; r++)
                        L.need[r] = 0;
                    plan.unmet.Add(string.Format("{0}(圈层{1}) 兵临城下且守军劣势(敌{2}/我{3})，本回合不接收补给",
                        L.city.Name, L.ring, L.city.EnemyTroops, L.city.troops));
                }
            }

            // ---------- 2) 分配（三趟） ----------
            OrderBook book = new OrderBook();
            List<int> targetOrder = BuildTargetOrder(ledgers);

            // 运输队兵种（运兵的护送粮要按它的粮耗倍率算）
            TroopType transportType = TroopType.GetTransportType(scenario, force);

            Allocate(ledgers, targetOrder, AllocMode.WaterLevel, book, w, scenario, transportType, 0f);
            if (w.enableOverflowRelief)
                Allocate(ledgers, targetOrder, AllocMode.OverflowRelief, book, w, scenario, transportType, 0f);
            if (w.enableEmergencyDrain)
            {
                // 紧急抽调（第三趟）之前先量一次前线的告急程度：
                // 它决定纵深城可以被抽到什么程度（0 = 只抽到水位线的 20%，1 = 允许抽空）。
                plan.frontUrgency = FrontUrgency(ledgers, w, scenario);
                Allocate(ledgers, targetOrder, AllocMode.Emergency, book, w, scenario, transportType,
                    plan.frontUrgency);
            }

            // ---------- 3) 按"最该送的发货"排序 ----------
            // 优先级档（含紧急插队）→ 缺口紧急度 → 货量大 → 城 id 兜底。
            // 配额（每回合就那么几车）有限，排序错了就会把运力花在不着急的城上。
            List<ResourceShipment> orders = book.orders;
            orders.Sort(delegate (ResourceShipment a, ResourceShipment b)
            {
                if (a.toTier != b.toTier) return a.toTier.CompareTo(b.toTier);
                if (a.toUrgency != b.toUrgency) return b.toUrgency.CompareTo(a.toUrgency);
                int am = a.TotalAmount();
                int bm = b.TotalAmount();
                if (am != bm) return bm.CompareTo(am);
                if (a.toCityId != b.toCityId) return a.toCityId.CompareTo(b.toCityId);
                return a.fromCityId.CompareTo(b.fromCityId);
            });
            plan.shipments = orders;

            // ---------- 3.5) 运不出去的富余（产物端背压） ----------
            // 三趟都跑完之后，`now` 已经扣掉了本次承诺运走的量：
            // 某城某类资源若仍高于自身水位，就说明"有富余但没地方放"——
            // 产物端据此在满仓时停产（见 ResourceDispatchState.CanKeepProducing）。
            for (int i = 0; i < ledgers.Count; i++)
            {
                Ledger L = ledgers[i];
                for (int r = 0; r < ResCount; r++)
                {
                    if (!L.active[r])
                        continue;
                    int left = L.now[r] - L.target[r];
                    if (left < MinShip((ResourceKind)r, w))
                        continue;

                    ResourceBacklog item;
                    item.cityId = L.city.Id;
                    item.cityName = L.city.Name;
                    item.kind = r;
                    item.amount = left;
                    plan.backlog.Add(item);
                }
            }

            // ---------- 3.7) 兵装待产：守军缺兵装 + 生产端阻塞诊断 ----------
            // 兵没有兵装就拉不出一支部队（组建要按 TroopType.costItems 扣兵装，1 件装备 1 兵），
            // 而"兵装一直是 0"通常不是没安排生产，是 AICreateItems 的前置没满足
            // （无空闲武将 / 无空闲锻冶马厩 / 金不足 / 各类已达城内目标）。
            // 这里把"缺口 + 阻塞原因"一起记下来：
            //   · 报告里直接写出来 —— 省得只知道缺兵装却查不到为什么；
            //   · 执行模式下登记给 ResourceDispatchState，供城池 AI 命令排序给"造兵装"加权
            //     （命令是按分数**依次执行**的，排在后面只会拿到"人已被用光"的空池子，
            //      这正是兵装长期为 0 的机制）。
            for (int i = 0; i < ledgers.Count; i++)
            {
                Ledger L = ledgers[i];
                if (L.city == null || L.city.troops <= 0)
                    continue;

                int armsNow = WeaponCount(L.city.itemStore);
                int gap = (int)(L.city.troops * w.armsDemandCoverRatio) - armsNow;
                if (gap < w.armsDemandMinGap)
                    continue;

                ArmsDemandNote note;
                note.cityId = L.city.Id;
                note.cityName = L.city.Name;
                note.gap = gap;
                note.blocker = DescribeArmsBlocker(L.city);
                plan.armsDemand.Add(note);
            }

            // ---------- 4) 未满足的缺口 ----------
            for (int i = 0; i < ledgers.Count; i++)
            {
                Ledger L = ledgers[i];
                StringBuilder sb = null;
                AppendGap(ref sb, L, ResourceKind.Gold, "金");
                AppendGap(ref sb, L, ResourceKind.Food, "粮");
                AppendGap(ref sb, L, ResourceKind.Troop, "兵");
                AppendGap(ref sb, L, ResourceKind.Arms, "装");
                AppendGap(ref sb, L, ResourceKind.Machine, "器");
                AppendGap(ref sb, L, ResourceKind.Boat, "船");
                if (sb != null)
                    plan.unmet.Add(string.Format("{0}(圈层{1}) 缺口未补齐: {2}", L.city.Name, L.ring, sb));
            }

            return plan;
        }

        // ==================== 账本 ====================

        /// <summary>
        /// 采集单城账本。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        /// <param name="hopsTable">本域"城池 id → 域内跳数表"（可为 null，此时按不连通处理）</param>
        static Ledger BuildLedger(City city, Scenario scenario, ResourceDispatchWeights w,
            Dictionary<int, Dictionary<int, int>> hopsTable)
        {
            if (city == null)
                return null;

            CitySituation situation = CitySituation.Collect(city, scenario);

            Ledger L = new Ledger();
            L.city = city;
            L.portGate = city.IsPort() || city.IsGate();
            L.ring = CityEstablishment.ResolveRing(city);
            // 军情：港关**只有**在有军情时才提资源需求（平时不许和主城抢运力，见 ResourceDispatchWeights 港关一段）。
            // 顺带把依据记下来 —— 报告里能直接看到"凭什么算有军情"，免得又出现"为什么全是前线"这类疑问。
            // 接壤判定走 CitySituation.hasForeignNeighbor（与人才调度的港关编制同一口径，只算一次）。
            if (situation.isUnderSiege)
            {
                L.military = true;
                L.militaryReason = "被围";
            }
            else if (situation.hasThreatTroop || city.EnemyCount > 0)
            {
                L.military = true;
                L.militaryReason = "境内有敌军";
            }
            else if (w.portGateMilitaryNeighbor && situation.hasForeignNeighbor)
            {
                L.military = true;
                L.militaryReason = "接壤" + ForeignNeighborText(city);
            }
            L.attacking = situation.attackTroopsCount > 0;
            L.underSiege = situation.isUnderSiege;
            L.doomed = IsTargetDoomed(city, w.doomedDefenseRatio);
            // 战况（只用城况快照里已算好的量，不额外扫地图）：
            //   邻接友城被围 / 附近有敌方部队 → 战火就在旁边；三者都没有且非前线 → 彻底太平。
            L.threatNearby = situation.besiegedNeighborCount > 0 || situation.hasThreatTroop;
            L.peaceful = !L.underSiege && !L.threatNearby && L.ring >= 1;
            // 水路口径与编制层的"船岗"一致（CityEstablishment：hasPort || portList 非空）
            L.hasWater = situation.hasPort || (city.portList != null && city.portList.Count > 0);
            L.isProducer = situation.hasBlacksmith || situation.hasStable
                || situation.hasMachineFactory || situation.hasBoatFactory;

            SumInbound(scenario, city.Id, L.inbound);

            for (int r = 0; r < ResCount; r++)
            {
                ResourceKind kind = (ResourceKind)r;
                int limit;
                if (!Includes(r, w) || !TryGetLimit(city, kind, scenario, out limit))
                {
                    // 参数没开 / 上限取不到 → 本城这类资源**完全不参与**（既不发货也不收货）
                    L.active[r] = false;
                    L.now[r] = 0;
                    L.target[r] = 0;
                    continue;
                }

                L.active[r] = true;
                L.limit[r] = limit;
                L.now[r] = GetNow(city, kind, situation);
                L.target[r] = GetTarget(L, kind, w, scenario);
                int total = L.now[r] + L.inbound[r];
                int need = L.target[r] - total;
                L.need[r] = need > 0 ? need : 0;
            }

            L.deficitWeight = CalcDeficitWeight(L, w);
            // 【紧急插队】缺口大到"快断粮"的城按上一个圈层对待，可以越过"只是轻微不足"的靠前圈层城抢配额
            L.priorityTier = (w.criticalDeficitWeight > 0 && L.deficitWeight >= w.criticalDeficitWeight)
                ? Math.Max(0, L.ring - 1)
                : L.ring;

            // 域内跳数表（整域一次算完 + 缓存）。取不到就给一张空表：本城既不发货也不收货，
            // 但后续各处都只做 TryGetValue，空表天然表示"域内不连通"，不会出错。
            Dictionary<int, int> myHops = null;
            if (hopsTable != null)
                hopsTable.TryGetValue(city.Id, out myHops);
            L.hops = myHops != null ? myHops : new Dictionary<int, int>();
            return L;
        }

        /// <summary>
        /// 本城某类资源的**水位线**（与库存同量纲）—— 调度的"宗旨"就在这里：
        /// 前线（0）留得最多（进攻时再多备一档），次前线（1）作为支援池，后方（≥2）只留底仓。
        /// 圈层 -1（未定）按最深层处理（与 <see cref="CityEstablishment.CalcDevelopRingFactor"/> 同口径）。
        ///
        /// 两类口径（细节见 <see cref="BaseLine"/>）：
        ///   · 金 / 粮 / 兵 → 绝对数量（不按仓容比例：改过上限的剧本里比例会虚高，后方永远达不到基本线）；
        ///   · 兵装 / 器械 / 船 → **每千兵件数 × 本城兵力水位**（这三类在游戏里都是按人头消耗的）。
        /// </summary>
        /// <param name="L">账本（需要圈层 / 进攻姿态 / 战况标记）</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <returns>水位线（≥ 0）</returns>
        static int WaterLine(Ledger L, ResourceKind kind, ResourceDispatchWeights w)
        {
            float v = BaseLine(L, kind, w);

            // 【战况驱动】打起来的地方多备、彻底太平的地方少留 —— 后者腾出来的正好推向前线。
            // 只用城况快照里已经算好的量（被围 / 邻城被围 / 附近有敌军），不额外扫描地图。
            // （战况 / 进攻系数只在**这里**乘一次；军备的 BaseLine 借用兵力线时不会再乘。）
            if (L.ring == 0 && L.attacking)
                v *= Math.Max(0f, w.frontAttackFactor);
            if (L.underSiege)
                v *= 1f + w.threatWaterBonusHigh;
            else if (L.threatNearby)
                v *= 1f + w.threatWaterBonusMid;
            else if (L.peaceful)
                v *= 1f - w.peacefulWaterCut;

            if (v < 0f)
                v = 0f;
            return (int)Math.Round(v);
        }

        /// <summary>
        /// 本城某类资源的**基础水位**（不含战况 / 进攻微调）。
        ///
        /// · 金 / 粮 / 兵：绝对数量。前线程取"仓容 × frontFillRatio"（默认 95%，能装多少装多少，
        ///   它是全势力的资源终点），次前线 / 后方取配置里的绝对数。
        /// · 兵装 / 器械 / 船：**随兵力水位走** —— <c>本圈层的兵力基础水位 × 每千兵件数 / 1000</c>。
        ///   本游戏这三类的消耗都是"每千兵多少件"（<c>TroopType.costItems</c> →
        ///   <c>ItemStore.CheckCostMin</c>，1 件装备 1 兵），不是原版那种"一个独立单位"，
        ///   所以写成固定件数会严重偏小（例：次前线 3 万兵却只留 20 件器械）。
        /// </summary>
        /// <param name="L">账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <returns>基础水位（≥ 0）</returns>
        static float BaseLine(Ledger L, ResourceKind kind, ResourceDispatchWeights w)
        {
            // ---------- 军备类：按人头（每千兵）折算 ----------
            if (kind == ResourceKind.Arms || kind == ResourceKind.Machine || kind == ResourceKind.Boat)
            {
                int perThousand = PerThousandOf(w, kind, L.ring);
                if (perThousand <= 0)
                    return 0f;
                float troopLine = BaseLine(L, ResourceKind.Troop, w);       // 借同一圈的兵力水位
                return troopLine * perThousand / 1000f;
            }

            // ---------- 金 / 粮 / 兵：绝对数量 ----------
            ResourceDispatchWeights.WaterLine line = LineOf(w, kind);
            if (line == null)
                return 0f;

            if (L.ring == 0)
            {
                // 【前线 = 能装多少装多少】前线是全势力的资源终点，囤得越多越扛得住进攻，
                // 所以按"仓容 × frontFillRatio"（默认 95%，与安全线同口径）取水位，不用绝对值。
                // frontFillRatio 配 0 时才回落到 water*.front 那条绝对值线。
                int cap = L.limit[(int)kind];
                return (w.frontFillRatio > 0f && cap > 0) ? cap * w.frontFillRatio : line.front;
            }
            return L.ring == 1 ? line.subFront : line.rear;
        }

        /// <summary>
        /// 前线紧急度（0 = 从容，1 = 快守不住）—— 决定后方可以被抽到什么程度。
        ///
        /// 口径刻意**不看水位线**（前线的水位线是"能装多少装多少"，永远填不满，
        /// 拿它算紧急度会恒定拉满 → 后方每回合被抽空），只看两件要命的事：
        ///   · 守军口粮还能撑几回合（低于 <c>vitalFoodTurns</c> 开始计分）；
        ///   · 守军兵力是否低于底线（都市 <c>vitalFrontTroops</c>；港关用它自己的"扎住线"
        ///     <c>portGateQuietKeepTroops</c>）。
        /// 取**最告急的那座守备据点**：一座前线快断了，后方就该让路。
        /// **无军情的港关不参与** —— 那是仓储点（允许空着），算进来会让紧急度永久拉满。
        /// </summary>
        /// <param name="ledgers">账本</param>
        /// <param name="w">参数</param>
        /// <param name="scenario">剧本（取口粮系数）</param>
        /// <returns>0 ~ 1 的紧急度</returns>
        static float FrontUrgency(List<Ledger> ledgers, ResourceDispatchWeights w, Scenario scenario)
        {
            if (ledgers == null)
                return 0f;

            float foodPerTroop = scenario != null && scenario.Variables != null
                ? scenario.Variables.baseFoodCostInTroop : 0.1f;

            float urgency = 0f;
            for (int i = 0; i < ledgers.Count; i++)
            {
                Ledger L = ledgers[i];
                if (L == null || L.city == null || L.ring != 0)
                    continue;

                // 【只有真守备据点才算】无军情的港关是仓储点：编制上只留 1 个运输岗、
                // 兵力按 portGateQuietKeepTroops(2000) 保底，本来就允许空着 ——
                // 拿它当"前线守军"会让紧急度被永久拉满（几座空港口、甚至 0 兵就够），
                // 于是后方每回合被抽干。那是设计，不是紧急。
                if (L.portGate && !L.military)
                    continue;

                // 粮：还能撑几回合（1 粮养 10 兵）
                float perTurn = L.city.troops * foodPerTroop;
                float vitalFood = perTurn * Math.Max(0f, w.vitalFoodTurns);
                if (vitalFood > 0f)
                {
                    float f = 1f - L.city.food / vitalFood;
                    if (f > urgency)
                        urgency = f;
                }

                // 兵：守军底线。港关的底线是它自己的"扎住线"（2000），不是都市的万人 ——
                // 拿都市的底线去量一座守备港，等于它天生就"告急"。
                int vitalTroops = L.portGate
                    ? Math.Max(0, w.portGateQuietKeepTroops)
                    : w.vitalFrontTroops;
                if (vitalTroops > 0)
                {
                    float t = 1f - (float)L.city.troops / vitalTroops;
                    if (t > urgency)
                        urgency = t;
                }
            }

            if (urgency < 0f)
                urgency = 0f;
            return urgency > 1f ? 1f : urgency;
        }

        /// <summary>金 / 粮 / 兵 → 对应的**绝对**水位线配置。</summary>
        /// <param name="w">参数</param>
        /// <param name="kind">资源种类</param>
        /// <returns>水位线（金 / 粮 / 兵之外返回 null）</returns>
        static ResourceDispatchWeights.WaterLine LineOf(ResourceDispatchWeights w, ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Gold: return w.waterGold;
                case ResourceKind.Food: return w.waterFood;
                case ResourceKind.Troop: return w.waterTroops;
                default: return null;                                   // 军备三类按人头折算，不走绝对值
            }
        }

        /// <summary>
        /// 兵装 / 器械 / 船 → 对应的**每千兵件数**（按圈层取档）。
        /// 本游戏这三类都按人头消耗（1 件装备 1 兵，见 <c>ItemStore.CheckCostMin</c>），
        /// 所以水位必须随兵力走。
        /// </summary>
        /// <param name="w">参数</param>
        /// <param name="kind">资源种类</param>
        /// <param name="ring">圈层（0 前线 / 1 次前线 / ≥2 后方）</param>
        /// <returns>每千兵件数</returns>
        static int PerThousandOf(ResourceDispatchWeights w, ResourceKind kind, int ring)
        {
            ResourceDispatchWeights.WaterLine line;
            switch (kind)
            {
                case ResourceKind.Arms: line = w.armsPerThousand; break;
                case ResourceKind.Machine: line = w.machinePerThousand; break;
                default: line = w.boatPerThousand; break;
            }
            if (line == null)
                return 0;
            if (ring == 0)
                return line.front;
            return ring == 1 ? line.subFront : line.rear;
        }

        /// <summary>该资源是否参与调度。</summary>
        static bool Includes(int r, ResourceDispatchWeights w)
        {
            switch ((ResourceKind)r)
            {
                case ResourceKind.Gold: return w.includeGold;
                case ResourceKind.Food: return w.includeFood;
                case ResourceKind.Troop: return w.includeTroops;
                case ResourceKind.Arms: return w.includeArms;
                case ResourceKind.Machine: return w.includeMachine;
                default: return w.includeBoat;
            }
        }

        /// <summary>
        /// 安全读取一座城某类资源的上限（**六类都有上限**）。
        /// 金 / 粮 / 兵走各自的库容；兵装 / 器械 / 船走道具容器的换算上限。
        ///
        /// 这里必须做异常兜底：<c>City.GoldLimit / FoodLimit / TroopsLimit / StoreLimit</c>
        /// 都要取 <c>City.CityLevelType</c> 的加值，而**港关等据点的等级数据可能为空**
        /// （编制层 <see cref="CityEstablishment.CalcDevelopGap"/> 就为此专门写了 try/catch，
        /// 注释原文是"港关等极端数据下上限链可能抛异常"）。取不到就返回 false 让调用方跳过该类资源，
        /// 绝不能让一个据点的脏数据把整回合的 AI 打断。
        /// </summary>
        /// <param name="city">城池</param>
        /// <param name="kind">资源种类</param>
        /// <param name="scenario">剧本</param>
        /// <param name="limit">输出：上限（返回 true 时有效且 &gt; 0）</param>
        /// <returns>取到有效上限返回 true</returns>
        static bool TryGetLimit(City city, ResourceKind kind, Scenario scenario, out int limit)
        {
            limit = 0;
            if (city == null)
                return false;

            try
            {
                switch (kind)
                {
                    case ResourceKind.Gold: limit = city.GoldLimit; break;
                    case ResourceKind.Food: limit = city.FoodLimit; break;
                    case ResourceKind.Troop: limit = city.TroopsLimit; break;
                    default: limit = GetItemLimit(city, scenario, kind); break;     // 兵装 / 器械 / 船
                }
            }
            catch (Exception)
            {
                limit = 0;
            }
            return limit > 0;
        }

        /// <summary>
        /// 本城**有没有可能**造出兵装 —— 给"抽运输主将时为生产端留人"用。
        ///
        /// 复用 <see cref="DescribeArmsBlocker"/> 的条件集：那一串阻塞原因里，只有"无空闲武将"
        /// 是**留人**能解决的；没锻冶 / 没马厩 / 缺钱 / 已达城内目标都不是留人能救的 ——
        /// 那种城留人只是白瞎一个武将。
        ///
        /// 【港关一律 false】港关不产内政（没有锻冶 / 马厩），留人对兵装毫无帮助 ——
        /// 这正是"渡口明明有 2 名空闲武将、却因为要'留人给生产'而不发车"那种卡死的根源。
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>留人能帮上忙返回 true</returns>
        public static bool CanProduceArms(City city)
        {
            if (city == null || !city.IsCity())
                return false;
            string blocker = DescribeArmsBlocker(city);
            return string.IsNullOrEmpty(blocker) || blocker == "无空闲武将";
        }

        /// <summary>兵装 / 器械 / 船的容器上限（按组换算，见 <see cref="GetItemGroupLimit"/>）。</summary>
        static int GetItemLimit(City city, Scenario scenario, ResourceKind kind)        {
            switch (kind)
            {
                case ResourceKind.Arms:
                    return GetItemGroupLimit(city, scenario,
                        (int)ItemStoreKindType.Spear, (int)ItemStoreKindType.Horse);
                case ResourceKind.Machine:
                    return GetItemGroupLimit(city, scenario,
                        (int)ItemStoreKindType.Helepolis, (int)ItemStoreKindType.Catapult);
                default:
                    return GetItemGroupLimit(city, scenario,
                        (int)ItemStoreKindType.Boat, (int)ItemStoreKindType.Boat);
            }
        }

        /// <summary>资源现有量（兵装 / 器械 / 船 走道具库，与"军备"岗同口径）。</summary>
        static int GetNow(City city, ResourceKind kind, CitySituation situation)
        {
            if (city == null)
                return 0;
            switch (kind)
            {
                case ResourceKind.Gold: return city.gold;
                case ResourceKind.Food: return city.food;
                case ResourceKind.Troop: return city.troops;
                case ResourceKind.Arms: return situation.weaponCount;
                case ResourceKind.Machine: return situation.machineCount;
                default: return BoatCount(city.itemStore);
            }
        }

        /// <summary>
        /// 资源的水位目标量 = **本城圈层的绝对水位线**（与库存同量纲）。
        /// 前线留得最多、后方只留底仓，多出来的部分自然往前线流。
        ///
        /// 兵装**不**在这里受"每兵一件"约束 —— 它的水位就是 <c>waterArms</c> 那条线；
        /// 兵力只参与"它到底缺不缺、缺得多急"的**紧缺程度**计算（见 <see cref="SeverityOf"/>），
        /// 用来决定发货 / 插队顺序，不参与定水位。
        ///
        /// 只有两处特例：
        ///   · 船：内陆城（无港口 / 非港关）目标为 0；
        ///   · 金：产地（有锻冶 / 马厩 / 工坊 / 船厂）保留一笔启动资金。
        ///
        /// 最后统一夹一道**安全线** <c>仓容 × safeMargin</c>：水位线是绝对数量，但它不该越过仓容，
        /// 否则"收货能收多少"与"想要多少"就不是同一个数 —— 会出现永远填不满的缺口，还会把货硬塞到超上限。
        /// （产物端满仓后并不会立刻停产 —— 是否继续生产由 ResourceDispatchState 的
        ///   覆盖 / 背压信号决定，见 <c>CanKeepProducing</c>。）
        /// </summary>
        /// <param name="L">账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <param name="scenario">剧本（港关留守军口粮要按粮耗系数算）</param>
        /// <returns>水位目标量（≥ 0）</returns>
        static int GetTarget(Ledger L, ResourceKind kind, ResourceDispatchWeights w, Scenario scenario)
        {
            if (L == null || L.city == null)
                return 0;

            // 【内陆城不需要船】船下水才有用，上岸既不能用也开不走；编制层的"船岗"同样只在
            // 有港口（或附属港口）时才成立。不给内陆城设船只水位 —— 否则会白白把船运进死地。
            if (kind == ResourceKind.Boat && !L.hasWater)
                return 0;

            // 【港关·无军情】只是主城伸出去的仓储点：**不提任何需求**，只留一点底仓
            // （粮 = 守军口粮、兵 = 保底驻军），其余（金 / 兵装 / 器械 / 船）全算富余、往主城送。
            if (L.portGate && !L.military)
                return QuietPortGateTarget(L, kind, scenario, w);

            int raw = WaterLine(L, kind, w);

            // 【有军情的港关】按主城那一档取水位后再降一档：关口是前进基地，守得住就行，
            // 不必像主城那样把仓库堆满（堆太满被攻破时整仓资敌）。
            if (L.portGate && w.portGateFillFactor != 1f)
                raw = (int)Math.Round(raw * Math.Max(0f, w.portGateFillFactor));

            // 【保留生产资金】有生产设施的城（锻冶 / 马厩 / 工坊 / 船厂）要留得住启动资金：
            // 造兵装 / 器械 / 船都按绝对金额设了门槛（AIConfig.createItemsMinGold 等），
            // 把钱一股脑运去前线，后方的生产会当场停摆 —— 那才是真正的浪费。
            if (kind == ResourceKind.Gold && L.isProducer && w.productionGoldKeep > 0)
            {
                int keep = Math.Min(w.productionGoldKeep, L.limit[(int)kind]);
                if (raw < keep)
                    raw = keep;
            }

            return ClampToCap(L, kind, raw, w);
        }

        /// <summary>
        /// 无军情港关的水位目标：只留底仓，其余全部算富余（会往归属主城送）。
        ///   · 粮：守军几回合口粮（<c>portGateQuietFoodTurns</c>，1 粮养 10 兵）；
        ///   · 兵：保底驻军（<c>portGateQuietKeepTroops</c>，默认 0 = 全上交）；
        ///   · 金：保底资金（<c>portGateQuietKeepGold</c>，默认 0 = 全上交）；
        ///   · 兵装 / 器械 / 船：0（全上交）。
        /// 这些城的 <see cref="Ledger.need"/> 因此恒为 0 —— 它们不会和主城抢运力。
        /// </summary>
        /// <param name="L">账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数</param>
        /// <returns>底仓量（≥ 0）</returns>
        static int QuietPortGateTarget(Ledger L, ResourceKind kind, Scenario scenario, ResourceDispatchWeights w)
        {
            switch (kind)
            {
                case ResourceKind.Food:
                {
                    float perTroop = scenario != null && scenario.Variables != null
                        ? scenario.Variables.baseFoodCostInTroop : 0.1f;
                    int keep = (int)Math.Ceiling(L.city.troops * perTroop * Math.Max(0f, w.portGateQuietFoodTurns));
                    return ClampToCap(L, kind, keep, w);
                }
                case ResourceKind.Troop:
                    return ClampToCap(L, kind, Math.Max(0, w.portGateQuietKeepTroops), w);
                case ResourceKind.Gold:
                    return ClampToCap(L, kind, Math.Max(0, w.portGateQuietKeepGold), w);
                default:
                    return 0;                                       // 兵装 / 器械 / 船 全上交
            }
        }

        /// <summary>
        /// 把水位目标夹到**安全线** <c>仓容 × safeMargin</c> 以内。
        /// 水位线是绝对数量，但它不该越过仓容：否则"想要多少"与"最多能收多少"就不是同一个数，
        /// 会出现永远填不满的缺口，还会把货硬塞到超上限。
        /// </summary>
        /// <param name="L">账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="raw">原始目标量</param>
        /// <param name="w">参数</param>
        /// <returns>夹过上限的目标量（≥ 0）</returns>
        static int ClampToCap(Ledger L, ResourceKind kind, int raw, ResourceDispatchWeights w)
        {
            int cap = L.limit[(int)kind];
            if (cap > 0)
            {
                float margin = w.safeMargin;
                if (margin > 1f)
                    margin = 1f;
                if (margin <= 0f)
                    margin = 1f;                                    // 配置异常时退化为"不越仓容"
                // 向上取整：小仓容（器械 / 船常常只有个位数）用 Floor 会被压到 0 或 1，
                // 于是"前线想要几件"这种需求会莫名其妙地消失。
                int ceiling = (int)Math.Ceiling(cap * margin);
                if (raw > ceiling)
                    raw = ceiling;
            }
            return raw > 0 ? raw : 0;
        }

        // ==================== 运兵的护送粮 ====================

        /// <summary>
        /// 每个兵随车要带的干粮 = 每回合口粮 × 路程回合数 × 倍数。
        ///   · 每回合口粮 = <c>ScenarioVariables.baseFoodCostInTroop</c>（默认 0.1，即"1 粮养 10 兵"）
        ///     × 运输队兵种的粮耗倍率；
        ///   · 路程回合数 = 跳数 × <c>escortTurnsPerHop</c>（运输队走得慢，默认 2 回合/跳）。
        /// </summary>
        /// <param name="hops">行程跳数（不足 1 按 1 算）</param>
        /// <param name="scenario">剧本</param>
        /// <param name="transportType">运输队兵种（取它的粮耗倍率；为空按 1 倍）</param>
        /// <param name="w">参数</param>
        /// <returns>每兵需要携带的粮草（可为 0 表示无法估算）</returns>
        public static float EscortFoodPerTroop(int hops, Scenario scenario, TroopType transportType,
            ResourceDispatchWeights w)
        {
            if (scenario == null || scenario.Variables == null)
                return 0f;
            float perTurn = scenario.Variables.baseFoodCostInTroop
                * (transportType != null ? transportType.foodCostFactor : 1f);
            float turns = (hops > 1 ? hops : 1) * Math.Max(1f, w.escortTurnsPerHop);
            return perTurn * turns * Math.Max(1f, w.escortFoodFactor);
        }

        /// <summary>
        /// 运 <paramref name="troops"/> 兵需要随车携带的护送粮总量。
        /// 口径是**大于 2 倍路程消耗**：一半路上吃掉、一半留给接收城当缓冲 ——
        /// 否则援军一到就把接收城的粮吃空，等于把缺口从前线转成了口粮负担。
        /// </summary>
        /// <param name="troops">随行的兵力</param>
        /// <param name="hops">行程跳数</param>
        /// <param name="scenario">剧本</param>
        /// <param name="transportType">运输队兵种</param>
        /// <param name="w">参数</param>
        /// <returns>需要的护送粮总量</returns>
        public static int EscortFoodFor(int troops, int hops, Scenario scenario, TroopType transportType,
            ResourceDispatchWeights w)
        {
            if (troops <= 0)
                return 0;
            float perTroop = EscortFoodPerTroop(hops, scenario, transportType, w);
            return perTroop <= 0f ? 0 : (int)Math.Ceiling(troops * perTroop);
        }

        /// <summary>同样路程里**真的会被路上吃掉**的那部分（= 护送粮 ÷ 倍数），用于目标城的到货记账。</summary>
        /// <param name="troops">随行的兵力</param>
        /// <param name="hops">行程跳数</param>
        /// <param name="scenario">剧本</param>
        /// <param name="transportType">运输队兵种</param>
        /// <param name="w">参数</param>
        /// <returns>路上消耗的粮草</returns>
        static int JourneyFoodFor(int troops, int hops, Scenario scenario, TroopType transportType,
            ResourceDispatchWeights w)
        {
            if (troops <= 0)
                return 0;
            float perTroop = EscortFoodPerTroop(hops, scenario, transportType, w)
                / Math.Max(1f, w.escortFoodFactor);
            return perTroop <= 0f ? 0 : (int)Math.Ceiling(troops * perTroop);
        }

        /// <summary>
        /// 运输队**自备口粮**：不管这车运什么，队伍自己路上都要吃粮。
        ///
        /// 【为什么单独算】<c>Troop.food</c> 是**货物与口粮共用的同一个池**，而"只运金 / 兵装 /
        /// 器械 / 船"的单子在求解层不带护送粮（护送粮只给"运兵"算）→ <c>troop.food = 0</c>。
        /// 后果不是"慢一点"：<c>Troop.OnForceTurnStart</c> 里 <c>food ≤ 0</c> 且兵力 &lt; 500
        /// 直接 <c>Clear()</c> —— 车队与整车货当回合一起消失。
        ///
        /// 口径 = ceil(每回合粮耗) × (行程回合数 + <c>convoyProvisionTurns</c>)：
        ///   · 行程回合数 = 跳数 × <c>escortTurnsPerHop</c>（运输队走得慢，默认 2 回合/跳）；
        ///   · <c>convoyProvisionTurns</c> = **额外**要带的"10 天口粮"（不是总回合数，见参数注释）；
        ///   · 每回合粮耗与游戏一致（<c>baseFoodCostInTroop × 兵力 × 运输兵种粮耗倍率</c>），
        ///     **先向上取整到 1 再乘** —— 1 兵的车队每回合也要吃 1 粮（游戏就是 ceil），
        ///     先乘小数会算出"0 粮"这种看着合理、实际必死的数。
        /// </summary>
        /// <param name="troops">运输队自身兵力（含随队押运兵）</param>
        /// <param name="hops">行程跳数（不足 1 按 1 算）</param>
        /// <param name="scenario">剧本</param>
        /// <param name="transportType">运输队兵种（取粮耗倍率；为空按 1 倍）</param>
        /// <param name="w">参数</param>
        /// <returns>车队应随车携带的口粮（≥1；无法估算时返回 0）</returns>
        public static int ConvoyFoodFor(int troops, int hops, Scenario scenario, TroopType transportType,
            ResourceDispatchWeights w)
        {
            if (troops <= 0 || scenario == null || scenario.Variables == null)
                return 0;
            int perTurn = (int)Math.Ceiling(scenario.Variables.baseFoodCostInTroop * troops
                * (transportType != null ? transportType.foodCostFactor : 1f));
            if (perTurn < 1)
                perTurn = 1;                                     // 兵力再少也要 1 粮/回合（与游戏 ceil 同口径）
            float perHop = w != null ? Math.Max(1f, w.escortTurnsPerHop) : 2f;
            float journeyTurns = (hops > 1 ? hops : 1) * perHop;   // 路上要吃的
            float extraTurns = w != null ? Math.Max(0f, w.convoyProvisionTurns) : 10f;
            return (int)Math.Ceiling(perTurn * (journeyTurns + extraTurns));
        }

        // ==================== 兵装待产诊断 ====================

        /// <summary>
        /// 诊断"本城为什么造不出兵装"，返回空串表示条件齐备。
        ///
        /// 【与 <c>CityAI.AICreateItems</c> 的关系】这里是那串前置条件的**只读镜像**：
        /// 那边每一条不满足都只是静默 <c>return true</c>，外面完全看不出卡在哪一步，
        /// 而"城市全是 0 兵装"这种事恰恰需要这个答案。镜像而不是直接调用，是因为那边
        /// 还要顺手挑兵装种类、派将开工 —— 那是执行，不是诊断。
        /// 若以后改了 <c>AICreateItems</c> 的前置，这里要一起改（两处条件必须同口径）。
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>阻塞原因；空串 = 没有阻塞</returns>
        public static string DescribeArmsBlocker(City city)
        {
            if (city == null)
                return string.Empty;

            // ① 军团委任整体禁止造兵装（枪 / 戟 / 弩 三禁齐上，与 BuildingWorking 的判定同口径）
            Corps corps = city.BelongCorps;
            if (corps != null
                && corps.GetAppointValue(Corps.AppointContentType.MakeItem_Spear) == 1
                && corps.GetAppointValue(Corps.AppointContentType.MakeItem_Halberd) == 1
                && corps.GetAppointValue(Corps.AppointContentType.MakeItem_Crossbow) == 1)
                return "军团委任禁止造兵装";

            // ② 没有空闲武将 —— 两条产线都卡这一条：
            //    经典内政看 AICreateItems 的 freePersons.Count，工作制看建筑能不能派人上岗。
            if (city.freePersons == null || city.freePersons.Count == 0)
                return "无空闲武将";

            // ③ 资金（AIConfig.createItemsMinGold）
            AIConfig cfg = AIConfig.Instance;
            if (cfg != null && city.gold < cfg.createItemsMinGold)
                return "金钱不足";

            // ④ 产出建筑：要有**完工且空岗**的锻冶（枪 / 戟 / 弩）或马厩（军马）
            bool exists = false, complete = false;
            if (city.allBuildings != null)
            {
                for (int i = 0; i < city.allBuildings.Count; i++)
                {
                    Building b = city.allBuildings[i];
                    if (b == null || b.BuildingType == null)
                        continue;
                    int k = b.BuildingType.kind;
                    if (k != (int)BuildingKindType.BlacksmithShop && k != (int)BuildingKindType.Stable)
                        continue;
                    exists = true;
                    if (b.isComplate)
                        complete = true;
                }
            }
            bool smithFree = city.GetFreeBuilding((int)BuildingKindType.BlacksmithShop) != null;
            bool stableFree = city.GetFreeBuilding((int)BuildingKindType.Stable) != null;
            if (!smithFree && !stableFree)
            {
                if (!exists)
                    return "无锻冶/马厩建筑";
                if (!complete)
                    return "锻冶/马厩未完工";
                return "锻冶/马厩已有人在岗";
            }

            // 每类兵装的目标量（同 AICreateItems 口径：levelTotal × 兵力 / sumTotal + 5000）
            int[] levelTotal = new int[4] { 100, 100, 100, 100 };
            if (city.allPersons != null && city.allPersons.Count > 8)
            {
                levelTotal = new int[4] { 1, 1, 1, 1 };
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    Person p = city.allPersons[i];
                    if (p == null)
                        continue;
                    levelTotal[0] += p.SpearLv;
                    levelTotal[1] += p.HalberdLv;
                    levelTotal[2] += p.CrossbowLv;
                    levelTotal[3] += p.RideLv;
                }
            }

            int sumTotal = 0;
            if (smithFree)
                sumTotal += levelTotal[0] + levelTotal[1] + levelTotal[2];
            if (stableFree)
                sumTotal += levelTotal[3];
            if (sumTotal <= 0)
                return "无可用产出建筑";

            int producible = 0;
            for (int t = 0; t < 4; t++)
            {
                int itemTypeId = t + 2;                          // 2 枪 / 3 戟 / 4 弩 / 5 马
                if (itemTypeId == 5 && !stableFree)
                    continue;
                if (itemTypeId < 5 && !smithFree)
                    continue;

                int itemNum = city.itemStore.GetNumber(itemTypeId);
                if (itemNum >= city.storeLimit)                  // 仓库已满
                    continue;
                if (itemNum < levelTotal[t] * city.troops / sumTotal + 5000)
                    producible++;                                // 这类还该造
            }

            if (producible <= 0)
                return "各类兵装已达城内目标/仓库已满";

            return string.Empty;
        }

        /// <summary>
        /// 目标城是否"即将失守"：**兵临城下**（最近敌人 ≤ <see cref="BesiegeRange"/> 格）
        /// 且敌方兵力 ≥ 我方守军 × <paramref name="ratio"/>。
        ///
        /// 调度侧（不再往这座城送）与运输侧（已在路上的运输队立即回撤）**共用这一个判定**
        /// （见 <c>TroopTransformGoodsToCity</c>），避免两边规则各写一份而后走偏。
        /// 只读城池上已预计算的敌军信息，因此可以放心地在每回合的部队 AI 里调用。
        /// </summary>
        /// <param name="city">目标城</param>
        /// <param name="ratio">劣势比例（&lt;= 0 表示不判断）</param>
        /// <returns>很可能守不住返回 true</returns>
        public static bool IsTargetDoomed(City city, float ratio)
        {
            if (city == null || ratio <= 0f)
                return false;
            if (city.EnemyCount <= 0)
                return false;
            if (city.NearestEnemyDistance > BesiegeRange)
                return false;
            return city.EnemyTroops >= city.troops * ratio;
        }

        /// <summary>缺口权重：把六类资源的**紧缺程度**求和（每类 0..1，合计最大 600），用于给目标城排序。</summary>
        static int CalcDeficitWeight(Ledger L, ResourceDispatchWeights w)
        {
            float sum = 0f;
            for (int r = 0; r < ResCount; r++)
            {
                if (!L.active[r])
                    continue;
                sum += SeverityOf(L, (ResourceKind)r, w);
            }
            return (int)Math.Round(sum * 100f);
        }

        /// <summary>
        /// 单类资源的**紧缺程度**（0..1）—— 只用来排序 / 判断"该不该插队"，不参与定水位。
        ///
        /// · **兵装**：用**兵力**衡量。基准 = <c>兵力 × armamentCoverTarget</c>（默认每兵一件），
        ///   再与实际可达到的量（仓容的安全线）取小 ——
        ///   否则"兵力 3 万、军械库只能放 3 千"的城会永远算成"缺 2.7 万件"，一直霸占插队名额。
        ///   于是：兵多而兵装少 → 真缺，排在前面；兵少的小城仓库空着也不算急。
        /// · **其余**：缺额 ÷ 目标（本城该有的量还差几成）。
        /// </summary>
        /// <param name="L">账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <returns>0（不缺）~ 1（完全缺）</returns>
        static float SeverityOf(Ledger L, ResourceKind kind, ResourceDispatchWeights w)
        {
            int idx = (int)kind;
            int total = L.now[idx] + L.inbound[idx];

            if (kind == ResourceKind.Arms)
            {
                float demand = L.city.troops * Math.Max(0f, w.armamentCoverTarget);
                float reachable = L.limit[idx];
                if (reachable > 0)
                {
                    float ceiling = (float)Math.Ceiling(reachable * w.safeMargin);
                    if (demand > ceiling)
                        demand = ceiling;
                }
                if (demand <= 0f)
                    return 0f;

                float lack = demand - total;
                if (lack <= 0f)
                    return 0f;
                float s = lack / demand;
                return s > 1f ? 1f : s;
            }

            double baseValue = L.target[idx];
            if (baseValue <= 0.0)
                return 0f;
            double v = L.need[idx] / baseValue;
            if (v <= 0.0)
                return 0f;
            return v > 1.0 ? 1f : (float)v;
        }

        /// <summary>
        /// 目标城排序：**边境优先** ——
        /// 圈层小的先补（0 = 前线）→ 被围的优先 → 进攻姿态优先 → 缺口大的优先 → 城 id 兜底（可复现）。
        /// </summary>
        /// <summary>
        /// 目标城排序：**边境优先 + 紧急插队** ——
        /// 优先级档（= 圈层，紧急城已提前一档）→ 被围优先 → 进攻姿态优先 → 缺口大的优先 → 城 id 兜底（可复现）。
        /// </summary>
        static List<int> BuildTargetOrder(List<Ledger> ledgers)
        {
            List<int> order = new List<int>(ledgers.Count);
            for (int i = 0; i < ledgers.Count; i++)
                order.Add(i);

            order.Sort(delegate (int a, int b)
            {
                Ledger la = ledgers[a];
                Ledger lb = ledgers[b];
                // 【优先级档】= 圈层（紧急城已被 BuildLedger 提到上一档）→ 被围 → 都市先于港关
                // → 进攻 → 缺口大的 → 圈层 → id
                if (la.priorityTier != lb.priorityTier) return la.priorityTier.CompareTo(lb.priorityTier);
                if (la.underSiege != lb.underSiege) return la.underSiege ? -1 : 1;
                // 【港关优先级降低】同一档里**主城（都市）先于港关**：先把主城填够，再轮到渡口 / 关隘。
                // 无军情的港关需求恒为 0，本来就不会抢；这里主要管"有军情的港口 vs 主城"的先后。
                if (la.portGate != lb.portGate) return la.portGate ? 1 : -1;
                if (la.attacking != lb.attacking) return la.attacking ? -1 : 1;
                if (la.deficitWeight != lb.deficitWeight) return lb.deficitWeight.CompareTo(la.deficitWeight);
                if (la.ring != lb.ring) return la.ring.CompareTo(lb.ring);
                return la.city.Id.CompareTo(lb.city.Id);
            });
            return order;
        }

        /// <summary>
        /// 找中转城：源城 → 目标城的**最短路**上、比目标更近、且还有仓容的己方城。
        /// 取"离源城最近的那一个"作为第一腿的落点 —— 第一腿越短，物资越快离开后方、越不容易被半路截掉。
        ///
        /// 判"在最短路上"用的是两张已有的 BFS 表（源城一张、目标城一张），不需要额外寻路：
        ///     <c>hops(源→M) + hops(M→目标) == hops(源→目标)</c>
        ///
        /// 找不到（没有中转城 / 中转城都满了 / 第一腿也超距离上限）时返回 -1，
        /// 调用方继续走**直接投送** —— 接力是优化，不能变成"前线拿不到货"的原因。
        /// </summary>
        /// <param name="ledgers">全部账本</param>
        /// <param name="srcIndex">源城账本下标</param>
        /// <param name="targetIndex">最终目标账本下标</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <returns>中转城下标；没有合适的中转城返回 -1</returns>
        static int FindRelayStaging(List<Ledger> ledgers, int srcIndex, int targetIndex,
            ResourceKind kind, ResourceDispatchWeights w)
        {
            Ledger S = ledgers[srcIndex];
            Ledger T = ledgers[targetIndex];
            if (S.hops == null || T.hops == null)
                return -1;

            int full;
            if (!S.hops.TryGetValue(T.city.Id, out full) || full <= 1)
                return -1;

            int best = -1;
            int bestHops = int.MaxValue;

            for (int i = 0; i < ledgers.Count; i++)
            {
                if (i == srcIndex || i == targetIndex)
                    continue;

                Ledger M = ledgers[i];
                if (!M.active[(int)kind])
                    continue;                                       // 中转城得收得下这类资源
                if (w.skipDoomedTarget && M.doomed)
                    continue;

                int sm, mt;
                if (!S.hops.TryGetValue(M.city.Id, out sm) || !T.hops.TryGetValue(M.city.Id, out mt))
                    continue;
                if (sm < 1 || mt < 1 || sm + mt != full)
                    continue;                                       // 必须落在最短路上（且比最终目标更近）
                if (w.maxDirectHops > 0 && sm > w.maxDirectHops)
                    continue;                                       // 第一腿也得在距离上限内

                // 中转城要有仓容（按"仓位安抚"口径放宽到安全线）
                if (ReceiverCapacity(M, kind, AllocMode.OverflowRelief, w) <= 0)
                    continue;

                if (sm < bestHops)
                {
                    bestHops = sm;
                    best = i;
                }
            }
            return best;
        }

        // ==================== 分配 ====================

        /// <summary>分配一趟：把可用富余按优先级推给缺货的城。</summary>
        /// <param name="ledgers">账本</param>
        /// <param name="targetOrder">目标城顺序</param>
        /// <param name="mode">趟次口径</param>
        /// <param name="book">单据簿</param>
        /// <param name="w">参数</param>
        /// <param name="scenario">剧本（运兵算护送粮用）</param>
        /// <param name="transportType">运输队兵种（算粮耗倍率用，可为 null）</param>
        /// <param name="frontUrgency">前线紧急度（0~1，只对紧急抽调趟有意义：越大后方越可被抽空）</param>
        static void Allocate(List<Ledger> ledgers, List<int> targetOrder, AllocMode mode, OrderBook book,
            ResourceDispatchWeights w, Scenario scenario, TroopType transportType, float frontUrgency)
        {
            for (int oi = 0; oi < targetOrder.Count; oi++)
            {
                Ledger T = ledgers[targetOrder[oi]];
                // 即将失守的城一律不收货。前两趟靠"缺口已清零"就能挡住，
                // 但仓位安抚（第二趟）看的是仓容而不是缺口，所以这里再挡一次。
                if (w.skipDoomedTarget && T.doomed)
                    continue;

                string reason = ReasonText(T, mode);

                for (int r = 0; r < ResCount; r++)
                {
                    if (!T.active[r])
                        continue;                               // 该资源在本城不参与调度
                    // 六类资源**都有上限**（金 / 粮 / 兵是库容，兵装 / 器械 / 船是道具容器），
                    // 所以溢出安抚对全部资源都成立。

                    int need = ReceiverCapacity(T, (ResourceKind)r, mode, w);
                    if (need <= 0)
                        continue;

                    int minShip = MinShip((ResourceKind)r, w);
                    ResourceKind kind = (ResourceKind)r;

                    // 【攒够一车再走】一车至少要覆盖收货城缺口的 batchShipRatio，否则这一趟不发：
                    // 源城继续攒货，等够一车再一次性运走 —— 车次少了，押车武将也省下来了。
                    //   · 溢出安抚趟不压：货已压仓，必须走；
                    //   · 兵不压：兵是整批走的单位，且它的缺口按（可能被剧本改过的）兵力上限算，虚高；
                    //   · 粮有救急豁免：口粮见底时不讲批次（见 BatchCrisisBypass）。
                    if (mode != AllocMode.OverflowRelief && kind != ResourceKind.Troop
                        && w.batchShipRatio > 0f && need > 0
                        && !BatchCrisisBypass(T, kind, w, scenario))
                    {
                        long byRatio = (long)Math.Ceiling(need * w.batchShipRatio);
                        if (byRatio > minShip)
                            minShip = byRatio > int.MaxValue ? int.MaxValue : (int)byRatio;
                    }
                    int targetIndex = targetOrder[oi];
                    List<SourceCandidate> sources = CollectSources(ledgers, T, kind, mode, w, frontUrgency);
                    for (int si = 0; si < sources.Count; si++)
                    {
                        if (need <= 0)
                            break;

                        SourceCandidate c = sources[si];
                        Ledger S = ledgers[c.index];

                        // 【中转接力】距离太远的直接投送改为一腿一腿走：先运到途中最靠近源城的己方城，
                        // 下一回合由那座城转发。找不到合适的中转城时仍然直接投送（不会把前线饿着）。
                        int destIndex = targetIndex;
                        if (mode == AllocMode.WaterLevel && w.enableRelayStaging
                            && w.maxDirectHops > 0 && c.rawHops > w.maxDirectHops)
                        {
                            int relay = FindRelayStaging(ledgers, c.index, targetIndex, kind, w);
                            if (relay >= 0)
                                destIndex = relay;
                        }
                        Ledger D = ledgers[destIndex];
                        bool relayed = destIndex != targetIndex;

                        // 限流只看"**新开**几张单"：同一对（源 → 目标）的多种资源会合并成一张单、
                        // 共用一个运输主将，因此给同一目标续运粮草 / 兵装不该被自己的第一张单挡住。
                        if (!book.HasOrder(S.city.Id, D.city.Id)
                            && book.FromCount(S.city.Id) >= w.maxShipmentsPerSourceCity)
                            continue;

                        int avail = SourceAvail(S, kind, mode, w, frontUrgency);
                        if (avail <= 0)
                            continue;

                        // 中转城的收货容量按"仓位安抚"口径（可以放到安全线）——
                        // 中转城的水位是它自己那个圈层的，不该因为"替前线收一批货"就被撑爆。
                        int room = ReceiverCapacity(D, kind,
                            relayed ? AllocMode.OverflowRelief : mode, w);
                        if (room <= 0)
                            continue;

                        int ship = need < avail ? need : avail;
                        if (ship > room)
                            ship = room;

                        // ---------- 【兵粮一定要保证】 ----------
                        // 兵在途中每回合要吃"兵力 × 0.1 × 兵种倍率"，**断粮每回合掉 30% 的兵**；
                        // 到了城里又变成接收城要养的**口粮负担**。所以运兵**一律**要带够护送粮
                        // （> 2 倍路程消耗：一半路上吃掉、一半留给接收城做缓冲），
                        // 粮不够就按粮缩兵 —— 任何规模都不例外，小股调动也一样。
                        //
                        // 【兵装不做要求】见 w.requireArmsForTroops（默认关）：缺装不该挡援兵，
                        // 装备由兵装调度那条线单独送。escorts 只用来判断"要不要按兵装配装"。
                        bool escorts = kind == ResourceKind.Troop && ship > 0
                            && ship >= Math.Max(0, w.escortMinTroops);

                        int escortFood = 0;
                        if (kind == ResourceKind.Troop && ship > 0)
                        {
                            float perTroop = EscortFoodPerTroop(c.rawHops, scenario, transportType, w);
                            if (perTroop > 0f)
                            {
                                int foodUsable = w.escortFoodMustBeSurplus
                                    ? SourceAvail(S, ResourceKind.Food, mode, w, frontUrgency)
                                    // 默认允许动用储备：随兵离城的还有这些兵往后每回合的口粮需求，
                                    // 抽走这笔粮对源城基本是中性的，而"运不出兵"的代价更实在。
                                    : S.now[(int)ResourceKind.Food];

                                int maxTroops = (int)(foodUsable / perTroop);
                                if (maxTroops < ship)
                                    ship = maxTroops;
                                if (ship < minShip)
                                    continue;                   // 缩到起运门槛以下就不运了

                                escortFood = EscortFoodFor(ship, c.rawHops, scenario, transportType, w);
                                if (escortFood > foodUsable)
                                    escortFood = foodUsable > 0 ? foodUsable : 0;

                                // 记账：护送粮从源城扣掉；路上吃掉一部分，剩下的随军进城（算目标城的到货）
                                int arriving = escortFood
                                    - JourneyFoodFor(ship, c.rawHops, scenario, transportType, w);
                                if (arriving < 0)
                                    arriving = 0;
                                S.now[(int)ResourceKind.Food] -= escortFood;
                                if (S.now[(int)ResourceKind.Food] < 0)
                                    S.now[(int)ResourceKind.Food] = 0;
                                D.now[(int)ResourceKind.Food] += arriving;
                                D.receivedThisRun[(int)ResourceKind.Food] += arriving;
                                D.need[(int)ResourceKind.Food] -= arriving;
                                if (D.need[(int)ResourceKind.Food] < 0)
                                    D.need[(int)ResourceKind.Food] = 0;
                            }
                        }

                        // ---------- 【兵装随行：按**目标城的兵装富余 / 缺口**来算】 ----------
                        // 兵没有兵装确实拉不出一支部队（组建按 TroopType.costItems 扣，1 件装备 1 兵），
                        // 所以随这批兵带多少装，**不按死 1:1 硬配**，而看目标城自己的账：
                        //   带装量 = min(这批兵该配的量, 目标城的兵装缺口, 源城可动用兵装)
                        //   · 目标城有富余（兵装 ≥ 它的水位线）→ 缺口 0 → **一件不带**，不浪费运力、也不硬塞；
                        //   · 目标城缺装 → 顺手把缺口撮上（同车运过去，省一趟车）。
                        // 水位线本身就是"每千兵 1000 = 1 件装 1 兵"（armsPerThousand），
                        // 所以"目标城的缺口"就是"它离装备得起自己守军还差多少" —— 这才是要害。
                        //
                        // 默认**不因此缩兵、也不卡单**（兵装不需要保证）；只有把 requireArmsForTroops
                        // 打开、且单批 ≥ escortMinTroops 时，才会反过来按兵装缩兵。
                        int escortArms = 0;
                        if (kind == ResourceKind.Troop && ship > 0 && w.escortArmsPerTroop > 0f)
                        {
                            float perTroop = w.escortArmsPerTroop;
                            int want = (int)Math.Ceiling(ship * perTroop);         // 这批兵"该配"的量
                            int gap = D.need[(int)ResourceKind.Arms];              // 目标城的兵装缺口（有富余则为 0）
                            if (want > gap)
                                want = gap;

                            int armsUsable = SourceAvail(S, ResourceKind.Arms, mode, w, frontUrgency);

                            // 严格要求（默认关）：凑不齐这批兵该配的量就按兵装缩兵
                            if (w.requireArmsForTroops && escorts && armsUsable < want)
                            {
                                int maxTroops = (int)Math.Floor(armsUsable / perTroop);
                                if (maxTroops < ship)
                                    ship = maxTroops;
                                if (ship < minShip)
                                    continue;
                                want = (int)Math.Ceiling(ship * perTroop);
                                if (want > gap)
                                    want = gap;
                            }

                            if (want > 0)
                                escortArms = want > armsUsable ? armsUsable : want;

                            if (escortArms > 0)
                            {
                                // 兵装不会被路上吃掉，全额随军抵达
                                S.now[(int)ResourceKind.Arms] -= escortArms;
                                if (S.now[(int)ResourceKind.Arms] < 0)
                                    S.now[(int)ResourceKind.Arms] = 0;
                                D.now[(int)ResourceKind.Arms] += escortArms;
                                D.receivedThisRun[(int)ResourceKind.Arms] += escortArms;
                                D.need[(int)ResourceKind.Arms] -= escortArms;
                                if (D.need[(int)ResourceKind.Arms] < 0)
                                    D.need[(int)ResourceKind.Arms] = 0;
                            }
                        }

                        if (ship < minShip)
                            continue;

                        // 记账：源城现有量减少、实际收货城现有量增加（代表"已承诺在途"），
                        // 后面几趟 / 后续资源都会看到这笔账，避免重复发货或超上限。
                        // need 也要同步递减，报告里的"缺口未补齐"才是**发货之后**真正剩下的缺口。
                        S.now[r] -= ship;
                        D.now[r] += ship;
                        D.receivedThisRun[r] += ship;      // 本趟刚收下 → 不再被当成自有库存往外发
                        D.need[r] -= ship;
                        if (D.need[r] < 0)
                            D.need[r] = 0;
                        need -= ship;
                        if (relayed)
                        {
                            // 中转时"被服务"的其实是最终目标，它的缺口同样要记上
                            T.need[r] -= ship;
                            if (T.need[r] < 0)
                                T.need[r] = 0;
                        }

                        // 排序信息取**最终目标**的档位与紧急度：这张单是替它办的。
                        // 理由里写明最终目的地，否则报告只会看到"运到中转城"，看不出目的。
                        string orderReason = relayed ? reason + "·中转前置→" + T.city.Name : reason;
                        book.Add(S.city, D.city, D.ring, T.priorityTier, T.deficitWeight,
                            orderReason, kind, ship, escortFood, escortArms, c.rawHops);
                        // 护送粮 / 随行兵装同时按各自资源记一笔
                        // （这样才能真正装上车、也会在报告里显示出来）
                        if (escortFood > 0)
                        {
                            book.Add(S.city, D.city, D.ring, T.priorityTier, T.deficitWeight,
                                orderReason, ResourceKind.Food, escortFood);
                        }
                        if (escortArms > 0)
                        {
                            book.Add(S.city, D.city, D.ring, T.priorityTier, T.deficitWeight,
                                orderReason, ResourceKind.Arms, escortArms);
                        }
                    }
                }
            }
        }

        /// <summary>接收方还能收多少（按趟次取不同口径，见 <see cref="AllocMode"/>）。</summary>
        static int ReceiverCapacity(Ledger L, ResourceKind kind, AllocMode mode, ResourceDispatchWeights w)
        {
            int idx = (int)kind;
            if (!L.active[idx])
                return 0;                                       // 该资源在本城不参与调度（参数没开 / 上限取不到）

            // 【港关·无军情】**不接货**：它只是主城的仓储点，水位线近乎 0、仓容却是空的 ——
            // 若按"还有仓容"给它发货，就会变成"主城塞给港口 → 港口下回合又送回主城"的来回倒腾，
            // 白占运输队（每个运输队都要一名武将）。要货的港关必须先是"有军情"。
            if (L.portGate && !L.military)
                return 0;

            int total = L.now[idx] + L.inbound[idx];
            if (mode == AllocMode.OverflowRelief)
            {
                // 仓位安抚：接收方放宽到"安全线"（不再只到自己的水位），但绝不越过安全线。
                // 取不到上限（上限为 0）时不参与 —— 不知道能装多少就不硬塞。
                int limit = L.limit[idx];
                if (limit <= 0)
                    return 0;
                int cap = (int)Math.Ceiling(limit * w.safeMargin) - total;
                return cap > 0 ? cap : 0;
            }
            // 按水位补缺 / 紧急抽调：接收方只补到自己的水位
            int left = L.target[idx] - total;
            return left > 0 ? left : 0;
        }

        /// <summary>
        /// 源城可出口多少（按趟次取不同口径，见 <see cref="AllocMode"/>）。
        /// </summary>
        /// <remarks>
        /// <c>frontUrgency</c> = 前线紧急度（0~1），只在紧急抽调趟生效：
        /// 紧急度 0 → 纵深城只出到"自身水位线的 20%"；紧急度 1 → 底线压到 0，**允许把后方抽空**。
        /// </remarks>
        static int SourceAvail(Ledger L, ResourceKind kind, AllocMode mode, ResourceDispatchWeights w,
            float frontUrgency)
        {
            int idx = (int)kind;
            if (!L.active[idx])
                return 0;                                       // 该资源在本城不参与调度（参数没开 / 上限取不到）

            // 只算**本趟开始前**的自有库存：
            //   · 在途量（L.inbound）不是"可出口"—— 那笔货已经在路上；
            //   · 本趟刚收下的量（L.receivedThisRun）也不算 —— 否则会排出 A→B、B→C 这种落空的单。
            int total = L.now[idx] - L.receivedThisRun[idx];
            if (total <= 0)
                return 0;

            if (mode == AllocMode.Emergency)
            {
                // 紧急抽调：只允许纵深城（圈层 ≥1）跌破自身水位，且不低于紧急底线。
                // 绝不动前线（圈层 0）的库存 —— 前线自己就是最需要货的地方。
                // 底线按**自身水位线**的比例算，与仓容无关：水位线本身就是按剧本调过的绝对数量，
                // 而仓容在改过上限的剧本里高得离谱 —— 拿它算底线会一点货都抽不出来（见 emergencyFloorRatio）。
                if (L.ring < 1)
                    return 0;
                int line = L.target[idx];
                if (line <= 0)
                    return total;                               // 本城这类资源没有水位 → 全部可抽调

                // 底线 = 水位线 × emergencyFloorRatio × (1 − 前线紧急度)：
                // 前线程从"没堆满"到"快守不住"，后方从"留 20% 底仓"逐步让到"被抽空"。
                float urgency = frontUrgency;
                if (urgency < 0f)
                    urgency = 0f;
                else if (urgency > 1f)
                    urgency = 1f;
                float floorRatio = Math.Max(0f, w.emergencyFloorRatio) * (1f - urgency);
                int floor = (int)Math.Floor(line * floorRatio);
                int left = total - floor;
                return left > 0 ? left : 0;
            }
            int avail = total - L.target[idx];
            return avail > 0 ? avail : 0;
        }

        /// <summary>起运门槛：低于门槛不派车（一支运输队要占一个武将，不能为几百粮出动）。</summary>
        static int MinShip(ResourceKind kind, ResourceDispatchWeights w)
        {
            switch (kind)
            {
                case ResourceKind.Gold: return w.minShipGold;
                case ResourceKind.Food: return w.minShipFood;
                case ResourceKind.Troop: return w.minShipTroops;
                case ResourceKind.Arms: return w.minShipArms;
                case ResourceKind.Machine: return w.minShipMachine;
                default: return w.minShipBoat;
            }
        }

        /// <summary>
        /// "攒够一车再走"（<see cref="ResourceDispatchWeights.batchShipRatio"/>）的**救急豁免**：
        /// 收货城此刻等不起攒车，货要立刻发。
        ///
        /// 目前只有一个口径 —— **粮**：口粮不足 <c>batchFoodCrisisTurns</c> 回合
        /// （口粮 = 兵力 × baseFoodCostInTroop，即"1 粮养 10 兵"）。
        /// 理由：断粮的队伍每回合直接掉 30% 的兵，而粮草缺口在改过上限的剧本里动辄几十万，
        /// 按比例压批次会让一车都凑不齐 —— 那边省下几名押车武将，这边饿掉几万兵，不值。
        /// </summary>
        /// <param name="T">收货城账本</param>
        /// <param name="kind">资源种类</param>
        /// <param name="w">参数</param>
        /// <param name="scenario">剧本（取口粮系数）</param>
        /// <returns>true = 不压批次，立刻发</returns>
        static bool BatchCrisisBypass(Ledger T, ResourceKind kind, ResourceDispatchWeights w, Scenario scenario)
        {
            if (T == null || T.city == null || kind != ResourceKind.Food)
                return false;
            float turns = Math.Max(0f, w.batchFoodCrisisTurns);
            if (turns <= 0f)
                return false;
            float perTurn = T.city.troops * (scenario != null && scenario.Variables != null
                ? scenario.Variables.baseFoodCostInTroop : 0.1f);
            return perTurn > 0f && T.city.food < perTurn * turns;
        }

        /// <summary>
        /// 挑源城：只从**顺梯度**的方向取货（源圈层 ≥ 目标圈层），港关↔归属都市例外。
        /// 排序：更靠后方的优先（先把纵深囤货搬走）→ 离得近的优先 → 可出口量大的优先 → 城 id 兜底。
        /// </summary>
        /// <remarks><c>frontUrgency</c> = 前线紧急度（0~1），只影响紧急抽调趟里"纵深城可以被抽到什么程度"。</remarks>
        static List<SourceCandidate> CollectSources(List<Ledger> ledgers, Ledger T, ResourceKind kind,
            AllocMode mode, ResourceDispatchWeights w, float frontUrgency)
        {
            List<SourceCandidate> result = new List<SourceCandidate>();
            if (T.hops == null)
                return result;

            for (int i = 0; i < ledgers.Count; i++)
            {
                Ledger S = ledgers[i];
                if (S.city == T.city || S.hops == null)
                    continue;
                // 源城必须能成军（CreateTransportTroop 要求城内还有兵）
                if (S.city.troops <= 0)
                    continue;
                // 【守城优先】兵临城下的城不出货：守军需要这些粮草，运输队也多半出不了城
                if (w.skipBesiegedSource && S.underSiege)
                    continue;

                // 【港关·无军情】只往**归属主城**送货：它是主城伸出去的仓储点，
                // 由主城统一往前线 / 其它城转（避免几十个渡口各自乱发货、也避免它们去抢运力）。
                if (w.portGateOnlyToParent && S.portGate && !S.military
                    && S.city.BelongCity != null && S.city.BelongCity != T.city)
                    continue;

                int hops;
                if (!S.hops.TryGetValue(T.city.Id, out hops) && !T.hops.TryGetValue(S.city.Id, out hops))
                    continue;                                   // 域内不连通（被敌占 / 数据缺失）

                bool parentLink = IsParentLink(S.city, T.city);
                if (!parentLink && S.ring < T.ring)
                    continue;                                   // 不许把前线的东西往后方倒

                SourceCandidate c = new SourceCandidate();
                c.index = i;
                c.ring = S.ring;
                c.cityId = S.city.Id;
                c.rawHops = hops;                                   // 原始距离（中转接力按它判定）
                c.hops = hops - (parentLink ? w.parentCityBias : 0);
                // 军团委任指定了运输目标城：该军团的货优先往这座城送（玩家的人工干预通道）
                if (T.city.Id == DesignatedTransportTarget(S.city))
                    c.hops -= w.designatedTargetBias;
                if (c.hops < 1)
                    c.hops = 1;
                c.avail = SourceAvail(S, kind, mode, w, frontUrgency);
                if (c.avail <= 0)
                    continue;
                result.Add(c);
            }

            result.Sort(delegate (SourceCandidate a, SourceCandidate b)
            {
                if (a.ring != b.ring) return b.ring.CompareTo(a.ring);
                if (a.hops != b.hops) return a.hops.CompareTo(b.hops);
                if (a.avail != b.avail) return b.avail.CompareTo(a.avail);
                return a.cityId.CompareTo(b.cityId);
            });
            return result;
        }

        /// <summary>
        /// 该城的军团是否禁止运输（军团委任 <c>Corps.AppointContentType.TransportDisable</c>：1 = 禁止）。
        /// 与旧的城池运输命令同一判定 —— 玩家的"运输禁止"必须仍然算数。
        /// </summary>
        static bool IsTransportDisabled(City city)
        {
            Corps corps = city != null ? city.BelongCorps : null;
            if (corps == null)
                return false;
            return corps.GetAppointValue(Corps.AppointContentType.TransportDisable) == 1;
        }

        /// <summary>
        /// 该城军团委任指定的运输目标城 id（<c>Corps.AppointContentType.Transport</c>；0 = 未指定）。
        /// 指定后该军团的货**优先**往这座城送（见 ResourceDispatchWeights.designatedTargetBias）。
        /// </summary>
        static int DesignatedTransportTarget(City city)
        {
            Corps corps = city != null ? city.BelongCorps : null;
            if (corps == null)
                return 0;
            int v = corps.GetAppointValue(Corps.AppointContentType.Transport);
            return v > 0 ? v : 0;
        }

        /// <summary>港关与归属都市是否互为同一资源单元（调运成本打优惠）。</summary>
        static bool IsParentLink(City a, City b)
        {
            if (a == null || b == null)
                return false;
            if (a.BelongCity != null && a.BelongCity == b)
                return true;
            return b.BelongCity != null && b.BelongCity == a;
        }

        // ==================== 图 / 在途 ====================

        // ---- 域内跳数表缓存 ----
        // 每城一张"域内跳数表"意味着 O(N) 次 BFS、整体 O(N²)；而**域内城池集合没变时结果完全一样**
        // （图的边来自静态的地图邻接表 + 港关↔归属都市，只有域本身变了结果才会变）。
        // 所以按"域城池 id 序列"**精确比较**后复用：只有归属发生变化的那一回合才重算。

        /// <summary>缓存所依附的剧本</summary>
        static Scenario cachedHopsScenario;
        /// <summary>缓存对应的域城池 id 序列（按域采集顺序，逐项精确比较）</summary>
        static int[] cachedHopsDomainIds;
        /// <summary>缓存：城池 id → 域内跳数表</summary>
        static Dictionary<int, Dictionary<int, int>> cachedHops;

        /// <summary>
        /// 取本作用域的"每城跳数表"（城池 id → 域内跳数表）。
        /// 域城池集合与上次完全一致且同一剧本时直接复用缓存，一张 BFS 都不用跑。
        /// </summary>
        /// <param name="domain">域内城池（采集顺序即比较顺序）</param>
        /// <param name="domainIds">域内城池 id 集合</param>
        /// <param name="scenario">剧本</param>
        /// <returns>城池 id → 跳数表（永不为 null）</returns>
        static Dictionary<int, Dictionary<int, int>> GetDomainHops(List<City> domain, HashSet<int> domainIds,
            Scenario scenario)
        {
            if (domain == null || domainIds == null)
                return null;

            if (ReferenceEquals(cachedHopsScenario, scenario) && cachedHops != null
                && SameDomain(cachedHopsDomainIds, domain))
                return cachedHops;

            Dictionary<int, Dictionary<int, int>> fresh =
                new Dictionary<int, Dictionary<int, int>>(domain.Count);
            int[] ids = new int[domain.Count];
            for (int i = 0; i < domain.Count; i++)
            {
                City city = domain[i];
                if (city == null)
                    continue;
                ids[i] = city.Id;
                fresh[city.Id] = BfsHops(city, domainIds);
            }

            cachedHopsScenario = scenario;
            cachedHopsDomainIds = ids;
            cachedHops = fresh;
            return fresh;
        }

        /// <summary>
        /// 接壤的外势力 / 无主城名单（最多 2 个，报告用）。
        /// 接壤**布尔值**统一走 <see cref="CitySituation.hasForeignNeighbor"/>（只算一次，与人才调度同口径）。
        /// <c>[无主]</c> 是重点：它常常就是"这座城凭什么算有军情"的真实答案。
        /// </summary>
        /// <param name="city">城池</param>
        /// <returns>形如 "南阳[袁术],汉中[无主]"；没有接壤城返回空串</returns>
        static string ForeignNeighborText(City city)
        {
            if (city == null || city.NeighborList == null)
                return string.Empty;

            StringBuilder sb = null;
            int shown = 0;
            for (int i = 0; i < city.NeighborList.Count && shown < 2; i++)
            {
                City n = city.NeighborList[i];
                if (n == null || n == city || n.IsSameForce(city))
                    continue;

                if (sb == null)
                    sb = new StringBuilder();
                else
                    sb.Append(',');
                sb.Append(n.Name).Append('[')
                  .Append(n.BelongForce != null ? n.BelongForce.Name : "无主")
                  .Append(']');
                shown++;
            }
            return sb != null ? sb.ToString() : string.Empty;
        }

        /// <summary>缓存里的域城池序列与本次是否逐项一致（顺序由 <see cref="CollectDomain"/> 决定，稳定）。</summary>
        static bool SameDomain(int[] cachedIds, List<City> domain)
        {
            if (cachedIds == null || cachedIds.Length != domain.Count)
                return false;
            for (int i = 0; i < cachedIds.Length; i++)
            {
                City city = domain[i];
                if (city == null || cachedIds[i] != city.Id)
                    return false;
            }
            return true;
        }

        /// <summary>域内跳数表（本城 → 其它城）。只走域内城池，因此天然避开敌占区。</summary>
        static Dictionary<int, int> BfsHops(City start, HashSet<int> domainIds)
        {
            Dictionary<int, int> hops = new Dictionary<int, int>();
            if (start == null || domainIds == null)
                return hops;

            Queue<City> queue = new Queue<City>();
            hops[start.Id] = 0;
            queue.Enqueue(start);

            List<City> links = new List<City>();
            while (queue.Count > 0)
            {
                City cur = queue.Dequeue();
                int d = hops[cur.Id];
                CollectLinks(cur, links);
                for (int i = 0; i < links.Count; i++)
                {
                    City next = links[i];
                    if (next == null || !next.IsAlive)
                        continue;
                    if (!domainIds.Contains(next.Id))
                        continue;
                    if (hops.ContainsKey(next.Id))
                        continue;
                    hops[next.Id] = d + 1;
                    queue.Enqueue(next);
                }
            }
            return hops;
        }

        /// <summary>
        /// 取一座城的连接关系：城池邻接表 + 港关 / 归属都市。
        /// 后两者补进来是必须的 —— "港关与归属都市是同一个资源单元"这条规则
        /// 不能依赖邻接表里恰好写了这一对。
        /// </summary>
        static void CollectLinks(City city, List<City> output)
        {
            output.Clear();
            if (city.NeighborList != null)
            {
                for (int i = 0; i < city.NeighborList.Count; i++)
                {
                    City n = city.NeighborList[i];
                    if (n != null && n != city)
                        output.Add(n);
                }
            }
            if (city.BelongCity != null && city.BelongCity != city)
                output.Add(city.BelongCity);
            if (city.subCities != null)
            {
                for (int i = 0; i < city.subCities.Count; i++)
                {
                    City sub = city.subCities[i];
                    if (sub != null && sub != city)
                        output.Add(sub);
                }
            }
        }

        /// <summary>
        /// 统计"正在路上、目的地是本城"的部队货量（金 / 粮 / 兵 / 兵装 / 器械 / 船）。
        /// 必须算进去：否则同一座城会被反复发货，货到之后一起超过上限被丢掉。
        ///
        /// 两类都要数：
        ///   · <c>TroopTransformGoodsToCity</c>：正在送货的运输队；
        ///   · <c>TroopReturnCity</c>：**返程**的运输队 / 打道回府的部队 ——
        ///     目标城易主或即将失守时运输队会掉头回创建城，返程货依然会入库
        ///     （<c>Troop.EnterCity</c> 里 AddGold / AddFood / AddTroops / itemStore.Add 都有），
        ///     漏算它们就会在"部队回城那一下"把自家城顶爆 —— 正是要避免的事。
        /// </summary>
        static void SumInbound(Scenario scenario, int cityId, int[] inbound)
        {
            if (scenario == null || scenario.troopsSet == null || inbound == null)
                return;

            for (int i = 0; i < scenario.troopsSet.Count; i++)
            {
                Troop t = scenario.troopsSet[i];
                if (t == null || !t.IsAlive)
                    continue;
                if (t.missionTarget != cityId)
                    continue;

                bool isTransport = t.IsTransport
                    && t.missionType == (int)MissionType.TroopTransformGoodsToCity;
                bool isReturning = t.missionType == (int)MissionType.TroopReturnCity;
                if (!isTransport && !isReturning)
                    continue;

                inbound[(int)ResourceKind.Gold] += t.gold;
                inbound[(int)ResourceKind.Food] += t.food;
                inbound[(int)ResourceKind.Troop] += t.troops;
                inbound[(int)ResourceKind.Arms] += WeaponCount(t.itemStore);
                inbound[(int)ResourceKind.Machine] += MachineCount(t.itemStore);
                inbound[(int)ResourceKind.Boat] += BoatCount(t.itemStore);
            }
        }

        /// <summary>兵装件数（枪 / 戟 / 弩 / 马，与 <see cref="CitySituation.weaponCount"/> 同口径）。</summary>
        internal static int WeaponCount(ItemStore store)
        {
            return CountKinds(store, (int)ItemStoreKindType.Spear, (int)ItemStoreKindType.Horse);
        }

        /// <summary>器械件数（冲车 / 投石，与 <see cref="CitySituation.machineCount"/> 同口径）。</summary>
        internal static int MachineCount(ItemStore store)
        {
            return CountKinds(store, (int)ItemStoreKindType.Helepolis, (int)ItemStoreKindType.Catapult);
        }

        /// <summary>船只数（走舸 / 楼船 / 斗舰 —— 道具库里只有一个 <c>Boat</c> 种类）。</summary>
        internal static int BoatCount(ItemStore store)
        {
            return CountKinds(store, (int)ItemStoreKindType.Boat, (int)ItemStoreKindType.Boat);
        }

        /// <summary>按"连续的道具种类 id 区间"统计件数（区间是闭区间）。</summary>
        static int CountKinds(ItemStore store, int fromKind, int toKind)
        {
            if (store == null)
                return 0;
            int total = 0;
            for (int id = fromKind; id <= toKind; id++)
                total += store.GetNumber(id);
            return total;
        }

        // ==================== 道具容器的上限（兵装 / 器械 / 船） ====================
        // 道具库**有上限**：单类上限 = 城池仓库上限 × 该道具的 p1 千分比
        // （<see cref="ItemType.TransformLimit"/>；p1 == 0 表示占满整个仓库上限）。
        // 同一 storeKind 可能挂多个道具（例：冲车 / 木兽共用 6 号库），它们**共用一格库存**，
        // 因此按"该 storeKind 里最大的比例"折算 —— 对应"再也没有任何一类道具能继续生产"的那条线。
        // 组级上限（兵装 = 4 个 storeKind，器械 = 2 个，船 = 1 个）= 各组内上限之和。

        /// <summary>比例缓存所属的剧本（换剧本自动重建）</summary>
        static Scenario cachedItemScenario;
        /// <summary>storeKind → 上限比例（千分比；p1 == 0 已归一成 1000 = 整个仓库上限）</summary>
        static Dictionary<int, int> storeKindP1;

        /// <summary>按剧本重建 storeKind → 比例表（同一 storeKind 取最大比例）。</summary>
        static void EnsureItemCapCache(Scenario scenario)
        {
            if (ReferenceEquals(cachedItemScenario, scenario) && storeKindP1 != null)
                return;

            cachedItemScenario = scenario;
            storeKindP1 = new Dictionary<int, int>();

            if (scenario == null || scenario.CommonData == null || scenario.CommonData.ItemTypeList == null)
                return;

            List<ItemType> list = scenario.CommonData.ItemTypeList;
            for (int i = 0; i < list.Count; i++)
            {
                ItemType t = list[i];
                if (t == null)
                    continue;
                // p1 == 0 → TransformLimit 直接返回仓库上限，归一成 1000 便于与其它道具比较大小
                int p1 = t.p1 == 0 ? 1000 : t.p1;
                int kind = t.storeKind;

                int old;
                if (!storeKindP1.TryGetValue(kind, out old) || p1 > old)
                    storeKindP1[kind] = p1;
            }
        }

        /// <summary>
        /// 道具类资源（兵装 / 器械 / 船）在该城的容器上限：
        /// 组内每个 storeKind 各自按 <c>仓库上限 × p1 / 1000</c> 换算后求和。
        /// 返回 0 表示**取不到上限**（道具数据缺失或城池仓库上限为 0）→ 调用方按"不设上限"处理，
        /// 避免因为数据缺失就把兵装 / 器械 / 船彻底停运。
        /// </summary>
        /// <param name="city">城池（用它的 <see cref="City.StoreLimit"/>）</param>
        /// <param name="scenario">剧本（取道具表）</param>
        /// <param name="fromKind">组内起始 storeKind（闭区间）</param>
        /// <param name="toKind">组内结束 storeKind（闭区间）</param>
        internal static int GetItemGroupLimit(City city, Scenario scenario, int fromKind, int toKind)
        {
            if (city == null || scenario == null)
                return 0;

            EnsureItemCapCache(scenario);
            if (storeKindP1 == null || storeKindP1.Count == 0)
                return 0;

            int storeLimit = city.StoreLimit;
            if (storeLimit <= 0)
                return 0;

            int total = 0;
            for (int id = fromKind; id <= toKind; id++)
            {
                int p1;
                if (storeKindP1.TryGetValue(id, out p1))
                    total += storeLimit * p1 / 1000;
            }
            return total;
        }

        // ==================== 报告 ====================

        static string ReasonText(Ledger T, AllocMode mode)
        {
            string tag;
            if (T.ring == 0)
                tag = T.attacking ? "前线进攻" : "前线防御";
            else if (T.ring == 1)
                tag = "次前线支援";
            else
                tag = "后方储备";

            if (mode == AllocMode.OverflowRelief)
                tag += "·溢出安抚";
            else if (mode == AllocMode.Emergency)
                tag += "·紧急抽调";

            // 水位线现在是绝对值，各路资源各不相同 —— 具体数字在报告开头的城况行里按资源列出，
            // 这里只写圈层档位与战况（原来那行"水位 90%"是"仓容的百分比"，改口径后已无意义）。
            if (T.underSiege)
                tag += "·被围";
            else if (T.peaceful)
                tag += "·太平";
            return string.Format("{0} 圈层{1}", tag, T.ring);
        }

        static string CityStateText(Ledger L, ResourceDispatchWeights w)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[').Append(L.city.Name).Append("] 圈层").Append(L.ring);
            if (L.ring == 0)
                sb.Append(L.attacking ? "(进攻)" : "(防御)");
            if (L.portGate)
            {
                // 港关分两种：有军情（要兵要粮守关口）/ 无军情（只是主城的仓储点，金粮全上交主城）
                sb.Append(L.military ? "(港关·军情" : "(港关·仓储");
                if (L.military && !string.IsNullOrEmpty(L.militaryReason))
                    sb.Append(':').Append(L.militaryReason);
                sb.Append(')');
            }
            if (L.underSiege)
                sb.Append("(被围)");
            if (L.doomed)
                sb.Append("(将失守)");
            sb.Append(' ');
            AppendState(sb, L, ResourceKind.Gold, "金");
            AppendState(sb, L, ResourceKind.Food, "粮");
            AppendState(sb, L, ResourceKind.Troop, "兵");
            AppendState(sb, L, ResourceKind.Arms, "装");
            AppendState(sb, L, ResourceKind.Machine, "器");
            AppendState(sb, L, ResourceKind.Boat, "船");
            return sb.ToString();
        }

        static void AppendState(StringBuilder sb, Ledger L, ResourceKind kind, string label)
        {
            int idx = (int)kind;
            if (!L.active[idx])
            {
                // 该资源在本城不参与调度（参数没开 / 上限取不到）→ 不报数字，免得被误读成"库存为 0"
                sb.Append(label).Append(" - ");
                return;
            }

            // 读法：现有 / 水位线（绝对值） 达成率 —— 达成率 >100% 表示"超出基本线、有富余可外运"。
            // 这里刻意**不**按仓容上限算百分比：改过上限的剧本里那个百分比毫无参考价值
            //（而且会让人误以为"水位线 = 上限的百分之几"）。
            sb.Append(label).Append(' ').Append(L.now[idx]);
            if (L.inbound[idx] > 0)
                sb.Append("(在途+").Append(L.inbound[idx]).Append(')');
            sb.Append('/').Append(L.target[idx]);
            if (L.target[idx] > 0)
                sb.Append(' ').Append(((float)L.now[idx] / L.target[idx]).ToString("P0"));
            else
                sb.Append(" 无水位线");
            sb.Append(' ');
        }

        static void AppendGap(ref StringBuilder sb, Ledger L, ResourceKind kind, string label)
        {
            int idx = (int)kind;
            if (L.need[idx] <= 0)
                return;
            if (sb == null)
                sb = new StringBuilder();
            else
                sb.Append(' ');
            sb.Append(label).Append(L.need[idx]);
        }
    }
}
