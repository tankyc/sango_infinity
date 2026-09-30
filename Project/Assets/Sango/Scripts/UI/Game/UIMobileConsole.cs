using Sango.Core.Player;

using Sango.Core;
using SKFramework;
using UnityEngine;

namespace Sango.UI
{
    public class UIMobileConsole : UGUIWindow
    {
        public MobileConsole mobileConsole;
        public RectTransform red;
        public RectTransform backgroundMask;
        public RectTransform info;
        bool showRed = false;
        bool showBg = false;
        public void OnConsole()
        {
            mobileConsole.Visible = true;
            backgroundMask.gameObject.SetActive(true);
            showBg = true;
        }

        private void Start()
        {
            if(mobileConsole != null)
            {
                mobileConsole.onError = OnConsole;
            }
            info.gameObject.SetActive(false);
            if (UnityEngine.PlayerPrefs.GetInt("game_show_info", 0) == 0)
            {
                GameEvent.OnScenarioStart += OnScenarioStart;
                UnityEngine.PlayerPrefs.SetInt("game_show_info", 1);
            }
        }

        void OnScenarioStart(Scenario scenario)
        {
            info.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (mobileConsole != null)
            {
                if(!showRed)
                {
                    if(mobileConsole.HasError)
                    {
                        showRed = true;
                        red.gameObject.SetActive(true);
                    }
                }

                if(showBg)
                {
                    if(!mobileConsole.Visible)
                    {
                        showBg = false;
                        backgroundMask.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
