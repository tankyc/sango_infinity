/*
 * 文件名：PersonInjury.cs
 * 描述：武将伤病（负伤）的恢复规则。每个回合由 Person.OnTurnStart 逐个武将结算一次。
 *
 * ── 三国志11 的既有做法（查到的资料）────────────────────────────
 *   · 伤病分四档：健康 / 轻伤 / 重伤 / 濒危；受伤后各项能力下降，
 *     原版数值为 轻伤 -20%、重伤 -50%、濒危 -70%（多个攻略站口径一致），
 *     本项目已按此曲线折算属性，见 Person.InjuryFactorPercent。
 *   · 恢复途径：① 随时间自动好转，且"伤得越重恢复越久"；
 *               ② 执行"探索"时若华佗恰在本城，触发「巧遇华佗」一次性治愈所有受伤武将。
 *   · 官方从未公布具体恢复回合数，也没有可信的数值表，"夫妻/义兄弟疗伤"一类说法
 *     只出现在内容站且无出处，因此下面这套节奏是本项目自定的（数值都抽成字段，便于调）。
 *   · 三国志11 的一回合是"旬"（10 天）；本项目 Scenario.IncreaseDate 同样是 Info.day += 10，
 *     节奏对得上，所以本规则按**回合**结算（1 回合 = 10 天）。
 *
 * ── 本项目的规则 ────────────────────────────────────────────
 *   1) 每回合按当前等级掷一次概率，中了就**恢复 1 级**：
 *        轻伤 50% / 中伤 25% / 重伤 10%  →  期望 2 / 4 / 10 回合（20 / 40 / 100 天），伤越重拖越久。
 *   2) 回城休养恢复最快：**随军出征（在部队里）或没有据点归属时，恢复概率减半**
 *      （FieldChancePercent = 50）——所以把伤员调回城养伤更划算。
 *   3) 配偶或义兄弟在旁（同一部队 / 相邻部队 / 同在一座城）时，恢复概率 +100%（翻倍，封顶 100%），
 *      对应三国志11 的"夫妻疗伤"梗。
 *   4) 另提供 HealCity / HealAll 给"华佗"一类的剧情事件一次性治愈用（目前还没接探索系统）。
 *
 * 伤病对能力的影响见 Person.ApplyInjuryDecay：五维的 getter 都会按伤病折算，
 * 所以外部读到的 Person.Command / Strength / Intelligence / Politics / Glamour 已经是带伤后的最终值。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 伤病恢复规则（静态配置 + 判定）。数值可直接改字段调平衡，或调 Reset() 还原默认。
    /// </summary>
    public static class PersonInjury
    {
        /// <summary>是否启用伤病恢复</summary>
        public static bool Enabled = true;

        /// <summary>
        /// 各等级每回合的恢复概率（百分点，下标 = 伤病等级；0 = 健康，不参与）。
        /// 默认 轻伤 50% / 中伤 25% / 重伤 10%，伤越重恢复越久。
        /// </summary>
        public static readonly int[] HealChanceByLevel = { 0, 50, 25, 10 };

        /// <summary>
        /// 不在据点内休养时的恢复概率倍率（百分点）。默认 50 = **野外减半**；
        /// 设 0 就变成"必须回城休养才恢复"。
        /// </summary>
        public static int FieldChancePercent = 50;

        /// <summary>配偶 / 义兄弟在旁时的额外恢复概率（百分点，100 表示概率翻倍）</summary>
        public static int CompanionBonus = 100;

        /// <summary>还原为默认配置</summary>
        public static void Reset()
        {
            Enabled = true;
            FieldChancePercent = 50;
            CompanionBonus = 100;
            HealChanceByLevel[0] = 0;
            HealChanceByLevel[1] = 50;
            HealChanceByLevel[2] = 25;
            HealChanceByLevel[3] = 10;
        }

        #region 回合结算

        /// <summary>
        /// 回合结算：掷一次恢复概率，中了就把伤病减轻 1 级（0 = 健康）。
        /// 由 Person.OnTurnStart 调用，每个武将每回合一次。
        /// </summary>
        /// <returns>是否真的好转了一级</returns>
        public static bool TryRecover(Person person)
        {
            if (!Enabled) return false;
            if (person == null || !person.IsAlive) return false;
            if (person.injury <= 0) return false;

            int chance = GetHealChance(person);
            if (chance <= 0) return false;
            // 100% 直接算数，不必劳驾随机数（也方便单独验证等级递减）
            if (chance < 100 && !GameRandom.Chance(chance)) return false;

            person.injury = person.injury - 1;
            if (person.injury < 0) person.injury = 0;
            return true;
        }

        /// <summary>
        /// 当前等级的每回合恢复概率（百分点，已包含"野外减半"与"配偶/义兄弟在旁"的修正）
        /// </summary>
        public static int GetHealChance(Person person)
        {
            if (person == null) return 0;

            int level = person.injury;
            if (level <= 0) return 0;
            if (level >= HealChanceByLevel.Length) level = HealChanceByLevel.Length - 1;

            int chance = HealChanceByLevel[level];
            if (chance <= 0) return 0;

            // 野外 / 随军：恢复概率打折（默认减半）
            if (!IsRestingInCity(person))
                chance = chance * FieldChancePercent / 100;

            if (HasCompanionNearby(person))
                chance = chance * (100 + CompanionBonus) / 100;

            return chance > 100 ? 100 : chance;
        }

        /// <summary>
        /// 是否在据点内安心休养：不在任何部队里（随军出征也算野外），且有所在城市。
        /// 注意不能只看 CurrentCity——编成部队时并不会清空它，只看它会把手上的部队当成"在城里"。
        /// </summary>
        public static bool IsRestingInCity(Person person)
        {
            if (person == null) return false;
            if (person.mBelongTroop != null) return false;
            return person.CurrentCity != null;
        }

        #endregion

        #region 一次性治愈（华佗一类事件用）

        /// <summary>把一座城里的伤员全部治愈，返回治愈人数</summary>
        public static int HealCity(City city)
        {
            if (city == null || city.allPersons == null) return 0;

            int count = 0;
            for (int i = 0; i < city.allPersons.Count; i++)
            {
                if (Heal(city.allPersons[i])) count++;
            }
            return count;
        }

        /// <summary>把整个剧本里的伤员全部治愈，返回治愈人数</summary>
        public static int HealAll(Scenario scenario)
        {
            if (scenario == null || scenario.personSet == null) return 0;

            int count = 0;
            for (int i = 1; i < scenario.personSet.Count; i++)
            {
                if (Heal(scenario.personSet[i])) count++;
            }
            return count;
        }

        /// <summary>把单个武将治成健康，返回是否真的治过</summary>
        public static bool Heal(Person person)
        {
            if (person == null || person.injury <= 0) return false;
            person.injury = 0;
            return true;
        }

        #endregion

        #region 在旁的配偶 / 义兄弟

        /// <summary>
        /// 配偶或义兄弟是否在旁：同一部队、相邻部队、或同在一座城里。
        /// 命中时恢复概率翻倍（三国志11 的"夫妻疗伤"）。
        /// </summary>
        public static bool HasCompanionNearby(Person person)
        {
            if (person == null) return false;

            Troop troop = person.mBelongTroop;
            if (troop != null)
            {
                if (IsCompanion(person, troop.Leader)) return true;
                if (IsCompanion(person, troop.Member1)) return true;
                if (IsCompanion(person, troop.Member2)) return true;

                Cell cell = troop.cell;
                if (cell != null)
                {
                    bool found = false;
                    cell.GetNeighbors(neighbor =>
                    {
                        if (found) return;
                        Troop other = neighbor != null ? neighbor.troop : null;
                        if (other == null || other == troop) return;
                        if (IsCompanion(person, other.Leader)) { found = true; return; }
                        if (IsCompanion(person, other.Member1)) { found = true; return; }
                        if (IsCompanion(person, other.Member2)) { found = true; }
                    });
                    if (found) return true;
                }
            }

            City city = person.CurrentCity;
            if (city != null && city.allPersons != null)
            {
                for (int i = 0; i < city.allPersons.Count; i++)
                {
                    if (IsCompanion(person, city.allPersons[i])) return true;
                }
            }
            return false;
        }

        /// <summary>other 是否为本人的配偶或义兄弟</summary>
        public static bool IsCompanion(Person person, Person other)
        {
            if (person == null || other == null || other == person) return false;
            if (person.IsSpouse(other)) return true;
            return person.IsBrotherGroupmate(other);
        }

        #endregion
    }
}
