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
        public List<TalkData> talkData;
        public System.Action talkEndAction;
        public UGUIWindow Window { get; set; }

        public override void OnOpen(params object[] objects)
        {
            dialogData = (DialogData)objects[0];
            sureAction = dialogData.sureAction;
            cancelAction = dialogData.cancelAction;
            content.text = dialogData.content;
            if (btnRect != null)
            {
                Vector2 anchorPos;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(GetComponent<RectTransform>(),
                    dialogData.startPoint, Sango.Core.Game.Instance.UICamera, out anchorPos);

                btnRect.anchoredPosition = anchorPos + new Vector2(-74, 0);
            }
            SetPerson(dialogData.person);
            GameMedia.Instance.PlaySfx(dialogData.sound);
        }

        public void OnSure()
        {
            Close();
            sureAction?.Invoke();
        }

        public void OnCancel()
        {
            Close();
            if(cancelAction == null)
                sureAction?.Invoke();
            else
                cancelAction.Invoke();
        }

        public void StartTalk(List<TalkData> talkData, System.Action talkEndAction)
        {
            this.talkData = talkData;
            this.talkEndAction = talkEndAction;
            NextTalk();
        }

        public void NextTalk()
        {
            TalkData data = talkData[0];
            talkData.RemoveAt(0);
            if (talkData.Count == 0)
                sureAction = talkEndAction;
            else
                sureAction = NextTalk;
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
