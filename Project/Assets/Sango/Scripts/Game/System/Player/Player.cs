using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core.Player
{
    [GameSystem]
    public class Player : GameSystem
    {
        public ShortScenario[] all_saved_scenario_list = new ShortScenario[40];
        public ShortScenario[] all_auto_saved_scenario_list = new ShortScenario[10];
        private int autoSaveIndex = -1;
        private long autoSaveTime = 0;
        public int autoSave = 0;
        public int autoSaveTurnType = 0;
        int[] autoTurn = new int[] { 2, 3, 4, 6, 10 };
        public int currentTurnCount = 0;

        public override void Init()
        {
            InitSaveFile();
            InitAutoSaveFile();
            currentTurnCount = 0;
            GameEvent.OnForceTurnStart += OnForceTurnStart;
            GameEvent.OnGameSetting += OnGameSetting;
            GameEvent.OnGameSettingApply += OnGameSettingApply;
            GameEvent.OnGameSettingCancel += OnGameSettingCancel;
            autoSave = PlayerPrefs.GetInt("AutoSave", 0);
            autoSaveTurnType = PlayerPrefs.GetInt("AutoSaveTurnType", 0);

        }

        public override void Clear()
        {
            GameEvent.OnForceTurnStart -= OnForceTurnStart;
            GameEvent.OnGameSetting -= OnGameSetting;
            GameEvent.OnGameSettingApply -= OnGameSettingApply;
            GameEvent.OnGameSettingCancel -= OnGameSettingCancel;
        }

        /// <summary>
        /// 保存自定义数据
        /// </summary>
        /// <param name="scenario"></param>
        /// <param name="index"></param>
        void OnGameSettingApply()
        {
            PlayerPrefs.SetInt("AutoSave", autoSave);
            PlayerPrefs.SetInt("AutoSaveTurnType", autoSaveTurnType);
            PlayerPrefs.Save();
        }

        void OnGameSettingCancel()
        {

        }

        void OnGameSetting(IVariablesSetting variablesSetting)
        {
            // 音频设置
            variablesSetting.AddBigTitle("游戏设置");

            variablesSetting.AddToggleItem("自动存档", autoSave == 1,
                (v) =>
                {
                    autoSave = v ? 1 : 0;
                });
            variablesSetting.AddDropdownItem("自动存档间隔回合", autoSaveTurnType,
                new List<string>(new string[]
                {
                    "1回合",
                    "2回合",
                    "3回合",
                    "5回合",
                    "10回合",
                }),
                (index) =>
                {
                    autoSaveTurnType = index;
                });
        }

        void OnForceTurnStart(Force force, Scenario scenario)
        {
            if (autoSave == 1 && force.IsPlayer)
            {
                currentTurnCount++;
                if (currentTurnCount >= autoTurn[autoSaveTurnType])
                {
                    AutoSave();
                    currentTurnCount = 0;
                }
            }
        }

        public static string GetSaveFileName(int index)
        {
            return $"{Path.SaveRootPath}/Save/save{index}.json";
        }
        public static string GetAutoSaveFileName(int index)
        {
            return $"{Path.SaveRootPath}/Save/auto_save{index}.json";
        }

        void InitSaveFile()
        {
            for (int i = 1; i <= all_saved_scenario_list.Length; i++)
            {
                string fileName = GetSaveFileName(i);
                if (File.Exists(fileName))
                {
#if SANGO_DEBUG
                    Sango.Log.Info($"Find Saved data : {fileName}");
#endif
                    ShortScenario scenario = new ShortScenario(fileName);
                    all_saved_scenario_list[i - 1] = scenario;
                }
            }
        }
        void InitAutoSaveFile()
        {
            for (int i = 1; i <= all_auto_saved_scenario_list.Length; i++)
            {
                string fileName = GetAutoSaveFileName(i);
                if (File.Exists(fileName))
                {
#if SANGO_DEBUG
                    Sango.Log.Info($"Find Saved data : {fileName}");
#endif
                    ShortScenario scenario = new ShortScenario(fileName);
                    if (scenario.Info.dateTime > autoSaveTime)
                    {
                        autoSaveTime = scenario.Info.dateTime;
                        autoSaveIndex = i - 1;
                        if (autoSaveIndex >= all_auto_saved_scenario_list.Length)
                            autoSaveIndex = 0;
                    }
                    all_auto_saved_scenario_list[i - 1] = scenario;
                }
                else
                {
                    if (autoSaveIndex == -1)
                        autoSaveIndex = i - 1;
                }
            }
        }
        public void Save(int index)
        {
            string fileName = GetSaveFileName(index + 1);
            GameEvent.OnGameSave?.Invoke(Scenario.Cur, index, false);
            Scenario.Cur.Save(fileName);
            ShortScenario scenario = new ShortScenario(fileName);
            all_saved_scenario_list[index] = scenario;
        }

        public void Load(int index)
        {
            Window.Instance.CloseAll();
            Window.Instance.Open("window_loading");
            Quit();
            string fileName = GetSaveFileName(index + 1);
            Scenario.CurSelected = new Scenario(fileName);
            Scenario.StartScenario(Scenario.CurSelected);
        }

        public void AutoSave()
        {
            string fileName = GetAutoSaveFileName(autoSaveIndex + 1);
            GameEvent.OnGameSave?.Invoke(Scenario.Cur, autoSaveIndex, true);
            Scenario.Cur.Save(fileName);
            ShortScenario scenario = new ShortScenario(fileName);
            all_auto_saved_scenario_list[autoSaveIndex] = scenario;
            autoSaveIndex++;
            if (autoSaveIndex >= all_auto_saved_scenario_list.Length)
                autoSaveIndex = 0;
        }

        public void LoadAutoFile(int index)
        {
            Window.Instance.CloseAll();
            Window.Instance.Open("window_loading");
            Quit();
            string fileName = GetAutoSaveFileName(index + 1);
            Scenario.CurSelected = new Scenario(fileName);
            Scenario.StartScenario(Scenario.CurSelected);
        }

        public void Quit()
        {
            Scenario.Cur?.OnGameShutdown();
        }

        public void QuitToMainMenu()
        {
            Scenario.Cur?.OnGameShutdown();
            Window.Instance.CloseAll();
            Window.Instance.DestroyAll();
            Window.Instance.Open("window_start");

        }
    }
}
