/*
 * 文件名：DuelPersonBehaviours.cs
 * 描述：武将单挑行为的注册表 + 数据驱动实现。
 *
 * 数据文件：Data/Common/duelPersonBehaviour.json（走 ModManager，支持 Mod 覆盖）
 * 未配置的武将 → Default（全部钩子都是默认实现，等于"没有特殊行为"）。
 *
 * 数据格式（每个武将一项，字段都可省略）：
 * {
 *   "behaviours": [
 *     {
 *       "id": "Ryofu",                  // 单挑内部 PersonId 名（PersonId 枚举名）
 *       "personId": -1,                 // 或直接用武将真实 Id（>= 0 时优先命中）
 *       "name": "吕布",
 *       "criticalChanceAdd": 3,         // 会心(暴击)概率 +
 *       "criticalChanceVirtual": 0,     // 会心：虚拟寿命模式下的固定加成（黄忠）
 *       "criticalAge": [ { "maxAge": 60, "add": 2 }, ... ],   // 会心：年龄档（maxAge=0 表示"其余"）
 *       "duelStrengthAdd": 10,          // 战力(revised) +
 *       "duelStrengthVirtual": 0,       // 战力：虚拟寿命模式下的固定加成
 *       "duelStrengthAge": [ ... ],     // 战力：年龄档
 *       "ftkChanceAdd": 10,             // 一击必杀概率 +
 *       "ftkChanceVirtual": 0,
 *       "ftkAge": [ ... ],              // 一击必杀：年龄档
 *       "ftkImmune": true,              // 不会被一击必杀
 *       "retreatChanceAdd": 2,          // 退却概率 +
 *       "firstChanceAdd": 2,            // 先攻概率 +（关羽）
 *       "opponentFirstChanceAdd": -2,   // 自己作为对手时，对对方先攻概率的修正
 *       "attackDamageMulNum": 13,       // 普通攻击 ×num/den
 *       "attackDamageMulDen": 11,
 *       "specialDamageMul": [ { "specials": ["Nisetaikyaku"], "num": 11, "den": 10 } ],
 *       "specialHitMul":    { "specials": ["Nisetaikyaku"], "opponents": ["Kanu"], "num": 11, "den": 10 },
 *       "alwaysBlockWith": ["Chouhi", "Kanu"],
 *       "aiTable": "Ryofu"
 *     }
 *   ]
 * }
 */

using System;
using System.Collections.Generic;
using System.IO;
using Sango.Mod;
using TKNewtonsoft.Json;

namespace Sango.Core.Duel
{
    #region 数据模型

    /// <summary>年龄档加成：age &lt; maxAge 时生效；maxAge = 0 表示"其余年龄"</summary>
    public class AgeBonusConfig
    {
        public int maxAge;
        public int add;
    }

    /// <summary>一个武将的行为配置</summary>
    public class DuelPersonBehaviourConfig
    {
        /// <summary>单挑内部 PersonId 名（PersonId 枚举名，如 "Ryofu"）</summary>
        public string id;
        /// <summary>武将真实 Id；>= 0 时优先用它命中</summary>
        public int personId = -1;
        /// <summary>备注名</summary>
        public string name;

        public int criticalChanceAdd;
        public int criticalChanceVirtual;
        public AgeBonusConfig[] criticalAge;

        public int duelStrengthAdd;
        public int duelStrengthVirtual;
        public AgeBonusConfig[] duelStrengthAge;

        public int ftkChanceAdd;
        public int ftkChanceVirtual;
        public AgeBonusConfig[] ftkAge;
        public bool ftkImmune;

        public int retreatChanceAdd;
        public int firstChanceAdd;
        public int opponentFirstChanceAdd;

        public int attackDamageMulNum = 1;
        public int attackDamageMulDen = 1;

        public MulConfig[] specialDamageMul;
        public SpecialHitMulConfig specialHitMul;

        public string[] alwaysBlockWith;
        public string aiTable;
    }

    /// <summary>倍率配置（乘 num/den，整数运算，与既有代码口径一致）</summary>
    public class MulConfig
    {
        public string[] specials;
        public int num = 1;
        public int den = 1;
    }

    /// <summary>对特定对手的倍率配置</summary>
    public class SpecialHitMulConfig : MulConfig
    {
        public string[] opponents;
        /// <summary>对手不在 opponents 名单里时，直接把数值设成它（0 = 不启用，保持原值）</summary>
        public int otherwiseValue;
    }

    /// <summary>数据文件根</summary>
    public class DuelPersonBehaviourDataSet
    {
        public List<DuelPersonBehaviourConfig> behaviours = new List<DuelPersonBehaviourConfig>();
    }

    #endregion

    #region 数据驱动实现

    /// <summary>按配置驱动的武将单挑行为</summary>
    public class DataDrivenDuelPersonBehaviour : DuelPersonBehaviour
    {
        protected DuelPersonBehaviourConfig cfg;

        public DataDrivenDuelPersonBehaviour(DuelPersonBehaviourConfig config)
        {
            cfg = config;
            name = config != null ? config.name : null;
        }

        /// <summary>年龄档命中：按顺序找第一个 age &lt; maxAge 的档；maxAge = 0 的档兜底</summary>
        protected static int AgeBonus(AgeBonusConfig[] stages, int age)
        {
            if (stages == null || stages.Length == 0) return 0;
            for (int i = 0; i < stages.Length; i++)
            {
                AgeBonusConfig s = stages[i];
                if (s == null) continue;
                if (s.maxAge == 0 || age < s.maxAge)
                    return s.add;
            }
            return 0;
        }

        /// <summary>
        /// 名字匹配。数据里既能写"带前缀的枚举全名"（DuelSpecial_Nisetaikyaku），
        /// 也能写"去掉前缀的短名"（Nisetaikyaku），两边都认。
        /// </summary>
        protected static bool MatchEnumName(string[] ids, string fullName, string shortName)
        {
            if (ids == null || ids.Length == 0) return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.Equals(ids[i], fullName, StringComparison.OrdinalIgnoreCase)) return true;
                if (!string.IsNullOrEmpty(shortName) && string.Equals(ids[i], shortName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>去掉枚举名前缀（形如 DuelSpecial_ / DuelAIType_）</summary>
        protected static string ShortEnumName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            int i = fullName.IndexOf('_');
            return i >= 0 ? fullName.Substring(i + 1) : fullName;
        }

        protected static bool MatchName(string[] ids, string cur)
        {
            return MatchEnumName(ids, cur, ShortEnumName(cur));
        }

        protected static bool Match(Person person, string[] ids)
        {
            if (ids == null || ids.Length == 0) return false;
            if (person == null) return false;
            return MatchName(ids, person.GetId().ToString());
        }

        /// <summary>按年龄档 + 虚拟模式求一次加成</summary>
        protected int CalcAgedAdd(int add, int virtualAdd, AgeBonusConfig[] stages, Person self)
        {
            if (DuelRules.GetLifeMode() == LifeMode.Virtual && virtualAdd != 0)
                return virtualAdd;
            if (stages != null && self != null)
                add += AgeBonus(stages, self.GetAge());
            return add;
        }

        public override int ModifyCriticalChance(Person self, Person opponent, int chance, bool attacker)
        {
            if (!attacker) return chance;
            return chance + CalcAgedAdd(cfg.criticalChanceAdd, cfg.criticalChanceVirtual, cfg.criticalAge, self);
        }

        public override int ModifyDuelStrength(Person self, Person opponent, int strength, bool revised)
        {
            if (!revised) return strength;
            return strength + CalcAgedAdd(cfg.duelStrengthAdd, cfg.duelStrengthVirtual, cfg.duelStrengthAge, self);
        }

        public override int ModifyFtkChance(Person self, Person opponent, int chance, bool attacker)
        {
            if (!attacker) return chance;
            return chance + CalcAgedAdd(cfg.ftkChanceAdd, cfg.ftkChanceVirtual, cfg.ftkAge, self);
        }

        public override bool IsFtkImmune(Person self, Person opponent) { return cfg.ftkImmune; }

        public override int ModifyRetreatChance(Person self, Person opponent, int chance)
        {
            return chance + cfg.retreatChanceAdd;
        }

        public override int ModifyFirstChance(Person self, Person opponent, int chance)
        {
            return chance + cfg.firstChanceAdd;
        }

        public override int ModifyOpponentFirstChance(Person self, Person opponent, int chance)
        {
            return chance + cfg.opponentFirstChanceAdd;
        }

        public override bool IsAlwaysBlock(Duel duel, Person self, Person opponent)
        {
            // 名单是"互相必定格挡的组合"：双方都要在名单里
            return Match(self, cfg.alwaysBlockWith) && Match(opponent, cfg.alwaysBlockWith);
        }

        public override int GetAITableId(Person self)
        {
            if (string.IsNullOrEmpty(cfg.aiTable)) return -1;
            try { return (int)(DuelAIType)Enum.Parse(typeof(DuelAIType), cfg.aiTable, true); }
            catch { }
            try { return (int)(DuelAIType)Enum.Parse(typeof(DuelAIType), "DuelAIType_" + cfg.aiTable, true); }
            catch { }
            Sango.Log.Warning("duelPersonBehaviour: 无法识别的 aiTable " + cfg.aiTable);
            return -1;
        }

        public override int ModifyAttackDamage(Person self, Person opponent, int damage)
        {
            if (cfg.attackDamageMulNum == 1 && cfg.attackDamageMulDen == 1) return damage;
            return damage * cfg.attackDamageMulNum / Math.Max(1, cfg.attackDamageMulDen);
        }

        protected static bool MatchSpecial(int special, string[] specials)
        {
            if (specials == null || specials.Length == 0) return true;
            string full = ((DuelSpecial)special).ToString();
            return MatchEnumName(specials, full, ShortEnumName(full));
        }

        public override int ModifySpecialDamage(Person self, Person opponent, int special, int damage)
        {
            if (cfg.specialDamageMul == null) return damage;
            for (int i = 0; i < cfg.specialDamageMul.Length; i++)
            {
                MulConfig m = cfg.specialDamageMul[i];
                if (m != null && MatchSpecial(special, m.specials))
                    damage = damage * m.num / Math.Max(1, m.den);
            }
            return damage;
        }

        public override int ModifySpecialHitDamage(Person self, Person opponent, int special, int damage)
        {
            SpecialHitMulConfig m = cfg.specialHitMul;
            if (m == null) return damage;
            if (!MatchSpecial(special, m.specials)) return damage;

            if (m.opponents != null && m.opponents.Length > 0)
            {
                if (Match(opponent, m.opponents))
                    return damage * m.num / Math.Max(1, m.den);
                // 对手不在名单里：有的武将反而是"必定命中"（例：夏侯渊/黄忠 用伪退却打无名武将）
                return m.otherwiseValue != 0 ? m.otherwiseValue : damage;
            }
            return damage * m.num / Math.Max(1, m.den);
        }
    }

    #endregion

    #region 注册表

    /// <summary>武将单挑行为注册表</summary>
    public static class DuelPersonBehaviours
    {
        /// <summary>数据文件（走 ModManager，支持 Mod 覆盖）</summary>
        public const string ConfigPath = "Data/Common/duelPersonBehaviour.json";

        /// <summary>未配置武将使用的行为（全部默认实现）</summary>
        public static readonly DuelPersonBehaviour Default = new DuelPersonBehaviour();

        private static Dictionary<PersonId, DuelPersonBehaviour> s_byPersonId;
        private static Dictionary<int, DuelPersonBehaviour> s_byRealId;
        private static bool s_loaded;

        /// <summary>按武将取行为（未配置返回 Default）</summary>
        public static DuelPersonBehaviour Get(Person person)
        {
            EnsureLoaded();
            if (person == null) return Default;

            if (s_byRealId != null && s_byRealId.TryGetValue(person.Id, out DuelPersonBehaviour byId))
                return byId;

            PersonId pid = person.GetId();
            if (pid != PersonId.Invalid && s_byPersonId != null && s_byPersonId.TryGetValue(pid, out DuelPersonBehaviour byPid))
                return byPid;

            return Default;
        }

        /// <summary>重新加载（换剧本 / 调试用）</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_byPersonId = null;
            s_byRealId = null;
        }

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_byPersonId = new Dictionary<PersonId, DuelPersonBehaviour>();
            s_byRealId = new Dictionary<int, DuelPersonBehaviour>();

            try
            {
                ModManager.Instance.LoadFile(ConfigPath, file =>
                {
                    string json = File.ReadAllText(file);
                    DuelPersonBehaviourDataSet data = JsonConvert.DeserializeObject<DuelPersonBehaviourDataSet>(json);
                    if (data == null || data.behaviours == null) return;

                    for (int i = 0; i < data.behaviours.Count; i++)
                    {
                        DuelPersonBehaviourConfig cfg = data.behaviours[i];
                        if (cfg == null) continue;

                        DataDrivenDuelPersonBehaviour behaviour = new DataDrivenDuelPersonBehaviour(cfg);

                        if (cfg.personId >= 0)
                            s_byRealId[cfg.personId] = behaviour;

                        if (!string.IsNullOrEmpty(cfg.id))
                        {
                            try
                            {
                                PersonId pid = (PersonId)Enum.Parse(typeof(PersonId), cfg.id, true);
                                behaviour.personId = pid;
                                s_byPersonId[pid] = behaviour;
                            }
                            catch
                            {
                                Sango.Log.Warning("duelPersonBehaviour: 无法识别的 PersonId 名 " + cfg.id);
                            }
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Sango.Log.Warning("duelPersonBehaviour 加载失败，全部武将使用默认行为：" + e.Message);
            }
        }
    }

    #endregion
}
