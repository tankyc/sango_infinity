using Sango.Core.Player;
using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using Sango.Manager;

namespace Sango.UI
{
    public class UIGame : UGUIWindow
    {
        public UIPlayerInfoPanel uIPlayerInfoPanel;

        public Text forceText;
        public Text dateText;
        public Text fpsText;

        public Text cellInfoLabel;
        public Image seasonImg;
        public Text seasonLabel;
        public Text actionNumberLabel;
        public Text techPointLabel;

        public Text frameBtnText;
        public Text speedBtnText;

        public GameObject miniMapObj;
        public GameObject miniMapBtnObj;
        public GameObject messageObj;

        public RectTransform gameSettingRect;
        public RectTransform gameInformationRect;

        public bool gridShow = true;
        public bool troopListShow = false;
        public GameObject troopListObj;
        public Text troopListShowText;

        public LoopScrollRect loopScrollRect;
        public GameObject troopListItemObj;
        public Transform troopListContent;
        public int totalCount = -1;
        Stack<Transform> pool = new Stack<Transform>();
        //List<Troop> troops_list = new List<Troop>();
        List<SangoObject> item_list = new List<SangoObject>();
        public Type itemType;
        bool needUpdateItem = true;

        public GameObject pauseObj;
        public GameObject resumeObj;

        public Button endTurnButton;

        public GameObject[] fpaObj;

        int destSaveTurn = -1;
        bool needSave = false;
        private float deltaTime = 0.0f;
        bool cityInfoShow = true;

        float gameSpeed = 1;

        public override void OnOpen()
        {
            base.OnOpen();

#if !UNITY_EDITOR
            foreach(var  obj in fpaObj)
            {
                if(obj != null)
                    obj.gameObject.SetActive(false);
            }
#endif

            Window.Instance.Close("window_loading");
            GameController.Instance.onCellOverEnter += OnCellOverEnter;
            GameController.Instance.onCellOverExit += OnCellOverExit;

            Window.Instance.Open("window_object_pop_info");
        }

        public override void OnClose()
        {
            GameController.Instance.onCellOverEnter -= OnCellOverEnter;
            GameController.Instance.onCellOverExit -= OnCellOverExit;
            base.OnClose();
        }

        void OnCellOverEnter(Cell cell)
        {
            if (cell == null)
            {
                cellInfoLabel.text = "";
                return;
            }
            if (cell.moveAble)
            {
                string cityName = cell.BelongCity != null ? cell.BelongCity.Name : "--";

                if (cell.CanBuild && cell.building == null)
                {
                    bool can_place_obstacle = true;
                    bool can_place_other = true;

                    int buildSpace = Scenario.Cur.Variables.BuildingSpace;
                    cell.SpiralHasBuilding(buildSpace, (b) =>
                     {
                         if (b.BuildingType.majorType == 0)
                         {
                             can_place_obstacle = false;
                             can_place_other = false;
                         }
                         else if (!b.BuildingType.IsObstacle)
                         {
                             can_place_other = false;
                         }

                         return b.BuildingType.majorType == 0 || !b.BuildingType.IsObstacle;
                     });

                    if (can_place_obstacle && !can_place_other)
                        cellInfoLabel.text = $"地形: {cell.TerrainType.Name}({cityName})  坐标: ({cell.x}, {cell.y}) 可设置:<color=#11ff11>障碍物</color>";
                    else if (!can_place_obstacle && can_place_other)
                        cellInfoLabel.text = $"地形: {cell.TerrainType.Name}({cityName})  坐标: ({cell.x}, {cell.y}) 可设置:<color=#11ff11>军事设施</color>";
                    else if (can_place_obstacle && can_place_other)
                        cellInfoLabel.text = $"地形: {cell.TerrainType.Name}({cityName})  坐标: ({cell.x}, {cell.y}) 可设置:<color=#11ff11>军事设施,障碍物</color>";
                    else
                        cellInfoLabel.text = $"地形: {cell.TerrainType.Name}({cityName})  坐标: ({cell.x}, {cell.y}) <color=#ff1111>不可建筑</color>";

                }
                else
                    cellInfoLabel.text = $"地形: {cell.TerrainType.Name}({cityName})  坐标: ({cell.x}, {cell.y}) <color=#ff1111>不可建筑</color>";
            }
            else
            {
                cellInfoLabel.text = $"地形: <color=#ff0000>不可进入</color>  坐标: ({cell.x}, {cell.y})     ";
            }
        }

        void OnCellOverExit(Cell cell)
        {

        }

        void Start()
        {
            //GameEvent.OnTroopCreated += OnTroopChange;
            //GameEvent.OnTroopDestroyed += OnTroopChange;
            GameEvent.OnForceTurnStart += OnForceStart;
            GameEvent.OnDayUpdate += OnDayUpdate;
            GameEvent.OnCityFall += OnCityFall;
            GameEvent.OnSeasonUpdate += OnSeasonUpdate;
            GameEvent.OnForceGainTechniquePoint += OnForceGainTechniquePoint;
            GameEvent.OnCorpsActionPointChange += OnCorpsActionPointChange;
            GameEvent.OnScenarioStart += OnScenarioStart;
            GameEvent.OnPlayerEndTurn += OnPlayerEndTurn;
            GameSystem.GetSystem<PlayerMessage>().onVisibleChange += OnMessagePlaneVisible;


            //loopScrollRect.prefabSource = this;
            //loopScrollRect.dataSource = this;

            itemType = typeof(Troop);
            needUpdateItem = true;

            InvokeRepeating("UpdateFPS", 1f, 1f);
            //loopScrollRect.totalCount = totalCount;
            //loopScrollRect.RefillCells();
            OnSeasonUpdate(Scenario.Cur);
            OnDayUpdate(Scenario.Cur);
            OnForceStart(Scenario.Cur.CurRunForce, Scenario.Cur);

            for (int i = 0; i < Scenario.Cur.corpsSet.Count; ++i)
            {
                var c = Scenario.Cur.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == Scenario.Cur.CurRunForce)
                {
                    OnCorpsActionPointChange(c);
                    break;
                }
            }
        }

        void OnScenarioStart(Scenario scenario)
        {
            OnForceStart(Scenario.Cur.CurRunForce, Scenario.Cur);
            OnDayUpdate(Scenario.Cur);
            OnSeasonUpdate(Scenario.Cur);
            for (int i = 0; i < Scenario.Cur.corpsSet.Count; ++i)
            {
                var c = Scenario.Cur.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == Scenario.Cur.CurRunForce)
                {
                    OnCorpsActionPointChange(c);
                    break;
                }
            }

            GameMedia.Instance.PlaySfx(36);

        }

        protected override void OnDestroy()
        {
            //GameEvent.OnTroopCreated -= OnTroopChange;
            //GameEvent.OnTroopDestroyed -= OnTroopChange;
            GameEvent.OnForceTurnStart -= OnForceStart;
            GameEvent.OnDayUpdate -= OnDayUpdate;
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnSeasonUpdate -= OnSeasonUpdate;
            GameEvent.OnForceGainTechniquePoint -= OnForceGainTechniquePoint;
            GameEvent.OnCorpsActionPointChange -= OnCorpsActionPointChange;
            GameEvent.OnScenarioStart -= OnScenarioStart;
            GameEvent.OnPlayerEndTurn -= OnPlayerEndTurn;


            PlayerMessage playerMessage = GameSystem.GetSystem<PlayerMessage>();
            if (playerMessage != null) playerMessage.onVisibleChange -= OnMessagePlaneVisible;

        }

        public void OnCityFall(City city, Force lastForce, Troop atker)
        {
            if (itemType == typeof(City))
            {
                loopScrollRect.RefreshCells();
            }

        }

        public void OnTroopChange(Troop troop, Scenario scenario)
        {
            if (itemType == typeof(Troop))
            {
                needUpdateItem = true;
            }

        }

        public void OnPlayerEndTurn(Force force, Scenario scenario)
        {
            endTurnButton.interactable = false;
            uIPlayerInfoPanel.gameObject.SetActive(false);
        }

        public void OnForceStart(Force force, Scenario scenario)
        {
            if (force == null)
            {
                forceText.text = "";
                techPointLabel.text = "";
                endTurnButton.interactable = false;
                uIPlayerInfoPanel.gameObject.SetActive(false);
                return;
            }
            forceText.text = force.Name;
            techPointLabel.text = force.TechniquePoint.ToString();

            endTurnButton.interactable = force.IsPlayer;
            uIPlayerInfoPanel.gameObject.SetActive(force.IsPlayer);

            if (force.IsPlayer)
            {
                GameMedia.Instance.PlayNewTurnSfx();
                uIPlayerInfoPanel.UpdateShowType();
                GameSystem.GetSystem<PlayerTurnStartGreeting>().Push();
            }
            if (force.IsPlayer)
            {
                int sIndex = (int)scenario.CurSeason;
                GameMedia.Instance.PlayBgm(CheckBGM(seasonBGMPath[sIndex], force, scenario));
            }
        }

        public void OnCorpsActionPointChange(Corps corps)
        {
            if (corps.IsCaptainCorps)
                actionNumberLabel.text = corps.ActionPoint.ToString();
        }

        public void OnForceGainTechniquePoint(Force force, int value)
        {
            if (!force.IsPlayer) return;
            techPointLabel.text = force.TechniquePoint.ToString();
        }

        string[] seasonIconPath = new string[] {
            "Assets/UI/AtlasTexture/4846-6/4846-6_10.png",      //秋
            "Assets/UI/AtlasTexture/4846-6/4846-6_8.png",       //春
            "Assets/UI/AtlasTexture/4846-6/4846-6_9.png",       //夏
            "Assets/UI/AtlasTexture/4846-6/4846-6_11.png"       //冬
        };

        int[] seasonBGMPath = new int[] {
            10,                             //秋
            11,       //春
            12,       //夏
            13       //冬
        };
        int[] seasonSfxPath = new int[] {
            14,                             //秋
            15,       //春
            16,       //夏
            17       //冬
        };
        public void OnDayUpdate(Scenario scenario)
        {
            dateText.text = scenario.GetDateStr();
        }
        public void OnSeasonUpdate(Scenario scenario)
        {
            int sIndex = (int)scenario.CurSeason;
            seasonImg.sprite = ObjectLoader.LoadObject<UnityEngine.Sprite>(seasonIconPath[sIndex]);
            seasonLabel.text = GameDefine.seasonName[sIndex];
            GameMedia.Instance.PlayBgm(seasonBGMPath[sIndex]);
            GameMedia.Instance.PlaySfx(seasonSfxPath[sIndex]);
        }

        public void OnBtnPause()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Core.Scenario.Pause();
        }

        public void OnBtnResume()
        {
            pauseObj.SetActive(true);
            resumeObj.SetActive(false);
            Sango.Core.Scenario.Resume();
        }


        public void OnBtnNextForce()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Core.Scenario.NextForce();
        }

        public void OnBtnNextTurn()
        {
            pauseObj.SetActive(false);
            resumeObj.SetActive(true);
            Sango.Core.Scenario.NextTurn();
        }

        public void OnBtnDebugAI()
        {

        }

        public void OnBtnGirdShow()
        {
            gridShow = !gridShow;
            MapRender.Instance.ShowGrid(gridShow);
        }

        public void OnTroopListShow()
        {
            troopListShow = !troopListShow;
            troopListObj.SetActive(troopListShow);
            troopListShowText.text = troopListShow ? "隐藏" : "显示";
        }

        public void OnTroopListSelected(int index)
        {
            if (index < 0 || index >= item_list.Count)
                return;

            SangoObject obj = item_list[index];
            if (obj is Troop)
            {
                Troop troop = (Troop)obj;
                Vector3 position = troop.cell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
            else if (obj is City)
            {
                City troop = (City)obj;
                Vector3 position = troop.CenterCell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
        }

        public void OnTroopListShow(UITroopListItem item)
        {
            if (item.index < 0 || item.index >= item_list.Count)
            {
                item.name.text = "无效";
                return;
            }
            SangoObject obj = item_list[item.index];
            if (obj is Troop)
            {
                Troop troop = (Troop)obj;

                if (troop.BelongForce == null)
                {
                    int dd = 33;
                    dd++;
                }
                if (troop.TroopType.isFight)
                    item.name.text = $"[{troop.BelongForce.Name}]<{troop.TroopType.Name}>{troop.Name}队,{troop.Member1?.Name}{troop.Member2?.Name}";
                else
                    item.name.text = $"**[{troop.BelongForce.Name}]<{troop.TroopType.Name}>{troop.Name}运输队,{troop.Member1?.Name}{troop.Member2?.Name}";

                item.name.color = troop.BelongForce.Flag.color;
            }
            else if (obj is City)
            {
                City city = (City)obj;
                if (city.BelongForce != null)
                {
                    item.name.text = $"[{city.BelongForce.Name}]{city.Name}";
                    item.name.color = city.BelongForce.Flag.color;

                }
                else
                {
                    item.name.text = $"{city.Name}";
                    item.name.color = Color.white;
                }

            }
        }

        public void OnTroopTab(Toggle b)
        {
            if (b.isOn)
            {
                itemType = typeof(Troop);
                needUpdateItem = true;
            }
        }

        public void OnCityTab(Toggle b)
        {
            if (b.isOn)
            {
                itemType = typeof(City);
                needUpdateItem = true;
            }
        }

        void Save()
        {
            int count = Scenario.all_scenario_list.Count;
            string savePath = Path.ContentRootPath + $"/Scenario/scenario_save_{count}.json";
            GameEvent.OnGameSave?.Invoke(Scenario.Cur, count, false);
            Scenario.Cur.Save(savePath);
        }

        public void OnSave()
        {
            Save();
            //if (Sango.Core.Scenario.Cur.PauseTrunCount == Sango.Core.Scenario.Cur.Info.turnCount)
            //{
            //    Save();
            //}
            //else
            //{
            //    needSave = true;
            //    OnBtnNextTurn();
            //}
        }

        public void OnLoad()
        {

        }

        public void OnEndPlayerTurn()
        {
            GameSystem.GetSystem<GameInformationSystem>().Back();
            GameSystem.GetSystem<GameSettingInScenario>().Back();

            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            ContextMenu.CloseAll();

            Force force = Scenario.Cur.CurRunForce;
            if (force != null && force.IsPlayer)
            {
                GameSystem.GetSystem<PlayerEndTurn>().Push();
            }
        }

        public void OnSwitchCityInfoShow()
        {
            GameSystem.GetSystem<GameSettingInScenario>().Back();
            GameSystem.GetSystem<GameInformationSystem>().Back();
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            UICityHeadbar.showIndo = !UICityHeadbar.showIndo;
            GameEvent.OnCityHeadbarShowInfoChange?.Invoke();
        }

        public void OnSwitchMiniMapShow()
        {
            GameSystem.GetSystem<GameSettingInScenario>().Back();
            GameSystem.GetSystem<GameInformationSystem>().Back();
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            miniMapObj.SetActive(!miniMapObj.activeSelf);
            miniMapBtnObj.SetActive(!miniMapBtnObj.activeSelf);
        }

        public void OnSwitchMessageShow()
        {
            GameSystem.GetSystem<GameSettingInScenario>().Back();
            GameSystem.GetSystem<GameInformationSystem>().Back();
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            messageObj.SetActive(false);
            GameSystem.GetSystem<PlayerMessage>().onVisibleChange?.Invoke(true); ;
        }

        public void OnGameSetting()
        {
            GameSystem.GetSystem<GameInformationSystem>().Back();
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            Window.Instance.Close("window_city_info_panel");
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Game.Instance.UICamera, gameSettingRect.position);
            GameSystem.GetSystem<GameSettingSystem>().Start(screenPos + new Vector2(0, -gameSettingRect.sizeDelta.y - 5));
        }

        public void OnGameInformation()
        {
            GameSystem.GetSystem<GameSettingInScenario>().Back();
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;
            Window.Instance.Close("window_city_info_panel");
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Game.Instance.UICamera, gameInformationRect.position);
            GameSystem.GetSystem<GameInformationSystem>().Start(screenPos + new Vector2(0, -gameInformationRect.sizeDelta.y - 5));
        }

        public void OnSpeedChange()
        {
            gameSpeed = gameSpeed * 2;
            if (gameSpeed > 8)
                gameSpeed = 1;

            Time.timeScale = gameSpeed;
            speedBtnText.text = $"游戏速度:{(int)gameSpeed}倍";
        }

        public void OnLowFPS()
        {
#if UNITY_STANDALONE_WIN
            if (Application.targetFrameRate == 60)
                Application.targetFrameRate = 120;
            else
                Application.targetFrameRate = 60;

#else
            if (Application.targetFrameRate == 30)
                Application.targetFrameRate = 60;
            else
                Application.targetFrameRate = 30;
#endif
            frameBtnText.text = $"切换帧率:{Application.targetFrameRate}";
        }

        void UpdateFPS()
        {
            float FPS = 1f / deltaTime;
            fpsText.text = $"Ver:{Application.version}  FPS:{Math.Floor(FPS)}";
        }

        void OnMessagePlaneVisible(bool b)
        {
            messageObj?.SetActive(!b);
        }

        public void Update()
        {
            if (needSave)
            {
                if (Sango.Core.Scenario.Cur.PauseTrunCount == Sango.Core.Scenario.Cur.Info.turnCount)
                {
                    Save();
                    needSave = false;
                }
            }

            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

            //if (!troopListShow) return;

            //if (needUpdateItem)
            //{
            //    needUpdateItem = false;
            //    if (itemType == typeof(Troop))
            //    {

            //        item_list.Clear();
            //        Scenario.Cur.troopsSet.ForEach(t =>
            //        {
            //            if (t.IsAlive)
            //            {
            //                item_list.Add(t);
            //            }
            //        });

            //        loopScrollRect.totalCount = item_list.Count;
            //        loopScrollRect.RefillCells(loopScrollRect.GetFirstItem(out _));
            //    }
            //    else if (itemType == typeof(City))
            //    {
            //        item_list.Clear();
            //        Scenario.Cur.citySet.ForEach(t =>
            //        {
            //            if (t.IsAlive)
            //            {
            //                item_list.Add(t);
            //            }
            //        });

            //        loopScrollRect.totalCount = item_list.Count;
            //        loopScrollRect.RefillCells(loopScrollRect.GetFirstItem(out _));
            //    }
            //}
        }

        /*
         * 
         * 三国志11除了春夏秋冬的非战斗BGM外，
            战斗BGM一共有五个：战、劣势、优势（优位）、威风、破竹。
            系统会有一套规则选择播放哪个。
            规则如下：
            对全国42个城市依次做判断：
            一、属于我方领地的城，附近有敌人，附近我方人数小于1W，我方小于敌人的1/3：劣势
            （只要有任何城符合劣势，就劣势优先）
            二、任何城市附近，领地内我方人数大于1W，大于敌人的1/3：优势
            （只要有任何城符合优势，就不会播放战）
            三、没有一个城市符合一、二；但我方部队周围有敌方部队：战。
            四、我方城池数如果大于10，那么优势换成破竹，战换成威风。
            但很不幸的是……基本上，前中期90%的时间都是在听劣势…………
            哪怕敌人1兵运输队兵临城下，城里10W人，也播放劣势……
            后期割草后，90%的时间是破竹……
            战和威风基本听不到。
            因为这套音乐播放规则，个人感觉不是太合理。
            就做了一些改动。
            改动后的规则：
            一、我方领地内有敌人，领地内敌方人数（注意是敌方）大于1W，我方小于敌人的1/4：劣势
            (劣势优先级最高，所以条件应该苛刻点)
            二、任何城市附近，领地内我方人数：
            1、大于5W，大于敌人的两倍：破竹（当然这个5W只是我定的，能改）
            2、大于5W，小于敌人的两倍：威风
            3、小于5W，大于1W，大于敌人的两倍：优势
            4、其他情况，我方部队附近有敌人，战。
            三、城市数大于10这个限制去除，1城也可能出现破竹威风。
         * 
         */

        public int CheckBGM(int dstBgm, Force force, Scenario scenario)
        {
            if (!force.IsPlayer)
            {
                return dstBgm;
            }
            bool hasEnemy = false;
            for (int i = 1; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                int selfTroopNum = 0;
                int enemyTroopNum = 0;
                for (int j = 0; j < city.areaCellList.Count; j++)
                {
                    Troop troop = city.areaCellList[j].troop;
                    if (troop != null)
                    {
                        if (troop.BelongForce == force || troop.BelongForce.IsAlliance(force))
                        {
                            selfTroopNum += troop.troops;
                        }
                        else
                        {
                            enemyTroopNum += troop.troops;
                        }
                    }
                }

                // 优先判断劣势
                if (city.BelongForce == force)
                {
                    if (enemyTroopNum > 10000 && selfTroopNum < enemyTroopNum / 4)
                        return 2246;
                }

                if (selfTroopNum > 50000 && selfTroopNum > enemyTroopNum * 2)
                    return 2247;
                else if (selfTroopNum > 50000 && selfTroopNum <= enemyTroopNum * 2)
                    return 2248;
                else if (selfTroopNum > 10000 && selfTroopNum <= 50000 && selfTroopNum <= enemyTroopNum * 2)
                    return 2245;

                hasEnemy = enemyTroopNum > 0;
            }

            if (hasEnemy)
                return 2244;

            return dstBgm;
        }
    }
}
