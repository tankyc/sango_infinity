/*
 * 文件名：Game.cs
 * 描述：游戏核心类，管理游戏的初始化、更新、暂停、恢复和关闭
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core.Action;
using Sango.Mod;
using Sango.Render;
using Sango.Tools;
using Sango.Manager;
using Sango.Core.Debate;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core
{
    /// <summary>
    /// 游戏核心类，管理游戏的初始化、更新、暂停、恢复和关闭
    /// </summary>
    public class Game : App
    {
        private static Game _instance;

        public new static Game Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    try
                    {
                        _instance = new Game();
                        _app_instance = _instance;
                    }
                    catch (System.Exception e)
                    {
                        UnityEngine.Debug.LogError(e);
                    }
#else
                    _instance = new Game();
                    _app_instance = _instance;
#endif
                }
                return _instance;
            }
        }


        /// <summary>
        /// 初始化状态
        /// </summary>
        bool inited = false;

        /// <summary>
        /// 初始化游戏
        /// </summary>
        /// <param name="start">启动脚本实例</param>
        /// <param name="targetPlatform">目标平台</param>
        public override void Init(MonoBehaviour start, Platform.PlatformName targetPlatform)
        {
            GameMedia.Instance.Init();
            GameSetting.Instance.Initialize();
            inited = false;
            CanvasScalerFactor = CanvasScaler.referenceResolution.y / 1080f;
            base.Init(start, targetPlatform);
            Window.Instance.Init(1024, 720);
            ActionBase.Init();
            Condition.Init();
            BuffEffect.Init();
            SkillEffect.Init();
            Trigger.Init();
            PersonFunctions.Init();
            TroopCompareFunction.Init();
            SkillSuccessMethod.Init();
            SkillSpellConditionMethod.Init();
            SkillCriticalMethod.Init();
            SkillRangeFilterMethod.Init();
            ModManager.Instance.Init();
            GameLanguage.Instance.Init("cn");
            GameSystemManager.Instance.Init();
            DebateManager.Instance.Init();
            StartCoroutine(GameInit());
        }

        /// <summary>
        /// 关闭游戏
        /// </summary>
        public override void Shutdown()
        {
            //MapRender.Instance.Clear();
#if SANGO_DEBUG
            Sango.Log.Info("游戏关闭");
#endif
            GameEvent.OnGameShutdown?.Invoke();
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public override void Pause()
        {
#if SANGO_DEBUG
            Sango.Log.Info("游戏暂停");
#endif
            GameEvent.OnGamePause?.Invoke();
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public override void Resume()
        {
#if SANGO_DEBUG
            Sango.Log.Info("游戏恢复");
#endif
            GameEvent.OnGameResume?.Invoke();
        }

        /// <summary>
        /// 游戏初始化协程
        /// </summary>
        /// <returns>协程迭代器</returns>
        IEnumerator GameInit()
        {
            //Window.Instance.Open("window_loading");
            //yield return new WaitForSeconds(0.5f);
            yield return null;
            ModManager.Instance.InitMods();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            CursorManager.Instance.InitCursorTextures();
            CursorManager.Instance.SetCursorStyle(0);
#endif
            // 初始化音效管理器
            SkillVisualizer.Init();

            // 初始化外交管理器
            //DiplomacyManager.Instance.Init();
            //DiplomacyEventManager.Instance.Init();
            
            GameData.Instance.Init();

            GameEvent.OnGameInit?.Invoke();
            GameState.Instance.ChangeState((int)GameState.State.GAME_START_MENU);
            Window.Instance.Open("window_start");
            Window.Instance.Close("window_loading");
            inited = true;
            //Scenario scenario = new Scenario();
            //string path = Path.FindFile("Data/Scenario/Scenario.json");
            //scenario.FilePath = path;
            //scenario.CommonData = GameData.Instance.LoadCommonData();
            ////EnterMapEdior();
            //Scenario.Start(scenario);
            ////scenario.Save(Path.ContentRootPath + "/Save/Scenario.xml");
            //Player.Player.Instance.Init();
        }

        /// <summary>
        /// 进入地图编辑器
        /// </summary>
        public void EnterMapEditor()
        {
            Window.Instance.Close("window_start");
            GameObject map = new GameObject("map");
            MapEditor mapEditor = map.AddComponent<MapEditor>();
        }

        /// <summary>
        /// 开始新游戏
        /// </summary>
        public void StartNewGame()
        {
            Window.Instance.Open("window_scenario_select");
            Window.Instance.Close("window_start");
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        /// <param name="target">场景实例</param>
        public void StartGame(Scenario target)
        {

        }

        /// <summary>
        /// 更新游戏
        /// </summary>
        public override void Update()
        {
            if(!inited) return;
            GameController.Instance.Update();
            base.Update();
            // 更新音效管理器
            GameMedia.Instance.Update();
            // 更新舌战管理器
            DebateManager.Instance.Update(Time.deltaTime);
            Scenario scenario = Scenario.Cur;
            if (scenario != null)
            {
                GameEvent.OnScenarioTick?.Invoke(scenario, Time.deltaTime);
                if (!Scenario.Cur.useThreadRun)
                    Scenario.Cur.Run();
            }
        }

        /// <summary>
        /// 调试AI
        /// </summary>
        public static void DebugAI()
        {
            GameAIDebug.Enabled = !GameAIDebug.Enabled;
        }

    }
}
