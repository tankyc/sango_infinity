using System;

namespace Sango.Core
{
    /// <summary>
    /// 执行层：把一张调拨单落成一支**运输部队**。
    ///
    /// 刻意**不新造运输通道** —— 复用城池既有的一条路径：
    ///   <c>CityAI.AIMakeTransportTroop</c>（挑运输主将、扣货、挂 <c>TroopTransformGoodsToCity</c> 任务）
    ///   + <c>CityTroopFactory.EmitTroop</c>（登记出城）。
    /// 于是"路上会被敌军拦截 / 到城由 <c>Troop.EnterCity</c> 入库"这些既有行为全部保留，
    /// 玩家与 AI 看到的是同一种运输队。
    ///
    /// 【可运的货】金、粮、兵，以及道具库里的三类军械：
    ///   兵装（枪 / 戟 / 弩 / 马）、器械（冲车 / 投石）、船（走舸 / 楼船 / 斗舰）。
    ///
    /// 【扣货契约 · 重要】<c>AIMakeTransportTroop</c> 会在最后一步执行
    /// <c>city.itemStore.Remove(itemStore)</c>（与 <c>AITransfromToBelongCity</c> 传 <c>Split(part, true)</c> 同一契约），
    /// 所以本类**只负责准备货、不负责扣货** —— 组队失败（缺人 / 缺编制）时城里一件都不会少，
    /// 也不会出现"先扣一次、成军时再扣一次"的双重扣除。
    /// </summary>
    public static class ResourceTransfer
    {
        /// <summary>
        /// "源城无空闲武将"这条失败原因。
        /// 导出成常量是因为 <see cref="ResourceDispatcher"/> 要按它登记"待发存量"人员需求 ——
        /// 用字符串字面量两处比对，改一处就会静默失效。
        /// </summary>
        public const string ReasonNoFreeEnvoy = "源城无空闲武将";

        /// <summary>兵装四件套（与 <see cref="CitySituation"/> 统计 weaponCount 的口径一致）。</summary>
        static readonly int[] WeaponKinds = new int[]
        {
            (int)ItemStoreKindType.Spear,
            (int)ItemStoreKindType.Halberd,
            (int)ItemStoreKindType.Crossbow,
            (int)ItemStoreKindType.Horse,
        };

        /// <summary>器械（与 <see cref="CitySituation"/> 统计 machineCount 的口径一致）。</summary>
        static readonly int[] MachineKinds = new int[]
        {
            (int)ItemStoreKindType.Helepolis,
            (int)ItemStoreKindType.Catapult,
        };

        /// <summary>船（道具库里只有一个 <c>Boat</c> 种类）。</summary>
        static readonly int[] BoatKinds = new int[]
        {
            (int)ItemStoreKindType.Boat,
        };

        /// <summary>
        /// 派一支运输队把货从 <paramref name="from"/> 送到 <paramref name="to"/>。
        /// </summary>
        /// <param name="from">源城</param>
        /// <param name="to">目标城</param>
        /// <param name="shipment">调拨单</param>
        /// <param name="scenario">剧本</param>
        /// <param name="w">参数（起运门槛以外的兜底判定用）</param>
        /// <param name="reason">失败原因（成功为 null）</param>
        /// <returns>是否真的派出了运输队</returns>
        public static bool Ship(City from, City to, ResourceShipment shipment, Scenario scenario,
            ResourceDispatchWeights w, out string reason)
        {
            reason = null;
            if (from == null || to == null || scenario == null)
            {
                reason = "参数缺失";
                return false;
            }
            if (from == to)
            {
                reason = "源城与目标城相同";
                return false;
            }
            if (from.BelongForce == null || from.BelongForce != to.BelongForce)
            {
                reason = "势力不同";
                return false;
            }
            // 【人员下限】别把源城最后的空闲武将都抽去赶车：
            // 运输主将抵达目标城后就**并入目标城**（Troop.EnterCity），等于一次人员外流；
            // 抽空最后几个人会让源城的征兵 / 内政当场停摆（而编制层的"后方保底"管不到运输主将）。
            //
            // 【兵装待产城再让一步】本城守军缺兵装、**而且留人真能帮上忙**时，才**多留几个人给生产端**：
            // 造兵装（经典内政的 AICreateItems / 工作制的锻冶在岗）同样要吃空闲武将，
            // 被运输抽干就会出现"运来一堆兵、却一件兵装都造不出来"的自锁。
            //
            // 【关键】留人只对**能造兵装的城**成立（CanProduceArms：都市 + 有锻冶/马厩 + 有钱）。
            // 港关不产内政，给它们留人毫无意义 —— 早先没加这个判断时，渡口会因为
            // "守军缺装"被要求留 3 名空闲武将，而它们往往只有 2 名，于是**一件货都发不出去**，
            // 只剩"待发存量"的岗一年年挂着（部署日志里满屏的 运输(5)+运输(6) 就是这么来的）。
            if (from.freePersons == null)
            {
                reason = ReasonNoFreeEnvoy;
                return false;
            }

            // 【港关只需 1 人】港关不产内政、不征兵，编制上（无军情时）**只有 1 个运输岗** ——
            // 那唯一的空闲武将本来就是"押车的人"。若仍按都市的 minSourceFreePersons(2) 判定，
            // 绝大多数渡口（在册 1 人）会**永远发不出车**：逐回合登记"待发存量"、逐回合被拒，
            // 货只能堆到溢出（背压里那些 金 / 粮 就是这么堆住的）。
            bool isPortGate = from.IsPort() || from.IsGate();
            int needFree = isPortGate
                ? 1
                : Math.Max(1, w != null ? w.minSourceFreePersons : 1);
            if (w != null && w.armsDemandProtectPersons > 0 && w.armsDemandCoverRatio > 0f
                && ResourceBalance.WeaponCount(from.itemStore) < from.troops * w.armsDemandCoverRatio
                && ResourceBalance.CanProduceArms(from))
                needFree += w.armsDemandProtectPersons;
            if (from.freePersons.Count < needFree)
            {
                reason = ReasonNoFreeEnvoy;
                return false;
            }
            if (from.troops <= 0)
            {
                reason = "源城无兵";
                return false;
            }
            if (TroopType.GetTransportType(scenario, from.BelongForce) == null)
            {
                reason = "无可用运输队编制";
                return false;
            }

            // 【通路】沿途经过敌方建筑时不要硬闯 —— 旧的城池运输命令一直有这条检查
            // （CityAI.IsPathClear），换成资源调度后必须保留：否则运输队会被半路吃掉、整车物资全丢。
            // 放在"备货"之前，失败时城里一件货都不会少。
            if (w != null && w.requireClearPath && !CityAI.IsPathClear(from, to, scenario))
            {
                reason = "沿途有敌军阻路";
                return false;
            }

            // ---------- 二次夹取 ----------
            // 求解到执行之间局面可能已经变了（部队回城、别的运输队先抢了货），
            // 所以这里按**当前实际库存**再夹一次，绝不让库存变成负数。
            int gold = shipment.gold > 0 ? Math.Min(shipment.gold, from.gold) : 0;
            int food = shipment.food > 0 ? Math.Min(shipment.food, from.food) : 0;
            int troops = shipment.troops > 0 ? Math.Min(shipment.troops, from.troops) : 0;
            // 运输队本身至少要 1 兵才出得了城（旧运输逻辑在"目标已满"时也是运 1 兵）
            if (troops < 1)
                troops = 1;
            if (troops > from.troops)
            {
                reason = "源城兵力不足";
                return false;
            }

            // ---------- 护送粮兜底（兵粮一定要保证，任何规模都不例外） ----------
            // 本单为这些兵预留了 shipment.escortFood 的干粮（口径：> 2 倍路程消耗，见 ResourceBalance）。
            // 求解到执行之间源城的粮可能已经变少，那就**按比例缩兵**：
            // 断粮的队伍每回合掉 30% 兵（Troop.OnForceTurnStart），
            // 与其送一支半路减员的援军、还把口粮负担丢给目标城，不如少运一点。
            // 兵装不做要求（w.requireArmsForTroops 默认关），所以这里不需要对应兜底。
            int escort = shipment.escortFood;
            if (shipment.troops > 0 && shipment.escortFood > 0 && food < shipment.escortFood)
            {
                int scaled = (int)((long)shipment.troops * food / shipment.escortFood);
                if (scaled < troops)
                    troops = scaled;
                if (troops < 1)
                    troops = 1;
            }
            if (escort > food)
                escort = food;
            if (escort < 0)
                escort = 0;

            float safeMargin = w != null ? w.safeMargin : 0.95f;

            // ---------- 目标容量兜底 ----------
            // 求解到执行之间局面可能又变了（别的运输队先到货 / 城内生产），
            // 这里按目标城的**当前仓容**再夹一次，保证"不超过容器上限"不依赖求解时的快照。
            // 道具容器上限 = 城池仓库上限 × 道具比例（ItemType.TransformLimit，见 ResourceBalance.GetItemGroupLimit）。
            int armsWant = ClampToRoom(to, scenario, shipment.arms,
                (int)ItemStoreKindType.Spear, (int)ItemStoreKindType.Horse,
                ResourceBalance.WeaponCount(to.itemStore), safeMargin);
            int machineWant = ClampToRoom(to, scenario, shipment.machines,
                (int)ItemStoreKindType.Helepolis, (int)ItemStoreKindType.Catapult,
                ResourceBalance.MachineCount(to.itemStore), safeMargin);
            int boatWant = ClampToRoom(to, scenario, shipment.boats,
                (int)ItemStoreKindType.Boat, (int)ItemStoreKindType.Boat,
                ResourceBalance.BoatCount(to.itemStore), safeMargin);

            // 只有军械的单：城里那批道具可能已经被别的运输队 / 生产消耗掉了，
            // 或者目标城仓容已满 —— 这时不该派一支只运 1 兵的"空车"出去（白占一个武将）。
            if (gold <= 0 && food <= 0 && shipment.troops <= 0
                && armsWant <= 0 && machineWant <= 0 && boatWant <= 0)
            {
                reason = "没有可运的货物";
                return false;
            }

            // ---------- 备货（不扣货，扣货由 AIMakeTransportTroop 完成，见类注释） ----------
            ItemStore cargo = BuildCargo(from, armsWant, machineWant, boatWant);

            Troop troop = CityAI.AIMakeTransportTroop(from, to, troops, gold, food, cargo, scenario);
            if (troop == null)
            {
                reason = "运输队组建失败";
                return false;
            }

            troop = CityTroopFactory.EmitTroop(from, troop, scenario);
            if (troop == null)
            {
                reason = "运输队登记失败";
                return false;
            }
            troop.missionParams1 = 1;
            from.CurActiveTroop = troop;

            Sango.Log.Info(string.Format(
                "{0}{1}势力在{2}由{3}率领运输队向{4}运输物资: 金{5} 粮{6}(护送{12}) 兵{7} 装{8} 器{9} 船{10}({11})",
                scenario.GetDateStr(), from.BelongForce.Name, from.Name,
                troop.Leader != null ? troop.Leader.Name : "?",
                to.Name, gold, food, troops,
                ResourceBalance.WeaponCount(cargo),
                ResourceBalance.MachineCount(cargo),
                ResourceBalance.BoatCount(cargo),
                shipment.reason, escort));
            return true;
        }

        /// <summary>
        /// 按调拨单准备货物容器（**只读源城库存、不扣货**）。
        /// 三类军械各自按比例在自己内部摊分，不把某一种一次抽干。
        /// </summary>
        /// <param name="from">源城</param>
        /// <param name="arms">兵装件数（已按目标城仓容夹过）</param>
        /// <param name="machines">器械件数（已按目标城仓容夹过）</param>
        /// <param name="boats">船只数（已按目标城仓容夹过）</param>
        /// <returns>货物容器（可能为空容器）</returns>
        static ItemStore BuildCargo(City from, int arms, int machines, int boats)
        {
            ItemStore cargo = new ItemStore();
            if (from == null || from.itemStore == null)
                return cargo;

            FillCargo(cargo, from.itemStore, WeaponKinds, arms);
            FillCargo(cargo, from.itemStore, MachineKinds, machines);
            FillCargo(cargo, from.itemStore, BoatKinds, boats);
            return cargo;
        }

        /// <summary>
        /// 目标城还能收多少件该类道具：<c>容器上限 × safeMargin − 现有</c>。
        /// 上限按 <c>城池仓库上限 × 道具比例</c> 换算（<see cref="ResourceBalance.GetItemGroupLimit"/>）；
        /// **取不到上限时不限制**（道具数据缺失 / 仓库上限为 0），避免误判导致军械完全不运。
        /// </summary>
        /// <param name="to">目标城</param>
        /// <param name="scenario">剧本</param>
        /// <param name="want">本单原计划的件数</param>
        /// <param name="fromKind">组内起始 storeKind（闭区间）</param>
        /// <param name="toKind">组内结束 storeKind（闭区间）</param>
        /// <param name="now">目标城该类道具现有件数</param>
        /// <param name="safeMargin">安全余量（不超过上限的该比例）</param>
        /// <returns>实际可运件数</returns>
        static int ClampToRoom(City to, Scenario scenario, int want, int fromKind, int toKind, int now, float safeMargin)
        {
            if (want <= 0)
                return 0;
            if (to == null)
                return want;

            int limit = ResourceBalance.GetItemGroupLimit(to, scenario, fromKind, toKind);
            if (limit <= 0)
                return want;                                    // 取不到上限 → 不做限制

            int room = (int)Math.Ceiling(limit * safeMargin) - now;
            if (room <= 0)
                return 0;
            return Math.Min(want, room);
        }

        /// <summary>
        /// 把 <paramref name="kinds"/> 这几类道具按比例取出 <paramref name="want"/> 件放进 <paramref name="cargo"/>。
        /// **不改动 <paramref name="source"/>**（扣货留给 AIMakeTransportTroop）。
        /// </summary>
        /// <param name="cargo">目标容器</param>
        /// <param name="source">源容器（只读）</param>
        /// <param name="kinds">参与摊分的道具种类</param>
        /// <param name="want">想要的总件数</param>
        static void FillCargo(ItemStore cargo, ItemStore source, int[] kinds, int want)
        {
            if (cargo == null || source == null || kinds == null || kinds.Length == 0 || want <= 0)
                return;

            int total = 0;
            for (int i = 0; i < kinds.Length; i++)
                total += source.GetNumber(kinds[i]);
            if (total <= 0)
                return;
            int take = Math.Min(want, total);

            int[] have = new int[kinds.Length];
            int[] takeArr = new int[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                have[i] = source.GetNumber(kinds[i]);
                takeArr[i] = (int)((long)have[i] * take / total);
            }

            // 整除的余数按"谁还有货"依次补，保证总量正好等于 take
            int left = take;
            for (int i = 0; i < takeArr.Length; i++)
                left -= takeArr[i];
            while (left > 0)
            {
                bool progressed = false;
                for (int i = 0; i < takeArr.Length && left > 0; i++)
                {
                    if (takeArr[i] < have[i])
                    {
                        takeArr[i]++;
                        left--;
                        progressed = true;
                    }
                }
                if (!progressed)
                    break;                                  // 理论不可达，兜底防死循环
            }

            for (int i = 0; i < kinds.Length; i++)
            {
                if (takeArr[i] > 0)
                    cargo.Add(kinds[i], takeArr[i]);
            }
        }
    }
}
