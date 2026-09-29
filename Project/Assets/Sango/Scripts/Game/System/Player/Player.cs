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

        /// <summary>
        /// 上一次为自动存档计数的回合号，用于"一个大回合只累加一次"。
        /// 初值 -1：任何剧本的回合号都从 0/1 起，不会与它撞车。
        /// </summary>
        int lastCountedTurn = -1;

        public override void Init()
        {
            InitSaveFile();
            InitAutoSaveFile();
            currentTurnCount = 0;
            lastCountedTurn = -1;
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

        /// <summary>
        /// 自动存档计时。
        ///
        /// 为什么仍然挂在"势力回合开始（<see cref="GameEvent.OnForceTurnStart"/>）"而不是"大回合开始"：
        /// <see cref="AutoSave"/> 会把 <see cref="ScenarioInfo.curForceId"/> 一起写进存档，
        /// 而读档恢复的做法是"重建队列后从队首逐个出队，直到撞上 curForceId"，撞中之前的势力全部丢弃。
        /// 大回合开始时 curForceId 还是**上一轮最后行动的那支 AI 势力**（玩家势力被排在队首），
        /// 在那一刻存档会让玩家势力在读档时被整批丢掉、白等一整个大回合；
        /// 那支 AI 势力若在存档期间灭亡，恢复循环还会因为队列被取空而直接抛异常。
        /// 所以存档点必须落在"某支势力刚出队开始行动"的时刻 —— 此时 curForceId 正是队首，读档什么都不丢。
        ///
        /// 计时口径改按大回合去重（一个回合只在最先开始行动的势力这里累加一次），于是同时修好两件事：
        ///   · 上帝放置模式（场上没有玩家势力）以前永远不计进度 → 放置几小时也不会自动存档；
        ///   · 同时控制多个玩家势力时以前一个回合被累加多次 → 存档间隔被悄悄缩短。
        /// 单一玩家势力的常规局面下"大回合数 == 玩家回合数"，现有手感不变。
        /// </summary>
        /// <param name="force">本回合开始行动的势力</param>
        /// <param name="scenario">当前剧本</param>
        void OnForceTurnStart(Force force, Scenario scenario)
        {
            if (autoSave != 1 || force == null || scenario == null)
                return;

            // 本大回合已经计过就直接返回：每个回合只会有"第一支开始行动的势力"走到下面的累加
            if (scenario.Info.turnCount == lastCountedTurn)
                return;
            lastCountedTurn = scenario.Info.turnCount;

            currentTurnCount++;
            if (currentTurnCount >= autoTurn[autoSaveTurnType])
            {
                AutoSave();
                currentTurnCount = 0;
            }
        }

        /// <summary>
        /// 跨剧本复位自动存档计时，由 <see cref="ScenarioLifecycle.BeginShutdown"/> 在收尾时调用。
        /// 不一起复位 <see cref="lastCountedTurn"/> 的话，新剧本首个回合可能被上一次开局留下的回合号吞掉。
        /// </summary>
        public void ResetTurnCounters()
        {
            currentTurnCount = 0;
            lastCountedTurn = -1;
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
                    Sango.Log.Info($"Find Saved data : {fileName}");
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
                    Sango.Log.Info($"Find Saved data : {fileName}");
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

        /// <summary>
        /// 关闭当前剧本。读档 / 回主菜单都必须走这里。
        ///
        /// 不要直接调 <see cref="Scenario.OnGameShutdown"/>：那只收剧本自身，
        /// 而表现层事件队列、对话队列、系统栈、AI 参数等常驻对象会留下上一次开局的痕迹，
        /// 第二次开局就会出现幽灵行为（旧事件被续播、订阅叠加、输入点不动…）。
        /// </summary>
        public void Quit()
        {
            ScenarioLifecycle.BeginShutdown();
        }

        public void QuitToMainMenu()
        {
            Quit();
            Window.Instance.CloseAll();
            Window.Instance.DestroyAll();
            Window.Instance.Open("window_start");

        }
    }
}
