/*
 * 文件名：PersonSeekOfficeRules.cs
 * 描述：在野武将主动求仕的纯规则层（吸引度、月概率、关系判定、播报判据、弹窗文案）。
 *       本类全部为无副作用的静态方法，不读全局单例、不写状态、不分配集合，
 *       月度扫描的热路径可以直接调用；随机数由调用方（PersonSeekOffice）注入。
 * 创建日期：2026-11-26
 * 最后修改：2026-11-26
 */

namespace Sango.Core
{
    /// <summary>
    /// 在野武将求仕规则。
    ///
    /// 【规则口径（玩家需求）】
    ///   · 求仕只向「武将所在城归属的势力」发起 —— 本地势力；
    ///   · 所在城没有势力（中立 / 空城 / 关港无主）时，本月不执行求仕；
    ///   · 不计算城距、不跨城挑选目标，也不跨回合保留「待答求仕」。
    ///
    /// 【原版对应】Open311 没有对应的"在野武将月度主动求仕"实现（原版为探索 / 登用 +
    /// `person_debut` 仕官介绍人；本机亦无 D:\open311\s11 源码可比对），
    /// 因此本类是自主设计，公式与系数全部落在 <see cref="ScenarioVariables"/> 里可调。
    ///
    /// 【性能约束】sango-gc-perf：本类在月度扫描里按每名在野武将调用一次，
    /// 不 new 容器、不使用 LINQ / lambda、不拼字符串；
    /// 只有真正命中（每月至多几人）时，才调用 <see cref="ComposeOfferText"/> 拼文本。
    /// </summary>
    public static class PersonSeekOfficeRules
    {
        /// <summary>相性档位上限：相性差 / 25，封顶 5 档（与 ForceAIHostile 的势力相性距离同口径）</summary>
        public const int MaxAishouLevel = 5;

        /// <summary>吸引度上限（防止极端关系叠出过高的分值，也让月概率上限有意义）</summary>
        public const int MaxAttraction = 60;

        /// <summary>
        /// 月概率硬顶（%）：无论剧本变量怎么配，单次求仕概率都不会超过 50%。
        /// 50 是"强关系武将单月最高"的绝对封顶；<see cref="ScenarioVariables.seekOfficeMaxChance"/> 只能在 1~50 之间再往下收。
        /// </summary>
        public const int MaxChanceLimit = 50;

        /// <summary>候选人最低成年年龄（避免登场未元服的少年求仕）</summary>
        public const int MinSeekAge = 15;

        /// <summary>
        /// 相性档位：相性差越小档位越低（越亲近）。
        /// </summary>
        /// <param name="distance">相性差（0~255）</param>
        /// <param name="step">每多少点相性差算一档（默认 25）</param>
        /// <returns>0~5 档</returns>
        public static int AishouLevel(int distance, int step)
        {
            if (distance <= 0)
                return 0;
            if (step <= 0)
                step = 25;
            int level = distance / step;
            if (level > MaxAishouLevel)
                level = MaxAishouLevel;
            return level;
        }

        /// <summary>在野月数（3 旬 = 1 月，向下取整）。</summary>
        /// <param name="wildTurnCount">在野回合数（Person.wildTurnCount）</param>
        /// <returns>在野月数</returns>
        public static int WildMonths(int wildTurnCount)
        {
            if (wildTurnCount <= 0)
                return 0;
            return wildTurnCount / 3;
        }

        /// <summary>
        /// 在野时长门槛：低于配置月数不执行求仕（刚下野 / 刚被释放的武将需要先沉淀）。
        /// </summary>
        /// <param name="wildMonths">在野月数</param>
        /// <param name="v">剧本变量</param>
        /// <returns>达到门槛返回 true</returns>
        public static bool MeetsWildMonthsRequirement(int wildMonths, ScenarioVariables v)
        {
            if (v == null)
                return false;
            return wildMonths >= v.seekOfficeMinWildMonths;
        }

        /// <summary>
        /// 月度求仕概率：用吸引度折算，限制在 [min, max] 之间；上限绝对不超过 50%。
        /// </summary>
        /// <param name="attraction">吸引度</param>
        /// <param name="v">剧本变量</param>
        /// <returns>概率（%）</returns>
        public static int MonthlyChance(int attraction, ScenarioVariables v)
        {
            if (v == null || attraction <= 0)
                return 0;

            int divisor = v.seekOfficeChanceDivisor > 0 ? v.seekOfficeChanceDivisor : 1;
            int chance = attraction / divisor;

            // 配置上限只能在 1~40 内再收；40% 是硬顶，旧存档里配了 50 也不会突破
            int maxChance = v.seekOfficeMaxChance;
            if (maxChance > MaxChanceLimit) maxChance = MaxChanceLimit;
            if (maxChance < 1) maxChance = 1;

            int minChance = v.seekOfficeMinChance;
            if (minChance < 0) minChance = 0;
            if (minChance > maxChance) minChance = maxChance;

            if (chance < minChance) chance = minChance;
            if (chance > maxChance) chance = maxChance;
            return chance;
        }

        /// <summary>
        /// 吸引度 S：基础 + 相性 + 在野时长 + 关系武将 + 君主魅力 + 小势力。
        ///
        /// 【相性与关系放大（v3）】相性只对"近相性"给大加成：
        /// 0 档 +24、1 档 +16、2 档 +8、3 档及以后 0（按默认权重 8）；关系武将也放大到
        /// 亲爱每人 5（封顶 10）、血亲 / 义兄弟 / 配偶 15。这样"相性近 + 强关系"能顶到 50% 硬顶，
        /// 而相性一般、无关系的普通在野仍然只有个位数概率。
        ///
        /// 逐项含义与默认值见设计稿 §4.4；全部整数运算并最终 clamp 到 [1, 60]。
        /// </summary>
        /// <param name="wildMonths">在野月数（未封顶，内部按参数封顶）</param>
        /// <param name="aishouDistance">与所在城势力君主的相性差</param>
        /// <param name="likeCount">该势力中的亲爱武将数</param>
        /// <param name="kinship">该势力中是否有义兄弟 / 配偶 / 父母</param>
        /// <param name="governorGlamour">该势力君主魅力</param>
        /// <param name="forceCityCount">该势力都市数</param>
        /// <param name="v">剧本变量</param>
        /// <returns>吸引度（1~60）</returns>
        public static int Attraction(int wildMonths, int aishouDistance,
            int likeCount, bool kinship, int governorGlamour, int forceCityCount,
            ScenarioVariables v)
        {
            if (v == null)
                return 0;

            int score = v.seekOfficeBaseScore;

            // 相性项：按 4 : 2 : 1 衰减（对应 0 / 1 / 2 档），3 档及以后不再加分。
            // 这比线性更强调"近相性"：相性差 50+ 的普通武将几乎拿不到相性分，
            // 月概率被压在 10% 左右；而相性差 <50 的武将差距被明显拉开。
            int level = AishouLevel(aishouDistance, v.seekOfficeCompatibilityStep);
            int compatSteps;
            if (level == 0) compatSteps = 4;
            else if (level == 1) compatSteps = 2;
            else if (level == 2) compatSteps = 1;
            else compatSteps = 0;
            score += compatSteps * v.seekOfficeCompatibilityWeight;

            // 在野项：越久越想出仕，封顶避免"老在野"压过一切
            int months = wildMonths;
            if (months > v.seekOfficeWildMonthCap) months = v.seekOfficeWildMonthCap;
            if (months > 0)
            {
                int den = v.seekOfficeWildMonthWeightDen > 0 ? v.seekOfficeWildMonthWeightDen : 1;
                score += months * v.seekOfficeWildMonthWeightNum / den;
            }

            // 关系项：亲爱每人计分并封顶；血亲 / 义兄弟 / 配偶只计一次
            if (likeCount > 0)
            {
                int like = likeCount * v.seekOfficeLikePersonWeight;
                if (like > v.seekOfficeLikePersonCap) like = v.seekOfficeLikePersonCap;
                score += like;
            }
            if (kinship)
                score += v.seekOfficeKinshipWeight;

            // 君主魅力项：50 为中性，每 glamourWeight 点 ±1，截断 -3..+5
            int glamourWeight = v.seekOfficeGlamourWeight > 0 ? v.seekOfficeGlamourWeight : 10;
            int glamour = (governorGlamour - 50) / glamourWeight;
            if (glamour < -3) glamour = -3;
            else if (glamour > 5) glamour = 5;
            score += glamour;

            // 小势力项：城少的势力更容易被投奔（配置 0 即关闭）
            if (forceCityCount > 0 && forceCityCount <= v.seekOfficeSmallForceCities)
                score += v.seekOfficeSmallForceBonus;

            if (score < 1) score = 1;
            else if (score > MaxAttraction) score = MaxAttraction;
            return score;
        }

        /// <summary>
        /// 判断某个武将此刻是否属于目标势力（在职，或作为该势力的俘虏被关押）。
        /// 供「关系武将在目标势力」判定复用；关系列表规模很小，无需缓存。
        /// </summary>
        /// <param name="person">关系武将</param>
        /// <param name="force">目标势力</param>
        /// <returns>属于返回 true</returns>
        public static bool IsInForce(Person person, Force force)
        {
            if (person == null || force == null)
                return false;

            Force belong = person.BelongForce;
            if (belong == force)
                return true;

            // 俘虏的 BelongForce 通常已为空，需要查势力的俘虏名单（列表很短，Contains 足够）
            if (belong == null && person.IsPrisoner)
            {
                System.Collections.Generic.List<Person> captives = force.BeCaptiveList;
                return captives != null && captives.Contains(person);
            }
            return false;
        }

        /// <summary>目标势力中的亲爱武将数量（用于加分与文案）。</summary>
        /// <param name="person">在野武将</param>
        /// <param name="force">目标势力</param>
        /// <returns>数量</returns>
        public static int LikeCountInForce(Person person, Force force)
        {
            if (person == null || force == null)
                return 0;

            SangoObjectList<Person> likes = person.LikePersonList;
            if (likes == null)
                return 0;

            int count = 0;
            for (int i = 0; i < likes.Count; i++)
            {
                if (IsInForce(likes[i], force))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 目标势力中是否有义兄弟 / 配偶 / 父母。
        /// 注意 BrotherList 是"别人把我加进去"的单侧列表，兄弟组头不一定在列表里，
        /// 因此额外检查 <see cref="Person.Brother"/>；父子只覆盖"我的父母"，子女不可枚举。
        /// </summary>
        /// <param name="person">在野武将</param>
        /// <param name="force">目标势力</param>
        /// <returns>有血亲 / 义兄弟关系返回 true</returns>
        public static bool HasKinshipInForce(Person person, Force force)
        {
            if (person == null || force == null)
                return false;

            if (person.Brother != null && IsInForce(person.Brother, force))
                return true;

            System.Collections.Generic.List<Person> brothers = person.BrotherList;
            if (brothers != null)
            {
                for (int i = 0; i < brothers.Count; i++)
                {
                    if (IsInForce(brothers[i], force))
                        return true;
                }
            }

            SangoObjectList<Person> spouses = person.SpouseList;
            if (spouses != null)
            {
                for (int i = 0; i < spouses.Count; i++)
                {
                    if (IsInForce(spouses[i], force))
                        return true;
                }
            }

            return IsInForce(person.Father, force) || IsInForce(person.Mother, force);
        }

        /// <summary>是否存在任意关系武将（亲爱 / 血缘）在目标势力，用于入仕时的忠诚加成。</summary>
        /// <param name="person">在野武将</param>
        /// <param name="force">目标势力</param>
        /// <returns>有则 true</returns>
        public static bool HasRelationInForce(Person person, Force force)
        {
            return LikeCountInForce(person, force) > 0 || HasKinshipInForce(person, force);
        }

        /// <summary>
        /// 取第一个在目标势力里的关系武将（仅用于弹窗文案，允许分配字符串）。
        /// </summary>
        /// <param name="person">在野武将</param>
        /// <param name="force">目标势力</param>
        /// <returns>姓名，无则 null</returns>
        public static string FirstRelationName(Person person, Force force)
        {
            if (person == null || force == null)
                return null;

            SangoObjectList<Person> likes = person.LikePersonList;
            if (likes != null)
            {
                for (int i = 0; i < likes.Count; i++)
                {
                    if (IsInForce(likes[i], force))
                        return likes[i].Name;
                }
            }

            if (person.Brother != null && IsInForce(person.Brother, force))
                return person.Brother.Name;

            System.Collections.Generic.List<Person> brothers = person.BrotherList;
            if (brothers != null)
            {
                for (int i = 0; i < brothers.Count; i++)
                {
                    if (IsInForce(brothers[i], force))
                        return brothers[i].Name;
                }
            }

            SangoObjectList<Person> spouses = person.SpouseList;
            if (spouses != null)
            {
                for (int i = 0; i < spouses.Count; i++)
                {
                    if (IsInForce(spouses[i], force))
                        return spouses[i].Name;
                }
            }

            if (IsInForce(person.Father, force) && person.Father != null)
                return person.Father.Name;
            if (IsInForce(person.Mother, force) && person.Mother != null)
                return person.Mother.Name;
            return null;
        }

        /// <summary>
        /// 是否名士（投奔 AI 势力时值得播报）。
        /// 判据与 ForceAICommand.IsTalentPerson 一致：五项任一 ≥ 阈值。
        /// 只读五维属性，不碰任何集合。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="threshold">阈值（默认 80）</param>
        /// <returns>名士返回 true</returns>
        public static bool IsFamous(Person person, int threshold)
        {
            if (person == null)
                return false;
            if (threshold <= 0)
                threshold = 80;
            return person.Command >= threshold
                || person.Strength >= threshold
                || person.Intelligence >= threshold
                || person.Politics >= threshold
                || person.Glamour >= threshold;
        }

        /// <summary>相性档位的中文描述（弹窗文案用）。</summary>
        /// <param name="level">0~5 档</param>
        /// <returns>近 / 中 / 远 / 极远</returns>
        public static string AishouText(int level)
        {
            if (level <= 1) return "近";
            if (level == 2) return "中";
            if (level <= 4) return "远";
            return "极远";
        }

        /// <summary>
        /// 拼装玩家求仕弹窗的正文。只在真正发起求仕时调用（每月至多 5 人），
        /// 允许字符串分配，不影响月度扫描的热路径。
        /// </summary>
        /// <param name="person">求仕武将</param>
        /// <param name="force">目标势力（所在城的归属势力）</param>
        /// <param name="city">接受求仕的城（所在城或其主城）</param>
        /// <returns>弹窗正文</returns>
        public static string ComposeOfferText(Person person, Force force, City city)
        {
            if (person == null || force == null || city == null)
                return string.Empty;

            // 君主可能在"月初判定"到"玩家答复"之间死亡 / 被俘：这里不能再直接解引用 mGovernor，
            // 否则对话框正文拼装会 NRE，演出队列会卡住。缺君主时退化为势力名，相性按最远档显示。
            Person governor = force.mGovernor;
            string governorName = governor != null ? governor.ColorName : force.Name;
            string forceName = string.IsNullOrEmpty(force.ColorName) ? force.Name : force.ColorName;
            int aishouDistance = governor != null ? person.CompatibilityDistance(governor) : 999;

            int level = AishouLevel(aishouDistance, 25);
            string relationName = FirstRelationName(person, force);
            int months = WildMonths(person.wildTurnCount);

            string relationPart = string.IsNullOrEmpty(relationName)
                ? string.Empty
                : $" · 与{relationName}有旧";

            return $"【求仕】{person.ColorName}：在下{person.Name}，久闻{governorName}主公之名，\n"
                 + $"今在{city.ColorName}，愿为{forceName}效犬马之劳，望主公收纳。\n\n"
                 + $"（相性：{AishouText(level)} · 在野{months}个月{relationPart}）";
        }
    }
}
