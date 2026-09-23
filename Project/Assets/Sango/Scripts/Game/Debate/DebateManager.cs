/*
 * 文件名：DebateManager.cs
 * 描述：舌战管理器 —— 舌战的唯一入口（对应单挑的 Game/Duel/DuelManager.cs）
 *
 * 职责：
 *   1. 判定能否发起舌战（双方武将有效、不是同一人、都还活着）
 *   2. 组装 Debate.Param（参战武将、体力、是否玩家操作、是否反转）
 *   3. 创建 DebateGameSystem（业务桥）+ 表现层（IDebateView，可选）并驱动舌战到结束
 *   4. 舌战结束后的收尾（关闭表现层、抛结束事件）
 *
 * 与单挑的区别（都是 C++ 的事实，不是本工程的取舍）：
 *   · 舌战**不要求敌对、也不要求相邻** —— C++ 里它用于外交交涉，双方可以是同势力甚至自己人；
 *   · 结算（经验 / 功绩 / 伤病 / 技术点 / 消息）在逻辑层 ClosingPhase → ParamSetWinner 里就地落地，
 *     所以收尾的 Finish() 不再像单挑那样需要额外调 ResultHandler。
 *
 * 驱动方式：
 *   · 带表现层：由外部每帧调用 Update()，逐帧推进（表现层还在播时逻辑层会自己等，见 Debate.IsIdle）；
 *   · 无表现层：Update() 内部一次 Run() 跑完整场，用于 AI 推演与测试。
 *
 * 接线情况（已完成）：
 *   · 安装：Game.Init 调 Debate.DebateIntegration.Install()（表现层工厂 + 发起流程 + 触发点 + 结束改判）；
 *   · 驱动：Game.Update 里舌战进行中时调 DebateManager.Instance.Update() 并暂停剧本推进。
 */

using System;

namespace Sango.Core.Debate
{
    /// <summary>舌战管理器</summary>
    public class DebateManager : Singleton<DebateManager>
    {
        /// <summary>当前正在进行的舌战，null 表示没有舌战</summary>
        public Debate Current { get; private set; }

        /// <summary>当前舌战使用的业务层</summary>
        public DebateGameSystem System { get; private set; }

        /// <summary>当前舌战使用的表现层</summary>
        public IDebateView View { get; private set; }

        /// <summary>是否正在舌战中</summary>
        public bool IsDebating { get { return Current != null; } }

        /// <summary>创建表现层的委托。返回 null 表示本次舌战不带表现（瞬时结算）</summary>
        public static Func<Debate, IDebateView> CreateViewHandler;

        /// <summary>
        /// 释放表现层的委托。由集成层负责关闭舌战窗口。
        /// 注意写成 System.Action：Sango.Core 下有同名 Action 命名空间，裸写 Action 会解析成命名空间。
        /// </summary>
        public static System.Action ReleaseViewHandler;

        /// <summary>
        /// 是否由 Update() 逐帧推进当前舌战。
        /// 带表现层时必须逐帧推进：Debate.Run() 是"一次跑完整场"的阻塞实现，
        /// 而表现层的动画计时器只在 MonoBehaviour.Update() 里递减。
        /// </summary>
        protected bool m_stepMode;

        #region 发起

        /// <summary>
        /// 尝试发起一场舌战。
        /// </summary>
        /// <param name="challenger">挑战方武将</param>
        /// <param name="challenged">应战方武将</param>
        /// <param name="withView">是否带表现层（false 时瞬时结算，用于 AI 推演与测试）</param>
        /// <returns>是否成功发起</returns>
        public virtual bool StartDebate(Person challenger, Person challenged, bool withView = true)
        {
            if (IsDebating) return false;
            if (!CanStartDebate(challenger, challenged)) return false;

            Debate.Param param = BuildParam(challenger, challenged, withView);
            if (param == null) return false;

            View = withView ? CreateViewHandler?.Invoke(null) : null;
            System = new DebateGameSystem(View);
            Current = new Debate(System, param);
            Current.View = View != null;

            GameEvent.OnDebateStart?.Invoke(challenger, challenged);

            // 逐帧模式先初始化；瞬时模式由 Run() 内部 Init，避免重复初始化影响随机序列
            m_stepMode = View != null;
            if (m_stepMode)
                Current.Init();
            return true;
        }

        /// <summary>双方是否符合舌战资格（舌战不判敌对与距离，只判武将本身）</summary>
        public virtual bool CanStartDebate(Person challenger, Person challenged)
        {
            if (challenger == null || challenged == null) return false;
            if (challenger == challenged) return false;
            if (challenger.IsDead || challenged.IsDead) return false;
            return true;
        }

        /// <summary>组装舌战启动参数</summary>
        protected virtual Debate.Param BuildParam(Person challenger, Person challenged, bool withView)
        {
            Debate.Param param = new Debate.Param();

            param.characters[0].person = challenger;
            param.characters[0].hp = Debate.MaxHP;
            // 由玩家亲自操作的一方手动出牌，其余交给 AI；无表现层（不观战）时一律自动。
            // 口径与单挑一致：玩家直属军团（Person.IsPlayerControl）。
            param.characters[0].control = withView && challenger.IsPlayerControl;

            param.characters[1].person = challenged;
            param.characters[1].hp = Debate.MaxHP;
            param.characters[1].control = withView && challenged.IsPlayerControl;

            // 玩家在应战方时反转 —— 对应 C++ ParamInit 里的 reverse = !aControl && bControl
            param.reverse = !param.characters[0].control && param.characters[1].control;
            param.tutorial = false;
            param.finished = false;
            return param;
        }

        #endregion

        #region 驱动

        /// <summary>每帧驱动舌战。返回 true 表示本帧发生了推进</summary>
        public virtual bool Update()
        {
            if (Current == null) return false;

            // 无表现层：一次跑完整场（AI 推演、测试）
            if (!m_stepMode)
            {
                Current.Run();
                if (Current.DebateParam.finished)
                    Finish();
                return false;
            }

            // 带表现层：每帧推进一个 step。
            // 注意 Debate.Update 的返回值语义与单挑相反：true = 仍在继续，false = 已结束。
            // 表现层还在播动画时，逻辑层内部的 IsIdle 会拦住它，本帧等于空转，等下一帧再来；
            // 所以"结算画面还没关"这种情况表现层只要继续报 DebateIsAnimating = true 即可，
            // 管理器不需要额外的"离开按钮"判定。
            bool running = Current.Update(0);
            if (!running)
            {
                Finish();
                return true;
            }
            return true;
        }

        /// <summary>结束并清理当前舌战</summary>
        protected virtual void Finish()
        {
            Debate finished = Current;
            if (finished == null) return;

            Current = null;
            m_stepMode = false;

            // 结算已经在逻辑层落地（ParamSetWinner：经验 / 功绩 / 伤病 / 技术点 / 战报），这里只做收尾
            GameEvent.OnDebateEnd?.Invoke(finished);

            ReleaseView();
            System = null;
        }

        /// <summary>强制中断当前舌战（例如退出游戏、读档）</summary>
        public virtual void Abort()
        {
            if (Current == null) return;
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
