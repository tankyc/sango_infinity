/*
 * 文件名：DebatePersonBehaviours.cs
 * 描述：武将舌战行为的注册表 + 数据驱动实现（与单挑的 DuelPersonBehaviours 同款式）。
 *
 * 数据文件：Data/Common/debatePersonBehaviour.json（走 ModManager，支持 Mod 覆盖）
 * 未配置的武将 → Default（全部钩子都是默认实现，等于"没有特殊行为"）。
 *
 * 命中方式（按既定决策：按武将身份 + 特技，不做势力 / 姓名匹配；**不引入优先级配置**）：
 *   · personId  = 武将真实 Id（Person.Id，与 PersonLibrary.json 里的 Id 一致）；
 *   · featureId = 特技 Id（Features.json），一条配置即"所有持有该特技的武将共用的模板"。
 * 命中顺序是**固定层级**（不是可配的 priority）：
 *   1) 武将专属条目（personId）命中即用，**不再叠加**特技条目；
 *   2) 否则按本文件的**声明顺序**，取第一条"该武将持有的特技"对应的条目；
 *   3) 都没有 → Default（全部钩子默认实现 = 不加任何修正）。
 *
 * 话术来源（按既定决策，保持现状，不新增来源）：
 *   · 基础值 = Person.wordTac（持有"书籍"时由 DebateGameSystem.HasAllRhetoric 置全解锁）；
 *   · 本文件只做 rhetoricAdd（与 wordTac 取并集）或 rhetoricOverride（以它为准）。
 *
 * 数据格式（每个武将 / 每个特技一项，字段都可省略；省略 = 该钩子不加任何修正）：
 * {
 *   "behaviours": [
 *     {
 *       "personId": 290,                              // 武将真实 Id（与 featureId 二选一，personId 优先）
 *       "featureId": 87,                              // 特技 Id（例：87 = 论客），本条对持有者全体生效
 *       "name": "诸葛亮",                              // 备注名
 *       "rhetoricAdd": [ 0, 1, 3 ],                    // 追加话术（与 Person.wordTac 取并集）
 *       "rhetoricOverride": [ 1 ],                     // 覆盖话术（写了就以它为准，无视 wordTac）
 *       "allRhetoric": true,                           // 解锁全部话术（等同"持有书籍"）
 *       "attackMulNum": 11, "attackMulDen": 10,        // 攻击力 ×num/den
 *       "hpDamageMulNum": 11, "hpDamageMulDen": 10,    // 体力伤害 ×num/den
 *       "criticalForce": "HaveMercy",                  // 会心强制：PushOn=追击 / HaveMercy=留情
 *       "criticalPushOnBias": 0                        // 会心倾向：随机那支往"追击"偏几个百分点
 *     }
 *   ]
 * }
 */

using System;
using System.Collections.Generic;
using System.IO;
using Sango.Mod;
using TKNewtonsoft.Json;

namespace Sango.Core.Debate
{
    #region 数据模型

    /// <summary>一个武将的舌战行为配置</summary>
    public class DebatePersonBehaviourConfig
    {
        /// <summary>武将真实 Id（Person.Id，与 PersonLibrary.json 里的 Id 一致）；与 featureId 二选一，personId 优先</summary>
        public int personId = -1;
        /// <summary>
        /// 特技 Id（Features.json）。写了这一项的条目对所有**持有该特技**的武将生效，
        /// 相当于一份可共享的模板；与武将专属条目同时命中时，专属条目优先（不叠加）。
        /// </summary>
        public int featureId = -1;
        /// <summary>备注名</summary>
        public string name;

        /// <summary>追加话术（与 wordTac 取并集）</summary>
        public int[] rhetoricAdd;
        /// <summary>覆盖话术（写了就以它为准，无视 wordTac）</summary>
        public int[] rhetoricOverride;
        /// <summary>解锁全部话术</summary>
        public bool allRhetoric;

        public int attackMulNum = 1;
        public int attackMulDen = 1;
        public int hpDamageMulNum = 1;
        public int hpDamageMulDen = 1;

        /// <summary>"PushOn" / "HaveMercy"；留空 = 不强制</summary>
        public string criticalForce;
        /// <summary>随机那一支往"追击"偏几个百分点（正数更爱追击）</summary>
        public int criticalPushOnBias;
    }

    /// <summary>数据文件根</summary>
    public class DebatePersonBehaviourDataSet
    {
        public List<DebatePersonBehaviourConfig> behaviours = new List<DebatePersonBehaviourConfig>();
    }

    #endregion

    #region 数据驱动实现

    /// <summary>按配置驱动的武将舌战行为</summary>
    public class DataDrivenDebatePersonBehaviour : DebatePersonBehaviour
    {
        protected DebatePersonBehaviourConfig cfg;

        public DataDrivenDebatePersonBehaviour(DebatePersonBehaviourConfig config)
        {
            cfg = config;
            name = config != null ? config.name : null;
        }

        protected static bool Contains(int[] arr, int value)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == value) return true;
            }
            return false;
        }

        public override bool ModifyRhetoric(Person self, int rhetoric, bool has)
        {
            if (cfg.allRhetoric) return true;
            // 覆盖：写了这份名单就以它为准（含"一个都不给"的情况）
            if (cfg.rhetoricOverride != null && cfg.rhetoricOverride.Length > 0)
                return Contains(cfg.rhetoricOverride, rhetoric);
            // 追加：与 wordTac 取并集
            if (Contains(cfg.rhetoricAdd, rhetoric)) return true;
            return has;
        }

        public override int ModifyAttack(Person self, Person opponent, int attack)
        {
            if (cfg.attackMulNum == 1 && cfg.attackMulDen == 1) return attack;
            return attack * cfg.attackMulNum / Math.Max(1, cfg.attackMulDen);
        }

        public override int ModifyHpDamage(Person self, Person opponent, int card, int damage)
        {
            if (cfg.hpDamageMulNum == 1 && cfg.hpDamageMulDen == 1) return damage;
            return damage * cfg.hpDamageMulNum / Math.Max(1, cfg.hpDamageMulDen);
        }

        /// <summary>把数据里的 "PushOn" / "DebateCritical_PushOn" 两种写法都认下来</summary>
        protected static int ParseCritical(string text, out bool ok)
        {
            ok = false;
            if (string.IsNullOrEmpty(text)) return -1;
            try
            {
                return (int)(DebateCritical)Enum.Parse(typeof(DebateCritical), text, true);
            }
            catch { }
            try
            {
                int v = (int)(DebateCritical)Enum.Parse(typeof(DebateCritical), "DebateCritical_" + text, true);
                ok = true;
                return v;
            }
            catch { }
            Sango.Log.Warning("debatePersonBehaviour: 无法识别的 criticalForce " + text);
            return -1;
        }

        public override int ModifyCritical(Person self, Person opponent, int critical, bool related)
        {
            int force = ParseCritical(cfg.criticalForce, out bool forced);
            if (forced) return force;
            // 关系判定（怨恨 / 敬爱）优先保留；只把随机那一支往"追击"偏
            if (related || cfg.criticalPushOnBias == 0) return critical;
            int chance = Utils.Clamp(50 + cfg.criticalPushOnBias, 0, 100);
            return DebateRandom.Chance(chance)
                ? (int)DebateCritical.DebateCritical_PushOn
                : (int)DebateCritical.DebateCritical_HaveMercy;
        }
    }

    #endregion

    #region 注册表

    /// <summary>武将舌战行为注册表</summary>
    public static class DebatePersonBehaviours
    {
        /// <summary>数据文件（走 ModManager，支持 Mod 覆盖）</summary>
        public const string ConfigPath = "Data/Common/debatePersonBehaviour.json";

        /// <summary>未配置武将使用的行为（全部默认实现）</summary>
        public static readonly DebatePersonBehaviour Default = new DebatePersonBehaviour();

        /// <summary>特技条目：按数据文件声明顺序参与命中（先声明者优先）</summary>
        private class FeatureRule
        {
            public int featureId;
            public DebatePersonBehaviour behaviour;
        }

        private static Dictionary<int, DebatePersonBehaviour> s_byId;
        private static List<FeatureRule> s_featureRules;
        private static bool s_loaded;

        /// <summary>
        /// 按武将取行为（未配置返回 Default）。命中顺序为固定层级：
        ///   1) 武将专属条目（Person.Id）；
        ///   2) 按声明顺序取第一条"该武将持有的特技"对应的条目；
        ///   3) Default。
        /// 专属命中后不再叠加特技条目（无优先级配置，结果确定）。
        /// </summary>
        public static DebatePersonBehaviour Get(Person person)
        {
            EnsureLoaded();
            if (person == null) return Default;

            if (s_byId != null && s_byId.TryGetValue(person.Id, out DebatePersonBehaviour byId))
                return byId;

            if (s_featureRules != null && s_featureRules.Count > 0)
            {
                // 注意：舌战命名空间下存在同名类型 Debate.Feature，这里必须写全限定名
                SangoObjectList<Sango.Core.Feature> features = person.mFeatureList;
                if (features != null && features.Count > 0)
                {
                    for (int i = 0; i < s_featureRules.Count; i++)
                    {
                        if (features.Contains(s_featureRules[i].featureId))
                            return s_featureRules[i].behaviour;
                    }
                }
            }

            return Default;
        }

        /// <summary>重新加载（换剧本 / 调试用）</summary>
        public static void Reload()
        {
            s_loaded = false;
            s_byId = null;
            s_featureRules = null;
        }

        private static void EnsureLoaded()
        {
            if (s_loaded) return;
            s_loaded = true;
            s_byId = new Dictionary<int, DebatePersonBehaviour>();
            s_featureRules = new List<FeatureRule>();

            try
            {
                ModManager.Instance.LoadFile(ConfigPath, file =>
                {
                    string json = File.ReadAllText(file);
                    DebatePersonBehaviourDataSet data = JsonConvert.DeserializeObject<DebatePersonBehaviourDataSet>(json);
                    if (data == null || data.behaviours == null) return;

                    for (int i = 0; i < data.behaviours.Count; i++)
                    {
                        DebatePersonBehaviourConfig cfg = data.behaviours[i];
                        if (cfg == null) continue;

                        DataDrivenDebatePersonBehaviour behaviour = new DataDrivenDebatePersonBehaviour(cfg);

                        if (cfg.personId >= 0)
                        {
                            // 武将专属条目：同一 Id 重复配置时后写的生效
                            behaviour.personId = cfg.personId;
                            s_byId[cfg.personId] = behaviour;
                        }
                        else if (cfg.featureId >= 0)
                        {
                            // 特技条目：所有持有该特技的武将共用；按声明顺序参与命中
                            FeatureRule rule = new FeatureRule();
                            rule.featureId = cfg.featureId;
                            rule.behaviour = behaviour;
                            s_featureRules.Add(rule);
                        }
                        else
                        {
                            Sango.Log.Warning("debatePersonBehaviour: 有一条配置既没写 personId 也没写 featureId（或都为负值），已忽略："
                                + (string.IsNullOrEmpty(cfg.name) ? "(无名)" : cfg.name));
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Sango.Log.Warning("debatePersonBehaviour 加载失败，全部武将使用默认行为：" + e.Message);
            }
        }
    }

    #endregion
}
