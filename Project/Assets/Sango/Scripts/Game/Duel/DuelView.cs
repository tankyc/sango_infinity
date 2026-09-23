/*
 * 文件名：DuelView.cs
 * 描述：单挑表现层接口与默认空实现
 *
 * 设计说明：
 *   · 逻辑层（Duel / DuelPhase / DuelAI）只依赖 IDuelView，完全不关心是卡牌、2D 还是 3D 表现。
 *   · 表现层实现（例如 CardDuelView）负责把逻辑层抛出的"表现请求"翻译成实际的动画/UI 操作，
 *     并按自己的节奏（动画播放中）通过 DuelIsAnimating 回告逻辑层，让逻辑层停在当前阶段等待。
 *   · DuelViewBase 提供"无表现层"的空实现：此时 Duel.View = false，
 *     逻辑层直接瞬时结算，不调用本接口的任何方法，可用于 AI 推演与单元测试。
 *   · 新增表现方式（如 3D 单挑）只需再实现一个 IDuelView，不需要改动任何单挑逻辑。
 */

namespace Sango.Core.Duel
{
    /// <summary>
    /// 单挑表现层接口。对应 C++ Engine。
    /// 逻辑层通过本接口驱动表现，并通过本接口回收玩家的操作输入。
    /// </summary>
    public interface IDuelView
    {
        #region 表现：整体流程

        /// <summary>是否正在播放动画（返回 true 时逻辑层会停在当前阶段等待）</summary>
        bool DuelIsAnimating(Duel duel);

        /// <summary>消息框是否可见（可见时逻辑层暂停推进）</summary>
        bool DuelIsMessageBoxVisible(Duel duel);

        /// <summary>重置动画队列</summary>
        void DuelResetAnim(Duel duel);

        /// <summary>刷新合数显示</summary>
        void DuelUpdateBlowCounter(Duel duel);

        /// <summary>播放开场</summary>
        void DuelOpening(Duel duel);

        /// <summary>播放结束</summary>
        void DuelClosing(Duel duel);

        /// <summary>播放平局</summary>
        void DuelDraw(Duel duel);

        /// <summary>播放退却</summary>
        void DuelRetreat(Duel duel);

        #endregion

        #region 表现：状态变化

        /// <summary>播放"合数"动画</summary>
        void DuelBlowAnim(Duel duel, Duel.BlowAnim[] queue, int count);

        /// <summary>播放体力动画</summary>
        void DuelHpAnim(Duel duel, Duel.HPAnim[] queue, int count);

        /// <summary>播放斗志动画</summary>
        void DuelSpiritAnim(Duel duel, Duel.SpiritAnim[] queue, int count);

        /// <summary>播放登场动画</summary>
        void DuelJoin(Duel duel, int team, int oldChara);

        /// <summary>播放交替动画</summary>
        void DuelSwitch(Duel duel, int team, int oldChara);

        /// <summary>切换当前武将</summary>
        void DuelChangeCurrentChara(Duel duel, int team);

        /// <summary>播放一击必杀</summary>
        void DuelFtk(Duel duel, int team, int chara, int ftkType, int opponentTeam, int opponentChara);

        /// <summary>
        /// 一击必杀**判定**演出：寒暄之后、真正结算之前播一次（对应的是一击必杀的判定阶段）。
        /// ftkTeam &gt;= 0 表示判定命中，紧接着会调到 DuelFtk 播必杀演出（表现层应让两卡停留在中间承接）；
        /// ftkTeam &lt; 0 表示没命中，表现层把卡牌归位，随后正常进入回合。
        /// 逻辑层会停在原地等这段表演播完（DuelIsAnimating），所以播多久由表现层决定。
        /// </summary>
        void DuelFtkJudge(Duel duel, int ftkTeam, int ftkType);

        /// <summary>清除无敌状态显示</summary>
        void DuelResetInvulnerable(Duel duel, int team);

        /// <summary>清除增益状态显示</summary>
        void DuelResetBuff(Duel duel, int team, int buff);

        /// <summary>
        /// 必杀即将释放：表现层在这里播一句必杀台词（每个必杀只会调一次）。
        /// 逻辑层随后会停在原地等对话框关掉（DuelIsMessageBoxVisible），
        /// 也就是"关闭对话框之后才真正释放必杀技"。
        /// </summary>
        void DuelSpecialBegin(Duel duel, int team, int chara, int special);

        #endregion

        #region 表现：播放控制

        /// <summary>暂停</summary>
        void DuelStop(Duel duel);

        /// <summary>继续</summary>
        void DuelPlay(Duel duel);

        /// <summary>停止按钮是否被按下（玩家请求暂停）</summary>
        bool DuelIsStopButtonPushed(Duel duel);

        /// <summary>继续按钮是否被按下（玩家请求恢复播放）</summary>
        bool DuelIsPlayButtonPushed(Duel duel);

        #endregion

        #region 输入：玩家操作

        /// <summary>获取玩家选择的行动方针（DuelStance，-1 表示尚未选择）</summary>
        int DuelGetStance(Duel duel, int team);

        /// <summary>获取玩家选择的必杀（DuelSpecial，-1 表示尚未选择）</summary>
        int DuelGetSpecial(Duel duel, int team);

        /// <summary>获取玩家选择的交替武将（-1 表示尚未选择）</summary>
        int DuelGetSwitchingChara(Duel duel, int team);

        /// <summary>必杀按钮是否被按下</summary>
        bool DuelIsSpecialButtonPushed(Duel duel, int team);

        /// <summary>必杀取消按钮是否被按下</summary>
        bool DuelIsSpecialCancelButtonPushed(Duel duel);

        /// <summary>
        /// 玩家是否按了结算画面的「离开」。
        /// 单挑打完不再自动收场：表现层先播胜利者台词，底部按钮变「离开」，
        /// 逻辑层/管理器等到这里返回 true 才真正结束并关闭窗口。
        /// </summary>
        bool DuelIsLeavePushed(Duel duel);

        /// <summary>弹出是否确认对话框</summary>
        bool YesNo(string text);

        #endregion
    }

    /// <summary>
    /// 表现层默认空实现：所有方法均为空操作，所有输入均返回"未选择"。
    /// 使用本实现时 Duel.View 必须为 false，逻辑层将瞬时结算整场单挑。
    /// </summary>
    public class DuelViewBase : IDuelView
    {
        public virtual bool DuelIsAnimating(Duel duel) { return false; }
        public virtual bool DuelIsMessageBoxVisible(Duel duel) { return false; }
        public virtual void DuelResetAnim(Duel duel) { }
        public virtual void DuelUpdateBlowCounter(Duel duel) { }
        public virtual void DuelOpening(Duel duel) { }
        public virtual void DuelClosing(Duel duel) { }
        public virtual void DuelDraw(Duel duel) { }
        public virtual void DuelRetreat(Duel duel) { }
        public virtual void DuelBlowAnim(Duel duel, Duel.BlowAnim[] queue, int count) { }
        public virtual void DuelHpAnim(Duel duel, Duel.HPAnim[] queue, int count) { }
        public virtual void DuelSpiritAnim(Duel duel, Duel.SpiritAnim[] queue, int count) { }
        public virtual void DuelJoin(Duel duel, int team, int oldChara) { }
        public virtual void DuelSwitch(Duel duel, int team, int oldChara) { }
        public virtual void DuelChangeCurrentChara(Duel duel, int team) { }
        public virtual void DuelFtk(Duel duel, int team, int chara, int ftkType, int opponentTeam, int opponentChara) { }
        public virtual void DuelFtkJudge(Duel duel, int ftkTeam, int ftkType) { }
        public virtual void DuelResetInvulnerable(Duel duel, int team) { }
        public virtual void DuelResetBuff(Duel duel, int team, int buff) { }
        public virtual void DuelSpecialBegin(Duel duel, int team, int chara, int special) { }
        public virtual void DuelStop(Duel duel) { }
        public virtual void DuelPlay(Duel duel) { }
        public virtual bool DuelIsStopButtonPushed(Duel duel) { return false; }
        public virtual bool DuelIsPlayButtonPushed(Duel duel) { return false; }
        public virtual int DuelGetStance(Duel duel, int team) { return -1; }
        public virtual int DuelGetSpecial(Duel duel, int team) { return -1; }
        public virtual int DuelGetSwitchingChara(Duel duel, int team) { return -1; }
        public virtual bool DuelIsSpecialButtonPushed(Duel duel, int team) { return false; }
        public virtual bool DuelIsSpecialCancelButtonPushed(Duel duel) { return false; }
        public virtual bool DuelIsLeavePushed(Duel duel) { return true; }
        public virtual bool YesNo(string text) { return true; }
    }
}
