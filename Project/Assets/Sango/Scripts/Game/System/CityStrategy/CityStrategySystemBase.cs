using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 城市计略指令基类（常驻窗口 + 通用对象选择器）。
    /// 职责：承载计略通用的交互外壳——开窗展示"目标 / 与目标方关系 / 抵达日数 / 资金 / 行动力 / 使者五维"，
    /// 把"选目标""选使者"两件事委托给通用对象选择器，并在玩家点"决定"时拼出行为交给 CityStrategyManager 派遣。
    /// 子类只负责三件事：本计略的目标语义（几个框、每个框选什么）、窗口文案的取法、将行为参数交给工厂。
    /// 每个计略用自己的窗口 prefab（流言 window_city_strategy_rumor、二虎竞食 window_city_strategy_two_tigers），
    /// 互不共用也不与其他界面共用，这样二虎多出的第二目标行不会改变流言窗口的布局；
    /// 三者共用同一个 UICityStrategy 脚本类，第二框在流言 prefab 里根本不存在，字段为空即自然跳过。
    /// 使者沿用 CityBaseSystem.personList 的第 0 位（与送礼一致），不另设字段。
    /// </summary>
    public abstract class CityStrategySystemBase : CityBaseSystem
    {
        /// <summary>
        /// 本次计略类型
        /// </summary>
        protected abstract CityStrategyType StrategyType { get; }

        /// <summary>
        /// 本次计略对应的城市工作类型，用于读取金钱与行动力消耗
        /// </summary>
        protected abstract CityJobType StrategyJob { get; }

        /// <summary>
        /// 目标框数量：流言 1 个（一座城池），二虎竞食 2 个（两个势力）。
        /// 窗口按这个数量决定要显示几组"目标行 + 选择按钮"，第二组在没有被用到的计略里整组隐藏
        /// </summary>
        public abstract int TargetSlotCount { get; }

        /// <summary>
        /// 第 slot 个目标框的标题：流言是"目标城池"，二虎竞食是"对象势力一/二"
        /// </summary>
        /// <param name="slot">目标框序号，从 0 开始</param>
        /// <returns>字段标题</returns>
        public abstract string GetTargetTitle(int slot);

        /// <summary>
        /// 第 slot 个目标框已选中的对象名；未选时返回空串让窗口留白
        /// </summary>
        /// <param name="slot">目标框序号，从 0 开始</param>
        /// <returns>对象显示名</returns>
        public abstract string GetTargetDescription(int slot);

        /// <summary>
        /// 第 slot 个目标框此刻是否可选。
        /// 二虎竞食的第二框必须先选定第一框（否则没有"与谁相邻"的参照），此时按钮置灰而不是隐藏
        /// </summary>
        /// <param name="slot">目标框序号，从 0 开始</param>
        /// <returns>可以打开该框的选择器返回 true</returns>
        public abstract bool CanSelectTargetSlot(int slot);

        /// <summary>
        /// 打开第 slot 个目标的选择器，由该目标框的按钮调用。
        /// 各计略自己决定每个框选什么、候选如何过滤，基类不假设目标数量与类型
        /// </summary>
        /// <param name="slot">目标框序号，从 0 开始</param>
        public abstract void OpenTargetSelector(int slot);

        /// <summary>
        /// 关系行文案：本计略真正结算的那两方之间的交情，用于让玩家先看到"这次会得罪谁"。
        /// 流言是本势力与据点所属势力，二虎竞食是两个被挑拨势力之间，口径由子类各自给出
        /// </summary>
        public abstract string RelationshipDescription { get; }

        /// <summary>
        /// 使者抵达目的地所需日数（已按 1 回合 = 10 日折算），目标或使者未定时返回 0
        /// </summary>
        public abstract int EnvoyDistanceDays { get; }

        /// <summary>
        /// 目标是否已经选齐（流言选 1 个据点，二虎竞食选 2 个势力），决定"决定"按钮可否点
        /// </summary>
        public abstract bool TargetReady { get; }

        /// <summary>
        /// 用已选定的使者拼出本次计略行为，不做消耗校验
        /// </summary>
        /// <param name="selectedEnvoy">使者武将</param>
        /// <returns>计略行为，目标组合不成立时返回 null</returns>
        protected abstract CityStrategyActionBase BuildAction(Person selectedEnvoy);

        /// <summary>
        /// 把世界距离（`City.Distance` 返回的邻接步数，一步即一回合行军）折算成窗口上显示的日数。
        /// 1 回合 = 10 日是工程既有口径，运输/征兵/武将移动等窗口都是 `距离 * 10` 日
        /// （`UI/City/UICityTransport.cs:226`、`UI/City/UICityRecruit.cs:52`），
        /// 直接显示步数会让"6"看起来像回合数而不是 60 日
        /// </summary>
        /// <param name="distanceTurn">两城之间的邻接步数</param>
        /// <returns>折算后的行军日数</returns>
        protected static int ToDays(int distanceTurn)
        {
            return distanceTurn * 10;
        }

        /// <summary>
        /// 当前使者：沿用基类的 personList 第 0 位，由军师举荐框确认后落入，或由玩家自己点选
        /// </summary>
        protected Person Envoy => personList.Count > 0 ? personList[0] : null;

        /// <summary>
        /// 本计略的资金消耗，供窗口读取（StrategyJob 保持 protected，不对外暴露枚举本身）
        /// </summary>
        public int JobGoldCost => JobType.GetJobCost((int)StrategyJob);

        /// <summary>
        /// 本计略的行动力消耗，供窗口读取
        /// </summary>
        public int JobActionPointCost => JobType.GetJobCostAP((int)StrategyJob);

        /// <summary>
        /// 菜单项是否可用：出发城有效、本城有空闲武将、行动力与金钱够、且存在足够多的合法目标
        /// </summary>
        public override bool IsValid
        {
            get
            {
                if (TargetCity == null)
                    return false;
                if (BuildEnvoyCandidates().Count <= 0)
                    return false;
                Corps corps = TargetCity.BelongCorps;
                if (corps != null && corps.ActionPoint < JobActionPointCost)
                    return false;
                if (TargetCity.gold < JobGoldCost)
                    return false;
                return HasEnoughTargets();
            }
        }

        /// <summary>
        /// 是否存在足够多的合法目标，用于菜单项灰显判定。
        /// 流言要求至少 1 个可施法据点，二虎竞食要求至少 2 个可挑拨的第三方势力
        /// </summary>
        /// <returns>目标数量满足本计略要求返回 true</returns>
        protected abstract bool HasEnoughTargets();

        /// <summary>
        /// 军师推荐的使者人选：预估成功率达到军师智力这条承诺门槛的候选，按成功率降序。
        /// 既作为举荐框要落的人选（见 `OnTargetsChanged`），也作为选择器"军师推荐"列的判定依据
        /// </summary>
        protected List<Person> counsellorRecommendList = new List<Person>();

        /// <summary>
        /// 每个候选使者对本次目标的预估成功率（百分比）。由 `RefreshCounsellorRecommend` 在目标选齐后重算，
        /// 供"军师推荐"列直接显示数字；没参与试算的候选人不在表里，按 0 计
        /// </summary>
        protected Dictionary<Person, int> envoySuccessRates = new Dictionary<Person, int>();

        /// <summary>
        /// 当前使者是否由军师举荐框落入。玩家自己在选择器里挑过人就是 false：
        /// 换目标后只有"上一次也是举荐落入"才重新弹举荐框，不覆盖玩家自选的人选
        /// </summary>
        bool envoyFromCounsellor;

        /// <summary>
        /// 进入指令时基类会调到这里。城市计略的推荐口径依赖目标（抵抗方智力由目标据点/目标势力决定），
        /// 开窗时玩家还没选目标，算不出成功率，所以这里只复位状态；
        /// 真正的名单在目标选齐后由 `RefreshCounsellorRecommend` 生成，默认使者则等举荐框确认才落入
        /// </summary>
        public override void RecommandPersonList()
        {
            counsellorRecommendList.Clear();
            envoySuccessRates.Clear();
            personList.Clear();
            envoyFromCounsellor = false;
        }

        /// <summary>
        /// 用真实成功率公式给每个候选使者预演一次成算，再按"军师智力即承诺门槛"筛出推荐名单：
        /// 只有预估成功率 ≥ 军师智力的武将才会被推荐，所以智力 100 的军师一开口就是必成，
        /// 智力 90 的军师推荐的人也至少九成把握；军师不在位或无人达门槛时名单为空（宁缺毋滥，不做退让凑数）。
        /// 试算安全：`CityStrategyManager.CreateAction` 只是 new 一个行为对象，
        /// `CalculateSuccessRate` 只读世界状态并写自身字段，不扣消耗也不挂任务（`CityStrategyManager.cs:25`）。
        /// 预估只反映出发时刻，抵达时会按当时局势重算（`CityStrategyManager.cs:162`），本表仅供参考
        /// </summary>
        protected void RefreshCounsellorRecommend()
        {
            counsellorRecommendList.Clear();
            envoySuccessRates.Clear();

            if (!TargetReady || TargetCity == null || TargetCity.BelongForce == null)
                return;

            Person counsellor = TargetCity.BelongForce.mCounsellor;
            if (counsellor == null)
                return;

            List<Person> candidates = BuildEnvoyCandidates();
            for (int i = 0; i < candidates.Count; i++)
            {
                Person candidate = candidates[i];
                CityStrategyActionBase probe = BuildAction(candidate);
                if (probe == null)
                    continue;

                int rate = probe.CalculateSuccessRate();
                envoySuccessRates[candidate] = rate;
                if (rate >= counsellor.Intelligence)
                    counsellorRecommendList.Add(candidate);
            }

            // 把握最大的排最前，举荐框默认取第 0 位
            counsellorRecommendList.Sort((a, b) => GetEnvoySuccessRate(b).CompareTo(GetEnvoySuccessRate(a)));
        }

        /// <summary>
        /// 取某个候选使者对本次目标的预估成功率；未参与试算（目标未定或行为拼不出来）时返回 0
        /// </summary>
        /// <param name="person">候选使者</param>
        /// <returns>预估成功率（百分比）</returns>
        protected int GetEnvoySuccessRate(Person person)
        {
            if (person == null)
                return 0;
            return envoySuccessRates.TryGetValue(person, out int rate) ? rate : 0;
        }

        /// <summary>
        /// 目标选择结果变化后的统一收尾：先按新目标重算推荐名单，刷新窗口让玩家看到刚选定的目标，再决定是否弹举荐框。
        /// 弹框条件：目标已选齐、军师敢给出承诺（名单非空）、且当前使者要么为空、要么就是上一次举荐落入的
        /// （玩家自己在选择器里挑过使者后不再打扰，见 `envoyFromCounsellor`）。
        /// 因此流言换目标城池、二虎换第二个势力都会重新走一遍举荐流程。
        /// 文案与弹框形态对齐探索人才（`UI/City/UICitySearching.cs:23-43`），并额外报出预估成功率
        /// </summary>
        protected void OnTargetsChanged()
        {
            RefreshCounsellorRecommend();
            RefreshWindow();

            if (!TargetReady || counsellorRecommendList.Count <= 0)
                return;
            if (Envoy != null && !envoyFromCounsellor)
                return;

            Person recommended = counsellorRecommendList[0];
            Person counsellor = TargetCity.BelongForce.mCounsellor;
            int rate = GetEnvoySuccessRate(recommended);
            string content = $"最适合担任此任务的人，\n除{recommended.ColorName}之外别无其他人选，预计成功率 {rate}%。";
            // 推荐人选本身就是军师时，语气要改成自荐，否则"除他自己之外别无其他人选"读起来不通
            if (recommended == counsellor)
                content = $"我对此任务很有信心，\n请务必交给我吧，预计成功率 {rate}%。";

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, content, () =>
            {
                // 换目标后重新举荐时 personList 里还留着上一次的使者，必须先清再落，否则第 0 位仍是旧人
                personList.Clear();
                personList.Add(recommended);
                envoyFromCounsellor = true;
                RefreshWindow();
            }, counsellor);
        }

        /// <summary>
        /// 打开使者选择器：候选为本城空闲且存活的武将，并把当前人选作为已勾选项传入，便于玩家换人
        /// </summary>
        public void OpenEnvoySelector()
        {
            List<Person> candidates = BuildEnvoyCandidates();
            if (candidates.Count <= 0)
                return;

            // 用一份副本作为初始勾选集，避免选择器内部清空结果列表时把本指令的人选一起抹掉
            List<Person> preChecked = new List<Person>(personList);
            GameSystem.GetSystem<PersonSelectSystem>().Start(
                candidates, preChecked, 1, OnEnvoySelected, EnvoySortTitles(), customTitleName);
        }

        /// <summary>
        /// 目标或使者选择完成后的统一收尾：刷新常驻窗口，让玩家立刻看到最新组合
        /// </summary>
        protected void RefreshWindow()
        {
            Window.Instance.GetWindow(windowName)?.Refresh();
        }

        /// <summary>
        /// 窗口的"决定"按钮入口：拼行为并派遣
        /// </summary>
        public override void DoJob()
        {
            PerformStrategy();
        }

        /// <summary>
        /// 正式派遣：使者与目标齐备才拼行为，派遣失败要给出原因而不是静默退回
        /// </summary>
        protected void PerformStrategy()
        {
            Person envoy = Envoy;
            if (envoy == null || !TargetReady)
                return;

            CityStrategyActionBase action = BuildAction(envoy);
            if (action == null)
            {
                Back();
                return;
            }

            if (!GameSystem.GetSystem<CityStrategyManager>().Dispatch(action))
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickSay, "时机已失，" + action.GetActionName() + "未能施行!", () =>
                {
                    Back();
                }, null, null);
                return;
            }

            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧, 保证完成任务!!", () =>
            {
                Done();
            }, null, envoy, 5);
        }

        /// <summary>
        /// 使者选择器的确认回调：换人后同步到 personList 并刷新窗口。
        /// 玩家自己挑过人之后置 envoyFromCounsellor = false，此后换目标不再弹举荐框覆盖他的选择
        /// </summary>
        /// <param name="persons">选中的使者，单选时长度为 1</param>
        void OnEnvoySelected(List<Person> persons)
        {
            if (persons == null || persons.Count <= 0)
                return;

            personList = persons;
            envoyFromCounsellor = false;
            RefreshWindow();
        }

        /// <summary>
        /// 生成使者候选列表：只负责筛出本城空闲且存活的武将，不做排序。
        /// 排序统一交给使者选择器的"军师推荐"列（见 `EnvoySortTitles`），因为
        /// `PersonSelectSystem.Start` 打开时会按传入的列头重排一次，在这里排的顺序会被覆盖
        /// </summary>
        /// <returns>本城空闲且存活的武将</returns>
        protected List<Person> BuildEnvoyCandidates()
        {
            List<Person> candidates = new List<Person>();
            if (TargetCity == null)
                return candidates;

            List<Person> freePersons = TargetCity.freePersons;
            for (int i = 0; i < freePersons.Count; i++)
            {
                Person person = freePersons[i];
                if (person != null && person.IsAlive && person.IsFree)
                    candidates.Add(person);
            }
            return candidates;
        }

        /// <summary>
        /// 使者选择器的列头：只给姓名、军师推荐列和智力，政治与计略成败无关，摆出来只会误导玩家。
        /// 军师推荐列直接显示预估成功率数字（如 92%），只给达到军师智力门槛的人显示，其余留白，
        /// 玩家一眼就能看出这次有几个人被军师打包票、各自几成把握。
        /// 选择器打开时默认按第二列排序，所以该列的比较规则就是玩家第一眼看到的顺序：
        /// 被推荐者优先，其内按预估成功率降序，再按智力降序
        /// </summary>
        /// <returns>列头配置</returns>
        protected List<ObjectSortTitle> EnvoySortTitles()
        {
            PersonSortFunction.SortTitle recommendTitle = new PersonSortFunction.SortTitle()
            {
                name = "军师推荐",
                width = 2.00f,
                // 达到门槛才显数字：留白即"军师没替他打包票"，比打叉更能表达"这不是推荐人选"
                valueStrGetCall = person => counsellorRecommendList.Contains(person)
                    ? $"{GetEnvoySuccessRate(person)}%"
                    : "",
                valueObjGet = person => counsellorRecommendList.Contains(person),
                valueObjSet = null,
                // 推荐者优先，其内按预估成功率降序、再按智力降序；不能复用 GetSortByContainsInList，
                // 它的比较器是升序且没有第二键，会把推荐者排到最后
                valueSortFunc = (left, right) =>
                {
                    int recommendedLeft = counsellorRecommendList.Contains(left) ? 1 : 0;
                    int recommendedRight = counsellorRecommendList.Contains(right) ? 1 : 0;
                    if (recommendedLeft != recommendedRight)
                        return recommendedRight.CompareTo(recommendedLeft);

                    int rateCompare = GetEnvoySuccessRate(right).CompareTo(GetEnvoySuccessRate(left));
                    if (rateCompare != 0)
                        return rateCompare;
                    return right.Intelligence.CompareTo(left.Intelligence);
                },
            };

            return new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                recommendTitle,
                PersonSortFunction.SortByIntelligence,
            };
        }
    }
}
