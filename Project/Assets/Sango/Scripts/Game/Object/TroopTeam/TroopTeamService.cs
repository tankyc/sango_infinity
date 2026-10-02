using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>队伍在指定城池的可用状态。</summary>
    public enum TroopTeamAvailability
    {
        /// <summary>可用：身份成员全在本城**空闲**武将里（freePersons），可以立刻出征。</summary>
        Ready = 0,
        /// <summary>人员满足但不可用：身份成员在籍（allPersons）却不在空闲表 —— 在部队里 / 正在执行任务。</summary>
        MembersBusy = 1,
        /// <summary>推荐兵种兵力不足：本城拿不出该队伍要求的最低兵力（含粮/金/人口/技巧/兵装）→ 跳过。</summary>
        TroopShortage = 2,
        /// <summary>凑不起来（界面可过滤掉）。</summary>
        Unavailable = 3,
    }

    /// <summary>队伍成员槽位的状态（界面据此区分展示：可用 / 被占用 / 不在本城）。</summary>
    public enum TroopTeamMemberState
    {
        /// <summary>该槽没有成员需求（队伍人本来就少）。</summary>
        Empty = 0,
        /// <summary>可用：人在本城**空闲**武将里，可以立刻出征。</summary>
        Ready = 1,
        /// <summary>被占用：人在本城**在籍**武将里（在部队 / 正在执行任务）。</summary>
        Busy = 2,
        /// <summary>不在：这个人不在本城（阵亡 / 被俘 / 未登场），或该槽没人满足条件。</summary>
        Missing = 3,
    }

    /// <summary>队伍引用（区分"我的队伍"与"推荐模板"）。</summary>
    public struct TroopTeamRef
    {
        /// <summary>队伍</summary>
        public TroopTeam team;
        /// <summary>true = 玩家自建（可删可覆盖），false = 预制作推荐模板（只读）</summary>
        public bool custom;
    }

    /// <summary>
    /// 一条待选队伍 + 它在当前城池的评估结果（推荐队伍面板 / AI 出征共用）。
    /// 列表项展示成员时，把 <see cref="members"/>（可用）或 <see cref="busyMembers"/>（被占用）
    /// 逐个喂给 <c>Sango.UI.UIPersonItem.SetPerson</c> 即可；
    /// 需要区分"哪个人不在 / 被占用"时，用 <see cref="slotPersons"/> + <see cref="memberStates"/>
    /// 按槽位逐个展示（索引 0 = 主将）。
    /// </summary>
    public class TroopTeamCandidate
    {
        /// <summary>队伍定义</summary>
        public TroopTeam team;
        /// <summary>是否玩家自建</summary>
        public bool custom;
        /// <summary>可用状态</summary>
        public TroopTeamAvailability availability = TroopTeamAvailability.Unavailable;
        /// <summary>该用哪个兵种（null = 不限，沿用界面/AI 当前选择）</summary>
        public TroopType troopType;
        /// <summary>可用成员（<see cref="TroopTeamAvailability.Ready"/> 时有值，索引 0 为主将）</summary>
        public List<Person> members;
        /// <summary>被占用 / 在籍但不可用的成员（<see cref="TroopTeamAvailability.MembersBusy"/> 时有值，仅供展示）</summary>
        public List<Person> busyMembers;
        /// <summary>
        /// 逐槽位期望的成员（索引 0 = 主将，长度为 <see cref="TroopTeam.MaxMembers"/>）。
        /// <see cref="TroopTeamMemberState.Missing"/> 的槽也给对象（固定武将按 id 解析，界面可以显示"这个人不在"）。
        /// </summary>
        public Person[] slotPersons = new Person[TroopTeam.MaxMembers];
        /// <summary>逐槽位状态，与 <see cref="slotPersons"/> 一一对应。</summary>
        public TroopTeamMemberState[] memberStates = new TroopTeamMemberState[TroopTeam.MaxMembers];
        /// <summary>身份成员数（不含补位）</summary>
        public int requiredCount;
        /// <summary>身份成员里当前可用的数量</summary>
        public int readyCount;
        /// <summary>该队伍要求的最低兵力（0 = 不要求）</summary>
        public int requiredTroops;
        /// <summary>本城该兵种当前能拿出的兵力（兵力与兵装双重限制；仅供界面显示）</summary>
        public int buildableTroops;

        /// <summary>队伍名</summary>
        public string name { get { return team != null ? team.name : ""; } }
        /// <summary>说明</summary>
        public string desc { get { return team != null ? team.desc : ""; } }
        /// <summary>是否可用</summary>
        public bool IsReady { get { return availability == TroopTeamAvailability.Ready; } }
        /// <summary>兵种名（不限时显示"不限"）</summary>
        public string troopTypeName { get { return troopType != null ? troopType.Name : "不限"; } }
        /// <summary>成员进度文本，如 "2/2"（无身份成员时显示可用人数）</summary>
        public string memberText
        {
            get
            {
                if (requiredCount > 0)
                    return readyCount + "/" + requiredCount;
                return readyCount.ToString();
            }
        }
        /// <summary>状态文本（界面直接显示）</summary>
        public string statusText
        {
            get
            {
                switch (availability)
                {
                    case TroopTeamAvailability.Ready: return "可出征";
                    case TroopTeamAvailability.MembersBusy: return "人员被占用";
                    case TroopTeamAvailability.TroopShortage:
                        return "兵力不足(" + buildableTroops + "/" + requiredTroops + ")";
                    default: return "不可用";
                }
            }
        }
    }

    /// <summary>
    /// 出征队伍服务层：把"队伍定义"×"当前城池"算出候选列表与可用性，供界面与 AI 共用。
    ///
    /// 判定口径（两种池）：
    ///   · freePersons —— 本城**空闲**武将（能立刻出征）→ <see cref="TroopTeamAvailability.Ready"/>;
    ///   · allPersons  —— 本城**在籍**武将（含在部队 / 执行任务的）→ <see cref="TroopTeamAvailability.MembersBusy"/>。
    /// 两种都只看**身份成员**（固定武将 / 固定特技），补位是可选的、不影响可用性。
    /// </summary>
    public static class TroopTeamService
    {
        /// <summary>圈层是否匹配该队伍（队伍 ring &lt; 0 = 不限）。</summary>
        public static bool RingMatches(TroopTeam team, int ring)
        {
            if (team == null) return false;
            return team.ring < 0 || team.ring == ring;
        }

        /// <summary>
        /// 全部待选队伍：**我的队伍在前，推荐模板在后**；各自按 priority 降序。
        /// </summary>
        public static List<TroopTeamRef> AllTeamsSorted()
        {
            List<TroopTeamRef> list = new List<TroopTeamRef>();

            List<TroopTeam> mine = CustomTroopTeams.teams;
            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i] == null) continue;
                TroopTeamRef r;
                r.team = mine[i];
                r.custom = true;
                list.Add(r);
            }

            List<TroopTeam> templates = RecommendedTroopTeams.All;
            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i] == null) continue;
                TroopTeamRef r;
                r.team = templates[i];
                r.custom = false;
                list.Add(r);
            }

            // 我的队伍优先（同档），档内按 priority 降序
            list.Sort(delegate (TroopTeamRef a, TroopTeamRef b)
            {
                if (a.custom != b.custom)
                    return a.custom ? -1 : 1;
                int pa = a.team != null ? a.team.priority : 0;
                int pb = b.team != null ? b.team.priority : 0;
                if (pa != pb)
                    return pb.CompareTo(pa);
                string na = a.team != null ? a.team.name : "";
                string nb = b.team != null ? b.team.name : "";
                return string.Compare(na, nb, StringComparison.Ordinal);
            });

            return list;
        }

        /// <summary>
        /// 本城组建某兵种的"可组建兵力"：受现有兵力与兵装双重限制（兵种为空时只看兵力）。
        /// 仅用于界面显示；判定够不够用请用 <see cref="TroopEnough"/>。
        /// </summary>
        public static int BuildableTroops(City city, TroopType troopType)
        {
            if (city == null) return 0;
            int amount = city.troops;
            if (troopType != null && city.itemStore != null)
                amount = city.itemStore.CheckCostMin(troopType.costItems, amount);
            return amount > 0 ? amount : 0;
        }

        /// <summary>
        /// 本城能否拿出这么多兵力组建该兵种：兵力要够，
        /// 且（指定兵种时）粮 / 金 / 人口 / 技巧 / 兵装都够（<c>TroopType.CheckCost</c>）。
        /// </summary>
        public static bool TroopEnough(City city, TroopType troopType, int need)
        {
            if (city == null) return false;
            if (need <= 0) return true;
            if (city.troops < need) return false;
            if (troopType != null && city.BelongForce != null && !troopType.CheckCost(city, need))
                return false;
            return true;
        }

        /// <summary>
        /// 评估一支队伍在某城的可用性。
        /// </summary>
        /// <param name="team">队伍定义</param>
        /// <param name="custom">是否玩家自建</param>
        /// <param name="city">目标城（用其 freePersons / allPersons）</param>
        /// <param name="available">当前可造兵种（用于解析推荐兵种；可空 = 不限）</param>
        public static TroopTeamCandidate Evaluate(TroopTeam team, bool custom, City city, List<TroopType> available)
        {
            TroopTeamCandidate c = new TroopTeamCandidate();
            c.team = team;
            c.custom = custom;
            if (team == null || city == null)
                return c;

            c.troopType = TroopTeamMatcher.ResolveTroopType(team, available);
            c.requiredCount = team.RequiredMemberCount;
            c.requiredTroops = team.EffectiveMinTroops;
            c.buildableTroops = BuildableTroops(city, c.troopType);

            // 逐槽位算出"期望的人 + 状态"，供界面在武将头像上区分 可用 / 被占用 / 不在
            BuildSlotStates(c, city);

            // ① 空闲武将（可立刻出征）
            List<Person> free = TroopTeamMatcher.Match(team, city.freePersons, c.troopType);
            c.readyCount = free != null ? free.Count : 0;

            bool membersEnough = c.requiredCount > 0 ? c.readyCount >= c.requiredCount : c.readyCount > 0;
            bool troopsEnough = TroopEnough(city, c.troopType, c.requiredTroops);

            if (membersEnough && troopsEnough)
            {
                c.availability = TroopTeamAvailability.Ready;
                c.members = free;
                return c;
            }

            // ② 兵力不够 → 跳过这支队伍（比"人不够"更硬的理由，先报它）
            if (!troopsEnough)
            {
                c.availability = TroopTeamAvailability.TroopShortage;
                return c;
            }

            // ③ 在籍但不可用（在部队 / 执行任务）—— 让玩家知道"人其实是有的，只是现在抽不出来"
            List<Person> all = city.allPersons != null ? city.allPersons.objects : null;
            List<Person> busy = TroopTeamMatcher.Match(team, all, c.troopType);
            int busyCount = busy != null ? busy.Count : 0;
            bool busyEnough = c.requiredCount > 0 ? busyCount >= c.requiredCount : busyCount > 0;
            if (busyEnough)
            {
                c.availability = TroopTeamAvailability.MembersBusy;
                c.busyMembers = busy;
            }

            return c;
        }

        /// <summary>武将列表里是否有该 id 的人（列表可空；空列表视为没有）。</summary>
        static bool ContainsPerson(List<Person> list, int personId)
        {
            if (list == null || personId <= 0)
                return false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].Id == personId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 逐槽位算出"期望的人 + 状态"（<see cref="TroopTeamCandidate.slotPersons"/> / <see cref="TroopTeamCandidate.memberStates"/>）。
        ///
        /// 口径（槽位顺序与 <see cref="TroopTeamMatcher.Match"/> 一致：固定武将 → 固定特技 → 补位）：
        ///   · 固定武将槽：按 id **直接解析**（人不在本城也能拿到对象，界面才能显示"这个人不在"）→
        ///     在空闲表 = Ready，在在籍表 = Busy，两边都没有 = Missing；
        ///   · 固定特技 / 补位槽：拿本城**在籍**武将匹配，匹配到的人落位（在空闲表 = Ready，否则 Busy），
        ///     一个都没匹配到而队伍又期望这个槽 = Missing（没人满足条件）；
        ///   · 队伍本来就不到 3 人的空位 = Empty。
        /// </summary>
        static void BuildSlotStates(TroopTeamCandidate c, City city)
        {
            if (c == null || c.team == null || city == null)
                return;

            List<Person> free = city.freePersons;
            List<Person> all = city.allPersons != null ? city.allPersons.objects : null;

            int[] fixedIds = c.team.FixedMemberIds;
            int slot = 0;

            // ① 固定武将：按 id 解析出对象（与他在不在本城无关）
            for (int i = 0; i < fixedIds.Length && slot < TroopTeam.MaxMembers; i++)
            {
                Person person = IdRef.Resolve<Person>(fixedIds[i]);
                TroopTeamMemberState state;
                if (person == null)
                    state = TroopTeamMemberState.Missing;
                else if (ContainsPerson(free, person.Id))
                    state = TroopTeamMemberState.Ready;
                else if (ContainsPerson(all, person.Id))
                    state = TroopTeamMemberState.Busy;
                else
                    state = TroopTeamMemberState.Missing;

                c.slotPersons[slot] = person;
                c.memberStates[slot] = state;
                slot++;
            }

            // ② 固定特技 / 补位槽：用在籍武将的匹配结果落位（跳过已被固定武将占用的）
            if (c.team.HasFixedFeatures || c.team.HasFill)
            {
                List<Person> matched = TroopTeamMatcher.Match(c.team, all, c.troopType);
                if (matched != null)
                {
                    for (int i = 0; i < matched.Count && slot < TroopTeam.MaxMembers; i++)
                    {
                        Person person = matched[i];
                        if (person == null) continue;
                        if (FindSlot(c, person.Id) >= 0) continue;      // 已被前面的槽占用

                        c.slotPersons[slot] = person;
                        c.memberStates[slot] = ContainsPerson(free, person.Id)
                            ? TroopTeamMemberState.Ready : TroopTeamMemberState.Busy;
                        slot++;
                    }
                }
            }

            // ③ 队伍期望还有人，但一个都没匹配到 → 标记为"没人满足条件"
            int planned = c.team.PlannedMemberCount;
            for (; slot < planned && slot < TroopTeam.MaxMembers; slot++)
                c.memberStates[slot] = TroopTeamMemberState.Missing;

            // ④ 其余槽位为空槽（保持 slotPersons = null / Empty）
            for (; slot < TroopTeam.MaxMembers; slot++)
                c.memberStates[slot] = TroopTeamMemberState.Empty;
        }

        /// <summary>该武将在候选里已占用哪个槽位（没有返回 -1）。</summary>
        static int FindSlot(TroopTeamCandidate c, int personId)
        {
            if (c == null || personId <= 0)
                return -1;
            for (int i = 0; i < c.slotPersons.Length; i++)
            {
                Person person = c.slotPersons[i];
                if (person != null && person.Id == personId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 当前城池的候选列表：可用 + 人员满足但不可用（凑不齐的也返回，由界面决定是否展示）。
        /// 排序：可用 → 人员被占用 → 其它；同档内"我的队伍"优先、再按 priority 降序。
        /// </summary>
        public static List<TroopTeamCandidate> BuildList(City city, List<TroopType> available)
        {
            List<TroopTeamCandidate> result = new List<TroopTeamCandidate>();
            if (city == null)
                return result;

            List<TroopTeamRef> all = AllTeamsSorted();
            for (int i = 0; i < all.Count; i++)
                result.Add(Evaluate(all[i].team, all[i].custom, city, available));

            result.Sort(delegate (TroopTeamCandidate a, TroopTeamCandidate b)
            {
                int ra = (int)a.availability;
                int rb = (int)b.availability;
                if (ra != rb) return ra.CompareTo(rb);
                if (a.custom != b.custom) return a.custom ? -1 : 1;
                int pa = a.team != null ? a.team.priority : 0;
                int pb = b.team != null ? b.team.priority : 0;
                if (pa != pb) return pb.CompareTo(pa);
                string na = a.team != null ? a.team.name : "";
                string nb = b.team != null ? b.team.name : "";
                return string.Compare(na, nb, StringComparison.Ordinal);
            });

            return result;
        }

        /// <summary>
        /// 在某个人选池里按队伍定义挑人（索引 0 = 主将），并解析出该用的兵种。
        /// </summary>
        /// <param name="team">队伍定义</param>
        /// <param name="pool">人选池（出征界面传 <c>city.freePersons</c>）</param>
        /// <param name="available">当前可造兵种（可空）</param>
        /// <param name="troopType">输出：推荐 / 推断出的兵种（null = 不限）</param>
        /// <returns>挑中的成员；空 = 用不了这支队伍</returns>
        public static List<Person> MatchMembers(TroopTeam team, List<Person> pool, List<TroopType> available,
            out TroopType troopType)
        {
            troopType = TroopTeamMatcher.ResolveTroopType(team, available);
            if (team == null || pool == null || pool.Count == 0)
                return new List<Person>();

            List<Person> picked = TroopTeamMatcher.Match(team, pool, troopType);
            int need = team.RequiredMemberCount;
            if (need > 0 && picked.Count < need)
                return new List<Person>();          // 身份成员没凑齐 → 不算用得上
            return picked;
        }

        /// <summary>
        /// 给 AI 出征用：在"本次允许的兵种"范围内，挑一支**能立刻组起来**（身份凑齐）的推荐队伍。
        /// 按 我的队伍 → 推荐模板、priority 降序尝试；圈层不匹配的跳过。
        /// </summary>
        /// <param name="city">目标城</param>
        /// <param name="available">本次允许组建的兵种（AI 已按资源/器械规则筛过）</param>
        /// <param name="ring">目标城圈层（0 = 前线）</param>
        /// <param name="result">输出：命中的候选（含 members 与 troopType）</param>
        public static bool TryPickForAI(City city, List<TroopType> available, int ring,
            out TroopTeamCandidate result)
        {
            result = null;
            if (city == null)
                return false;

            List<TroopTeamRef> all = AllTeamsSorted();
            for (int i = 0; i < all.Count; i++)
            {
                TroopTeam team = all[i].team;
                if (team == null || !RingMatches(team, ring))
                    continue;

                TroopTeamCandidate c = Evaluate(team, all[i].custom, city, available);
                if (!c.IsReady || c.members == null || c.members.Count == 0)
                    continue;
                // 队伍指定了推荐兵种，但本次不允许造 → 跳过（换成别的队伍，别硬凑）
                if (team.HasRecommendedTroopType && c.troopType == null)
                    continue;

                result = c;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 给 <c>ForceAI.CounsellorRecommendMakeTroop</c> 用：在给定候选里按推荐队伍挑人。
        /// 只认"身份凑齐"的队伍；队伍指定了兵种且与 <paramref name="troopType"/> 不一致时跳过。
        /// </summary>
        /// <param name="candidates">候选（通常就是本城 freePersons）</param>
        /// <param name="troopType">调用方已选定的兵种（可空）</param>
        /// <param name="maxMembers">最多取几人（≤0 = 不限）</param>
        /// <param name="city">目标城（**传了就做"最低兵力"门槛判定**；null = 不判，调用方没有城信息时用）</param>
        /// <returns>成员列表；null = 没有可用队伍（调用方回落原逻辑）</returns>
        public static List<Person> TryPickMembersForTroop(List<Person> candidates, TroopType troopType, int maxMembers,
            City city = null)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            List<TroopTeamRef> all = AllTeamsSorted();
            for (int i = 0; i < all.Count; i++)
            {
                TroopTeam team = all[i].team;
                if (team == null)
                    continue;
                if (maxMembers > 0 && team.RequiredMemberCount > maxMembers)
                    continue;
                if (troopType != null && team.HasRecommendedTroopType && team.troopTypeId != troopType.Id)
                    continue;                                       // 兵种不匹配，换下一支

                // 最低兵力门槛：本城拿不出该兵种要求的兵力 → 跳过这支队伍
                if (city != null)
                {
                    TroopType gateType = troopType;
                    if (gateType == null && team.HasRecommendedTroopType)
                        gateType = IdRef.Resolve<TroopType>(team.troopTypeId);
                    if (!TroopEnough(city, gateType, team.EffectiveMinTroops))
                        continue;
                }

                List<Person> picked = TroopTeamMatcher.Match(team, candidates, troopType);
                int need = team.RequiredMemberCount;
                if (picked.Count == 0)
                    continue;
                if (need > 0 && picked.Count < need)
                    continue;                                       // 身份没凑齐

                if (maxMembers > 0 && picked.Count > maxMembers)
                    picked.RemoveRange(maxMembers, picked.Count - maxMembers);
                return picked;
            }

            return null;
        }

        /// <summary>
        /// 把"当前编队"采集为一条队伍定义（玩家点"新增 / 保存队伍"用）。
        /// 固定武将 = 当前三人；兵种 = 当前选中兵种。
        /// </summary>
        /// <param name="members">当前编队（索引 0 = 主将）</param>
        /// <param name="troopType">当前兵种（可空 = 不限）</param>
        /// <param name="name">队伍名（空 = 自动取名）</param>
        public static TroopTeam CaptureFrom(List<Person> members, TroopType troopType, string name)
        {
            TroopTeam team = new TroopTeam();
            team.name = string.IsNullOrEmpty(name) ? CustomTroopTeams.SuggestName() : name;
            team.troopTypeId = troopType != null ? troopType.Id : -1;

            if (members != null && members.Count > 0)
            {
                int n = Math.Min(TroopTeam.MaxMembers, members.Count);
                int[] ids = new int[n];
                for (int i = 0; i < n; i++)
                    ids[i] = members[i] != null ? members[i].Id : 0;
                team.memberPersonIds = ids;
            }

            team.ring = -1;          // 玩家自己的队伍不限圈层
            team.priority = 100;     // 自建队伍优先于推荐模板
            return team;
        }
    }

    /// <summary>
    /// "推荐队伍要人"的需求快照：把队伍成员与特技持有者摊平成 id 集合，供人才调度层 O(1) 查询。
    ///
    /// 为什么需要它：调度打分是"每个岗位 × 每个候选"级别的调用，
    /// 每次都去遍历 + 排序队伍表太浪费；求解开始时建一次即可。
    /// </summary>
    public class TroopTeamDemand
    {
        /// <summary>不限圈层（队伍 ring &lt; 0）的队伍成员：任何城都需要</summary>
        public readonly HashSet<int> anyCityMembers = new HashSet<int>();
        /// <summary>前线 / 次前线（队伍 ring = 0 / 1）的队伍成员：只有前线城需要</summary>
        public readonly HashSet<int> frontCityMembers = new HashSet<int>();

        /// <summary>
        /// 建立需求快照。
        /// </summary>
        /// <param name="candidates">本次可能被调动的人（调度层的可调动池；用于解析"特技持有者"）</param>
        public static TroopTeamDemand Build(List<Person> candidates)
        {
            TroopTeamDemand demand = new TroopTeamDemand();
            List<TroopTeamRef> teams = TroopTeamService.AllTeamsSorted();
            for (int i = 0; i < teams.Count; i++)
            {
                TroopTeam team = teams[i].team;
                if (team == null)
                    continue;

                HashSet<int> set = team.ring < 0 ? demand.anyCityMembers : demand.frontCityMembers;

                int[] fixedIds = team.FixedMemberIds;
                for (int k = 0; k < fixedIds.Length; k++)
                {
                    if (fixedIds[k] > 0) set.Add(fixedIds[k]);
                }

                if (team.HasFixedFeatures && candidates != null)
                {
                    int[] feats = team.FixedFeatureIds;
                    for (int c = 0; c < candidates.Count; c++)
                    {
                        Person p = candidates[c];
                        if (p == null) continue;
                        if (TroopTeamMatcher.HoldsAnyFeature(p, feats))
                            set.Add(p.Id);
                    }
                }
            }
            return demand;
        }

        /// <summary>
        /// 该候选去这个圈层的城时的加成（0 = 不是任何队伍想要的人）。
        /// </summary>
        /// <param name="p">候选武将</param>
        /// <param name="cityRing">目标城圈层（0 = 前线，1 = 次前线）</param>
        /// <param name="bonus">单次加分</param>
        public float BonusFor(Person p, int cityRing, float bonus)
        {
            if (p == null || bonus <= 0f)
                return 0f;

            float total = 0f;
            if (anyCityMembers.Contains(p.Id))
                total += bonus;
            if (cityRing <= 1 && frontCityMembers.Contains(p.Id))
                total += bonus;
            return total;
        }
    }
}
