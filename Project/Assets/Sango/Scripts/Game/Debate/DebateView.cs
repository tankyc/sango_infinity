/*
 * 文件名：DebateView.cs
 * 描述：舌战表现层接口与默认空实现
 *
 * 设计说明（与单挑 DuelView.cs 同构，便于两种玩法共用一套经验）：
 *   · 逻辑层（Debate / DebatePhase / DebateAI）只依赖 IDebateView，不关心表现是卡牌、2D 还是 3D。
 *   · 表现层实现（当前为 UI/Debate/CardDebateView）把逻辑层抛出的"表现请求"翻译成实际的动画/UI 操作，
 *     并用自己的节奏通过 DebateIsAnimating / DebateIsMessageBoxVisible 回告逻辑层，
 *     让逻辑层停在当前步骤等待（见 Debate.IsIdle 与 Debate.Update）。
 *   · 无表现层时（Debate.View = false，纯逻辑推演 / 单元测试）逻辑层不会调用本接口的任何方法，
 *     数值立即结算，可用于 AI 推演与批量测试。
 *   · 新增表现方式（例如 3D 舌战）只需再实现一个 IDebateView，不需要改动任何舌战逻辑。
 *
 * 对应 C++：Engine（s11_sys_debate.h 中 Debate 持有的 engine_ 指针），
 *           C++ 里所有回调都被 if (view_) 门控，本接口承接的就是那批回调。
 *
 * 【重要】原 C++ 阶段代码里被 #if s11_removed 移除的空步骤（Opening step1/2、Play step1/3/4/6/8、
 *         Damage step0、Closing step1/2 等）就是当年的"等待动画"步骤。本工程不恢复这些空转步骤，
 *         改为由 Debate.Update → IsIdle → DebateIsAnimating 统一节流，等价且更干净。
 */

namespace Sango.Core.Debate
{
    /// <summary>
    /// 舌战表现层接口。对应 C++ Engine。
    /// 逻辑层通过本接口驱动表现，并通过本接口回收玩家的操作输入。
    /// </summary>
    public interface IDebateView
    {
        #region 表现：节奏回告（逻辑层据此在阶段内等待）

        /// <summary>
        /// 是否正在播放表现（返回 true 时逻辑层会停在当前步骤等待）。
        /// 除了动画，凡是"玩家还没看完"的界面（例如结算画面尚未关闭）也应返回 true。
        /// </summary>
        bool DebateIsAnimating(Debate debate);

        /// <summary>是否有消息框/对白正在显示（显示期间逻辑层暂停推进）</summary>
        bool DebateIsMessageBoxVisible(Debate debate);

        #endregion

        #region 表现：整体流程

        /// <summary>播放开场</summary>
        void DebateOpening(Debate debate);

        /// <summary>播放一击必杀（先制必胜，直接进入结算）</summary>
        void DebateFtk(Debate debate);

        /// <summary>播放结束（结算画面）</summary>
        void DebateClosing(Debate debate);

        /// <summary>愤怒计时结束（激昂状态消失）</summary>
        void DebateAngerEnd(Debate debate, int team);

        #endregion

        #region 表现：出牌与卡牌效果

        /// <summary>播放出牌（某方打出第 index 张手牌）</summary>
        void DebatePlayCard(Debate debate, int team, int index);

        /// <summary>播放平局（双方同牌同威，各自增加压力）</summary>
        void DebateAttackDraw(Debate debate, int stressDamage);

        /// <summary>播放大喝效果</summary>
        void DebateShout(Debate debate, int team, int hpDamage, int stressDamage);

        /// <summary>
        /// 播放话题卡效果。
        /// reflected 表示这张牌实际打到了自己（对手用诡辩反弹回来）。
        /// </summary>
        void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected);

        /// <summary>播放再考（重抽手牌）</summary>
        void DebateRethink(Debate debate, int team);

        /// <summary>播放无视效果</summary>
        void DebateIgnore(Debate debate, int team, int stressDamage);

        /// <summary>
        /// 播放镇静效果。
        /// reflected 表示这张牌实际作用到了自己（对手用诡辩反弹回来）。
        /// </summary>
        void DebateCompose(Debate debate, int team, int stressDamage, bool reflected);

        /// <summary>
        /// 播放激昂效果。
        /// reflected 表示这张牌实际作用到了自己（对手用诡辩反弹回来）。
        /// </summary>
        void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected);

        #endregion

        #region 表现：愤怒（激昂）

        /// <summary>愤怒触发（压力满，触发方即将进入激昂；card 为用于抵消的话术卡）</summary>
        void DebateAngerTrigger(Debate debate, int team, int card);

        /// <summary>播放莽撞性格的激昂爆发</summary>
        void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage);

        /// <summary>播放胆小性格的连续攻击（comboIndex 为第几张，从 0 起）</summary>
        void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex);

        #endregion

        #region 输入：玩家操作

        /// <summary>获取玩家选择的手牌下标（-1 表示尚未选择；逻辑层会停在出牌阶段等待）</summary>
        int DebateSelectCard(Debate debate, int team);

        /// <summary>获取玩家选择的会心类型（DebateCritical，-1 表示尚未选择）</summary>
        int DebateSelectCritical(Debate debate, int team);

        /// <summary>弹出是否确认对话框（无表现层时默认同意）</summary>
        bool YesNo(string text);

        #endregion
    }

    /// <summary>
    /// 表现层默认空实现：所有方法均为空操作，所有输入均返回"未选择"。
    /// 使用本实现时 Debate.View 必须为 false（或让 DebateIsAnimating 恒为 false），
    /// 逻辑层将瞬时结算整场舌战。可用于 AI 推演与单元测试。
    /// </summary>
    public class DebateViewBase : IDebateView
    {
        public virtual bool DebateIsAnimating(Debate debate) { return false; }
        public virtual bool DebateIsMessageBoxVisible(Debate debate) { return false; }
        public virtual void DebateOpening(Debate debate) { }
        public virtual void DebateFtk(Debate debate) { }
        public virtual void DebateClosing(Debate debate) { }
        public virtual void DebateAngerEnd(Debate debate, int team) { }
        public virtual void DebatePlayCard(Debate debate, int team, int index) { }
        public virtual void DebateAttackDraw(Debate debate, int stressDamage) { }
        public virtual void DebateShout(Debate debate, int team, int hpDamage, int stressDamage) { }
        public virtual void DebateTopic(Debate debate, int team, int card, int hpDamage, int stressDamage, bool reflected) { }
        public virtual void DebateRethink(Debate debate, int team) { }
        public virtual void DebateIgnore(Debate debate, int team, int stressDamage) { }
        public virtual void DebateCompose(Debate debate, int team, int stressDamage, bool reflected) { }
        public virtual void DebateAgitate(Debate debate, int team, int stressDamage, bool reflected) { }
        public virtual void DebateAngerTrigger(Debate debate, int team, int card) { }
        public virtual void DebateAngerReckless(Debate debate, int team, int hpDamage, int stressDamage) { }
        public virtual void DebateAngerTimid(Debate debate, int team, int hpDamage, int stressDamage, int comboIndex) { }
        public virtual int DebateSelectCard(Debate debate, int team) { return -1; }
        public virtual int DebateSelectCritical(Debate debate, int team) { return -1; }
        public virtual bool YesNo(string text) { return true; }
    }
}
