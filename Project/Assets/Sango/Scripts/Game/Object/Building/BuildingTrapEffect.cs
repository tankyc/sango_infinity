using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 陷阱爆发效果（对齐 Open311 的 trap_anim / trap_next / fireball_trap_anim / calc_trap_damage / calc_fire_damage）。
    ///
    /// 【触发时机】命中即爆：由 <see cref="Building.OnHit"/> 在耐久扣减前调用，
    /// 与 Open311「火球 / 落石被攻击命中时立即结算并自毁」一致。
    /// 非攻击性销毁（势力灭亡清场、中立化、未完工拆除）走 OnFall，不引爆。
    ///
    /// 【爆发模型】原版陷阱爆炸是「连锁引爆」而非「圆形 AoE」：
    ///   · 被引爆的陷阱只点燃自己那一格，并向外扩散（火种周围一圈 / 火球沿直线滚）；
    ///   · 扩散到的每一格：点燃 + 对上面的部队 / 建筑造成「陷阱爆炸 + 火焰」即时伤害；
    ///   · 若扩散格上还有另一个已完成陷阱，则它连锁引爆（销毁并继续扩散）。
    /// 详见 <see cref="TrapChain"/>。
    ///
    /// 【覆盖的设施】
    ///   · 火种 / 火焰种 / 火球 / 火焰球 / 业火球 / 业火种（canFire）→ 连锁引爆
    ///   · 火船 → 同上，但只在水面生效（被拖上岸不烧）
    ///   · 落石 → 沿"攻击者 → 陷阱"方向滚出线状伤害
    ///   · 堤防 / 浅滩 → 纯地形效果，不爆发
    ///
    /// 【归属】陷阱建成后是中立的（见 Building.OnCompleted），爆发同样不分敌我。
    /// </summary>
    public static class BuildingTrapEffect
    {
        // ==================== 特技 / 科技 Id ====================
        // 对齐 Open311 s11_enum_preset.h 的 SkillId / TechId
        /// <summary>「火神」（Features.json Id 63，原版 SkillId_Kashin）</summary>
        const int FeatureId_Kashin = 63;
        /// <summary>「藤甲」（Features.json Id 32，原版 SkillId_Toukou）</summary>
        const int FeatureId_Toukou = 32;
        /// <summary>「踏破」（Features.json Id 7，原版 SkillId_Touha，陷阱伤害 ×0.1）</summary>
        const int FeatureId_Touha = 7;
        /// <summary>「炼成炸药」（Techniques.json Id 32，原版 TechId_Bakuyakurensei 爆药炼成，火系 +300/+100）</summary>
        const int TechId_Bakuyakurensei = 32;

        /// <summary>
        /// BFS 连锁引爆的节点：记录「当前格」「来源格」「引爆它的陷阱类型」。
        /// </summary>
        class TrapNode
        {
            public Cell cell;
            public Cell srcCell;
            public BuildingKindType kind;
        }

        /// <summary>
        /// 该设施是否为可爆发的陷阱。供 <see cref="Building.OnHit"/> 预判，
        /// 避免每次挨打都进一遍完整流程。
        /// </summary>
        public static bool CanTrigger(Building building)
        {
            if (building == null || !building.isComplate)
                return false;

            BuildingType type = building.BuildingType;
            if (type == null || !type.IsExplosive)
                return false;

            // 堤防是陷阱类型里的例外（原版 is_trap_type 显式排除）：只挡水，不爆炸
            if (type.kind == (int)BuildingKindType.Levee)
                return false;

            // 浅滩是纯地形障碍，没有爆发效果
            if (type.kind == (int)BuildingKindType.Sandbar)
                return false;

            return true;
        }

        /// <summary>
        /// 结算陷阱爆发。
        /// </summary>
        /// <param name="building">陷阱（调用时其 CenterCell / BuildingType 必须仍有效）</param>
        /// <param name="atk">引爆者（攻击部队，或引燃它的火焰），用于确定落石 / 火球滚动方向</param>
        public static void Trigger(Building building, SangoObject atk)
        {
            if (!CanTrigger(building))
                return;

            BuildingType type = building.BuildingType;
            Cell center = building.CenterCell;
            if (center == null)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            BuildingKindType kind = (BuildingKindType)type.kind;

            // 【链路日志 ④】陷阱引爆 —— 记录"谁点的火、炸的是哪座陷阱"，
            // 这是「出征带金 → 前线建陷阱 → 高智部队火计点火」链路的终点。
            if (kind != BuildingKindType.Rockfall)
            {
                // 【统一解析】引爆者可能是 Troop，也可能是 SkillInstance（战法点爆陷阱）
                Troop fireTroop = atk?.AsTroop();
                string who = fireTroop != null
                    ? $"{fireTroop.BelongForce?.Name}的[{fireTroop.Name}]"
                    : (atk != null ? atk.Name : "火焰");
                Sango.Log.Info($"*陷阱[{building.Name}] 被{who}引爆");
            }

            // ---- 落石：方向性滚动伤害（对齐原版 Rockfall 方向性）----
            if (kind == BuildingKindType.Rockfall)
            {
                TriggerRockfall(building, atk, center, type);
                return;
            }

            // ---- 火船：只在水面生效（对齐 BuildingType.CanBuildToHere 的水面约束）----
            if (kind == BuildingKindType.FireShip && !center.IsWater)
            {
                Sango.Log.Info($"*陷阱[{building.Name}] 不在水面，未点燃");
                return;
            }

            // ---- 其它火系陷阱：连锁引爆 ----
            TrapChain(building, atk, center, kind);
        }

        #region 连锁引爆（trap_anim / trap_next / fireball_trap_anim）

        /// <summary>
        /// 连锁引爆主流程，对齐 Open311：
        ///   trap_anim（s11_sys_anim_building.cpp:257-352）用 list 做 BFS，
        ///   trap_next（:354-467）对每个节点「点燃 + 即时伤害 + 陷阱连锁」，
        ///   fireball_trap_anim（:469-511）火球沿直线滚。
        ///
        /// 本方法用「队列 + visited」等价复现 list 的 BFS。
        /// 注意：第一个陷阱（被直接命中的那个）由 <see cref="Building.OnHit"/> 返回 true、
        /// ChangeDurability 置耐久为 0 销毁，因此这里只对它做「点燃 + 扩散」，不再二次销毁。
        /// </summary>
        static void TrapChain(Building firstTrap, SangoObject atk, Cell firstCell, BuildingKindType firstKind)
        {
            // 【统一解析】战法点爆时 atk 是 SkillInstance，必须经 master 拿到施法部队，
            // 否则 srcCell 为 null，火球不滚、落石退化成圆形范围。
            Troop attacker = atk?.AsTroop();
            Scenario scenario = Scenario.Cur;

            Queue<TrapNode> queue = new Queue<TrapNode>();
            HashSet<Cell> visited = new HashSet<Cell>();

            // 命中目标数：原版 trap_anim 用 len(damage_list) 累计，既用于放大技巧点，也用于功绩日志。
            int hitCount = 0;

            // 火球 / 落石需要"来源方向"：攻击者所在格 → 陷阱格
            Cell srcCell = (attacker != null && attacker.cell != null) ? attacker.cell : null;

            visited.Add(firstCell);

            // 第一个陷阱格点燃（对齐 trap_next:358-359：水面且无部队不点）
            if (!firstCell.IsWater || firstCell.troop != null)
                IgniteCell(firstCell, scenario);

            // 从第一个陷阱向外扩散
            SpreadChain(queue, firstCell, srcCell, firstKind);

            while (queue.Count > 0)
            {
                TrapNode node = queue.Dequeue();
                Cell cell = node.cell;
                if (cell == null || !visited.Add(cell))
                    continue;

                // 点燃（水面且无部队不点）
                if (!cell.IsWater || cell.troop != null)
                    IgniteCell(cell, scenario);

                Troop troop = cell.troop;
                Building building = cell.building as Building;

                if (troop != null && troop.IsAlive)
                {
                    // 扩散格上的部队：陷阱爆炸 + 火焰 即时伤害
                    if (ApplyTroopDamage(node.kind, attacker, troop, atk))
                        hitCount++;
                }
                else if (building != null && CanTrigger(building))
                {
                    // 扩散格上是另一个已完成陷阱 → 连锁：销毁它并继续扩散。
                    // 直接 OnFall 而非走 ChangeDurability，避免再次触发 OnHit 造成无限递归；
                    // 手动置 0 对齐 ChangeDurability 的销毁前置（耐久归零 → OnFall）。
                    BuildingKindType kind = (BuildingKindType)building.BuildingType.kind;
                    building.durability = 0;
                    building.OnFall(atk);
                    SpreadChain(queue, cell, node.srcCell, kind);
                }
                else if (building != null)
                {
                    // 扩散格上是非陷阱建筑 → 陷阱爆炸 + 火焰 即时扣耐久
                    if (ApplyBuildingDamage(node.kind, attacker, building, atk))
                        hitCount++;
                }
            }

            // 引爆者的战果结算（原版 trap_anim:325-350 的 unit_add_exp_with_kouseki + force_add_tech_point）
            GainTrapperExp(attacker, CombatExpEvent.TrapAttack, hitCount);
        }

        /// <summary>
        /// 按陷阱类型向外扩散，对齐 trap_next 的连锁分支：
        ///   火种 / 火船       → 半径 1（MapRangeIterator 1,1）
        ///   火焰种 / 业火种   → 半径 2（MapRangeIterator 1,2）
        ///   火球             → 直线 3
        ///   火焰球           → 直线 5
        ///   业火球           → 直线 4（可穿内政 / 军事设施）
        /// </summary>
        static void SpreadChain(Queue<TrapNode> queue, Cell cell, Cell srcCell, BuildingKindType kind)
        {
            switch (kind)
            {
                case BuildingKindType.Fire:
                case BuildingKindType.FireShip:
                    cell.Spiral(1, n =>
                    {
                        if (CanSpreadTo(n))
                            queue.Enqueue(new TrapNode { cell = n, srcCell = cell, kind = kind });
                    });
                    break;

                case BuildingKindType.HellFire:
                case BuildingKindType.Inferno:
                    cell.Spiral(2, n =>
                    {
                        if (CanSpreadTo(n))
                            queue.Enqueue(new TrapNode { cell = n, srcCell = cell, kind = kind });
                    });
                    break;

                case BuildingKindType.FireBall:
                    FireballChain(queue, cell, srcCell, 3, BuildingKindType.FireBall);
                    break;

                case BuildingKindType.HellFireBall:
                    FireballChain(queue, cell, srcCell, 5, BuildingKindType.HellFireBall);
                    break;

                case BuildingKindType.InfernoBall:
                    FireballChain(queue, cell, srcCell, 4, BuildingKindType.InfernoBall);
                    break;
            }
        }

        /// <summary>
        /// 火球沿直线滚，对齐 fireball_trap_anim（s11_sys_anim_building.cpp:469-511）。
        /// 方向 = 来源格 → 当前陷阱格（calc_dir(src_pos, pos)），火球朝该方向继续滚 distance 格。
        /// 遇不可通行地形停；遇非陷阱建筑停（据点在业火球里也停；业火球不因内政/军事设施停）。
        /// </summary>
        static void FireballChain(Queue<TrapNode> queue, Cell cell, Cell srcCell, int distance, BuildingKindType kind)
        {
            int dir = srcCell != null ? srcCell.DirectionTo(cell) : -1;
            if (dir < 0)
                return;

            Cell pos = cell;
            for (int i = 0; i < distance; i++)
            {
                Cell next = pos.GetNeighbor(dir);
                if (next == null)
                    break;

                TerrainType terrain = next.TerrainType;
                if (terrain == null || !terrain.moveable)
                    break;                                  // 不可通行地形停（对齐 enabled_terrains_ 检查）

                queue.Enqueue(new TrapNode { cell = next, srcCell = pos, kind = kind });

                if (next.building != null)
                {
                    BuildingBase b = next.building;
                    if (!b.BuildingType.IsExplosive)
                    {
                        if (b.IsCityBase())
                            break;                          // 据点中断（对齐 is_general_type）
                        if (kind != BuildingKindType.InfernoBall)
                            break;                          // 业火球不因内政/军事设施中断
                    }
                }
                pos = next;
            }
        }

        /// <summary>
        /// 是否应向该格扩散，对齐 add_trapped（s11_sys_anim_building.cpp:513-527）：
        /// 「格子非空（有部队 / 建筑）或地形可引燃」。
        /// </summary>
        static bool CanSpreadTo(Cell cell)
        {
            if (cell == null)
                return false;
            if (cell.troop != null || cell.building != null)
                return true;

            TerrainType terrain = cell.TerrainType;
            if (terrain == null || terrain.isWater)
                return false;
            return terrain.fireRate > 0;
        }

        #endregion

        #region 伤害计算（calc_trap_damage / calc_fire_damage）

        /// <summary>
        /// 陷阱爆炸伤害，对齐 calc_trap_damage（s11_sys_damage.cpp:1096-1208）。
        /// </summary>
        /// <param name="kind">陷阱类型</param>
        /// <param name="attacker">引爆部队（可为 null，火焰引燃时无部队）</param>
        /// <param name="targetTroop">目标部队（null 表示目标是建筑）</param>
        static int CalcTrapDamage(BuildingKindType kind, Troop attacker, Troop targetTroop)
        {
            bool isUnit = targetTroop != null;
            int n;

            if (isUnit)
            {
                switch (kind)
                {
                    case BuildingKindType.Fire:
                    case BuildingKindType.FireBall:
                        n = 300 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.HellFire:
                        n = 400 + RandInt(200) + 50;
                        break;
                    case BuildingKindType.HellFireBall:
                    case BuildingKindType.FireShip:
                        n = 400 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.InfernoBall:
                    case BuildingKindType.Inferno:
                        n = 500 + RandInt(200) + 50;
                        break;
                    case BuildingKindType.Rockfall:
                        n = 1500 + RandInt(500) + 50;
                        break;
                    default:
                        n = 50;
                        break;
                }
            }
            else
            {
                switch (kind)
                {
                    case BuildingKindType.Fire:
                        n = 100 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.HellFire:
                    case BuildingKindType.FireBall:
                    case BuildingKindType.FireShip:
                        n = 200 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.HellFireBall:
                    case BuildingKindType.Inferno:
                        n = 300 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.InfernoBall:
                        n = 400 + RandInt(100) + 50;
                        break;
                    case BuildingKindType.Rockfall:
                        n = 800 + RandInt(1000) + 50;
                        break;
                    default:
                        n = 50;
                        break;
                }
            }

            // 落石 / 堤防：目标藤甲减半（对齐 :1165-1172）
            if (kind == BuildingKindType.Rockfall || kind == BuildingKindType.Levee)
            {
                if (targetTroop != null && HasTroopFeature(targetTroop, FeatureId_Toukou))
                    n /= 2;
            }
            // 火系：攻击者爆药炼成 +300/+100、火神 ×2；目标藤甲 ×2、目标火神免疫（对齐 :1174-1191）
            else
            {
                if (attacker != null && HasForceTechnique(attacker, TechId_Bakuyakurensei))
                    n += isUnit ? 300 : 100;

                if (attacker != null && HasTroopFeature(attacker, FeatureId_Kashin))
                    n *= 2;

                if (targetTroop != null)
                {
                    if (HasTroopFeature(targetTroop, FeatureId_Toukou))
                        n *= 2;
                    else if (IsFireImmune(targetTroop))
                        n = 0;
                }
            }

            // 踏破（SkillId_Touha）n *= 0.1（对齐 :1193-1197）
            if (targetTroop != null && HasTroopFeature(targetTroop, FeatureId_Touha))
                n = (int)(n * 0.1);

            return n;
        }

        /// <summary>
        /// 火焰伤害，对齐 calc_fire_damage（s11_sys_damage.cpp:991-1043）。
        /// </summary>
        static int CalcFireDamage(Troop attacker, Troop targetTroop)
        {
            int atk;
            int dmg;

            if (targetTroop != null)
            {
                atk = 300;
                dmg = atk + RandInt(200);
            }
            else
            {
                atk = 100;
                dmg = atk + RandInt(50);
            }

            // 爆药炼成（TechId_Bakuyakurensei）追加 atk（对齐 :1010-1017）
            if (attacker != null && HasForceTechnique(attacker, TechId_Bakuyakurensei))
                dmg += atk;

            if (attacker != null && HasTroopFeature(attacker, FeatureId_Kashin))
                dmg *= 2;

            if (targetTroop != null)
            {
                if (HasTroopFeature(targetTroop, FeatureId_Toukou))
                    dmg *= 2;
                else if (IsFireImmune(targetTroop))
                    dmg = 0;
            }

            return dmg;
        }

        /// <summary>
        /// 对扩散格上的部队结算「陷阱爆炸 + 火焰」即时伤害，对齐 trapped_damage_anim
        /// （s11_sys_anim_building.cpp:529-584）的 unit_add_troops 部分。
        /// </summary>
        /// <returns>是否真的结算了伤害（原版 trap_anim 用它累计 hit_count）</returns>
        static bool ApplyTroopDamage(BuildingKindType kind, Troop attacker, Troop target, SangoObject atk)
        {
            if (target == null || !target.IsAlive)
                return false;

            int trapDmg = CalcTrapDamage(kind, attacker, target);
            int fireDmg = CalcFireDamage(attacker, target);
            int total = trapDmg + fireDmg;
            if (total <= 0)
            {
                // 伤害为 0（火神免疫火焰 / 守城方设施减伤到 0）也要打一行日志，
                // 否则现场只看到"陷阱炸了但没人掉血"，无法判断是免疫还是配置为 0。
                if (IsFireImmune(target))
                    Sango.Log.Info($"*陷阱引爆命中 {target.Name}，但该部队免疫火焰伤害（伤害 0）");
                return false;
            }

            target.ChangeTroops(-total, atk, 0);
            Sango.Log.Info($"*陷阱引爆命中 {target.Name}，伤害 {total}（陷阱 {trapDmg} + 火焰 {fireDmg}）");

            // 原版 trapped_damage_anim 的混乱（:566-576）：业火种 50% 混乱 1~4 回合、火船 25% 混乱 1~3 回合
            switch (kind)
            {
                case BuildingKindType.Inferno:
                    if (GameRandom.Chance(50))
                        target.AddBuff(1, GameRandom.Range(1, 5), attacker);
                    break;
                case BuildingKindType.FireShip:
                    if (GameRandom.Chance(25))
                        target.AddBuff(1, GameRandom.Range(1, 4), attacker);
                    break;
            }
            return true;
        }

        /// <summary>
        /// 对扩散格上的非陷阱建筑结算「陷阱爆炸 + 火焰」即时耐久伤害，对齐 trap_next 里
        /// 的 building_add_hp（s11_sys_anim_building.cpp:439）。
        /// </summary>
        /// <returns>是否真的结算了耐久伤害</returns>
        static bool ApplyBuildingDamage(BuildingKindType kind, Troop attacker, BuildingBase building, SangoObject atk)
        {
            if (building == null)
                return false;

            int trapDmg = CalcTrapDamage(kind, attacker, null);
            int fireDmg = CalcFireDamage(attacker, null);
            int total = trapDmg + fireDmg;
            if (total <= 0)
                return false;

            building.ChangeDurability(-total, atk);
            return true;
        }

        #endregion

        #region 落石（Rockfall）

        /// <summary>
        /// 落石：先打中心格，再沿"攻击者 → 陷阱"的方向滚出线状伤害。
        /// 没有攻击者方向信息时（例如被火焰引爆）退回圆形范围伤害。
        /// 伤害值对齐 calc_trap_damage 的 Rockfall 分档。
        /// </summary>
        static void TriggerRockfall(Building building, SangoObject atk, Cell center, BuildingType type)
        {
            // 【统一解析】同上：战法引爆时靠 AsTroop 拿施法部队，才能算出滚动方向
            Troop attacker = atk?.AsTroop();
            int hitCount = 0;
            int dir = -1;
            if (attacker != null && attacker.cell != null && attacker.cell != center)
                dir = attacker.cell.DirectionTo(center);

            if (dir < 0)
            {
                // 没有方向信息：退回圆形范围（含中心格，Spiral 本身不含中心）
                if (ApplyRockfallDamage(center, attacker, atk)) hitCount++;
                center.Spiral(1, cell =>
                {
                    if (ApplyRockfallDamage(cell, attacker, atk)) hitCount++;
                });
                GainTrapperExp(attacker, CombatExpEvent.TrapAttack, hitCount);
                return;
            }

            // 起点：陷阱所在格
            if (ApplyRockfallDamage(center, attacker, atk)) hitCount++;

            // 沿途滚动：长度取 attackMaxRange，未配置时滚 2 格
            int length = type.attackMaxRange > 0 ? type.attackMaxRange : 2;
            center.DirectionLine(dir, length, cell =>
            {
                if (ApplyRockfallDamage(cell, attacker, atk)) hitCount++;
            });

            GainTrapperExp(attacker, CombatExpEvent.TrapAttack, hitCount);
        }

        /// <returns>是否真的造成了伤害</returns>
        static bool ApplyRockfallDamage(Cell cell, Troop attacker, SangoObject atk)
        {
            if (cell == null)
                return false;

            Troop troop = cell.troop;
            if (troop != null && troop.IsAlive)
            {
                int dmg = CalcTrapDamage(BuildingKindType.Rockfall, attacker, troop);
                troop.ChangeTroops(-dmg, atk, 0);
                Sango.Log.Info($"*落石命中 {troop.Name}，伤害 {dmg}");
                return dmg > 0;
            }
            else if (cell.building != null)
            {
                int dmg = CalcTrapDamage(BuildingKindType.Rockfall, attacker, null);
                cell.building.ChangeDurability(-dmg, atk);
                return dmg > 0;
            }
            return false;
        }

        /// <summary>
        /// 陷阱引爆者的战果结算（功绩 / 能力经验 / 适性经验 / 势力技巧点）。
        ///
        /// 【原版对齐】s11_sys_anim_building.cpp:325-350 <c>trap_anim</c>：
        /// <code>
        /// exp_t exp = Exp_TrapAttack;                       // 按 reason 切换 TrapStrategy / TrapCollision
        /// int tech_point = unit_add_exp_with_kouseki(by, get_exp(exp), by-&gt;get_equipment_id(), hit_count, off_screen);
        /// if (hit_count &gt; 0)
        /// {
        ///     tech_point /= hit_count;
        ///     hit_count = std::min(hit_count, 20);
        ///     tech_point *= (int)(std::sqrt(hit_count) * hit_count);
        /// }
        /// force_add_tech_point(force, by-&gt;get_pos(), tech_point, off_screen);
        /// </code>
        /// 拆开看：功绩 / 属性经验 / 适性经验按行原值给**一次**（不受 hit_count 影响），
        /// 技巧点则是 <c>base * sqrt(min(hit,20)) * min(hit,20)</c>。
        /// 项目 <see cref="Troop.GainEP"/> 的 techniqueMultiplier 只放大技巧点，正好对应。
        /// </summary>
        /// <param name="attacker">引爆者（火焰引爆时为 null，原版 <c>is_alive(by)</c> 同样跳过）</param>
        /// <param name="expEvent">TrapAttack / TrapStrategy / TrapCollision</param>
        /// <param name="hitCount">本次引爆实际受伤的目标数</param>
        static void GainTrapperExp(Troop attacker, CombatExpEvent expEvent, int hitCount)
        {
            if (attacker == null || !attacker.IsAlive || hitCount <= 0)
                return;

            int clamped = System.Math.Min(hitCount, 20);
            int multiplier = (int)(System.Math.Sqrt(clamped) * clamped);
            if (multiplier < 1) multiplier = 1;

            attacker.GainEP(expEvent, false, multiplier);
            Sango.Log.Info($"*[战果]陷阱引爆者 {attacker.Name} 命中 {hitCount} 个目标，技巧点倍数 x{multiplier}");
        }

        #endregion

        #region 工具

        /// <summary>
        /// 目标部队是否免疫火焰伤害。
        ///
        /// 【为什么不能只看特技 Id】「火神」只是免疫来源之一：项目里真正的开关是
        /// <see cref="Troop.ignoreFire"/>（PathFlag.IgnoreFire），由 <c>TroopIgnoreFire</c>
        /// 这类 Action 在 <c>OnTroopCalculateAttribute</c> 里写入。以后任何新特技 / 科技 /
        /// buff 只要置这一位就该免疫，不必再回到本文件加特技 Id。
        ///
        /// 【为什么还兜底判特技】属性重算有节拍，刚出场的部队在首次重算之前
        /// <c>ignoreFire</c> 仍是 false；此时按特技 Id 兜底可避免「火神部队被第一发陷阱打伤」。
        /// </summary>
        static bool IsFireImmune(Troop troop)
        {
            if (troop == null)
                return false;
            return troop.ignoreFire || HasTroopFeature(troop, FeatureId_Kashin);
        }

        /// <summary>队内任一武将是否具备指定特技（等价 Open311 的 Unit::has_skill）。</summary>
        static bool HasTroopFeature(Troop troop, int featureId)
        {
            if (troop == null)
                return false;

            Person leader = troop.Leader;
            if (leader != null && leader.HasFeatrue(featureId))
                return true;
            Person member1 = troop.Member1;
            if (member1 != null && member1.HasFeatrue(featureId))
                return true;
            Person member2 = troop.Member2;
            if (member2 != null && member2.HasFeatrue(featureId))
                return true;
            return false;
        }

        /// <summary>势力是否已掌握指定技巧（等价 Open311 的 Force::has_tech）。</summary>
        static bool HasForceTechnique(Troop troop, int techniqueId)
        {
            Force force = troop != null ? troop.BelongForce : null;
            if (force == null || force.Techniques == null)
                return false;
            return force.Techniques.Contains(techniqueId);
        }

        /// <summary>在指定格子点燃火焰（等价 Open311 的 set_fire_timer）。</summary>
        static void IgniteCell(Cell cell, Scenario scenario)
        {
            if (cell == null || cell.fire != null || scenario == null)
                return;

            // 陷阱不是武将放的火：没有智力加成；火焰基础伤害对齐 calc_fire_damage 的 300
            Fire fire = new Fire()
            {
                damage = 300,
                intelligence = 0,
                cell = cell,
                generation = 0,
                counter = 3,
            };
            scenario.Add(fire);
            fire.Init(scenario);
        }

        /// <summary>rand_int(n)：[0, n] 均匀随机整数（对齐 Open311 的 rand_int）。</summary>
        static int RandInt(int n)
        {
            return GameRandom.Range(0, n + 1);
        }

        #endregion
    }
}
