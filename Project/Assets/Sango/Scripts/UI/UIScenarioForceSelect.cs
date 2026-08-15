using Sango.Loader;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    /// <summary>
    /// 剧本势力选择界面
    /// </summary>
    public class UIScenarioForceSelect : UGUIWindow
    {
        public UITextField scenarioNameLabel;
        public UITextField[] selectedForceNameLabel;
        public Image[] selectedForceColorImg;
        public UITextField selectedForceCountLabel;

        public UITextField forceNameLabel;
        public RawImage forceHead;
        public UITextField forceCityLabel;
        public UITextField forceCityCountLabel;
        public UITextField forcePersonCountLabel;
        public UITextField forceDifficultyLabel;
        public UITextField forceGoldLabel;
        public UITextField forceFoodLabel;
        public UITextField forceTroopsLabel;
        public UITextField forceCounsellorLabel;
        public UITextField forceOfficialLabel;
        public UITextField forceFriendLabel;
        public UITextField forceEnemyLabel;

        public UITextField forceInfoLabel;

        public RectTransform mapBounds;

        public Button nextBtn;
        public Button unSelectBtn;

        public GameObject cityObject;
        int MaxSelect = 8;
        List<GameObject> cityList = new List<GameObject>();
        List<UIMapCitySelectItem> cityToggleList = new List<UIMapCitySelectItem>();
        List<ShortForce> playerList = new List<ShortForce>();

        protected override void Awake()
        {
            base.Awake();
            GameEvent.OnScenarioInit += OnScenarioInit;
        }

        void OnScenarioInit(Scenario scenario)
        {
            Clear();
        }

        public void Clear()
        {
            playerList.Clear();
        }

        void SetSelectedForceName()
        {
            ShortScenario scenario = ShortScenario.CurSelected;
            for (int i = 0; i < selectedForceNameLabel.Length; i++)
            {
                if (i < playerList.Count)
                {
                    ShortForce force_sel = playerList[i];
                    ShortPerson governor = scenario.personSet[force_sel.Governor];
                    Flag flag = scenario.CommonData.Flags[force_sel.Flag];
                    selectedForceNameLabel[i].text = governor.Name;
                    selectedForceColorImg[i].enabled = true;
                    selectedForceColorImg[i].color = flag.color;

                }
                else
                {
                    selectedForceNameLabel[i].text = "";
                    selectedForceColorImg[i].enabled = false;
                }
            }
        }

        public override void OnOpen()
        {
            cityToggleList.Clear();
            ShortScenario scenario = ShortScenario.CurSelected;
            scenario.LoadFullPersonContent();

            selectedForceCountLabel.text = $"{playerList.Count}/{selectedForceNameLabel.Length}";
            scenarioNameLabel.text = scenario.GetDateName();
            if (playerList.Count > 0)
                ShowForce(playerList[playerList.Count - 1]);
            else
                ShowForce((ShortForce)null);
            SetSelectedForceName();
            //nextBtn.interactable = false;
            int i = 0;
            foreach (ShortCity city in scenario.citySet)
            {
                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;
                GameObject cityObj;
                if (i >= cityList.Count)
                {
                    cityObj = GameObject.Instantiate(cityObject, cityObject.transform.parent);
                    cityList.Add(cityObj);
                    cityObj.name = city.Id.ToString();
                }
                else
                {
                    cityObj = cityList[i];
                    cityObj.name = city.Id.ToString();
                }

                i++;
                UIMapCitySelectItem toggle = cityObj.GetComponent<UIMapCitySelectItem>();
                if (toggle != null)
                {
                    toggle.shortCity = city;
                    if (city.BelongForce == 0)
                    {
                        toggle.SetSelected(false).SetInavtive(true);
                    }
                    else
                    {
                        ShortForce shortForce = scenario.forceSet[city.BelongForce];
                        Flag flag = scenario.CommonData.Flags[shortForce.Flag];
                        toggle.SetSelected(playerList.Contains(shortForce)).SetInavtive(false).SetColor(flag.color).onSelectShortAction = SetPlayer;
                    }
                    cityToggleList.Add(toggle);
                }

                RectTransform rectTransform = cityObj.GetComponent<RectTransform>();
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);

                cityObj.SetActive(true);
            }
        }

        void ShowForce(ShortCity city)
        {
            if (city == null)
            {
                ShowForce((ShortForce)null);
                return;
            }

            ShortScenario scenario = ShortScenario.CurSelected;
            ShortForce force = scenario.forceSet[city.BelongForce];
            ShowForce(force);
        }

        void ShowForce(ShortForce force)
        {
            if (force == null)
            {
                forceNameLabel.text = "";
                forceHead.enabled = false;
                forceCityLabel.text = "";
                forceCityCountLabel.text = "";
                forcePersonCountLabel.text = "";
                forceDifficultyLabel.text = "";
                forceGoldLabel.text = "";
                forceFoodLabel.text = "";
                forceTroopsLabel.text = "";
                forceCounsellorLabel.text = "";
                forceOfficialLabel.text = "";
                forceFriendLabel.text = "";
                forceEnemyLabel.text = "";
                forceInfoLabel.text = "";


            }
            else
            {
                ShortScenario scenario = ShortScenario.CurSelected;
                int personCount = 0;
                foreach (ShortPerson x in scenario.personSet)
                {
                    if (x.BelongForce == force.Id)
                        personCount++;
                }

                int cityCount = 0;
                int foodCount = 0;
                int troopsCount = 0;
                int goldCount = 0;

                ShortPerson governor = scenario.personSet[force.Governor];
                ShortPerson counsellor = null;
                if (force.Counsellor > 0)
                    counsellor = scenario.personSet[force.Counsellor];
                ShortCity centerCity = scenario.citySet[governor.BelongCity];
                foreach (ShortCity x in scenario.citySet)
                {
                    if (x.BelongForce == force.Id && x.BuildingType == 1)
                    {
                        cityCount++;
                        foodCount += x.food;
                        troopsCount += x.troops;
                        goldCount += x.gold;
                    }
                }
                forceNameLabel.text = governor.Name;
                forceHead.enabled = true;
                forceHead.texture = GameRenderHelper.LoadHeadIcon(ShortScenario.CurSelected.personSet[force.Governor].headIconID, 2);
                forceCityLabel.text = centerCity.Name;
                forceCityCountLabel.text = cityCount.ToString();
                forcePersonCountLabel.text = personCount.ToString();
                forceDifficultyLabel.text = "";
                forceGoldLabel.text = goldCount.ToString();
                forceFoodLabel.text = foodCount.ToString();
                forceTroopsLabel.text = troopsCount.ToString();
                forceCounsellorLabel.text = counsellor?.Name ?? "";
                forceOfficialLabel.text = "";
                forceFriendLabel.text = "";
                forceEnemyLabel.text = "";
                forceInfoLabel.text = force.desc;
            }
        }

        public void SetPlayer(UIMapCitySelectItem item, ShortCity city, bool b)
        {
            if (city.BelongForce == 0)
            {
               return;
            }

            ShortScenario scenario = ShortScenario.CurSelected;
            ShortForce force = scenario.forceSet[city.BelongForce];

            if (force == null) return;

            if (b)
            {
                if (playerList.Count < selectedForceNameLabel.Length)
                    playerList.Add(force);
                else
                {
                    item.SetSelected(false);
                    return;
                }
            }
            else
            {
                playerList.Remove(force);
            }

            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                if (toggle.shortCity.BelongForce == city.BelongForce)
                {
                    toggle.SetSelected(b);
                }
            }

            selectedForceCountLabel.text = $"{playerList.Count}/{selectedForceNameLabel.Length}";

            //nextBtn.interactable = playerList.Count > 0;

            SetSelectedForceName();
            unSelectBtn.interactable = playerList.Count > 0;

            if (!b)
            {
                if (playerList.Count == 0)
                {
                    ShowForce((ShortForce)null);
                    return;
                }
                else
                {
                    force = playerList[playerList.Count - 1];
                    ShowForce(force);
                }
            }
            else
            {
                ShowForce(force);
            }
        }


        public void PointEnterItem(UIMapCitySelectItem item)
        {
            if (item.shortCity.BelongForce <= 0)
                return;

            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                if (toggle.shortCity.BelongForce == item.shortCity.BelongForce)
                {
                    toggle.SetOver(true);
                }
            }

            ShowForce(item.shortCity);
        }

        public void PointExitItem(UIMapCitySelectItem item)
        {
            if (item.shortCity.BelongForce <= 0)
                return;

            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                if (toggle.shortCity.BelongForce == item.shortCity.BelongForce)
                {
                    toggle.SetOver(false);
                }
            }

            ShowForce((ShortForce)null);
        }

        public override void OnClose()
        {
            for (int i = 0; i < cityList.Count; i++)
            {
                cityList[i].SetActive(false);
            }
        }

        public void OnReturn()
        {
            playerList.Clear();
            Scenario.CurSelected.Clear();
            Window.Instance.Open("window_scenario_select");
            Window.Instance.Close("window_scenario_force_select");
        }

        public void StartGame()
        {
            List<int> forceIds = new List<int>();
            for (int i = 0; i < playerList.Count; i++)
                forceIds.Add(playerList[i].Id);
            bool isFind = false;
            for (int j = 0; j < playerList.Count; j++)
            {
                // 确定第一个视角
                foreach (ShortForce x in ShortScenario.CurSelected.forceSet)
                {
                    if (playerList[j].Id == x.Id)
                    {
                        ShortPerson person = ShortScenario.CurSelected.personSet[playerList[j].Governor];
                        ShortCity city = ShortScenario.CurSelected.citySet[person.BelongCity];
                        Vector3 position = ShortScenario.CurSelected.Map.Coords2Position(city.x, city.y);
                        Scenario.CurSelected.View = new ScenarioView()
                        {
                            cameraPosition = position,
                            cameraRotation = new Vector3(40f, -50f, 0f),
                            cameraDistance = 400f
                        };
                        isFind = true;
                        break;
                    }
                }
                if (isFind) break;
            }

            //if(Scenario.CurSelected.View == null)
            //{
            //    for (int j = 0; j < playerList.Count; j++)
            //    {
            //        ScenarioPersonAddData scenarioAddon = UIScenarioAddonMenu.AddData;
            //        foreach (var item in scenarioAddon.NewForces)
            //        {
            //            if (playerList[j].Id == item.ForceId)
            //            {
            //                ShortCity city = item.CapitalCity;
            //                Vector3 position = ShortScenario.CurSelected.Map.Coords2Position(city.x, city.y);
            //                Scenario.CurSelected.View = new ScenarioView()
            //                {
            //                    cameraPosition = position,
            //                    cameraRotation = new Vector3(40f, -50f, 0f),
            //                    cameraDistance = 400f
            //                };
            //                isFind = true;
            //                break;
            //            }
            //            if (isFind) break;
            //        }
            //    }
            //}


            if (playerList.Count == 0)
            {
                foreach (ShortForce x in ShortScenario.CurSelected.forceSet)
                {
                    ShortForce force = x;
                    if (force != null)
                    {
                        ShortPerson person = ShortScenario.CurSelected.personSet[force.Governor];
                        ShortCity city = ShortScenario.CurSelected.citySet[person.BelongCity];
                        Vector3 position = ShortScenario.CurSelected.Map.Coords2Position(city.x, city.y);
                        Scenario.CurSelected.View = new ScenarioView()
                        {
                            cameraPosition = position,
                            cameraRotation = new Vector3(40f, -50f, 0f),
                            cameraDistance = 400f
                        };
                        break;
                    }
                }
            }
            Scenario.CurSelected.Info.playerForceList = forceIds.ToArray();
            Window.Instance.Open("window_scenario_variables");
            Window.Instance.Close("window_scenario_force_select");
            //
            //Window.Instance.Open("window_loading");
            //Window.Instance.Close("window_scenario_force_select");
            //Scenario.StartScenario(Scenario.CurSelected, forceIds);
        }

        public void OnNext()
        {
            if (playerList.Count == 0)
            {
                GameDialog.Open("未选择任何势力,将进入上帝放置模式,确定进入游戏吗??", () =>
                {
                    GameDialog.Close();
                    StartGame();
                }).cancelAction = () =>
                {
                    GameDialog.Close();
                };
                return;
            }

            StartGame();
        }

        public void Update()
        {
            //GameObject gameObject = EventSystem.current.currentSelectedGameObject;
            //if (gameObject != null)
            //{
            //    Debug.LogError(gameObject.name);
            //}
        }

        public void UnSelectAllPlayer()
        {
            playerList.Clear();
            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                toggle.SetSelected(false);
            }
            SetSelectedForceName();
            ShowForce((ShortForce)null);
            unSelectBtn.interactable = false;
        }

        public void SwitchSelectShow()
        {

        }
    }
}
