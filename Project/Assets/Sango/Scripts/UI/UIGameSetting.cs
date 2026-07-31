using Sango.Core;
using System;

namespace Sango.UI
{
    /// <summary>
    /// 剧本选择界面
    /// </summary>
    public class UIGameSetting : UIScenarioVariables
    {
        public override void OnOpen()
        {
            onSure = null; onCancel = null;
            for (int i = 0; i < itemList.Count; i++)
                RemoveItem(itemList[i]);
            itemList.Clear();
            ShowVariables();
        }

        public override void OnOpen(params object[] objects)
        {
            OnOpen();
            if (objects.Length > 0)
                onSure = objects[0] as System.Action;
            if (objects.Length > 1)
                onCancel = objects[1] as System.Action;
        }

        public void ShowVariables()
        {
            GameEvent.OnGameSetting?.Invoke(this);
        }

        public override void RefreshSetting()
        {
            for (int i = 0; i < itemList.Count; i++)
                RemoveItem(itemList[i]);
            itemList.Clear();
            ShowVariables();
        }
        public override void OnStartGame()
        {
            GameSetting.Instance.Apply();
            if (onSure != null)
            {
                onSure();
                return;
            }
            Close();
        }

        public override void OnCancel()
        {
            if (onCancel != null)
            {
                onCancel();
                return;
            }
            Close();
        }
    }
}
