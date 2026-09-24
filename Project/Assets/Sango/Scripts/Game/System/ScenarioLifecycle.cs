using Sango.Core.Player;
using Sango.Render;

namespace Sango.Core
{
    /// <summary>
    /// 剧本生命周期编排：读档 / 回主菜单时必须走一遍的统一收尾。
    ///
    /// 为什么需要它：<see cref="Scenario.OnGameShutdown"/> 只关掉剧本自身（数据集合 + 它自己订的几个事件），
    /// 而下面这些"剧本外的常驻对象"会留着上一次开局的痕迹，于是第二次开局出现幽灵行为
    /// （旧事件被续播、事件播两遍、AI 参数不更新…）。这里用一份**显式清单**逐个复位。
    ///
    /// 顺序有讲究，别随意调换：先清表现层队列，再出栈系统（触发对称退订），最后才收剧本自身。
    ///
    /// 关于 GameEvent 的静态委托（第 9 步）：
    ///   不再靠"每个类自己写对称退订"来防泄漏 —— 那是抓漏，成本高且会反复。
    ///   改为在**开局前**拍一份基线（<see cref="GameEventBaseline"/>，含 Game.Init 里订的
    ///   应用级订阅：GameMedia / AudioManager / ModManager 等），收尾时把每个事件委托
    ///   **整体替换回基线**：基线里有的保留、没有的一律移除，且天然去重。
    ///   这样"应用级订阅不会被误清"与"剧本级残留一定会被清"同时成立。
    ///
    /// 刻意**不做**的一件事：
    ///   · 不做 Window.DestroyAll：<c>CloseAll</c> 已经够用，UGUIWindow.OnClose 是对称退订的，
    ///     DestroyAll 只会让每次读档白白重建一遍预制件。
    /// </summary>
    public static class ScenarioLifecycle
    {
        /// <summary>关闭当前剧本：读档与回主菜单都从这里走。</summary>
        public static void BeginShutdown()
        {
            // 0) 诊断快照（编辑器 / 开发包才有输出，见 GameEventDiagnostics.LogEnabled）：
            //    拍下"收尾前"的订阅全景。它与**上一次同标签**快照对比，就能看出上一次收尾
            //    有没有漏掉退订（两次 shutdown-begin 的事件订阅数不一致 = 有残留）。
            GameEventDiagnostics.Snapshot("shutdown-begin");

            // 1) 表现层队列：必须最先清。旧剧本的事件若留在队列里，新剧本第一次 Run 会"续播"它
            //    （CurEvent 的 IsInited 已经是 true，会跳过 Enter 直接 Update 旧对象）。
            RenderEvent.Instance.Reset();

            // 2) 系统出栈：GameSystem 家族在 OnDestroy 里做对称退订，
            //    不调用 Done() 时它们会带着上一个剧本的订阅活到下一次开局。
            GameSystemManager.Instance.Done();

            // 3) 对话队列 + 输入开关复位（Enabled 可能停在 false：窗口开不出来等路径）
            GameDialog.Instance.Reset();

            // 4) 消息列表（退订由系统自身生命周期负责，这里只清数据）
            PlayerMessage message = GameSystem.GetSystem<PlayerMessage>();
            if (message != null)
                message.ClearMessages();

            // 5) 输入残留（与"安卓切后台恢复"共用同一个复位方法）
            GameController.Instance.ResetInputState();

            // 6) 剧本自身收尾：End + Clear + Cur = null
            Scenario current = Scenario.Cur;
            if (current != null)
                current.OnGameShutdown();

            // 7) AI 参数重新加载（含 Mod 覆盖），切剧本后才会按新剧本生效
            AIConfig.Reset();

            // 8) 跨剧本累加器
            // 注意：类型要写全名——Sango.Core.Player 同时是命名空间，直接写 Player 会被当成命名空间
            Sango.Core.Player.Player player = GameSystem.GetSystem<Sango.Core.Player.Player>();
            if (player != null)
                player.currentTurnCount = 0;

            // 9) 事件基线兜底还原：把 GameEvent 的静态订阅整体还原回"开局前"（见 GameEventBaseline）。
            //    前面各步是"正常退订"，这一步兜住所有没退干净 / 重复的订阅，
            //    因此不必再逐个类去抓漏。返回值为被移除的残留订阅数，正常应接近 0。
            //    放在最后一步：让各系统先按自己的生命周期正常退订，兜底只处理剩下的。
            GameEventBaseline.Restore();

            // 10) 收尾后的快照：正常情况下应与下一次的 shutdown-begin 完全一致（即"无残留"）。
            GameEventDiagnostics.Snapshot("shutdown-end");
        }
    }
}
