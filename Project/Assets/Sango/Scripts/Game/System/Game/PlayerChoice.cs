namespace Sango.Core
{
    /// <summary>
    /// 城池治安系统逻辑
    /// </summary>
    [GameSystem]
    public class PlayerChoice : GameSystem
    {
        public struct ChoiceData
        {
            public string lab;
            public System.Action call;
        }

        public ChoiceData[] choiceDatas;
        string windowName = "window_choice";

        public void Start(ChoiceData[] choices)
        {
            choiceDatas = choices;
            Push();
        }

        public override void OnEnter()
        {
            base.OnEnter();
            Window.Instance.Open(windowName, this);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Window.Instance.Close(windowName);

            if (choiceDatas == null || choiceDatas.Length == 0)
                return;

            ChoiceData data = choiceDatas[0];
            data.call?.Invoke();
            choiceDatas = null;
        }

        public void OnPlayerChoose(int index)
        {
            if (choiceDatas == null || index < 0 || index >= choiceDatas.Length)
            {
                Back();
                return;
            }
            Back();
            ChoiceData data = choiceDatas[index];
            data.call?.Invoke();
            choiceDatas = null;
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            // 不能直接关闭

        }
    }
}
