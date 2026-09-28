/*
 * 文件名：EventRunnerOps.cs
 * 描述：事件执行器的指令实现（EventRunner 的 partial 部分）
 * 创建日期：2026-09-26
 * 最后修改：2026-09-26
 *
 * 分工：EventRunner.cs 管状态机（推进 / 等待 / 结束），本文件管"每条指令到底做什么"。
 * 拆开是因为指令集会长期膨胀（设计里规划到 27 条），混在状态机里会越来越难读。
 *
 * 指令分两类：
 *   · 已实现（Phase 1）：Talk / TalkWindow / Choice / Effect / Label / Jump / Branch /
 *     SetVar / SetFlag / Message / Wait / End / SkipAll / Parallel / Schedule / Duel / DebateFake
 *   · 待实现（Phase 3，需表现层支持）：Camera / Bgm / Se / Voice / Window / Battle /
 *     InfoPanel / SceneChange / WeatherEffect / FireEffect / CloseUp —— 目前只记日志并跳过，
 *     这样数据可以先写、演出后补，不会因为缺指令而卡住流程。
 */

using Newtonsoft.Json.Linq;
using Sango.Core.Duel;
using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Event
{
    public partial class EventRunner
    {
        /// <summary>是否已进入"跳过演出"状态（SkipAll 之后只跑效果，不再弹任何 UI）</summary>
        bool skipPresentation;

        /// <summary>
        /// 现在还能不能开演出窗口。
        ///
        /// 退出 Play 模式 / 切场景时，UI 根会被销毁，而 {EventRunner} 可能仍停在
        /// "等玩家点对话框"的状态上；此时任何一次开窗都会把新对象挂到正在销毁的 UIRoot 上。
        /// 因此进入"只结算、不演出"的降级模式：效果与流程照跑，UI 全部跳过。
        /// </summary>
        /// <returns>true = UI 可用</returns>
        internal static bool CanOpenUI()
        {
            Game game = Game.Instance;
            return game != null && game.UIRoot != null;
        }

        /// <summary>指令分派入口</summary>
        /// <param name="stage">指令</param>
        void RunOp(EventStage stage)
        {
            // UI 已不可用（场景销毁中）时切到静默模式。
            // 用 sticky 标记：一旦确认 UI 没了，后续指令不再反复检查，也不再尝试开窗。
            if (!skipPresentation && !CanOpenUI())
            {
                skipPresentation = true;
                Log.Warning($"事件 {Context?.definition} 检测到 UI 已不可用（场景销毁中），后续演出改为静默结算");
            }

            switch (stage.Op)
            {
                case EventOpType.Talk: OpTalk(stage); break;
                case EventOpType.TalkWindow: OpTalkWindow(stage); break;
                case EventOpType.Choice: OpChoice(stage); break;
                case EventOpType.Effect: OpEffect(stage); break;
                case EventOpType.Label: OpLabel(stage); break;
                case EventOpType.Jump: OpJump(stage); break;
                case EventOpType.Branch: OpBranch(stage); break;
                case EventOpType.SetVar: OpSetVar(stage); break;
                case EventOpType.SetFlag: OpSetFlag(stage); break;
                case EventOpType.Message: OpMessage(stage); break;
                case EventOpType.Wait: OpWait(stage); break;
                case EventOpType.SkipAll: OpSkipAll(); break;
                case EventOpType.Parallel: OpParallel(stage); break;
                case EventOpType.Schedule: OpSchedule(stage); break;
                case EventOpType.Duel: OpDuel(stage); break;
                case EventOpType.DebateFake: OpDebateFake(stage); break;
                case EventOpType.Picture: OpPicture(stage); break;
                case EventOpType.PictureClose: OpPictureClose(); break;
                case EventOpType.End: Finish(true, "遇到 End 指令"); break;
                default: OpNotImplemented(stage); break;
            }
        }

        /// <summary>
        /// 把上下文的触发序号与事件 Id 同步进条件数据库。
        /// 每次求值条件前都要调——FireCountCompare / EventFired 依赖它们。
        /// </summary>
        void PrepareDatabase()
        {
            if (Context == null || Context.database == null) return;

            Context.database.fireCount = Context.fireCount;
            Context.database.eventId = Context.definition != null ? Context.definition.Id : 0;
        }

        /// <summary>求值一个条件树（已同步上下文数据）</summary>
        /// <param name="condition">条件树 JSON</param>
        /// <param name="defaultValue">条件为空时的返回值</param>
        /// <returns>是否成立</returns>
        bool EvalCondition(JObject condition, bool defaultValue)
        {
            if (condition == null) return defaultValue;

            PrepareDatabase();
            bool result = EventConditionEvaluator.Evaluate(condition, Context.database, out string reason);

            if (!result && !string.IsNullOrEmpty(reason))
                Log.Info($"事件 {Context.definition} 的条件未满足：{reason}");

            return result;
        }

        #region 台词

        /// <summary>台词（多句链路，走 GameDialog.OpenTalk 在同一个窗口里逐句显示）</summary>
        /// <param name="stage">指令</param>
        void OpTalk(EventStage stage)
        {
            string slotKey = stage.GetString("Slot");
            Person speaker = Context.GetSlot<Person>(slotKey);

            List<GameDialog.TalkData> talks = BuildTalks(stage, speaker);

            // 没有台词就不占位，直接继续（OpenTalk 会同步回调，重入由 Step 锁兜住）
            if (talks.Count == 0 || skipPresentation)
            {
                if (talks.Count == 0)
                    Log.Warning($"事件 {Context.definition} 的 Talk 指令没有台词（Slot={slotKey}）");
                return;
            }

            GameDialog.DialogStyle style = ParseStyle(stage.GetString("Style"), GameDialog.DialogStyle.ClickPersonSay);

            SetWaiting(true);
            GameDialog.Instance.OpenTalk(talks, OnPresentationDone, style);
        }

        /// <summary>旁白文本框</summary>
        /// <param name="stage">指令</param>
        void OpTalkWindow(EventStage stage)
        {
            if (skipPresentation) return;

            string text = EvalTextOf(stage.GetString("Text"));
            if (string.IsNullOrEmpty(text)) return;

            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>
            {
                new GameDialog.TalkData { text = text }
            };

            SetWaiting(true);
            GameDialog.Instance.OpenTalk(talks, OnPresentationDone, GameDialog.DialogStyle.Window);
        }

        /// <summary>把指令的 Lines 数组转成台词链，并替换占位符</summary>
        /// <param name="stage">指令</param>
        /// <param name="speaker">说话人（可为 null）</param>
        /// <returns>台词链</returns>
        List<GameDialog.TalkData> BuildTalks(EventStage stage, Person speaker)
        {
            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>();

            JArray lines = stage.GetArray("Lines");
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    string raw = null;
                    if (lines[i] is JObject obj) raw = obj.Value<string>("Text");
                    else raw = lines[i].Value<string>();

                    if (string.IsNullOrEmpty(raw)) continue;

                    talks.Add(new GameDialog.TalkData
                    {
                        text = EvalTextOf(raw),
                        person = speaker,
                    });
                }
            }
            else
            {
                // 兼容只写一句的简写
                string single = stage.GetString("Text");
                if (!string.IsNullOrEmpty(single))
                    talks.Add(new GameDialog.TalkData { text = EvalTextOf(single), person = speaker });
            }

            return talks;
        }

        /// <summary>演出环节（台词 / 选择肢）结束后的统一续跑回调</summary>
        void OnPresentationDone()
        {
            if (Context == null) return;

            SetWaiting(false);
            Step();
        }

        /// <summary>解析对话框风格</summary>
        /// <param name="name">风格名</param>
        /// <param name="fallback">缺省风格</param>
        /// <returns>对话框风格</returns>
        static GameDialog.DialogStyle ParseStyle(string name, GameDialog.DialogStyle fallback)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            return Enum.TryParse(name, out GameDialog.DialogStyle style) ? style : fallback;
        }

        /// <summary>替换文本里的占位符</summary>
        /// <param name="raw">原始文本</param>
        /// <returns>替换后的文本</returns>
        string EvalTextOf(string raw)
        {
            return EventValue.EvalText(raw, Context);
        }

        #endregion

        #region 选择肢与分支

        /// <summary>玩家选择肢</summary>
        /// <param name="stage">指令</param>
        void OpChoice(EventStage stage)
        {
            JArray options = stage.GetArray("Options");
            if (options == null || options.Count == 0)
            {
                Log.Warning($"事件 {Context.definition} 的 Choice 指令没有选项");
                return;
            }

            // 跳过演出时直接选第一项，保证分支仍然生效
            if (skipPresentation)
            {
                string firstNext = (options[0] as JObject)?.Value<string>("Next");
                if (!string.IsNullOrEmpty(firstNext)) JumpTo(firstNext);
                return;
            }

            List<PlayerChoice.ChoiceData> choices = new List<PlayerChoice.ChoiceData>(options.Count);

            for (int i = 0; i < options.Count; i++)
            {
                JObject option = options[i] as JObject;
                if (option == null) continue;

                // 闭包捕获：index 与 next 必须在循环体内取局部副本
                int index = i;
                string next = option.Value<string>("Next");
                string text = EvalTextOf(option.Value<string>("Text"));

                choices.Add(new PlayerChoice.ChoiceData
                {
                    lab = text,
                    call = () => OnChoicePicked(index, next)
                });
            }

            if (choices.Count == 0) return;

            PlayerChoice playerChoice = GameSystem.GetSystem<PlayerChoice>();
            if (playerChoice == null)
            {
                // 没有选择肢系统（例如无 UI 上下文）时降级为"选第一项"，不能把流程卡死
                Log.Warning($"事件 {Context.definition} 无法打开选择肢（PlayerChoice 系统不可用），自动选择第一项");
                OnChoicePicked(0, (options[0] as JObject)?.Value<string>("Next"));
                return;
            }

            SetWaiting(true);
            playerChoice.Start(choices.ToArray());
        }

        /// <summary>玩家选完之后</summary>
        /// <param name="index">选项序号</param>
        /// <param name="next">选项指向的 Label</param>
        void OnChoicePicked(int index, string next)
        {
            if (Context == null) return;

            Context.chosenOptionIndex = index;
            SetWaiting(false);

            if (!string.IsNullOrEmpty(next)) JumpTo(next);
            Step();
        }

        /// <summary>Label：只作为跳转目标，本身不做事</summary>
        /// <param name="stage">指令</param>
        void OpLabel(EventStage stage)
        {
            // Label 在 BuildLabelIndex 里已建好索引，执行到它直接跳过
        }

        /// <summary>无条件跳转</summary>
        /// <param name="stage">指令</param>
        void OpJump(EventStage stage)
        {
            JumpTo(stage.GetString("Target"));
        }

        /// <summary>条件跳转</summary>
        /// <param name="stage">指令</param>
        void OpBranch(EventStage stage)
        {
            bool met = EvalCondition(stage.GetObject("Conditions"), false);

            if (met)
            {
                string target = stage.GetString("Target");
                if (!string.IsNullOrEmpty(target)) JumpTo(target);
                return;
            }

            // 条件不成立时的可选跳转
            string elseTarget = stage.GetString("Else");
            if (!string.IsNullOrEmpty(elseTarget)) JumpTo(elseTarget);
        }

        #endregion

        #region 效果与变量

        /// <summary>效果组</summary>
        /// <param name="stage">指令</param>
        void OpEffect(EventStage stage)
        {
            JArray effects = stage.GetArray("Effects");
            if (effects == null || effects.Count == 0) return;

            for (int i = 0; i < effects.Count; i++)
            {
                JObject obj = effects[i] as JObject;
                if (obj == null) continue;

                EventEffectNode node;
                try
                {
                    node = obj.ToObject<EventEffectNode>();
                }
                catch (Exception e)
                {
                    Log.Error($"事件 {Context.definition} 解析效果节点失败（已跳过）：{e.Message}");
                    continue;
                }

                if (node == null) continue;

                // 局部生效条件：不满足则跳过本效果，其余效果继续
                if (node.Condition != null && !EvalCondition(node.Condition, true))
                    continue;

                EventEffectBase effect = EventEffectBase.Create(node);
                if (effect == null) continue;

                try
                {
                    effect.Execute(Context);
                }
                catch (Exception e)
                {
                    Log.Error($"事件 {Context.definition} 执行效果 {node.Type} 异常（已跳过）：{e.Message}");
                }
            }
        }

        /// <summary>事件私有变量运算</summary>
        /// <param name="stage">指令</param>
        void OpSetVar(EventStage stage)
        {
            string varName = stage.GetString("Var");
            if (string.IsNullOrEmpty(varName)) return;

            int value = EventValue.EvalInt(stage.Params?["Value"], Context);
            string op = stage.GetString("Op", "Set");

            int result;
            switch (op)
            {
                case "Add": result = Context.GetVar(varName) + value; break;
                case "Sub": result = Context.GetVar(varName) - value; break;
                default: result = value; break;
            }

            Context.SetVar(varName, result);
        }

        /// <summary>事件旗标（默认全局，跨事件可读、进存档）</summary>
        /// <param name="stage">指令</param>
        void OpSetFlag(EventStage stage)
        {
            string flag = stage.GetString("Flag");
            if (string.IsNullOrEmpty(flag)) return;

            bool value = stage.GetBool("Value", true);
            if (stage.GetString("Scope") == "Event") Context.state?.SetFlag(flag, value);
            else Context.SetGlobalFlag(flag, value);
        }

        /// <summary>结果情报消息</summary>
        /// <param name="stage">指令</param>
        void OpMessage(EventStage stage)
        {
            string text = EvalTextOf(stage.GetString("Text"));
            if (string.IsNullOrEmpty(text)) return;

            AddResultMessage(text);
            Log.Info($"事件情报：{text}");

            // 一期直接走对话框展示；Phase 3 改为"情报面板"窗口
            if (skipPresentation) return;

            List<GameDialog.TalkData> talks = new List<GameDialog.TalkData>
            {
                new GameDialog.TalkData { text = text }
            };

            SetWaiting(true);
            GameDialog.Instance.OpenTalk(talks, OnPresentationDone, GameDialog.DialogStyle.Window);
        }

        #endregion

        #region 流程控制

        /// <summary>等待</summary>
        /// <param name="stage">指令</param>
        void OpWait(EventStage stage)
        {
            if (skipPresentation) return;

            float seconds = stage.Params != null && stage.Params["Seconds"] != null
                ? stage.Params.Value<float>("Seconds")
                : 0f;

            if (seconds > 0f) SetWaitTimer(seconds);
        }

        /// <summary>
        /// 跳过剩余演出，只结算效果。
        /// 语义：之后的台词 / 选择肢 / 等待全部跳过（选择肢取第一项），
        /// 但 Effect / SetFlag / Branch / Jump 照常执行——否则"跳过"会把剧情效果一起吞掉。
        /// </summary>
        void OpSkipAll()
        {
            skipPresentation = true;
            Log.Info($"事件 {Context.definition} 进入跳过演出模式");
        }

        /// <summary>
        /// 并行执行一组指令。
        /// 一期是"顺序执行 + 全部不等待"的简化实现：把需要等待的演出指令降级为跳过，
        /// 只跑同步的效果类指令。真正的并行演出（镜头推近的同时说话）要等 Phase 3 的演出层。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpParallel(EventStage stage)
        {
            JArray ops = stage.GetArray("Ops");
            if (ops == null || ops.Count == 0) return;

            bool resumeSkip = skipPresentation;
            skipPresentation = true;    // 并行期间不等待任何演出

            try
            {
                for (int i = 0; i < ops.Count; i++)
                {
                    JObject obj = ops[i] as JObject;
                    if (obj == null) continue;

                    EventStage sub;
                    try
                    {
                        sub = obj.ToObject<EventStage>();
                    }
                    catch (Exception e)
                    {
                        Log.Error($"事件 {Context.definition} 解析 Parallel 子指令失败：{e.Message}");
                        continue;
                    }

                    if (sub == null) continue;

                    RunOpIfSynchronous(sub);
                }
            }
            finally
            {
                skipPresentation = resumeSkip;
            }
        }

        /// <summary>只执行同步类指令（Parallel 用；遇到演出/流程指令直接忽略）</summary>
        /// <param name="stage">子指令</param>
        void RunOpIfSynchronous(EventStage stage)
        {
            switch (stage.Op)
            {
                case EventOpType.Effect:
                case EventOpType.SetVar:
                case EventOpType.SetFlag:
                    RunOp(stage);
                    break;
                default:
                    // 演出类 / 流程跳转类在 Parallel 里不生效——并行跳转会让程序计数器失去意义
                    break;
            }
        }

        /// <summary>
        /// 登记定时待办（跨回合事件链）。
        /// 槽位以"类型 + Id"的形式随待办项保存，下一回合重新解析，避免持有失效引用。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpSchedule(EventStage stage)
        {
            int targetEventId = stage.GetInt("EventId");
            if (targetEventId <= 0)
            {
                Log.Warning($"事件 {Context.definition} 的 Schedule 指令没有指定合法的 EventId");
                return;
            }

            ScenarioEventManager manager = ScenarioEventManager.Instance;
            if (manager == null) return;

            int delayTurns = stage.GetInt("DelayTurns");
            int chainId = Context.definition.Chain != null ? Context.definition.Chain.ChainId : 0;
            int stage2 = Context.definition.Chain != null ? Context.definition.Chain.Stage : 0;

            // CancelIf 条件原样存成 JSON 文本，跨回合后再求值
            JObject cancelIf = stage.GetObject("CancelIf");
            string cancelJson = cancelIf != null ? cancelIf.ToString(Newtonsoft.Json.Formatting.None) : null;

            manager.Schedule(targetEventId, delayTurns, chainId, stage2,
                Context.boundSlots, cancelJson, Context.definition.Id);
        }

        #endregion

        #region 单挑与舌战

        /// <summary>
        /// 强制发起事件单挑（三英战吕布）。
        /// 3 打 1 不靠本指令实现——把关羽张飞设为刘备部队的副将，
        /// "刘备部队 vs 吕布部队"在数据层天然就是 3v1（这与三国志系列的实际设计一致）。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpDuel(EventStage stage)
        {
            Troop challenger = Context.GetSlot<Troop>(stage.GetString("ChallengerSlot"));
            Troop challenged = Context.GetSlot<Troop>(stage.GetString("ChallengedSlot"));

            if (challenger == null || challenged == null)
            {
                Log.Warning($"事件 {Context.definition} 的 Duel 指令槽位未绑定（挑战方={challenger != null}，应战方={challenged != null}），跳过单挑");
                return;
            }

            if (skipPresentation)
            {
                // 跳过演出时不打单挑，但要把强制结果结算掉，保证剧情状态一致
                Log.Info($"事件 {Context.definition} 跳过演出，单挑不执行（强制结果在条件中自行处理）");
                return;
            }

            DuelChallengeFlow.RequestOptions options = new DuelChallengeFlow.RequestOptions
            {
                forceAccept = stage.GetBool("ForceAccept", true),
                forceView = stage.GetBool("ForceView", true),
                skipLines = stage.GetBool("SkipLines", true),
                duelType = stage.GetBool("AsEvent", true) ? DuelType.DuelType_Event : DuelType.DuelType_2,
                outcome = BuildDuelOutcome(stage),
            };

            if (DuelChallengeFlow.Request(challenger, challenged, DuelChallengeFlow.Source.GameEvent, options))
            {
                SetWaitingDuel();
            }
            else
            {
                Log.Warning($"事件 {Context.definition} 的单挑请求被拒（部队已灭 / 已在单挑中），继续后续指令");
            }
        }

        /// <summary>从指令参数构造单挑结果覆盖</summary>
        /// <param name="stage">指令</param>
        /// <returns>结果覆盖；未指定返回 null</returns>
        static DuelOutcomeOverride BuildDuelOutcome(EventStage stage)
        {
            JObject raw = stage.GetObject("Outcome");
            if (raw == null) return null;

            DuelOutcomeOverride outcome = new DuelOutcomeOverride
            {
                winnerTeam = raw.Value<int?>("WinnerTeam") ?? -1,
                ignoreGovernorImmunity = raw.Value<bool?>("IgnoreGovernorImmunity") ?? false,
                ignoreForceKind = raw.Value<bool?>("IgnoreForceKind") ?? false,
            };

            // 结局名兼容两种写法：枚举全名 DuelCharaResult_Escaped 与简写 Escaped。
            // 简写更好写（策划手写 JSON 时不必记前缀），因此两种都收。
            string loserResult = raw.Value<string>("LoserResult");
            if (!string.IsNullOrEmpty(loserResult))
            {
                if (!Enum.TryParse(loserResult, out DuelCharaResult parsed)
                    && !Enum.TryParse("DuelCharaResult_" + loserResult, out parsed))
                {
                    Log.Warning($"单挑结果覆盖的 LoserResult \"{loserResult}\" 无法识别（可选值：Dead 战死 / Captured 被俘 / Escaped 逃脱），该项已被忽略");
                }
                else
                {
                    outcome.loserResult = parsed;
                }
            }

            return outcome.IsSpecified ? outcome : null;
        }

        /// <summary>
        /// 伪舌战：台词链 + 直接结算。
        /// 真实舌战界面未完成（DebateChallengeFlow.ForceNoView == true），
        /// 因此"舌战群儒""骂死王朗"一期只能靠台词表现，胜负由事件自己用效果表达。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpDebateFake(EventStage stage)
        {
            if (skipPresentation) return;

            List<GameDialog.TalkData> talks = BuildTalks(stage, null);
            if (talks.Count == 0) return;

            SetWaiting(true);
            GameDialog.Instance.OpenTalk(talks, OnPresentationDone, GameDialog.DialogStyle.ClickPersonSay);
        }

        #endregion

        #region 图文演出

        /// <summary>
        /// 图文演出：铺满屏幕显示一张 CG（或序列帧动图）+ 文字，**等玩家点击关闭后才继续**。
        ///
        /// 参数：`Image`（资源名，可省扩展名）／`Title`／`Text`（支持 {:槽位Key} 占位符）
        ///       ／`MinSeconds`（最少展示秒数，0 = 用入场动画时长）／`PauseBgm`。
        ///
        /// 窗口实例复用与"关不掉就卡死"这两个坑都由 <see cref="UIEventPicture"/> 内部处理：
        /// 缺图只降级为"只显示文字"，参数缺失则放开关闭权限。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpPicture(EventStage stage)
        {
            if (skipPresentation) return;

            EventPictureArgs args = new EventPictureArgs
            {
                Image = stage.GetString("Image"),
                Title = EvalTextOf(stage.GetString("Title")),
                Text = EvalTextOf(stage.GetString("Text")),
                MinSeconds = stage.Params != null && stage.Params["MinSeconds"] != null
                    ? stage.Params.Value<float>("MinSeconds")
                    : 0f,
                PauseBgm = stage.GetBool("PauseBgm", false),
            };

            if (string.IsNullOrEmpty(args.Image)
                && string.IsNullOrEmpty(args.Title)
                && string.IsNullOrEmpty(args.Text))
            {
                Log.Warning($"事件 {Context.definition} 的 Picture 指令既没有图也没有文字，已跳过");
                return;
            }

            Window.WindowInterface win = Window.Instance.Open(UIEventPicture.WindowName, args);
            if (win == null || !win.HasValid())
            {
                // 缺预制件（还没做 window_event_picture）时降级为跳过，不要让事件卡在等待上
                Log.Warning($"事件 {Context.definition} 的图文窗口打开失败（缺 {UIEventPicture.WindowName} 预制件？），已跳过");
                return;
            }

            win.ugui_instance.OnCloseAction = OnPictureClosed;
            SetWaiting(true);
        }

        /// <summary>
        /// 提前收掉图文窗口。
        /// 正常情况由玩家点击关闭；这条用于分支里"演到一半强制收场"。
        /// </summary>
        void OpPictureClose()
        {
            Window.WindowInterface win = Window.Instance.GetWindow(UIEventPicture.WindowName);

            if (win.HasValid())
            {
                // 先摘掉回调再关：本指令只是收场，不需要它再触发一次"续播"
                win.ugui_instance.OnCloseAction = null;
            }

            Window.Instance.Close(UIEventPicture.WindowName);
            SetWaiting(false);
        }

        /// <summary>
        /// 图文窗口关闭回调。
        ///
        /// 【为什么只记标记、不在回调里直接续播】
        /// 回调是在 <c>UGUIWindow.Close()</c> **内部**触发的，而它执行完 <c>OnClose()</c> 之后
        /// 还有一句 <c>IsOpen = false;</c>。若在这时同步续播并又打开了同一个窗口实例
        /// （窗口是复用的），那一句就会把新打开的状态覆盖成"已关闭"。
        /// 因此推迟到下一帧的 <c>Update()</c> 里再续播。
        /// </summary>
        void OnPictureClosed()
        {
            pictureClosedPending = true;
        }

        #endregion

        #region 未实现指令

        /// <summary>
        /// Phase 3 的演出指令：只记日志并跳过。
        /// 这样事件数据可以先写全（策划不必等表现层），流程也不会因缺指令而卡住。
        /// </summary>
        /// <param name="stage">指令</param>
        void OpNotImplemented(EventStage stage)
        {
            Log.Info($"事件 {Context.definition} 的指令 {stage.Op} 尚未实现（Phase 3 补），已跳过");
        }

        #endregion
    }
}
