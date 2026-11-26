/*
 * 文件名：PersonSeekOfficeEvent.cs
 * 描述：在野武将求仕的演出事件——所在城归属玩家势力时弹出「接纳 / 婉拒」对话框，
 *       玩家答复后当回合立即形成结果（入仕或记冷却），不跨回合保留待答状态。
 * 创建日期：2026-11-26
 * 最后修改：2026-11-26
 */

using Sango.Core;
using Sango.Core.Player;

namespace Sango.Render
{
    /// <summary>
    /// 求仕演出：玩家势力的来投确认框。
    ///
    /// 【为什么用 RenderEvent】<see cref="Scenario.Run"/> 只在 RenderEvent 未完成时整帧返回，
    /// GameDialog 本身只关输入、不暂停回合；若直接弹窗，AI 会在后台把回合走完。
    ///
    /// 【为什么自带兜底】演出层已经没有任何超时机制（MaxDuration / OnForceComplete 已移除），
    /// 因此这里用 <see cref="GameDialog.IsDialogAlive"/> 检测"窗口是否真的开出来了"：
    /// 缺 prefab / 无 UI 上下文时 GameDialog.TryAdvance 会丢弃该对话且不回调，
    /// 不检测的话本事件会永远等待，回合推进彻底停摆。检测失败按「本次放弃」收尾：
    /// 不入仕、不记冷却（下月重新判定），也不留下任何跨回合状态。
    ///
    /// 【性能】本事件每月至多 5 个实例；委托在构造时缓存，避免每次 Enter 分配 Action。
    /// </summary>
    public class PersonSeekOfficeEvent : RenderEventBase
    {
        /// <summary>求仕武将</summary>
        public Person person;

        /// <summary>目标势力（武将所在城的归属势力）</summary>
        public Force force;

        /// <summary>接受求仕的城（武将所在城或其主城）</summary>
        public City city;

        /// <summary>对话框建立后的看护计时（秒）</summary>
        float guardTimer;

        /// <summary>复用的确定回调（构造时绑定，避免每次打开对话框分配委托）</summary>
        readonly System.Action acceptAction;

        /// <summary>复用的取消回调</summary>
        readonly System.Action rejectAction;

        /// <summary>构造：缓存两个回调委托。</summary>
        public PersonSeekOfficeEvent()
        {
            acceptAction = OnAccept;
            rejectAction = OnReject;
        }

        /// <summary>
        /// 初始化（RenderEvent 工厂 / 池化复用入口）。
        /// </summary>
        /// <param name="person">求仕武将</param>
        /// <param name="force">目标势力</param>
        /// <param name="city">接受求仕的城</param>
        public void Init(Person person, Force force, City city)
        {
            this.person = person;
            this.force = force;
            this.city = city;
            IsDone = false;
            IsInited = false;
            guardTimer = 0f;
        }

        /// <summary>进入演出：打开「确定 = 接纳 / 取消 = 婉拒」对话框。</summary>
        /// <param name="scenario">当前剧本</param>
        public override void Enter(Scenario scenario)
        {
            guardTimer = 0f;

            if (person == null || force == null || city == null
                || GameDialog.Instance == null)
            {
                IsDone = true;
                return;
            }

            string text = PersonSeekOfficeRules.ComposeOfferText(person, force, city);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ChoosePersonSay, text,
                acceptAction, rejectAction, person);
        }

        /// <summary>每帧检查：对话框没能显示出来时直接放弃本次（无跨回合状态），避免卡住回合。</summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="deltaTime">帧间隔</param>
        /// <returns>是否已完成</returns>
        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsDone)
            {
                guardTimer += deltaTime;
                // 给窗口 1 秒的建立时间；之后既没有对话在显示、也没有回调，就判定为异常
                if (guardTimer > 1f
                    && (GameDialog.Instance == null || !GameDialog.Instance.IsDialogAlive()))
                {
                    Sango.Log.Warning($"*[求仕]{person?.Name} 的求仕对话未能显示，本次放弃（下月重新判定）");
                    IsDone = true;
                }
            }
            return IsDone;
        }

        /// <summary>玩家点「确定」：接纳入仕（当回合立即形成结果）。</summary>
        void OnAccept()
        {
            IsDone = true;

            if (!PersonStateSystem.JoinByPetition(person, force, city))
            {
                // 等待期间剧本事件可能已经改变归属（时局已变）
                PersonSeekOffice.NotifyStale(person, force);
            }

            GameEvent.OnPersonSeekOfficeDecided?.Invoke(person, force, true);
        }

        /// <summary>玩家点「取消」：婉拒并记冷却（当回合立即形成结果）。</summary>
        void OnReject()
        {
            IsDone = true;
            PersonSeekOffice.StartCooldown(person, force);
            Sango.Log.Info($"@人才@[求仕]{person?.Name} -> {force?.Name}（玩家婉拒）");
            GameEvent.OnPersonSeekOfficeDecided?.Invoke(person, force, false);
        }
    }
}
