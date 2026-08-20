using Sango.Render;
using Sango.UI;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core
{
    public class GameDialog
    {
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

        public enum DialogStyle
        {
            Normal,
            ChoosePersonSay,
            Window,
            ClickPersonSay,
            ClickSay
        }
        public struct TalkData
        {
            public string text;
            public Person person;
            public int sound;
            public int bgm;
        }

        public static void Close()
        {
            if (CurInstance != null)
            {
                GameMedia.Instance.PlaySfx(4);
                CurInstance.Close();
            }
            GameController.Instance.Enabled = true;
            CurInstance = null;
        }
        public static void Close(IDialog uIDialog)
        {
            if (uIDialog == CurInstance)
            {
                GameMedia.Instance.PlaySfx(4);
                uIDialog.Close();
            }
            CurInstance = null;
            GameController.Instance.Enabled = true;
        }


        public static IDialog StartTalk(List<TalkData> talk_content, System.Action endAction)
        {
            string windowName = "window_dialog4";
            IDialog uIDialog = Open(windowName, "", null, Input.mousePosition);
            uIDialog.StartTalk(talk_content, endAction);
            return uIDialog;
        }

        public static IDialog Open(string content, System.Action sureAction)
        {
            return Open(content, sureAction, Input.mousePosition);
        }

        public static IDialog Open(DialogStyle dialogStyle, string content, System.Action sureAction)
        {
            string windowName = "window_dialog";
            switch (dialogStyle)
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
            return Open(windowName, content, sureAction, Input.mousePosition);
        }

        public static IDialog Open(string content, System.Action sureAction, Vector3 startPoint)
        {
            return Open("window_dialog", content, sureAction, startPoint);
        }

        public static IDialog Open(string windowName, string content, System.Action sureAction, Vector3 startPoint)
        {
            Window.WindowInterface windowInterface = Window.Instance.Open(windowName, content, sureAction, (System.Action)Close, startPoint);
            if (windowInterface.ugui_instance == null)
                return null;

            IDialog uIDialog = windowInterface.ugui_instance.GetComponent<IDialog>();
            if (uIDialog == null) return null;
            uIDialog.Window = windowInterface.ugui_instance;
            CurInstance = uIDialog;
            GameController.Instance.Enabled = false;
            return uIDialog;
        }
    }

}
