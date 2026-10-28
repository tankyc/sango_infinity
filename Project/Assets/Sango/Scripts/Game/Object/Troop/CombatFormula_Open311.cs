namespace Sango.Core
{
    /*
     * Open311 版战斗公式实现
     * ────────────────────────────────────────────────────────────────
     * 本文件是"搬家"结果：三个类里的算法与搬家前 Troop.CalculateSkillDamage* 完全一致，
     * 只是把输入改成从 Context 取，便于通过配置切换/回退。
     *
     * 对齐出处：
     *   · 部队 → 部队：s11_sys_damage.cpp:278-635（部队分支）、1404-1440（公式本体）
     *   · 部队 → 守军：s11_sys_damage.cpp:438-467
     *   · 部队 → 耐久：s11_sys_damage.cpp:542-628、1531-1553
     */

    /// <summary>
    /// 部队打部队的兵力伤害（原版 calc_attack_damage → calc_troops_damage）。
    ///
    /// 对齐要点：
    ///  1. 主体公式在 <see cref="CombatFormulaMath.CalcTroopsDamageBody"/>，逐行照搬；
    ///  2. 兵种相克在本层乘，且**计略不乘**——原版相克只在普通攻击/战法的 switch 内生效
    ///     （s11_sys_damage.cpp:372-436），计略伤害不走这段；
    ///  3. 科技/难度系数沿用本项目的 DamageTroopExtraFactor 与难度变量。
    /// </summary>
    public class Open311TroopDamageMethod : TroopDamageMethod
    {
        public override int Calculate(in TroopDamageContext ctx)
        {
            Troop attacker = ctx.Attacker;
            Troop target = ctx.Target;
            SkillInstance skill = ctx.Skill;

            // 原版 troops_atk：战法取战法自身的兵力攻击，普攻近战固定 10（s11_sys_damage.cpp:294-306）。
            // 本项目把该值存在 SkillInstance.atk 上，普通攻击技能配置为 10。
            int troopsAtk = skill != null ? skill.atk : 10;

            double damage = CombatFormulaMath.CalcTroopsDamageBody(
                ctx.Vars, attacker.Attack, target.Defence, attacker.troops, target.troops, troopsAtk);

            // 兵种相克：原版 switch 位于普通攻击/战法路径内，计略伤害不参与相克。
            if (skill == null || !skill.IsStrategy())
                damage *= Troop.CalculateRestrainBoost(attacker, target);

            // 额外增益 (科技系数等)
            damage *= System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor));

            // 士兵伤害微调（100 = 原版；纯平衡旋钮，见 TroopType.roleDmg）
            int roleDmg = attacker.TroopType != null ? attacker.TroopType.roleDmg : 100;
            if (roleDmg <= 0) roleDmg = 100;
            damage *= roleDmg / 100.0;

            // 敌方阵地压制（阵 85 / 砦 75 / 城塞 65，见 BuildingType.enemyDamageFactor）
            int campFactor = CalcEnemyCampFactor(ctx.Target);
            if (campFactor < 100)
                damage = damage * campFactor / 100.0;

            // 难度系数,仅对玩家生效
            damage *= ctx.DifficultyFactor;

            return (int)damage;
        }

        /// <summary>
        /// 防守方是否处于**敌方**阵/砦/城塞的压制范围内。
        ///
        /// 【原版出处】s11_sys_damage.cpp:665-671 的 debuffer 判定 + :792-798 的系数应用：
        /// 遍历目标周围，找到第一个敌对势力的阵/砦/城塞，按设施系数缩放本次兵力伤害。
        ///
        /// 范围与系数都由数据决定（<see cref="BuildingType.debuffRange"/> /
        /// <see cref="BuildingType.enemyDamageFactor"/>），代码不认设施 Id。
        /// 多个设施同时覆盖时取**最强者**（系数最小）。
        /// </summary>
        static int CalcEnemyCampFactor(Troop target)
        {
            if (target == null || target.cell == null) return 100;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return 100;

            int factor = 100;

            // 只扫目标周围 4 格（原版设施削弱范围都是 1~2，留余量），
            // 避免在 AI 评分热路径（每次预演伤害都会调用）上遍历全部建筑。
            scenario.Map.SpiralAction(target.cell, 4, cell =>
            {
                if (cell == null || cell.building == null) return;

                BuildingBase building = cell.building;
                BuildingType type = building.BuildingType;
                if (type == null) return;
                if (type.debuffRange <= 0 || type.enemyDamageFactor >= factor) return;

                int distance = scenario.Map.Distance(target.cell, building.CenterCell);
                if (distance <= 0 || distance > type.debuffRange) return;

                // 必须是防守方的敌对势力（原版 find_camp_pred）
                if (!target.IsEnemy(building)) return;

                factor = type.enemyDamageFactor;
            });

            return factor;
        }
    }

    /// <summary>
    /// 部队打据点（城 / 关 / 港）的守军兵力伤害（原版 s11_sys_damage.cpp:438-467）。
    ///
    ///     井阑型  → calc_troops_damage(完整兵力) × buildingTroopDamageFactor%
    ///     其他    → calc_troops_damage(兵力 × 75%) × 城0.55 / 关0.45 / 港0.60
    ///
    /// 【关键】"兵力 × 75%"是用打折后的兵力**重算整条公式**（公式非线性），不是把结果乘 0.75；
    /// 井阑型则用完整兵力算、再乘配置倍率，且**不再乘**城/关/港倍率。
    ///
    /// 【配置驱动】代码里不认任何兵种 Id：哪些兵种走"井阑型"由 TroopTypes.json 的
    /// `siegeRole`（见 SiegeRoleType.Artillery）指定，倍率由 `buildingTroopDamageFactor` 给。
    /// </summary>
    public class Open311GarrisonDamageMethod : GarrisonDamageMethod
    {
        public override int Calculate(in GarrisonDamageContext ctx)
        {
            Troop attacker = ctx.Attacker;
            City target = ctx.Target;

            // 原版 get_ability（s11_sys_damage.cpp:1387）在算建筑防御时**无条件**把 troops_atk
            // 重置为 10——不论普攻还是战法，攻城时战法的"兵力攻击"加成都不生效，
            // 公式首项恒为 sqrt(10 * 64)。
            const int BuildingTroopsAtk = 10;

            TroopType attackTroopType = attacker.TroopType;
            int siegeRole = attackTroopType != null ? attackTroopType.siegeRole : 0;

            // 原版 get_ability（s11_sys_damage.cpp:1384-1402）的 command：
            //   有太守 → min(守军兵力, 太守带兵上限)
            //   无太守 → min(守军兵力, 7000)，且非零时下限 500
            int targetCommand = CalcGarrisonCommand(target);

            double damage;
            if (siegeRole == (int)SiegeRoleType.Artillery)
            {
                // 井阑/投石型：吃完整兵力，倍率由兵种配置给，且不再乘城/关/港倍率
                // （原版 442-451 命中 case 后不会落入 452-465 的 default 分支）
                int factor = attackTroopType.buildingTroopDamageFactor;
                if (factor <= 0) factor = 100;
                damage = CombatFormulaMath.CalcTroopsDamageBody(
                             ctx.Vars, attacker.Attack, target.GetDefence(), attacker.troops, targetCommand, BuildingTroopsAtk)
                         * (factor / 100.0);
            }
            else
            {
                // 其他兵装：兵力打 75% 后重算整条公式，再乘据点类型倍率
                int troops = System.Math.Max(attacker.troops * 75 / 100, 1);
                damage = CombatFormulaMath.CalcTroopsDamageBody(
                    ctx.Vars, attacker.Attack, target.GetDefence(), troops, targetCommand, BuildingTroopsAtk);

                if (target.IsCity()) damage *= 0.55;
                else if (target.IsGate()) damage *= 0.45;
                else if (target.IsPort()) damage *= 0.60;
            }

            // 额外增益 (科技系数等)
            damage *= System.Math.Max(0, (1 + attacker.DamageTroopExtraFactor));

            // 士兵伤害微调（100 = 原版；纯平衡旋钮，见 TroopType.roleDmg）
            int roleDmg = attackTroopType != null ? attackTroopType.roleDmg : 100;
            if (roleDmg <= 0) roleDmg = 100;
            damage *= roleDmg / 100.0;

            // 难度系数,仅对玩家生效
            damage *= ctx.DifficultyFactor;

            return (int)damage;
        }

        /// <summary>
        /// 据点的"指挥兵力" —— 原版 get_ability 的 command（s11_sys_damage.cpp:1384-1402）。
        ///
        ///   有太守：min(守军兵力, 太守带兵上限)
        ///   无太守：min(守军兵力, 7000)，且结果非零时下限 500
        /// </summary>
        static int CalcGarrisonCommand(City target)
        {
            if (target == null) return 0;

            Person governor = target.Leader;
            if (governor != null && governor.IsAlive)
                return System.Math.Min(target.troops, governor.TroopsLimit);

            int command = System.Math.Min(target.troops, 7000);
            if (command != 0)
                command = System.Math.Max(command, 500);
            return command;
        }
    }

    /// <summary>
    /// 部队打建筑的耐久伤害（原版 calc_attack_damage 耐久分支 542-628 与 calc_hp_damage 1531-1553）。
    ///
    ///     hp_atk = 战法 ? 战法的 hp_attack : (近战 ? 15 : 5)
    ///     普通兵装：sqrt(atk²/15) × sqrt(兵力) × (hp_atk×4+100) × (buffed+10) × 0.0001
    ///     冲车型  ：a × min(sqrt(兵力), 40) × c × d × 0.0001 + 兵力/25 + sqrt(兵力)
    ///
    /// 【建筑倍率】原版用一张按设施分类的倍率表（567-605），本项目沿用 BuildingType.damageBounds
    /// —— 其中城 0.7 / 关 0.6 / 港 0.8 与原版取值一致。该倍率**只对非冲车型生效**
    /// （原版 833-834 的 if 保护）；哪些兵种是"冲车型"由 TroopTypes.json 的 `siegeRole` 配置。
    ///
    /// 【已对齐】原版耐久公式里**没有**任何兵种系数，兵装差异只通过 Attack 体现
    /// （Attack = 武力 × 兵装atk × 适性 / 100），所以这里不再乘 TroopType.durabilityDmg。
    /// </summary>
    public class Open311DurabilityDamageMethod : DurabilityDamageMethod
    {
        public override int Calculate(in DurabilityDamageContext ctx)
        {
            Troop attacker = ctx.Attacker;
            BuildingBase target = ctx.Target;
            SkillInstance skill = ctx.Skill;
            TroopType attackTroopType = attacker.TroopType;

            // 原版 hp_atk：战法取战法自身攻城值，普攻近战 15 / 远程 5（s11_sys_damage.cpp:548-551）
            int hpAtk;
            if (skill != null && !skill.IsNormal())
                hpAtk = skill.atkDurability;
            else
                hpAtk = (skill != null && skill.IsRange()) ? 5 : 15;

            int troops = System.Math.Max(attacker.troops, 1);
            // 原版 buffed 恒 false → (0 + 10)
            float a = (float)System.Math.Sqrt((float)attacker.Attack * attacker.Attack / 15f);
            float b = (float)System.Math.Sqrt(troops);
            float c = hpAtk * 4 + 100f;
            const float d = 10f;

            int siegeRole = attackTroopType != null ? attackTroopType.siegeRole : 0;

            double damage;
            if (siegeRole == (int)SiegeRoleType.Ram)
            {
                // 冲车型（原版 Shousha / Mokujuu）：calc_shousha_hp_damage，
                // 比通用式多 +兵力/25 + sqrt(兵力)，且 sqrt 处封顶 40（s11_sys_damage.cpp:1543-1553）
                damage = a * System.Math.Min(b, 40f) * c * d * 0.0001f + troops / 25f + b;
            }
            else
            {
                damage = a * b * c * d * 0.0001f;
            }

            // 建筑被伤害倍率：原版这张表只对"非冲车/木兽"生效
            // （s11_sys_damage.cpp:833-834 的 if 保护），冲车型跳过整张表。
            if (siegeRole != (int)SiegeRoleType.Ram)
                damage *= target.BuildingType != null ? target.BuildingType.damageBounds : 1f;

            // 额外增益 (科技系数等)
            damage *= System.Math.Max(0, (1 + attacker.DamageBuildingExtraFactor));

            // 耐久伤害微调（100 = 原版；见 TroopType.durabilityDmg）。
            // 原版公式没有兵种系数，兵装差异只通过 Attack 体现，所以 100 才是对齐值；
            // 这里保留乘数只是给平衡调整留旋钮，数据默认已统一为 100。
            int durabilityDmg = attackTroopType != null ? attackTroopType.durabilityDmg : 100;
            if (durabilityDmg <= 0) durabilityDmg = 100;
            damage *= durabilityDmg / 100.0;

            // 难度系数,仅对玩家生效
            damage *= ctx.DifficultyFactor;

            return (int)damage;
        }
    }
}
