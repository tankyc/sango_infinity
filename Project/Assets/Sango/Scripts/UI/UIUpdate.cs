using Sango.Manager;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System.Collections;
using System;

namespace Sango.UI
{
    /// <summary>
    /// 游戏开始界面
    /// </summary>
    public class UIUpdate : UGUIWindow
    {
        public Text text;
        // Start is called before the first frame update
        public override void OnOpen()
        {
            base.OnOpen();
            text.text = GameVersion.Instance.LatestVersionInfo.Description;
        }

        public void OnDownload()
        {
            Application.OpenURL(GameVersion.Instance.LatestVersionInfo.Url);
        }
    }
}
