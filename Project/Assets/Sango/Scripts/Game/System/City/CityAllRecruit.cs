using System.Collections.Generic;
using System.Text;

namespace Sango.Core.Player
{
    /// <summary>
    /// 一键登庸：批量调用原有登庸逻辑。
    /// 核心不变：成功率（RecruitPersonProbability）、军师推荐算法、行动力消耗、
    /// 登庸结果处理（JobRecruitPerson）全部沿用原逻辑，
    /// 只把“单目标单选执行武将”合并为“多目标 + 自动逐一推荐执行武将”。
    ///
    /// 关键约定：
    ///  · target[i] 与 personList[i] 索引一一对应（一个目标对应一个执行武将）；
    ///  · 执行武将不可重复，后续目标推荐时排除已占用的执行武将；
    ///  · 无执行武将的目标保留在列表里（personList[i] == null），不静默删除；
    ///  · 执行前统一校验行动力，避免执行到一半行动力不足；
    ///  · 执行阶段只按既定对应关系循环调用 JobRecruitPerson，不再重新推荐。
    /// </summary>
    [GameSystem]
    public class CityAllRecruit : CityBaseSystem
    {
        public string customTargetTitleName;
        public List<ObjectSortTitle> customTargetTitleList;

        /// <summary>候选目标武将（与 CityRecruit 相同口径：敌方在野/除君主俘虏外的武将/本城在野与俘虏）</summary>
        public List<Person> targetList = new List<Person>();

        /// <summary>玩家选中的目标武将（索引与 personList 一一对应）</summary>
        public List<Person> target = new List<Person>();

        /// <summary>与 target 索引一一对应的执行武将；null 表示该目标没有可用执行武将</summary>
        public new List<Person> personList = new List<Person>();

        /// <summary>已被前面目标占用的执行武将（推荐时排除）</summary>
        readonly List<Person> usedActionPersons = new List<Person>();

        /// <summary>是否存在无执行武将的目标</summary>
        public bool hasUnavailable;

        /// <summary>
        /// 供选择目标武将的列表界面（UIObjectSelector）查询“目标是否已被我方派出登庸”的静态委托。
        /// 仅本系统从 OnEnter 到 OnDestroy 期间注册，避免影响配属等其他选择场景。
        /// </summary>
        public static System.Func<Person, bool> IsTargetDispatchedQuery;

        public CityAllRecruit()
        {
            customTargetTitleName = "登庸";
            customTitleName = "一键登庸";
            customMenuName = "人事/一键登庸";
            customMenuOrder = 2500;
            windowName = "window_city_allrecruit";
        }

        protected override bool MenuCanShow()
        {
            return TargetCity.IsCityBase();
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 &&
                    TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.RecruitPerson);
            }
        }

        /// <summary>单次登庸行动力消耗（沿用原有 JobType 配置）</summary>
        public int GetJobAP()
        {
            return JobType.GetJobCostAP((int)CityJobType.RecruitPerson);
        }

        public override void OnEnter()
        {
            personList.Clear();
            target.Clear();
            usedActionPersons.Clear();
            hasUnavailable = false;

            customTargetTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByBelongForce,
                PersonSortFunction.SortByState,
                PersonSortFunction.SortByLoyalty,
                PersonSortFunction.GetSortByDistanceDay(TargetCity),
                PersonSortFunction.SortByBelongCity,
                PersonSortFunction.SortByCurrentCity,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
            };

            targetList.Clear();
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (!x.IsSameForce(TargetCity))
                {
                    x.allPersons.ForEach(y =>
                    {
                        if (y.state != (int)PersonStateType.Governor && y.state != (int)PersonStateType.Prisoner)
                            targetList.Add(y);
                    });
                    x.wildPersons.ForEach(y => { targetList.Add(y); });
                }
                else
                {
                    x.wildPersons.ForEach(y => { targetList.Add(y); });
                    x.captiveList.ForEach(y => { targetList.Add(y); });
                }
            });
            targetList.Sort((a, b) =>
            {
                if (a.state == b.state)
                {
                    if (a.loyalty == b.loyalty)
                    {
                        return a.DistanceDays(TargetCity).CompareTo(b.DistanceDays(TargetCity));
                    }
                    else
                    {
                        return a.loyalty.CompareTo(b.loyalty);
                    }
                }
                else
                {
                    return -a.state.CompareTo(b.state);
                }
            });

            Window.Instance.Open(windowName);

            // 注册“是否已派出登庸”查询委托，供选择目标武将的列表界面显示圈圈标记
            IsTargetDispatchedQuery = p => IsTargetDispatched(p);
        }

        public override void OnDestroy()
        {
            IsTargetDispatchedQuery = null;
            // 销毁时清空选择残留：任何方式退出（返回/执行/切换系统）后再次进入都是一张白纸
            ClearSelection();
            Window.Instance.Close(windowName);
        }

        /// <summary>
        /// 被选择武将窗口弹回（Back）时，确保一键登庸窗口重新可见。
        /// 框架 GameSystem.OnBack 默认为空，原登庸窗口靠“从未被关闭”自然露出；
        /// 此处显式兜底，避免选择窗口关闭后本窗口仍处于隐藏状态。
        /// </summary>
        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
            Window.WindowInterface win = Window.Instance.GetWindow(windowName);
            if (win != null && !win.IsVisible())
                win.Open();
        }

        /// <summary>
        /// 清空当前选择（退出界面 / 挂起恢复时调用，避免上次的 target 残留导致重复登庸）。
        /// </summary>
        public void ClearSelection()
        {
            target.Clear();
            personList.Clear();
            usedActionPersons.Clear();
            hasUnavailable = false;
        }

        /// <summary>
        /// 目标选择发生变化后，重新建立“目标 → 执行武将”对应关系。
        /// 每个目标单独按原军师推荐公式 RecruitPersonProbability 选最优执行武将；
        /// 已分配的执行武将不再参与后续目标推荐（执行武将不可重复）；
        /// 没有可用执行武将的目标保留在列表中（personList[i] == null）。
        /// </summary>
        public void SetTargets(List<Person> targets)
        {
            target.Clear();
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    Person p = targets[i];
                    if (p != null && !target.Contains(p))
                        target.Add(p);
                }
            }

            personList.Clear();
            usedActionPersons.Clear();
            hasUnavailable = false;

            for (int i = 0; i < target.Count; i++)
            {
                Person action = RecommendActionPerson(target[i]);
                personList.Add(action);
                if (action != null)
                {
                    usedActionPersons.Add(action);
                }
                else
                {
                    hasUnavailable = true;
                }
            }
        }

        /// <summary>
        /// 军师推荐：遍历 TargetCity.freePersons，排除已占用的执行武将，
        /// 按 RecruitPersonProbability 选择概率最高且 &gt; 0 者（与原版“无合适人选”口径一致：
        /// 概率为 0 表示登庸必定失败，不作为推荐人选）；一个都没有则返回 null。
        /// </summary>
        Person RecommendActionPerson(Person dest)
        {
            Person best = null;
            int bestProbability = 0;
            for (int i = 0; i < TargetCity.freePersons.Count; i++)
            {
                Person candidate = TargetCity.freePersons[i];
                if (usedActionPersons.Contains(candidate))
                    continue;

                int probability = GameFormula.Instance.RecruitPersonProbability(candidate, dest, 0);
                if (probability > 0 && probability > bestProbability)
                {
                    bestProbability = probability;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>总行动力消耗 = 单次登庸消耗 × 有执行武将的目标数</summary>
        public int GetTotalAP()
        {
            int count = 0;
            for (int i = 0; i < personList.Count; i++)
            {
                if (personList[i] != null)
                    count++;
            }
            return count * GetJobAP();
        }

        /// <summary>
        /// 一键登庸按钮是否可点击：
        /// 已选择目标 && 每个目标都有执行武将 && 行动力足够。
        /// </summary>
        public bool CanExecute()
        {
            if (target.Count <= 0 || hasUnavailable)
                return false;
            return TargetCity.BelongCorps.ActionPoint >= GetTotalAP();
        }

        /// <summary>
        /// 本军团中正在外执行登庸任务的武将（异城登庸派发后 missionType = PersonRecruitPerson，
        /// 执行武将离开本城去目标武将所在城登庸）。这些武将已不在 freePersons 里，
        /// 系统推荐不会重复选他们；UI 显示出来，让玩家知道“谁已经派出去了”，
        /// 避免误以为执行武将不足或重复操作。
        /// </summary>
        public List<Person> GetDispatchedRecruitingPersons()
        {
            List<Person> result = new List<Person>();
            if (TargetCity == null || TargetCity.BelongForce == null)
                return result;
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x.BelongForce != TargetCity.BelongForce)
                    return;
                x.allPersons.ForEach(p =>
                {
                    if (p != null && p.missionType == (int)MissionType.PersonRecruitPerson)
                        result.Add(p);
                });
            });
            return result;
        }

        /// <summary>
        /// 目标武将是否已经被我方派出登庸（已有执行武将的登庸任务指向该武将）。
        /// 在途的执行武将会去登庸该目标，玩家再选它会造成重复派发/行动力浪费。
        /// </summary>
        public bool IsTargetDispatched(Person dest)
        {
            if (dest == null || TargetCity == null || TargetCity.BelongForce == null)
                return false;
            bool found = false;
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x.BelongForce != TargetCity.BelongForce)
                    return;
                x.allPersons.ForEach(p =>
                {
                    if (!found && p != null && p.missionType == (int)MissionType.PersonRecruitPerson
                        && p.missionTarget == dest.Id)
                        found = true;
                });
            });
            return found;
        }

        /// <summary>
        /// 真正执行：按既定 target[i] ↔ personList[i] 对应关系，
        /// 依次调用原有 TargetCity.JobRecruitPerson(personList[i], target[i])，
        /// 不重新推荐、不改变原有登庸规则。
        /// 执行前统一校验行动力，任何目标行动力不足都不开始执行。
        /// </summary>
        public override void DoJob()
        {
            if (target.Count <= 0)
            {
                Done();
                return;
            }

            // ① 统一行动力检查（不执行任何登庸，避免半执行状态）
            int totalAP = GetTotalAP();
            if (TargetCity.BelongCorps.ActionPoint < totalAP)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay,
                    $"行动力不足，需要 {totalAP} 点行动力，当前只有 {TargetCity.BelongCorps.ActionPoint} 点。",
                    () => { }, TargetCity.BelongForce.mCounsellor);
                return;
            }

            // ② 存在无执行武将的目标 → 不执行
            if (hasUnavailable)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay,
                    "有目标武将没有可用执行武将，无法一键登庸。",
                    () => { }, TargetCity.BelongForce.mCounsellor);
                return;
            }

            // ③ 先关闭一键登庸窗口（界面立即消失；只 Close 窗口、不 Done 系统——
            //    Done() 会把 currentSystem 整个收尾，系统结束后再执行 JobRecruitPerson
            //    登庸不生效、后续提示也不弹——正是"没效果、消息框消失"的根因）。
            Window.Instance.Close(windowName);

            // ④ 按既定对应关系依次执行原有登庸逻辑（系统仍存活，与原有 DoJob 一致：
            //    先执行登庸，成功后再 Done 收尾）
            List<Person> dispatched = null;
            for (int i = 0; i < target.Count && i < personList.Count; i++)
            {
                Person action = personList[i];
                Person dest = target[i];
                if (action == null)
                    continue;

                // 返回 false 表示异城登庸（派发登庸任务），与原有 DoJob 的判定一致
                if (!TargetCity.JobRecruitPerson(action, dest))
                {
                    if (dispatched == null)
                        dispatched = new List<Person>();
                    dispatched.Add(dest);
                }
            }

            // ⑤ 登庸执行完毕后再收尾系统（触发 Done 链：OnDone→OnDestroy→窗口关闭等）
            Done();

            // ⑤ 若有异城登庸（派发任务）再弹提示；窗口已关，提示不再阻塞界面关闭。
            if (dispatched != null && dispatched.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < dispatched.Count; i++)
                {
                    if (i > 0)
                        sb.Append("、");
                    sb.Append(dispatched[i].ColorName);
                }
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay,
                    $"以下武将的登庸任务已派发：\n{sb}",
                    () => { }, TargetCity.BelongForce.mCounsellor);
            }

            // ⑥ 执行完成，清空已选目标/执行武将。
            //    防止窗口未完全关闭时再次打开选择界面出现残留预选，导致目标被重复登庸。
            target.Clear();
            personList.Clear();
            usedActionPersons.Clear();
            hasUnavailable = false;
        }
    }
}
