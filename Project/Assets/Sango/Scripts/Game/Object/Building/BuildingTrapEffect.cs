using System;

namespace Sango.Core
{
    /// <summary>
    /// 陷阱爆发效果（对应 Open311 的 FacilityType_Trap 设施）。
    ///
    /// 【触发时机】命中即爆：由 <see cref="Building.OnHit"/> 在耐久扣减前调用，
    /// 与 Open311「火球 / 落石被攻击命中时立即结算并自毁」一致。
    /// 非攻击性销毁（势力灭亡清场、中立化、未完工拆除）走 OnFall，不引爆。
    ///
    /// 【覆盖的设施】
    ///   · 火种 / 火焰种 / 火球 / 火焰球 / 业火球 / 业火种（canFire）
    ///       → 自身及相邻格点燃火焰，后续逐回合伤害由既有火焰系统结算
    ///   · 火船
    ///       → 同上，但**只在水面**生效（被拖上岸不烧）
    ///   · 落石
    ///       → 沿"攻击者 → 陷阱"方向滚出线状伤害（原版就是方向性滚动，不是圆形）
    ///   · 堤防 / 浅滩
    ///       → 纯地形效果，不爆发
    ///
    /// 【归属】陷阱建成后是中立的（见 Building.OnCompleted），爆发同样不分敌我，
    /// 与三国志11「自己人踩到也会被烧」的口径一致。
    /// </summary>
    public static class BuildingTrapEffect
    {
        /// <summary>
        /// 该设施是否为可爆发的陷阱。供 <see cref="Building.OnHit"/> 预判，
        /// 避免每次挨打都进一遍完整流程。
        /// </summary>
        /// <param name="building">建筑</param>
        /// <returns>是否可爆发</returns>
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
        /// <param name="atk">引爆者，用于确定落石滚动方向</param>
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

            // 范围：数据表配了 atkRange 就用它，否则按"自身 + 相邻一圈"
            int range = type.atkRange > 0 ? type.atkRange : 1;
            int damage = type.atk > 0 ? type.atk : DefaultDamage(scenario);

            // ---- 落石：方向性滚动伤害 ----
            if (type.kind == (int)BuildingKindType.Rockfall)
            {
                TriggerRockfall(building, atk, center, type, range, damage);
                return;
            }

            // ---- 火船：只在水面生效 ----
            if (type.kind == (int)BuildingKindType.FireShip)
            {
                if (!center.IsWater)
                {
                    Sango.Log.Info($"*陷阱[{building.Name}] 不在水面，未点燃");
                    return;
                }
                IgniteArea(building, center, range, damage, scenario, true);
                return;
            }

            // ---- 其它火系陷阱 ----
            if (type.canFire)
                IgniteArea(building, center, range, damage, scenario, false);
        }

        /// <summary>
        /// 落石：先打中心格，再沿"攻击者 → 陷阱"的方向滚出线状伤害。
        ///
        /// 原版落石是被击中后朝着攻击来源的反方向滚，碾过沿途部队。
        /// 没有攻击者方向信息时（例如被火焰引爆）退回圆形范围伤害。
        /// </summary>
        /// <param name="building">陷阱</param>
        /// <param name="atk">引爆者</param>
        /// <param name="center">陷阱中心格</param>
        /// <param name="type">建筑类型</param>
        /// <param name="range">半径兜底</param>
        /// <param name="damage">伤害</param>
        private static void TriggerRockfall(Building building, SangoObject atk, Cell center, BuildingType type, int range, int damage)
        {
            int dir = -1;
            if (atk is Troop attacker && attacker.cell != null && attacker.cell != center)
                dir = attacker.cell.DirectionTo(center);

            if (dir < 0)
            {
                // 没有方向信息：退回圆形范围（含中心格，Spiral 本身不含中心）
                DamageCell(center, damage, building);
                center.Spiral(range, cell => DamageCell(cell, damage, building));
                return;
            }

            // 起点：陷阱所在格
            DamageCell(center, damage, building);

            // 沿途滚动：长度取 atkRange，未配置时滚 2 格
            int length = type.atkRange > 0 ? type.atkRange : 2;
            center.DirectionLine(dir, length, cell => DamageCell(cell, damage, building));
        }

        /// <summary>
        /// 点燃范围内火焰。
        /// </summary>
        /// <param name="building">陷阱</param>
        /// <param name="center">中心格</param>
        /// <param name="range">半径</param>
        /// <param name="damage">火焰伤害</param>
        /// <param name="scenario">当前剧本</param>
        /// <param name="waterOnly">是否只点燃水面格（火船）</param>
        private static void IgniteArea(Building building, Cell center, int range, int damage, Scenario scenario, bool waterOnly)
        {
            int life = FireLife(scenario);

            // Spiral 从邻居开始环行，不含中心格，所以陷阱自身那格要先单独处理
            IgniteCell(center, damage, life, scenario, waterOnly);

            center.Spiral(range, cell => IgniteCell(cell, damage, life, scenario, waterOnly));

            Sango.Log.Info($"*陷阱[{building.Name}] 引燃范围 {range} 的火焰，伤害 {damage}");
        }

        /// <summary>
        /// 在单个格子上点燃火焰。
        /// </summary>
        /// <param name="cell">目标格</param>
        /// <param name="damage">伤害</param>
        /// <param name="life">持续回合</param>
        /// <param name="scenario">当前剧本</param>
        /// <param name="waterOnly">是否仅限水面</param>
        private static void IgniteCell(Cell cell, int damage, int life, Scenario scenario, bool waterOnly)
        {
            if (cell == null || cell.fire != null)
                return;
            if (waterOnly && !cell.IsWater)
                return;

            Fire fire = new Fire()
            {
                damage = damage,
                intelligence = 0,      // 陷阱不是武将放的火，没有智力加成
                cell = cell,
                generation = 0,        // 视为第 0 代，蔓延概率满额
                counter = life,
            };
            scenario.Add(fire);
            fire.Init(scenario);
        }

        /// <summary>
        /// 对单格内的部队造成伤害。
        /// </summary>
        /// <param name="cell">目标格</param>
        /// <param name="damage">伤害</param>
        /// <param name="building">伤害来源</param>
        private static void DamageCell(Cell cell, int damage, Building building)
        {
            if (cell == null)
                return;

            Troop troop = cell.troop;
            if (troop == null || !troop.IsAlive)
                return;

            // 第三个参数是"反击伤害"，落石不是部队攻击，填 0
            troop.ChangeTroops(-damage, building, 0);
            Sango.Log.Info($"*落石[{building.Name}] 命中 {troop.Name}，伤害 {damage}");
        }

        /// <summary>
        /// 未配置 atk 时的默认伤害，沿用剧本的火计基础伤害。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>伤害值</returns>
        private static int DefaultDamage(Scenario scenario)
        {
            int value = scenario.Variables != null ? scenario.Variables.baseFireDamage : 0;
            return value > 0 ? value : 500;
        }

        /// <summary>
        /// 火焰持续回合数。优先取剧本变量，没有配置时给 3 回合的保守值。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>持续回合</returns>
        private static int FireLife(Scenario scenario)
        {
            // 目前 Variables 没有"陷阱火焰寿命"这一项，先固定 3 回合；
            // 后续若加入配置（如 baseFireCounter），改这里即可，调用方不需要动。
            return 3;
        }
    }
}
