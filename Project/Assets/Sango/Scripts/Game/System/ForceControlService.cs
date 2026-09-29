/*
 * 文件名：ForceControlService.cs
 * 描述：势力控制权（玩家 / 电脑AI）切换服务，"中途参加"与"委托给AI"的唯一落地入口
 * 创建日期：2026-09-29
 * 作者：XBJ
 */

using System.Collections.Generic;
using Sango.Render;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 势力控制权切换服务：在"玩家亲自操作"与"电脑（AI）代管"之间移交一个势力。
    ///
    /// 为什么要有这个类：<see cref="Force.IsPlayer"/> 是全工程唯一的权威控制位
    /// （<c>Corps/Person/Troop/Building.IsPlayer</c> 都只转发它），
    /// 但它**不进存档** —— 序列化载体是 <see cref="ScenarioInfo.playerForceList"/>，
    /// 读档时由 <see cref="Scenario.CheckPlayer"/> 按数组重建 bool。
    /// 所以任何一处"散点直接写 bool"都会造出一个能玩、但一存一读就丢失控制权的假切换。
    /// 本类把切换必须同时完成的四件事收在一起，界面侧只允许调用 <see cref="TrySetPlayerControl"/>：
    ///   1) 写 <see cref="Force.IsPlayer"/>：当回合立刻改变 <see cref="Force.Run"/> 的 AI / 玩家分叉；
    ///      若目标势力**此刻正在行动中**，则不当场翻控制位，而是排队到它自己的下一个回合开始处生效
    ///      （见 <c>Force.pendingPlayerControl</c>），否则会出"同一回合被玩家和 AI 各结算一遍"或
    ///      "AI 半截命令队列被重复执行"；
    ///   2) 同步 <see cref="ScenarioInfo.playerForceList"/>：保证存档与读档一致，无论控制权是否已生效；
    ///   3) 接管时把该势力提到本大回合行动队列的队首（若它还没行动），避免白等一整个大回合；
    ///   4) 委托给AI且该势力正在行动中时，沿用玩家"结束回合"的收尾口径把这一回合交掉
    ///      （见 <see cref="CloseOutCurrentTurn"/>），不会把回合停在"等玩家操作"上。
    ///
    /// HUD 为什么不需要额外事件：<c>UIGame</c> 的"结束回合按钮 / 玩家信息面板"一律跟随
    /// <see cref="Scenario.CurRunForce"/> 在 <c>GameEvent.OnForceTurnStart</c> 里刷新，
    /// 而本类要么在"该势力不是当前行动势力"时当场改控制位（界面本来就不显示它的归属），
    /// 要么排队到它自己回合开始时才改（那次回合开始事件自然会按新归属刷界面），
    /// 两种生效时机都不需要再造一个控制权事件。
    /// </summary>
    public static class ForceControlService
    {
        /// <summary>
        /// 回合推进阻塞令牌。
        ///
        /// 只记录"当前是谁在要求冻结回合推进"，由该命令在进入时登记、离开时注销。
        /// 用令牌而不是计数器：令牌可以被剧本收尾**无条件清空**，
        /// 不会出现"读档时计数没配平 → 新世界永远不推进"的僵尸锁。
        /// </summary>
        static object turnBlockOwner;

        /// <summary>是否正处于"控制权交接界面打开中"，true 时 <see cref="Scenario.Run"/> 不再推进回合。</summary>
        public static bool IsTurnBlocked { get { return turnBlockOwner != null; } }

        /// <summary>
        /// 登记"冻结回合推进"的请求。
        ///
        /// 只影响 <see cref="Scenario.Run"/> 这一层：地图输入、对话框、右键菜单照常工作，
        /// 玩家随时可以取消，不会把自己锁死在菜单里。
        /// </summary>
        /// <param name="owner">发起冻结的对象（一般是当前命令实例），注销时必须传同一个引用</param>
        public static void BlockTurn(object owner)
        {
            if (owner == null)
            {
                Sango.Log.Error("BlockTurn 传入空的持有者,拒绝登记,否则将无法注销并造成永久冻结");
                return;
            }
            turnBlockOwner = owner;
        }

        /// <summary>
        /// 注销"冻结回合推进"的请求。
        /// 只有持有者本人才能注销，避免上一个界面的迟到回调把新界面的冻结误解除。
        /// </summary>
        /// <param name="owner">当初调用 <see cref="BlockTurn"/> 传入的同一个对象</param>
        public static void UnblockTurn(object owner)
        {
            if (owner == null || !ReferenceEquals(owner, turnBlockOwner))
                return;
            turnBlockOwner = null;
        }

        /// <summary>
        /// 强制清除冻结状态。
        /// 由 <see cref="ScenarioLifecycle.BeginShutdown"/> 在收尾时调用：
        /// 读档 / 回主菜单走的是窗口与命令的整体拆除，个别路径不会经过界面侧的注销回调，
        /// 不在这里兜住的话新剧本会一帧都不推进。
        /// </summary>
        public static void ClearTurnBlock()
        {
            turnBlockOwner = null;
        }

        /// <summary>
        /// 当前剧本里"可以被玩家中途接管"的势力：存活 且 目前不由玩家控制。
        /// </summary>
        /// <returns>候选势力列表，按势力 Id 升序；不在剧本中时返回空列表</returns>
        public static List<Force> GetJoinableForces()
        {
            return CollectForces(false);
        }

        /// <summary>
        /// 当前剧本里"可以委托给AI"的势力：存活 且 目前由玩家控制。
        /// </summary>
        /// <returns>可委托给AI的势力列表，按势力 Id 升序；不在剧本中时返回空列表</returns>
        public static List<Force> GetDelegatableForces()
        {
            return CollectForces(true);
        }

        /// <summary>
        /// 按"是否玩家势力"收集候选势力。
        /// 只列 <see cref="Force.IsAlive"/> 的势力：已灭亡势力没有君主所在城，接管进来没有任何可操作对象。
        /// </summary>
        /// <param name="playerControlled">true = 取玩家势力；false = 取非玩家势力</param>
        /// <returns>按 Id 升序的势力列表</returns>
        static List<Force> CollectForces(bool playerControlled)
        {
            List<Force> result = new List<Force>();
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.forceSet == null)
                return result;

            for (int i = 0; i < scenario.forceSet.Count; i++)
            {
                Force force = scenario.forceSet[i];
                if (force == null || !force.IsAlive)
                    continue;
                if (force.IsPlayer != playerControlled)
                    continue;
                result.Add(force);
            }

            // 势力 Id 即剧本数据顺序，稳定排序，避免每次打开列表顺序跳变
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
            return result;
        }

        /// <summary>
        /// 切换一个势力的控制权。中途参加与委托给AI共用这一套主体逻辑，
        /// 界面层只负责选目标和确认，不得另写一份。
        /// </summary>
        /// <param name="force">目标势力，必须属于当前剧本且存活</param>
        /// <param name="playerControlled">true = 交给人（加入游戏）；false = 交给AI（委托）</param>
        /// <returns>真正发生了切换返回 true；目标非法或状态本就一致时返回 false 且不做任何改动</returns>
        public static bool TrySetPlayerControl(Force force, bool playerControlled)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
            {
                Sango.Log.Error("TrySetPlayerControl 在剧本未运行时被调用,已忽略");
                return false;
            }
            if (force == null || !force.IsAlive)
            {
                Sango.Log.Error($"控制权切换失败:目标势力为空或已灭亡(playerControlled={playerControlled})");
                return false;
            }
            // 意图口径：有未生效的移交请求时以它为准，否则以当前控制位为准。
            // 用它做幂等判断，避免"连点两次同向移交"把待生效请求重复覆盖成中间态。
            int intent = force.pendingPlayerControl >= 0 ? force.pendingPlayerControl : (force.IsPlayer ? 1 : 0);
            if (intent == (playerControlled ? 1 : 0))
            {
                // 幂等：状态本就一致（或已在期待同一目标），不改数据也不刷界面
                return false;
            }

            // 存档载体先落到目标状态：数组才是读档时重建控制权的依据（Force.IsPlayer 不入档）。
            // 运行时 bool 什么时候跟上，取决于下面的"生效时机"。
            SyncPlayerForceList(scenario, force.Id, playerControlled);

            if (scenario.CurRunForce == force && !force.ActionOver)
            {
                // 正处于该势力的回合中：不能当场翻控制位，否则会出两类状态破坏 ——
                //   · 交给AI时，Force.Run 立刻转 DoAI，科技/官职/俘虏/计略在玩家已经动过的同一回合再结算一遍；
                //   · 交给玩家时，AI 半截命令队列会残留到下次 AIPrepare 之后被重复执行。
                // 统一延后到它自己的下一个回合开始处（Force.OnForceTurnStart 里 AI 状态本就整体复位）。
                force.pendingPlayerControl = playerControlled ? 1 : 0;

                // 委托方向额外交出本回合：玩家既然已经撒手，就不该再把回合停在"等玩家操作"上。
                // 效果等同于玩家点了"结束回合"（同一套收尾主体，见 CloseOutCurrentTurn），
                // 差别只有一处：本回合里尚未行动的部队不再由 AI 补跑，从下一回合起完全交给AI。
                if (!playerControlled)
                    CloseOutCurrentTurn(scenario, force);

                Sango.Log.Info($"控制权移交排队:{force.Name} → {(playerControlled ? "玩家" : "AI")}，" +
                               $"该势力本回合正在进行中，下个回合开始时生效；当前玩家势力数 {CountPlayerForces(scenario)}");
                return true;
            }

            // 该势力本回合还没轮到（或已行动完）：干净状态，当场生效。
            // 此时 CurRunForce 一定不是它，所以"结束回合按钮 / 玩家信息面板"这些跟随当前行动势力的
            // 界面控件不需要额外刷一遍 —— 等轮到它时 OnForceTurnStart 会按新归属自己刷新。
            force.IsPlayer = playerControlled;

            // 接管方提到本大回合行动队列队首，避免白等一整个大回合；
            // 已行动过的势力不在队列里，PromoteForceToRunQueueHead 自己会返回 false，
            // 它会在下一次 MakeForceQuene 按 playerForceList 排到最前。
            if (playerControlled)
                scenario.PromoteForceToRunQueueHead(force);

            Sango.Log.Info($"控制权切换:{force.Name} → {(playerControlled ? "玩家" : "AI")}，" +
                           $"当前玩家势力数 {CountPlayerForces(scenario)}，回合 {scenario.Info.turnCount}");
            return true;
        }

        /// <summary>
        /// 交出一个势力"正在进行的本回合"，收尾效果与玩家点"结束回合"一致。
        ///
        /// 为什么需要它：把正在行动中的势力委托给AI时，控制权要排队到它的下一个回合开始才生效
        /// （见 <see cref="TrySetPlayerControl"/> 里的 pending 分支），但玩家已经明确撒手了，
        /// 不该再把回合停在"等玩家操作"上。这里只做"结束回合"的状态收尾：
        /// 把该势力名下所有尚未结束的军团标记为已结束，再标记势力本回合结束，
        /// 下一帧 <see cref="Force.Run"/> 开头即短路，<c>RunForces</c> 照常走 OnForceTurnEnd 推进到下一个势力。
        ///
        /// 与玩家手动结束回合的唯一差别（刻意不做）：不补跑该势力尚未行动部队的 AI 行动 ——
        /// 这些部队保持现有任务状态，从下一回合起完全交由AI接管。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="force">正在行动中、即将委托给AI的势力</param>
        static void CloseOutCurrentTurn(Scenario scenario, Force force)
        {
            for (int i = 0; i < scenario.corpsSet.Count; i++)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps == null || !corps.IsAlive || corps.BelongForce != force)
                    continue;
                corps.ActionOver = true;
            }

            force.ActionOver = true;

            // 沿用既有的"结束回合"HUD 口径（与 PlayerEndTurn 走同一条事件）：
            // 结束回合按钮与玩家信息面板当场收起，不用等下一个势力回合开始才反映"已经不是你在操作了"。
            GameEvent.OnPlayerEndTurn?.Invoke(force, scenario);
        }

        /// <summary>
        /// 把势力 Id 写进 / 移出 <see cref="ScenarioInfo.playerForceList"/>。
        ///
        /// 该数组来自旧存档时可能为 null，这里统一按空数组处理后再增删；
        /// 只动这一个字段，不碰 <see cref="ScenarioInfo"/> 的其它内容，避免影响存档头显示。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="forceId">目标势力 Id</param>
        /// <param name="add">true = 加入玩家势力列表；false = 移出</param>
        static void SyncPlayerForceList(Scenario scenario, int forceId, bool add)
        {
            ScenarioInfo info = scenario.Info;
            List<int> playerList = new List<int>();
            if (info.playerForceList != null)
                playerList.AddRange(info.playerForceList);

            if (add)
            {
                if (!playerList.Contains(forceId))
                    playerList.Add(forceId);
            }
            else
            {
                playerList.RemoveAll(id => id == forceId);
            }

            info.playerForceList = playerList.ToArray();
        }

        /// <summary>
        /// 统计当前玩家势力数量，仅用于日志与界面文案。
        /// 直接以存档载体为准，不遍历 <see cref="Force.IsPlayer"/>：两者必须一致，
        /// 以存档口径为准能在两者意外不同步时暴露出真实状态。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <returns>玩家势力个数</returns>
        public static int CountPlayerForces(Scenario scenario)
        {
            if (scenario == null || scenario.Info == null || scenario.Info.playerForceList == null)
                return 0;
            return scenario.Info.playerForceList.Length;
        }

        /// <summary>
        /// 场上当前是否真的存在由玩家控制的势力（按运行时控制位判定，不是按存档数组）。
        ///
        /// 与 <see cref="CountPlayerForces"/> 的分工：后者读 <c>playerForceList</c>，代表"目标状态 / 入档口径"，
        /// 用于日志与存档列表显示；本方法代表"此刻谁在操作"，用于决定设置菜单里
        /// "中途参加"与"委托给AI"哪一项该出现 —— 交接排队等待生效的那一回合里，
        /// 玩家此刻确实还在操作旧势力，所以按实时口径显示更符合所见即所得。
        /// </summary>
        /// <returns>true = 至少有一个存活的玩家势力</returns>
        public static bool HasPlayerControlledForce()
        {
            List<Force> playerForces = CollectForces(true);
            return playerForces.Count > 0;
        }

        /// <summary>
        /// 统计一个势力当前实际持有的城池数，用于势力列表的展示后缀。
        ///
        /// 不用 <see cref="Force.CityList"/> / <see cref="Force.CityCount"/>：那两个是
        /// <c>Force.UpdateCityCount</c> 在**该势力回合开始时**刷新的缓存，
        /// 中途参加时对方势力可能整轮还没轮到，列表上会显示成"0城"这种误导信息。
        /// 这里直接按归属现算，只在打开界面那一次遍历，代价可以忽略。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <returns>存活且归属该势力的城池数；势力或剧本为空时返回 0</returns>
        public static int CountCities(Force force)
        {
            Scenario scenario = Scenario.Cur;
            if (force == null || scenario == null || scenario.citySet == null)
                return 0;

            int count = 0;
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive && city.BelongForce == force)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 把镜头移到该势力的首都。
        ///
        /// 复用项目既有做法（见 <c>UIForceElementItem.OnClick</c> / <c>UIGame.OnTroopListSelected</c>）：
        /// 城池的世界坐标取 <see cref="City.CenterCell"/>，再交给 <see cref="MapRender.MoveCameraTo(Vector3)"/>。
        /// 首都不保证一定有中心格（数据异常的存档），取不到时只记日志不动镜头，不影响控制权切换本身。
        /// </summary>
        /// <param name="force">目标势力</param>
        /// <returns>镜头是否移动成功</returns>
        public static bool FocusCameraOnCapital(Force force)
        {
            if (force == null)
                return false;

            City capital = force.CapitalCity;
            if (capital == null || capital.CenterCell == null)
            {
                Sango.Log.Error($"移动镜头失败:势力 {force.Name} 的首都或其中心格为空");
                return false;
            }

            MapRender.Instance.MoveCameraTo(capital.CenterCell.Position);
            return true;
        }
    }
}
