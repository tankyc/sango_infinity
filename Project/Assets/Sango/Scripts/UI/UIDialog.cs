using Sango.Manager;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using static Sango.Core.GameDialog;

namespace Sango.UI
{
    /// <summary>
    /// 游戏开始界面
    /// </summary>
    public class UIDialog : UGUIWindow, IDialog
    {
        DialogData dialogData;
        public Text content;
        public System.Action cancelAction { get; set; }
        public System.Action sureAction { get; set; }
        public RectTransform panelRect;
        public RectTransform btnRect;
        public RawImage headImg;
        public Text nameText;

        /// <summary>多句台词（同一窗口逐句显示）；为空表示单句对话</summary>
        public List<TalkData> talkData;
        /// <summary>台词链收尾回调：确定读完最后一句、或中途取消时都会被调用一次</summary>
        public System.Action talkEndAction;

        public UGUIWindow Window { get; set; }

        /// <summary>本条对话在 GameDialog 里的序号，关闭时回传，用于丢弃陈旧回调</summary>
        int m_Seq;
        /// <summary>一次性保护。窗口实例是按窗口名复用的同一个对象，每次打开必须复位</summary>
        bool m_Closed;
        /// <summary>当前显示到第几句（-1 表示不是台词链）</summary>
        int m_LineIndex = -1;

        public override void OnOpen(params object[] objects)
        {
            dialogData = (DialogData)objects[0];
            m_Seq = dialogData.seq;
            m_Closed = false;               // 复用实例：每次打开都要复位
            sureAction = dialogData.sureAction;
            cancelAction = dialogData.cancelAction;
            talkData = null;
            talkEndAction = null;
            m_LineIndex = -1;

            if (btnRect != null)
            {
                Vector2 anchorPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(),
                    dialogData.startPoint, Sango.Core.Game.Instance.UICamera, out anchorPos);

                btnRect.anchoredPosition = anchorPos + new Vector2(-74, 0);
            }

            if (dialogData.talks != null && dialogData.talks.Count > 0)
            {
                // 台词链：内容 / 立绘 / 音效由每句自己带
                StartTalk(dialogData.talks, dialogData.talkEndAction);
            }
            else
            {
                content.text = dialogData.content;
                SetPerson(dialogData.person);
                GameMedia.Instance.PlaySfx(dialogData.sound);
            }
        }

        public void OnSure()
        {
            if (HasMoreLine)
            {
                NextTalk();          // 还有台词：只换内容，**不关窗口**
                return;
            }

            CloseWith(true);
        }

        public void OnCancel()
        {
            // 台词链视为"跳过剩余台词"；单句对话就是取消。
            // 收尾统一在 CloseWith → Close → GameDialog.OnWindowClosed 里执行一次。
            CloseWith(false);
        }

        /// <summary>
        /// 关闭本窗口并**恰好一次**通知 GameDialog。
        /// 顺序：先标记已关闭 → 关窗口 → 执行台词链收尾 → 让 GameDialog 清状态并推进队列。
        /// 收尾放在通知之前，是为了保持"用户回调先执行、它新开的对话排在队尾、随后按 FIFO 继续"。
        /// </summary>
        void CloseWith(bool sure)
        {
            if (m_Closed) return;
            m_Closed = true;

            System.Action tail = talkEndAction;
            talkData = null;
            talkEndAction = null;
            m_LineIndex = -1;

            Close();                 // 关窗口；OnClose 里因 m_Closed 已置位，不会重复通知

            tail?.Invoke();          // 台词链收尾（可能又开新对话，会被排到队尾）
            GameDialog.Instance.OnWindowClosed(m_Seq, sure);
        }

        /// <summary>
        /// 关闭出口兜底。
        ///
        /// 窗口被 Window.Close / CloseAll / Window.DestroyAll 从外部关掉时不会走 OnSure/OnCancel，
        /// 这里补一次通知，否则 GameDialog 的当前数据会一直挂着、队列不再推进
        /// （读档 / 回主菜单之后就点不动了）。
        /// 外部关闭不执行业务回调：那种场合通常是整个剧本在切换，业务回调不该再跑。
        /// </summary>
        public override void OnClose()
        {
            base.OnClose();

            if (m_Closed) return;
            m_Closed = true;
            talkData = null;
            talkEndAction = null;
            m_LineIndex = -1;

            GameDialog.Instance.OnWindowClosed(m_Seq, false, false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Window.DestroyAll 是直接把 GameObject 销毁，不走 Close()，这里也要让状态落地
            if (m_Closed) return;
            m_Closed = true;
            talkData = null;
            talkEndAction = null;
            m_LineIndex = -1;

            GameDialog.Instance.OnWindowClosed(m_Seq, false, false);
        }

        /// <summary>是否还有**下一句**没显示（注意：不是"当前这句是否存在"）</summary>
        bool HasMoreLine
        {
            get { return m_LineIndex >= 0 && talkData != null && m_LineIndex + 1 < talkData.Count; }
        }

        public void StartTalk(List<TalkData> talkData, System.Action talkEndAction)
        {
            this.talkData = talkData != null ? new List<TalkData>(talkData) : null;
            this.talkEndAction = talkEndAction;
            m_LineIndex = -1;

            // 第一句无条件显示：只有一句时 HasMoreLine 为 false，不能拿它当"有没有内容"用
            if (this.talkData != null && this.talkData.Count > 0)
            {
                m_LineIndex = 0;
                ApplyLine(0);
            }
        }

        /// <summary>显示下一句台词</summary>
        public void NextTalk()
        {
            if (!HasMoreLine) return;
            ApplyLine(++m_LineIndex);
        }

        /// <summary>显示第 index 句（内容 + 立绘 + 音效）</summary>
        void ApplyLine(int index)
        {
            if (talkData == null || index < 0 || index >= talkData.Count) return;

            TalkData data = talkData[index];
            SetPerson(data.person);
            content.text = data.text;
            GameMedia.Instance.PlaySfx(data.sound);
            //GameMedia.Instance.PlayBgm(data.bgm);
        }

        public void SetPerson(Person person)
        {
            if (headImg == null || nameText == null) return;
            if (person == null)
            {
                headImg.enabled = false;
                nameText.text = "";
                return;
            }

            headImg.enabled = true;
            headImg.texture = GameRenderHelper.LoadHeadIcon(person.headIconID, 1);
            nameText.text = person.Name;
        }

        public void SetContent(string str)
        {
            content.text = str;
        }

        public void SetSureAction(Action action)
        {
            sureAction = action;
        }

        public void SetCancelAction(Action action)
        {
            cancelAction = action;
        }

        public void Init(string str, Action sure, Action cancel, Vector3 startPoint)
        {
            content.text = str;
            sureAction = sure;
            cancelAction = cancel;
            if (btnRect != null)
            {
                Vector2 anchorPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(),
                    startPoint, Sango.Core.Game.Instance.UICamera, out anchorPos);

                btnRect.anchoredPosition = anchorPos + new Vector2(-74, 0);
            }
        }
    }
}
