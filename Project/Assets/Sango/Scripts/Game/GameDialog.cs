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
            /// <summary>
            /// 本条对话的序号。对话框窗口是按窗口名复用的**同一个实例**，
            /// 上一轮的关闭回调如果迟到，会错误地把当前这条推进掉；靠序号丢弃它（见 OnWindowClosed）。
            /// </summary>
            public int seq;
            public DialogStyle style;
            public System.Action cancelAction;
            public System.Action sureAction;
            public string content;
            public Person person;
            public Vector3 startPoint;
            public int sound;
            public int bgm;
            /// <summary>
            /// 多句台词（同一个窗口里逐句显示）。非空表示这是一条台词链，
            /// 由 UIDialog 按句切换内容，最后一句点确定才关窗口。
            /// </summary>
            public List<TalkData> talks;
            /// <summary>台词链收尾回调。由 UIDialog 在关闭时执行一次（确定与取消都会执行，避免等待方卡死）。</summary>
            public System.Action talkEndAction;
        }

        public List<DialogData> dialogDatas = new List<DialogData>();

        /// <summary>
        /// 窗口句柄。只用来判断"界面是否真的显示着"，
        /// **不参与**"是否已有对话在占位"的判断（那个用 hasCurrent）。
        /// </summary>
        Window.WindowInterface windowInterface;

        /// <summary>
        /// 是否已有一条"数据已经准备好"的对话。
        ///
        /// 不能用 windowInterface 判断：它是在 Window.Open 返回之后才赋值的，
        /// 而 Open 内部（UIDialog.OnOpen / GameEvent.OnWindowCreate）可能又走进 GameDialog.Open，
        /// 那一刻 windowInterface 还是 null，会被误判成"没有对话"从而递归推进队列。
        /// hasCurrent 在调用 Window.Open **之前**就置位，重入时只会入队，不会递归。
        /// </summary>
        bool hasCurrent;
        DialogData curData;      // 当前这条对话的数据
        int curSeq;              // 当前这条对话的序号
        string curWindowName;    // 当前这条对话用的窗口名（日志用）
        int nextSeq = 1;         // 序号发号器
        bool advancing;          // 推进重入锁

        /// <summary>是否已有一条"已准备好数据"的对话在占位。</summary>
        public bool HasCurrent { get { return hasCurrent; } }

        #region Open

        public void Open(DialogStyle style, string content, System.Action sureAction, System.Action cancelAction,
            Person person, Vector3 startPoint, int sound = 0, int bgm = 0)
        {
            Enqueue(style, content, sureAction, cancelAction, person, startPoint, sound, bgm, null, null);
        }

        public void Open(DialogStyle style, string content, System.Action sureAction, System.Action cancelAction,
            Person person = null, int sound = 0, int bgm = 0)
        {
            Enqueue(style, content, sureAction, cancelAction, person, Input.mousePosition, sound, bgm, null, null);
        }

        public void Open(DialogStyle style, string content, System.Action sureAction, int sound = 0, int bgm = 0)
        {
            Enqueue(style, content, sureAction, null, null, Input.mousePosition, sound, bgm, null, null);
        }

        public void Open(DialogStyle style, string content, System.Action sureAction, Person person, int sound = 0, int bgm = 0)
        {
            Enqueue(style, content, sureAction, null, person, Input.mousePosition, sound, bgm, null, null);
        }

        /// <summary>
        /// 打开一条"多句台词"对话：同一个窗口里逐句显示，玩家点一次走一句，最后一句之后执行 endAction。
        ///
        /// 与"每句一个窗口"相比，不会出现"关掉同一个实例又立刻打开"的翻转，
        /// 也不会因为中途关闭而丢掉收尾回调（取消视为"跳过剩余台词"，同样执行 endAction）。
        /// </summary>
        public void OpenTalk(List<TalkData> talks, System.Action endAction, DialogStyle style = DialogStyle.ClickPersonSay)
        {
            if (talks == null || talks.Count == 0)
            {
                endAction?.Invoke();
                return;
            }

            TalkData first = talks[0];
            Enqueue(style, first.text, null, null, first.person, Input.mousePosition,
                first.sound, first.bgm, new List<TalkData>(talks), endAction);
        }

        void Enqueue(DialogStyle style, string content, System.Action sureAction, System.Action cancelAction,
            Person person, Vector3 startPoint, int sound, int bgm, List<TalkData> talks, System.Action talkEndAction)
        {
            DialogData dialogData = new DialogData
            {
                seq = nextSeq++,
                style = style,
                sureAction = sureAction,
                cancelAction = cancelAction,
                content = content,
                person = person,
                startPoint = startPoint,
                sound = sound,
                bgm = bgm,
                talks = talks,
                talkEndAction = talkEndAction,
            };

            dialogDatas.Add(dialogData);
            TryAdvance();
        }

        #endregion

        /// <summary>
        /// 推进队列：把下一条对话交给窗口。
        /// 任何一次"当前对话结束"（点确定/取消、被外部关闭）都会走到这里。
        /// </summary>
        void TryAdvance()
        {
            if (advancing) return;      // 已经有人在推进（例如关闭回调里又 Open 了）
            advancing = true;
            try
            {
                while (true)
                {
                    if (hasCurrent) return;     // 已经有一条在占位，等它关闭

                    if (dialogDatas.Count == 0)
                    {
                        hasCurrent = false;
                        curData = default;
                        curSeq = 0;
                        curWindowName = null;
                        windowInterface = null;
                        GameController.Instance.Enabled = true;
                        return;
                    }

                    curData = dialogDatas[0];
                    dialogDatas.RemoveAt(0);
                    curSeq = curData.seq;
                    curWindowName = WindowNameOf(curData.style);
                    hasCurrent = true;      // ★ 必须在 Window.Open 之前：重入时才不会递归推进

                    Window.WindowInterface win = Window.Instance.Open(curWindowName, curData);
                    windowInterface = win;

                    if (win == null || !win.HasValid())
                    {
                        // 窗口没开出来（缺 prefab / 没有 UI 上下文）。
                        // 丢弃这一条继续下一条；队列空时上面的分支会把 Enabled 放开，
                        // 不会再把输入永久留在关闭状态。
                        Sango.Log.Error($"对话框窗口创建失败,跳过该条对话: {curWindowName}");
                        hasCurrent = false;
                        curData = default;
                        curSeq = 0;
                        curWindowName = null;
                        windowInterface = null;
                        continue;
                    }

                    GameController.Instance.Enabled = false;
                    return;
                }
            }
            finally
            {
                advancing = false;
            }
        }

        static string WindowNameOf(DialogStyle style)
        {
            switch (style)
            {
                case DialogStyle.ChoosePersonSay: return "window_dialog2";
                case DialogStyle.Window: return "window_dialog3";
                case DialogStyle.ClickPersonSay: return "window_dialog4";
                case DialogStyle.ClickSay: return "window_dialog5";
                default: return "window_dialog";
            }
        }

        /// <summary>
        /// 窗口关闭时**唯一**的通知入口（由 UIDialog 调用）。
        /// </summary>
        /// <param name="seq">发起关闭的那条对话的序号，用于丢弃陈旧回调</param>
        /// <param name="sure">点的是确定(true)还是取消/其它关闭(false)</param>
        /// <param name="invokeAction">是否执行该对话携带的用户回调。
        /// 玩家主动点按钮时为 true；被 Window.Close / CloseAll / DestroyAll 等**外部**关闭时为 false——
        /// 那种场合往往是读档 / 回主菜单，业务回调不该再跑，但状态必须清掉、队列必须继续。</param>
        public void OnWindowClosed(int seq, bool sure, bool invokeAction = true)
        {
            if (!hasCurrent || seq != curSeq)
            {
                Sango.Log.Info($"丢弃陈旧的对话框关闭回调: seq={seq}, cur={curSeq}", Sango.Log.LogType.UI);
                return;
            }

            DialogData data = curData;
            hasCurrent = false;     // ★ 先摘掉"当前"，再执行用户回调（回调里可能又 Open）
            curData = default;
            curSeq = 0;
            curWindowName = null;
            windowInterface = null;

            try
            {
                if (invokeAction)
                {
                    if (sure)
                        data.sureAction?.Invoke();
                    else
                        data.cancelAction?.Invoke();
                }
            }
            finally
            {
                // 回调里新 Open 的已经排在队尾，这里按 FIFO 继续推进
                TryAdvance();
            }
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

        /// <summary>
        /// 当前是否真有对话框窗口在显示。
        ///
        /// hasCurrent 表示"有一条对话已经准备好并交给了窗口"，配合窗口有效性即可判断——
        /// 而且 hasCurrent 在调用 Window.Open 之前就置位，所以 Open 之后**同帧**查询也是准的
        /// （窗口是同步创建的）。调用方（例如单挑表现层）据此判断"这句台词到底播没播出去"，
        /// 免得把流程卡在一个永远不会被关闭的对话框上。
        /// </summary>
        public bool IsDialogAlive()
        {
            return hasCurrent && windowInterface != null && windowInterface.HasValid();
        }


        public struct TalkData
        {
            public string text;
            public Person person;
            public int sound;
            public int bgm;
        }

        public static void StartTalk(List<TalkData> talk_content, System.Action endAction)
        {
            // 台词链走"单窗口逐句"模式：不再一句一个窗口，
            // 避免对同一个复用实例做"关闭又立刻打开"的翻转，也避免中途关闭丢掉收尾回调。
            Instance.OpenTalk(talk_content, endAction);
        }
    }
}
