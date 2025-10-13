using UnityEngine;
using System.IO;
using System;
using HSVPicker;
using System.Drawing;
using Sango.Render;
using Sango;
using System.Collections.Generic;
using DG.Tweening.Plugins.Core.PathCore;
using Sango.Game;

namespace Sango.Tools
{

    public class TerrainBrush : BrushBase
    {
        public enum BrushType : int
        {
            RaiseHeight = 0,
            LowerHeight,
            PullHeight,
            SmoothHeight,
            Texture,
            Water,
            BaseMap,
            Unknown,
        }
        public float size = 5f;
        public float opacity;
        private string[] toolbarTitle = new string[] { "升高", "降低", "平整", "平滑", "贴图", "水面", "底色" };
        private int currentEditMode = 0;
        public Texture[] brushTexture;
        public BrushType brushType = BrushType.Unknown;
        private int textureIndex = 0;
        private Vector2 scrollPos;
        private RenderTexture[] baseMap;
        private int brushIndex = 0;
        private Material brushMat;
        private Material blitMat;
        private Vector2 mapSize;
        private ColorPicker picker;
        private UnityEngine.Color brushColor;
        EditorWindow contentWindow;
        UnityEngine.Rect InitWindowRect = new UnityEngine.Rect(0, 0, 100, 100);
        public TerrainBrush(MapEditor e) : base(e)
        {
            if (brushType == BrushType.Unknown)
                brushType = BrushType.RaiseHeight;

           
        }
        public override void OnEnter()
        {
            //创建笔刷贴图
            List<Texture> brush_texturs = new List<Texture>();
            for (int i = 0; i < 100; i++)
            {
                Texture tex = editor.map.CreateTexture($"Brush/brush_{i}.png");
                if (tex != Texture2D.whiteTexture)
                {
                    brush_texturs.Add(tex);
                }
                else
                    break;
            }

            brushTexture = brush_texturs.ToArray();

            Shader.SetGlobalFloat("_terrainTypeAlpha", gridInfoAlpha);
            base.OnEnter();
            if (contentWindow == null)
            {
                contentWindow = EditorWindow.AddWindow(1000, InitWindowRect, DrawContentWindow, "");
            }

            if (brushType == BrushType.BaseMap || brushType == BrushType.Texture)
            {
                contentWindow.windowRect.size = InitWindowRect.size;
                contentWindow.visible = true;
            }
            else
                contentWindow.visible = false;
        }
        float gridInfoAlpha = 1;
        void DrawContentWindow(int winId, EditorWindow window)
        {
            switch (brushType)
            {
                case BrushType.BaseMap:
                    {
                        GUILayout.Label(baseMap[editor.map.curSeason], GUILayout.Width(256), GUILayout.Height(256));
                    }
                    break;
                case BrushType.Texture:
                    {

                        GUILayout.Label("地格信息透明度");
                        float _alpha = GUILayout.HorizontalSlider(gridInfoAlpha, 0f, 1f);
                        if (_alpha != gridInfoAlpha)
                        {
                            gridInfoAlpha = _alpha;
                            Shader.SetGlobalFloat("_terrainTypeAlpha", gridInfoAlpha);
                        }

                        MapLayer.LayerData data = editor.map.mapLayer.GetLayer(textureIndex);
                        if (data != null)
                        {
                            GUILayout.Label(data.GetDiffuseName(editor.map.curSeason));
                            GUILayout.Label(data.GetDiffuse(editor.map.curSeason), GUILayout.Width(128), GUILayout.Height(128));
                        }

                        EditorUIDraw.OnGUI(editor.map.mapLayer);

                        if (textureIndex != EditorUIDraw.selectLayer)
                        {
                            textureIndex = EditorUIDraw.selectLayer;
                        }
                    }
                    break;

            }
        }

        public override void OnBrushSizeChange()
        {
            Shader.SetGlobalFloat("_BrushSize", mapSize.x / size);
        }
        public Rect GetBounds(Vector3 center)
        {
            return new Rect(new Vector2(center.z - size, center.x - size), new Vector2(size * 2, size * 2));
        }

        public override void OnSeasonChanged(int curSeason)
        {
            if (brushType == BrushType.BaseMap)
            {
                if (baseMap == null)
                {
                    baseMap = new RenderTexture[4];
                }

                if (baseMap[curSeason] == null)
                {
                    baseMap[curSeason] = CreateBaseTextrue();
                }

            }
        }

        public RenderTexture CreateBaseTextrue()
        {
            int curSeason = editor.map.curSeason;
            int width = Math.Min(4096, editor.map.mapData.vertex_width);
            int height = Math.Min(4096, editor.map.mapData.vertex_height);
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.ARGB32, 0);
            Texture t = editor.map.mapBaseColor.texture[curSeason];
            if (t.width != width || t.height != height)
            {
                //int w = Math.Min(t.width, width);
                //int h = Math.Min(t.height, height);
                //blitMat.SetTexture("_MainTex", editor.map.mapBaseColor.texture[curSeason]);
                ////blitMat.SetFloat("_BrushSize", mapSize.x / editor.map.mapBaseColor.texture[curSeason].width);
                //Vector2 scale = new Vector2(2, 1);
                //blitMat.SetTextureScale("_MainTex", scale);
                //UnityEngine.Graphics.Blit(editor.map.mapBaseColor.texture[curSeason], rt, blitMat);
                UnityEngine.Graphics.Blit(editor.map.mapBaseColor.texture[curSeason], rt);
            }
            else
            {
                UnityEngine.Graphics.Blit(editor.map.mapBaseColor.texture[curSeason], rt);

            }
            editor.map.mapBaseColor.texture[curSeason] = rt;
            return rt;
        }

        public override void OnBrushTypeChange()
        {
            if (brushType == BrushType.BaseMap || brushType == BrushType.Texture)
            {
                contentWindow.windowRect.size = InitWindowRect.size;
                contentWindow.visible = true;
            }
            else
                contentWindow.visible = false;

            if (brushType == BrushType.BaseMap)
            {
                size = 15;
                if (baseMap == null)
                {
                    baseMap = new RenderTexture[4];
                }
                // blitMat = new Material(Shader.Find("Sango/blit"));
                if (baseMap[editor.map.curSeason] == null)
                {
                    baseMap[editor.map.curSeason] = CreateBaseTextrue();
                }

                Shader.SetGlobalTexture("_BaseTex", baseMap[editor.map.curSeason]);
                brushMat = new Material(Shader.Find("Sango/brush"));

                mapSize = new Vector2(editor.mapData.vertex_width * editor.mapData.quadSize, editor.mapData.vertex_height * editor.mapData.quadSize);

                if (picker == null)
                {
                    GameObject obj = GameObject.Instantiate(Resources.Load("Picker")) as GameObject;
                    if (obj != null)
                    {
                        picker = obj.GetComponentInChildren<ColorPicker>(true);
                        if (picker != null)
                        {
                            picker.onValueChanged.AddListener(color =>
                            {
                                brushColor = color;
                                Shader.SetGlobalColor("_BrushColor", brushColor);
                            });
                        }
                    }
                }
                else
                {
                    if (picker != null)
                        picker.gameObject.SetActive(true);
                }
                Shader.SetGlobalFloat("_BrushType", 1);
                Shader.SetGlobalFloat("_BrushSize", mapSize.x / size);
                Shader.SetGlobalTexture("_BrushTex", brushTexture[brushIndex]);
            }
            else
            {
                Shader.SetGlobalFloat("_BrushType", 0);
                if (picker != null)
                    picker.gameObject.SetActive(false);
            }
        }


        /// <summary>
        /// 吸取目标值
        /// </summary>
        /// <param name="center"></param>
        /// <param name="editor"></param>
        bool SuckValue(Vector3 center, MapEditor editor)
        {
            if (brushType == BrushType.PullHeight && Input.GetKey(KeyCode.LeftAlt))
            {
                opacity = (int)(center.y * 2 + 0.5f);
                return true;
            }

            // 吸取
            if (brushType == BrushType.Water && Input.GetKey(KeyCode.LeftAlt))
            {
                opacity = editor.mapData.GetVertexData(center.z, center.x).water;
                return true;
            }

            return false;
        }

        public override void Modify(Vector3 center, MapEditor editor)
        {
            switch (brushType)
            {
                case BrushType.RaiseHeight:
                case BrushType.LowerHeight:
                case BrushType.PullHeight:
                case BrushType.SmoothHeight:
                case BrushType.Texture:
                case BrushType.Water:
                    {

                        // 吸取目标值
                        if (SuckValue(center, editor))
                            return;

                        int xStart = Mathf.FloorToInt(center.z - size) / editor.mapData.quadSize;
                        int yStart = Mathf.FloorToInt(center.x - size) / editor.mapData.quadSize;
                        Vector3 cPos = center;
                        int length = Mathf.FloorToInt(size * 2 / editor.mapData.quadSize) + 1;
                        int xEnd = xStart + length;
                        int yEnd = yStart + length;
                        for (int x = xStart; x < xEnd; x++)
                            for (int y = yStart; y < yEnd; y++)
                            {
                                if (x >= 0 && x <= editor.mapData.vertex_width && y >= 0 && y <= editor.mapData.vertex_height)
                                {
                                    MapData.VertexData vertexData = editor.vertexMapData[x][y];
                                    if (Do(cPos, ref vertexData, x, y))
                                    {
                                        //Vector3 normal = editor.map.mapData.VertexNormal(vertexData, x, y);
                                        //vertexData.normal = normal;
                                        editor.vertexMapData[x][y] = vertexData;
                                    }
                                }
                            }


                        // 判断哪些cell需要重新刷新
                        Rect rect = GetBounds(center);
                        for (int i = 0; i < editor.map.mapTerrain.terrainCells.Length; i++)
                        {
                            MapCell cell = editor.map.mapTerrain.terrainCells[i];
                            if (cell != null)
                            {
                                if (cell.Overlaps(rect))
                                {
                                    cell.PrepareDatas();
                                }
                            }
                        }


                    }
                    break;
                case BrushType.BaseMap:
                    {
                        UnityEngine.Graphics.Blit(Texture2D.whiteTexture, baseMap[editor.map.curSeason], brushMat);
                    }
                    break;
                default:
                    {
                    }
                    break;
            }
        }

        public virtual bool Do(Vector3 center, ref MapData.VertexData vertexData, int x, int y)
        {
            float centerY = center.y;
            center.y = 0;
            Vector3 vPos = editor.map.mapData.VertexPosition(vertexData, x, y);
            vPos.y = 0;
            float distance = Vector3.Distance(vPos, center);
            if (distance <= size)
            {
                switch (brushType)
                {
                    case BrushType.RaiseHeight:
                        {
                            int destHeight = Mathf.FloorToInt(opacity * (size - distance) / size) + vertexData.height;
                            if (destHeight > 255)
                            {
                                destHeight = 255;
                            }
                            vertexData.height = (byte)destHeight;
                        }
                        break;
                    case BrushType.LowerHeight:
                        {
                            int destHeight = Mathf.FloorToInt(-opacity * (size - distance) / size) + vertexData.height;
                            if (destHeight < 0)
                            {
                                destHeight = 0;
                            }
                            vertexData.height = (byte)destHeight;
                        }
                        break;
                    case BrushType.PullHeight:
                        {

                            int destHeight;
                            if (Input.GetKey(KeyCode.LeftShift))
                            {
                                destHeight = (int)(centerY * 2 + 0.5f);
                            }
                            else if (Input.GetKey(KeyCode.LeftAlt))
                            {
                                destHeight = (int)(centerY * 2 + 0.5f);
                                opacity = destHeight;
                                return false;
                            }
                            else
                            {
                                destHeight = (int)opacity;
                            }
                            if (destHeight > 255)
                            {
                                destHeight = 255;
                            }
                            else if (destHeight < 0)
                            {
                                destHeight = 0;
                            }
                            vertexData.height = (byte)destHeight;
                        }
                        break;
                    case BrushType.SmoothHeight:
                        {
                            int x_start = x - 1;
                            int y_start = y - 1;
                            int totalHeight = 0;
                            for (int i = x_start; i <= x + 1; i++)
                            {
                                for (int j = y_start; j <= y + 1; j++)
                                {
                                    if (i >= 0 && i < editor.map.mapData.vertexDatas.Length)
                                    {
                                        MapData.VertexData[] xSet = editor.map.mapData.vertexDatas[i];
                                        if (j >= 0 && j < xSet.Length)
                                        {
                                            MapData.VertexData neighbor = xSet[j];
                                            totalHeight += neighbor.height;
                                        }
                                    }
                                }
                            }

                            int destHeight = totalHeight / 9;
                            if (destHeight > 255)
                            {
                                destHeight = 255;
                            }
                            else if (destHeight < 0)
                            {
                                destHeight = 0;
                            }
                            vertexData.height = (byte)destHeight;
                        }
                        break;
                    case BrushType.Texture:
                        {
                            vertexData.textureIndex = (byte)textureIndex;
                        }
                        break;
                    case BrushType.Water:
                        {
                            int destHeight = (int)opacity;
                            if (destHeight > 255)
                            {
                                destHeight = 255;
                            }
                            else if (destHeight < 0)
                            {
                                destHeight = 0;
                            }
                            vertexData.water = (byte)destHeight;
                        }
                        break;
                    default:
                        {
                        }
                        break;
                }
                return true;
            }
            return false;
        }

        int NameSort(string a, string b)
        {
            if (a.Length == b.Length)
                return a.CompareTo(b);
            else
            {
                if (a.Length > b.Length)
                    return 1;
                else
                    return -1;
            }
        }

        public override void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size), GUILayout.Width(80));
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            if (brushType == BrushType.BaseMap)
            {
                float _size = GUILayout.HorizontalSlider(size, 15f, 150f);
                if (_size != size)
                {
                    size = _size;
                    OnBrushSizeChange();
                }
            }
            else
            {
                float _size = GUILayout.HorizontalSlider(size, 5f, 100f);
                if (_size != size)
                {
                    size = _size;
                    OnBrushSizeChange();
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("笔刷强度", GUILayout.Width(80));
            float v = EditorUtility.FloatField(opacity, GUILayout.MaxWidth(32));
            if (GUI.changed)
            {
                opacity = v;
                if (opacity < 0)
                    opacity = 0;
                if (opacity > 255)
                    opacity = 255;
            }

            GUILayout.BeginVertical();
            GUILayout.Space(8);
            float _opacity = GUILayout.HorizontalSlider(opacity, 0f, 255f);
            if (_opacity != opacity)
            {
                opacity = _opacity;
                OnBrushOpacityChange();
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int editMode = GUILayout.SelectionGrid(currentEditMode, toolbarTitle, 4, GUILayout.Height(60));
            if (editMode != currentEditMode)
            {
                currentEditMode = editMode;
                brushType = (BrushType)currentEditMode;
                OnBrushTypeChange();
            }
            GUI.backgroundColor = lastColor;
            switch (brushType)
            {
                case BrushType.RaiseHeight:
                case BrushType.LowerHeight:
                case BrushType.PullHeight:
                case BrushType.SmoothHeight:
                    {
                        if (GUILayout.Button("保存高度数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("height.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {

#if UNITY_STANDALONE_WIN

                                using (Bitmap bmp24 = new Bitmap(editor.map.mapData.vertex_width + 1, editor.map.mapData.vertex_height + 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                                //using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp24))
                                {
                                    MapData.VertexData[][] vertexDatas = editor.map.mapData.vertexDatas;
                                    for (int x = 0; x < vertexDatas.Length; x++)
                                    {
                                        MapData.VertexData[] yTable = vertexDatas[x];
                                        for (int y = 0; y < yTable.Length; y++)
                                        {
                                            MapData.VertexData data = yTable[y];
                                            int h = 255 - data.height;
                                            bmp24.SetPixel(x, y, System.Drawing.Color.FromArgb(h, h, h));
                                        }
                                    }

                                    bmp24.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
                                }
#endif
                                //Bitmap bitmapSrc = new Bitmap(path);//获取的位图大小
                                //bitmapSrc.Save(bmpPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                        }
                    }
                    break;
                case BrushType.Texture:
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("自动加载图层信息"))
                        {
                            AutoImportLayerTexture();
                        }

                        if (GUILayout.Button("保存图层数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("layer.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN

                                using (Bitmap bmp24 = new Bitmap(editor.map.mapData.vertex_width + 1, editor.map.mapData.vertex_height + 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                                //using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp24))
                                {
                                    MapData.VertexData[][] vertexDatas = editor.map.mapData.vertexDatas;
                                    for (int x = 0; x < vertexDatas.Length; x++)
                                    {
                                        MapData.VertexData[] yTable = vertexDatas[x];
                                        for (int y = 0; y < yTable.Length; y++)
                                        {
                                            MapData.VertexData data1 = yTable[y];
                                            Color32 c = MapData.get_layer_color(data1.textureIndex);
                                            bmp24.SetPixel(x, y, System.Drawing.Color.FromArgb(c.r, c.g, c.b));
                                        }
                                    }

                                    bmp24.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
                                }
#endif
                                //Bitmap bitmapSrc = new Bitmap(path);//获取的位图大小
                                //bitmapSrc.Save(bmpPath, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                        }
                        GUILayout.EndHorizontal();

                        //MapLayer.LayerData data = editor.map.mapLayer.GetLayer(textureIndex);
                        //if (data != null)
                        //{
                        //    GUILayout.Label(data.GetDiffuseName(editor.map.curSeason));
                        //    GUILayout.Label(data.GetDiffuse(editor.map.curSeason), GUILayout.Width(128), GUILayout.Height(128));
                        //}

                        //EditorUIDraw.OnGUI(editor.map.mapLayer);

                        //if (textureIndex != EditorUIDraw.selectLayer)
                        //{
                        //    textureIndex = EditorUIDraw.selectLayer;
                        //}
                    }
                    break;
                case BrushType.Water:
                    {
                        if (GUILayout.Button("保存水数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("water.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN

                                using (Bitmap bmp24 = new Bitmap(editor.map.mapData.vertex_width, editor.map.mapData.vertex_height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                                //using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp24))
                                {
                                    MapData.VertexData[][] vertexDatas = editor.map.mapData.vertexDatas;
                                    for (int x = 0; x < editor.map.mapData.vertex_width; x++)
                                    {
                                        MapData.VertexData[] yTable = vertexDatas[x];
                                        for (int y = 0; y < editor.map.mapData.vertex_height; y++)
                                        {
                                            MapData.VertexData data = yTable[y];
                                            int h = 255 - data.water;
                                            bmp24.SetPixel(x, y, System.Drawing.Color.FromArgb(h, h, h));
                                        }
                                    }

                                    bmp24.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
                                }
#endif
                            }
                        }
                    }
                    break;
                case BrushType.BaseMap:
                    {
                        Texture[] textures = brushTexture;
                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button("重置编辑"))
                        {
                            int season = (int)editor.map.curSeason;
                            RenderTexture.ReleaseTemporary(baseMap[season]);
                            baseMap[season] = CreateBaseTextrue();
                            Shader.SetGlobalTexture("_BaseTex", baseMap[season]);
                        }
                        if (GUILayout.Button("加载"))
                        {
#if UNITY_STANDALONE_WIN

                            Tools.EditorUtility.OpenTexture("贴图文件(*.png)|*.png", editor.map.curSeason, (string fileName, UnityEngine.Object obj, object customData) =>
#else
                            Tools.EditorUtility.OpenTexture("贴图文件(*.png)|*.png", editor.map.curSeason, (string fileName, UnityEngine.Object obj, object customData) =>
#endif

                            {
                                int season = (int)customData;
                                //editor.map.mapBaseColor.baseTextrueName[season] = System.IO.Path.GetFileNameWithoutExtension(fileName);
                                editor.map.mapBaseColor.texture[season] = obj as Texture;
                                RenderTexture.ReleaseTemporary(baseMap[season]);
                                baseMap[season] = CreateBaseTextrue();
                                Shader.SetGlobalTexture("_BaseTex", baseMap[season]);
                            });
                        }

                        if (GUILayout.Button("保存"))
                        {
                            int i = editor.map.curSeason;
                            string path = WindowDialog.SaveFileDialog("BaseMap" + i + ".png", "贴图文件(*.png)|*.png\0");
                            if (path != null)
                            {
                                SaveBaseTexture(path, i);
                            }
                        }
                        GUILayout.EndHorizontal();
                        //GUILayout.Label(textures[brushIndex], GUILayout.Width(128), GUILayout.Height(128));

                        //scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(256), GUILayout.Height(256));
                        int brushRow = textures.Length > 0 ? (textures.Length - 1) / 4 + 1 : 1;
                        int sel = GUILayout.SelectionGrid(brushIndex, textures, 4, GUILayout.MaxWidth(224), GUILayout.MaxHeight(brushRow * 56));
                        if (sel != brushIndex)
                        {
                            brushIndex = sel;
                            Shader.SetGlobalTexture("_BrushTex", brushTexture[brushIndex]);
                        }

                        //GUILayout.EndScrollView();

                        //GUILayout.Label(baseMap[editor.map.curSeason], GUILayout.Width(200), GUILayout.Height(200));

                    }
                    break;
                default:
                    {
                    }
                    break;
            }
        }


        public override void DrawGizmos(Vector3 center)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 1);
            }
            else if (Input.GetKeyUp(KeyCode.Space))
            {
                Shader.SetGlobalFloat("_TerrainTypeShowFlag", 0);
            }

            if (brushType == BrushType.BaseMap)
            {
                if (Input.GetKey(KeyCode.RightBracket))
                {
                    size += 0.3f;
                    if (size > 150f)
                        size = 150f;
                    OnBrushSizeChange();
                }
                else if (Input.GetKey(KeyCode.LeftBracket))
                {
                    size -= 0.3f;
                    if (size < 15f)
                        size = 15f;
                    OnBrushSizeChange();
                }
            }
            else
            {
                if (Input.GetKey(KeyCode.RightBracket))
                {
                    size += 0.3f;
                    if (size > 100f)
                        size = 100f;
                    OnBrushSizeChange();
                }
                else if (Input.GetKey(KeyCode.LeftBracket))
                {
                    size -= 0.3f;
                    if (size < 5)
                        size = 5;
                    OnBrushSizeChange();
                }
            }

            if (brushType == BrushType.BaseMap)
            {
                if (Input.GetKey(KeyCode.LeftAlt))
                {
                    Shader.SetGlobalVector("_Brush", new Vector4(0, 0, 0, size));
                    Shader.SetGlobalVector("_BrushUV", new Vector4((0) / mapSize.x, (mapSize.y - 0) / mapSize.y, 1, 1));
                    if (picker != null)
                    {
                        if (!picker.isPickingColor)
                        {
                            picker.OnPickScreenColor();
                        }
                    }

                    if (Input.GetKeyDown(KeyCode.LeftAlt))
                    {
                        editor.map.mapModels.EditorShow(false);
                    }
                    return;
                }

                if (Input.GetKeyUp(KeyCode.LeftAlt))
                {
                    editor.map.mapModels.EditorShow(true);
                }
            }
            else if (brushType == BrushType.Water)
            {

            }
            else if (brushType == BrushType.PullHeight)
            {

            }
            // else
            {
                Shader.SetGlobalVector("_Brush", new Vector4(center.x, center.y, center.z, size));
                Shader.SetGlobalVector("_BrushUV", new Vector2((center.z) / mapSize.x, (mapSize.y - center.x) / mapSize.y));
            }
        }

        public void AutoImportLayerTexture()
        {
            string[][] seasonfiles = new string[4][];
            int maxCount = 0;
            for (int j = 0; j < 4; ++j)
            { 
                seasonfiles[j] = new string[100];
                string seasonName = MapRender.SeasonNames[j];
                for (int i = 0; i < 100; i++)
                {
                    string file = ($"Terrain/{seasonName}/layer_{i}");
                    if (file != null)
                    {
                        maxCount = Math.Max(maxCount, (i + 1));
                        seasonfiles[j][i] = file;
                    }
                    else
                    {
                        seasonfiles[j][i] = "";
                    }
                }
            }

            int len = editor.map.mapLayer.layerDatas.Length;
            if (len < maxCount)
            {
                for (int j = len; j < maxCount; ++j)
                    editor.map.mapLayer.AddLayer();
            }

            for (int i = 0; i < editor.map.mapLayer.layerDatas.Length - 1; ++i)
            {
                MapLayer.LayerData data_layer = editor.map.mapLayer.layerDatas[i];
                for (int j = 0; j < 4; ++j)
                {
                    data_layer.diffuseTexName[j] = System.IO.Path.GetFileNameWithoutExtension(seasonfiles[j][i]);
                }
                data_layer.AutoLoadDiffuse();
            }

            int waterBegin = maxCount;

            // 处理水层
            seasonfiles = new string[4][];
            maxCount = 0;
            for (int j = 0; j < 4; ++j)
            {
                seasonfiles[j] = new string[100];
                string seasonName = MapRender.SeasonNames[j];
                for (int i = 0; i < 100; i++)
                {
                    string file = ($"Terrain/{seasonName}/water_{i}");
                    if (file != null)
                    {
                        maxCount = Math.Max(maxCount, (i + 1));
                        seasonfiles[j][i] = file;
                    }
                    else
                    {
                        seasonfiles[j][i] = "";
                    }
                }
            }

            for (int j = 0; j < maxCount; ++j)
                editor.map.mapLayer.AddLayer();

            for (int i = 0; i < maxCount; ++i)
            {
                MapLayer.LayerData data_layer = editor.map.mapLayer.layerDatas[waterBegin + i];
                for (int j = 0; j < 4; ++j)
                {
                    data_layer.diffuseTexName[j] = System.IO.Path.GetFileNameWithoutExtension(seasonfiles[j][i]);
                }
                data_layer.AutoLoadDiffuse();
            }

        }

        public void AutoLoadBaseTexture()
        {

        }

        public void AutoSaveBaseTexture()
        {

        }

        public void SaveBaseTexture(string fileDir)
        {
            for (int i = 0; i < baseMap.Length; i++)
            {
                string final_file_name = $"{fileDir}/BaseMap{i}.png";
                SaveBaseTexture(final_file_name, i);
            }
        }

        public void SaveBaseTexture(string fileName, int i)
        {
            RenderTexture renderTexture = baseMap[i];
            int width = renderTexture.width;
            int height = renderTexture.height;
            Texture2D texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            texture2D.Apply();

            UnityEngine.Color32[] colors = texture2D.GetPixels32();


            byte[] vs = texture2D.EncodeToPNG();
            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            fileStream.Write(vs, 0, vs.Length);
            fileStream.Dispose();
            fileStream.Close();
            RenderTexture.active = null;
            string bmpPath = fileName.Remove(fileName.Length - 4) + ".bmp";
#if UNITY_STANDALONE_WIN
            using (Bitmap bmp24 = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                for (int x = 0; x < width; ++x)
                    for (int y = 0; y < height; ++y)
                    {
                        UnityEngine.Color32 c = colors[(height - 1 - y) * width + x];
                        bmp24.SetPixel(x, y, System.Drawing.Color.FromArgb(c.r, c.g, c.b));
                    }

                bmp24.Save(bmpPath, System.Drawing.Imaging.ImageFormat.Bmp);
            }
#endif
        }
    }
}