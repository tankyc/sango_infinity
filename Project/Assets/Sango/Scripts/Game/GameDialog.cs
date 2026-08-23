using Sango.Render;
using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core
{
    public class GameDialog : Singleton<GameDialog>
    {
        public enum DialogStyle
        {
            Normal,
            ChoosePersonSay,
            Window,
            ClickPersonSay,
            ClickSay
        }

        public struct DialogData
        {
            public DialogStyle style;
            public System.Action cancelAction;
            public System.Action sureAction;
            public string content;
            public Person person;
            public Vector3 startPoint;
            public int sound;
            public int bgm;
        }

        public List<DialogData> dialogDatas = new List<DialogData>();
        Window.WindowInterface windowInterface;

        public void Open(DialogStyle style, string content, System.Action sureAction, System.Action cancelAction,
            Person person, Vector3 startPoint, int sound = 0, int bgm = 0)
        {
            DialogData dialogData = new DialogData
            {
                style = style,
                sureAction = () =>
                {
                    sureAction?.Invoke();
                    Next();
                },
                content = content,
                cancelAction = () =>
                {
                    cancelAction?.Invoke();
                    Next();
                },
                person = person,
                startPoint = startPoint,
                sound = sound,
                bgm = bgm
            };
            dialogDatas.Add(dialogData);
            if (windowInterface == null)
                Next();
        }

        public void Open(DialogStyle style, string content, System.Action sureAction, System.Action cancelAction,
            Person person = null, int sound = 0, int bgm = 0)
        {
            Open(style, content, sureAction, cancelAction, person, Input.mousePosition, sound, bgm);
        }
        public void Open(DialogStyle style, string content, System.Action sureAction, int sound = 0, int bgm = 0)
        {
            Open(style, content, sureAction, null, null, Input.mousePosition, sound, bgm);
        }
        public void Open(DialogStyle style, string content, System.Action sureAction, Person person, int sound = 0, int bgm = 0)
        {
            Open(style, content, sureAction, null, person, Input.mousePosition, sound, bgm);
        }
        void Next()
        {
            windowInterface = null;
            if (dialogDatas.Count == 0)
            {
                GameController.Instance.Enabled = true;
                return;
            }

            DialogData dialogData = dialogDatas[0];
            dialogDatas.RemoveAt(0);
            string windowName = "window_dialog";
            switch (dialogData.style)
            {
                case DialogStyle.ChoosePersonSay:
                    windowName = "window_dialog2"; break;
                case DialogStyle.Window:
                    windowName = "window_dialog3"; break;
                case DialogStyle.ClickPersonSay:
                    windowName = "window_dialog4"; break;
                case DialogStyle.ClickSay:
                    windowName = "window_dialog5"; break;
            }

            windowInterface = Window.Instance.Open(windowName, dialogData);
            //if (windowInterface.ugui_instance == null)
            //{
            //    windowInterface = null;
            //    Next();
            //    return;
            //}

            GameController.Instance.Enabled = false;
        }

        public interface IDialog
        {
            UGUIWindow Window { get; set; }
            void StartTalk(List<TalkData> talkData, System.Action talkEndAction);
            void SetPerson(Person person);
            void NextTalk();
            void SetContent(string str);
            void SetSureAction(System.Action action);
            void SetCancelAction(System.Action action);
            void Init(string str, System.Action sure, System.Action cancel, Vector3 startPoint);
            System.Action cancelAction { get; set; }
            System.Action sureAction { get; set; }
            void Close();
            void Open();

        }
        public static IDialog CurInstance;


        public struct TalkData
        {
            public string text;
            public Person person;
            public int sound;
            public int bgm;
        }

        public static void StartTalk(List<TalkData> talk_content, System.Action endAction)
        {
            for(int i = 0; i < talk_content.Count; i++)
            {
                TalkData talkData = talk_content[i];
                if(i == talk_content.Count - 1)
                    Instance.Open(DialogStyle.ClickPersonSay, talkData.text, () => { endAction?.Invoke(); }, talkData.person, talkData.sound, talkData.bgm);
                else
                    Instance.Open(DialogStyle.ClickPersonSay, talkData.text, () => { }, talkData.person, talkData.sound, talkData.bgm);
            }
        }
    }
}
