using Sango.Manager;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System.Collections;

namespace Sango.UI
{
    /// <summary>
    /// 游戏开始界面
    /// </summary>
    public class UIStart : UGUIWindow
    {
        public Text version;
        public GameObject mapEditorBtn;
        public GameObject thxNode;
        public Text thxText;

        const string thx_content = @"<color=#ff9330>手机仔,
komorebi,
zd同学,
冬之绿叶,
鲁迪,
小半,
在路上,
雨辰-测试员,
阿尔萨斯,
李十六,
刑部尚书,
殤,
七寻,
平步青云,
(wx)*倫,
(wx)*鬼,
温柔的强韧,
(wx).
(wx)岁
东,
此世乃無間
</color>

        ";

        protected override void Awake()
        {
            base.Awake();
            thxNode.SetActive(false);
            thxText.text = thx_content;

        }

        private void Start()
        {
            version.text = $"版本: {Application.version}";
            GameMedia.Instance.PlayBgm(9);
#if (UNITY_ANDROID || UNITY_IPHONE) && !UNITY_EDITOR
            mapEditorBtn.SetActive(false);
#endif
            GameMedia.Instance.PlaySfx(45);
        }

        public void OnNewGame()
        {
            GameMedia.Instance.PlayButtonSfx();
            Game.Instance.StartNewGame();
        }
        public void QuitGame()
        {
            GameMedia.Instance.PlayButtonSfx();
            GameDialog.Open("是否要退出游戏??", () =>
            {
                GameDialog.Close();
                Application.Quit();
            }).cancelAction = () =>
            {
                GameDialog.Close();
            };
        }

        public void OnGameSetting()
        {
            GameMedia.Instance.PlayButtonSfx();
            Window.Instance.Open("window_game_setting");
        }

        public void OnMapEditor()
        {
            GameMedia.Instance.PlayButtonSfx();
            Game.Instance.EnterMapEditor();
        }
        public void OpenThx()
        {
            thxNode.SetActive(true);
            StartCoroutine(DelayActive());
        }

        IEnumerator DelayActive()
        {
            yield return new WaitForSeconds(0.05f);
            thxNode.SetActive(false);
            thxNode.SetActive(true);

        }

        public void OnLoadGame()
        {
            GameMedia.Instance.PlayButtonSfx();
            Window.Instance.Open("window_scenario_save", 2);
        }

        public void OnTest()
        {
            string path = Sango.Path.FindFile("Scenario/Scenario.json");
            Scenario scenario = new Scenario(path);
            scenario.View = new ScenarioView
            {
                cameraPosition = new UnityEngine.Vector3(1407, 0, 796),
                cameraRotation = new UnityEngine.Vector3(40f, -50f, 0f),
                cameraDistance = 400f
            };


            Scenario.StartScenario(scenario);
        }

        public void OnModManager()
        {
            GameMedia.Instance.PlayButtonSfx();
            Window.Instance.Open("window_mod_manager");
            Window.Instance.Close("window_start");
        }

        public void JumpBilibili()
        {
            Application.OpenURL("https://space.bilibili.com/3546816591170057");
        }
    }
}
