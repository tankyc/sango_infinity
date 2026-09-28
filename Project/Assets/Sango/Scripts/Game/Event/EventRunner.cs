/*
 * 文件名：EventRunner.cs
 * 描述：事件执行器——驱动演出指令流的状态机（指令实现见 EventRunnerOps.cs）
 * 创建日期：2026-09-26
 * 最后修改：2026-09-26
 */

using Sango.Core.Duel;
using System;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    /// <summary>
    /// 事件执行器。
    ///
    /// 【为什么是 GameSystem 而不是协程】
    /// 演出过程中需要等待三类外部输入：对话框关闭、玩家选择肢、单挑结束。
    /// 用协程就得把"等待期间玩家读档 / 关窗口"的清理写得到处都是；
    /// 做成 GameSystem 入栈后，退出时 `OnDestroy` 天然是一个统一的收尾点，
    /// 且 `Update()` 每帧被系统栈驱动，不需要自己管 MonoBehaviour 生命周期。
    ///
    /// 【为什么只用一条指令流 + Label/Jump，而不是嵌套树】
    /// 编辑器里可以直接上下拖拽调整顺序；"跳过某段""回到某段"天然可表达；
    /// 心智与剧本脚本一致，Mod 作者容易上手。
    ///
    /// 【失败兜底】任何指令抛异常都只记日志并跳到下一条，绝不中断整个事件——
    /// 玩家不该因为一条配置笔误而卡在无法操作的状态。
    /// </summary>
    [GameSystem(order = 910)]
    public partial class EventRunner : GameSystem
    {
        /// <summary>单例入口（本类由 GameSystemManager 反射创建）</summary>
        public static EventRunner Instance
        {
            get { return GameSystem.GetSystem<EventRunner>(); }
        }

        /// <summary>当前运行时上下文；空闲时为 null</summary>
        public EventContext Context { get; private set; }

        /// <summary>指令流</summary>
        List<EventStage> stages;

        /// <summary>程序计数器（指向下一条待执行指令）</summary>
        int pc;

        /// <summary>Label 名 → 指令下标</summary>
        Dictionary<string, int> labels;

        /// <summary>是否在等待外部输入（对话关闭 / 选择肢 / 单挑结束）</summary>
        bool waiting;

        /// <summary>Wait 指令的剩余秒数</summary>
        float waitTimer;

        /// <summary>是否已中止（槽位失效、异常等），中止后不再推进</summary>
        bool aborted;

        /// <summary>
        /// 播放结束回调（无论正常结束还是中止都会调用一次）。
        /// 必须写成 System.Action：本文件在 Sango.Core.Event 下，
        /// 而 Sango.Core 里有一个 Action **命名空间**，写裸 Action 会被解析成命名空间（CS0118）。
        /// </summary>
        System.Action onFinished;

        /// <summary>本事件累计收集到的"结果情报"消息</summary>
        readonly List<string> resultMessages = new List<string>();

        /// <summary>是否正在播放事件</summary>
        public bool IsRunning { get { return Context != null; } }

        /// <summary>当前是否卡在等待外部输入（调试面板用）</summary>
        public bool IsWaiting { get { return waiting; } }

        /// <summary>当前指令下标（调试面板用）</summary>
        public int ProgramCounter { get { return pc; } }

        /// <summary>本事件收集到的结果情报（调试面板 / 结算弹窗用）</summary>
        public List<string> ResultMessages { get { return resultMessages; } }

        #region 播放入口

        /// <summary>
        /// 开始播放一个事件。
        /// </summary>
        /// <param name="definition">事件定义</param>
        /// <param name="context">已绑定好槽位的运行时上下文</param>
        /// <param name="finished">播放结束回调（正常结束与中止都会调用一次）</param>
        /// <returns>是否成功开始播放</returns>
        public bool Play(EventDefinition definition, EventContext context, System.Action finished)
        {
            if (definition == null || context == null) return false;

            if (IsRunning)
            {
                // 同一时刻只允许一个事件播放（演出独占输入，并发会互相覆盖窗口）
                Log.Warning($"事件 {definition} 未能播放：已有事件 {Context.definition} 正在播放");
                return false;
            }

            Context = context;
            stages = definition.Stages;
            pc = 0;
            waiting = false;
            waitTimer = 0f;
            aborted = false;
            onFinished = finished;
            resultMessages.Clear();

            BuildLabelIndex();

            Log.Info($"事件开始播放：{definition}（共 {(stages != null ? stages.Count : 0)} 条指令）");

            // 入栈：从此每帧由系统栈驱动 Update()
            Push();

            // 第一条指令立即执行，避免第一帧空转导致"点了没反应"的观感
            Step();

            return true;
        }

        /// <summary>建立 Label → 下标索引（一次遍历，避免每次 Jump 都线性查找）</summary>
        void BuildLabelIndex()
        {
            if (labels == null) labels = new Dictionary<string, int>();
            labels.Clear();

            if (stages == null) return;
            for (int i = 0; i < stages.Count; i++)
            {
                EventStage stage = stages[i];
                if (stage == null || stage.Op != EventOpType.Label) continue;

                string name = stage.GetString("Name");
                if (string.IsNullOrEmpty(name)) continue;

                if (labels.ContainsKey(name))
                {
                    Log.Warning($"事件 {Context.definition} 存在重复的 Label：{name}（后者被忽略）");
                    continue;
                }
                labels[name] = i;
            }
        }

        #endregion

        #region 驱动

        /// <summary>
        /// 是否在等单挑 / 舌战结束。
        /// 刻意用"每帧轮询状态"而不是订阅 GameEvent.OnDuelEnd：
        /// 单挑可能被玩家中途取消、也可能因前置校验失败根本没启动，
        /// 轮询最终状态不会漏事件、也不会因订阅未解绑而泄漏。
        /// </summary>
        bool waitingDuel;

        /// <summary>
        /// 图文演出窗口已关闭，等下一帧再续播。
        /// 不直接在窗口的 OnClose 回调里续播：那是 UGUIWindow.Close() 内部触发的，
        /// 它执行完 OnClose() 后还有一句 IsOpen = false，会把"重入后新打开的状态"覆盖掉。
        /// </summary>
        bool pictureClosedPending;

        /// <summary>每帧驱动：等待结束后继续推进指令流</summary>
        public override void Update()
        {
            if (Context == null) return;
            if (aborted) { Finish(false, "已中止"); return; }

            // 图文窗口关闭后的续播（推迟一帧，理由见 pictureClosedPending 的注释）
            if (pictureClosedPending)
            {
                pictureClosedPending = false;
                SetWaiting(false);
                Step();
                return;
            }

            // 等单挑 / 舌战：两者都结束（且应战询问也已收尾）才算过
            if (waitingDuel)
            {
                // 注意：命名空间 Sango.Core.Duel 里同时有类 Duel，
                // 因此不能写成 Duel.DuelManager（会被解析成"类 Duel 的嵌套成员"）。
                bool dueling = DuelManager.Instance != null && DuelManager.Instance.IsDueling;
                if (!dueling && !DuelChallengeFlow.IsPending)
                {
                    waitingDuel = false;
                    waiting = false;
                }
                return;
            }

            // 等待对话框 / 选择肢
            if (waiting) return;

            // Wait 指令倒计时
            if (waitTimer > 0f)
            {
                waitTimer -= UnityEngine.Time.deltaTime;
                if (waitTimer > 0f) return;
                waitTimer = 0f;
            }

            Step();
        }

        /// <summary>进入"等单挑结束"状态（供 Duel 指令调用）</summary>
        internal void SetWaitingDuel()
        {
            waitingDuel = true;
            waiting = true;
        }

        /// <summary>Step 重入锁。台词为空 / 窗口打开失败时，收尾回调是**同步**执行的，
        /// 会在 Step 内部再调一次 Step；没有这个锁就会递归出两套执行循环，指令被重复执行。</summary>
        bool stepping;

        /// <summary>
        /// 推进指令流。
        /// 循环执行"同步指令"直到遇到需要等待的指令或指令流结束——
        /// 这样台词/效果之间的衔接不会因为等一帧而显得卡顿。
        /// </summary>
        void Step()
        {
            if (stepping) return;
            if (Context == null) return;

            stepping = true;
            try
            {
                if (stages == null || pc >= stages.Count)
                {
                    Finish(true, "指令流结束");
                    return;
                }

                // 单次 Step 的指令上限：防止 Jump 成环导致死循环卡死整帧
                const int maxOpsPerStep = 256;
                int executed = 0;

                while (Context != null && !waiting && waitTimer <= 0f && !aborted)
                {
                    if (pc < 0 || pc >= stages.Count)
                    {
                        Finish(true, "指令流结束");
                        return;
                    }

                    if (++executed > maxOpsPerStep)
                    {
                        Log.Error($"事件 {Context.definition} 单帧执行指令超过 {maxOpsPerStep} 条，疑似 Jump 死循环，强制中止");
                        Finish(false, "疑似死循环");
                        return;
                    }

                    ExecuteStage(stages[pc]);
                }
            }
            finally
            {
                stepping = false;
            }
        }

        /// <summary>
        /// 执行一条指令（具体实现在 EventRunnerOps.cs）。
        /// 执行前统一做禁用与异常兜底。
        /// </summary>
        /// <param name="stage">指令</param>
        void ExecuteStage(EventStage stage)
        {
            if (stage == null)
            {
                pc++;
                return;
            }

            // 被禁用的指令直接跳过（编辑器里临时屏蔽某几句台词用）
            if (stage.Disabled)
            {
                pc++;
                return;
            }

            // 默认推进：需要跳转的指令会在实现里改写 pc
            int before = pc;
            pc++;

            try
            {
                RunOp(stage);
            }
            catch (Exception e)
            {
                Log.Error($"事件 {Context.definition} 的指令 {stage.Op} 执行异常（已跳过）：{e.Message}\n{e.StackTrace}");

                // 异常时不回退 pc，避免同一条指令反复抛异常形成死循环
                if (pc <= before) pc = before + 1;
            }
        }

        #endregion

        #region 跳转与结束

        /// <summary>跳转到指定 Label</summary>
        /// <param name="labelName">Label 名</param>
        /// <returns>是否找到并跳转成功</returns>
        public bool JumpTo(string labelName)
        {
            if (string.IsNullOrEmpty(labelName))
            {
                Log.Warning($"事件 {Context?.definition} 的跳转指令没有指定目标 Label");
                return false;
            }

            if (labels != null && labels.TryGetValue(labelName, out int index))
            {
                pc = index;
                return true;
            }

            Log.Warning($"事件 {Context?.definition} 跳转失败：找不到 Label {labelName}");
            return false;
        }

        /// <summary>设置等待状态（供指令实现调用）</summary>
        /// <param name="value">是否等待</param>
        internal void SetWaiting(bool value)
        {
            waiting = value;
        }

        /// <summary>设置 Wait 指令的倒计时（供指令实现调用）</summary>
        /// <param name="seconds">秒数</param>
        internal void SetWaitTimer(float seconds)
        {
            waitTimer = seconds;
            waiting = seconds > 0f;
        }

        /// <summary>追加一条结果情报</summary>
        /// <param name="text">消息文本</param>
        internal void AddResultMessage(string text)
        {
            if (!string.IsNullOrEmpty(text)) resultMessages.Add(text);
        }

        /// <summary>
        /// 结束播放并归还控制权。
        /// </summary>
        /// <param name="success">是否正常结束（false 表示中止，不计触发次数）</param>
        /// <param name="reason">中文原因（日志用）</param>
        public void Finish(bool success, string reason)
        {
            if (Context == null) return;

            EventDefinition definition = Context.definition;
            System.Action callback = onFinished;

            Log.Info($"事件结束：{definition}（{(success ? "正常" : "中止")}，{reason}）");

            // 先清状态再回调：回调里可能立刻启动下一个事件
            Context = null;
            stages = null;
            pc = 0;
            waiting = false;
            waitTimer = 0f;
            aborted = false;
            onFinished = null;
            skipPresentation = false;
            waitingDuel = false;
            pictureClosedPending = false;

            // 退出系统栈。
            // 用 Back(this) 而不是 Back()：Back() 弹的是栈顶，若此时栈顶是别人
            // （例如一个尚未关闭的选择肢系统），就会把别人弹掉而自己留在栈里，
            // 导致事件系统永远卡在"播放中"。Back(this) 精确移除自己。
            // 这里 Context 已置空，因此 OnDestroy 不会再做一次业务收尾。
            GameSystemManager.Instance.Back(this);

            callback?.Invoke();
        }

        /// <summary>中止播放（槽位失效、异常等）</summary>
        /// <param name="reason">中文原因</param>
        public void Abort(string reason)
        {
            aborted = true;
            Finish(false, reason);
        }

        /// <summary>
        /// 离开系统栈时的收尾。
        /// 走到这里说明是被外部（读档 / 回主菜单 / 强制弹出）摘掉的，
        /// 必须自己把状态清干净并把控制权还回去，否则事件系统会永远卡在"播放中"。
        /// </summary>
        public override void OnDestroy()
        {
            if (Context != null)
            {
                Log.Warning($"事件 {Context.definition} 被外部中断（可能是读档或回主菜单）");

                System.Action callback = onFinished;
                Context = null;
                stages = null;
                pc = 0;
                waiting = false;
                waitTimer = 0f;
                aborted = false;
                onFinished = null;
                skipPresentation = false;
                waitingDuel = false;
                pictureClosedPending = false;

                callback?.Invoke();
            }
        }

        /// <summary>清空运行期状态（读档 / 回主菜单时由 ScenarioEventManager 调用）</summary>
        public override void Clear()
        {
            Context = null;
            stages = null;
            pc = 0;
            waiting = false;
            waitTimer = 0f;
            aborted = false;
            onFinished = null;
            skipPresentation = false;
            waitingDuel = false;
            pictureClosedPending = false;
            resultMessages.Clear();
            labels?.Clear();
        }

        #endregion
    }
}
