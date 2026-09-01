using RTEditor;
using Sango.Render;
using Sango.Tools.UndoRedo;
using UnityEngine;
using UnityEngine.EventSystems;
using System;
using UnityEngine.Rendering.Universal;
using Sango.Manager;

namespace Sango.Tools
{
    /// <summary>
    /// 地图编辑器
    /// </summary>
    public class MapEditor : Behaviour
    {
        int frameLimit = 60;
        float timeInterval;
        float currentTime;
        /// <summary>
        /// 编辑器是否启用
        /// </summary>
        public static bool IsEditOn { get; set; }
        public readonly string DefaultContentName = "Default";

        public static Sango.Hexagon.Coord SelectedCoord { get; set; }

        /// <summary>
        /// 撤销/重做管理器
        /// </summary>
        public UndoRedoManager undoRedoManager { get; private set; }

        /// <summary>
        /// 编辑模式
        /// </summary>
        enum EditorModType : int
        {
            // 基础
            Base = 0,
            // 地形编辑
            Terrain,
            // 地格编辑
            Grid,
            // 模型
            Model,
            // 地图标记
            MapLabel,
            // 城池
            City
        }

        public Render.MapRender map;
        public MapData.VertexData[][] vertexMapData { get { return map.mapData.vertexDatas; } }
        public MapData mapData { get { return map.mapData; } }

        // 可编辑的物件层
        internal int rayCastLayer;
        internal int rayCastObjectLayer;

        // 编辑器UI框体范围
        internal UnityEngine.Rect windowRect = new UnityEngine.Rect(500, 400, 240, 100);

        // 属性窗口滚动位置
        private Vector2 scrollPosition = Vector2.zero;

        private BrushBase[] brushArray;
        public BrushBase[] brushes { get { return brushArray; } }
        public TerrainBrush terrain_brush;
        internal GridBrush grid_brush;
        internal ModelBrush model_brush;
        internal MapLabelBrush mapLabel_brush;
        internal CityBrush city_brush;

        public EditorWindow editorToolsBarWindow;
        public EditorWindow editorContentWindow;
        public OperationHistoryWindow operationHistoryWindow;
        public LayoutManager layoutManager { get; private set; }

        /// <summary>
        /// 自动保存扩展类
        /// </summary>
        public MapEditorAutoSave autoSave { get; private set; }

        /// <summary>
        /// 窗口扩展类
        /// </summary>
        public MapEditorWindows windows { get; private set; }

        /// <summary>
        /// 菜单扩展类
        /// </summary>
        public MapEditorMenu menu { get; private set; }

        /// <summary>
        /// UI地图编辑器组件引用
        /// </summary>
        public UIMapEditor uiMapEditor { get; private set; }

        /// <summary>
        /// 是否使用311相机模式
        /// </summary>
        public bool ViewIs311Camera { get; set; } = true;

        public string lastSavedPath { get; set; } = "";

        GUISkin skin;

        /// <summary>
        /// 当前加载的剧本对象
        /// </summary>
        public Sango.Core.Scenario scenario { get; private set; }

        /// <summary>
        /// 剧本编辑器窗口
        /// </summary>
        private Sango.ScenarioMaker.ScenarioMakerWindow scenarioMakerWindow;

        public virtual bool IsPointerOverUI()
        {
            return EditorWindow.IsPointOverUI() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        private void Awake()
        {
            skin = Resources.Load<GUISkin>("GUISkin/MapEditor");
            timeInterval = 1.0f / frameLimit;
            UnityEngine.Application.targetFrameRate = 60;
            Sango.Path.Init();
            //Path.AddSearchPath("D:/project_tk/Build/Mods/CoreMap");
            string assetsPath = $"{Sango.Path.ContentRootPath}/Assets/Map/{DefaultContentName}";
            Sango.Path.AddSearchPath(assetsPath, false);

            IsEditOn = true;

            // 初始化撤销/重做管理器
            undoRedoManager = new UndoRedoManager();

            // 创建笔刷
            terrain_brush = new TerrainBrush(this);
            grid_brush = new GridBrush(this);
            model_brush = new ModelBrush(this);
            mapLabel_brush = new MapLabelBrush(this);
            city_brush = new CityBrush(this);
            brushArray = new BrushBase[] { terrain_brush, grid_brush, model_brush, mapLabel_brush, city_brush };

            // 关闭游戏主相机
            Camera.main.gameObject.SetActive(false);

            // 加载模型编辑工具, 设置监听
            DontDestroyOnLoad(GameObject.Instantiate(Resources.Load("New(Singleton)RTEditor.RuntimeEditorApplication")));

            // 监听Gizmo变换消息，用于Undo/Redo
            EditorUndoRedoSystem.Instance.overrideAction = RegisterUndoRedoAction;
            EditorObjectSelection.Instance.IsOverUIHandler += IsPointerOverUI;

            // 创建编辑器主窗口，高度自适应屏幕高度减去顶部菜单栏高度(30像素)
            windowRect.y += 30;
            windowRect.height = Screen.height - 30;
            windowRect = ConstrainWindowToScreen(windowRect);
            editorToolsBarWindow = EditorWindow.AddWindow(0, windowRect, DrawToolbarWindow, "地图编辑器");
            editorToolsBarWindow.canClose = false;
            editorToolsBarWindow.visible = false;
            editorToolsBarWindow.skin = skin;

            // 属性窗口不再单独创建，内容合并到工具栏窗口下方

            // 初始化操作历史窗口
            operationHistoryWindow = new OperationHistoryWindow(this);

            // 初始化布局管理器并加载布局
            layoutManager = new LayoutManager(this);
            layoutManager.LoadLayout();

            // 初始化扩展类
            autoSave = new MapEditorAutoSave(this);
            windows = new MapEditorWindows(this);
            //menu = new MapEditorMenu(this);

            var cameraData = Camera.main.GetUniversalAdditionalCameraData();
            cameraData.cameraStack.Add(App.Instance.UICamera);

            Sango.Core.GameController.Instance.DragMoveViewEnabled = false;

            AudioManager.Instance.StopBgm();
        }

        /// <summary>
        /// 加载UI地图编辑器预制件
        /// </summary>
        private void LoadUIMapEditor()
        {
            EditorEvent.OnEditorTopMenuInit += OnEditorTopMenuInit;

            GameObject prefab = Sango.Loader.ObjectLoader.LoadObject<GameObject>("Assets/UI/Prefab/window_editor.prefab");
            if (prefab != null)
            {
                GameObject uiRoot = UnityEngine.GameObject.Instantiate(prefab);
                uiRoot.transform.SetParent(App.Instance.UIRoot, false);
                uiMapEditor = uiRoot.GetComponent<UIMapEditor>();
                if (uiMapEditor == null)
                {
                    uiMapEditor = uiRoot.AddComponent<UIMapEditor>();
                }
                Sango.Log.Info("UI地图编辑器已加载");
            }
            else
            {
                Sango.Log.Error("加载UI地图编辑器预制件失败");
            }
        }

        public void OnEditorTopMenuInit(EditorMenuItemData menuData, UIMapEditor ui)
        {
            // 文件菜单
            menuData.Add("文件/新建地图", () => { windows.ShowNewMapWindow(); });
            menuData.Add("文件/加载地图", () =>
            {
                string[] path = WindowDialog.OpenFileDialog("加载地图", $"{Path.ContentRootPath}/Map", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    string fName = path[0];
                    lastSavedPath = fName;
                    map.LoadMap(fName);
                    editorToolsBarWindow.visible = true;
                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = map.mapCamera.GetCenterTransform();
                    BrushBase brush = CheckBrush();
                    if (brush != null)
                        brush.OnEnter();
                    if (ViewIs311Camera)
                        SetCameraControlType(1);
                    else
                        SetCameraControlType(0);
                    Sango.Log.Info($"地图已加载: {fName}");
                }
            });
            menuData.Add("文件/保存地图", () =>
            {
                string path = WindowDialog.SaveFileDialog("保存地图", $"{Path.ContentRootPath}/Map", "map.bin", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    lastSavedPath = path;
                    map.SaveMap(path);

                    if (scenario != null && !string.IsNullOrEmpty(scenario.FilePath))
                    {
                        CorrectCityPositions();
                        scenario.Export(scenario.FilePath);
                        Sango.Log.Info("剧本数据已同步保存");
                    }

                    autoSave.ShowSaveNotification($"地图已保存到: {System.IO.Path.GetFileName(path)}");
                }
            });
            menuData.Add("文件/放大2倍保存", () =>
            {
                string path = WindowDialog.SaveFileDialog("map_2x.bin", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    // 这里实现放大2倍保存的逻辑
                    lastSavedPath = path;
                    map.SaveScaleMap(path, 2);

                    if (scenario != null && !string.IsNullOrEmpty(scenario.FilePath))
                    {
                        CorrectCityPositionsScale(2);
                        scenario.Export(scenario.FilePath);
                        Sango.Log.Info("剧本数据已同步保存");
                    }

                    autoSave.ShowSaveNotification($"地图已保存到: {System.IO.Path.GetFileName(path)}");
                }
            });
            menuData.Add("文件/-", null); // 分隔线
            menuData.Add("文件/从剧本加载地图", () =>
            {

                string[] path = WindowDialog.OpenFileDialog("加载剧本", $"{Path.ContentRootPath}/Scenario", "剧本文件(*.json)\0*.json;\0\0");
                if (path != null)
                {
                    LoadMapFromScenario(path[0]);
                    editorToolsBarWindow.visible = true;
                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = map.mapCamera.GetCenterTransform();
                    BrushBase brush = CheckBrush();
                    if (brush != null)
                        brush.OnEnter();
                    if (ViewIs311Camera)
                        SetCameraControlType(1);
                    else
                        SetCameraControlType(0);
                }
            });
            menuData.Add("文件/-", null); // 分隔线
            menuData.Add("文件/设置", () => { windows.ShowSettingsWindow(); });
            menuData.Add("文件/退出编辑器", () => { UnityEngine.Application.Quit(); });

            // 编辑菜单
            menuData.Add("编辑/撤销", () => { undoRedoManager.Undo(); });
            menuData.Add("编辑/重做", () => { undoRedoManager.Redo(); });
            menuData.Add("编辑/-", null); // 分隔线
            menuData.Add("编辑/历史记录", () => { operationHistoryWindow.ToggleWindow(); }, true, false);
            menuData.Add("编辑/清空历史", () => { undoRedoManager.ClearHistory(); });
            menuData.Add("编辑/-", null); // 分隔线
            menuData.Add("编辑/加载剧本", () =>
            {
                string[] path = WindowDialog.OpenFileDialog("剧本文件(*.json)\0*.json;\0\0");
                if (path != null)
                {
                    LoadScenario(path[0]);
                }
            });
            menuData.Add("编辑/剧本编辑器", () =>
            {
                if (scenario != null)
                {
                    Sango.ScenarioMaker.ScenarioMaker.Instance.SetScenario(scenario);
                }
                OpenScenarioMakerWindow();
            });

            // 视图菜单
            menuData.Add("视图/游戏视角", () =>
            {
                
            }, true, ViewIs311Camera, (b) =>
            {
                ViewIs311Camera = b;
                if (ViewIs311Camera)
                    SetCameraControlType(1);
                else
                    SetCameraControlType(0);
            });
            menuData.Add("视图/重置相机", () =>
            {
                map.mapCamera.position = new Vector3(500, 250, 500);
                map.mapCamera.lookRotate = new Vector3(90, -90, 0);
                ViewIs311Camera = false;
                SetCameraControlType(0);
                Camera.main.gameObject.transform.position = map.mapCamera.position;
                Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);
            });
            menuData.Add("视图/-", null); // 分隔线
            menuData.Add("视图/加载高度", () =>
            {
                map.mapData.LoadHeight();

            });
            menuData.Add("视图/加载图层", () =>
            {
                map.mapData.LoadLayer();
            });
            menuData.Add("视图/加载水", () =>
            {
                map.mapData.LoadWater();

            });
            menuData.Add("视图/-", null); // 分隔线
            menuData.Add("视图/保存布局", () =>
            {
                layoutManager.SaveLayout();
                string message = "布局已保存";
                Sango.Log.Info(message);
                autoSave.ShowSaveNotification(message);
            });
            menuData.Add("视图/-", null); // 分隔线
            menuData.Add("视图/季节/秋", () =>
            {
                map.curSeason = 0;
                foreach (BrushBase brush in brushes)
                {
                    brush.OnSeasonChanged(0);
                }
            }, true, map.curSeason == 0).toggleGroup = 1;
            menuData.Add("视图/季节/春", () =>
            {
                map.curSeason = 1;
                foreach (BrushBase brush in brushes)
                {
                    brush.OnSeasonChanged(1);
                }
            }, true, map.curSeason == 1).toggleGroup = 1;
            menuData.Add("视图/季节/夏", () =>
            {
                map.curSeason = 2;
                foreach (BrushBase brush in brushes)
                {
                    brush.OnSeasonChanged(2);
                }
            }, true, map.curSeason == 2).toggleGroup = 1;
            menuData.Add("视图/季节/冬", () =>
            {
                map.curSeason = 3;
                foreach (BrushBase brush in brushes)
                {
                    brush.OnSeasonChanged(3);
                }
            }, true, map.curSeason == 3).toggleGroup = 1;

            // 渲染菜单
            menuData.Add("渲染/灯光设置", () => { windows.ShowLightWindow(); });
            menuData.Add("渲染/雾效设置", () => { windows.ShowFogWindow(); });
            menuData.Add("渲染/雾效开关", () =>
            {
                UnityEngine.RenderSettings.fog = !UnityEngine.RenderSettings.fog;
            }, true, UnityEngine.RenderSettings.fog);

            // 原版311菜单
            menuData.Add("原版311/导出地格", () =>
            {
                grid_brush.SaveTo311GridData();
            });
            menuData.Add("原版311/导入地格", () =>
            {
                grid_brush.Load311GridData();
            });
        }


        public void CreateNewMap(int width, int height, string mapName)
        {
            map.WorkContent = mapName;
            map.NewMap(width, height);
            //map.LoadMap(Sango.Path.FindFile("Data/Map/Editor/default_map.bin"));
            map.mapCamera.position = new Vector3(500, 0, 500);
            map.mapCamera.lookRotate = new Vector3(90, -90, 0);
            Camera.main.gameObject.transform.position = map.mapCamera.position;
            Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);
        }

        /// <summary>
        /// 从剧本加载地图和剧本数据
        /// </summary>
        /// <param name="scenarioPath">剧本文件路径</param>
        public void LoadMapFromScenario(string scenarioPath)
        {
#if SANGO_DEBUG
            try
            {
#endif
            scenario = new Sango.Core.Scenario(scenarioPath);

            if (!string.IsNullOrEmpty(scenario.Info.mapType))
            {
                string mapPath = Sango.Path.FindFile($"Map/{scenario.Info.mapType}.bin");
                if (!string.IsNullOrEmpty(mapPath) && System.IO.File.Exists(mapPath))
                {
                    map.LoadMap(mapPath);
                    lastSavedPath = mapPath;
                    editorToolsBarWindow.visible = true;

                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = map.mapCamera.GetCenterTransform();

                    BrushBase brush = CheckBrush();
                    if (brush != null)
                        brush.OnEnter();

                    if (ViewIs311Camera)
                        SetCameraControlType(1);
                    else
                        SetCameraControlType(0);

                    Sango.Log.Info($"地图已加载: {mapPath}");
                }
            }

            scenario.LoadContent();

            SpawnCityModels();

            scenario.Prepare();

            Sango.ScenarioMaker.ScenarioMaker.Instance.SetScenario(scenario);

            string message = $"剧本已加载: {System.IO.Path.GetFileName(scenarioPath)}";
            Sango.Log.Info(message);
            autoSave.ShowSaveNotification(message);
#if SANGO_DEBUG

            }
            catch (Exception e)
            {
                Sango.Log.Error($"加载剧本失败: {e.Message}");
            }
#endif
        }

        /// <summary>
        /// 加载剧本数据（不加载地图）
        /// </summary>
        /// <param name="scenarioPath">剧本文件路径</param>
        public void LoadScenario(string scenarioPath)
        {
            try
            {
                scenario = new Sango.Core.Scenario(scenarioPath);

                scenario.LoadContent();

                SpawnCityModels();

                Sango.ScenarioMaker.ScenarioMaker.Instance.SetScenario(scenario);

                string message = $"剧本数据已加载: {System.IO.Path.GetFileName(scenarioPath)}";
                Sango.Log.Info(message);
                autoSave.ShowSaveNotification(message);
            }
            catch (Exception e)
            {
                Sango.Log.Error($"加载剧本数据失败: {e.Message}");
            }
        }

        /// <summary>
        /// 打开剧本编辑器窗口
        /// </summary>
        private void OpenScenarioMakerWindow()
        {
            if (scenarioMakerWindow == null)
            {
                scenarioMakerWindow = EditorWindow.AddWindow<Sango.ScenarioMaker.ScenarioMakerWindow>(
                    1001,
                    new UnityEngine.Rect(0, 0, 800, 600),
                    null,
                    "剧本编辑器"
                ) as Sango.ScenarioMaker.ScenarioMakerWindow;
                scenarioMakerWindow.Initialize();
            }
            else
            {
                scenarioMakerWindow.visible = true;
            }
        }

        /// <summary>
        /// 根据剧本数据生成城市模型
        /// </summary>
        private void SpawnCityModels()
        {
            if (scenario == null || scenario.citySet == null)
                return;

            foreach (Sango.Core.City city in scenario.citySet)
            {
                if (city != null)
                {
                    city.Render = new EditorBuildingRender(city);
                    Sango.Log.Info($"城市已加载: {city.Name}");
                }
            }
        }

        /// <summary>
        /// 校正所有城池位置
        /// 根据 City 的 Render 中 MapObject 的 position 算出网格坐标，更新 City 的 x, y 和 rot
        /// </summary>
        public void CorrectCityPositions()
        {
            if (scenario == null || scenario.citySet == null)
                return;

            int correctedCount = 0;
            foreach (Sango.Core.City city in scenario.citySet)
            {
                if (city != null && city.Render != null && city.Render.MapObject != null)
                {
                    Vector3 worldPos = city.Render.MapObject.transform.position;

                    UnityEngine.Vector2Int gridPos = map.PositionToCoords(worldPos);
                    city.x = gridPos.x;
                    city.y = gridPos.y;

                    float rotationY = city.Render.MapObject.transform.rotation.eulerAngles.y;
                    city.rot = rotationY * Mathf.Deg2Rad;

                    correctedCount++;
                    Sango.Log.Info($"城池位置已校正: {city.Name} -> ({city.x}, {city.y}), 旋转: {city.rot}");
                }
            }

            Sango.Log.Info($"共校正了 {correctedCount} 个城池位置");
        }

        public void CorrectCityPositionsScale(int scale)
        {
            if (scenario == null || scenario.citySet == null)
                return;

            int correctedCount = 0;
            foreach (Sango.Core.City city in scenario.citySet)
            {
                if (city != null && city.Render != null && city.Render.MapObject != null)
                {
                    Vector3 worldPos = city.Render.MapObject.transform.position;

                    UnityEngine.Vector2Int gridPos = map.PositionToCoords(worldPos);
                    city.x = gridPos.x * scale;
                    city.y = gridPos.y * scale;

                    float rotationY = city.Render.MapObject.transform.rotation.eulerAngles.y;
                    city.rot = rotationY * Mathf.Deg2Rad;

                    correctedCount++;
                    Sango.Log.Info($"城池位置已校正: {city.Name} -> ({city.x}, {city.y}), 旋转: {city.rot}");
                }
            }

            Sango.Log.Info($"共校正了 {correctedCount} 个城池位置");
        }

        protected void Start()
        {
            GameObject l = GameObject.Find("Directional Light");
            if (l != null) l.SetActive(false);
            l = GameObject.Find("post");
            if (l != null) l.SetActive(false);

            rayCastLayer = LayerMask.GetMask(new string[] { "Map" });
            rayCastObjectLayer = LayerMask.GetMask(new string[] { "Building" });

            // 创建空地图
            if (map == null)
                map = MapRender.Instance; //CreateEmptyMap(1024, 1024);

            map.OnMapLoaded = () =>
            {
                // 设置基础可视化距离
                map.showLimitLength = 3500;
                map.mapFog.fogEnabled = false;
                // 不使用游戏相机逻辑,采用另外的编辑机相机逻辑
                map.mapCamera.enabled = false;
                map.mapCamera.SetCamera(Camera.main);

                //EditorFreeCamera editorfree = Camera.main.gameObject.AddComponent<Sango.Tools.EditorFreeCamera>();
                //if (editorfree != null)
                //    editorfree.lookAt = map.mapCamera.GetCenterTransform();
                editorToolsBarWindow.visible = true;

                Invoke("DelaySetFreeCamera", 0.1f);
            };

            Shader.EnableKeyword("SANGO_EDITOR");
            Shader.SetGlobalFloat("_BrushType", 0);
            Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);


            Camera.main.farClipPlane = 30000;
            SetModelSelectionMod(false);
            SetGizmoCameraEnable(false);
            EditorCameraExtend.Instance.Camera.farClipPlane = 30000;


            // 加载UI地图编辑器预制件
            LoadUIMapEditor();
        }

        void DelaySetFreeCamera()
        {
            map.mapCamera.position = new Vector3(0, 50, 0);
            map.mapCamera.lookRotate = new Vector3(90, -90, 0);
            ViewIs311Camera = true;
            SetCameraControlType(1);
            Camera.main.gameObject.transform.position = map.mapCamera.position;
            Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);

            UnityEngine.RenderSettings.fog = false;

            //
            terrain_brush.AutoImportLayerTexture();
        }

        protected void OnDestroy()
        {
            IsEditOn = false;
            Shader.DisableKeyword("SANGO_EDITOR");
            //EditorUndoRedoSystem.Instance.overrideAction = null;
            //EditorObjectSelection.Instance.IsOverUIHandler -= IsPointerOverUI;
        }

        public void SetModelSelectionMod(bool b)
        {
            SceneGizmoExtend.Instance.enabled = false;
            if (SceneGizmoExtend.Instance._gizmoCamera != null)
                SceneGizmoExtend.Instance._gizmoCamera.enabled = false;
            RuntimeEditorApplication.Instance.enabled = b;
            EditorGizmoSystem.Instance.enabled = b;
            EditorObjectSelection.Instance.enabled = b;
            EditorUndoRedoSystem.Instance.enabled = b;
            EditorMeshDatabase.Instance.enabled = b;
            MessageListenerDatabase.Instance.enabled = b;
            InputDevice.Instance.enabled = b;
        }

        void SetGizmoCameraEnable(bool b)
        {
            EditorCameraExtend.Instance.enabled = b;
        }

        public void RegisterUndoRedoAction(IUndoableAndRedoableAction action)
        {
            string desc = action.GetType().Name;
            if (action is PostObjectSelectionChangedAction)
                desc = "选取模型";
            else if (action is PostGizmoTransformedObjectsAction)
                desc = "移动模型";
            else if (action is ObjectDuplicationAction)
                desc = "复制模型";
            else if (action is DeleteSelectedObjectsAction)
                desc = "删除模型";
            else
                return;
            ModelEditCommand command = new ModelEditCommand(this, action, desc);
            undoRedoManager.AddCommand(command);
        }

        /// <summary>
        /// 设置相机控制模式: 0:自由相机模式 1,游戏相机模式 
        /// </summary>
        /// <param name="t"></param>
        public void SetCameraControlType(int t)
        {
            if (t == 0)
            {
                SetGizmoCameraEnable(true);
                EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                if (editorfree != null)
                {
                    editorfree.enabled = false;
                }
                map.mapSkyBox.SetVisible(false);
                map.mapCamera.enabled = false;
            }
            else if (t == 1)
            {
                SetGizmoCameraEnable(false);

                EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                if (editorfree != null)
                {
                    editorfree.enabled = false;
                }
                map.mapSkyBox.SetVisible(true);
                map.mapCamera.enabled = true;
            }
        }

        public static float QueryHeight(Vector3 pos)
        {
            Vector3 begin = pos;
            begin.y = 500;
            Ray ray = new Ray(begin, Vector3.down);
            RaycastHit raycastHit;
            if (Physics.Raycast(ray, out raycastHit, 1000, LayerMask.GetMask("Map"), QueryTriggerInteraction.Ignore))
                return raycastHit.point.y;
            else return 0;
        }

        public static bool QueryHeight(Vector3 pos, out float height)
        {
            Vector3 begin = pos;
            begin.y = 500;
            Ray ray = new Ray(begin, Vector3.down);
            RaycastHit raycastHit;
            if (Physics.Raycast(ray, out raycastHit, 1000, LayerMask.GetMask("Map"), QueryTriggerInteraction.Ignore))
            {
                height = raycastHit.point.y;
                return true;
            }
            else
            {
                height = 0;
                return false;
            }
        }

        public BrushBase CheckBrush()
        {
            BrushBase brush = null;
            switch ((EditorModType)currentEditMode)
            {
                case EditorModType.Base:
                    break;
                case EditorModType.Terrain:
                    {
                        brush = brushes[0];
                    }
                    break;
                case EditorModType.Grid:
                    {
                        brush = brushes[1];
                    }
                    break;
                case EditorModType.Model:
                    {
                        brush = brushes[2];
                    }
                    break;
                case EditorModType.MapLabel:
                    {
                        brush = brushes[3];
                    }
                    break;
                case EditorModType.City:
                    {
                        brush = brushes[4];
                    }
                    break;
                default:
                    break;
            }
            return brush;
        }

        public void Update()
        {
            //currentTime += Time.deltaTime;
            //if (currentTime < timeInterval)
            //    return;

            //while (currentTime >= timeInterval)
            //    currentTime = currentTime - timeInterval;

            // 处理快捷键（使用快捷键配置类）
            if (shortcuts.IsUndoPressed())
            {
                undoRedoManager.Undo();
            }
            else if (shortcuts.IsRedoPressed())
            {
                undoRedoManager.Redo();
            }
            else if (shortcuts.IsSavePressed())
            {
                autoSave.QuickSave();
            }
            else if (shortcuts.IsLoadPressed())
            {
                // 加载地图快捷键
                string[] path = WindowDialog.OpenFileDialog("地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    string fName = path[0];
                    lastSavedPath = fName;
                    map.LoadMap(fName);
                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = map.mapCamera.GetCenterTransform();
                    BrushBase brush = CheckBrush();
                    if (brush != null)
                        brush.OnEnter();
                    if (ViewIs311Camera)
                        SetCameraControlType(1);
                    else
                        SetCameraControlType(0);
                    Sango.Log.Info($"地图已加载: {fName}");
                }
            }
            else if (shortcuts.IsToggleViewPressed())
            {
                // 切换视角快捷键
                ViewIs311Camera = !ViewIs311Camera;
                if (ViewIs311Camera)
                    SetCameraControlType(1);
                else
                    SetCameraControlType(0);
            }

            // 自动保存逻辑（委托给扩展类）
            autoSave.Update();

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, map.showLimitLength + 2000, rayCastLayer))
            {
                Sango.Hexagon.Hex hex = map.mapGrid.hexWorld.PositionToHex(hit.point);
                SelectedCoord = Sango.Hexagon.Coord.OffsetFromCube(hex);
            }
            {
                BrushBase brush = CheckBrush();
                if (brush == null) return;
                brush.Update();
            }
        }

        int currentEditMode = 0;
        private string[] toolbarTitle = new string[]
        {
            "无", "地形", "地格", "模型", "标记", "城池"
        };
        private string[] toolbarSeason = new string[]
        {
            "秋", "春", "夏", "冬"
        };

        // 设置配置变量
        public ShortcutConfig shortcuts = new ShortcutConfig();

        /// <summary>
        /// 快捷键配置类
        /// </summary>
        [System.Serializable]
        public class ShortcutConfig
        {
            public KeyCode save = KeyCode.S;
            public KeyCode undo = KeyCode.Z;
            public KeyCode redo = KeyCode.Y;
            public KeyCode load = KeyCode.L;
            public KeyCode toggleView = KeyCode.V;

            /// <summary>
            /// 检查是否按下保存快捷键
            /// </summary>
            public bool IsSavePressed()
            {
                return Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(save);
            }

            /// <summary>
            /// 检查是否按下撤销快捷键
            /// </summary>
            public bool IsUndoPressed()
            {
                return Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(undo);
            }

            /// <summary>
            /// 检查是否按下重做快捷键
            /// </summary>
            public bool IsRedoPressed()
            {
                return Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(redo);
            }

            /// <summary>
            /// 检查是否按下加载快捷键
            /// </summary>
            public bool IsLoadPressed()
            {
                return Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(load);
            }

            /// <summary>
            /// 检查是否按下切换视角快捷键
            /// </summary>
            public bool IsToggleViewPressed()
            {
                return Input.GetKeyDown(toggleView);
            }

            /// <summary>
            /// 重置为默认快捷键
            /// </summary>
            public void ResetToDefault()
            {
                save = KeyCode.S;
                undo = KeyCode.Z;
                redo = KeyCode.Y;
                load = KeyCode.L;
                toggleView = KeyCode.V;
            }
        }

        /// <summary>
        /// OnGUI方法，用于绘制顶部菜单条和窗口
        /// </summary>
        public void OnGUI()
        {
            // 委托给菜单扩展类绘制顶部菜单条
            //menu.DrawTopMenuBar();

            // 委托给窗口扩展类绘制各个窗口
            windows.Draw();
        }

        void DrawToolbarWindow(int windowID, EditorWindow window)
        {
            // 绘制保存通知（放在最顶层，避免遮挡）
            autoSave.DrawSaveNotification();

            // 当前鼠标格子信息
            GUILayout.Label($"当前鼠标格子:{{{SelectedCoord.col},{SelectedCoord.row}}}");
            GUILayout.Space(10);

            // 编辑模式切换
            Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f); // 更美观的蓝色
            int editMode = GUILayout.Toolbar(currentEditMode, toolbarTitle, GUILayout.Height(35));
            if (editMode != currentEditMode)
            {
                BrushBase brush = CheckBrush();
                if (brush != null)
                    brush.Clear();
                currentEditMode = editMode;
                brush = CheckBrush();
                if (brush != null)
                {
                    SetModelSelectionMod(currentEditMode == (int)EditorModType.Model);
                    brush.OnEnter();
                }
            }
            GUI.backgroundColor = lastColor;

            // 快捷键提示
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            GUILayout.Label("快捷键:", GUILayout.Width(60));
            GUILayout.Label("Ctrl+S 保存", GUILayout.Width(80));
            GUILayout.Label("Ctrl+Z 撤销", GUILayout.Width(80));
            GUILayout.Label("Ctrl+Y 重做", GUILayout.Width(80));
            GUILayout.EndHorizontal();

            // 分隔线
            GUILayout.Space(10);
            GUILayout.Label("", GUI.skin.horizontalSlider);
            GUILayout.Space(10);

            // 属性窗口内容 - 添加滚动支持
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            switch (currentEditMode)
            {
                case 0:
                    //OnGUI_Base();
                    break;
                default:
                    CheckBrush().OnGUI();
                    break;
            }

            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 保存底图
        /// </summary>
        /// <param name="mapPath">地图文件路径</param>
        private void SaveBaseMap(string mapPath)
        {
            int maxRetries = 3;
            int retryCount = 0;
            bool saveSuccess = false;

            while (!saveSuccess && retryCount < maxRetries)
            {
                try
                {
                    // 计算底图保存路径
                    string mapDirectory = System.IO.Path.GetDirectoryName(mapPath);
                    string mapName = System.IO.Path.GetFileNameWithoutExtension(mapPath);
                    // 移除_auto_save后缀（如果有）
                    if (mapName.Contains("_auto_save"))
                    {
                        mapName = mapName.Substring(0, mapName.IndexOf("_auto_save"));
                    }
                    string baseMapPath = System.IO.Path.Combine(mapDirectory, "..", "Assets", "Map", mapName, "BaseTex");

                    // 创建目录
                    System.IO.Directory.CreateDirectory(baseMapPath);

                    // 保存每个季节的底图
                    for (int season = 0; season < 4; season++)
                    {
                        if (terrain_brush != null && terrain_brush.baseMap != null && terrain_brush.baseMap[season] != null)
                        {
                            string baseMapFileName = System.IO.Path.Combine(baseMapPath, $"BaseMap{season}.png");
                            terrain_brush.SaveBaseTexture(baseMapFileName, season);
                        }
                    }

                    Sango.Log.Info($"底图已保存到: {baseMapPath}");
                    saveSuccess = true;
                }
                catch (System.Exception e)
                {
                    retryCount++;
                    Sango.Log.Error($"保存底图失败 (尝试 {retryCount}/{maxRetries}): {e.Message}");

                    if (retryCount >= maxRetries)
                    {
                        Sango.Log.Error("保存底图失败，已达到最大重试次数");
                    }
                    else
                    {
                        // 等待一小段时间后重试
                        System.Threading.Thread.Sleep(500);
                    }
                }
            }
        }

        void DrawContentWindow(int windowID, EditorWindow window)
        {
            switch (currentEditMode)
            {
                case 0:
                    //OnGUI_Base();
                    break;
                case 1:
                    OnGUI_Edit_Terrain();
                    break;
                case 2:
                    OnGUI_Edit_Grid();
                    break;
                case 3:
                    OnGUI_Models();
                    break;
                case 4:
                    OnGUI_Setting();
                    break;
                default:
                    break;
            }
        }

        void OnGUI_Base()
        {
            GUI.changed = false;
            float v = EditorUtility.FloatField(map.showLimitLength, "可视距离");
            if (GUI.changed)
            {
                map.showLimitLength = v;
            }
            EditorUIDraw.OnGUI(map.mapCamera);

            EditorUIDraw.OnGUI(map.mapData);
            EditorUIDraw.OnGUI(map.mapGrid);
            EditorUIDraw.OnGUI(map.mapLight);
            EditorUIDraw.OnGUI(map.mapSkyBox);
            EditorUIDraw.OnGUI(map.mapFog);
        }

        void OnGUI_Edit_Terrain()
        {
            CheckBrush().OnGUI();
        }

        void OnGUI_Edit_Grid()
        {
            CheckBrush().OnGUI();
        }

        void OnGUI_Models()
        {
            CheckBrush().OnGUI();
        }

        void OnGUI_Setting()
        {
            // 操作说明部分
            GUILayout.BeginVertical("Box");
            GUILayout.Label("操作说明", GUILayout.ExpandWidth(true));
            GUILayout.Space(5);
            GUILayout.Label("• 鼠标中键拖拽地图移动");
            GUILayout.Label("• 地形编辑模式: Ctrl按住连续绘制, Shift推平模式以鼠标点高度推平");
            GUILayout.Label("• 地格编辑模式: Alt取鼠标点格子值, Ctrl按住连续绘制");
            GUILayout.Label("• 模型编辑模式: 选中模型后 Q(无) W(平移) E(旋转) R(缩放)");
            GUILayout.Label("• 模型编辑模式: 鼠标右键或ESC取消选择, Delete删除选中模型");
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // 快捷键部分
            GUILayout.BeginVertical("Box");
            GUILayout.Label("快捷键", GUILayout.ExpandWidth(true));
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            GUILayout.Label("保存:", GUILayout.Width(60));
            GUILayout.Label("Ctrl+S");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("撤销:", GUILayout.Width(60));
            GUILayout.Label("Ctrl+Z");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("重做:", GUILayout.Width(60));
            GUILayout.Label("Ctrl+Y");
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.Space(15);

            // 自动保存设置部分
            GUILayout.BeginVertical("Box");
            GUILayout.Label("自动保存设置", GUILayout.ExpandWidth(true));
            GUILayout.Space(10);

            // 自动保存启用/禁用
            GUILayout.BeginHorizontal();
            GUILayout.Label("启用自动保存", GUILayout.Width(120));
            bool _autoSaveEnabled = GUILayout.Toggle(autoSave.AutoSaveEnabled, "");
            if (_autoSaveEnabled != autoSave.AutoSaveEnabled)
            {
                autoSave.AutoSaveEnabled = _autoSaveEnabled;
            }
            GUILayout.EndHorizontal();

            // 自动保存间隔
            GUILayout.BeginHorizontal();
            GUILayout.Label("自动保存间隔(分钟)", GUILayout.Width(140));
            float _autoSaveInterval = EditorUtility.FloatField(autoSave.AutoSaveInterval / 60f, GUILayout.MaxWidth(60));
            if (GUI.changed)
            {
                _autoSaveInterval = Mathf.Clamp(_autoSaveInterval, 1f, 60f);
                autoSave.AutoSaveInterval = _autoSaveInterval * 60f;
            }
            GUILayout.EndHorizontal();

            // 自动保存数量限制
            GUILayout.BeginHorizontal();
            GUILayout.Label("自动保存数量限制", GUILayout.Width(140));
            int _autoSaveLimit = EditorUtility.IntField(autoSave.AutoSaveLimit, GUILayout.MaxWidth(60));
            if (GUI.changed)
            {
                _autoSaveLimit = Mathf.Clamp(_autoSaveLimit, 1, 20);
                autoSave.AutoSaveLimit = _autoSaveLimit;
            }
            GUILayout.EndHorizontal();

            // 自动保存说明
            GUILayout.Space(10);
            GUILayout.Label("• 自动保存文件会添加_auto_save后缀");
            GUILayout.Label("• 默认最多保存20个自动保存文件");
            GUILayout.Label("• 超出数量限制时会自动删除最旧的文件");
            GUILayout.EndVertical();
        }

        /// <summary>
        /// 强制相机指向目标对象
        /// </summary>
        /// <param name="obj"></param>
        public void ForceCameraToGameObject(GameObject obj)
        {
            EditorObjectSelection.Instance.ClearSelection(false);
            EditorObjectSelection.Instance.AddObjectToSelection(obj, false);
            ForceCameraToPosition(obj.transform.position);
        }

        /// <summary>
        /// 强制相机指向目标点
        /// </summary>
        /// <param name="obj"></param>
        public void ForceCameraToPosition(Vector3 position)
        {
            map.mapCamera.position = position;
        }

        /// <summary>
        /// 将窗口限制在屏幕范围内
        /// </summary>
        /// <param name="windowRect">原始窗口位置</param>
        /// <returns>限制后的窗口位置</returns>
        private UnityEngine.Rect ConstrainWindowToScreen(UnityEngine.Rect windowRect)
        {
            // 获取屏幕尺寸
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // 限制窗口位置，确保不超出屏幕边界
            float x = Mathf.Clamp(windowRect.x, 0, screenWidth - windowRect.width);
            float y = Mathf.Clamp(windowRect.y, 0, screenHeight - windowRect.height);

            return new UnityEngine.Rect(x, y, windowRect.width, windowRect.height);
        }
    }
}
