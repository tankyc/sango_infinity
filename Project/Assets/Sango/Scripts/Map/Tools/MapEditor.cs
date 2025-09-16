using RTEditor;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using Sango;
using Sango.Game;
using System;
using UnityEditor;

namespace Sango.Tools
{
    /// <summary>
    /// 地图编辑器
    /// </summary>
    public class MapEditor : Behaviour
    {
        /// <summary>
        /// 编辑器是否启用
        /// </summary>
        public static bool IsEditOn { get; set; }
        public string WorkContent { set; get; }
        public string DefaultContentName { get { return "Default"; } }
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
            // 设置
            Setting
        }

        public Render.MapRender map;
        public MapData.VertexData[][] vertexMapData { get { return map.mapData.vertexDatas; } }
        public MapData mapData { get { return map.mapData; } }

        // 可编辑的物件层
        internal int rayCastLayer;

        // 编辑器UI框体范围
        internal UnityEngine.Rect windowRect = new UnityEngine.Rect(20, 20, 120, 50);

        BrushBase[] brushes;
        internal TerrainBrush terrain_brush;
        internal GridBrush grid_brush;
        internal ModelBrush model_brush;

        EditorWindow editorToolsBarWindow;
        EditorWindow editorContentWindow;
        private void Awake()
        {
            //Path.Init();
            //Path.AddSearchPath("D:/project_tk/Build/Mods/CoreMap");


            IsEditOn = true;

            // 创建笔刷
            terrain_brush = new TerrainBrush(this);
            grid_brush = new GridBrush(this);
            model_brush = new ModelBrush(this);
            brushes = new BrushBase[] { terrain_brush, grid_brush, model_brush };

            // 关闭游戏主相机
            Camera.main.gameObject.SetActive(false);

            // 加载模型编辑工具, 设置监听
            GameObject.Instantiate(Resources.Load("New(Singleton)RTEditor.RuntimeEditorApplication"));
            EditorObjectSelection.Instance.SelectionDeleted += SelectionDeletedHandler;
            EditorObjectSelection.Instance.SelectionChanged += SelectionChangedHandler;

            editorToolsBarWindow = EditorWindow.AddWindow(0, windowRect, DrawToolbarWindow, "地图编辑器");
            editorToolsBarWindow.canClose = false;
            editorContentWindow = EditorWindow.AddWindow(1, windowRect, DrawContentWindow, "属性窗口");
            editorContentWindow.canClose = false;
        }

        public Render.MapRender CreateEmptyMap(int w, int h)
        {
            map = MapRender.Instance;
            map.NewMap(w, h);

            return map;
        }

        protected override void Start()
        {
            GameObject l = GameObject.Find("Directional Light");
            if (l != null) l.SetActive(false);
            l = GameObject.Find("post");
            if (l != null) l.SetActive(false);

            rayCastLayer = LayerMask.GetMask(new string[] { "Map" });

            // 创建空地图
            if (map == null)
                CreateEmptyMap(1024, 1024);

            // 设置基础可视化距离
            map.showLimitLength = 3500;
            map.mapFog.fogEnabled = false;

            Shader.EnableKeyword("SANGO_EDITOR");
            Shader.SetGlobalFloat("_BrushType", 0);
            Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);

            // 不使用游戏相机逻辑,采用另外的编辑机相机逻辑
            map.mapCamera.enabled = false;
            map.mapCamera.SetCamera(Camera.main);
            EditorFreeCamera editorfree = Camera.main.gameObject.AddComponent<Sango.Tools.EditorFreeCamera>();
            if (editorfree != null)
                editorfree.lookAt = map.mapCamera.GetCenterTransform();
            Camera.main.farClipPlane = 30000;
            SetModelSelectionMod(false);
            SetGizmoCameraEnable(false);
            EditorCameraExtend.Instance.Camera.farClipPlane = 30000;

            base.Start();

            Invoke("DelaySetFreeCamera", 0.1f);
        }

        void DelaySetFreeCamera()
        {
            map.mapCamera.position = new Vector3(0, 500, 0);
            map.mapCamera.lookRotate = new Vector3(90, -90, 0);
            viewIs311Camera = false;
            SetCameraControlType(0);
            Camera.main.gameObject.transform.position = map.mapCamera.position;
            Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);


            terrain_brush.AutoImportLayerTexture();

        }

        protected override void OnDestroy()
        {
            IsEditOn = false;
            Shader.DisableKeyword("SANGO_EDITOR");
            if (EditorObjectSelection.Instance != null)
                EditorObjectSelection.Instance.SelectionDeleted -= SelectionDeletedHandler;
            if (EditorObjectSelection.Instance != null)
                EditorObjectSelection.Instance.SelectionChanged -= SelectionChangedHandler;
            base.OnDestroy();
        }

        /// <summary>
        /// 删除模型时的回调
        /// </summary>
        /// <param name="deletedObjects"></param>
        void SelectionDeletedHandler(List<GameObject> deletedObjects)
        {
            foreach (GameObject o in deletedObjects)
            {
                MapObject mapObject = o.GetComponent<MapObject>();
                if (mapObject != null)
                {
                    map.RemoveStatic(mapObject);
                }
            }
        }

        /// <summary>
        /// 选择模型时的回调
        /// </summary>
        /// <param name="selectionChangedEventArgs"></param>
        public void SelectionChangedHandler(ObjectSelectionChangedEventArgs selectionChangedEventArgs)
        {
            // 这里处理模型的相关数据展示


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
            //SceneGizmo.Instance.enabled = b;
        }
        void SetGizmoCameraEnable(bool b)
        {
            EditorCameraExtend.Instance.enabled = b;
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
            }
            else if (t == 1)
            {
                SetGizmoCameraEnable(false);

                EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                if (editorfree != null)
                {
                    editorfree.enabled = true;
                    editorfree.UpdateCamera();
                }
                map.mapSkyBox.SetVisible(true);
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

        BrushBase CheckBrush()
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
                default:
                    break;
            }
            return brush;
        }

        public void Update()
        {
            BrushBase brush = CheckBrush();
            if (brush == null) return;
            brush.Update();
        }

        int currentEditMode = 0;
        private string[] toolbarTitle = new string[]
        {
            "基础", "编辑地形", "编辑地格","模型放置", "设置"
        };
        private string[] toolbarSeason = new string[]
        {
            "春", "夏", "秋", "冬"
        };
        bool viewIs311Camera = true;
        void DrawToolbarWindow(int windowID, EditorWindow window)
        {
            GUILayout.BeginHorizontal();

            int season = GUILayout.Toolbar(map.curSeason, toolbarSeason);
            if (season != map.curSeason)
            {
                map.curSeason = season;
                foreach (BrushBase brush in brushes)
                {
                    brush.OnSeasonChanged(season);
                }
            }

            bool viewTpye = GUILayout.Toggle(viewIs311Camera, "固定视角");
            if (viewTpye != viewIs311Camera)
            {
                viewIs311Camera = viewTpye;
                if (viewIs311Camera)
                    SetCameraControlType(1);
                else
                    SetCameraControlType(0);
            }

            if (GUILayout.Button("重置相机"))
            {
                map.mapCamera.position = new Vector3(0, 500, 0);
                map.mapCamera.lookRotate = new Vector3(90, -90, 0);
                viewIs311Camera = false;
                SetCameraControlType(0);
                Camera.main.gameObject.transform.position = map.mapCamera.position;
                Camera.main.gameObject.transform.rotation = Quaternion.Euler(90, -90, 0);
            }

            if (GUILayout.Button("加载"))
            {

                string[] path = WindowDialog.OpenFileDialog("地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    string fName = path[0];
                    map.LoadMap(fName);
                    EditorFreeCamera editorfree = Camera.main.gameObject.GetComponent<Sango.Tools.EditorFreeCamera>();
                    if (editorfree != null)
                        editorfree.lookAt = map.mapCamera.GetCenterTransform();
                }
            }

            if (GUILayout.Button("保存"))
            {

                string path = WindowDialog.SaveFileDialog("map.bin", "地图文件(*.bin)\0*.bin;\0\0");
                if (path != null)
                {
                    map.SaveMap(path);
                }
            }

            GUILayout.EndHorizontal();


            Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.cyan;
            int editMode = GUILayout.Toolbar(currentEditMode, toolbarTitle, GUILayout.Height(30));
            if (editMode != currentEditMode)
            {
                editorContentWindow.windowRect.size = windowRect.size;
                BrushBase brush = CheckBrush();
                if (brush != null)
                    brush.Clear();
                currentEditMode = editMode;
                brush = CheckBrush();
                if (brush == null) return;
                brush.OnEnter();
                SetModelSelectionMod(currentEditMode == (int)EditorModType.Model);
                editorContentWindow.visible = true;
            }
            GUI.backgroundColor = lastColor;
        }

        void DrawContentWindow(int windowID, EditorWindow window)
        {
            switch (currentEditMode)
            {
                case 0:
                    OnGUI_Base();
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
            GUILayout.Label("鼠标中建拖拽移动地图");
            GUILayout.Label("地形编辑模式下: Ctrl按住可以连续绘制 Shift在推平模式下可以以鼠标点高度推平");
            GUILayout.Label("地格编辑模式下: Alt按住可以取到鼠标点格子的值 Ctrl按住可以连续绘制");
            GUILayout.Label("模型编辑模式下: 选中模型 Q(无) W(平移) E(旋转) R(缩放)快捷键  鼠标右键或ESC取消选择 Delete删除选中的模型");
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

        public string FindTexture(string textureName, string extensions = ".png")
        {
            string destPath = $"Assets/Map/{WorkContent}/{textureName}{extensions}";
            string finalPath = Path.FindFile(destPath);
            if (finalPath == null)
            {
                destPath = $"Assets/Map/{DefaultContentName}/{textureName}{extensions}";
                finalPath = Path.FindFile(destPath);
            }
            if (!string.IsNullOrEmpty(finalPath))
            {
                return finalPath;
            }
            return null;
        }
    }
}