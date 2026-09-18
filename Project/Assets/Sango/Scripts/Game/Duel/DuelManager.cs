/*
 * 文件名：DuelManager.cs
 * 描述：单挑管理器——单挑的唯一入口
 *
 * 职责：
 *   1. 判定能否发起单挑（相邻、敌对、双方均为战斗部队且有主将）
 *   2. 组装 Duel.Param（参战武将、部队、体力、斗志、伤病、操作方式、场地）
 *   3. 创建 DuelGameSystem（业务桥接）+ 表现层（IDuelView，可选）并驱动单挑到结束
 *   4. 单挑结束后的收尾（关闭表现层、抛出结束事件）
 *
 * 驱动方式：由 Game.Update() 每帧调用 Update()，单挑处于进行中时会阻塞剧本推进。
 * 表现层缺失（或 View=false）时，Duel.Run() 会在一次调用内瞬时跑完整场，用于 AI 推演与测试。
 */

using System;
using System.Collections.Generic;

namespace Sango.Core.Duel
{
    /// <summary>单挑管理器</summary>
    public class DuelManager : Singleton<DuelManager>
    {
        /// <summary>当前正在进行的单挑，null 表示没有单挑</summary>
        public Duel Current { get; private set; }

        /// <summary>当前单挑使用的业务层</summary>
        public DuelGameSystem System { get; private set; }

        /// <summary>当前单挑使用的表现层</summary>
        public IDuelView View { get; private set; }

        /// <summary>是否正在单挑中</summary>
        public bool IsDueling { get { return Current != null; } }

        /// <summary>创建表现层的委托。返回 null 表示本次单挑不带表现（瞬时结算）</summary>
        public static Func<Duel, IDuelView> CreateViewHandler;

        /// <summary>释放表现层的委托。由集成层负责关闭单挑窗口</summary>
        public static System.Action ReleaseViewHandler;

        /// <summary>单挑场地解析委托。默认返回草地</summary>
        public static Func<Troop, Troop, int> ResolveStageHandler;

        /// <summary>
        /// 单挑回合数（合数）上限。
        /// 界面上的合数是"十位 + 个位"两张数字图，两位即上限 99。
        /// </summary>
        public const int MaxBlowCounter = 99;

        /// <summary>
        /// 是否由 Update() 逐帧推进当前单挑。
        ///
        /// 带表现层时必须逐帧推进：Duel.Run() 是"一次跑完整场"的阻塞实现，
        /// 而表现层的动画计时器只在 MonoBehaviour.Update() 里递减，
        /// 每帧调 Run() 会既阻塞主线程又把整场单挑反复重开（表现为「单挑开始」无限刷屏）。
        /// 无表现层（AI 推演 / 跳过观战）时直接 Run() 一次跑完。
        /// </summary>
        protected bool m_stepMode;

        #region 发起

        /// <summary>
        /// 尝试发起单挑。
        /// </summary>
        /// <param name="challenger">挑战方部队</param>
        /// <param name="challenged">应战方部队</param>
        /// <param name="withView">是否带表现层（false 时瞬时结算，用于 AI 推演）</param>
        /// <returns>是否成功发起</returns>
        public virtual bool StartDuel(Troop challenger, Troop challenged, bool withView = true)
        {
            if (IsDueling) return false;
            if (!CanStartDuel(challenger, challenged)) return false;

            Duel.Param param = BuildParam(challenger, challenged, withView);
            if (param == null) return false;

            View = withView ? CreateViewHandler?.Invoke(null) : null;
            System = new DuelGameSystem(View);
            Current = new Duel(System, param);
            Current.View = View != null;
            System.CurrentDuel = Current;

            GameEvent.OnDuelStart?.Invoke(challenger, challenged);

            // 表现层在首次被逻辑层调用时会自行绑定单挑本体，这里无需额外处理。
            // 逐帧推进模式下先做一次初始化；瞬时结算模式由 Run() 内部自行 Init，
            // 避免重复初始化影响随机数种子。
            m_stepMode = View != null;
            if (m_stepMode)
                Current.Init();
            return true;
        }

        /// <summary>
        /// 双方是否符合单挑资格（只判属性，不判距离）。
        /// 部队指令需要在"移动过去叫阵"之前预判目标，此时部队还没走过去，
        /// 距离校验必然不成立，所以把资格判定单独拆出来复用。
        /// </summary>
        public virtual bool CanDuelEligible(Troop challenger, Troop challenged)
        {
            if (challenger == null || challenged == null) return false;
            if (challenger == challenged) return false;

            // 双方必须敌对
            if (!challenger.IsEnemy(challenged)) return false;

            // 双方必须都是战斗部队（运输队、器械不可单挑）
            if (!challenger.IsFight || !challenged.IsFight) return false;

            // 双方都要有主将
            if (challenger.Leader == null || challenged.Leader == null) return false;

            return true;
        }

        /// <summary>单挑是否满足发起条件（资格 + 相邻）</summary>
        public virtual bool CanStartDuel(Troop challenger, Troop challenged)
        {
            if (!CanDuelEligible(challenger, challenged)) return false;

            // 双方距离为 1（相邻）
            if (challenger.cell == null || challenged.cell == null) return false;
            if (Scenario.Cur == null || Scenario.Cur.Map == null) return false;
            if (Scenario.Cur.Map.Distance(challenger.cell, challenged.cell) > 1) return false;

            return true;
        }

        /// <summary>组装单挑启动参数</summary>
        protected virtual Duel.Param BuildParam(Troop challenger, Troop challenged, bool withView)
        {
            Duel.Param param = new Duel.Param();
            param.unit[(int)DuelTeam.DuelTeam_Challenger] = challenger;
            param.unit[(int)DuelTeam.DuelTeam_Challenged] = challenged;

            Troop[] troops = { challenger, challenged };
            for (int t = 0; t < Duel.MaxTeamCount; t++)
            {
                Troop troop = troops[t];
                Person[] members = { troop.Leader, troop.Member1, troop.Member2 };

                int count = 0;
                for (int i = 0; i < Duel.MaxTeamCharaCount; i++)
                {
                    Person person = i < members.Length ? members[i] : null;
                    if (person == null || person.IsDead) continue;

                    param.person[t][count] = person;
                    param.hp[t][count] = Duel.MaxHP;
                    param.spirit[t][count] = 0;
                    // 伤病直接沿用武将当前状态；-1 表示不参战
                    param.shoubyou[t][count] = Math.Max(0, person.injury);
                    count++;
                }

                param.startChara[t] = count > 0 ? 0 : -1;
                param.playerId[t] = troop.IsPlayer ? 0 : -1;
                // 玩家部队手动操作，其余交给 AI；无表现层时一律自动
                bool manual = withView && troop.IsPlayer;
                param.control[t] = manual ? (int)DuelControl.DuelControl_Manual : (int)DuelControl.DuelControl_Auto;
            }

            // 参战武将为空的队伍无法单挑
            if (param.startChara[0] < 0 || param.startChara[1] < 0) return null;

            param.type = (int)DuelType.DuelType_2;
            // 单挑回合数（合数）上限
            param.maxBlowCounter = MaxBlowCounter;
            param.stage = ResolveStageHandler != null
                ? ResolveStageHandler(challenger, challenged)
                : (int)DuelStage.DuelStage_Grassland;
            param.ftkType = -1;
            param.ftkTeam = -1;
            return param;
        }

        #endregion

        #region 驱动

        /// <summary>
        /// 每帧驱动单挑。返回 true 表示本帧发生了推进。
        /// </summary>
        public virtual bool Update()
        {
            if (Current == null) return false;

            // 无表现层：一次跑完整场（AI 推演、玩家选了"跳过观战"）
            if (!m_stepMode)
            {
                Current.Run();
                if (Current.IsFinished)
                    Finish();
                return false;
            }

            // 带表现层：每帧推进一个 step。
            // OnPhase 内部通过 IsIdle() 检查表现层是否还在播动画，
            // 动画未播完就不推进，等下一帧再来——这正是表现层驱动的本意。
            bool finished = Current.OnPhase(0);
            if (finished)
                Finish();
            return true;
        }

        /// <summary>结束并清理当前单挑</summary>
        protected virtual void Finish()
        {
            Duel finished = Current;
            if (finished == null) return;

            Current = null;
            m_stepMode = false;
            GameEvent.OnDuelEnd?.Invoke(finished);

            finished.Exit();
            ReleaseView();
            System = null;
        }

        /// <summary>强制中断当前单挑（例如退出游戏、读档）</summary>
        public virtual void Abort()
        {
            if (Current == null) return;
            Current.Exit();
            Current = null;
            m_stepMode = false;
            ReleaseView();
            System = null;
        }

        /// <summary>释放表现层资源</summary>
        protected virtual void ReleaseView()
        {
            View = null;
            ReleaseViewHandler?.Invoke();
        }

        #endregion
    }
}
