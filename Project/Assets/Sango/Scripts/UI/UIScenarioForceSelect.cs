using Sango.Core;
using Sango.Render;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        public RawImage mapBounds_img;

        public Slider mapScaleSlider;
        public int mapDefaultSize = 256;
        public int boundsSize = 448;
        public float mapScale = 1;

        public Button nextBtn;
        public Button unSelectBtn;
        ShortScenario targetScenario;
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

        /// <summary>
        /// 【泄漏修复】本窗口在 Awake 里订阅 OnScenarioInit，之前**没有**任何退订路径：
        /// 窗口按名字缓存、回主菜单时销毁重建，于是每重建一次就多留一份订阅
        /// （GameEventDiagnostics 的 shutdown-begin 跨次对比里 OnScenarioInit 逐轮 +1）。
        /// </summary>
        protected override void OnDestroy()
        {
            GameEvent.OnScenarioInit -= OnScenarioInit;
            base.OnDestroy();
        }

        void OnScenarioInit(Scenario scenario)
        {
            Clear();
        }

        public void Clear()
        {
            playerList.Clear();
        }

        /// <summary>
        /// 安全取剧本对象：集合为 null / 内部数组为 null / Id 越界 / 对象缺失，一律返回 null。
        ///
        /// 【为什么必须这样取】剧本三家集合（forceSet / personSet / citySet）在数据不完整时
        /// 会缺项 —— 原版剧本摘要只登记君主与军师（见 ShortScenario.AddSummaryPerson），
        /// 势力若没有君主就查不到人；普通剧本也可能缺项。直接写 <c>personSet[force.Governor]</c>
        /// 会拿到 null（或抛越界），紧接着 <c>governor.Name</c> 就是 NullReferenceException
        /// （实测栈：UIScenarioForceSelect.SetSelectedForceName 第 91 行）。
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="set">对象集合（可为 null）</param>
        /// <param name="id">对象 Id（0 表示无效值）</param>
        /// <returns>找到的对象；找不到返回 null</returns>
        static T GetSafe<T>(SangoObjectSet<T> set, int id) where T : SangoObject, new()
        {
            if (set == null || set.objects == null || id <= 0)
                return null;
            return set.Get(id);
        }

        void SetSelectedForceName()
        {
            ShortScenario scenario = targetScenario;
            for (int i = 0; i < selectedForceNameLabel.Length; i++)
            {
                if (scenario != null && i < playerList.Count)
                {
                    ShortForce force_sel = playerList[i];
                    // 君主 / 旗帜数据缺失时退化为占位文本与隐藏色块，选势力界面不能因此崩掉
                    ShortPerson governor = GetSafe(scenario.personSet, force_sel.Governor);
                    Flag flag = scenario.CommonData != null ? GetSafe(scenario.CommonData.Flags, force_sel.Flag) : null;
                    if (selectedForceNameLabel[i] != null)
                        selectedForceNameLabel[i].text = governor != null ? governor.Name : $"势力{force_sel.Id}";
                    if (selectedForceColorImg[i] != null)
                    {
                        selectedForceColorImg[i].enabled = flag != null;
                        if (flag != null)
                            selectedForceColorImg[i].color = flag.color;
                    }
                }
                else
                {
                    if (selectedForceNameLabel[i] != null)
                        selectedForceNameLabel[i].text = "";
                    if (selectedForceColorImg[i] != null)
                        selectedForceColorImg[i].enabled = false;
                }
            }
        }

        public override void OnOpen(params object[] objects)
        {
            targetScenario = (ShortScenario)objects[0];
            cityToggleList.Clear();
            ShortScenario scenario = targetScenario;
            // 地图数据缺失时直接退出：Map 为 null 时下面每一行都会空引用
            if (scenario == null || scenario.Map == null)
            {
                Sango.Log.Error("剧本数据缺失(地图 Map 为空)，势力选择界面无法初始化");
                return;
            }
            mapBounds_img.texture = MapRender.LoadBoundTex(scenario.Map.WorkContent);
            selectedForceCountLabel.text = $"{playerList.Count}/{selectedForceNameLabel.Length}";
            scenarioNameLabel.text = scenario.GetDateName();
            if (playerList.Count > 0)
                ShowForce(playerList[playerList.Count - 1]);
            else
                ShowForce((ShortForce)null);
            SetSelectedForceName();
            //nextBtn.interactable = false;
            mapScaleSlider.SetValueWithoutNotify(0);
            UpdateMap(scenario, 0);

        }

        public void OnMapScale(float scale)
        {
            UpdateMap(targetScenario, scale);
        }

        public void UpdateMap(ShortScenario scenario, float scale)
        {
            if (scenario == null || scenario.Map == null)
            {
                Sango.Log.Error("剧本数据缺失(地图 Map 为空)，跳过城池点位刷新");
                return;
            }
            int mapW = scenario.Map.Width;
            int mapH = scenario.Map.Height;

            //int minSize = Math.Min(mapW, mapH);
            int maxSize = Math.Max(mapW, mapH);
            int boundsW = boundsSize * mapW / maxSize;
            int boundsH = boundsSize * mapH / maxSize;

            float targetW = Mathf.LerpUnclamped(boundsW, boundsW * 10, scale);
            float targetH = Mathf.LerpUnclamped(boundsH, boundsH * 10, scale);

            // 【缩放保位】地图框(mapBounds)的轴心与锚点都在中心, 其 anchoredPosition 即"内容中心"相对
            // "视口(map 地图框)中心"的偏移量。缩放时若只改尺寸而保留偏移量, 视口中心对应的地图点会跑掉,
            // 视觉上地图就会跳动。这里先记录缩放前的尺寸与偏移, 反推出视口中心当前对应的那个地图点,
            // 缩放后再把该地图点重新摆回视口中心, 从而保证缩放前后画面位置在视觉上保持不变
            Vector2 oldSize = mapBounds.sizeDelta;
            Vector2 oldPos = mapBounds.anchoredPosition;

            mapBounds.sizeDelta = new Vector2(targetW, targetH);

            if (oldSize.x > 0.001f && oldSize.y > 0.001f)
            {
                // 视口中心对应的地图点(归一化坐标, 取值 0~1, x 自左向右, y 自下向上)
                float centerU = 0.5f - oldPos.x / oldSize.x;
                float centerV = 0.5f - oldPos.y / oldSize.y;
                // 缩放后让同一个地图点依旧落在视口中心
                mapBounds.anchoredPosition = new Vector2((0.5f - centerU) * targetW, (0.5f - centerV) * targetH);
            }

            int i = 0;
            foreach (ShortCity city in scenario.citySet)
            {
                if (city == null) continue;
                if (!city.IsCity()) continue;
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

                if (city.BuildingType == 1)
                    cityObj.transform.localScale = Vector3.one;
                else
                    cityObj.transform.localScale = Vector3.one * 0.75f;

                i++;
                UIMapCitySelectItem toggle = cityObj.GetComponent<UIMapCitySelectItem>();
                if (toggle != null)
                {
                    toggle.shortCity = city;
                    // 用 GetSafe：无主城池（BelongForceId == 0）与"势力数据缺失"统一按不可选处理，
                    // 避免 forceSet[Id] 越界或拿到 null 后再解引用
                    ShortForce shortForce = GetSafe(scenario.forceSet, city.BelongForceId);
                    if (shortForce == null)
                    {
                        toggle.SetSelected(false).SetInavtive(true);
                    }
                    else
                    {
                        Flag flag = scenario.CommonData != null ? GetSafe(scenario.CommonData.Flags, shortForce.Flag) : null;
                        toggle.SetSelected(playerList.Contains(shortForce)).SetInavtive(false);
                        if (flag != null)
                            toggle.SetColor(flag.color);
                        toggle.onSelectShortAction = SetPlayer;
                    }
                    toggle.ShowName(city.Name);
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

            ShortScenario scenario = targetScenario;
            ShortForce force = GetSafe(scenario.forceSet, city.BelongForceId);
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
                ShortScenario scenario = targetScenario;
                if (scenario == null) return;
                int personCount = 0;
                if (scenario.personSet != null)
                {
                    foreach (ShortPerson x in scenario.personSet)
                    {
                        if (x != null && x.BelongForceId == force.Id)
                            personCount++;
                    }
                }

                int cityCount = 0;
                int foodCount = 0;
                int troopsCount = 0;
                int goldCount = 0;

                // 君主 / 军师 / 都城一律安全取值：数据缺失时显示占位，不让详情面板崩掉
                ShortPerson governor = GetSafe(scenario.personSet, force.Governor);
                ShortPerson counsellor = null;
                if (force.Counsellor > 0)
                    counsellor = GetSafe(scenario.personSet, force.Counsellor);
                ShortCity centerCity = governor != null ? GetSafe(scenario.citySet, governor.BelongCityId) : null;
                if (scenario.citySet != null)
                {
                    foreach (ShortCity x in scenario.citySet)
                    {
                        if (x == null) continue;
                        if (x.BelongForceId == force.Id && x.BuildingType == 1)
                        {
                            cityCount++;
                            foodCount += x.food;
                            troopsCount += x.troops;
                            goldCount += x.gold;
                        }
                    }
                }
                forceNameLabel.text = governor != null ? governor.Name : $"势力{force.Id}";
                if (governor != null)
                {
                    forceHead.enabled = true;
                    forceHead.texture = GameRenderHelper.LoadHeadIcon(governor.headIconID, 2);
                }
                else
                {
                    forceHead.enabled = false;
                }
                forceCityLabel.text = centerCity != null ? centerCity.Name : "";
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
            if (city.BelongForceId == 0)
            {
                return;
            }

            ShortScenario scenario = targetScenario;
            ShortForce force = GetSafe(scenario.forceSet, city.BelongForceId);

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
                if (toggle.shortCity.BelongForceId == city.BelongForceId)
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
            if (item.shortCity.BelongForceId <= 0)
                return;

            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                if (toggle.shortCity.BelongForceId == item.shortCity.BelongForceId)
                {
                    toggle.SetOver(true);
                }
            }

            ShowForce(item.shortCity);
        }

        public void PointExitItem(UIMapCitySelectItem item)
        {
            if (item.shortCity.BelongForceId <= 0)
                return;

            for (int i = 0; i < cityToggleList.Count; i++)
            {
                UIMapCitySelectItem toggle = cityToggleList[i];
                if (toggle.shortCity.BelongForceId == item.shortCity.BelongForceId)
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

        /// <summary>
        /// 取势力的"君主所在城池"世界坐标，作为进入游戏时的初始镜头。
        ///
        /// 任何一环数据缺失都返回 false 并记录日志，调用方跳过即可 —— 不再抛
        /// <see cref="System.NullReferenceException"/> 打断"开始游戏"。
        ///
        /// 【为什么需要】<see cref="ShortScenario.personSet"/> / <see cref="ShortScenario.citySet"/>
        /// 字段没有挂 [JsonConverter]，走的是 Newtonsoft 默认反序列化；剧本文件里一旦缺少
        /// objects 数组（写成 null 或整个字段缺失），索引器 <c>personSet[id]</c> 会直接取
        /// <c>objects[id]</c> 而抛 NullReferenceException（实测栈：
        /// UIScenarioForceSelect.StartGame → 第 421 行）。改用 <see cref="SangoObjectSet{T}.Get"/>，
        /// 它在 <c>Check()</c> 里先判越界，越界/缺失统一返回 null。
        /// </summary>
        /// <param name="force">要定位的势力</param>
        /// <param name="position">输出：君主所在城池的世界坐标</param>
        /// <returns>true = 定位成功</returns>
        bool TryGetForceViewPosition(ShortForce force, out Vector3 position)
        {
            position = Vector3.zero;
            if (force == null || targetScenario == null)
                return false;

            if (targetScenario.personSet == null || targetScenario.personSet.objects == null)
            {
                Sango.Log.Error($"剧本[{targetScenario.Name}] 缺少武将数据(personSet)，无法定位势力[{force.Id}]的君主");
                return false;
            }

            ShortPerson person = targetScenario.personSet.Get(force.Governor);
            if (person == null)
            {
                Sango.Log.Error($"剧本[{targetScenario.Name}] 势力[{force.Id}] 的君主 Id={force.Governor} 在武将表中不存在");
                return false;
            }

            if (targetScenario.citySet == null || targetScenario.citySet.objects == null)
            {
                Sango.Log.Error($"剧本[{targetScenario.Name}] 缺少城池数据(citySet)，无法定位君主[{person.Id}]所在城池");
                return false;
            }

            ShortCity city = targetScenario.citySet.Get(person.BelongCityId);
            if (city == null)
            {
                Sango.Log.Error($"剧本[{targetScenario.Name}] 君主[{person.Id}] 的所属城池 Id={person.BelongCityId} 在城池表中不存在");
                return false;
            }

            if (targetScenario.Map == null)
            {
                Sango.Log.Error($"剧本[{targetScenario.Name}] 缺少地图数据(Map)，无法换算镜头坐标");
                return false;
            }

            position = targetScenario.Map.Coords2Position(city.x, city.y);
            return true;
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
                if (targetScenario == null || targetScenario.forceSet == null)
                {
                    Sango.Log.Error("剧本数据缺失(forceSet)，跳过初始镜头定位");
                    break;
                }
                foreach (ShortForce x in targetScenario.forceSet)
                {
                    if (x == null) continue;
                    if (playerList[j].Id == x.Id)
                    {
                        // 用安全取值：剧本数据缺失时只丢镜头，不再抛异常打断开始游戏
                        if (TryGetForceViewPosition(x, out Vector3 position))
                        {
                            Scenario.CurSelected.View = new ScenarioView()
                            {
                                cameraPosition = position,
                                cameraRotation = new Vector3(40f, -50f, 0f),
                                cameraDistance = 400f
                            };
                            isFind = true;
                        }
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
            //                Vector3 position = targetScenario.Map.Coords2Position(city.x, city.y);
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
                // 一个势力都没选（上帝模式）：随便挑第一个能定位的势力当镜头，
                // 同样走安全取值，数据缺失时只是没有初始镜头，不影响进入游戏。
                if (targetScenario != null && targetScenario.forceSet != null)
                {
                    foreach (ShortForce x in targetScenario.forceSet)
                    {
                        ShortForce force = x;
                        if (force == null) continue;
                        if (!TryGetForceViewPosition(force, out Vector3 position)) continue;
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
            Window.Instance.Open("window_scenario_variables", targetScenario);
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
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, "未选择任何势力,将进入上帝放置模式,确定进入游戏吗??", () =>
                {
                    StartGame();
                });
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
