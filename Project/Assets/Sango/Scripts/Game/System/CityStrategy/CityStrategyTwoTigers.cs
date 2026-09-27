using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 二虎竞食计略指令：窗口上是"计略对象一/二"两个独立目标框与各自的选取按钮，
    /// 第二个框的候选只保留与第一个框所选势力领土接壤的势力（只有领土相邻才存在"挑拨关系"这说），
    /// 再选执行使者，最后由 CityStrategyManager 把使者派往往第一个目标势力的治所。
    /// 成功与否在使者抵达时结算，本指令只负责把选择结果交给派遣流程。
    /// 进入指令即常驻窗口：两框都选定后军师举荐框才弹出，点确认才把按智力最优的推荐人选落为默认使者，
    /// 使者可另点按钮换人，使者与两框齐备后"决定"才可点。
    /// </summary>
    [GameSystem]
    public class CityStrategyTwoTigers : CityStrategySystemBase
    {
        /// <summary>
        /// 玩家按点击顺序选定的两个被挑拨势力，第一方同时提供出使目的地
        /// </summary>
        List<Force> targetForces = new List<Force>();

        public CityStrategyTwoTigers()
        {
            customTitleName = "二虎竞食";
            customMenuName = "计略/二虎竞食";
            // 计略整组要排在"外交"（3000~3900）之后、"君主"（4000~4500）之前，故取两者中间的 3950；
            // 父级"计略"按子级最小值参与排序，整组位置由本行决定（原值 400 与"征兵"同值，会挤在内政区里）
            customMenuOrder = 3950;
            // 二虎竞食独占一个窗口 prefab：比送礼窗口多一行"计略对象二"，不影响流言窗口的布局
            windowName = "window_city_strategy_two_tigers";
        }

        /// <summary>
        /// 本指令对应的计略类型
        /// </summary>
        protected override CityStrategyType StrategyType => CityStrategyType.TwoTigers;

        /// <summary>
        /// 本指令对应的城市工作类型，消耗从 JobTypes.json 的 25 号行读取
        /// </summary>
        protected override CityJobType StrategyJob => CityJobType.TwoTigers;

        /// <summary>
        /// 二虎竞食要挑拨两个势力，所以窗口给两个目标框
        /// </summary>
        public override int TargetSlotCount => 2;

        /// <summary>
        /// 目标行标题：按序号区分第一方与第二方
        /// </summary>
        /// <param name="slot">目标框序号，0 或 1</param>
        /// <returns>字段标题</returns>
        public override string GetTargetTitle(int slot)
        {
            return slot <= 0 ? "对象势力一" : "对象势力二";
        }

        /// <summary>
        /// 目标行：该框已选的势力名，未选时留白
        /// </summary>
        /// <param name="slot">目标框序号，0 或 1</param>
        /// <returns>势力名</returns>
        public override string GetTargetDescription(int slot)
        {
            if (targetForces.Count <= slot)
                return "";
            Force force = targetForces[slot];
            return force != null ? force.Name : "";
        }

        /// <summary>
        /// 第二个目标框必须先选定第一个势力才有"与谁相邻"的参照，因此按已选数量决定可否点
        /// </summary>
        /// <param name="slot">目标框序号，0 或 1</param>
        /// <returns>该框此刻可选返回 true</returns>
        public override bool CanSelectTargetSlot(int slot)
        {
            return slot <= 0 || targetForces.Count >= 1;
        }

        /// <summary>
        /// 关系行：显示两个被挑拨势力之间的交情。
        /// 二虎竞食的成败只看这两方原本的关系，发起方与它们各自的交情并非结算对象，
        /// 所以这里不能沿用"本势力↔目标方"的口径，否则玩家会拿错数字判断成功率
        /// </summary>
        public override string RelationshipDescription
        {
            get
            {
                if (targetForces.Count < 2)
                    return "";
                return Scenario.Cur.GetRelation(targetForces[0], targetForces[1]).ToString();
            }
        }

        /// <summary>
        /// 抵达日数：两个对象都选定后，按第一个势力的君主所在城（`Force.CapitalCity` 即君主治所）
        /// 到出发城的行军日数折算；未选齐时返回 0 让窗口留白，避免把半程距离当成结论
        /// </summary>
        public override int EnvoyDistanceDays
        {
            get
            {
                City destination = DestinationCity;
                if (destination == null || TargetCity == null)
                    return 0;
                return ToDays(TargetCity.Distance(destination));
            }
        }

        /// <summary>
        /// 两个目标势力都选定才算就绪
        /// </summary>
        public override bool TargetReady => targetForces.Count >= 2;

        /// <summary>
        /// 打开第 slot 个对象势力的选择器：0 号框选第一个被挑拨对象，1 号框只列与 0 号框所选势力接壤的势力。
        /// 两个框各开一次单选选择器、互不嵌套——在选择器的确认回调里直接压入下一个选择器，
        /// 会被前一个选择器的关窗动作把新窗口一起关掉
        /// </summary>
        /// <param name="slot">目标框序号，0 或 1</param>
        public override void OpenTargetSelector(int slot)
        {
            if (slot <= 0)
                OpenFirstForceSelector();
            else
                OpenSecondForceSelector();
        }

        /// <summary>
        /// 第一框：从所有第三方可出使势力里选第一个被挑拨对象。
        /// 重选第一框会连第二框一起清掉，因为第二框的候选是按第一框过滤出来的，换人后旧结果不再成立
        /// </summary>
        void OpenFirstForceSelector()
        {
            targetForces.Clear();
            List<Force> candidates = BuildTargetForces();
            if (candidates.Count < 2)
                return;

            GameSystem.GetSystem<ForceSelectSystem>().Start(
                candidates, new List<Force>(), 1, OnFirstForceSelected, ForceSortTitles(), "对象势力一");
        }

        /// <summary>
        /// 第二框：只能选与第一个被挑拨对象领土相邻的势力
        /// </summary>
        void OpenSecondForceSelector()
        {
            Force first = targetForces.Count > 0 ? targetForces[0] : null;
            // 第一框还没选就没有"与谁相邻"的参照；窗口此刻已把本按钮置灰，这里只做防御性返回
            if (first == null)
                return;

            List<Force> candidates = BuildNeighborTargetForces(first);
            if (candidates.Count <= 0)
                return;

            GameSystem.GetSystem<ForceSelectSystem>().Start(
                candidates, new List<Force>(), 1, OnSecondForceSelected, ForceSortTitles(), "对象势力二");
        }

        /// <summary>
        /// 存在可配对的相邻势力对，用于菜单项灰显判定
        /// </summary>
        /// <returns>满足返回 true</returns>
        protected override bool HasEnoughTargets()
        {
            return HasAdjacentTargetPair();
        }

        /// <summary>
        /// 第一步选择回调：记录第一个被挑拨势力并交给基类收尾。
        /// 此刻只选了一个势力，`TargetReady` 还不成立，基类只会刷新窗口、不会弹举荐框
        /// </summary>
        /// <param name="forces">选中的势力，单选时长度为 1</param>
        void OnFirstForceSelected(List<Force> forces)
        {
            if (forces == null || forces.Count <= 0)
                return;

            targetForces.Clear();
            targetForces.Add(forces[0]);
            OnTargetsChanged();
        }

        /// <summary>
        /// 第二框选择回调：写入第二个被挑拨势力并交给基类收尾，两框选齐后军师举荐框才弹出。
        /// 玩家可以在选满之后再点第二框换人，所以这里是覆盖而不是追加
        /// </summary>
        /// <param name="forces">选中的势力，单选时长度为 1</param>
        void OnSecondForceSelected(List<Force> forces)
        {
            if (forces == null || forces.Count <= 0 || targetForces.Count <= 0)
                return;

            if (targetForces.Count >= 2)
                targetForces[1] = forces[0];
            else
                targetForces.Add(forces[0]);
            OnTargetsChanged();
        }

        /// <summary>
        /// 进入指令时清掉上一次的目标选择，避免复用窗口实例时残留
        /// </summary>
        public override void OnEnter()
        {
            targetForces.Clear();
            base.OnEnter();
        }

        /// <summary>
        /// 出使目的地：第一方治所，第一方治所失效时退到第二方
        /// </summary>
        City DestinationCity
        {
            get
            {
                if (targetForces.Count < 2)
                    return null;
                return targetForces[0].CapitalCity ?? targetForces[1].CapitalCity;
            }
        }

        /// <summary>
        /// 拼出本次二虎竞食行为：目的地取先选一方的治所，该方治所失效时退到另一方
        /// </summary>
        /// <param name="selectedEnvoy">使者武将</param>
        /// <returns>计略行为；两个目标势力都没有治所时返回 null</returns>
        protected override CityStrategyActionBase BuildAction(Person selectedEnvoy)
        {
            Force targetForceA = targetForces[0];
            Force targetForceB = targetForces[1];
            City destination = DestinationCity;
            if (destination == null)
                return null;

            return GameSystem.GetSystem<CityStrategyManager>().CreateAction(
                StrategyType, TargetCity.mBelongForce, TargetCity, selectedEnvoy, destination, targetForceA, targetForceB);
        }

        /// <summary>
        /// 生成第一步的势力候选：存活、不是本势力、且主公驻有一座治所（否则使者无处可去）
        /// </summary>
        /// <returns>候选势力列表</returns>
        List<Force> BuildTargetForces()
        {
            List<Force> candidates = new List<Force>();
            if (TargetCity == null)
                return candidates;

            Force sender = TargetCity.mBelongForce;
            Scenario.Cur.forceSet.ForEach((System.Action<Force>)(force =>
            {
                if (force == null || force == sender)
                    return;
                if (!force.IsAlive)
                    return;
                // CapitalCity 即主公所在城，为空表示势力已无治所，使者没有目的地
                if (force.CapitalCity == null)
                    return;
                candidates.Add(force);
            }));
            return candidates;
        }

        /// <summary>
        /// 生成第二步的势力候选：在第一步候选基础上再过滤一层，只留与已选势力接壤的对象
        /// </summary>
        /// <param name="first">第一步已选定的被挑拨势力</param>
        /// <returns>可与之配对的相邻势力列表</returns>
        List<Force> BuildNeighborTargetForces(Force first)
        {
            List<Force> candidates = new List<Force>();
            List<Force> all = BuildTargetForces();
            for (int i = 0; i < all.Count; i++)
            {
                Force force = all[i];
                if (force == first)
                    continue;
                if (IsEligibleTarget(first, force))
                    candidates.Add(force);
            }
            return candidates;
        }

        /// <summary>
        /// 候选列表里是否至少存在一对领土相邻的势力，即本计略是否还有可行组合
        /// </summary>
        /// <returns>存在可行组合返回 true</returns>
        bool HasAdjacentTargetPair()
        {
            List<Force> all = BuildTargetForces();
            bool found = false;
            for (int i = 0; i < all.Count && !found; i++)
            {
                for (int j = i + 1; j < all.Count; j++)
                {
                    if (IsEligibleTarget(all[i], all[j]))
                    {
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>
        /// 两个势力是否领土相邻：只要甲的任意一座据点与乙的任意一座据点直接相连即成立。
        /// 不读 Force.NeighborForceList——那份缓存只在势力自身回合开始时刷新，会让菜单灰显与实际可选列表差一回合；
        /// 也不读 Force.CityList，全图据点数量很小，现扫一遍 citySet 代价可忽略且永远与当前归属一致
        /// </summary>
        /// <param name="forceA">被挑拨方一</param>
        /// <param name="forceB">被挑拨方二</param>
        /// <returns>两方接壤返回 true</returns>
        bool IsEligibleTarget(Force forceA, Force forceB)
        {
            if (forceA == null || forceB == null || forceA == forceB)
                return false;

            bool neighbor = false;
            Scenario.Cur.citySet.ForEach((System.Action<City>)(city =>
            {
                if (neighbor || !city.IsAlive || city.mBelongForce != forceA)
                    return;
                SangoObjectList<City> linkList = city.NeighborList;
                for (int i = 0; i < linkList.Count; i++)
                {
                    City link = linkList[i];
                    if (link != null && link.IsAlive && link.mBelongForce == forceB)
                    {
                        neighbor = true;
                        return;
                    }
                }
            }));
            return neighbor;
        }

        /// <summary>
        /// 势力选择器的列头，附带按到本城的行军天数排序，便于就近挑拨
        /// </summary>
        /// <returns>列头配置</returns>
        List<ObjectSortTitle> ForceSortTitles()
        {
            return new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
                ForceSortFunction.GetSortByDistanceDay(TargetCity),
            };
        }
    }
}
