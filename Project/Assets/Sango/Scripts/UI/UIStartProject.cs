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
    public class UIStartProject : UGUIWindow
    {
        protected override void Awake()
        {
            base.Awake();
        }

        private void Start()
        {

        }

        public void OnAddPerson()
        {
            Window.Instance.Open("window_create_person_menu");
        }
        public void OnEditPerson()
        {

        }
        public void OnMapEditor()
        {
            GameMedia.Instance.PlayButtonSfx();
            Game.Instance.EnterMapEditor();
        }

        public void OnScenarioEditor()
        {
            GameMedia.Instance.PlayButtonSfx();
            ScenarioEdit.GetSystem<ScenarioEdit>().Push();
        }

        public void OnScenarioEditorWeb()
        {
            GameMedia.Instance.PlayButtonSfx();
            Application.OpenURL("http://139.155.98.66/scenario/");
        }

        public void OnWorkshop()
        {
            GameMedia.Instance.PlayButtonSfx();
            Application.OpenURL("http://139.155.98.66/browse");
        }

        public void OnPersonlib()
        {
            GameMedia.Instance.PlayButtonSfx();
            Application.OpenURL("http://139.155.98.66/personlib/");
        }
    }
}
