using Sango.Core;
using Sango.Render;
using Sango.Tools.UndoRedo;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 城市编辑笔刷
    /// 支持城池、关卡、港口的添加、选择、删除和邻接关系编辑
    /// </summary>
    public class CityBrush : BrushBase
    {
        /// <summary>
        /// 城市类型枚举
        /// </summary>
        public enum CityType
        {
            /// <summary>
            /// 城池
            /// </summary>
            City,
            /// <summary>
            /// 关卡
            /// </summary>
            Gate,
            /// <summary>
            /// 港口
            /// </summary>
            Port
        }

        /// <summary>
        /// 城市类型对应的模型类型
        /// </summary>
        private Dictionary<CityType, int> cityTypeToModelType = new Dictionary<CityType, int>()
        {
            { CityType.City, 1 },
            { CityType.Gate, 2 },
            { CityType.Port, 3 }
        };

        /// <summary>
        /// 城市邻接关系编辑命令
        /// </summary>
        public class CityNeighborEditCommand : IUndoableCommand
        {
            private MapEditor editor;
            private City city1;
            private City city2;
            private bool isAdd;
            private string actionName;

            public CityNeighborEditCommand(MapEditor editor, City city1, City city2, bool isAdd, string actionName)
            {
                this.editor = editor;
                this.city1 = city1;
                this.city2 = city2;
                this.isAdd = isAdd;
                this.actionName = actionName;
            }

            public string Description
            {
                get { return actionName; }
            }

            public void Execute()
            {
                if (isAdd)
                {
                    if (!city1.NeighborList.Contains(city2))
                        city1.NeighborList.Add(city2);
                    if (!city2.NeighborList.Contains(city1))
                        city2.NeighborList.Add(city1);
                }
                else
                {
                    city1.NeighborList.Remove(city2);
                    city2.NeighborList.Remove(city1);
                }
            }

            public void Undo()
            {
                if (!isAdd)
                {
                    if (!city1.NeighborList.Contains(city2))
                        city1.NeighborList.Add(city2);
                    if (!city2.NeighborList.Contains(city1))
                        city2.NeighborList.Add(city1);
                }
                else
                {
                    city1.NeighborList.Remove(city2);
                    city2.NeighborList.Remove(city1);
                }
            }

            public void Redo()
            {
                Execute();
            }

            public void Destroy()
            {
                // 清理资源
            }
        }

        /// <summary>
        /// 当前选中的城市
        /// </summary>
        private City selectedCity = null;

        /// <summary>
        /// 鼠标悬停的城市
        /// </summary>
        private City hoverCity = null;

        /// <summary>
        /// 是否显示城市线路
        /// </summary>
        private bool showCityLines = false;

        /// <summary>
        /// 工具栏标题数组
        /// </summary>
        private string[] toolbarTitle = new string[] { "无", "创建", "修改", "连接", "附属",/* "区域", "地格" */};

        public enum CityEditorType : int
        {
            None = 0,
            Create,
            Move,
            Connect,
            Belong,
            Area,
            Grid
        }

        private CityEditorType controlType = CityEditorType.None;

        /// <summary>
        /// 是否正在连接城市
        /// </summary>
        private bool isConnecting = false;

        /// <summary>
        /// 是否正在
        /// </summary>
        private bool isMoving = false;

        /// <summary>
        /// 连接起始城市
        /// </summary>
        private City connectStartCity = null;

        /// <summary>
        /// 线路渲染器列表
        /// </summary>
        private List<GameObject> lineRenderers = new List<GameObject>();

        /// <summary>
        /// 属性编辑器窗口
        /// </summary>
        private CityPropertyEditorWindow propertyEditorWindow = null;

        /// <summary>
        /// 连接提示信息
        /// </summary>
        private string connectHint = "";

        /// <summary>
        /// 当前城市类型
        /// </summary>
        private CityType currentCityType = CityType.City;

        /// <summary>
        /// 城市类型名称数组
        /// </summary>
        private string[] cityTypeNames = { "城池", "关卡", "港口" };

        /// <summary>
        /// 当前选中的模型配置
        /// </summary>
        private ModelConfig selectedModelConfig = null;

        /// <summary>
        /// 当前预览模型
        /// </summary>
        private GameObject previewModel = null;

        /// <summary>
        /// 当前城市类型对应的模型列表
        /// </summary>
        private List<ModelConfig> currentModelList = new List<ModelConfig>();

        /// <summary>
        /// 滚动位置
        /// </summary>
        private Vector2 scrollPos = Vector2.zero;

        public CityBrush(MapEditor e) : base(e)
        {
            UpdateModelList();
        }

        /// <summary>
        /// 根据当前城市类型更新模型列表
        /// </summary>
        private void UpdateModelList()
        {
            currentModelList.Clear();
            int modelType = cityTypeToModelType[currentCityType];
            GameData.Instance.ModelConfigs.ForEach(config =>
            {
                if (config.modelType == modelType)
                {
                    currentModelList.Add(config);
                }
            });
        }

        public override void OnBrushTypeChange()
        {
            ClearLineRenderers();
        }

        public override void OnSeasonChanged(int curSeason)
        {
        }

        public override void Clear()
        {
            if (selectedCity != null && selectedCity.Render != null)
            {
                selectedCity.Render.SetFlash(false);
            }
            selectedCity = null;
            hoverCity = null;
            isConnecting = false;
            connectStartCity = null;
            ClearLineRenderers();
            ClosePropertyEditorWindow();
            ClearPreviewModel();
            controlType = CityEditorType.None;
        }

        /// <summary>
        /// 清理预览模型
        /// </summary>
        private void ClearPreviewModel()
        {
            if (previewModel != null)
            {
                PoolManager.Recycle(previewModel);
                previewModel.SetActive(false);
                previewModel = null;
            }
            selectedModelConfig = null;
        }

        /// <summary>
        /// 选择城市模型
        /// </summary>
        /// <param name="config">模型配置</param>
        public void SelectModel(ModelConfig config)
        {
            selectedModelConfig = config;

            if (previewModel != null)
            {
                GameObject.Destroy(previewModel);
            }
            previewModel = PoolManager.Create(selectedModelConfig.model);
            if (previewModel != null)
            {
                previewModel.transform.SetParent(null);
                previewModel.SetActive(true);
            }
        }

        /// <summary>
        /// 关闭属性编辑器窗口
        /// </summary>
        private void ClosePropertyEditorWindow()
        {
            if (propertyEditorWindow != null)
            {
                propertyEditorWindow.visible = false;
                EditorWindow.RemoveWindow(propertyEditorWindow);
                propertyEditorWindow = null;
            }
        }

        public override void Modify(Vector3 center, MapEditor editor)
        {
            switch (controlType)
            {
                case CityEditorType.Create:
                    {

                        if (previewModel != null && selectedModelConfig != null)
                        {
                            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                            Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);

                            City newCity = null;
                            switch (currentCityType)
                            {
                                case CityType.Gate:
                                    newCity = new Gate();
                                    newCity.BuildingType = GameData.Instance.ScenarioCommonData.BuildingTypes.Get(2);

                                    // 默认为最近的城池属地
                                    float minDis = 99999;
                                    editor.scenario.citySet.ForEach(x =>
                                    {
                                        if (x.IsCity())
                                        {
                                            float len = (x.Render.MapObject.position - center).sqrMagnitude;
                                            if (len < minDis)
                                            {
                                                newCity.mBelongCity = x;
                                                newCity.BelongCity = x.Id;
                                                minDis = len;
                                            }
                                        }
                                    });

                                    break;
                                case CityType.Port:
                                    newCity = new Port();
                                    newCity.BuildingType = GameData.Instance.ScenarioCommonData.BuildingTypes.Get(3);
                                    // 默认为最近的城池属地
                                    minDis = 99999;
                                    editor.scenario.citySet.ForEach(x =>
                                    {
                                        if (x.IsCity())
                                        {
                                            float len = (x.Render.MapObject.position - center).sqrMagnitude;
                                            if (len < minDis)
                                            {
                                                newCity.mBelongCity = x;
                                                newCity.BelongCity = x.Id;
                                                minDis = len;
                                            }
                                        }
                                    });

                                    break;
                                default:
                                    newCity = new City();
                                    newCity.BuildingType = GameData.Instance.ScenarioCommonData.BuildingTypes.Find(x=>x.kind == selectedModelConfig.modelKind);
                                    break;
                            }
                            newCity.CityLevelType = GameData.Instance.ScenarioCommonData.CityLevelTypes.Get(1);
                            newCity.x = offset.col;
                            newCity.y = offset.row;
                            newCity.model = selectedModelConfig.model;

                            editor.scenario.citySet.Add(newCity);
                            newCity.Name = $"[{newCity.Id}]{selectedModelConfig.Name}";

                            newCity.Render = new EditorBuildingRender(newCity);

                            Sango.Log.Info($"添加{cityTypeNames[(int)currentCityType]}: {newCity.Name}");

                            //if (showCityLines)
                            //{
                            //    CreateLineRenderers();
                            //}

                            if (!Input.GetKey(KeyCode.LeftShift))
                            {
                                ClearPreviewModel();
                            }
                        }
                    }
                    break;
                case CityEditorType.Connect:
                    {
                        City city = GetCityAtPosition(center);
                        if (city != null)
                        {
                            if (isConnecting && connectStartCity != null)
                            {
                                if (connectStartCity != city)
                                {
                                    bool isNeighbor = connectStartCity.NeighborList.Contains(city);
                                    if (!isNeighbor)
                                    {
                                        CityNeighborEditCommand command = new CityNeighborEditCommand(editor, connectStartCity, city, true, $"添加邻接城市: {connectStartCity.Name} - {city.Name}");
                                        editor.undoRedoManager.AddCommand(command, true);
                                        connectHint = $"添加邻接城市: {connectStartCity.Name} - {city.Name}";
                                        Sango.Log.Info($"添加邻接城市: {connectStartCity.Name} - {city.Name}");
                                        //if (showCityLines)
                                        {
                                            CreateLineRenderers();
                                        }
                                    }
                                    else
                                    {
                                        connectHint = $"城市 {city.Name} 已经是 {connectStartCity.Name} 的邻接城市";
                                        Sango.Log.Warning($"城市 {city.Name} 已经是 {connectStartCity.Name} 的邻接城市");
                                    }
                                }
                                isConnecting = false;
                                connectStartCity = null;
                            }
                            else
                            {
                                if (selectedCity != null && selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(false);
                                }
                                selectedCity = city;
                                if (selectedCity != null && selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(true);
                                }
                                Sango.Log.Info($"选中城市: {city.Name}");
                            }
                        }
                        break;
                    }
                case CityEditorType.Move:
                    {
                        if (selectedCity == null)
                        {
                            selectedCity = GetCityAtPosition(center);
                            if (selectedCity != null && selectedCity.Render != null)
                            {
                                selectedCity.Render.SetFlash(true);
                            }
                        }
                        else
                        {
                            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                            Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);
                            selectedCity.x = offset.col;
                            selectedCity.y = offset.row;
                            if (selectedCity != null && selectedCity.Render != null)
                            {
                                selectedCity.Render.SetFlash(false);
                            }
                            selectedCity = null;
                        }
                    }
                    break;
                case CityEditorType.Belong:
                    {
                        City city = GetCityAtPosition(center);
                        if (city != null)
                        {
                            if (isConnecting && connectStartCity != null)
                            {
                                if (connectStartCity != city)
                                {
                                    if (connectStartCity.IsCity() == city.IsCity())
                                    {
                                        connectHint = $"不能将相同类型的都市附属!!!";
                                        return;
                                    }

                                    if (!connectStartCity.IsCity())
                                    {
                                        connectStartCity.mBelongCity = city;
                                        connectStartCity.BelongCity = city.Id;
                                    }
                                    else
                                    {
                                        city.mBelongCity = connectStartCity;
                                        city.BelongCity = connectStartCity.Id;
                                    }
                                }
                                isConnecting = false;
                                connectStartCity = null;
                                CreateLineRenderers(false);
                            }
                            else
                            {
                                if (selectedCity != null && selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(false);
                                }
                                selectedCity = city;
                                if (selectedCity != null && selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(true);
                                }
                                Sango.Log.Info($"选中城市: {city.Name}");
                            }
                        }
                        break;
                    }
            }
        }

        public override void OnGUI()
        {
            GUILayout.Label("城池编辑");
            GUILayout.Space(10);

            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int editMode = GUILayout.SelectionGrid((int)controlType, toolbarTitle, 3, GUILayout.Height(90));
            if (editMode != (int)controlType)
            {
                controlType = (CityEditorType)editMode;
                isConnecting = false;
                editor.SetModelSelectionMod(controlType == CityEditorType.Move);
                if(controlType != CityEditorType.Move)
                {
                    RTEditor.EditorObjectSelection.Instance.ClearSelection(false);
                    selectedCity = null;
                }
                switch (controlType)
                {
                    case CityEditorType.None:
                        {
                            break;
                        }
                    case CityEditorType.Connect:
                        CreateLineRenderers();
                        break;
                    case CityEditorType.Belong:
                        CreateLineRenderers(false);
                        break;
                    default:
                        ClearLineRenderers();
                        break;
                }
                //OnEditTypeChange();
            }
            GUI.backgroundColor = lastColor;
            switch (controlType)
            {
                case CityEditorType.None:
                    { 
                    }
                    break;
                case CityEditorType.Create:
                    { // 城市类型选择
                        GUILayout.Label("城市类型:");
                        int typeIndex = GUILayout.SelectionGrid((int)currentCityType, cityTypeNames, 3);
                        if (typeIndex != (int)currentCityType)
                        {
                            currentCityType = (CityType)typeIndex;
                            UpdateModelList();
                            ClearPreviewModel();
                        }
                        GUILayout.Space(10);
                        // 模型选择列表
                        GUILayout.Label("选择模型:");
                        scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(306), GUILayout.Height(200));
                        foreach (var config in currentModelList)
                        {
                            GUILayout.BeginHorizontal();
                            bool isSelected = selectedModelConfig != null && selectedModelConfig.Id == config.Id;
                            lastColor = GUI.backgroundColor;
                            GUI.backgroundColor = isSelected ? UnityEngine.Color.green : UnityEngine.Color.white;
                            if (GUILayout.Button(config.Name, GUILayout.Width(150)))
                            {
                                SelectModel(config);
                            }
                            GUI.backgroundColor = lastColor;
                            GUILayout.Label($"ID: {config.Id}");
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndScrollView();
                        GUILayout.Space(10);
                        // 当前选中模型提示
                        if (selectedModelConfig != null)
                        {
                            GUILayout.Label($"当前选中: {selectedModelConfig.Name}", new GUIStyle(GUI.skin.label) { normal = new GUIStyleState() { textColor = Color.green } });
                            GUILayout.Label("点击地图放置城市，右键取消", new GUIStyle(GUI.skin.label) { fontSize = 12 });
                        }
                        else
                        {
                            GUILayout.Label("请先选择一个模型", new GUIStyle(GUI.skin.label) { normal = new GUIStyleState() { textColor = Color.yellow } });
                        }
                        GUILayout.Space(10);
                    }
                    break;
                case CityEditorType.Connect:
                    {
                        // 城市属性编辑按钮
                        if (selectedCity != null)
                        {
                            GUILayout.Label($"当前选中城市: {selectedCity.Name}");
                            GUILayout.Space(10);

                            // 连接城市按钮（只在选中城市时展示）
                            if (GUILayout.Button("连接城市"))
                            {
                                isConnecting = true;
                                connectStartCity = selectedCity;
                                connectHint = $"开始连接城市，选择起始城市: {selectedCity.Name}";
                                Sango.Log.Info($"开始连接城市，选择起始城市: {selectedCity.Name}");
                            }

                            // 显示连接提示
                            if (!string.IsNullOrEmpty(connectHint))
                            {
                                GUILayout.Space(5);
                                GUILayout.Label(connectHint, new GUIStyle(GUI.skin.label) { fontSize = 12, normal = new GUIStyleState() { textColor = Color.yellow } });
                            }

                            GUILayout.Space(10);

                            //if (GUILayout.Button("移动城市"))
                            //{
                            //    OpenPropertyEditorWindow();
                            //}

                            //if (GUILayout.Button("编辑城市属性"))
                            //{
                            //    OpenPropertyEditorWindow();
                            //}

                            // 显示邻接城市
                            GUILayout.Label("邻接城市:");
                            for (int i = 0; i < selectedCity.NeighborList.Count; i++)
                            {
                                City neighbor = selectedCity.NeighborList[i];
                                if (neighbor != null)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label(neighbor.Name);
                                    if (GUILayout.Button("移除"))
                                    {
                                        CityNeighborEditCommand command = new CityNeighborEditCommand(editor, selectedCity, neighbor, false, $"移除邻接城市: {selectedCity.Name} - {neighbor.Name}");
                                        editor.undoRedoManager.AddCommand(command, true);
                                        Sango.Log.Info($"移除邻接城市: {selectedCity.Name} - {neighbor.Name}");
                                        // 移除邻接城市后刷新linerenderer的展示
                                        CreateLineRenderers();
                                    }
                                    GUILayout.EndHorizontal();
                                }
                            }
                        }
                        else
                        {
                            GUILayout.Label("请选择一个城市");
                        }
                    }
                    break;
                case CityEditorType.Move:
                    {
                        // 城市属性编辑按钮
                        if (selectedCity != null)
                        {
                            GUILayout.Label($"当前选中城市: {selectedCity.Name}");
                            GUILayout.Space(10);

                            // 城市类型选择
                            if (GUILayout.Button("编辑城市属性"))
                            {
                                OpenPropertyEditorWindow();
                            }

                            // 显示邻接城市
                            GUILayout.Label("邻接城市:");
                            for (int i = 0; i < selectedCity.NeighborList.Count; i++)
                            {
                                City neighbor = selectedCity.NeighborList[i];
                                if (neighbor != null)
                                {
                                    GUILayout.BeginHorizontal();
                                    GUILayout.Label(neighbor.Name);
                                    if (GUILayout.Button("移除"))
                                    {
                                        CityNeighborEditCommand command = new CityNeighborEditCommand(editor, selectedCity, neighbor, false, $"移除邻接城市: {selectedCity.Name} - {neighbor.Name}");
                                        editor.undoRedoManager.AddCommand(command, true);
                                        Sango.Log.Info($"移除邻接城市: {selectedCity.Name} - {neighbor.Name}");
                                        // 移除邻接城市后刷新linerenderer的展示
                                        CreateLineRenderers();
                                    }
                                    GUILayout.EndHorizontal();
                                }
                            }
                        }
                        else
                        {
                            GUILayout.Label("请选择一个城市");
                        }
                    }
                    break;
                case CityEditorType.Belong:
                    {
                        // 城市属性编辑按钮
                        if (selectedCity != null)
                        {
                            GUILayout.Label($"当前选中城市: {selectedCity.Name}");
                            GUILayout.Space(10);

                            // 连接城市按钮（只在选中城市时展示）
                            if (selectedCity.IsCity() && GUILayout.Button("选择附庸港关"))
                            {
                                isConnecting = true;
                                connectStartCity = selectedCity;
                                connectHint = $"开始选择港关，选择目标港关: {selectedCity.Name}";
                            }

                            if (!selectedCity.IsCity() && GUILayout.Button("选择附庸都市"))
                            {
                                isConnecting = true;
                                connectStartCity = selectedCity;
                                connectHint = $"开始附庸都市，选择目标都市: {selectedCity.Name}";
                            }

                            // 显示连接提示
                            if (!string.IsNullOrEmpty(connectHint))
                            {
                                GUILayout.Space(5);
                                GUILayout.Label(connectHint, new GUIStyle(GUI.skin.label) { fontSize = 12, normal = new GUIStyleState() { textColor = Color.yellow } });
                            }

                            GUILayout.Space(10);

                            // 显示邻接城市
                            //if (selectedCity.IsCity())
                            //{
                            //    GUILayout.Label("附属城市:");
                            //    editor.scenario.citySet.ForEach(x =>
                            //    {
                            //        if (!x.IsCity() && x.BelongCity == selectedCity.Id)
                            //        {
                            //            GUILayout.Label(x.Name);
                            //            CreateLineRenderers(false);
                            //        }
                            //    });
                            //}
                        }
                        else
                        {
                            GUILayout.Label("请选择一个城市");
                        }
                    }
                    break;
            }

            //// 显示城市线路开关
            //showCityLines = GUILayout.Toggle(showCityLines, "显示城市线路");
            //if (showCityLines != (lineRenderers.Count > 0))
            //{
            //    if (showCityLines)
            //    {
            //        CreateLineRenderers();
            //    }
            //    else
            //    {
            //        ClearLineRenderers();
            //    }
            //}
            //GUILayout.Space(10);


        }

        /// <summary>
        /// 打开属性编辑窗口
        /// </summary>
        private void OpenPropertyEditorWindow()
        {
            if (propertyEditorWindow == null)
            {
                propertyEditorWindow = EditorWindow.AddWindow<CityPropertyEditorWindow>(1102, new UnityEngine.Rect(0, 0, 400, 600), null, "城市属性编辑") as CityPropertyEditorWindow;
                propertyEditorWindow.Initialize(editor.scenario, editor.undoRedoManager, selectedCity);
            }
            else
            {
                propertyEditorWindow.SetCity(selectedCity);
                propertyEditorWindow.visible = true;
            }
        }

        public override void OnEnter()
        {
            //if (showCityLines)
            {
                CreateLineRenderers();
            }
        }

        public override void DrawGizmos(Vector3 center)
        {
            switch (controlType)
            {
                case CityEditorType.Move:
                    {
                        // 城市类型选择
                        if (selectedCity != null)
                        {
                            Vector3 pos = center;
                            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                            Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);
                            pos = editor.map.mapGrid.hexWorld.CoordsToPosition(offset.col, offset.row);
                            pos.y = editor.map.mapGrid.GetGridHeight(offset.col, offset.row);
                            selectedCity.Render.MapObject.position = pos;
                        }
                    }
                    break;
                case CityEditorType.Create:
                    {
                        if (previewModel != null)
                        {
                            Vector3 pos = center;
                            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
                            Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(hex);
                            pos = editor.map.mapGrid.hexWorld.CoordsToPosition(offset.col, offset.row);
                            pos.y = editor.map.mapGrid.GetGridHeight(offset.col, offset.row);
                            previewModel.transform.position = pos;
                        }
                    }
                    break;
                case CityEditorType.Connect:
                    {
                        City city = GetCityAtPosition(center);
                        if (city != hoverCity)
                        {
                            hoverCity = city;
                        }

                        if (isConnecting && connectStartCity != null && hoverCity != null && hoverCity != connectStartCity)
                        {
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawLine(connectStartCity.Render.MapObject.transform.position, hoverCity.Render.MapObject.transform.position);
                        }
                    }
                    break;
            }
        }

        public override void Update()
        {
            if (RTEditor.EditorObjectSelection.Instance.SelectedGameObjects.Count > 0)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    RTEditor.EditorObjectSelection.Instance.ClearSelection(false);
                    selectedCity = null;
                }
                return;
            }

            switch (controlType)
            {
                case CityEditorType.Move:
                    {
                        // 城市类型选择
                        if (selectedCity != null)
                        {
                            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                            {
                                Vector3 pos = editor.map.mapGrid.hexWorld.CoordsToPosition(selectedCity.x, selectedCity.y);
                                pos.y = editor.map.mapGrid.GetGridHeight(selectedCity.x, selectedCity.y);
                                selectedCity.Render.MapObject.position = pos;
                                selectedCity = null;
                                return;
                            }

                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
                            {
                                if (hit.point != lastCenter)
                                {
                                    if (Input.GetMouseButtonDown(0))
                                    {
                                        Modify(hit.point, editor);
                                        lastCenter = hit.point;
                                        return;
                                    }
                                    DrawGizmos(hit.point);
                                }
                            }
                        }
                    }
                    break;
                case CityEditorType.Create:
                    {
                        if (previewModel != null)
                        {
                            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                            {
                                ClearPreviewModel();
                                return;
                            }

                            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
                            {
                                if (hit.point != lastCenter)
                                {
                                    if (!IsPointerOverUI() && Input.GetMouseButtonDown(0))
                                    {
                                        Modify(hit.point, editor);
                                        lastCenter = hit.point;
                                        return;
                                    }
                                    DrawGizmos(hit.point);
                                }
                            }
                        }

                        if (selectedCity != null && Input.GetKeyDown(KeyCode.Delete))
                        {
                            if(selectedCity.IsCity())
                            {
                                editor.scenario.citySet.ForEach(x =>
                                {
                                    if (x != null)
                                    {
                                        if (x.IsCity())
                                        {
                                            x.NeighborList.Remove(selectedCity);
                                        }
                                        else
                                        {
                                            if(x.BelongCity == selectedCity.Id)
                                            {
                                                float minDis = 99999;
                                                editor.scenario.citySet.ForEach(y =>
                                                {
                                                    if (y.IsCity() && y != selectedCity)
                                                    {
                                                        float len = (x.Render.MapObject.position - y.Render.MapObject.position).sqrMagnitude;
                                                        if (len < minDis)
                                                        {
                                                            x.mBelongCity = y;
                                                            x.BelongCity = y.Id;
                                                            minDis = len;
                                                        }
                                                    }
                                                });
                                            }
                                        }
                                    }
                                });
                            }

                            editor.scenario.citySet.Remove(selectedCity);
                            selectedCity.Render?.Clear();
                            Sango.Log.Info($"删除城市: {selectedCity.Name}");


                            selectedCity = null;
                        }
                    }
                    break;
                case CityEditorType.Connect:
                    {
                        if (lineRenderers.Count > 0)
                        {
                            UpdateLineRenderers();
                        }

                        if (Input.GetMouseButtonDown(1))
                        {
                            if (isConnecting)
                            {
                                isConnecting = false;
                                connectStartCity = null;
                                connectHint = "连接操作已取消";
                                Sango.Log.Info("连接操作已取消");
                            }
                            else if (selectedCity != null)
                            {
                                if (selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(false);
                                }
                                selectedCity = null;
                                Sango.Log.Info("取消选择城市");
                            }
                        }
                    }
                    break;
                case CityEditorType.Belong:
                    {
                        if (lineRenderers.Count > 0)
                        {
                            UpdateLineRenderers();
                        }

                        if (Input.GetMouseButtonDown(1))
                        {
                            if (isConnecting)
                            {
                                isConnecting = false;
                                connectStartCity = null;
                                connectHint = "连接操作已取消";
                                Sango.Log.Info("连接操作已取消");
                            }
                            else if (selectedCity != null)
                            {
                                if (selectedCity.Render != null)
                                {
                                    selectedCity.Render.SetFlash(false);
                                }
                                selectedCity = null;
                                Sango.Log.Info("取消选择城市");
                            }
                        }
                    }
                    break;
            }


            if (!IsPointerOverUI() && Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastObjectLayer))
                {
                    Modify(hit.point, editor);
                }
            }
        }

        /// <summary>
        /// 编辑城市属性（int类型）
        /// </summary>
        private void EditCityProperty(string name, ref int value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString(), GUILayout.Width(100));
            if (int.TryParse(newValueStr, out int newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                CityEditCommand command = new CityEditCommand(selectedCity, name.ToLower(), value, newValue, $"修改城市{name}: {value} -> {newValue}");
                editor.undoRedoManager.AddCommand(command, true);
                value = newValue;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 编辑城市属性（float类型）
        /// </summary>
        private void EditCityProperty(string name, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString(), GUILayout.Width(100));
            if (float.TryParse(newValueStr, out float newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                CityEditCommand command = new CityEditCommand(selectedCity, name.ToLower(), value, newValue, $"修改城市{name}: {value} -> {newValue}");
                editor.undoRedoManager.AddCommand(command, true);
                value = newValue;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 编辑城市属性（byte类型）
        /// </summary>
        private void EditCityProperty(string name, ref byte value, int min, int max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(100));
            string newValueStr = GUILayout.TextField(value.ToString(), GUILayout.Width(100));
            if (int.TryParse(newValueStr, out int newValue) && newValue != value)
            {
                newValue = Mathf.Clamp(newValue, min, max);
                CityEditCommand command = new CityEditCommand(selectedCity, name.ToLower(), value, (byte)newValue, $"修改城市{name}: {value} -> {newValue}");
                editor.undoRedoManager.AddCommand(command, true);
                value = (byte)newValue;
            }
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// 获取指定位置的城市
        /// </summary>
        private City GetCityAtPosition(Vector3 position)
        {
            if (editor.scenario == null || editor.scenario.citySet == null)
                return null;

            foreach (City city in editor.scenario.citySet)
            {
                if (city != null && city.Render != null && city.Render.MapObject != null)
                {
                    Collider collider = city.Render.MapObject.GetComponent<Collider>();
                    if (collider != null && collider.bounds.Contains(position))
                    {
                        return city;
                    }
                }
            }

            return null;
        }

        // 定义100个常用颜色
        Color32[] colorLibrary = new Color32[]
        {
                new Color32(255, 0, 0, 255),     // 红色
                new Color32(0, 0, 255, 255),     // 蓝色
                new Color32(0, 255, 0, 255),     // 绿色
                new Color32(255, 255, 0, 255),   // 黄色
                new Color32(255, 0, 255, 255),   // 洋红色
                new Color32(0, 255, 255, 255),   // 青色
                new Color32(255, 165, 0, 255),   // 橙色
                new Color32(128, 0, 128, 255),   // 紫色
                new Color32(0, 128, 128, 255),   // 蓝绿色
                new Color32(128, 128, 0, 255),   // 橄榄色
                new Color32(128, 0, 0, 255),     // 栗色
                new Color32(0, 128, 0, 255),     // 深绿色
                new Color32(0, 0, 128, 255),     // 深蓝色
                new Color32(192, 192, 192, 255), // 银色
                new Color32(128, 128, 128, 255), // 灰色
                new Color32(255, 192, 203, 255), // 粉色
                new Color32(240, 230, 140, 255), // 卡其色
                new Color32(143, 188, 143, 255), // 暗海绿色
                new Color32(70, 130, 180, 255),  // 钢蓝色
                new Color32(221, 160, 221, 255), // 李子色
                new Color32(255, 69, 0, 255),    // 橙红色
                new Color32(255, 215, 0, 255),   // 金色
                new Color32(0, 250, 154, 255),   // 中春绿色
                new Color32(138, 43, 226, 255),  // 蓝紫色
                new Color32(255, 20, 147, 255),  // 深粉色
                new Color32(0, 191, 255, 255),   // 深天蓝色
                new Color32(127, 255, 212, 255), // 中绿松石色
                new Color32(210, 105, 30, 255),  // 巧克力色
                new Color32(255, 127, 80, 255),  // 珊瑚色
                new Color32(32, 178, 170, 255),  // 暗青色
                new Color32(255, 228, 181, 255), // 菊苣色
                new Color32(0, 255, 127, 255),   // 春绿色
                new Color32(240, 128, 128, 255), // 浅珊瑚色
                new Color32(72, 61, 139, 255),   // 暗石板蓝
                new Color32(100, 149, 237, 255), // 矢车菊蓝
                new Color32(107, 142, 35, 255),  // 深卡其色
                new Color32(255, 99, 71, 255),   // 番茄色
                new Color32(152, 251, 152, 255), // 浅绿色
                new Color32(135, 206, 235, 255), // 天蓝色
                new Color32(244, 164, 96, 255),  // 沙褐色
                new Color32(218, 112, 214, 255), // 兰花色
                new Color32(189, 183, 107, 255), // 暗金色
                new Color32(255, 222, 173, 255), // 小麦色
                new Color32(233, 150, 122, 255), // 深鲑鱼色
                new Color32(255, 248, 220, 255), // 玉米色
                new Color32(250, 128, 114, 255), // 鲑鱼色
                new Color32(211, 211, 211, 255), // 浅灰色
                new Color32(190, 190, 190, 255), // 亮灰色
                new Color32(238, 232, 170, 255), // 帕洛米诺色
                new Color32(173, 255, 47, 255),  // 绿黄色
                new Color32(255, 218, 185, 255), // 粉珊瑚色
                new Color32(0, 255, 255, 255),   // 青色
                new Color32(255, 255, 255, 255), // 白色
                new Color32(0, 0, 0, 255),       // 黑色
                new Color32(255, 255, 0, 255),   // 黄色
                new Color32(255, 0, 255, 255),   // 洋红色
                new Color32(0, 255, 255, 255),   // 青色
                new Color32(255, 165, 0, 255),   // 橙色
                new Color32(128, 0, 128, 255),   // 紫色
                new Color32(0, 128, 128, 255),   // 蓝绿色
                new Color32(128, 128, 0, 255),   // 橄榄色
                new Color32(128, 0, 0, 255),     // 栗色
                new Color32(0, 128, 0, 255),     // 深绿色
                new Color32(0, 0, 128, 255),     // 深蓝色
                new Color32(192, 192, 192, 255), // 银色
                new Color32(128, 128, 128, 255), // 灰色
                new Color32(255, 192, 203, 255), // 粉色
                new Color32(240, 230, 140, 255), // 卡其色
                new Color32(143, 188, 143, 255), // 暗海绿色
                new Color32(70, 130, 180, 255),  // 钢蓝色
                new Color32(221, 160, 221, 255), // 李子色
                new Color32(255, 69, 0, 255),    // 橙红色
                new Color32(255, 215, 0, 255),   // 金色
                new Color32(0, 250, 154, 255),   // 中春绿色
                new Color32(138, 43, 226, 255),  // 蓝紫色
                new Color32(255, 20, 147, 255),  // 深粉色
                new Color32(0, 191, 255, 255),   // 深天蓝色
                new Color32(127, 255, 212, 255), // 中绿松石色
                new Color32(210, 105, 30, 255),  // 巧克力色
                new Color32(255, 127, 80, 255),  // 珊瑚色
                new Color32(32, 178, 170, 255),  // 暗青色
                new Color32(255, 228, 181, 255), // 菊苣色
                new Color32(0, 255, 127, 255),   // 春绿色
                new Color32(240, 128, 128, 255), // 浅珊瑚色
                new Color32(72, 61, 139, 255),   // 暗石板蓝
                new Color32(100, 149, 237, 255), // 矢车菊蓝
                new Color32(107, 142, 35, 255),  // 深卡其色
                new Color32(255, 99, 71, 255),   // 番茄色
                new Color32(152, 251, 152, 255), // 浅绿色
                new Color32(135, 206, 235, 255), // 天蓝色
                new Color32(244, 164, 96, 255),  // 沙褐色
                new Color32(218, 112, 214, 255), // 兰花色
                new Color32(189, 183, 107, 255), // 暗金色
                new Color32(255, 222, 173, 255), // 小麦色
                new Color32(233, 150, 122, 255), // 深鲑鱼色
                new Color32(255, 248, 220, 255), // 玉米色
                new Color32(250, 128, 114, 255), // 鲑鱼色
                new Color32(211, 211, 211, 255), // 浅灰色
                new Color32(190, 190, 190, 255), // 亮灰色
                new Color32(238, 232, 170, 255), // 帕洛米诺色
                new Color32(173, 255, 47, 255),  // 绿黄色
                new Color32(255, 218, 185, 255), // 粉珊瑚色
                new Color32(0, 255, 255, 255),   // 青色
        };


        /// <summary>
        /// 创建城市之间的线路
        /// </summary>
        private void CreateLineRenderers(bool showNeighbor = true)
        {
            ClearLineRenderers();

            if (editor.scenario == null || editor.scenario.citySet == null)
                return;


            foreach (City city in editor.scenario.citySet)
            {
                if (city != null && city.Render != null && city.Render.MapObject != null)
                {
                    if (city.IsCity())
                    {
                        if (showNeighbor)
                        {
                            foreach (City neighbor in city.NeighborList)
                            {
                                if (neighbor != null && neighbor.Render != null && neighbor.Render.MapObject != null)
                                {
                                    // 确保只创建一次线路，避免AB和BA重复
                                    if (city.Id > neighbor.Id) // 只处理city.Id > neighbor.Id的情况，确保每个组合只处理一次
                                    {
                                        // 避免重复创建线路
                                        bool lineExists = false;
                                        foreach (GameObject lineObj in lineRenderers)
                                        {
                                            LineRenderer lineRenderer = lineObj.GetComponent<LineRenderer>();
                                            if (lineRenderer != null)
                                            {
                                                Vector3 start = lineRenderer.GetPosition(0);
                                                Vector3 end = lineRenderer.GetPosition(2);
                                                Vector3 cityPos = city.Render.MapObject.transform.position;
                                                Vector3 neighborPos = neighbor.Render.MapObject.transform.position;
                                                if ((start == cityPos && end == neighborPos) || (start == neighborPos && end == cityPos))
                                                {
                                                    lineExists = true;
                                                    break;
                                                }
                                            }
                                        }

                                        if (!lineExists)
                                        {
                                            GameObject lineObj = new GameObject($"CityLine_{city.Name}_{neighbor.Name}");
                                            LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
                                            // 使用Sango/Particles/Alpha Blended shader
                                            Shader sangoShader = Shader.Find("Sango/Particles/Alpha Blended");
                                            if (sangoShader != null)
                                            {
                                                lineRenderer.material = new Material(sangoShader);
                                            }
                                            else
                                            {
                                                // 如果找不到Sango shader，使用默认的Unlit/Color作为备份
                                                lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
                                            }
                                            lineRenderer.material.renderQueue = 4000;
                                            // 设置ZTest为Disabled
                                            lineRenderer.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Disabled);

                                            // 根据城市ID从颜色库中获取颜色
                                            int cityColorIndex = city.Id % colorLibrary.Length;
                                            int neighborColorIndex = neighbor.Id % colorLibrary.Length;
                                            Color startColor = colorLibrary[cityColorIndex];
                                            Color endColor = colorLibrary[neighborColorIndex];

                                            // 设置LineRenderer的颜色渐变
                                            Gradient gradient = new Gradient();
                                            gradient.SetKeys(
                                                new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                                                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                                            );
                                            lineRenderer.colorGradient = gradient;
                                            lineRenderer.numCapVertices = 16;
                                            lineRenderer.numCornerVertices = 16;

                                            Vector3 startPos = city.Render.MapObject.transform.position;
                                            Vector3 endPos = neighbor.Render.MapObject.transform.position;
                                            Vector3 midPos = (startPos + endPos) / 2f;

                                            lineRenderer.positionCount = 3;
                                            lineRenderer.SetPosition(0, startPos);
                                            lineRenderer.SetPosition(1, midPos);
                                            lineRenderer.SetPosition(2, endPos);

                                            lineRenderer.startWidth = 3f;
                                            lineRenderer.endWidth = 3f;
                                            lineRenderer.widthCurve = new AnimationCurve(
                                                new Keyframe(0f, 3f),
                                                new Keyframe(0.5f, 10f),
                                                new Keyframe(1f, 3f)
                                            );

                                            lineRenderers.Add(lineObj);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (city != null && city.Render != null && city.Render.MapObject != null)
                        {
                            City neighbor = editor.scenario.citySet.Get(city.BelongCity);
                            if (neighbor != null && neighbor.Render != null && neighbor.Render.MapObject != null)
                            {
                                // 避免重复创建线路
                                bool lineExists = false;
                                foreach (GameObject lineObj in lineRenderers)
                                {
                                    LineRenderer lineRenderer = lineObj.GetComponent<LineRenderer>();
                                    if (lineRenderer != null)
                                    {
                                        Vector3 start = lineRenderer.GetPosition(0);
                                        Vector3 end = lineRenderer.GetPosition(2);
                                        Vector3 cityPos = city.Render.MapObject.transform.position;
                                        Vector3 neighborPos = neighbor.Render.MapObject.transform.position;
                                        if ((start == cityPos && end == neighborPos) || (start == neighborPos && end == cityPos))
                                        {
                                            lineExists = true;
                                            break;
                                        }
                                    }
                                }

                                if (!lineExists)
                                {
                                    GameObject lineObj = new GameObject($"CityLine_{city.Name}_{neighbor.Name}");
                                    LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();
                                    // 使用Sango/Particles/Alpha Blended shader
                                    Shader sangoShader = Shader.Find("Sango/Particles/Alpha Blended");
                                    if (sangoShader != null)
                                    {
                                        lineRenderer.material = new Material(sangoShader);
                                    }
                                    else
                                    {
                                        // 如果找不到Sango shader，使用默认的Unlit/Color作为备份
                                        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
                                    }
                                    lineRenderer.material.renderQueue = 4000;
                                    // 设置ZTest为Disabled
                                    lineRenderer.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Disabled);

                                    // 根据城市ID从颜色库中获取颜色
                                    int cityColorIndex = city.Id % colorLibrary.Length;
                                    int neighborColorIndex = neighbor.Id % colorLibrary.Length;
                                    Color startColor = colorLibrary[cityColorIndex];
                                    Color endColor = colorLibrary[neighborColorIndex];

                                    // 设置LineRenderer的颜色渐变
                                    Gradient gradient = new Gradient();
                                    gradient.SetKeys(
                                        new GradientColorKey[] { new GradientColorKey(startColor, 0f), new GradientColorKey(endColor, 1f) },
                                        new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                                    );
                                    lineRenderer.colorGradient = gradient;
                                    lineRenderer.numCapVertices = 16;
                                    lineRenderer.numCornerVertices = 16;

                                    Vector3 startPos = city.Render.MapObject.transform.position;
                                    Vector3 endPos = neighbor.Render.MapObject.transform.position;
                                    Vector3 midPos = (startPos + endPos) / 2f;

                                    lineRenderer.positionCount = 3;
                                    lineRenderer.SetPosition(0, startPos);
                                    lineRenderer.SetPosition(1, midPos);
                                    lineRenderer.SetPosition(2, endPos);

                                    lineRenderer.startWidth = 3f;
                                    lineRenderer.endWidth = 3f;
                                    lineRenderer.widthCurve = new AnimationCurve(
                                        new Keyframe(0f, 3f),
                                        new Keyframe(0.5f, 10f),
                                        new Keyframe(1f, 3f)
                                    );

                                    lineRenderers.Add(lineObj);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 更新城市之间的线路
        /// </summary>
        private void UpdateLineRenderers()
        {
            foreach (GameObject lineObj in lineRenderers)
            {
                LineRenderer lineRenderer = lineObj.GetComponent<LineRenderer>();
                if (lineRenderer != null)
                {
                    // 这里可以添加线路更新逻辑，比如根据城市位置变化更新线路
                }
            }
        }

        /// <summary>
        /// 清理城市之间的线路
        /// </summary>
        private void ClearLineRenderers()
        {
            foreach (GameObject lineObj in lineRenderers)
            {
                if (lineObj != null)
                {
                    GameObject.Destroy(lineObj);
                }
            }
            lineRenderers.Clear();
        }

        /// <summary>
        /// 拖拽开始
        /// </summary>
        public override void OnDragStart(Vector3 center)
        {
        }

        /// <summary>
        /// 拖拽过程
        /// </summary>
        public override void OnDrag(Vector3 center)
        {
        }

        /// <summary>
        /// 拖拽结束
        /// </summary>
        public override void OnDragEnd(Vector3 center)
        {
        }
    }
}
