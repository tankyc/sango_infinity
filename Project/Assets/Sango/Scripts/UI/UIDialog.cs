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
        public Text content;
        public System.Action cancelAction { get; set; }
        public System.Action sureAction { get; set; }
        public RectTransform panelRect;
        public RectTransform btnRect;
        public RawImage headImg;
        public Text nameText;
        public List<TalkData> talkData;
        public System.Action talkEndAction;

        public override void OnOpen(params object[] objects)
        {
            string _content = "";
            System.Action _sureAction = null;
            System.Action _cancelAction = null;
            Vector3 _startPoint = Vector3.zero;
            if (objects.Length > 0)
                _content = objects[0] as string;
            if (objects.Length > 1)
                _sureAction = objects[1] as System.Action;
            if (objects.Length > 2)
                _cancelAction = objects[2] as System.Action;
            if (objects.Length > 3)
                _startPoint = (Vector3)objects[3];
            Init(_content, _sureAction, _cancelAction, _startPoint);
        }

        public void OnSure()
        {
            sureAction?.Invoke();
        }

        public void OnCancel()
        {
            cancelAction?.Invoke();
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
