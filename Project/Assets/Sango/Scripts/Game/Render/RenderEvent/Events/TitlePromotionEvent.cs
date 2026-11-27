/*
 * 文件名：TitlePromotionEvent.cs
 * 描述：玩家势力君主爵位晋升提示。只做"告知 + 确认"，晋升本身已在
 *       TitlePromotionSystem 中结算完成，不产生任何跨回合待答状态。
 * 创建日期：2026-11-27
 */

using Sango.Core;

namespace Sango.Render
{
    /// <summary>
    /// 君主爵位晋升演出。
    ///
    /// 【为什么是 RenderEvent】GameDialog 只接管输入、不会暂停回合；
    /// 若在月结算里直接 Open，AI/回合会继续在后台推进。RenderEvent 未完成时
    /// Scenario.Run 整帧返回，玩家确认后再继续。
    ///
    /// 【兜底】没有窗口超时机制，用 IsDialogAlive 检测窗口是否真的开出来，
    /// 缺 prefab / 无 UI 上下文时按"本次放弃提示"收尾，不让回合卡死。
    /// </summary>
    public class TitlePromotionEvent : RenderEventBase
    {
        /// <summary>晋升的势力（玩家势力）</summary>
        Force force;

        /// <summary>旧 / 新爵位 Id</summary>
        int oldTitleId;
        int newTitleId;

        /// <summary>旧 / 新爵位名（Enter 前解析好，避免 Update 反复查表）</summary>
        string oldTitleName;
        string newTitleName;

        /// <summary>窗口看护计时（秒）</summary>
        float guardTimer;

        /// <summary>复用的关闭回调</summary>
        readonly System.Action closeAction;

        public TitlePromotionEvent()
        {
            closeAction = OnClosed;
        }

        /// <summary>
        /// 初始化提示内容。
        /// </summary>
        /// <param name="force">晋升势力（只对玩家势力创建本事件）</param>
        /// <param name="oldTitleId">旧爵位 Id</param>
        /// <param name="newTitleId">新爵位 Id</param>
        public void Init(Force force, int oldTitleId, int newTitleId)
        {
            this.force = force;
            this.oldTitleId = oldTitleId;
            this.newTitleId = newTitleId;
            oldTitleName = null;
            newTitleName = null;
            guardTimer = 0f;
            IsDone = false;
            IsInited = false;
        }

        /// <summary>进入演出：打开爵位晋升提示。</summary>
        /// <param name="scenario">当前剧本</param>
        public override void Enter(Scenario scenario)
        {
            guardTimer = 0f;
            if (force == null || GameDialog.Instance == null)
            {
                IsDone = true;
                return;
            }

            if (scenario != null && scenario.CommonData != null && scenario.CommonData.Titles != null)
            {
                Title oldTitle = scenario.CommonData.Titles.Get(oldTitleId);
                Title newTitle = scenario.CommonData.Titles.Get(newTitleId);
                if (oldTitle != null) oldTitleName = oldTitle.Name;
                if (newTitle != null) newTitleName = newTitle.Name;
            }
            if (string.IsNullOrEmpty(oldTitleName)) oldTitleName = oldTitleId.ToString();
            if (string.IsNullOrEmpty(newTitleName)) newTitleName = newTitleId.ToString();

            string governorName = force.mGovernor != null ? force.mGovernor.ColorName : force.Name;
            string text = $"【爵位晋升】\n{force.Name ?? "势力"}君主{governorName}的爵位由"
                + $"【{oldTitleName}】晋升为【{newTitleName}】。\n"
                + "君主爵位已提升，可任命的官职范围随之扩大。";

            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, text,
                closeAction, closeAction, force.mGovernor);
        }

        /// <summary>窗口关闭（确定 / 取消都视为已知晓）。</summary>
        void OnClosed()
        {
            IsDone = true;
        }

        /// <summary>每帧看护：窗口没能打开时直接放弃本次提示，避免回合卡死。</summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="deltaTime">帧间隔</param>
        /// <returns>是否完成</returns>
        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsDone)
            {
                guardTimer += deltaTime;
                if (guardTimer > 1f
                    && (GameDialog.Instance == null || !GameDialog.Instance.IsDialogAlive()))
                {
                    Sango.Log.Warning("*[爵位] 晋升提示窗口未能显示，本次仅记录日志");
                    IsDone = true;
                }
            }
            return IsDone;
        }
    }
}
