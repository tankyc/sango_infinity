using Sango.Mod;
using Sango.Tools;
using System.Collections;
using UnityEngine;

namespace Sango.Game
{
    public class Game : App<Game>
    {
        public override void Init(MonoBehaviour start, Platform.PlatformName targetPlatform)
        {
            base.Init(start, targetPlatform);
            Window.Instance.Init(1024, 720);
            ModManager.Instance.Init();
            StartCoroutine(GameInit());
        }
        public override void Shutdown() 
        {
            Sango.Log.Print("游戏关闭");
            Event.OnGameShutdown?.Invoke();
        }
        public override void Pause() 
        {
            Sango.Log.Print("游戏暂停");
            Event.OnGamePause?.Invoke();
        }
        public override void Resume() 
        {
            Sango.Log.Print("游戏恢复");
            Event.OnGameResume?.Invoke();
        }

        IEnumerator GameInit()
        {
            Window.Instance.ShowWindow("window_loading");
            //yield return new WaitForSeconds(0.5f);
            yield return null;
            ModManager.Instance.InitMods();
            GameData.Instance.Init();
            Event.OnGameInit?.Invoke();
            GameState.Instance.ChangeState((int)GameState.State.GAME_START_MENU);
            Window.Instance.ShowWindow("window_start");
            Window.Instance.HideWindow("window_loading");

            //Scenario scenario = new Scenario();
            //string path = Path.FindFile("Data/Scenario/Scenario.json");
            //scenario.FilePath = path;
            //scenario.CommonData = GameData.Instance.LoadCommonData();
            ////EnterMapEdior();
            //Scenario.Start(scenario);
            ////scenario.Save(Path.ContentRootPath + "/Save/Scenario.xml");

        }

        public void EnterMapEditor()
        {
            Window.Instance.HideWindow("window_start");
            GameObject map = new GameObject("map");
            MapEditor mapEditor = map.AddComponent<MapEditor>();
        }

        public void StartNewGame()
        {
            Window.Instance.ShowWindow("window_scenario_select");
            Window.Instance.HideWindow("window_start");
        }

        public void StartGame(Scenario target)
        {
           
        }

        public override void Update()
        {
            base.Update();
            if (Scenario.Cur != null && !Scenario.Cur.useThreadRun)
                Scenario.Cur.Run();
        }

        public static void DebugAI()
        {
            GameAIDebug.Enabled = !GameAIDebug.Enabled;
        }

    }
}
