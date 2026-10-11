using UnityEngine;
using Sango.MapGen;

namespace Sango.Tools
{
    public class MapEditorWindows
    {
        private MapEditor editor;
        
        // 顶部菜单高度常量
        private const float MENU_BAR_HEIGHT = 30f;
        
        private bool showSettingsWindow = false;
        private bool showNewMapWindow = false;
        private bool showLightWindow = false;
        private bool showFogWindow = false;
        
        private UnityEngine.Rect settingsWindowRect;
        private UnityEngine.Rect newMapWindowRect;
        private UnityEngine.Rect lightWindowRect;
        private UnityEngine.Rect fogWindowRect;
        
        private int newMapWidth = 1024;
        private int newMapHeight = 1024;
        private string newMapKey = "";

        // 程序化生成地图窗口
        private bool showMapGenWindow = false;
        private UnityEngine.Rect mapGenWindowRect;
        private Vector2 mapGenScrollPosition = Vector2.zero;
        private MapGenConfig mapGenConfig = new MapGenConfig();
        private RiverGenConfig mapGenRiverConfig = new RiverGenConfig();
        private bool mapGenWithRiver = true;
        private CityGenConfig mapGenCityConfig = new CityGenConfig();
        private bool mapGenWithCity = true;
        private RoadGenConfig mapGenRoadConfig = new RoadGenConfig();
        private bool mapGenWithRoad = true;
        private string mapGenResultText = "尚未生成";

        private Vector2 settingsScrollPosition = Vector2.zero;

        public MapEditorWindows(MapEditor editor)
        {
            this.editor = editor;
        }

        public void Draw()
        {
            DrawSettingsWindow();
            DrawNewMapWindow();
            DrawMapGenWindow();
            DrawLightWindow();
            DrawFogWindow();
        }

        public void ShowSettingsWindow()
        {
            showSettingsWindow = true;
        }

        public void ShowNewMapWindow()
        {
            showNewMapWindow = true;
        }

        /// <summary>
        /// 显示程序化生成地图窗口
        /// </summary>
        public void ShowMapGenWindow()
        {
            showMapGenWindow = true;
        }

        public void ShowLightWindow()
        {
            showLightWindow = true;
        }

        public void ShowFogWindow()
        {
            showFogWindow = true;
        }

        private void DrawSettingsWindow()
        {
            if (showSettingsWindow)
            {
                if (settingsWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    settingsWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 280,
                        windowY,
                        560,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (settingsWindowRect.y < MENU_BAR_HEIGHT)
                {
                    settingsWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(settingsWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(settingsWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("⚙️ 设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showSettingsWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                settingsScrollPosition = GUILayout.BeginScrollView(settingsScrollPosition, GUILayout.Height(410));

                DrawSettingsSection("⌨️ 快捷键设置", () =>
                {
                    DrawSettingRow("保存快捷键", $"Ctrl + {editor.shortcuts.save}", () => { });
                    DrawSettingRow("撤销快捷键", $"Ctrl + {editor.shortcuts.undo}", () => { });
                    DrawSettingRow("重做快捷键", $"Ctrl + {editor.shortcuts.redo}", () => { });
                    DrawSettingRow("加载快捷键", $"Ctrl + {editor.shortcuts.load}", () => { });
                    DrawSettingRow("切换视角", $"{editor.shortcuts.toggleView}", () => { });

                    if (GUILayout.Button("重置为默认", GUILayout.Width(150)))
                    {
                        editor.shortcuts.ResetToDefault();
                    }
                });

                DrawSettingsSection("� MapKey设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("地图标识:", GUILayout.Width(100));
                    editor.map.WorkContent = GUILayout.TextField(editor.map.WorkContent, GUILayout.Width(300));
                    GUILayout.EndHorizontal();
                });

                DrawSettingsSection("💾 自动保存设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("启用自动保存:", GUILayout.Width(130));
                    editor.autoSave.AutoSaveEnabled = GUILayout.Toggle(editor.autoSave.AutoSaveEnabled, "");
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("自动保存间隔(分钟):", GUILayout.Width(150));
                    float interval = EditorUtility.FloatField(editor.autoSave.AutoSaveInterval / 60f, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        interval = Mathf.Clamp(interval, 1f, 60f);
                        editor.autoSave.AutoSaveInterval = interval * 60f;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("自动保存数量限制:", GUILayout.Width(150));
                    int limit = EditorUtility.IntField(editor.autoSave.AutoSaveLimit, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        limit = Mathf.Clamp(limit, 1, 20);
                        editor.autoSave.AutoSaveLimit = limit;
                    }
                    GUILayout.EndHorizontal();
                });

                DrawSettingsSection("🎥 相机设置", () =>
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("移动速度:", GUILayout.Width(130));
                    float moveSpeed = EditorUtility.FloatField(editor.map.mapCamera.keyBoardMoveSpeed, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        editor.map.mapCamera.keyBoardMoveSpeed = moveSpeed;
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("可视距离:", GUILayout.Width(130));
                    float viewDistance = EditorUtility.FloatField(editor.map.showLimitLength, GUILayout.MaxWidth(70));
                    if (GUI.changed)
                    {
                        editor.map.showLimitLength = viewDistance;
                    }
                    GUILayout.EndHorizontal();
                });

                GUILayout.EndScrollView();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    ApplySettings();
                }
                if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showSettingsWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawNewMapWindow()
        {
            if (showNewMapWindow)
            {
                if (newMapWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 200); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    newMapWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 250,
                        windowY,
                        500,
                        400
                    ); 
                }

                // 确保窗口不会与顶部菜单重叠
                if (newMapWindowRect.y < MENU_BAR_HEIGHT)
                {
                    newMapWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(newMapWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(newMapWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("🗺️ 新建地图", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showNewMapWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("地图参数", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);

                GUILayout.BeginHorizontal();
                GUILayout.Label("宽度:", GUILayout.Width(100));
                string widthStr = GUILayout.TextField(newMapWidth.ToString(), GUILayout.Width(100));
                if (int.TryParse(widthStr, out int width) && width > 0)
                {
                    newMapWidth = width;
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("高度:", GUILayout.Width(100));
                string heightStr = GUILayout.TextField(newMapHeight.ToString(), GUILayout.Width(100));
                if (int.TryParse(heightStr, out int height) && height > 0)
                {
                    newMapHeight = height;
                }
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                GUILayout.Space(12);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("MapKey <color=red>*必填</color>", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);

                GUILayout.Label("剧本地图需要填写此标识,会在Assets/Map/下面新建一个此名字文件夹,地图相关资源会优先在此存放和读取!!");

                GUILayout.BeginHorizontal();
                GUILayout.Label("地图标识:", GUILayout.Width(100));
                newMapKey = GUILayout.TextField(newMapKey, GUILayout.Width(350));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(15);

                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("创建", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    if (newMapWidth <= 0 || newMapHeight <= 0)
                    {
                        Sango.Log.Error("地图宽度和高度必须为正整数");
                        return;
                    }
                    if (string.IsNullOrEmpty(newMapKey))
                    {
                        Sango.Log.Error("MapKey不能为空");
                        return;
                    }

                    editor.CreateNewMap(newMapWidth, newMapHeight, newMapKey);
                    editor.lastSavedPath = "";
                    showNewMapWindow = false;
                    Sango.Log.Info($"已创建新地图: {newMapWidth} x {newMapHeight}");
                }
                if (GUILayout.Button("取消", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showNewMapWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        /// <summary>
        /// 绘制程序化生成地图窗口
        /// </summary>
        private void DrawMapGenWindow()
        {
            if (!showMapGenWindow)
                return;

            if (mapGenWindowRect.width == 0)
            {
                float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 300);
                mapGenWindowRect = new UnityEngine.Rect(
                    Screen.width / 2 - 300,
                    windowY,
                    600,
                    600
                );
            }

            // 确保窗口不会与顶部菜单重叠
            if (mapGenWindowRect.y < MENU_BAR_HEIGHT)
                mapGenWindowRect.y = MENU_BAR_HEIGHT;

            GUI.DrawTexture(mapGenWindowRect, CreateWindowBackground());

            GUILayout.BeginArea(mapGenWindowRect);

            GUILayout.BeginHorizontal();
            GUILayout.Label("程序化生成地图", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
            {
                showMapGenWindow = false;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(10);

            mapGenScrollPosition = GUILayout.BeginScrollView(mapGenScrollPosition, GUILayout.Height(430));

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("基础参数", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            GUILayout.Label("随机种子:", GUILayout.Width(120));
            string seedStr = GUILayout.TextField(mapGenConfig.seed.ToString(), GUILayout.Width(120));
            if (int.TryParse(seedStr, out int seed))
                mapGenConfig.seed = seed;
            if (GUILayout.Button("随机", GUILayout.Width(60)))
            {
                mapGenConfig.seed = UnityEngine.Random.Range(0, 100000000);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("原版骨架(中国):", GUILayout.Width(120));
            mapGenConfig.useSkeleton = GUILayout.Toggle(mapGenConfig.useSkeleton, "启用", GUILayout.Width(60));
            GUILayout.Label("高度权重", GUILayout.Width(60));
            mapGenConfig.skeletonWeight = GUILayout.HorizontalSlider(mapGenConfig.skeletonWeight, 0f, 1f, GUILayout.Width(180));
            GUILayout.Label(mapGenConfig.skeletonWeight.ToString("F2"), GUILayout.Width(50));
            GUILayout.EndHorizontal();
            GUILayout.Label("骨架：" + mapGenConfig.skeletonPath, new GUIStyle(GUI.skin.label) { fontSize = 11 });

            DrawSliderRow("陆地占比", ref mapGenConfig.landRatio, 0.05f, 0.95f);
            DrawSliderRow("山地占比", ref mapGenConfig.mountainRatio, 0f, 0.6f);
            DrawSliderRow("浅滩占比", ref mapGenConfig.shallowRatio, 0f, 0.6f);
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("气候与地表", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            DrawSliderRow("森林阈值", ref mapGenConfig.wetThreshold, 0.3f, 0.9f);
            DrawSliderRow("荒地阈值", ref mapGenConfig.dryThreshold, 0.1f, 0.7f);
            DrawSliderRow("砂地阈值", ref mapGenConfig.sandThreshold, 0f, 0.4f);
            DrawSliderRow("湿地阈值", ref mapGenConfig.swampThreshold, 0.5f, 0.95f);
            DrawSliderRow("草地/土分割", ref mapGenConfig.soilThreshold, 0f, 1f);
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("噪声与细节", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            DrawSliderRow("地形特征尺寸", ref mapGenConfig.terrainFeatureSize, 4f, 128f);
            DrawSliderRow("气候特征尺寸", ref mapGenConfig.climateFeatureSize, 8f, 256f);
            DrawSliderRow("大陆频率", ref mapGenConfig.continentScale, 0.05f, 2f);
            DrawSliderRow("大陆权重", ref mapGenConfig.continentWeight, 0f, 1f);
            DrawSliderRow("边缘衰减(格)", ref mapGenConfig.edgeFalloffCells, 0f, 80f);
            DrawIntSliderRow("海平面高度", ref mapGenConfig.seaLevelByte, 1, 60);
            DrawIntSliderRow("水面高度", ref mapGenConfig.waterByte, 1, 60);
            DrawIntSliderRow("最高高度", ref mapGenConfig.maxLandByte, 30, 255);
            DrawSliderRow("陆地高度曲线", ref mapGenConfig.landHeightCurve, 1f, 8f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("生成底图:", GUILayout.Width(120));
            mapGenConfig.generateBaseMap = GUILayout.Toggle(mapGenConfig.generateBaseMap, "启用", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            DrawSliderRow("底图地形色权重", ref mapGenConfig.baseMapTint, 0f, 1f);
            DrawSliderRow("底图明暗强度", ref mapGenConfig.baseMapShade, 0f, 1f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("底图四季差异:", GUILayout.Width(120));
            mapGenConfig.baseMapSeasonal = GUILayout.Toggle(mapGenConfig.baseMapSeasonal, "启用(冬季含积雪)", GUILayout.Width(160));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("崖贴图分布:", GUILayout.Width(120));
            mapGenConfig.cliffLayerSpread = GUILayout.Toggle(mapGenConfig.cliffLayerSpread, "启用(按坡度混合岩石贴图)", GUILayout.Width(200));
            GUILayout.EndHorizontal();
            DrawSliderRow("陡坡阈值", ref mapGenConfig.cliffSteepSlope, 2f, 60f);
            DrawSliderRow("缓坡阈值", ref mapGenConfig.cliffGentleSlope, 0f, 30f);
            GUILayout.Space(4);
            if (GUILayout.Button("初始化四季贴图层(36 层地表 + 水面)", GUILayout.Height(26)))
            {
                if (editor.map == null || editor.map.mapLayer == null)
                {
                    mapGenResultText = "初始化失败：当前没有地图";
                }
                else
                {
                    int layers = SeasonLayerSetup.Setup(editor.map,
                        SeasonLayerSetup.DefaultTerrainLayerCount, true);
                    mapGenResultText = layers > 0
                        ? string.Format("四季贴图层已初始化：共 {0} 层(地表 {1} + 水面 1)", layers, layers - 1)
                        : "四季贴图层初始化失败";
                }
            }
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("河流", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("同时生成河流:", GUILayout.Width(120));
            mapGenWithRiver = GUILayout.Toggle(mapGenWithRiver, "启用", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("成河汇流量:", GUILayout.Width(120));
            mapGenRiverConfig.flowThreshold = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenRiverConfig.flowThreshold, 4f, 60f, GUILayout.Width(320)));
            GUILayout.Label(mapGenRiverConfig.flowThreshold.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("大江加宽倍数:", GUILayout.Width(120));
            mapGenRiverConfig.widenFlowFactor = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenRiverConfig.widenFlowFactor, 0f, 10f, GUILayout.Width(320)));
            GUILayout.Label(mapGenRiverConfig.widenFlowFactor.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("大江判定倍数:", GUILayout.Width(120));
            mapGenRiverConfig.majorRiverFactor = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenRiverConfig.majorRiverFactor, 0f, 20f, GUILayout.Width(320)));
            GUILayout.Label(mapGenRiverConfig.majorRiverFactor.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("河川水面落差:", GUILayout.Width(120));
            mapGenConfig.riverDrop = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenConfig.riverDrop, 1f, 40f, GUILayout.Width(320)));
            GUILayout.Label(mapGenConfig.riverDrop.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("河川岸线下沉:", GUILayout.Width(120));
            mapGenConfig.riverBankDrop = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenConfig.riverBankDrop, 0f, 20f, GUILayout.Width(320)));
            GUILayout.Label(mapGenConfig.riverBankDrop.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("城市与领地", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("生成城市:", GUILayout.Width(120));
            mapGenWithCity = GUILayout.Toggle(mapGenWithCity, "启用", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("城市数量:", GUILayout.Width(120));
            mapGenCityConfig.cityCount = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenCityConfig.cityCount, 2f, 120f, GUILayout.Width(320)));
            GUILayout.Label(mapGenCityConfig.cityCount.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("最小间距(格):", GUILayout.Width(120));
            mapGenCityConfig.minSpacing = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenCityConfig.minSpacing, 2f, 20f, GUILayout.Width(320)));
            GUILayout.Label(mapGenCityConfig.minSpacing.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("内政半径(格):", GUILayout.Width(120));
            mapGenCityConfig.interiorRadius = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenCityConfig.interiorRadius, 0f, 8f, GUILayout.Width(320)));
            GUILayout.Label(mapGenCityConfig.interiorRadius.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("道路", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUILayout.Label("生成道路:", GUILayout.Width(120));
            mapGenWithRoad = GUILayout.Toggle(mapGenWithRoad, "启用", GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("山地代价:", GUILayout.Width(120));
            mapGenRoadConfig.cliffCost = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenRoadConfig.cliffCost, 5f, 120f, GUILayout.Width(320)));
            GUILayout.Label(mapGenRoadConfig.cliffCost.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("渡河代价:", GUILayout.Width(120));
            mapGenRoadConfig.riverCost = Mathf.RoundToInt(GUILayout.HorizontalSlider(mapGenRoadConfig.riverCost, 5f, 120f, GUILayout.Width(320)));
            GUILayout.Label(mapGenRoadConfig.riverCost.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(8);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("生成结果", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(6);
            GUILayout.Label(mapGenResultText);
            GUILayout.EndVertical();

            GUILayout.EndScrollView();

            GUILayout.Space(10);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("开始生成", GUILayout.Width(120), GUILayout.Height(32)))
            {
                MapGenResult result = MapGenerator.GenerateAll(editor.map, mapGenConfig,
                    mapGenWithRiver ? mapGenRiverConfig : null,
                    mapGenWithCity ? mapGenCityConfig : null,
                    null,
                    (mapGenWithCity && mapGenWithRoad) ? mapGenRoadConfig : null);
                mapGenResultText = BuildMapGenResultText(result);
            }
            if (GUILayout.Button("写入剧本城市", GUILayout.Width(140), GUILayout.Height(32)))
            {
                if (editor.scenario == null)
                {
                    mapGenResultText = "未加载剧本：请先用「文件/从剧本加载地图」加载剧本";
                }
                else if (MapGenContext.Current == null || MapGenContext.Current.cities == null)
                {
                    mapGenResultText = "尚未生成城市：请先点击「开始生成」";
                }
                else
                {
                    ScenarioCityGenConfig scenarioCfg = new ScenarioCityGenConfig
                    {
                        buildingTypeId = 1,
                        autoExport = true
                    };
                    ScenarioCityResult scenarioResult = ScenarioCityBuilder.Build(
                        editor.scenario, MapGenContext.Current, scenarioCfg);
                    mapGenResultText = scenarioResult.message;
                }
            }
            if (GUILayout.Button("关闭", GUILayout.Width(100), GUILayout.Height(32)))
            {
                showMapGenWindow = false;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        /// <summary>
        /// 绘制一行带数值显示的滑动条
        /// </summary>
        /// <param name="label">标签</param>
        /// <param name="value">被编辑的数值</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        private void DrawSliderRow(string label, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(120));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(320));
            GUILayout.Label(value.ToString("F3"), GUILayout.Width(60));
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制一行带数值显示的整数滑动条
        /// </summary>
        /// <param name="label">标签</param>
        /// <param name="value">被编辑的整数</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        private void DrawIntSliderRow(string label, ref int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(120));
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(320)));
            GUILayout.Label(value.ToString(), GUILayout.Width(60));
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 把生成结果整理为中文文本
        /// </summary>
        /// <param name="result">生成结果</param>
        /// <returns>展示文本</returns>
        private string BuildMapGenResultText(MapGenResult result)
        {
            if (result == null)
                return "生成失败";

            if (!result.success)
                return result.message;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(result.message);
            sb.AppendFormat("水域: {0}    浅滩: {1}    山地(崖): {2}\n",
                result.terrainCounts[9], result.terrainCounts[14], result.terrainCounts[16]);
            sb.AppendFormat("草地: {0}    土: {1}    砂地: {2}\n",
                result.terrainCounts[1], result.terrainCounts[2], result.terrainCounts[3]);
            sb.AppendFormat("森林: {0}    荒地: {1}    湿地: {2}\n",
                result.terrainCounts[6], result.terrainCounts[10], result.terrainCounts[4]);
            sb.AppendFormat("都市: {0}    主径: {1}    栈道: {2}    渡所: {3}\n",
                result.terrainCounts[17], result.terrainCounts[11], result.terrainCounts[12], result.terrainCounts[13]);
            return sb.ToString();
        }

        private void DrawLightWindow()
        {
            if (showLightWindow)
            {
                if (lightWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    lightWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 300,
                        windowY,
                        600,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (lightWindowRect.y < MENU_BAR_HEIGHT)
                {
                    lightWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(lightWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(lightWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("💡 灯光设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showLightWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                string[] seasons = { "秋", "春", "夏", "冬" };

                // 保存原始背景颜色
                Color originalColor = GUI.backgroundColor;
                
                for (int i = 0; i < 4; i++)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();

                    // 如果是当前季节，设置较亮的颜色
                    if (i == editor.map.curSeason)
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f); // 亮蓝色
                    }
                    else
                    {
                        GUI.backgroundColor = originalColor;
                    }
                    
                    if (GUILayout.Button(seasons[i], GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        editor.map.curSeason = i;
                        foreach (BrushBase brush in editor.brushes)
                        {
                            brush.OnSeasonChanged(i);
                        }
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("灯光颜色:", GUILayout.Width(70));
                    int seasonIndex = i;
                    EditorUtility.ColorField(editor.map.mapLight.light_color[seasonIndex], "", (color) => { editor.map.mapLight.light_color[seasonIndex] = color; });

                    GUILayout.Label("灯光强度:", GUILayout.Width(70));
                    editor.map.mapLight.light_intensity[i] = EditorUtility.FloatField(editor.map.mapLight.light_intensity[i], GUILayout.Width(80));
                    editor.map.mapLight.light_intensity[i] = Mathf.Max(0, editor.map.mapLight.light_intensity[i]);

                    GUILayout.Label("灯光方向:", GUILayout.Width(70));
                    editor.map.mapLight.light_direction[i] = EditorUtility.Vector3Field(editor.map.mapLight.light_direction[i], "");

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(10);
                }
                
                // 恢复原始背景颜色
                GUI.backgroundColor = originalColor;

                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    editor.map.mapLight.UpdateRender();
                    string message = "灯光设置已应用";
                    Sango.Log.Info(message);
                    editor.autoSave.ShowSaveNotification(message);
                }
                if (GUILayout.Button("关闭", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showLightWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawFogWindow()
        {
            if (showFogWindow)
            {
                if (fogWindowRect.width == 0)
                {
                    float windowY = Mathf.Max(MENU_BAR_HEIGHT, Screen.height / 2 - 280); // 确保窗口顶部至少距离顶部菜单，避免与顶部菜单重叠
                    fogWindowRect = new UnityEngine.Rect(
                        Screen.width / 2 - 300,
                        windowY,
                        600,
                        560
                    );
                }

                // 确保窗口不会与顶部菜单重叠
                if (fogWindowRect.y < MENU_BAR_HEIGHT)
                {
                    fogWindowRect.y = MENU_BAR_HEIGHT;
                }

                GUI.DrawTexture(fogWindowRect, CreateWindowBackground());

                GUILayout.BeginArea(fogWindowRect);

                GUILayout.BeginHorizontal();
                GUILayout.Label("🌫️ 雾效设置", new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    showFogWindow = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(15);

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("雾效显示开关", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUILayout.Label("启用雾效:", GUILayout.Width(80));
                RenderSettings.fog = GUILayout.Toggle(RenderSettings.fog, "");
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(12);

                string[] seasons = { "秋", "春", "夏", "冬" };

                // 保存原始背景颜色
                Color originalColor = GUI.backgroundColor;
                
                for (int i = 0; i < 4; i++)
                {
                    GUILayout.BeginVertical(GUI.skin.box);
                    GUILayout.BeginHorizontal();

                    // 如果是当前季节，设置较亮的颜色
                    if (i == editor.map.curSeason)
                    {
                        GUI.backgroundColor = new Color(0.7f, 0.9f, 1.0f); // 亮蓝色
                    }
                    else
                    {
                        GUI.backgroundColor = originalColor;
                    }
                    
                    if (GUILayout.Button(seasons[i], GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        editor.map.curSeason = i;
                        foreach (BrushBase brush in editor.brushes)
                        {
                            brush.OnSeasonChanged(i);
                        }
                    }

                    GUILayout.Space(15);

                    GUILayout.Label("雾颜色:", GUILayout.Width(60));
                    int fogSeasonIndex = i;
                    EditorUtility.ColorField(editor.map.mapFog.fog_color[fogSeasonIndex], "", (color) => { 
                        editor.map.mapFog.fog_color[fogSeasonIndex] = color;
                        if (fogSeasonIndex == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    });

                    GUILayout.Label("起点距离:", GUILayout.Width(70));
                    float newStart = EditorUtility.FloatField(editor.map.mapFog.fog_start[i], GUILayout.Width(80));
                    if (newStart != editor.map.mapFog.fog_start[i])
                    {
                        editor.map.mapFog.fog_start[i] = Mathf.Max(0, newStart);
                        if (i == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    }

                    GUILayout.Label("结束距离:", GUILayout.Width(70));
                    float newEnd = EditorUtility.FloatField(editor.map.mapFog.fog_end[i], GUILayout.Width(80));
                    if (newEnd != editor.map.mapFog.fog_end[i])
                    {
                        editor.map.mapFog.fog_end[i] = Mathf.Max(editor.map.mapFog.fog_start[i], newEnd);
                        if (i == editor.map.curSeason)
                        {
                            editor.map.mapFog.UpdateRender();
                        }
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                    GUILayout.Space(10);
                }
                
                // 恢复原始背景颜色
                GUI.backgroundColor = originalColor;

                GUILayout.Space(10);
                GUILayout.Label("", GUI.skin.horizontalSlider);
                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    editor.map.mapFog.UpdateRender();
                    string message = "雾效设置已应用";
                    Sango.Log.Info(message);
                    editor.autoSave.ShowSaveNotification(message);
                }
                if (GUILayout.Button("关闭", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    showFogWindow = false;
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.EndArea();
            }
        }

        private void DrawSettingsSection(string title, System.Action content)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(title, new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            GUILayout.Space(8);
            content();
            GUILayout.EndVertical();
            GUILayout.Space(12);
        }

        private void DrawSettingRow(string label, string value, System.Action onModify)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + ":", GUILayout.Width(130));
            GUILayout.Label(value, new GUIStyle(GUI.skin.label) { normal = new GUIStyleState { textColor = Color.cyan } });
            if (GUILayout.Button("修改", GUILayout.Width(70)))
            {
                onModify();
            }
            GUILayout.EndHorizontal();
        }

        private Texture2D CreateWindowBackground()
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.18f, 0.95f));
            texture.Apply();
            return texture;
        }

        private void ApplySettings()
        {
            showSettingsWindow = false;
            string message = "设置已应用";
            Sango.Log.Info(message);
            editor.autoSave.ShowSaveNotification(message);
        }
    }
}
