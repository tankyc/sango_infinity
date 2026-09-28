/*
 * 文件名：UIEventPicture.cs
 * 描述：剧本事件图文演出窗口 —— 铺满屏幕展示一张 CG（或序列帧动图）+ 文字，等玩家点击后继续
 * 创建日期：2026-09-26
 * 最后修改：2026-09-26
 */

using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 剧本事件图文演出的入参。
    /// 由 <c>EventRunner</c> 的 <c>Op: Picture</c> 从 JSON 组装后传给 <see cref="UIEventPicture"/>。
    /// </summary>
    public class EventPictureArgs
    {
        /// <summary>CG 资源名（可省扩展名，如 "taoyuan"；也可写完整相对路径）</summary>
        public string Image;

        /// <summary>标题（可空）</summary>
        public string Title;

        /// <summary>正文（可空，支持多行）</summary>
        public string Text;

        /// <summary>
        /// 最少展示时长（秒）。演出未走完前不允许关闭，防止玩家手快点掉。
        /// 0 = 用入场动画时长；动画也没有则为 0（立即可关）。
        /// </summary>
        public float MinSeconds;

        /// <summary>展示期间是否暂停 BGM（关闭时自动恢复）</summary>
        public bool PauseBgm;
    }

    /// <summary>
    /// 剧本事件图文演出窗口。
    ///
    /// 骨架照 <see cref="UIForceDestroyed"/>（势力灭亡演出）改 —— 那一版本本身就是
    /// "事件演出窗口"的范例：全屏遮罩 + 入场 Animation + <see cref="UIImageAnimation"/> 序列帧
    /// + 音效 + 演完才允许关闭 + 关闭时回调驱动方继续。
    ///
    /// 【为什么不拿 window_skill_crit 改】那个只有一张静态 RawImage + SetNativeSize()，
    /// 没有演出节奏、没有关闭流程、也没有序列帧能力 —— 做"事件图文"等于从零补一遍。
    ///
    /// 【与旧体系的关系】本窗口由**新体系**（<c>EventRunner</c>）驱动，**不走 RenderEvent 队列**。
    /// 回合推进由 <c>Scenario.Run()</c> 里的 <c>ScenarioEventManager.IsPlayingNow</c> 挡住，
    /// 不依赖旧体系，避免两套体系互相抢窗口（这正是上个阶段修掉的那类冲突）。
    /// </summary>
    public class UIEventPicture : UGUIWindow
    {
        /// <summary>窗口名。Op: Picture / Op: PictureClose 都用它，避免两边各写一份字符串</summary>
        public const string WindowName = "window_event_picture";

        /// <summary>CG 节点（RawImage，可放任意贴图）</summary>
        public RawImage picture;

        /// <summary>备用节点（Image，走 Sprite）。prefab 上只填用到的那个</summary>
        public Image pictureSprite;

        /// <summary>标题文本（可空）</summary>
        public Text title;

        /// <summary>正文文本（可空）</summary>
        public Text content;

        /// <summary>入场动画（可空）</summary>
        public Animation animation;

        /// <summary>关闭按钮（可空；也可以直接在 prefab 上给按钮挂 <see cref="ClickClose"/>)</summary>
        public Button closeButton;

        /// <summary>当前是否允许关闭（演出未走完时不允许）</summary>
        bool canClose;

        /// <summary>是否由本窗口暂停了 BGM（关闭时必须恢复，否则整局静音）</summary>
        bool pausedBgm;

        /// <summary>本次是否真的显示了图（没显示时不必按 CG 的节奏干等）</summary>
        bool hasPicture;

        public override void OnOpen(params object[] ps)
        {
            base.OnOpen(ps);

            // 窗口是**复用**的（Close 只做 SetActive(false)），所以每次打开都要把状态清干净
            CancelInvoke();
            canClose = false;
            hasPicture = false;

            EventPictureArgs args = (ps != null && ps.Length > 0) ? ps[0] as EventPictureArgs : null;
            if (args == null)
            {
                // 参数缺失说明调用方写错了。但绝不能留一个关不掉的黑屏把玩家卡死：
                // 直接把 canClose 放开，让玩家点一下就过。
                Log.Error("图文演出窗口缺少参数（EventPictureArgs），已按“可立即关闭”处理");
                return;
            }

            ApplyPicture(args.Image);
            ApplyText(args);
            StartAnimation(args);
            ApplyBgm(args);
        }

        /// <summary>加载并显示 CG。找不到时保持占位 —— 不黑屏、不报错中断，事件照常往下走。</summary>
        /// <param name="imageName">资源名</param>
        void ApplyPicture(string imageName)
        {
            if (string.IsNullOrEmpty(imageName)) return;

            Texture tex = GameRenderHelper.LoadEventPicture(imageName);
            if (tex == null)
            {
                Log.Warning($"图文演出的 CG 未找到：{imageName}（查找目录 {GameRenderHelper.EventPicturePath}），本次只显示文字");
                return;
            }

            hasPicture = true;

            if (picture != null)
            {
                picture.texture = tex;
                picture.enabled = true;
            }

            if (pictureSprite != null)
            {
                // RawImage 与 Image 是两套节点：prefab 上填了哪个就用哪个，另一个关掉免得叠图
                pictureSprite.enabled = false;
            }
        }

        /// <summary>填充标题与正文</summary>
        /// <param name="args">入参</param>
        void ApplyText(EventPictureArgs args)
        {
            if (title != null) title.text = args.Title ?? string.Empty;
            if (content != null) content.text = args.Text ?? string.Empty;
        }

        /// <summary>播入场动画，并决定多久之后才允许关闭</summary>
        /// <param name="args">入参</param>
        void StartAnimation(EventPictureArgs args)
        {
            if (animation != null) animation.Play();

            // 没图也没字 —— 没有任何可看的，别让玩家对着空屏等着点
            if (!hasPicture && string.IsNullOrEmpty(args.Title) && string.IsNullOrEmpty(args.Text))
            {
                canClose = true;
                return;
            }

            float hold = args.MinSeconds;
            if (hold <= 0f && animation != null && animation.clip != null)
                hold = animation.clip.length;

            if (hold > 0f) Invoke(nameof(EnableClose), hold);
            else canClose = true;
        }

        /// <summary>按需暂停 BGM</summary>
        /// <param name="args">入参</param>
        void ApplyBgm(EventPictureArgs args)
        {
            if (!args.PauseBgm || GameMedia.Instance == null) return;

            GameMedia.Instance.PauseBgm();
            pausedBgm = true;
        }

        void EnableClose()
        {
            canClose = true;
        }

        /// <summary>点击关闭（在 prefab 的关闭按钮上直接挂这个方法）</summary>
        public void ClickClose()
        {
            if (canClose) Close();
        }

        public override void OnClose()
        {
            CancelInvoke();

            if (pausedBgm && GameMedia.Instance != null)
            {
                GameMedia.Instance.ResumeBgm();
                pausedBgm = false;
            }

            // BGM 必须在 base 之前恢复：base.OnClose 会触发 OnCloseAction，
            // 驱动方可能立刻开下一个窗口并自己接管 BGM。
            base.OnClose();
        }

        /// <summary>
        /// 兜底：被外部直接销毁（读档 / 回主菜单 / 退出 Play 模式）时 OnClose 不一定走，
        /// 这里保证 BGM 不会永远停在暂停状态。
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (pausedBgm && GameMedia.Instance != null)
            {
                GameMedia.Instance.ResumeBgm();
                pausedBgm = false;
            }
        }
    }
}
