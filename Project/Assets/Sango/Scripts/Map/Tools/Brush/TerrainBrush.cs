using HSVPicker;
using Sango.Render;
using Sango.Tools.UndoRedo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using UnityEngine;

namespace Sango.Tools
{
    /// <summary>
    /// 地形编辑笔刷
    /// 支持地形高度编辑、纹理绘制、水面编辑和底图绘制
    /// </summary>
    public class TerrainBrush : BrushBase
    {
        /// <summary>
        /// 笔刷类型枚举
        /// </summary>
        public enum BrushType : int
        {
            /// <summary>
            /// 升高高度
            /// </summary>
            RaiseHeight = 0,
            /// <summary>
            /// 降低高度
            /// </summary>
            LowerHeight,
            /// <summary>
            /// 平整高度
            /// </summary>
            PullHeight,
            /// <summary>
            /// 平滑高度
            /// </summary>
            SmoothHeight,
            /// <summary>
            /// 纹理绘制
            /// </summary>
            Texture,
            /// <summary>
            /// 水面编辑
            /// </summary>
            Water,
            /// <summary>
            /// BaseMap画笔
            /// </summary>
            BaseMap,
            /// <summary>
            /// BaseMap橡皮擦
            /// </summary>
            BaseMapEraser,
            /// <summary>
            /// 未知类型
            /// </summary>
            Unknown,
        }

        /// <summary>
        /// 笔刷大小
        /// </summary>
        public float size = 5f;

        /// <summary>
        /// 笔刷透明度
        /// </summary>
        public float opacity;

        /// <summary>
        /// 工具栏标题数组
        /// </summary>
        private string[] toolbarTitle = new string[] { "升高", "降低", "平整", "平滑", "贴图", "水面", "BaseMap画笔", "BaseMap橡皮擦" };

        /// <summary>
        /// 当前编辑模式索引
        /// </summary>
        private int currentEditMode = 0;

        /// <summary>
        /// 笔刷纹理数组
        /// </summary>
        public Texture[] brushTexture;

        /// <summary>
        /// 当前笔刷类型
        /// </summary>
        public BrushType brushType = BrushType.Unknown;

        /// <summary>
        /// 纹理索引
        /// </summary>
        private int textureIndex = 0;

        /// <summary>
        /// 滚动位置
        /// </summary>
        private Vector2 scrollPos;

        /// <summary>
        /// 底图数组
        /// </summary>
        public RenderTexture[] baseMap;

        /// <summary>
        /// 临时底图
        /// </summary>
        public RenderTexture temp_baseMap;

        /// <summary>
        /// 笔刷索引
        /// </summary>
        private int brushIndex = 0;

        /// <summary>
        /// 笔刷材质
        /// </summary>
        private Material brushMat;

        /// <summary>
        /// 绘制材质
        /// </summary>
        private Material blitMat;

        /// <summary>
        /// 地图大小
        /// </summary>
        private Vector2 mapSize;

        /// <summary>
        /// 颜色选择器
        /// </summary>
        private ColorPicker picker;

        /// <summary>
        /// 笔刷颜色
        /// </summary>
        private UnityEngine.Color brushColor;

        /// <summary>
        /// 拖拽相关变量 - 顶点数据变更字典
        /// </summary>
        private Dictionary<int, TerrainEditCommand.VertexDataChange> dragChangesMap = new Dictionary<int, TerrainEditCommand.VertexDataChange>();

        /// <summary>
        /// 拖拽编辑类型
        /// </summary>
        private TerrainEditCommand.EditType dragEditType;

        /// <summary>
        /// 拖拽描述
        /// </summary>
        private string dragDescription;

        /// <summary>
        /// 拖拽边界
        /// </summary>
        private Rect dragBounds;

        Transform textROOT;

        public TerrainBrush(MapEditor e) : base(e)
        {
            if (brushType == BrushType.Unknown)
                brushType = BrushType.RaiseHeight;
        }

        // BaseMap拖拽相关变量
        private Texture2D dragStartTexture;
        private string dragBaseMapDescription;
        private BaseMapEditCommand.EditType dragBaseMapEditType;

        /// <summary>
        /// 拖拽开始
        /// </summary>
        /// <param name="center">中心点</param>
        public override void OnDragStart(Vector3 center)
        {
            // 清空之前的拖拽数据
            dragChangesMap.Clear();

            // 初始化拖拽边界
            dragBounds = GetBounds(center);

            // 设置编辑类型和描述
            switch (brushType)
            {
                case BrushType.RaiseHeight:
                    dragEditType = TerrainEditCommand.EditType.Height;
                    dragDescription = "升高地形";
                    break;
                case BrushType.LowerHeight:
                    dragEditType = TerrainEditCommand.EditType.Height;
                    dragDescription = "降低地形";
                    break;
                case BrushType.PullHeight:
                    dragEditType = TerrainEditCommand.EditType.Height;
                    dragDescription = "平整地形";
                    break;
                case BrushType.SmoothHeight:
                    dragEditType = TerrainEditCommand.EditType.Height;
                    dragDescription = "平滑地形";
                    break;
                case BrushType.Texture:
                    dragEditType = TerrainEditCommand.EditType.Texture;
                    dragDescription = "修改纹理";
                    break;
                case BrushType.Water:
                    dragEditType = TerrainEditCommand.EditType.Water;
                    dragDescription = "修改水面";
                    break;
                case BrushType.BaseMap:
                    // 保存拖拽开始时的纹理
                    dragStartTexture = CaptureBaseMapTexture(editor.map.curSeason);
                    dragBaseMapEditType = BaseMapEditCommand.EditType.Brush;
                    dragBaseMapDescription = "BaseMap画笔";

                    if (brushBlendMode == 1)
                    {
                        int width = Math.Min(4096, editor.map.mapData.vertex_width);
                        int height = Math.Min(4096, editor.map.mapData.vertex_height);
                        temp_baseMap = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.ARGB32, 0);
                        UnityEngine.Graphics.Blit(baseMap[editor.map.curSeason], temp_baseMap);
                    }

                    break;
                case BrushType.BaseMapEraser:
                    // 保存拖拽开始时的纹理
                    dragStartTexture = CaptureBaseMapTexture(editor.map.curSeason);
                    dragBaseMapEditType = BaseMapEditCommand.EditType.Eraser;
                    dragBaseMapDescription = "BaseMap橡皮擦";
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
            // BaseMap和BaseMapEraser的拖拽处理
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
            {
                // 执行绘制操作
                if (brushType == BrushType.BaseMapEraser)
                {
                    // 橡皮擦模式 - 使用透明颜色
                    UnityEngine.Color originalColor = brushColor;
                    brushColor = new UnityEngine.Color(0, 0, 0, 0);
                    Shader.SetGlobalColor("_BrushColor", brushColor);
                    UnityEngine.Graphics.Blit(Texture2D.whiteTexture, baseMap[editor.map.curSeason], brushMat);
                    brushColor = originalColor;
                    Shader.SetGlobalColor("_BrushColor", brushColor);
                }
                else
                {
                    if (brushBlendMode == 1)
                    {
                        UnityEngine.Graphics.Blit(temp_baseMap, baseMap[editor.map.curSeason], brushMat);
                    }
                    else

                        // 画笔模式
                        UnityEngine.Graphics.Blit(Texture2D.whiteTexture, baseMap[editor.map.curSeason], brushMat);
                }
                return;
            }

            // 其他类型的拖拽处理
            // 获取当前笔刷边界并扩展拖拽边界
            Rect currentBounds = GetBounds(center);
            dragBounds.xMin = Mathf.Min(dragBounds.xMin, currentBounds.xMin);
            dragBounds.yMin = Mathf.Min(dragBounds.yMin, currentBounds.yMin);
            dragBounds.xMax = Mathf.Max(dragBounds.xMax, currentBounds.xMax);
            dragBounds.yMax = Mathf.Max(dragBounds.yMax, currentBounds.yMax);

            int xStart = Mathf.FloorToInt(center.z - size) / editor.mapData.quadSize;
            int yStart = Mathf.FloorToInt(center.x - size) / editor.mapData.quadSize;
            Vector3 cPos = center;
            int length = Mathf.FloorToInt(size * 2 / editor.mapData.quadSize) + 1;
            int xEnd = xStart + length;
            int yEnd = yStart + length;

            // 收集当前笔刷影响范围内的顶点变化
            for (int x = xStart; x < xEnd; x++)
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    if (x >= 0 && x <= editor.mapData.vertex_width && y >= 0 && y <= editor.mapData.vertex_height)
                    {
                        // 使用数学公式计算唯一key：x + width * y
                        int vertexKey = x + editor.mapData.vertex_width * y;

                        MapData.VertexData vertexData = editor.vertexMapData[x][y];
                        byte oldValue = 0;

                        switch (dragEditType)
                        {
                            case TerrainEditCommand.EditType.Height:
                                oldValue = vertexData.height;
                                break;
                            case TerrainEditCommand.EditType.Texture:
                                oldValue = vertexData.textureIndex;
                                break;
                            case TerrainEditCommand.EditType.Water:
                                oldValue = vertexData.water;
                                break;
                        }

                        // 创建临时副本进行修改
                        MapData.VertexData tempData = vertexData;
                        if (Do(cPos, ref tempData, x, y))
                        {
                            byte newValue = 0;
                            switch (dragEditType)
                            {
                                case TerrainEditCommand.EditType.Height:
                                    newValue = tempData.height;
                                    break;
                                case TerrainEditCommand.EditType.Texture:
                                    newValue = tempData.textureIndex;
                                    break;
                                case TerrainEditCommand.EditType.Water:
                                    newValue = tempData.water;
                                    break;
                            }

                            // 检查是否已经有记录
                            if (dragChangesMap.ContainsKey(vertexKey))
                            {
                                // 更新现有记录的新值为最终值
                                TerrainEditCommand.VertexDataChange existingChange = dragChangesMap[vertexKey];
                                existingChange.newValue = newValue;
                                dragChangesMap[vertexKey] = existingChange;
                            }
                            else
                            {
                                // 创建新记录
                                TerrainEditCommand.VertexDataChange change = new TerrainEditCommand.VertexDataChange();
                                change.x = x;
                                change.y = y;
                                change.oldValue = oldValue;
                                change.newValue = newValue;
                                dragChangesMap.Add(vertexKey, change);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < editor.map.mapTerrain.terrainCells.Length; i++)
            {
                MapCell cell = editor.map.mapTerrain.terrainCells[i];
                if (cell != null)
                {
                    if (cell.Overlaps(currentBounds))
                    {
                        cell.PrepareDatas(false);
                    }
                }
            }

        }

        /// <summary>
        /// 拖拽结束
        /// </summary>
        /// <param name="center">中心点</param>
        public override void OnDragEnd(Vector3 center)
        {
            // BaseMap和BaseMapEraser的拖拽结束处理
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
            {
                // 保存拖拽结束时的纹理
                Texture2D dragEndTexture = CaptureBaseMapTexture(editor.map.curSeason);

                // 创建Undo/Redo命令
                if (dragStartTexture != null && dragEndTexture != null)
                {
                    BaseMapEditCommand command = new BaseMapEditCommand(editor, dragBaseMapEditType, editor.map.curSeason, dragStartTexture, dragEndTexture, dragBounds, dragBaseMapDescription);
                    editor.undoRedoManager.AddCommand(command);
                }

                if (brushBlendMode == 1)
                {
                    RenderTexture.ReleaseTemporary(temp_baseMap);
                }

                // 清空拖拽数据
                dragStartTexture = null;
                return;
            }

            // 其他类型的拖拽结束处理
            // 如果有变化，创建批量命令并执行
            if (dragChangesMap.Count > 0)
            {
                List<TerrainEditCommand.VertexDataChange> finalChanges = new List<TerrainEditCommand.VertexDataChange>(dragChangesMap.Values);
                TerrainEditCommand command = new TerrainEditCommand(editor, dragEditType, finalChanges, dragDescription, dragBounds);
                editor.undoRedoManager.AddCommand(command);
            }

            // 清空拖拽数据
            dragChangesMap.Clear();
        }
        public override void OnEnter()
        {
            if (textROOT == null)
            {
                textROOT = GameObject.Find("GridTextRoot").transform;
            }

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
        }
        float gridInfoAlpha = 1;

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
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
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
                //blitMat.SetTexture("_BaseMap", editor.map.mapBaseColor.texture[curSeason]);
                ////blitMat.SetFloat("_BrushSize", mapSize.x / editor.map.mapBaseColor.texture[curSeason].width);
                //Vector2 scale = new Vector2(2, 1);
                //blitMat.SetTextureScale("_BaseMap", scale);
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

        public float brushOpacity = 1.0f; // 画笔透明度
        public int brushBlendMode = 0; // 画笔混合模式：0=正常, 1=正片叠底
        public bool pressureEnabled = true; // 是否启用压感
        public override void OnBrushTypeChange()
        {
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
            {
                size = 15;
                if (baseMap == null)
                {
                    baseMap = new RenderTexture[4];
                }
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
                Shader.SetGlobalFloat("_BrushOpacity", brushOpacity);
                Shader.SetGlobalInt("_BlendMode", brushBlendMode);
                Shader.SetGlobalFloat("_Pressure", 1.0f);
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

                        // 记录变化前的数据
                        List<TerrainEditCommand.VertexDataChange> changes = new List<TerrainEditCommand.VertexDataChange>();
                        TerrainEditCommand.EditType editType = TerrainEditCommand.EditType.Height;
                        string description = "";

                        switch (brushType)
                        {
                            case BrushType.RaiseHeight:
                                editType = TerrainEditCommand.EditType.Height;
                                description = "升高地形";
                                break;
                            case BrushType.LowerHeight:
                                editType = TerrainEditCommand.EditType.Height;
                                description = "降低地形";
                                break;
                            case BrushType.PullHeight:
                                editType = TerrainEditCommand.EditType.Height;
                                description = "平整地形";
                                break;
                            case BrushType.SmoothHeight:
                                editType = TerrainEditCommand.EditType.Height;
                                description = "平滑地形";
                                break;
                            case BrushType.Texture:
                                editType = TerrainEditCommand.EditType.Texture;
                                description = "修改纹理";
                                break;
                            case BrushType.Water:
                                editType = TerrainEditCommand.EditType.Water;
                                description = "修改水面";
                                break;
                        }

                        // 收集变化数据
                        for (int x = xStart; x < xEnd; x++)
                            for (int y = yStart; y < yEnd; y++)
                            {
                                if (x >= 0 && x <= editor.mapData.vertex_width && y >= 0 && y <= editor.mapData.vertex_height)
                                {
                                    MapData.VertexData vertexData = editor.vertexMapData[x][y];
                                    byte oldValue = 0;

                                    switch (editType)
                                    {
                                        case TerrainEditCommand.EditType.Height:
                                            oldValue = vertexData.height;
                                            break;
                                        case TerrainEditCommand.EditType.Texture:
                                            oldValue = vertexData.textureIndex;
                                            break;
                                        case TerrainEditCommand.EditType.Water:
                                            oldValue = vertexData.water;
                                            break;
                                    }

                                    // 创建临时副本进行修改
                                    MapData.VertexData tempData = vertexData;
                                    if (Do(cPos, ref tempData, x, y))
                                    {
                                        byte newValue = 0;
                                        switch (editType)
                                        {
                                            case TerrainEditCommand.EditType.Height:
                                                newValue = tempData.height;
                                                break;
                                            case TerrainEditCommand.EditType.Texture:
                                                newValue = tempData.textureIndex;
                                                break;
                                            case TerrainEditCommand.EditType.Water:
                                                newValue = tempData.water;
                                                break;
                                        }

                                        // 只有值发生变化时才记录
                                        if (oldValue != newValue)
                                        {
                                            TerrainEditCommand.VertexDataChange change = new TerrainEditCommand.VertexDataChange();
                                            change.x = x;
                                            change.y = y;
                                            change.oldValue = oldValue;
                                            change.newValue = newValue;
                                            changes.Add(change);
                                        }
                                    }
                                }
                            }

                        // 判断哪些cell需要重新刷新
                        Rect rect = GetBounds(center);

                        // 如果有变化，创建命令并执行
                        if (changes.Count > 0)
                        {
                            TerrainEditCommand command = new TerrainEditCommand(editor, editType, changes, description, rect);
                            editor.undoRedoManager.AddCommand(command);

                        }

                        for (int i = 0; i < editor.map.mapTerrain.terrainCells.Length; i++)
                        {
                            MapCell cell = editor.map.mapTerrain.terrainCells[i];
                            if (cell != null)
                            {
                                if (cell.Overlaps(rect))
                                {
                                    cell.PrepareDatas(false);
                                }
                            }
                        }


                    }
                    break;
                case BrushType.BaseMap:
                case BrushType.BaseMapEraser:
                    {
                        // 保存操作前的纹理
                        Texture2D oldTexture = CaptureBaseMapTexture(editor.map.curSeason);

                        // 执行绘制操作
                        if (brushType == BrushType.BaseMapEraser)
                        {
                            // 橡皮擦模式 - 使用透明颜色
                            UnityEngine.Color originalColor = brushColor;
                            brushColor = new UnityEngine.Color(0, 0, 0, 0);
                            Shader.SetGlobalColor("_BrushColor", brushColor);
                            UnityEngine.Graphics.Blit(Texture2D.whiteTexture, baseMap[editor.map.curSeason], brushMat);
                            brushColor = originalColor;
                            Shader.SetGlobalColor("_BrushColor", brushColor);
                        }
                        else
                        {
                            if (brushBlendMode == 1)
                            {
                                int width = Math.Min(4096, editor.map.mapData.vertex_width);
                                int height = Math.Min(4096, editor.map.mapData.vertex_height);
                                RenderTexture rt = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.ARGB32, 0);
                                UnityEngine.Graphics.Blit(baseMap[editor.map.curSeason], rt);
                                UnityEngine.Graphics.Blit(rt, baseMap[editor.map.curSeason], brushMat);
                                RenderTexture.ReleaseTemporary(rt);
                            }
                            else

                                // 画笔模式
                                UnityEngine.Graphics.Blit(Texture2D.whiteTexture, baseMap[editor.map.curSeason], brushMat);
                        }

                        // 保存操作后的纹理
                        Texture2D newTexture = CaptureBaseMapTexture(editor.map.curSeason);

                        // 创建Undo/Redo命令
                        string description = brushType == BrushType.BaseMapEraser ? "BaseMap橡皮擦" : "BaseMap画笔";
                        BaseMapEditCommand.EditType editType = brushType == BrushType.BaseMapEraser ? BaseMapEditCommand.EditType.Eraser : BaseMapEditCommand.EditType.Brush;
                        BaseMapEditCommand command = new BaseMapEditCommand(editor, editType, editor.map.curSeason, oldTexture, newTexture, GetBounds(center), description);
                        editor.undoRedoManager.AddCommand(command);
                    }
                    break;
                default:
                    {
                    }
                    break;
            }
        }

        private Dictionary<int, Texture2D> captureTextures = new Dictionary<int, Texture2D>();
        /// <summary>
        /// 捕获BaseMap纹理到Texture2D
        /// </summary>
        /// <param name="season">季节</param>
        /// <returns>捕获的纹理</returns>
        private Texture2D CaptureBaseMapTexture(int season)
        {
            if (baseMap == null || season < 0 || season >= baseMap.Length || baseMap[season] == null)
                return null;

            RenderTexture renderTexture = baseMap[season];
            int width = renderTexture.width;
            int height = renderTexture.height;

            // 复用Texture2D对象以减少内存开销
            Texture2D texture2D;
            if (!captureTextures.TryGetValue(season, out texture2D) || texture2D.width != width || texture2D.height != height)
            {
                // 如果不存在或尺寸不匹配，创建新的Texture2D
                if (texture2D != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(texture2D);
#else
                    UnityEngine.Object.Destroy(texture2D);
#endif
                }
                texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
                captureTextures[season] = texture2D;
            }

            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
            texture2D.Apply();
            RenderTexture.active = null;

            // 创建副本以确保Undo/Redo状态的独立性
            Texture2D copyTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            copyTexture.SetPixels(texture2D.GetPixels());
            copyTexture.Apply();

            return copyTexture;
        }

        public virtual bool Do(Vector3 center, ref MapData.VertexData vertexData, int x, int y)
        {
            float centerY = center.y;
            center.y = 0;
            Vector3 vPos = vertexData.position;
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
                            Vector3 vector3 = vertexData.position;
                            vector3.y = vertexData.height * 0.5f;
                            vertexData.position = vector3;
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
                            Vector3 vector3 = vertexData.position;
                            vector3.y = vertexData.height * 0.5f;
                            vertexData.position = vector3;
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
                            Vector3 vector3 = vertexData.position;
                            vector3.y = vertexData.height * 0.5f;
                            vertexData.position = vector3;
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
                            Vector3 vector3 = vertexData.position;
                            vector3.y = vertexData.height * 0.5f;
                            vertexData.position = vector3;
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
                            Vector3 vector3 = vertexData.waterPosition;
                            vector3.y = vertexData.water * 0.5f;
                            vertexData.waterPosition = vector3;
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

        string[] blendModes = new string[] { "正常", "正片叠底" };
        public override void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(String.Format("笔刷大小 [{0}]", size), GUILayout.Width(80));
            GUILayout.BeginVertical();
            GUILayout.Space(8);
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
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

            // 画笔透明度调节
            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("画笔透明度", GUILayout.Width(80));
                float _opacity = GUILayout.HorizontalSlider(brushOpacity, 0f, 1f);
                if (_opacity != brushOpacity)
                {
                    brushOpacity = _opacity;
                    Shader.SetGlobalFloat("_BrushOpacity", brushOpacity);
                }
                GUILayout.EndHorizontal();

                // 混合模式选择
                GUILayout.BeginHorizontal();
                GUILayout.Label("混合模式", GUILayout.Width(80));
              
                int _blendMode = GUILayout.SelectionGrid(brushBlendMode, blendModes, 2);
                if (_blendMode != brushBlendMode)
                {
                    brushBlendMode = _blendMode;
                    Shader.SetGlobalInt("_BlendMode", brushBlendMode);
                }
                GUILayout.EndHorizontal();

                // 压感控制
                GUILayout.BeginHorizontal();
                GUILayout.Label("启用压感", GUILayout.Width(80));
                bool _pressureEnabled = GUILayout.Toggle(pressureEnabled, "");
                if (_pressureEnabled != pressureEnabled)
                {
                    pressureEnabled = _pressureEnabled;
                    if (!pressureEnabled)
                    {
                        Shader.SetGlobalFloat("_Pressure", 1.0f);
                    }
                }
                GUILayout.EndHorizontal();
            }
            else
            {
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
                float _opacity = GUILayout.HorizontalSlider(opacity, 0f, 20f);
                if (_opacity != opacity)
                {
                    opacity = _opacity;
                    OnBrushOpacityChange();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(8);
            UnityEngine.Color lastColor = GUI.backgroundColor;
            GUI.backgroundColor = UnityEngine.Color.cyan;
            int editMode = GUILayout.SelectionGrid(currentEditMode, toolbarTitle, 3, GUILayout.Width(250),GUILayout.Height(90));
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

                                using (Bitmap bmp24 = new Bitmap(editor.map.mapData.vertex_x_max, editor.map.mapData.vertex_y_max, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
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
                            }
                        }
                    }
                    break;
                case BrushType.Texture:
                    {
                        GUILayout.BeginVertical();
                        if (GUILayout.Button("自动加载图层信息4800-0"))
                        {
                            string path = WindowDialog.OpenFolderDialog("选择目录");
                            if (path != null)
                            {
                                string[] rpStr = new string[]
                                {
                                    "4800-",
                                    "4801-",
                                    "4802-",
                                    "4803-",
                                };
                                AutoImportAndCreateLayerTexture(path, rpStr, 0);
                            }
                        }
                        if (GUILayout.Button("自动加载图层信息4801-1"))
                        {
                            string path = WindowDialog.OpenFolderDialog("选择目录");
                            if (path != null)
                            {
                                string[] rpStr = new string[]
                                {
                                    "4801-",
                                    "4802-",
                                    "4803-",
                                    "4804-",
                                };
                                AutoImportAndCreateLayerTexture(path, rpStr, 1);
                            }
                        }
                        if (GUILayout.Button("保存图层数据到BMP"))
                        {
                            string path = WindowDialog.SaveFileDialog("layer.bmp", "贴图文件(*.bmp)|*.bmp\0");
                            if (path != null)
                            {
#if UNITY_STANDALONE_WIN

                                using (Bitmap bmp24 = new Bitmap(editor.map.mapData.vertex_width + 1, editor.map.mapData.vertex_height + 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
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
                            }
                        }
                        GUILayout.EndVertical();
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
                case BrushType.BaseMapEraser:
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

                        // 笔刷选择
                        int brushRow = textures.Length > 0 ? (textures.Length - 1) / 4 + 1 : 1;
                        int sel = GUILayout.SelectionGrid(brushIndex, textures, 4, GUILayout.Width(204), GUILayout.MaxHeight(brushRow * 56));
                        if (sel != brushIndex)
                        {
                            brushIndex = sel;
                            Shader.SetGlobalTexture("_BrushTex", brushTexture[brushIndex]);
                        }
                    }
                    break;
                default:
                    {
                    }
                    break;
            }

            // 原 contentWindow 内容合并到此处
            switch (brushType)
            {
                case BrushType.BaseMap:
                case BrushType.BaseMapEraser:
                    {
                        GUILayout.Space(10);
                        GUILayout.Label("", GUI.skin.horizontalSlider);
                        GUILayout.Space(10);
                        GUILayout.Label(baseMap[editor.map.curSeason], GUILayout.Width(256), GUILayout.Height(256));
                    }
                    break;
                case BrushType.Texture:
                    {
                        GUILayout.Space(10);
                        GUILayout.Label("", GUI.skin.horizontalSlider);
                        GUILayout.Space(10);

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

            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
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

                // 检查并获取绘画板压感
                if (pressureEnabled)
                {
                    float pressure = 1.0f;

                    // 使用Unity3D的Touch API获取压感数据
                    if (Input.touchCount > 0)
                    {
                        Touch touch = Input.GetTouch(0);
                        if (touch.pressure > 0)
                        {
                            pressure = touch.pressure;
                        }
                    }

                    Shader.SetGlobalFloat("_Pressure", pressure);
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

            if (brushType == BrushType.BaseMap || brushType == BrushType.BaseMapEraser)
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
                    string file = Path.FindFile($"Terrain/{seasonName}/layer_{i}.png");
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
                    string file = Path.FindFile($"Terrain/{seasonName}/water_{i}");
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

        public void AutoImportAndCreateLayerTexture(string dir, string[] rpStr, int offset)
        {
            string[][] seasonfiles = new string[4][];
            int maxCount = 0;
            for (int j = 0; j < 4; ++j)
            {
                seasonfiles[j] = new string[100];
                string seasonName = MapRender.SeasonNames[j];
                string replaceStr = rpStr[j];
                for (int i = 0; i < 100; i++)
                {
                    string file = ($"{dir}/{replaceStr}{i + offset}.png");
                    if (File.Exists(file))
                    {
                        string saveFile = $"{Path.ContentRootPath}/Assets/Map/{editor.map.WorkContent}/Terrain/{seasonName}/layer_{i}.png";
                        Sango.Directory.Create(saveFile, false);
                        File.Copy(file, saveFile);
                        maxCount = Math.Max(maxCount, (i + 1));
                        seasonfiles[j][i] = saveFile;
                    }
                    else
                    {
                        seasonfiles[j][i] = "";
                    }
                }

                string water_src_file = $"{Path.ContentRootPath}/Assets/Map/Default/Terrain/{seasonName}/water_0.png";
                string water_saveFile = $"{Path.ContentRootPath}/Assets/Map/{editor.map.WorkContent}/Terrain/{seasonName}/water_0.png";
                File.Copy(water_src_file, water_saveFile);
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
                    string file = Path.FindFile($"Terrain/{seasonName}/water_{i}");
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

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            // 清理捕获纹理
            foreach (var texture in captureTextures.Values)
            {
                if (texture != null)
                {
#if UNITY_EDITOR
                    UnityEngine.Object.DestroyImmediate(texture);
#else
                    UnityEngine.Object.Destroy(texture);
#endif
                }
            }
            captureTextures.Clear();

            // 清理baseMap RenderTexture
            if (baseMap != null)
            {
                for (int i = 0; i < baseMap.Length; i++)
                {
                    if (baseMap[i] != null)
                    {
                        RenderTexture.ReleaseTemporary(baseMap[i]);
                        baseMap[i] = null;
                    }
                }
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