using UnityEngine;
using System.IO;
using System;
using HSVPicker;

using System.Drawing;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Sango.Core;
using System.Collections;
using Sango.Tools.UndoRedo;
using UnityEngine.UI;
using System.Text;
using static Sango.Render.MapGrid;
using Unity.VisualScripting;

namespace Sango.Tools
{
    /// <summary>
    /// 地格编辑笔刷
    /// 支持地格类型、区域、内政、防守等属性的编辑
    /// </summary>
    public class GridBrush : BrushBase
    {
        /// <summary>
        /// 笔刷类型枚举
        /// </summary>
        public enum BrushType : int
        {
            /// <summary>
            /// 地形类型
            /// </summary>
            TerrainType,
            /// <summary>
            /// 区域类型
            /// </summary>
            Area,
            /// <summary>
            /// 内政属性
            /// </summary>
            Interior,
            /// <summary>
            /// 防守属性
            /// </summary>
            Defence,
            /// <summary>
            /// 贼属性
            /// </summary>
            Thief,
            /// <summary>
            /// 未知类型
            /// </summary>
            Unknown,
        }

        /// <summary>
        /// 笔刷大小
        /// </summary>
        public int size = 1;

        /// <summary>
        /// 笔刷透明度
        /// </summary>
        public int opacity;

        /// <summary>
        /// 工具栏标题数组
        /// </summary>
        private string[] toolbarTitle = new string[] {
            "无",
            "类型",
            "区域",
            "内政",
            "防守",
            "贼",
        };

        /// <summary>
        /// 当前编辑模式索引
        /// </summary>
        private int currentEditMode = 0;

        /// <summary>
        /// 当前笔刷类型
        /// </summary>
        public BrushType brushType = BrushType.Unknown;

        /// <summary>
        /// 地形类型纹理名称数组
        /// </summary>
        public string[] terrainTypeTexNames = new string[] {
            "editor_terrain_type",
            "editor_area_type",
            "editor_interior_type",
            "editor_defence_type",
            "editor_thief_type",
        };

        /// <summary>
        /// 地形类型纹理数组
        /// </summary>
        public Texture[] terrainTypeTexes = new Texture[] {
            Texture2D.whiteTexture,
            Texture2D.whiteTexture,
            Texture2D.whiteTexture,
            Texture2D.whiteTexture,
            Texture2D.whiteTexture,
        };


        /// <summary>
        /// 当前地形类型纹理
        /// </summary>
        public Texture2D terrainTypeTex;

        /// <summary>
        /// 地形类型遮罩纹理
        /// </summary>
        public Texture2D terrainTypeMaskTex;

        /// <summary>
        /// 遮罩纹理列数
        /// </summary>
        public int terrainTypeMaskCol = 4;

        /// <summary>
        /// 遮罩纹理行数
        /// </summary>
        public int terrainTypeMaskRow = 8;

        /// <summary>
        /// 是否显示地形类型
        /// </summary>
        public bool showTerrainType = false;

        /// <summary>
        /// 是否显示网格
        /// </summary>
        private bool showGrid = true;

        /// <summary>
        /// 遮罩窗口矩形
        /// </summary>
        internal UnityEngine.Rect maskWindowRect = new UnityEngine.Rect(20, 20, 256, 256);

        private string[] dirTypeTitle = new string[] {
            "左上", "上", "右上", "右下", "下", "左下", "无"
        };
        private string[] trapTypeTitle = new string[] {
            "无", "堤防", "落石",
        };
        private string[] terrainTypeTitle = new string[] {
        "草地", "土地", "砂地", "湿地",
        "毒泉", "森林", "江河", "河道",
        "大海", "荒地", "道路", "栈道",
        "桥", "浅滩", "岸", "山崖",
        "都市", "港", "关所", "间道"};

        EditorWindow infoWind;
        float gridInfoAlpha = 1;

        // 拖拽相关变量
        private Dictionary<int, GridEditCommand.GridDataChange> dragChangesMap = new Dictionary<int, GridEditCommand.GridDataChange>();
        private GridEditCommand.EditType dragEditType;
        private string dragDescription;
        private List<Sango.Hexagon.Hex> dragHexList = new List<Sango.Hexagon.Hex>();

        public GridBrush(MapEditor e) : base(e)
        {
            brushType = BrushType.TerrainType;
            infoWind = EditorWindow.AddWindow(1101, maskWindowRect, DrawWindow, "信息图");
            infoWind.visible = false;
            int count = 32;
            terrainTypeTitle = new string[count];
            for (int i = 0; i < count; ++i)
            {
                TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(i);
                terrainTypeTitle[i] = terrainType.Name;
            }
        }

        /// <summary>
        /// 拖拽开始
        /// </summary>
        /// <param name="center">中心点</param>
        public override void OnDragStart(Vector3 center)
        {
            // 清空之前的拖拽数据
            dragChangesMap.Clear();
            dragHexList.Clear();

            // 设置编辑类型和描述
            switch (brushType)
            {
                case BrushType.TerrainType:
                    dragEditType = GridEditCommand.EditType.TerrainType;
                    dragDescription = "修改地形类型";
                    break;
                case BrushType.Area:
                    dragEditType = GridEditCommand.EditType.Area;
                    dragDescription = "修改区域";
                    break;
                case BrushType.Interior:
                    dragEditType = GridEditCommand.EditType.Interior;
                    dragDescription = "修改内政";
                    break;
                case BrushType.Defence:
                    dragEditType = GridEditCommand.EditType.Defence;
                    dragDescription = "修改防守";
                    break;
                case BrushType.Thief:
                    dragEditType = GridEditCommand.EditType.Thief;
                    dragDescription = "修改贼";
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// 拖拽过程
        /// </summary>
        /// <param name="center">中心点</param>
        public override void OnDrag(Vector3 center)
        {
            if (currentEditMode <= 0)
                return;

            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);

            // 计算笔刷影响范围
            dragHexList.Clear();
            hex.Spiral(size, dragHexList);

            int value = opacity;
            if (Input.GetKey(KeyCode.LeftShift))
                value = InvertOpacity(brushType, opacity);

            // 收集变化数据
            foreach (Sango.Hexagon.Hex h in dragHexList)
            {
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);

                // 假设地格的宽度为地图的列数
                int gridWidth = editor.map.mapGrid.bounds.x;
                // 使用数学公式计算唯一key：col + width * row
                int gridKey = coord.col + gridWidth * coord.row;

                MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
                int oldValue = GetGridDataProterty(brushType, data);

                // 创建临时副本进行修改
                MapGrid.GridData tempData = data;
                SetGridDataProterty(brushType, tempData, (byte)value);
                int newValue = GetGridDataProterty(brushType, tempData);
                SetTerrainMaskShowColor(coord.col, coord.row, newValue, terrainTypeMaskCol, terrainTypeMaskRow);

                switch (brushType)
                {
                    case BrushType.TerrainType:
                        editor.map.mapGrid.BeginUpdateMovable(coord.col, coord.row);
                        break;
                }

                // 检查是否已经有记录
                if (dragChangesMap.ContainsKey(gridKey))
                {
                    // 更新现有记录的新值为最终值
                    GridEditCommand.GridDataChange existingChange = dragChangesMap[gridKey];
                    existingChange.newValue = newValue;
                    dragChangesMap[gridKey] = existingChange;
                }
                else
                {
                    // 创建新记录
                    GridEditCommand.GridDataChange change = new GridEditCommand.GridDataChange();
                    change.col = coord.col;
                    change.row = coord.row;
                    change.oldValue = oldValue;
                    change.newValue = newValue;
                    dragChangesMap.Add(gridKey, change);
                }
            }

            // 更新地形掩码显示
            terrainTypeMaskTex.Apply(false);
            editor.map.mapGrid.EndUpdateMovable();
        }

        /// <summary>
        /// 拖拽结束
        /// </summary>
        /// <param name="center">中心点</param>
        public override void OnDragEnd(Vector3 center)
        {
            // 如果有变化，创建批量命令并执行
            if (dragChangesMap.Count > 0)
            {
                List<GridEditCommand.GridDataChange> finalChanges = new List<GridEditCommand.GridDataChange>(dragChangesMap.Values);
                GridEditCommand command = new GridEditCommand(editor, dragEditType, finalChanges, dragDescription, this);
                editor.undoRedoManager.AddCommand(command);
            }

            // 清空拖拽数据
            dragChangesMap.Clear();
            dragHexList.Clear();
        }

        // 准备图层贴图
        IEnumerator CreateLayerTexture()
        {
            Debug.LogError("正在创建所需贴图,请稍等!!");

            int celSize = 128;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainLayer")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 32;// GameData.Instance.ScenarioCommonData.TerrainTypes.Length;
            terrainTypeMaskCol = 8;
            while (terrainTypeMaskCol * terrainTypeMaskCol < count)
                terrainTypeMaskCol *= 2;

            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                TerrainType terrainType = GameData.Instance.ScenarioCommonData.TerrainTypes.Get(i);
                text.text = $"{terrainType.Name}\n{terrainType.Id}";
                text.color = UnityEngine.Color.white;
                image[0].color = terrainType.color;
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for (int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            terrainTypeTexes[(int)BrushType.TerrainType] = terrainTypeTex;

            string savePath = Path.ContentRootPath + $"/Assets/Map/{editor.map.WorkContent}/Editor/editor_terrain_type.png";
            Sango.Directory.Create(savePath, false);
            if (File.Exists(savePath))
                File.Delete(savePath);
            File.WriteAllBytes(savePath, terrainTypeTex.EncodeToPNG());

            terrainTypeTex.EncodeToPNG();
            UpdateTerrainMaskTex(brushType);
            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            terrainTypeTex = null;
            GameObject.Destroy(texCreator);

            yield return CreateAreaTexture();
        }

        IEnumerator CreateAreaTexture()
        {
            int celSize = 64;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainArea")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 2048 / 64;
            int terrainTypeMaskCol = count;
            int terrainTypeMaskRow = count;
            count = count * count;
            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                text.text = $"{i}";
                text.color = UnityEngine.Color.white;
                image[0].color = new UnityEngine.Color(UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f), UnityEngine.Random.Range(0, 1f));
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for (int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            terrainTypeTexes[(int)BrushType.Area] = terrainTypeTex;

            string savePath = Path.ContentRootPath + $"/Assets/Map/{editor.map.WorkContent}/Editor/editor_area_type.png";
            Sango.Directory.Create(savePath, false);
            if (File.Exists(savePath))
                File.Delete(savePath);
            File.WriteAllBytes(savePath, terrainTypeTex.EncodeToPNG());

            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);

            yield return CreateDefenceTexture();
        }

        IEnumerator CreateDefenceTexture()
        {
            int celSize = 128;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainLayer")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 2;
            int terrainTypeMaskCol = count;
            int terrainTypeMaskRow = count;

            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                text.fontSize = 30;
                text.text = i == 0 ? "\n" : $"\n防守";
                text.color = UnityEngine.Color.white;
                image[0].color = i == 0 ? UnityEngine.Color.white : UnityEngine.Color.red;
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for (int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            terrainTypeTexes[(int)BrushType.Defence] = terrainTypeTex;

            string savePath = Path.ContentRootPath + $"/Assets/Map/{editor.map.WorkContent}/Editor/editor_defence_type.png";
            Sango.Directory.Create(savePath, false);
            if (File.Exists(savePath))
                File.Delete(savePath);
            File.WriteAllBytes(savePath, terrainTypeTex.EncodeToPNG());

            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);

            yield return CreateThiefTexture();
        }

        IEnumerator CreateThiefTexture()
        {
            int celSize = 128;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainLayer")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 2;
            int terrainTypeMaskCol = count;
            int terrainTypeMaskRow = count;

            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                text.fontSize = 30;
                text.text = i == 0 ? "\n" : $"\n贼";
                text.color = UnityEngine.Color.white;
                image[0].color = i == 0 ? UnityEngine.Color.white : UnityEngine.Color.red;
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for (int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            terrainTypeTexes[(int)BrushType.Thief] = terrainTypeTex;

            string savePath = Path.ContentRootPath + $"/Assets/Map/{editor.map.WorkContent}/Editor/editor_thief_type.png";
            Sango.Directory.Create(savePath, false);
            if (File.Exists(savePath))
                File.Delete(savePath);
            File.WriteAllBytes(savePath, terrainTypeTex.EncodeToPNG());

            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);

            yield return CreateInteriorTexture();
        }


        IEnumerator CreateInteriorTexture()
        {
            int celSize = 128;
            GameObject texCreator = GameObject.Instantiate(Resources.Load("TerrainLayer")) as GameObject;
            UnityEngine.UI.Text text = texCreator.GetComponentInChildren<UnityEngine.UI.Text>();
            UnityEngine.UI.RawImage[] image = texCreator.GetComponentsInChildren<UnityEngine.UI.RawImage>(true);
            Camera cam = texCreator.GetComponent<Camera>();
            RenderTexture renderTexture = RenderTexture.GetTemporary(celSize, celSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 8);
            int count = 2;
            int terrainTypeMaskCol = count;
            int terrainTypeMaskRow = count;

            terrainTypeTex = new Texture2D(terrainTypeMaskCol * celSize, terrainTypeMaskCol * celSize);
            terrainTypeMaskRow = terrainTypeMaskCol;
            Texture gridTex = Resources.Load<Texture>("layer_grid");
            for (int i = 0; i < count; ++i)
            {
                text.fontSize = 30;
                text.text = i == 0 ? "\n" : $"\n内政";
                text.color = UnityEngine.Color.white;
                image[0].color = i == 0 ? UnityEngine.Color.white : UnityEngine.Color.green;
                image[0].texture = gridTex;
                cam.enabled = true;
                cam.targetTexture = renderTexture;
                cam.Render();
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                cam.enabled = false;
                cam.targetTexture = null;
                RenderTexture.active = renderTexture;
                Texture2D texture2D = new Texture2D(celSize, celSize);
                texture2D.ReadPixels(new UnityEngine.Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                texture2D.Apply(); // 应用更改
                RenderTexture.active = null; // 重置RenderTexture.active以避免潜在问题
                UnityEngine.Color[] pixels = texture2D.GetPixels();

                int xStart = i % terrainTypeMaskCol * texture2D.width;
                int yStart = (terrainTypeMaskCol - 1 - i / terrainTypeMaskCol) * texture2D.width;

                for (int x = 0; x < texture2D.width; ++x)
                    for (int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            terrainTypeTexes[(int)BrushType.Interior] = terrainTypeTex;

            string savePath = Path.ContentRootPath + $"/Assets/Map/{editor.map.WorkContent}/Editor/editor_interior_type.png";
            Sango.Directory.Create(savePath, false);
            if (File.Exists(savePath))
                File.Delete(savePath);
            File.WriteAllBytes(savePath, terrainTypeTex.EncodeToPNG());

            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);

            Debug.LogError("创建所需贴图完成!!");
            yield return null;
        }

        public override void OnEnter()
        {
            Shader.SetGlobalFloat("_terrainTypeAlpha", 1);
            bool needCreateTex = false;
            for (int i = 0; i < terrainTypeTexNames.Length; ++i)
            {
                terrainTypeTexes[i] = editor.map.CreateTexture("Editor/" + terrainTypeTexNames[i]);
                if (terrainTypeTexes[i] == Texture2D.whiteTexture)
                {
                    needCreateTex = true;
                }
            }

            if (terrainTypeMaskTex == null)
            {
                terrainTypeMaskTex = new Texture2D(editor.map.mapGrid.bounds.x, editor.map.mapGrid.bounds.y, TextureFormat.ARGB32, false);
                terrainTypeMaskTex.wrapMode = TextureWrapMode.Clamp;
                terrainTypeMaskTex.filterMode = FilterMode.Point;
            }

            if (needCreateTex)
                Game.Instance.StartCoroutine(CreateLayerTexture());

            InitGridShow();
        }
        public UnityEngine.Color TypeIndexToColor(int index)
        {
            int col = index % terrainTypeMaskCol;
            int row = index / terrainTypeMaskCol;
            row = terrainTypeMaskRow - 1 - row;
            return new UnityEngine.Color((float)col / (float)terrainTypeMaskCol, (float)row / (float)terrainTypeMaskRow, 0);
        }
        public void SetTerrainTypeShow(int x, int y, int index)
        {
            y = terrainTypeMaskTex.height - y - 1;
            terrainTypeMaskTex.SetPixel(x, y, TypeIndexToColor(index));
        }
        public void SetTerrainMaskShowColor(int x, int y, int index, int colCount, int rowCount)
        {
            if (brushType == BrushType.Defence || brushType == BrushType.Thief || brushType == BrushType.Interior)
            {
                tmpGridDatas[x][y].needUpdate = true;
                return;
            }
            int col = index % colCount;
            int row = index / colCount;
            row = rowCount - 1 - row;
            UnityEngine.Color c = new UnityEngine.Color((float)col / (float)terrainTypeMaskCol, (float)row / (float)terrainTypeMaskRow, 0);
            y = terrainTypeMaskTex.height - y - 1;
            terrainTypeMaskTex.SetPixel(x, y, c);
        }
        public int GetGridDataProterty(BrushType brushType, MapGrid.GridData data)
        {
            switch (brushType)
            {
                case BrushType.TerrainType:
                    return data.terrainType;
                case BrushType.Area:
                    return data.areaId;
                //case BrushType.Trap:
                //    return data.trap;
                //case BrushType.Dir:
                //    return data.dir;
                case BrushType.Interior:
                    return data.HasGridState(MapGrid.GridState.Interior) ? 1 : 0;
                case BrushType.Defence:
                    return data.HasGridState(MapGrid.GridState.Defence) ? 1 : 0;
                case BrushType.Thief:
                    return data.HasGridState(MapGrid.GridState.Thief) ? 1 : 0;
                    //case BrushType.Flood:
                    //    return data.flood;
                    //case BrushType.Ruins:
                    //    return data.ruins;
            }
            return 0;
        }
        public MapGrid.GridData SetGridDataProterty(BrushType brushType, MapGrid.GridData data, byte value)
        {
            switch (brushType)
            {
                case BrushType.TerrainType:
                    data.terrainType = value;
                    break;
                case BrushType.Area:
                    data.areaId = value;
                    break;
                //case BrushType.Trap:
                //    data.trap = value;
                //    break;
                //case BrushType.Dir:
                //    data.dir = value;
                //    break;
                case BrushType.Interior:
                    data.SetGridState(MapGrid.GridState.Interior, value > 0);
                    break;
                case BrushType.Defence:
                    data.SetGridState(MapGrid.GridState.Defence, value > 0);
                    break;
                case BrushType.Thief:
                    data.SetGridState(MapGrid.GridState.Thief, value > 0);
                    break;
                    //case BrushType.Flood:
                    //    data.flood = value;
                    //    break;
                    //case BrushType.Ruins:
                    //    data.ruins = value;
                    //    break;
            }

            return data;
        }
        public void UpdateTerrainMaskTex()
        {
            UpdateTerrainMaskTex(brushType);
        }

        public void UpdateTerrainMaskTex(BrushType b)
        {
            BrushType target = b;
            switch (b)
            {
                case BrushType.TerrainType:
                    {
                        terrainTypeMaskCol = 8;
                        terrainTypeMaskRow = 8;
                    }
                    break;
                case BrushType.Area:
                    {
                        terrainTypeMaskCol = 32;
                        terrainTypeMaskRow = 32;
                    }
                    break;
                //case BrushType.Dir:
                //    {
                //        terrainTypeMaskCol = 4;
                //        terrainTypeMaskRow = 4;
                //    }
                //    break;
                //case BrushType.Trap:
                //    {
                //        terrainTypeMaskCol = 2;
                //        terrainTypeMaskRow = 2;
                //    }
                //    break;
                case BrushType.Interior:
                case BrushType.Defence:
                case BrushType.Thief:
                    //case BrushType.Flood:
                    //case BrushType.Ruins:
                    {
                        //terrainTypeMaskCol = 2;
                        //terrainTypeMaskRow = 2;
                        target = BrushType.Area;
                        terrainTypeMaskCol = 32;
                        terrainTypeMaskRow = 32;
                    }
                    break;
            }
            Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTexes[(int)target]);

            Shader.SetGlobalFloat("_terrainTypeMaskCol", terrainTypeMaskCol);
            Shader.SetGlobalFloat("_terrainTypeMaskRow", terrainTypeMaskRow);



            for (int i = 0; i < editor.map.mapGrid.bounds.x; ++i)
            {
                for (int j = 0; j < editor.map.mapGrid.bounds.y; ++j)
                {
                    MapGrid.GridData data = editor.map.mapGrid.GetGridData(i, j);
                    SetTerrainMaskShowColor(i, j, GetGridDataProterty(target, data), terrainTypeMaskCol, terrainTypeMaskRow);
                }
            }
            terrainTypeMaskTex.Apply(false);
            Shader.SetGlobalTexture("_TerrainTypeMaskTex", terrainTypeMaskTex);
        }
        public override void OnBrushTypeChange()
        {
            UpdateTerrainMaskTex();
        }
        public override void OnSeasonChanged(int curSeason)
        {

        }
        public override void Clear()
        {
            ClearBrushShow();
            ClearGridShow();
        }
        public void ClearBrushShow()
        {
            bool changed = false;
            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.clear);
                changed = true;
            }
            tempHexList.Clear();
            if (changed)
            {
                editor.map.mapGrid.ApplyRangMask();
            }
        }
        public void Load311GridData()
        {
            string[] path = WindowDialog.OpenFileDialog("地格文件(*.SHEX)|*.SHEX\0");
            if (path != null)
            {
                string fName = path[0];
                editor.map.mapGrid.LoadFrom311GridData(fName);
                UpdateTerrainMaskTex();
            }
        }

        public void SaveTo311GridData()
        {
            string path = WindowDialog.SaveFileDialog("4791.SHEX", "地格文件(*.SHEX)|*.SHEX\0");
            if (path != null)
            {
                editor.map.mapGrid.SaveTo311GridData(path);
            }
        }

        public override void OnGUI()
        {
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size));
            float _size = GUILayout.HorizontalSlider(size, 0f, 12f);
            if ((int)_size != size)
            {
                size = (int)_size;
                OnBrushSizeChange();
            }

            int _opacity = EditorUtility.IntField(opacity, "笔刷值");
            if (_opacity != opacity)
            {
                opacity = _opacity;
            }

            GUILayout.Label("地格信息透明度");
            float _alpha = GUILayout.HorizontalSlider(gridInfoAlpha, 0f, 1f);
            if (_alpha != gridInfoAlpha)
            {
                gridInfoAlpha = _alpha;
                Shader.SetGlobalFloat("_terrainTypeAlpha", gridInfoAlpha);
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("加载311地格数据"))
            {
                Load311GridData();
            }
            if (GUILayout.Button("保存为311地格数据"))
            {
                SaveTo311GridData();
            }
            GUILayout.EndHorizontal();


            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int editMode = GUILayout.SelectionGrid(currentEditMode, toolbarTitle, 5, GUILayout.Height(60));
            if (editMode != currentEditMode)
            {
                currentEditMode = editMode;
                if (currentEditMode > 0)
                {
                    brushType = (BrushType)currentEditMode - 1;
                    Shader.SetGlobalFloat("_TerrainTypeShowFlag", 1);
                    OnBrushTypeChange();
                }
                else
                {
                    Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);
                }
            }
            GUI.backgroundColor = lastColor;
            infoWind.visible = GUILayout.Toggle(infoWind.visible, "显示信息图");
            bool show = GUILayout.Toggle(showGrid, "显示地格");
            if (show != showGrid)
            {
                showGrid = show;
                Shader.SetGlobalFloat("_GridFlag", showGrid ? 1 : 0);
            }
            if (currentEditMode <= 0) return;

            switch (brushType)
            {
                //case BrushType.Trap:
                //    {
                //        int state = GUILayout.SelectionGrid(opacity, trapTypeTitle, 3);
                //        if (state != opacity)
                //        {
                //            opacity = state;
                //        }
                //    }
                //    break;
                //case BrushType.Dir:
                //    {
                //        int state = GUILayout.SelectionGrid(opacity, dirTypeTitle, 3);
                //        if (state != opacity)
                //        {
                //            opacity = state;
                //        }
                //    }
                //    break;
                //case BrushType.Area:
                //    {
                //        int state = GUILayout.SelectionGrid(opacity, moveStateTitle);
                //        if (state != opacity)
                //        {
                //            moveState = state;
                //        }
                //    }
                //    break;
                case BrushType.TerrainType:
                    {

                        int state = GUILayout.SelectionGrid(opacity, terrainTypeTitle, 4);
                        if (state != opacity)
                        {
                            opacity = state;
                        }
                    }
                    break;
                default:
                    {
                    }
                    break;
            }
        }

        void DrawWindow(int windowID, EditorWindow window)
        {
            //if (windowID != 1101) return;
            GUILayout.Label(terrainTypeMaskTex, GUILayout.Width(256), GUILayout.Height(256));
        }

        public override void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 1);
            }
            base.Update();
            UpdateText(MapRender.Instance.ViewRectCache);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
            {
                if (hit.point != lastCenter)
                {
                    if (currentEditMode > 0 && !IsPointerOverUI())
                    {
                        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
                        {
                            // 吸取目标值
                            SuckValue(hit.point, editor);
                            lastCenter = hit.point;
                            return;
                        }
                    }
                }
            }

            base.Update();
        }

        List<Sango.Hexagon.Hex> tempHexList = new List<Hexagon.Hex>();
        Sango.Hexagon.Hex lastHexCenter = new Sango.Hexagon.Hex();
        public override void DrawGizmos(Vector3 center)
        {
            bool changed = false;
            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
            if (hex.IsSame(lastHexCenter))
            {
                return;
            }

            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.clear);
                changed = true;
            }
            tempHexList.Clear();
            hex.Spiral(size, tempHexList);
            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                editor.map.mapGrid.SetRangMaskColor(coord.col, coord.row, UnityEngine.Color.cyan);
                changed = true;
            }

            if (changed)
            {
                editor.map.mapGrid.ApplyRangMask();
            }
        }
        /// <summary>
        /// 获取相反操作值
        /// </summary>
        /// <param name="brushType"></param>
        /// <param name="opacity"></param>
        /// <returns></returns>        
        int InvertOpacity(BrushType brushType, int opacity)
        {
            switch (brushType)
            {
                //case BrushType.Interior:
                //case BrushType.Defence:
                //case BrushType.Thief:
                //case BrushType.Flood:
                //case BrushType.Ruins:
                //    {
                //        return Math.Abs(opacity - 1);
                //    }
            }
            return opacity;
        }
        /// <summary>
        /// 吸取目标值
        /// </summary>
        /// <param name="center"></param>
        /// <param name="editor"></param>
        public override void SuckValue(Vector3 center, MapEditor editor)
        {
            Sango.Hexagon.Hex hex = editor.map.mapGrid.hexWorld.PositionToHex(center);
            Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(hex);
            MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
            opacity = GetGridDataProterty(brushType, data);
        }
        public override void Modify(Vector3 center, MapEditor editor)
        {
            int value = opacity;
            if (Input.GetKey(KeyCode.LeftShift))
                value = InvertOpacity(brushType, opacity);

            // 记录变化前的数据
            List<GridEditCommand.GridDataChange> changes = new List<GridEditCommand.GridDataChange>();
            GridEditCommand.EditType editType = GridEditCommand.EditType.TerrainType;
            string description = "";

            switch (brushType)
            {
                case BrushType.TerrainType:
                    editType = GridEditCommand.EditType.TerrainType;
                    description = "修改地形类型";
                    break;
                case BrushType.Area:
                    editType = GridEditCommand.EditType.Area;
                    description = "修改区域";
                    break;
                case BrushType.Interior:
                    editType = GridEditCommand.EditType.Interior;
                    description = "修改内政";
                    break;
                case BrushType.Defence:
                    editType = GridEditCommand.EditType.Defence;
                    description = "修改防守";
                    break;
                case BrushType.Thief:
                    editType = GridEditCommand.EditType.Thief;
                    description = "修改贼";
                    break;
            }

            // 收集变化数据
            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);

                int oldValue = GetGridDataProterty(brushType, data);

                // 创建临时副本进行修改
                MapGrid.GridData tempData = data;
                SetGridDataProterty(brushType, tempData, (byte)value);
                int newValue = GetGridDataProterty(brushType, tempData);
                SetTerrainMaskShowColor(coord.col, coord.row, newValue, terrainTypeMaskCol, terrainTypeMaskRow);

                switch (brushType)
                {
                    case BrushType.TerrainType:
                        editor.map.mapGrid.BeginUpdateMovable(coord.col, coord.row);
                        break;
                }

                // 只有值发生变化时才记录
                if (oldValue != newValue)
                {
                    GridEditCommand.GridDataChange change = new GridEditCommand.GridDataChange();
                    change.col = coord.col;
                    change.row = coord.row;
                    change.oldValue = oldValue;
                    change.newValue = newValue;
                    changes.Add(change);
                }
            }

            terrainTypeMaskTex.Apply(false);
            editor.map.mapGrid.EndUpdateMovable();


            // 如果有变化，创建命令并执行
            if (changes.Count > 0)
            {
                GridEditCommand command = new GridEditCommand(editor, editType, changes, description, this);
                editor.undoRedoManager.AddCommand(command);
            }

        }

        CreatePool<Text> textPool;
        class TmpGridData
        {
            public int x;
            public int y;
            public Text text;
            public bool check;
            public GridData gridData;
            public bool needUpdate;
            bool _visible = false;
            public CreatePool<Text> Pool;
            public MapEditor mapEditor;

            public void Clear()
            {
                if (text != null)
                {
                    Pool.Recycle(text);
                    text = null;
                }
            }

            public void Update()
            {
                if (needUpdate)
                {
                    needUpdate = false;
                    bool has = false;
                    StringBuilder stringBuilder = new StringBuilder();
                    if (gridData.HasGridState(GridState.Defence))
                    {
                        stringBuilder.AppendLine("<color=#1100ff>防</color>");
                        has = true;
                    }
                    if (gridData.HasGridState(GridState.Interior))
                    {
                        stringBuilder.AppendLine("<color=#11ff00>内</color>");
                        has = true;
                    }
                    if (gridData.HasGridState(GridState.Thief))
                    {
                        stringBuilder.AppendLine("<color=#ff0011>贼</color>");
                        has = true;
                    }

                    TerrainType terrainType = Sango.Core.GameData.Instance.ScenarioCommonData.TerrainTypes.Get(gridData.terrainType);
                    if (terrainType == null)
                        terrainType = Sango.Core.GameData.Instance.ScenarioCommonData.TerrainTypes[0];

                    if (terrainType.moveable && has)
                    {
                        if (text == null)
                            text = Pool.Create();

                        Vector3 pos = MapRender.Instance.CoordsToPosition(x, y);
                        pos.y = pos.y + 2f;
                        text.transform.localPosition = pos;
                        text.text = stringBuilder.ToString();
                    }
                    else
                    {
                        Clear();
                    }
                }
            }

            public bool visible
            {
                set
                {
                    if (_visible != value)
                    {
                        _visible = value;
                        if (value)
                        {
                            needUpdate = true;
                            Update();
                        }
                        else
                        {
                            Clear();
                        }
                    }
                }
                get { return _visible; }
            }
        }
        List<TmpGridData> last;
        List<TmpGridData> temp1 = new List<TmpGridData>();
        List<TmpGridData> temp2 = new List<TmpGridData>();
        int switchIndex = 1;
        Transform textROOT;
        TmpGridData[][] tmpGridDatas;

        void ClearGridShow()
        {
            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    TmpGridData gridData = last[i];
                    gridData.visible = false;
                    gridData.check = false;
                    gridData.Clear();
                }
                last.Clear();
            }
            last = temp1;
            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    TmpGridData gridData = last[i];
                    gridData.visible = false;
                    gridData.check = false;
                    gridData.Clear();
                }
                last.Clear();
            }
            last = temp2;
            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    TmpGridData gridData = last[i];
                    gridData.visible = false;
                    gridData.check = false;
                    gridData.Clear();
                }
                last.Clear();
            }
            last = null;
        }

        void InitGridShow()
        {
            if (textROOT == null)
            {
                textROOT = GameObject.Find("GridTextRoot").transform;
            }

            GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("GridText")) as GameObject;
            obj.transform.SetParent(textROOT, false);
            UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            text.fontSize = 95;
            text.fontStyle = UnityEngine.FontStyle.Bold;
            Outline outline = text.AddComponent<Outline>();
            outline.effectDistance = new Vector2(3, -3);
            outline.effectColor = UnityEngine.Color.black;
            textPool = new CreatePool<Text>(text);
            obj.SetActive(false);

            MapGrid mapGrid = editor.map.mapGrid;
            tmpGridDatas = new TmpGridData[mapGrid.bounds.x][];
            for (int i = 0; i < tmpGridDatas.Length; i++)
                tmpGridDatas[i] = new TmpGridData[mapGrid.bounds.y];

            for (int x = 0; x < tmpGridDatas.Length; x++)
            {
                TmpGridData[] xRow = tmpGridDatas[x];
                for (int y = 0; y < xRow.Length; y++)
                {
                    xRow[y] = new TmpGridData()
                    {
                        x = x,
                        y = y,
                        gridData = mapGrid.GetGridData(x, y),
                        needUpdate = true,
                        Pool = textPool,
                        mapEditor = editor,
                    };
                }
            }
        }

        public void UpdateText(Tools.Rect rect)
        {
            if (!MapEditor.IsEditOn) return;
            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    last[i].check = false;
                }
            }

            List<TmpGridData> temp = switchIndex % 2 == 0 ? temp1 : temp2;
            Vector2Int lt = MapRender.Instance.PositionToCoords(rect.yMin, rect.xMin);
            Vector2Int rb = MapRender.Instance.PositionToCoords(rect.yMax, rect.xMax);
            StringBuilder stringBuilder = new StringBuilder();
            MapGrid mapGrid = editor.map.mapGrid;
            for (int x = lt.x; x <= rb.x; ++x)
            {
                for (int y = lt.y; y <= rb.y; ++y)
                {
                    if (x >= 0 && x < mapGrid.bounds.x && y >= 0 && y < mapGrid.bounds.y)
                    {

                        TmpGridData tmpGridData = tmpGridDatas[x][y];
                        tmpGridData.check = true;
                        tmpGridData.visible = true;
                        temp.Add(tmpGridData);
                        tmpGridData.Update();
                    }
                }
            }

            if (last != null)
            {
                for (int i = 0; i < last.Count; i++)
                {
                    TmpGridData gridData = last[i];
                    if (!gridData.check)
                    {
                        gridData.visible = false;
                        gridData.Clear();
                    }
                }
                last.Clear();
            }
            if (switchIndex == 1)
                switchIndex = 2;
            else
                switchIndex = 1;
            last = temp;
        }

    }

}