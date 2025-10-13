using UnityEngine;
using System.IO;
using System;
using HSVPicker;

using System.Drawing;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Sango.Game;
using System.Collections;

namespace Sango.Tools
{
    public class GridBrush : BrushBase
    {
        public enum BrushType : int
        {
            TerrainType,
            //Area,
            //Trap,
            //Dir,
            //Interior,
            //Defence,
            //Thief,
            //Flood,
            //Ruins,
            Unknown,
        }
        public int size = 1;
        public int opacity;

        //private string[] toolbarTitle = new string[] { "无", "可行走编辑", "地格类型刷", "地格信息编辑" };
        private string[] toolbarTitle = new string[] {
            "无",
            "类型",
            //"区域",
            ////"lpB",
            //"陷阱",
            //"方向",
            //"内政",
            //"防守",
            //"贼",
            //"水淹",
            ////"种类?",
            //"遗迹"
        };
        private int currentEditMode = 0;
        public BrushType brushType = BrushType.Unknown;
        public string[] terrainTypeTexNames = new string[] {
            "editor_terrain_type",
            //"editor_area_type",
            //"editor_trap_type",
            //"editor_dir_type",
            //"editor_interior_type",
            //"editor_defence_type",
            //"editor_thief_type",
            //"editor_flood_type",
            //"editor_ruins_type",
        };
        public Texture[] terrainTypeTexes = new Texture[] {
            Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
            //Texture2D.whiteTexture,
        };
        public Texture2D terrainTypeTex;

        public Texture2D terrainTypeMaskTex;
        public int terrainTypeMaskCol = 4;
        public int terrainTypeMaskRow = 8;
        public bool showTerrainType = false;
        private bool showGrid = true;
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

            Game.Game.Instance.StartCoroutine(CreateLayerTexture());
        }

        // 准备图层贴图
        IEnumerator CreateLayerTexture()
        {
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
                text.color = terrainType.color;
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
                    for(int y = 0; y < texture2D.height; ++y)
                    {
                        terrainTypeTex.SetPixel(xStart + x, yStart + y, pixels[x + y * texture2D.width]);
                    }

                GameObject.DestroyImmediate(texture2D);

            }
            RenderTexture.ReleaseTemporary(renderTexture);
            terrainTypeTex.Apply();
            Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);
            image[1].texture = terrainTypeTex;
            GameObject.Destroy(texCreator);
            yield return null;
        }

        public override void OnEnter()
        {
            Shader.SetGlobalFloat("_terrainTypeAlpha", 1);
            for (int i = 0; i < terrainTypeTexNames.Length; ++i)
            {
                terrainTypeTexes[i] = editor.map.CreateTexture("Editor/" + terrainTypeTexNames[i]);
                if (i == (int)brushType)
                    UpdateTerrainMaskTex();
            }
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
            int col = index % colCount;
            int row = index / colCount;
            row = rowCount - 1 - row;
            UnityEngine.Color c = new UnityEngine.Color((float)col / (float)terrainTypeMaskCol, (float)row / (float)terrainTypeMaskRow, 0);
            y = terrainTypeMaskTex.height - y - 1;
            terrainTypeMaskTex.SetPixel(x, y, c);
        }
        public byte GetGridDataProterty(BrushType brushType, MapGrid.GridData data)
        {
            switch (brushType)
            {
                case BrushType.TerrainType:
                    return data.terrainType;
                    //case BrushType.Area:
                    //    return data.areaId;
                    //case BrushType.Trap:
                    //    return data.trap;
                    //case BrushType.Dir:
                    //    return data.dir;
                    //case BrushType.Interior:
                    //    return data.interior;
                    //case BrushType.Defence:
                    //    return data.defence;
                    //case BrushType.Thief:
                    //    return data.thief;
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
                    //case BrushType.Area:
                    //    data.areaId = value;
                    //    break;
                    //case BrushType.Trap:
                    //    data.trap = value;
                    //    break;
                    //case BrushType.Dir:
                    //    data.dir = value;
                    //    break;
                    //case BrushType.Interior:
                    //    data.interior = value;
                    //    break;
                    //case BrushType.Defence:
                    //    data.defence = value;
                    //    break;
                    //case BrushType.Thief:
                    //    data.thief = value;
                    //    break;
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
            if (terrainTypeMaskTex == null)
            {
                terrainTypeMaskTex = new Texture2D(editor.map.mapGrid.bounds.x, editor.map.mapGrid.bounds.y, TextureFormat.ARGB32, false);
                terrainTypeMaskTex.wrapMode = TextureWrapMode.Clamp;
                terrainTypeMaskTex.filterMode = FilterMode.Point;
            }

            switch (b)
            {
                case BrushType.TerrainType:
                    {
                        //terrainTypeMaskCol = 4;
                        //terrainTypeMaskRow = 8;
                    }
                    break;
                    //case BrushType.Area:
                    //    {
                    //        terrainTypeMaskCol = 16;
                    //        terrainTypeMaskRow = 16;
                    //    }
                    //    break;
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
                    //case BrushType.Interior:
                    //case BrushType.Defence:
                    //case BrushType.Thief:
                    //case BrushType.Flood:
                    //case BrushType.Ruins:
                    //    {
                    //        terrainTypeMaskCol = 2;
                    //        terrainTypeMaskRow = 1;
                    //    }
                    //    break;
            }
            //Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTexes[(int)b]);
            Shader.SetGlobalTexture("_TerrainTypeTex", terrainTypeTex);

            Shader.SetGlobalFloat("_terrainTypeMaskCol", terrainTypeMaskCol);
            Shader.SetGlobalFloat("_terrainTypeMaskRow", terrainTypeMaskRow);

            for (int i = 0; i < editor.map.mapGrid.bounds.x; ++i)
            {
                for (int j = 0; j < editor.map.mapGrid.bounds.y; ++j)
                {
                    MapGrid.GridData data = editor.map.mapGrid.GetGridData(i, j);
                    SetTerrainMaskShowColor(i, j, GetGridDataProterty(b, data), terrainTypeMaskCol, terrainTypeMaskRow);
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
        //void Load311GridData()
        //{
        //    string[] path = WindowDialog.OpenFileDialog("地格文件(*.SHEX)|*.SHEX\0");
        //    if (path != null)
        //    {
        //        string fName = path[0];
        //        editor.map.mapGrid.LoadFrom311GridData(fName);
        //        UpdateTerrainMaskTex();
        //    }
        //}

        //void SaveTo311GridData()
        //{
        //    string path = WindowDialog.SaveFileDialog("4791.SHEX", "地格文件(*.SHEX)|*.SHEX\0");
        //    if (path != null)
        //    {
        //        editor.map.mapGrid.SaveTo311GridData(path);
        //    }
        //}

        public override void OnGUI()
        {
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size));
            float _size = GUILayout.HorizontalSlider(size, 0f, 12f);
            if ((int)_size != size)
            {
                size = (int)_size;
                OnBrushSizeChange();
            }

            //int _opacity = EditorUtility.IntField(opacity, "笔刷值");
            //if (_opacity != opacity) {
            //    opacity = _opacity;
            //}

            //GUILayout.BeginHorizontal();
            //if (GUILayout.Button("加载311地格数据"))
            //{
            //    Load311GridData();
            //}
            //if (GUILayout.Button("保存为311地格数据"))
            //{
            //    SaveTo311GridData();
            //}
            //GUILayout.EndHorizontal();


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
                //        //int state = GUILayout.SelectionGrid(opacity, moveStateTitle);
                //        //if (state != opacity) {
                //        //    moveState = state;
                //        //}
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
                        }
                        else if ((Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButton(0)) || Input.GetMouseButtonDown(0))
                        {
                            Modify(hit.point, editor);
                            lastCenter = hit.point;
                        }
                    }
                    DrawGizmos(hit.point);
                }
            }
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
        void SuckValue(Vector3 center, MapEditor editor)
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

            for (int i = 0; i < tempHexList.Count; i++)
            {
                Sango.Hexagon.Hex h = tempHexList[i];
                Sango.Hexagon.Coord coord = Sango.Hexagon.Coord.OffsetFromCube(h);
                MapGrid.GridData data = editor.map.mapGrid.GetGridData(coord.col, coord.row);
                SetGridDataProterty(brushType, data, (byte)value);
                SetTerrainMaskShowColor(coord.col, coord.row, GetGridDataProterty(brushType, data), terrainTypeMaskCol, terrainTypeMaskRow);
                if (brushType == BrushType.TerrainType)
                {
                    editor.map.mapGrid.BeginUpdateMovable(coord.col, coord.row);
                }
            }
            if (brushType == BrushType.TerrainType)
                editor.map.mapGrid.EndUpdateMovable();

            terrainTypeMaskTex.Apply(false);
        }

    }

}