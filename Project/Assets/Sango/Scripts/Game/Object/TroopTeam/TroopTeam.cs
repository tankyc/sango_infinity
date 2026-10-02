using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 出征队伍（编组模板）。三种定义方式，可混用（按 ①→②→③ 顺序占位）：
    ///
    ///   ① <b>固定武将</b> —— <see cref="memberPersonIds"/>（最多 3 人，[0] 为主将）；
    ///   ② <b>固定特技</b> —— <see cref="featureIds"/>（最多 3 个特技，**一个特技配一名持有者**）；
    ///   ③ <b>补位</b> —— <see cref="fill"/>（可选：按**属性下限**或**特技**挑人补位，**没人满足就不配**）。
    ///
    /// 例：吕布队 = 固定武将[吕布] + 补位"智力 ≥ 80" → 配出"吕布 + 军师"；城里没有够格的就吕布一人成队。
    ///     骑神突袭 = 固定特技[骑神, 突袭] + 补位"会『连击』" → 再补一名猛将，没有就不补。
    ///
    /// 兵种（<see cref="troopTypeId"/>）：
    ///   · &gt; 0 —— 推荐兵种（有强兵种特技时应当指定，如骑神→骑兵、枪神→枪兵）；
    ///   · = -1 —— 任一兵种，由 <see cref="TroopTeamMatcher.ResolveTroopType"/> **按特技去适配**
    ///     （拿特技去命中各兵种的 <c>TroopType.matchFeatures</c>，命中最多者胜出；并列取 id 最小 = 基础兵种优先）。
    ///
    /// 不含任何属性门槛（统率/武力/智力等只参与排序，不作为准入条件）。
    /// 只写数值 id、不持对象引用，符合工程"存档只存 id、读档用 IdRef 还原"的惯例。
    /// </summary>
    /// <summary>
    /// 补位条件（**可选**，第三类组队方式）：从候选里挑满足条件的武将补进队伍，
    /// **一个都没有也不影响队伍成立**（"没有可以不配"）。
    ///
    /// 支持两种条件，可单用也可叠加（叠加 = 同时满足）：
    ///   · <b>按属性</b>：<see cref="command"/> / <see cref="strength"/> / <see cref="intelligence"/> 下限
    ///     —— 例："吕布 + 智力 ≥ 80 的军师"；
    ///   · <b>按特技</b>：<see cref="featureIds"/> —— 拥有其中**任意一个**特技即可
    ///     —— 例："骑神队再补一个会『连击』的猛将"。
    ///
    /// 例：<c>fill { intelligence = 70, featureIds = [66, 62] }</c>
    ///   = 补一名"智力 ≥ 70 **且** 会鬼谋或洞察"的人，找不到就不补。
    /// </summary>
    [Serializable]
    public class TroopTeamFill
    {
        /// <summary>统率下限（0 = 不限）</summary>
        public int command;
        /// <summary>武力下限（0 = 不限）</summary>
        public int strength;
        /// <summary>智力下限（0 = 不限）</summary>
        public int intelligence;
        /// <summary>补位要求的特技（拥有其中**任意一个**即可；可空）</summary>
        public int[] featureIds;
        /// <summary>补几位（0 = 补满剩余槽位，最多到 3 人）</summary>
        public int count;

        /// <summary>是否写了补位条件（属性全 0 且无特技 = 没写，等同于不补位）。</summary>
        public bool HasCondition
        {
            get
            {
                return command > 0 || strength > 0 || intelligence > 0
                    || (featureIds != null && featureIds.Length > 0);
            }
        }

        /// <summary>深拷贝。</summary>
        public TroopTeamFill Clone()
        {
            TroopTeamFill f = new TroopTeamFill();
            f.command = command;
            f.strength = strength;
            f.intelligence = intelligence;
            f.featureIds = featureIds != null ? (int[])featureIds.Clone() : null;
            f.count = count;
            return f;
        }
    }

    [Serializable]
    public class TroopTeam
    {
        /// <summary>队伍名（界面显示 / 同名覆盖用）</summary>
        public string name;
        /// <summary>说明（用途与打法，给玩家看）</summary>
        public string desc;
        /// <summary>推荐兵种 id（见 `TroopTypes.json`）；-1 = 任一兵种，按特技适配推断</summary>
        public int troopTypeId = -1;
        /// <summary>固定武将（最多 3 人，[0] 为主将；可空）</summary>
        public int[] memberPersonIds;
        /// <summary>固定特技（最多 3 个，一个特技配一名持有者；可空）</summary>
        public int[] featureIds;
        /// <summary>补位条件：按属性下限 / 按特技（可空 = 不补位；满足者配入，**没人满足就不配**）</summary>
        public TroopTeamFill fill;
        /// <summary>
        /// 推荐兵种的最低兵力（0 = 用默认 <see cref="DefaultMinTroops"/>）。
        /// 本城拿不出这么多兵（或粮 / 金 / 人口 / 技巧 / 兵装不足以组建）→ **跳过这支队伍**，
        /// 面板里显示为"兵力不足"，AI 出征也直接换下一支。
        /// </summary>
        public int minTroops;

        /// <summary>默认最低兵力（模板没写 <c>minTroops</c> 时用）</summary>
        public const int DefaultMinTroops = 3000;

        /// <summary>实际生效的最低兵力。</summary>
        public int EffectiveMinTroops
        {
            get { return minTroops > 0 ? minTroops : DefaultMinTroops; }
        }

        /// <summary>适用圈层：-1 = 不限，0 = 前线，1 = 次前线（供人才调度把成员提前送到对应前线）</summary>
        public int ring = -1;
        /// <summary>优先级（越大越优先；AI 出征 / 人才调度按它排序）</summary>
        public int priority;

        /// <summary>队伍槽位上限：<c>Troop</c> 只有 Leader + Member1 + Member2 三个槽，没有成员列表。</summary>
        public const int MaxMembers = 3;

        /// <summary>是否固定了具体武将。</summary>
        public bool HasFixedMembers
        {
            get { return memberPersonIds != null && memberPersonIds.Length > 0; }
        }

        /// <summary>是否固定了特技。</summary>
        public bool HasFixedFeatures
        {
            get { return featureIds != null && featureIds.Length > 0; }
        }

        /// <summary>是否指定了推荐兵种。</summary>
        public bool HasRecommendedTroopType
        {
            get { return troopTypeId > 0; }
        }

        /// <summary>是否写了补位条件（属性 / 特技；写了就至少尝试补人，没人满足则不补）。</summary>
        public bool HasFill
        {
            get { return fill != null && fill.HasCondition; }
        }

        /// <summary>
        /// 身份成员数（**不含补位**）：固定武将数 或 固定特技数，最多 3。
        ///
        /// 用途：判断"这支队伍在本城凑不凑得起来" —— 补位是可选的（没人满足就少一个人），
        /// 所以可用性只看身份成员是否齐。只写了补位条件的队伍返回 0（能补到人就算可用）。
        /// </summary>
        public int RequiredMemberCount
        {
            get
            {
                if (HasFixedMembers)
                    return Math.Min(MaxMembers, memberPersonIds.Length);
                if (HasFixedFeatures)
                    return Math.Min(MaxMembers, featureIds.Length);
                return 0;
            }
        }

        /// <summary>
        /// 期望人数：固定武将按人数、固定特技按特技数、再加上补位人数（都受 3 槽上限约束）。
        /// 补位 <c>count = 0</c> 表示补满剩余槽位。
        /// </summary>
        public int PlannedMemberCount
        {
            get
            {
                int planned = 0;
                if (HasFixedMembers)
                    planned += memberPersonIds.Length;
                else if (HasFixedFeatures)
                    planned += featureIds.Length;

                if (HasFill)
                    planned += fill.count > 0 ? fill.count : MaxMembers;

                if (planned > MaxMembers)
                    planned = MaxMembers;
                return planned;
            }
        }

        /// <summary>固定武将 id（截断到 3 个，保持顺序：[0] 为主将）。</summary>
        public int[] FixedMemberIds
        {
            get { return Truncate(memberPersonIds); }
        }

        /// <summary>固定特技 id（截断到 3 个，保持顺序）。</summary>
        public int[] FixedFeatureIds
        {
            get { return Truncate(featureIds); }
        }

        /// <summary>深拷贝（玩家"以当前编队另存为"时用，避免共享数组引用）。</summary>
        public TroopTeam Clone()
        {
            TroopTeam t = new TroopTeam();
            t.name = name;
            t.desc = desc;
            t.troopTypeId = troopTypeId;
            t.memberPersonIds = memberPersonIds != null ? (int[])memberPersonIds.Clone() : null;
            t.featureIds = featureIds != null ? (int[])featureIds.Clone() : null;
            t.fill = fill != null ? fill.Clone() : null;
            t.minTroops = minTroops;
            t.ring = ring;
            t.priority = priority;
            return t;
        }

        /// <summary>
        /// 是否与另一支队伍"内容相同"（保存判重用）：兵种一致，且
        ///   · 有固定武将 → 是同一批人（**顺序不敏感**，换个主将顺序也算同一支）；
        ///   · 只有固定特技 → 是同一组特技；
        ///   · 两者都没有（纯补位队）→ 比较补位条件。
        /// </summary>
        public bool SameContentAs(TroopTeam other)
        {
            if (other == null)
                return false;
            if (troopTypeId != other.troopTypeId)
                return false;

            int[] a = FixedMemberIds;
            int[] b = other.FixedMemberIds;
            if (a.Length > 0 || b.Length > 0)
            {
                if (a.Length != b.Length)
                    return false;
                for (int i = 0; i < a.Length; i++)
                {
                    if (!ContainsId(b, a[i]))
                        return false;
                }
                return true;
            }

            int[] fa = FixedFeatureIds;
            int[] fb = other.FixedFeatureIds;
            if (fa.Length > 0 || fb.Length > 0)
            {
                if (fa.Length != fb.Length)
                    return false;
                for (int i = 0; i < fa.Length; i++)
                {
                    if (!ContainsId(fb, fa[i]))
                        return false;
                }
                return true;
            }

            return SameFill(fill, other.fill);
        }

        /// <summary>id 数组是否包含某 id。</summary>
        static bool ContainsId(int[] ids, int id)
        {
            if (ids == null)
                return false;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == id)
                    return true;
            }
            return false;
        }

        /// <summary>补位条件是否相同（空值视为"都没有条件"）。</summary>
        static bool SameFill(TroopTeamFill a, TroopTeamFill b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.command != b.command || a.strength != b.strength || a.intelligence != b.intelligence)
                return false;

            int[] fa = a.featureIds;
            int[] fb = b.featureIds;
            if (fa == null) fa = EmptyIds;
            if (fb == null) fb = EmptyIds;
            if (fa.Length != fb.Length)
                return false;
            for (int i = 0; i < fa.Length; i++)
            {
                if (!ContainsId(fb, fa[i]))
                    return false;
            }
            return true;
        }

        static readonly int[] EmptyIds = new int[0];

        static int[] Truncate(int[] source)
        {
            if (source == null || source.Length == 0)
                return EmptyIds;
            if (source.Length <= MaxMembers)
                return source;
            int[] result = new int[MaxMembers];
            for (int i = 0; i < MaxMembers; i++)
                result[i] = source[i];
            return result;
        }
    }

    /// <summary>
    /// 队伍匹配器：按队伍定义从"本城可用武将"里挑出主将 + 副将，并解析出该用哪个兵种。
    ///
    /// 排序评分（只用于**排序**，不是准入门槛）：
    ///   兵种适性等级 × 1.0 + 统率/100 × 0.6 + 武力/100 × 0.5 + 智力/100 × 0.4 + 每个适配特技 × 0.35
    /// （适性由 <c>Troop.CheckTroopTypeLevel</c> 按兵种 <c>influenceAbility</c> 取值；
    ///   适配特技取兵种自带的 <c>TroopType.matchFeatures</c>）
    /// </summary>
    public static class TroopTeamMatcher
    {
        /// <summary>每个适配特技命中的加分</summary>
        public const float FeatureBonus = 0.35f;

        /// <summary>单个候选的排序分（越大越适合这个队伍 / 兵种）。</summary>
        /// <param name="p">候选武将</param>
        /// <param name="troopType">目标兵种（可为空，为空则只看属性）</param>
        /// <param name="featureHits">输出：命中的兵种适配特技个数</param>
        public static float ScorePerson(Person p, TroopType troopType, out int featureHits)
        {
            featureHits = 0;
            if (p == null)
                return float.MinValue;

            float score = 0f;

            if (troopType != null)
                score += Troop.CheckTroopTypeLevel(troopType, p);

            score += p.Command / 100f * 0.6f;
            score += p.Strength / 100f * 0.5f;
            score += p.Intelligence / 100f * 0.4f;

            if (troopType != null && troopType.matchFeatures != null)
            {
                for (int i = 0; i < troopType.matchFeatures.Length; i++)
                {
                    if (p.HasFeatrue(troopType.matchFeatures[i]))
                    {
                        score += FeatureBonus;
                        featureHits++;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// 按队伍定义挑人，**索引 0 恒为主将**（挑不出来时返回空）。
        ///
        /// · 固定武将：按 id 顺序取（最多 3 人），已阵亡 / 被调走 / 不在候选里则跳过，**不回补**（阵容是明确的）；
        /// · 固定特技：每个特技配一名**持有该特技**的人（按排序分取最高，同一人不重复占位），
        ///   某个特技无人持有 → 该槽空着（队伍可能只有 1~2 人）；
        /// · 属性补位：按 <c>fill</c> 的属性下限补人（排序分最高者优先），**没人满足就不补**；
        ///   且**只在队伍身份已落地时执行** —— 写了固定武将/固定特技却一个都没命中 → 整队返回空
        ///   （避免"神算百出"在没人有神算时退化成一堆路人；只写补位条件的模板不受此限）。
        /// · 多种方式并存：先固定武将、再固定特技、最后属性补位，最多填满 3 个槽。
        /// </summary>
        /// <param name="team">队伍定义</param>
        /// <param name="candidates">候选武将（通常是目标城的 <c>freePersons</c>）</param>
        /// <param name="troopType">目标兵种，用于排序（可空）</param>
        public static List<Person> Match(TroopTeam team, List<Person> candidates, TroopType troopType)
        {
            List<Person> picked = new List<Person>();
            if (team == null || candidates == null || candidates.Count == 0)
                return picked;

            List<Person> usable = new List<Person>();
            for (int i = 0; i < candidates.Count; i++)
            {
                Person p = candidates[i];
                if (p == null || p.Id == 0) continue;
                if (p.IsDead || p.IsPrisoner) continue;
                usable.Add(p);
            }
            if (usable.Count == 0)
                return picked;

            // ① 固定武将
            if (team.HasFixedMembers)
            {
                int[] ids = team.FixedMemberIds;
                for (int i = 0; i < ids.Length; i++)
                {
                    Person found = FindUsable(usable, ids[i], picked);
                    if (found != null)
                        picked.Add(found);
                }
            }

            // ② 固定特技（每特技一人；补满剩余槽位）
            if (team.HasFixedFeatures)
            {
                int[] feats = team.FixedFeatureIds;
                for (int i = 0; i < feats.Length && picked.Count < TroopTeam.MaxMembers; i++)
                {
                    Person best = null;
                    float bestScore = float.MinValue;
                    for (int j = 0; j < usable.Count; j++)
                    {
                        Person p = usable[j];
                        if (ContainsPerson(picked, p.Id)) continue;
                        if (!p.HasFeatrue(feats[i])) continue;       // 该槽要求"持有此特技"
                        int hits;
                        float score = ScorePerson(p, troopType, out hits);
                        if (score > bestScore)
                        {
                            bestScore = score;
                            best = p;
                        }
                    }
                    if (best != null)
                        picked.Add(best);
                }
            }

            // ③ 属性补位（可选）：挑满足属性下限的人补入，**一个都没有就不补**（队伍照常成立）
            //
            // 【门禁】补位只在"队伍身份已经落地"时执行：
            //   · 队伍写了固定武将 / 固定特技 → 必须至少命中一名（picked.Count > 0），
            //     否则整队返回空（例：没人有"神算"时，"神算百出"不该退化成一堆路人）
            //   · 队伍只写了补位条件（没有固定武将也没有固定特技）→ 直接执行，条件本身就是身份
            bool identityMet = picked.Count > 0 || (!team.HasFixedMembers && !team.HasFixedFeatures);
            if (team.HasFill && identityMet)
            {
                int want = team.fill.count > 0 ? team.fill.count : TroopTeam.MaxMembers;
                int added = 0;
                while (picked.Count < TroopTeam.MaxMembers && added < want)
                {
                    Person candidate = null;
                    float candidateScore = float.MinValue;
                    for (int j = 0; j < usable.Count; j++)
                    {
                        Person p = usable[j];
                        if (ContainsPerson(picked, p.Id)) continue;
                        if (!MeetsFill(p, team.fill)) continue;
                        int hits;
                        float score = ScorePerson(p, troopType, out hits);
                        if (score > candidateScore)
                        {
                            candidateScore = score;
                            candidate = p;
                        }
                    }
                    if (candidate == null)
                        break;                                        // 没人满足 → 不补位
                    picked.Add(candidate);
                    added++;
                }
            }

            return picked;
        }

        /// <summary>
        /// 解析该用哪个兵种：
        ///   ① 指定了推荐兵种（<c>troopTypeId &gt; 0</c>）→ 在**可造兵种**里找同 id，找不到返回 null（调用方回落自选）；
        ///   ② 未指定 → **按特技适配推断**：拿队伍特技去命中各兵种的 <c>matchFeatures</c>，
        ///      命中最多者胜出；并列取 id 最小（= 基础兵种优先于特种兵）。
        ///      （例：骑神/突袭 → 骑兵；枪神 → 枪兵）
        /// </summary>
        /// <param name="team">队伍定义</param>
        /// <param name="available">当前可造的兵种（通常来自 <c>TroopType.CheckActivTroopTypeList</c>）</param>
        /// <returns>推荐兵种；null = 不作要求（沿用调用方当前选择）</returns>
        public static TroopType ResolveTroopType(TroopTeam team, List<TroopType> available)
        {
            if (team == null || available == null || available.Count == 0)
                return null;

            if (team.HasRecommendedTroopType)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    if (available[i] != null && available[i].Id == team.troopTypeId)
                        return available[i];
                }
                return null;                                        // 推荐兵种当前造不出来 → 不作要求
            }

            if (!team.HasFixedFeatures)
                return null;                                        // 既无推荐兵种也无特技 → 不限

            int[] feats = team.FixedFeatureIds;
            TroopType best = null;
            int bestHits = 0;
            int hitTypes = 0;                                        // 有命中的兵种数
            int consideredTypes = 0;                                 // 参与比较的兵种数（有 matchFeatures 数据的）
            for (int i = 0; i < available.Count; i++)
            {
                TroopType t = available[i];
                if (t == null || t.matchFeatures == null || t.matchFeatures.Length == 0)
                    continue;

                consideredTypes++;

                int hits = 0;
                for (int f = 0; f < feats.Length; f++)
                {
                    if (feats[f] <= 0) continue;
                    for (int m = 0; m < t.matchFeatures.Length; m++)
                    {
                        if (t.matchFeatures[m] == feats[f])
                        {
                            hits++;
                            break;
                        }
                    }
                }
                if (hits <= 0) continue;

                if (best == null || hits > bestHits)
                {
                    best = t;
                    bestHits = hits;
                    hitTypes = 1;
                }
                else if (hits == bestHits)
                {
                    hitTypes++;
                    if (t.Id < best.Id) best = t;                     // 并列取 id 最小 = 基础兵种优先
                }
            }

            if (best == null)
                return null;                                          // 没有任何兵种适配这些特技 → 不限
            // 所有参与比较的兵种命中数都一样（例：霸王 / 神将 这类全兵种通用特技）→ 无从区分 = 任一兵种
            if (hitTypes == consideredTypes)
                return null;
            return best;
        }

        /// <summary>
        /// 是否满足补位条件。属性部分是"下限都要满足"（AND），特技部分是"拥有任意一个即可"（OR），
        /// 两块之间也是 AND（例：智力 ≥ 70 且 会鬼谋或洞察）。
        /// 注意：补位是**可选**的 —— 没人满足时调用方不补人，队伍照常成立。
        /// </summary>
        public static bool MeetsFill(Person p, TroopTeamFill fill)
        {
            if (p == null || fill == null)
                return false;
            if (fill.command > 0 && p.Command < fill.command) return false;
            if (fill.strength > 0 && p.Strength < fill.strength) return false;
            if (fill.intelligence > 0 && p.Intelligence < fill.intelligence) return false;

            if (fill.featureIds != null && fill.featureIds.Length > 0
                && !HoldsAnyFeature(p, fill.featureIds))
                return false;                                         // 要求"拥有其中任意一个特技"

            return true;
        }

        /// <summary>是否拥有给定特技中的任意一个（空数组 = 不要求，返回 false 由调用方决定语义）。</summary>
        /// <param name="p">武将</param>
        /// <param name="featureIds">特技 id 列表</param>
        public static bool HoldsAnyFeature(Person p, int[] featureIds)
        {
            if (p == null || featureIds == null || featureIds.Length == 0)
                return false;
            for (int i = 0; i < featureIds.Length; i++)
            {
                if (featureIds[i] > 0 && p.HasFeatrue(featureIds[i]))
                    return true;
            }
            return false;
        }

        /// <summary>队伍里是否包含某个武将（按 id）。</summary>
        public static bool ContainsPerson(TroopTeam team, int personId)
        {
            if (team == null || personId <= 0)
                return false;
            int[] ids = team.FixedMemberIds;
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] == personId)
                    return true;
            }
            return false;
        }

        /// <summary>已选列表里是否包含某个人。</summary>
        static bool ContainsPerson(List<Person> list, int personId)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == personId)
                    return true;
            }
            return false;
        }

        /// <summary>在候选里找一个可用且未被选中的武将（按 id）。</summary>
        static Person FindUsable(List<Person> usable, int personId, List<Person> picked)
        {
            if (personId <= 0 || ContainsPerson(picked, personId))
                return null;
            for (int i = 0; i < usable.Count; i++)
            {
                if (usable[i].Id == personId)
                    return usable[i];
            }
            return null;
        }
    }
}
