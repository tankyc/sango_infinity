using HSVPicker;
using Sango.Render;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 地形笔刷类  继承BrushBase，用于地图地形编辑相关操作，例如改变地形高度、纹理、水面等不同类型的编辑功能
    /// </summary>
    public class TerrainBrush : BrushBase
    {
        /// <summary>
        /// 笔刷类型的枚举，定义了不同的地形编辑操作类型，如升高、降低、平整、平滑地形高度，处理纹理、水面以及基础地图相关操作等类型
        /// </summary>
        public enum BrushType : int
        {
            RaiseHeight = 0,    // 升高
            LowerHeight,        // 降低
            PullHeight,         // 平整
            SmoothHeight,       // 平滑
            Texture,            // 贴图
            Water,              // 水面
            BaseMap,            // 底色
            Unknown,       
        }
        /// <summary>
        /// 笔刷大小，用于控制地形编辑操作影响的范围大小
        /// </summary>
        public float size = 5f;
        /// <summary>
        /// 笔刷透明度，用于部分地形编辑操作中影响效果的强度等情况，取值范围是浮点数类型
        /// </summary>
        public float opacity;
        /// <summary>
        /// 工具栏标题数组，用于在界面上展示不同的编辑模式选项，方便用户直观选择对应的地形编辑操作类型
        /// </summary>
        private string[] toolbarTitle = new string[] { "升高", "降低", "平整", "平滑", "贴图", "水面", "底色" };
        /// <summary>
        /// 当前编辑模式的索引，对应着toolbarTitle数组中的某个元素，确定当前正在进行的地形编辑操作类型
        /// </summary>
        private int currentEditMode = 0;
        /// <summary>
        /// 笔刷纹理数组，存储了不同的纹理资源，用于在特定的地形编辑操作（如纹理相关操作）中使用
        /// </summary>
        public Texture[] brushTexture;
        /// <summary>
        /// 当前笔刷类型，从BrushType枚举中取值，决定了具体的地形编辑行为逻辑
        /// </summary>
        public BrushType brushType = BrushType.Unknown;
        /// <summary>
        /// 当前纹理索引，用于在纹理相关操作中确定当前使用的是哪个纹理，对应brushTexture数组中的元素索引
        /// </summary>
        private int textureIndex = 0;
        /// <summary>
        /// 滚动位置，用于在界面上有滚动视图需求时记录滚动的位置信息，方便展示较多内容
        /// </summary>
        private Vector2 scrollPos;
        /// <summary>
        /// 基础地图渲染纹理数组，用于存储不同季节对应的基础地图纹理数据，方便在基础地图相关编辑操作中使用
        /// </summary>
        private RenderTexture[] baseMap;
        /// <summary>
        /// 当前笔刷索引，用于在选择笔刷纹理等操作中确定当前使用的笔刷纹理在数组中的位置索引
        /// </summary>
        private int brushIndex = 0;
        /// <summary>
        /// 笔刷材质，用于在地形编辑过程中与渲染等相关操作配合，影响笔刷绘制的效果等情况
        /// </summary>
        private Material brushMat;
        /// <summary>
        /// 用于复制纹理的材质，在纹理相关的处理（如复制、转移纹理数据等操作）中发挥作用
        /// </summary>
        private Material blitMat;
        /// <summary>
        /// 地图尺寸，用于确定地图在水平方向和垂直方向的大小，可能影响笔刷与地图的比例关系等计算
        /// </summary>
        private Vector2 mapSize;
        /// <summary>
        /// 颜色选择器，用于在特定编辑操作（如基础地图编辑时选择颜色等情况）中让用户选择颜色，以应用到地形编辑中
        /// </summary>
        private ColorPicker picker;
        /// <summary>
        /// 笔刷颜色，用于记录当前笔刷所使用的颜色，由颜色选择器等操作来设定，并应用到相应的地形编辑中
        /// </summary>
        private UnityEngine.Color brushColor;
        /// <summary>
        /// 内容窗口，用于在界面上展示与地形编辑相关的特定内容，如基础地图、纹理等信息的显示窗口
        /// </summary>
        EditorWindow contentWindow;
        /// <summary>
        /// 初始化窗口矩形，定义了内容窗口最初的位置和大小，用于初始化窗口相关属性
        /// </summary>
        UnityEngine.Rect InitWindowRect = new UnityEngine.Rect(0, 0, 100, 100);

        /// <summary>
        /// 初始化TerrainBrush类的实例，根据条件初始化笔刷类型，创建笔刷贴图等资源，加载对应的纹理文件到笔刷纹理数组中
        /// </summary>
        /// <param name="e">地图编辑器实例，用于与地图相关功能进行交互，获取地图数据等操作</param>
        public TerrainBrush(MapEditor e) : base(e)
        {
            if (brushType == BrushType.Unknown)
                brushType = BrushType.RaiseHeight;

           
        }
        /// <summary>
        /// 处理进入笔刷模式时的逻辑，设置全局的地形类型透明度，调用基类的OnEnter方法，根据笔刷类型控制内容窗口的显示与隐藏等操作
        /// </summary>
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

        /// <summary>
        /// 绘制内容窗体，根据笔刷类型展示不同的信息，如基础地图纹理、纹理信息及透明度调整等相关内容
        /// </summary>
        /// <param name="winId">窗口的唯一标识符，用于区分不同的窗口等情况</param>
        /// <param name="window">要绘制内容的窗口实例，通过此实例进行具体的界面绘制操作</param>
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

        /// <summary>
        /// 处理笔刷大小改变时的逻辑，设置全局的笔刷大小相关参数，用于影响地形编辑中笔刷作用范围相关的渲染等操作
        /// </summary>
        public override void OnBrushSizeChange()
        {
            Shader.SetGlobalFloat("_BrushSize", mapSize.x / size);
        }

        /// <summary>
        /// 根据给定的中心点坐标获取对应的边界矩形，用于确定笔刷影响的大致区域范围，方便后续计算和判断等操作
        /// </summary>
        /// <param name="center">地图中的中心点坐标，作为计算边界矩形的参考位置</param>
        /// <returns>返回一个Rect类型的边界矩形，定义了笔刷影响的区域范围</returns>
        public Rect GetBounds(Vector3 center)
        {
            return new Rect(new Vector2(center.z - size, center.x - size), new Vector2(size * 2, size * 2));
        }

        /// <summary>
        /// 处理季节改变时的逻辑，若笔刷类型为基础地图，则根据当前季节情况创建或获取对应的基础地图纹理数据
        /// </summary>
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
                    baseMap[curSeason] = CreateBaseTexture();
                }

            }
        }

        /// <summary>
        /// 创建基础贴图，根据当前地图的季节、尺寸等信息获取或生成对应的基础地图纹理，用于基础地图相关的地形编辑操作
        /// </summary>
        /// <returns>返回创建好的基础贴图对应的RenderTexture实例，用于后续的渲染等操作</returns>
        public RenderTexture CreateBaseTexture()
        {
            // 获取当前季节信息
            int curSeason = editor.map.curSeason;
            // 计算目标宽度，取地图数据顶点宽度和4096的较小值并加1
            int width = Math.Min(4096, editor.map.mapData.vertex_width) + 1;
            // int width = Math.Min(4096, editor.map.mapData.vertex_width) ; // 原版
            // 计算目标高度，取地图数据顶点高度和4096的较小值并加1
            int height = Math.Min(4096, editor.map.mapData.vertex_height) + 1; 
            // int height = Math.Min(4096, editor.map.mapData.vertex_height);// 原版
            // 获取临时的渲染纹理，用于存储基础贴图
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.ARGB32, 0);
            // 获取当前季节对应的基础颜色纹理
            Texture t = editor.map.mapBaseColor.texture[curSeason];
            // 如果尺寸匹配，直接返回当前季节的贴图
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

        /// <summary>
        /// 处理笔刷类型改变时的逻辑，根据brushType的不同，控制contentWindow的可见性，设置全局变量以影响着色器，以及初始化相关资源等操作
        /// </summary>
        public override void OnBrushTypeChange()
        {
            // 当笔刷类型为基础地图或纹理时
            if (brushType == BrushType.BaseMap || brushType == BrushType.Texture)
            {
                // 重置内容窗口大小并显示
                contentWindow.windowRect.size = InitWindowRect.size;
                contentWindow.visible = true;
            }
            else
            {
                // 隐藏内容窗口
                contentWindow.visible = false;
            }

            // 当笔刷类型为基础地图时
            if (brushType == BrushType.BaseMap || brushType == BrushType.Texture)
            {
                size = 15;
                if (baseMap == null)
                {
                    baseMap = new RenderTexture[4];
                }
                // blitMat = new Material(Shader.Find("Sango/blit"));
                // 如果当前季节的基础地图 RenderTexture 为空，则创建新的基础纹理
                if (baseMap.Length > editor.map.curSeason && baseMap[editor.map.curSeason] == null)
                {
                    baseMap[editor.map.curSeason] = CreateBaseTexture();
                }

                // 设置全局基础纹理
                Shader.SetGlobalTexture("_BaseTex", baseMap[editor.map.curSeason]);
                brushMat = new Material(Shader.Find("Sango/brush"));

                // 计算地图大小
                mapSize = new Vector2(editor.mapData.vertex_width * editor.mapData.quadSize, editor.mapData.vertex_height * editor.mapData.quadSize);

                // 实例化颜色选择器
                if (picker == null)
                {
                    GameObject obj = GameObject.Instantiate(Resources.Load("Picker")) as GameObject;
                    if (obj != null)
                    {
                        picker = obj.GetComponentInChildren<ColorPicker>(true);
                        if (picker != null)
                        {
                            // 添加颜色改变监听器
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
                    // 如果颜色选择器不为空，则激活
                    if (picker != null)
                        picker.gameObject.SetActive(true);
                }

                // 设置笔刷类型和大小
                Shader.SetGlobalFloat("_BrushType", 1);
                Shader.SetGlobalFloat("_BrushSize", mapSize.x / size);

                // 在访问 brushTexture 数组元素之前添加边界检查
                if (brushIndex >= 0 && brushIndex < brushTexture.Length)
                {
                	Shader.SetGlobalTexture("_BrushTex", brushTexture[brushIndex]);
                }
            }
            else
            {
                // 设置笔刷类型为非基础地图
                Shader.SetGlobalFloat("_BrushType", 0);

                // 如果颜色选择器不为空，则隐藏
                if (picker != null)
                    picker.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 吸取目标值，根据笔刷当前类型（brushType）以及是否按下特定键盘按键（如左Alt键），来决定是否吸取相应的值并更新透明度（opacity），同时返回对应操作的执行结果（是否成功吸取）
        /// 例如，对于PullHeight类型笔刷且按下左Alt键时，会基于特定计算设置透明度；对于Water类型笔刷且按下左Alt键时，会从地图数据中获取对应顶点的水数据作为透明度值
        /// </summary>
        /// <param name="center">地图中的坐标点，通常为笔刷作用的中心点位置，用于相关计算，比如在某些吸取操作中参考其坐标值来获取对应地图数据</param>
        /// <param name="editor">地图编辑器实例，用于获取地图相关的数据，如地图顶点数据、不同季节的纹理数据等，以辅助进行吸取值等操作</param>
        bool SuckValue(Vector3 center, MapEditor editor)
        {
            // 如果画笔类型（brushType）是PullHeight（拉高）且用户正在按住键盘上的左Alt键
            if (brushType == BrushType.PullHeight && Input.GetKey(KeyCode.LeftAlt))
            {
                // 计算透明度（opacity）的值，基于center.y的值进行缩放和偏移  
                // 这里假设center.y的值在-0.5到0.5之间，通过乘以2并加上0.5f，将其转换为0到1之间的值  
                // 然后再将浮点数转换为整数（虽然这可能会丢失精度，但根据上下文可能是有意为之）  
                opacity = (int)(center.y * 2 + 0.5f);
                // 返回true，表示条件满足，可能用于后续的逻辑处理（比如更新UI、触发事件等）  
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

        /// <summary>
        /// 【核心方法】修改地形数据，根据笔刷类型（brushType）的不同，执行不同的地形数据修改操作，比如调整地形高度、更改纹理、设置水面高度等，
        /// 同时还会进行一些边界检查、数据更新以及判断哪些地图单元（cell）需要重新刷新等相关操作
        /// </summary>
        /// <param name="center">地图中的坐标点，作为笔刷操作的中心点，用于确定笔刷影响的范围以及相关计算的参考位置</param>
        /// <param name="editor">地图编辑器实例，用于获取和操作地图相关的数据，如顶点地图数据、地形单元数据等</param>
        public override void Modify(Vector3 center, MapEditor editor)
        {
            switch (brushType)
            {
                case BrushType.RaiseHeight:     // 升高         
                case BrushType.LowerHeight:     // 降低
                case BrushType.PullHeight:      // 平整
                case BrushType.SmoothHeight:    // 平滑
                case BrushType.Texture:         // 贴图
                case BrushType.Water:           // 水面
                    {
                        // 先尝试吸取目标值，如果吸取成功则直接返回，不进行后续的修改操作
                        if (SuckValue(center, editor))
                            return;
                        // 计算笔刷影响范围在地图上的边界值（x、y方向起始和结束坐标），并添加边界检查，确保坐标在地图数据范围内
                        int xStart = Mathf.FloorToInt(center.z - size) / editor.mapData.quadSize;
                        int yStart = Mathf.FloorToInt(center.x - size) / editor.mapData.quadSize;
                        Vector3 cPos = center;  // 笔刷中心点
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
                        // 根据笔刷中心点获取对应的边界矩形，用于判断哪些地图单元格（cell）在笔刷影响范围内，以便后续进行相应的刷新操作
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
                case BrushType.BaseMap:         // 底色
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

        /// <summary>
        /// 根据笔刷与地图顶点的相对位置以及笔刷类型，判断是否对给定的地图顶点数据（vertexData）进行相应的修改操作，并返回是否执行了修改的结果
        /// 例如，针对不同的笔刷类型（如升高、降低、平滑高度、设置纹理、设置水面高度等），会按照各自的逻辑计算并更新顶点数据中的对应属性（如高度、纹理索引、水的高度等）
        /// </summary>
        /// <param name="center">地图中的坐标点，通常为笔刷作用的中心点位置，用于计算与顶点的距离等相关判断</param>
        /// <param name="vertexData">地图顶点数据的引用，用于根据笔刷操作进行相应的属性修改，如高度、纹理索引、水的相关数据等</param>
        /// <param name="x">顶点在地图数据中的x坐标索引，用于定位和操作对应的顶点数据</param>
        /// <param name="y">顶点在地图数据中的y坐标索引，用于定位和操作对应的顶点数据</param>
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
                            // 根据笔刷透明度、笔刷大小与当前顶点到笔刷中心的距离等因素，计算新的高度值，确保高度值在合法范围内（0 - 255）后更新顶点的高度属性
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
                            // 根据笔刷透明度、笔刷大小与当前顶点到笔刷中心的距离等因素，计算新的降低后的高度值，确保高度值在合法范围内（0 - 255）后更新顶点的高度属性
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
                            // 遍历当前顶点周围九宫格内的顶点，累加它们的高度值，用于后续计算平均高度
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

                            // 计算平均高度值，确保高度值在合法范围内（0 - 255）后更新顶点的高度属性
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
                            // 更新顶点数据中的纹理索引属性，将其设置为当前选定的纹理索引（textureIndex）
                            vertexData.textureIndex = (byte)textureIndex;
                        }
                        break;
                    case BrushType.Water:
                        {
                            // 根据笔刷透明度计算并更新顶点数据中的水的高度值，确保高度值在合法范围内（0 - 255）
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

        /// <summary>
        /// 字符串排序方法，根据字符串长度先进行比较，如果长度相同则按照字典序进行比较，返回比较结果（小于返回 -1，等于返回 0，大于返回 1）
        /// 主要用于对文件名等字符串数组进行排序操作，例如在加载图层信息时对不同季节的纹理文件名字符串进行排序
        /// </summary>
        /// <param name="a">要比较的第一个字符串</param>
        /// <param name="b">要比较的第二个字符串</param>
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

        /// <summary>
        /// 绘制界面，展示并操作与地形笔刷相关的各种参数和功能按钮，如调整笔刷大小、强度，以及针对不同笔刷类型保存对应的数据到BMP文件等操作
        /// </summary>
        public override void OnGUI()
        {
            // 开始水平布局
            GUILayout.BeginHorizontal();
            // 显示笔刷大小的标签，显示当前笔刷大小
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size), GUILayout.Width(80));
            // 开始垂直布局
            GUILayout.BeginVertical();
            // 垂直方向添加一些空间
            GUILayout.Space(8);
            if (brushType == BrushType.BaseMap)
            {
                // 当笔刷类型为 BaseMap 时，使用滑块调整笔刷大小，范围是 15f 到 150f
                float _size = GUILayout.HorizontalSlider(size, 15f, 150f);
                if (_size != size)
                {
                    size = _size;
                    OnBrushSizeChange();
                }
            }
            else
            {
                // 当笔刷类型不为 BaseMap 时，使用滑块调整笔刷大小，范围是 5f 到 100f
                float _size = GUILayout.HorizontalSlider(size, 5f, 100f);
                if (_size != size)
                {
                    size = _size;
                    OnBrushSizeChange();
                }
            }
            // 结束垂直布局
            GUILayout.EndVertical();
            // 结束水平布局
            GUILayout.EndHorizontal();

            // 开始另一个水平布局
            GUILayout.BeginHorizontal();
            // 显示笔刷强度的标签
            GUILayout.Label("笔刷强度", GUILayout.Width(80));
            // 使用 EditorUtility 的 FloatField 获取输入笔刷强度值，限制输入范围，若 GUI 发生变化，更新透明度
            float v = EditorUtility.FloatField(opacity, GUILayout.MaxWidth(32));
            if (GUI.changed)
            {
                opacity = v;
                if (opacity < 0)
                    opacity = 0;
                if (opacity > 255)
                    opacity = 255;
            }

            // 开始垂直布局
            GUILayout.BeginVertical();
            // 垂直方向添加一些空间
            GUILayout.Space(8);
            // 使用滑块调整笔刷强度（透明度），范围是 0f 到 255f，改变时调用 OnBrushOpacityChange 方法，触发相应逻辑
            float _opacity = GUILayout.HorizontalSlider(opacity, 0f, 255f);
            if (_opacity != opacity)
            {
                opacity = _opacity;
                OnBrushOpacityChange();
            }
            // 结束垂直布局
            GUILayout.EndVertical();
            // 结束水平布局
            GUILayout.EndHorizontal();

            // 垂直方向添加一些空间
            GUILayout.Space(8);
            UnityEngine.Color lastColor = GUI.backgroundColor;
            // 将背景颜色设置为青色
            GUI.backgroundColor = UnityEngine.Color.cyan;
            // 使用选择网格切换编辑模式，根据选择结果改变笔刷类型并调用 OnBrushTypeChange 方法
            int editMode = GUILayout.SelectionGrid(currentEditMode, toolbarTitle, 4, GUILayout.Height(60));
            if (editMode != currentEditMode)
            {
                currentEditMode = editMode;
                brushType = (BrushType)currentEditMode;
                OnBrushTypeChange();
            }
            // 恢复背景颜色
            GUI.backgroundColor = lastColor;
            switch (brushType)
            {
                // 对于不同的笔刷类型，有不同的操作
                case BrushType.RaiseHeight:
                case BrushType.LowerHeight:
                case BrushType.PullHeight:
                case BrushType.SmoothHeight:
                    {
                        /// <summary>
                        /// 点击按钮时，弹出保存文件对话框，若路径有效，在Windows平台下将高度数据保存为BMP格式文件
                        /// </summary>
                        if (GUILayout.Button("保存高度数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("height.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN
                                // 创建一个 Bitmap 对象，遍历顶点数据，将高度数据映射为颜色，存储在 BMP 中
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
                        // 开始水平布局
                        GUILayout.BeginHorizontal();
                        /// <summary>
                        /// 点击按钮自动加载图层信息，包括根据不同季节获取纹理文件、排序并更新地图图层相关数据
                        /// </summary>
                        if (GUILayout.Button("自动加载图层信息"))
                        {
                            AutoImportLayerTexture();
                        }

                        /// <summary>
                        /// 点击按钮保存图层数据到BMP文件，若路径有效，在Windows平台下进行相应保存操作
                        /// </summary>
                        if (GUILayout.Button("保存图层数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("layer.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN
                                // 创建一个 Bitmap 对象，遍历顶点数据，将纹理颜色映射为颜色，存储在 BMP 中
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
                        // 结束水平布局
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
                        /// <summary>
                        /// 点击按钮保存水数据到BMP文件，若路径有效，在Windows平台下将水数据保存为BMP格式文件
                        /// </summary>
                        if (GUILayout.Button("保存水数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("water.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN
                                // 创建一个 Bitmap 对象，遍历顶点数据，将水数据映射为颜色，存储在 BMP 中
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
                        // 开始水平布局
                        GUILayout.BeginHorizontal();
                        /// <summary>
                        /// 点击按钮弹出确认弹框，确认后重置基础地图编辑相关数据
                        /// </summary>
                        if (GUILayout.Button("重置编辑"))
                        {
                            int season = (int)editor.map.curSeason;
                            RenderTexture.ReleaseTemporary(baseMap[season]);
                            baseMap[season] = CreateBaseTexture();
                            Shader.SetGlobalTexture("_BaseTex", baseMap[season]);
                        }
                        /// <summary>
                        /// 点击按钮加载底色纹理，打开纹理文件，更新相关地图数据并重新创建基础纹理
                        /// </summary>
                        if (GUILayout.Button("加载底色"))
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
                                baseMap[season] = CreateBaseTexture();
                                Shader.SetGlobalTexture("_BaseTex", baseMap[season]);
                            });
                        }
                        /// <summary>
                        /// 点击按钮保存底色纹理为PNG文件，同时在Windows平台下可转换保存为BMP文件
                        /// </summary>
                        if (GUILayout.Button("保存底色"))
                        {
                            int i = editor.map.curSeason;
                            string path = WindowDialog.SaveFileDialog("BaseMap" + i + ".png", "贴图文件(*.png)|*.png\0");
                            if (path != null)
                            {
                                SaveBaseTexture(path, i);
                            }
                        }
                        // 结束水平布局
                        GUILayout.EndHorizontal();
                        //GUILayout.Label(textures[brushIndex], GUILayout.Width(128), GUILayout.Height(128));
                        // 根据选择改变笔刷纹理索引，更新全局纹理
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

        /// <summary>
        /// 绘制Gizmos小控件，处理与笔刷相关的一些交互逻辑，如空格键操作、根据笔刷类型调整笔刷大小及其他特定逻辑等
        /// </summary>
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
                	{   // 设置所有静态对象在编辑器中隐藏
                        editor.map.mapModels.EditorShow(false);
                    }
                    return;
                }

                if (Input.GetKeyUp(KeyCode.LeftAlt))
            	{   // 设置所有静态对象在编辑器中显示
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