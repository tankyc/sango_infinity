
using Sango.Core;
using Sango.Loader;
using Sango.Tools;
using System;
using System.IO;
using UnityEngine;
using static Sango.Render.MapData;

namespace Sango.Render
{
    public class MapRender : System<MapRender>, IMapRender
    {
        public delegate void OnMapLoadedCall();
        public delegate void OnLoadModelCall(IMapManageObject mapObject);
        public delegate void OnInitGridCall(int gridSize, int boundx, int boundy);
        public delegate void OnBindGridDataCall(int x, int y, MapGrid.GridData data);
        public Action<MapCamera> onValueChanged;

        // 2022/10/20 add to 4
        // 2025/3/8 add to 5
        // 2025/3/14 add to 6
        // 2025/10/30 add to 8
        // 2026/1/7 add to 9
        // 2026/4/15 add to 10
        public const int VERSION = 10;
        static internal string[] SeasonNames = { "Autumn", "Spring", "Summer", "Winter" };

        public int mapWidth;
        public int mapHeight;
        public OnMapLoadedCall OnMapLoaded;
        public delegate void OnSeasonChange(int curSeason);
        public Material terrainOutlineMat;
        float _outlineWidth = 3.5f;
        bool _outlineShow = true;

        public float showLimitLength = 1100;
        // 数据
        public MapData mapData;
        public MapLayer mapLayer;
        public MapTerrain mapTerrain;
        public MapLight mapLight;
        public MapFog mapFog;
        public MapBaseColor mapBaseColor;
        public MapCamera mapCamera;
        public MapSkyBox mapSkyBox;
        public MapGrid mapGrid;
        public MapModels mapModels;
        public MapWater mapWater;
        public MapLabelSet mapLabelSet;
        // 战略地图图层：按地格烘焙势力色与势力/州边界，默认不显示
        public MapStrategyMap mapStrategyMap;
        // 顶视图（全局俯瞰）：缓动到地图正上方垂直俯视，并暂时只保留地形
        public MapTopView mapTopView;
        public OnSeasonChange onSeasonChange;

        /// <summary>顶视图开关回调。</summary>
        public delegate void OnTopViewChangeCall(bool isTopView);

        /// <summary>
        /// 顶视图开关事件：true = 已切到顶视图（模型已隐藏），false = 已开始恢复 / 已取消。
        /// UI（按钮高亮、提示条）、小地图等表现层可以订阅它，不需要自己轮询 isActive。
        /// </summary>
        public OnTopViewChangeCall onTopViewChange;

        // camera move
        Vector3 moveDest;
        Vector3 moveStart;
        float smoothMoveTime;
        float currentMoveTime;
        bool smoothMove;

        public string FileName { set; get; }
        public string WorkContent { set; get; }
        public string DefaultContentName { get { return "Default"; } }

        public OnLoadModelCall replaceLoadModelFunc;
        public OnLoadModelCall replaceBindModelFunc;
        public OnInitGridCall initGridFunc;
        public OnBindGridDataCall bindGridFunc;

        public static Transform modelRoot;
        public static GameObject mapRoot;
        public static int ScaleNX = 1;
        public MapRender()
        {

        }

        public static Texture LoadBoundTex()
        {
            return Instance.CreateTexture("map_bounds");
        }

        public static Texture LoadBoundTex(string mapName)
        {
            string destPath = $"Assets/Map/{mapName}/map_bounds";
            Texture texture = ObjectLoader.LoadObject<Texture>(destPath);
            if (texture == null)
            {
                destPath = $"Assets/Map/Default/map_bounds";
                texture = ObjectLoader.LoadObject<Texture>(destPath);
                if (texture == null)
                {
                    texture = Texture2D.whiteTexture;
                }
            }
            return texture;
        }

        public void Init()
        {
            WorkContent = "Default";
            if (mapRoot == null)
            {
                mapRoot = new GameObject("Map");
                mapRoot.AddComponent<MapLooper>();

                // 【不再创建旧地形节点】地形渲染已由 TerrainSango 接管（高度纹理 + 顶点着色器位移），
                // 旧的 "Terrain" 空节点以及挂在它下面的地形块 GameObject 已无消费者：
                //   · 渲染：TerrainSangoRenderer 独立出图，旧地形块的 MeshRenderer 本就全程关闭；
                //   · 点击：GameController → TerrainSangoBootstrap.TryGetMapHit，按同一份高度场求交。
                // 因此这里只保留 Model 节点（建筑/部队模型、地图标注仍挂在它下面）。
                GameObject tempGo = new GameObject("Model");
                tempGo.transform.SetParent(mapRoot.transform);
                modelRoot = tempGo.transform;
            }
        }

        protected void OnDestroy()
        {
            //if (Instance == this)
            //    Instance = null;

            // 旧实现靠 terrainRoot.parent 间接拿到 Map 根节点，现在没有 Terrain 中间层，直接销毁 mapRoot
            if (mapRoot != null)
            {
                GameObject.Destroy(mapRoot);
                mapRoot = null;
                modelRoot = null;
            }
        }

        protected void OnInitFunctions()
        {
            //loadModelFunc = GetFunction("OnCreateModel");
            //bindModelFunc = GetFunction("OnModelBind");
            //initGridFunc = GetFunction("OnGridInit");
            //bindGridFunc = GetFunction("OnGridBind");
        }

        public void LoadModel(IMapManageObject obj)
        {
            if (replaceLoadModelFunc != null)
            {
                replaceLoadModelFunc.Invoke(obj);
            }
            else
            {
                ModelConfig config = GameData.Instance.ModelConfigs.Get(obj.modelId);
                if (config != null)
                {
                    if (!string.IsNullOrEmpty(config.texture))
                    {
                        obj.modelAsset = $"Assets/Model/{config.model}";
                        obj.CreateModel(obj.modelAsset, $"Assets/Texture/{config.texture}", config.ShaderName, config.isShardMat);
                    }
                    else
                    {
                        obj.modelAsset = config.model;
                        obj.CreateModel(config.model);
                    }
                }

            }
        }

        public float outLineWidth
        {
            get { return _outlineWidth; }
            set
            {
                _outlineWidth = value;
                terrainOutlineMat.SetFloat("_OutlineWidth", _outlineWidth);
            }
        }
        public bool outLineShow
        {
            get { return _outlineShow; }
            set
            {
                _outlineShow = value;

                if (mapModels != null)
                {
                    mapModels.SetOutLineShow(_outlineShow ? terrainOutlineMat : null);
                }
            }
        }

        void InitMat()
        {
            //terrainOutlineMat = new Material(Shader.Find("Sango/outline_urp"));
            //terrainOutlineMat.renderQueue = 2500;
            //terrainOutlineMat.SetFloat("_OutlineWidth", _outlineWidth);
        }

        public void NewMap(int w, int h)
        {
            Clear();
            Init();
            InitMat();
            mapWidth = w;
            mapHeight = h;
            CreateDatas();
            OnMapLoaded?.Invoke();

        }

        public void LoadFromBMP(int w, int h, string height, string layer, string water)
        {
            InitMat();
            mapWidth = w;
            mapHeight = h;

            // 必须第一个初始化
            mapData = new MapData(this);
            mapData.Init();
            mapData.LoadFromBMP(w, h, height, layer, water);
            CreateDatas(false);
        }

        private void CreateDatas(bool newData = true)
        {
            // 重建子系统前先释放上一份战略地图：
            // 它订阅的是静态的 GameEvent，不先退订会让旧实例一直收到城池易主回调
            if (mapStrategyMap != null)
            {
                mapStrategyMap.Clear();
                mapStrategyMap = null;
            }

            // 必须第一个初始化
            if (newData)
            {
                mapData = new MapData(this);
                mapData.Init();
            }

            // 必须在terrain前
            mapLayer = new MapLayer(this);
            mapTerrain = new MapTerrain(this);
            mapLight = new MapLight(this);
            mapFog = new MapFog(this);
            mapBaseColor = new MapBaseColor(this);
            mapCamera = new MapCamera(this);
            mapSkyBox = new MapSkyBox(this);
            mapGrid = new MapGrid(this);
            // 依赖 mapGrid 的 bounds（在 mapGrid.Init 之后才能确定贴图尺寸），故放在其后面创建
            mapStrategyMap = new MapStrategyMap(this);
            mapModels = new MapModels(this);
            mapLabelSet = new MapLabelSet(this);
            // 顶视图必须在 mapCamera 之后创建：它的 Init 会把输入拦截器注册到 mapCamera
            mapTopView = new MapTopView(this);

            mapLayer.Init();
            // 必须在terrain之前
            mapModels.Init();
            mapTerrain.Init();
            mapLight.Init();
            mapFog.Init();
            mapBaseColor.Init();
            mapCamera.Init();
            mapSkyBox.Init();
            mapGrid.Init();
            mapStrategyMap.Init();
            mapLabelSet.Init();
            mapTopView.Init();
        }

        public void AddDynamic(IMapManageObject obj)
        {
            mapModels.AddDynamic(obj);
        }

        public void AddStatic(IMapManageObject obj)
        {
            mapModels.AddStatic(obj);
        }

        public void RemoveDynamic(IMapManageObject obj)
        {
            mapModels.RemoveDynamic(obj);
        }

        public void RemoveStatic(IMapManageObject obj)
        {
            mapModels.RemoveStatic(obj);
        }

        public void Remove(IMapManageObject obj)
        {
            if (obj.isStatic)
                mapModels.RemoveStatic(obj);
            else
                mapModels.RemoveDynamic(obj);
        }

        public Vector3 CoordsToPosition(int c, int r)
        {
            Vector3 pos = mapGrid.CoordsToPosition(c, r);
            pos.y = mapGrid.GetGridHeight(c, r);
            return pos;
        }
        public Vector2Int PositionToCoords(float x, float z)
        {
            return mapGrid.PositionToCoords(x, z);
        }

        public Vector2Int PositionToCoords(Vector3 pos)
        {
            return mapGrid.PositionToCoords(pos);
        }

        private int _curSeason = 0;
        public int curSeason
        {
            get { return _curSeason; }
            set
            {
                _curSeason = Math.Abs(value % 4);
                onSeasonChange?.Invoke(_curSeason);
            }
        }

        public void ChangeSeason(int s)
        {
            curSeason = s;
        }

        public void UpdateMaterials()
        {

        }

        public void Clear()
        {
            if (mapData != null)
                mapData.Clear();
            if (mapModels != null)
                mapModels.Clear();
            if (mapLayer != null)
                mapLayer.Clear();
            if (mapTerrain != null)
                mapTerrain.Clear();
            if (mapLight != null)
                mapLight.Clear();
            if (mapFog != null)
                mapFog.Clear();
            if (mapBaseColor != null)
                mapBaseColor.Clear();
            // 顶视图要在相机之前清理：它持有 mapCamera.interceptInput 的委托
            if (mapTopView != null)
                mapTopView.Clear();
            if (mapCamera != null)
                mapCamera.Clear();
            if (mapSkyBox != null)
                mapSkyBox.Clear();
            if (mapGrid != null)
                mapGrid.Clear();
            if (mapStrategyMap != null)
                mapStrategyMap.Clear();
            if (mapLabelSet != null)
                mapLabelSet.Clear();

            if (mapRoot != null)
            {
                GameObject.Destroy(mapRoot);
                mapRoot = null;
                modelRoot = null;
            }
        }

        public void LoadMap(string filename)
        {
            // 耗时打点：用于确认地图二进制读取的实际耗时，指导后续优化方向
            System.Diagnostics.Stopwatch loadStopwatch = System.Diagnostics.Stopwatch.StartNew();
            // 子阶段打点：定位 11 个子系统反序列化与材质加载各自的耗时占比
            System.Diagnostics.Stopwatch stageStopwatch = System.Diagnostics.Stopwatch.StartNew();
            long lastStage = 0;
            void LogStage(string stageName)
            {
                // 用 Info 级别输出（Log.Info 带 [Conditional("SANGO_DEBUG")]，正式包不输出）
                Sango.Log.Info(string.Format("地图子阶段[{0}]耗时:{1}毫秒", stageName, stageStopwatch.ElapsedMilliseconds - lastStage), Sango.Log.LogType.World);
                lastStage = stageStopwatch.ElapsedMilliseconds;
            }

            Clear();

            Init();

            FileName = filename;

            InitMat();
            LogStage("初始化材质");

            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            BinaryReader binr = new BinaryReader(fs);
            int versionCode = binr.ReadInt32();
            Sango.Log.Info("地图版本:" + versionCode, Sango.Log.LogType.World);
            if (versionCode >= 6)
            {
                WorkContent = binr.ReadString();
            }

            if (versionCode <= 2)
            {
                binr.ReadInt32();
                mapWidth = binr.ReadInt32();
                mapHeight = binr.ReadInt32();
            }
            else
            {
                mapWidth = binr.ReadInt32();
                mapHeight = binr.ReadInt32();
            }

            CreateDatas();
            LogStage("创建渲染子系统");

            if (versionCode > 5)
                mapGrid.OnLoad(versionCode, binr);
            LogStage("格子数据");
            mapData.OnLoad(versionCode, binr);
            LogStage("顶点数据");
            mapLayer.OnLoad(versionCode, binr);
            LogStage("图层");
            mapTerrain.OnLoad(versionCode, binr);
            LogStage("地形块");
            mapLight.OnLoad(versionCode, binr);
            LogStage("光照");
            mapFog.OnLoad(versionCode, binr);
            LogStage("迷雾");
            mapBaseColor.OnLoad(versionCode, binr);
            LogStage("底色");
            mapSkyBox.OnLoad(versionCode, binr);
            if (versionCode <= 5)
                mapGrid.OnLoad(versionCode, binr);
            LogStage("天空盒");
            mapCamera.OnLoad(versionCode, binr);
            LogStage("相机");
            mapModels.OnLoad(versionCode, binr);
            LogStage("模型");
            mapLabelSet.OnLoad(versionCode, binr);
            LogStage("城市标签");
            fs.Flush();
            binr.Close();
            fs.Close();

            // 图层贴图名已随 mapLayer.OnLoad 就绪，重建 MapCell 共享地形资产
            // （控制贴图 / TexArray / 共享材质）。首次 Init 时数据未加载，那一次是跳过的。
            if (mapTerrain != null)
                mapTerrain.RebuildTerrainAsset();

            Sango.Log.Info(string.Format("地图数据加载耗时:{0}毫秒", loadStopwatch.ElapsedMilliseconds), Sango.Log.LogType.World);

            // 【远景降级重新判定】降级状态是进程级静态缓存，地图却是每局重建的：
            // 读档 / 新开局时镜头可能仍停在上一个场景的远景距离，若不失效就会沿用旧的
            // IsFar 直接 return，新地图永远不被降级（表现为"远景镜头 + 近景画质"）。
            // 放在 OnMapLoaded 之前：让订阅方（相机定位 / TerrainSango 装配）先跑完，
            // 下一帧 Tick 再按最终镜头距离一次性施加。
            MapRenderLod.Invalidate();

            // 【视距参数重算】雾效 / 远裁剪面是按 MapFog 的基准值 + 当前镜头距离算出来的，
            // 而 MapFog.OnLoad 刚把基准值整份覆盖成新地图的值。若此时镜头距离与读档前相同，
            // "距离变化"判脏抓不到，必须显式置脏一次，否则远景会沿用上一份雾效参数。
            if (mapCamera != null)
                mapCamera.NeedUpdateViewDistance();

            OnMapLoaded?.Invoke();
        }

        public void SaveMap(string filename)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            BinaryWriter binr = new BinaryWriter(fs);
            binr.Write(VERSION);
            binr.Write(WorkContent);
            binr.Write(mapWidth);
            binr.Write(mapHeight);
            mapGrid.OnSave(binr);
            mapData.OnSave(binr);
            mapLayer.OnSave(binr);
            mapTerrain.OnSave(binr);
            mapLight.OnSave(binr);
            mapFog.OnSave(binr);
            mapBaseColor.OnSave(binr);
            mapSkyBox.OnSave(binr);
            mapCamera.OnSave(binr);
            mapModels.OnSave(binr);
            mapLabelSet.OnSave(binr);
            fs.Flush();
            binr.Close();
            fs.Close();
        }

        public void SaveScaleMap(string filename, int scale)
        {
            FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite);
            BinaryWriter binr = new BinaryWriter(fs);
            binr.Write(VERSION);
            binr.Write(WorkContent);
            binr.Write(mapWidth * scale);
            binr.Write(mapHeight * scale);
            mapGrid.OnSaveScale(binr, scale);
            mapData.OnSaveScale(binr, scale);
            mapLayer.OnSaveScale(binr, scale);
            mapTerrain.OnSaveScale(binr, scale);
            mapLight.OnSaveScale(binr, scale);
            mapFog.OnSaveScale(binr, scale);
            mapBaseColor.OnSaveScale(binr, scale);
            mapSkyBox.OnSaveScale(binr, scale);
            mapCamera.OnSaveScale(binr, scale);
            mapModels.OnSaveScale(binr, scale);
            mapLabelSet.OnSaveScale(binr, scale);
            fs.Flush();
            binr.Close();
            fs.Close();
        }

        public Texture CreateTexture(string textureName)
        {
            string destPath = $"Assets/Map/{WorkContent}/{textureName}";
            Texture texture = ObjectLoader.LoadObject<Texture>(destPath);
            if (texture == null)
            {
                destPath = $"Assets/Map/{DefaultContentName}/{textureName}";
                texture = ObjectLoader.LoadObject<Texture>(destPath);
                if (texture == null)
                {
                    texture = Texture2D.whiteTexture;
                }
            }
            return texture;
        }

        public Tools.Rect ViewRectCache;
        public void Update()
        {
            if (mapCamera != null)
            {
                if (smoothMove)
                {
                    currentMoveTime += Time.deltaTime;
                    float factor = currentMoveTime / smoothMoveTime;
                    mapCamera.position = Vector3.Lerp(moveStart, moveDest, Mathf.Pow(factor, 0.5f));
                    if (factor >= 1)
                        smoothMove = false;
                }

                // 顶视图缓动必须在 mapCamera.Update 之前推进：两者写的是同一组相机参数
                // （look_position / cur_distance / look_rotate），晚一帧会让镜头停在中间值上抖一下
                if (mapTopView != null)
                    mapTopView.Update();

                mapCamera.Update();
                float x, y, w, h;
                if (mapCamera.GetViewRect(showLimitLength, out x, out y, out w, out h))
                {
                    ViewRectCache.Set(x, y, w, h);
                }
            }

            //#if SANGO_DEBUG
            //            ViewRectCache = new Tools.Rect(0, 0, mapData.wrold_width, mapData.wrold_height);
            //#endif

            if (mapTerrain != null)
                mapTerrain.Update();

            if (mapModels != null)
                mapModels.Update(ViewRectCache);

            //if (mapGrid != null)
            //    mapGrid.Update(ViewRectCache);

            if (mapSkyBox != null)
                mapSkyBox.Update();

            if (mapLayer != null)
                mapLayer.Update();

            // 战略地图只在打脏标记后重建一次，这里必须每帧驱动
            if (mapStrategyMap != null)
                mapStrategyMap.Update();

            // 远景降级：镜头拉远后关掉实时阴影 / 地形描边 / 头顶条文字（内部判脏，只在翻转时切换）
            MapRenderLod.Tick();
        }

        public void UpdateImmediate()
        {
            if (mapTerrain != null)
                mapTerrain.UpdateImmediate();

            if (mapModels != null)
                mapModels.UpdateImmediate();

            //#if UNITY_EDITOR
            if (mapGrid != null)
                mapGrid.UpdateImmediate();
            //#endif

            if (mapSkyBox != null)
                mapSkyBox.UpdateImmediate();

            if (mapLayer != null)
                mapLayer.UpdateImmediate();
        }

        internal void BindModel(MapObject mapObject)
        {
            Sango.Log.Error($"BIND: objId:{mapObject.objId}, bindId:{mapObject.bindId} ");
            replaceBindModelFunc?.Invoke(mapObject);
        }

        internal void OnInitGrid()
        {
            initGridFunc?.Invoke(mapGrid.gridSize, mapGrid.bounds.x, mapGrid.bounds.y);
        }

        internal void OnBindGridData(int x, int y, MapGrid.GridData data)
        {
            bindGridFunc?.Invoke(x, y, data);
        }

        public void SetGridMask(int x, int y, bool b)
        {
            mapGrid.SetGridMaskColor(x, y, b ? Color.black : Color.clear);
        }

        public void SetGridMaskColor(int x, int y, Color c)
        {
            mapGrid.SetGridMaskColor(x, y, c);
        }

        public void ShowGrid(bool b)
        {
            mapGrid.ShowGrid(b);
        }

        public void SetDarkMask(bool b)
        {
            mapGrid.SetDarkMask(b);
        }

        public void SetDarkMaskColor(int x, int y, Color c)
        {
            mapGrid.SetDarkMaskColor(x, y, c);
        }

        public void EndSetDarkMask()
        {
            mapGrid.ApplyDarkMask();
        }

        public void EndSetGridMask()
        {
            mapGrid.ApplyGridMask();
        }

        public void SetRangeMask(int x, int y, bool b)
        {
            mapGrid.SetRangMaskColor(x, y, b ? Color.black : Color.clear);
        }

        public void SetRangeMaskColor(int x, int y, Color c)
        {
            mapGrid.SetRangMaskColor(x, y, c);
        }
        public void EndSetRangeMask()
        {
            mapGrid.ApplyRangMask();
        }

        /// <summary>显示 / 隐藏战略地图图层（按地格填充势力色并绘制势力边界）</summary>
        public void ShowStrategyMap(bool show)
        {
            if (mapStrategyMap != null)
                mapStrategyMap.SetVisible(show);
        }

        /// <summary>显示 / 隐藏战略地图上的地域边界（城池辖区界线）</summary>
        public void ShowStrategyAreaBorder(bool show)
        {
            if (mapStrategyMap != null)
                mapStrategyMap.SetAreaBorderVisible(show);
        }

        /// <summary>战略地图上的势力归属发生变化时打脏标记（通常由城池易主事件自动完成）</summary>
        public void RefreshStrategyMap()
        {
            if (mapStrategyMap != null)
                mapStrategyMap.MarkDirty();
        }

        public void OffsetCamera(Vector3 offset)
        {
            // 【空引用】mapCamera 只在 MapRender.Init 里创建（MapRender.cs:239），
            // 地图尚未加载完成 / 正在切换 / 刚 Clear 过的窗口期里它还是 null。
            // 而拖动镜头属于逐帧处理的输入（GameController.HandleMobileEvent → 本方法），
            // 世界没就绪时手指一拖就会抛 NullReferenceException。
            // 本类其余相机操作都已经是空安全写法（ZoomCamera:757、RotateCamera:762、
            // MoveCameraTo:772、SetKeyBoardMoveSpeed:843），这里补齐保持一致。
            mapCamera?.OffsetCamera(offset);
        }

        public static bool Raycast(Ray ray, out Vector3 hitPoint)
        {
            return Raycast(ray.origin, ray.direction, out hitPoint);
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
        {
            MapData mapData = Instance.mapData;
            VertexData[][] vertexDatas = mapData.vertexDatas;
            int quadSize = mapData.quadSize;
            int startX = (int)origin.z / quadSize;
            int startY = (int)origin.x / quadSize;

            float deltaX = origin.z - startX * quadSize;
            float deltaY = origin.x - startX * quadSize;
            float halfQuadSize = quadSize / 2.0f;

            if (deltaX < halfQuadSize && deltaY < halfQuadSize || deltaX > halfQuadSize && deltaY > halfQuadSize || deltaX < halfQuadSize && deltaY > halfQuadSize)
            {
                int x0 = startX, y0 = startY;
                int x1 = startX + 1, y1 = startY + 1;
                VertexData ver0 = vertexDatas[x0][y0];
                VertexData ver2 = vertexDatas[x1][y1];
                VertexData ver3 = vertexDatas[x0][y1];

                if ((ver0.water > 0 && ver0.water > ver0.height) || (ver2.water > 0 && ver2.water > ver2.height) || (ver3.water > 0 && ver3.water > ver3.height))
                {
                    if (PlaneLineIntersection.GetIntersectionWithXPerpendicularLine(mapData.VertexWaterPosition(ver0, x0, y0),
                        mapData.VertexWaterPosition(ver2, x1, y1), mapData.VertexWaterPosition(ver3, x0, y1), direction, Vector3.down, out hitPoint))
                    {
                        return true;
                    }
                }
                else
                {
                    if (PlaneLineIntersection.GetIntersectionWithXPerpendicularLine(mapData.VertexPosition(ver0.height, x0, y0),
                        mapData.VertexPosition(ver2.height, x1, y1), mapData.VertexPosition(ver3.height, x0, y1), direction, Vector3.down, out hitPoint))
                    {
                        return true;
                    }
                }


            }
            else
            {
                int x0 = startX, y0 = startY;
                int x1 = startX + 1, y1 = startY + 1;
                VertexData ver0 = vertexDatas[x0][y0];
                VertexData ver1 = vertexDatas[x1][y0];
                VertexData ver2 = vertexDatas[x1][y1];
                if (ver0.water > 0 || ver1.water > 0 || ver2.water > 0)
                {
                    if (PlaneLineIntersection.GetIntersectionWithXPerpendicularLine(mapData.VertexWaterPosition(ver1, x1, y0),
                        mapData.VertexWaterPosition(ver2, x1, y1), mapData.VertexWaterPosition(ver0, x0, y0), direction, Vector3.down, out hitPoint))

                    {
                        return true;
                    }
                }
                else
                {
                    if (PlaneLineIntersection.GetIntersectionWithXPerpendicularLine(mapData.VertexPosition(ver1.height, x1, y0),
                        mapData.VertexPosition(ver2.height, x1, y1), mapData.VertexPosition(ver0.height, x0, y0), direction, Vector3.down, out hitPoint))

                    {
                        return true;
                    }
                }

            }
            return false;
        }

        public static float QueryHeight(Vector3 pos)
        {
            if (Raycast(pos, pos + Vector3.up * 100, out Vector3 hitPoint))
                return hitPoint.y;
            return 0;
        }

        public static bool QueryHeight(Vector3 pos, out float height)
        {
            if (Raycast(pos, pos + Vector3.up * 100, out Vector3 hitPoint))
            {
                height = hitPoint.y;
                return true;
            }
            height = 0;
            return false;
        }

        public void ZoomCamera(float delta)
        {
            mapCamera?.ZoomCamera(delta);
        }

        public void RotateCamera(Vector2 offset)
        {
            mapCamera?.RotateCamera(offset);
        }

        public byte GetTerrainType(int x, int y)
        {
            return 0;
        }

        public void MoveCameraTo(Vector3 pos)
        {
            mapCamera?.MoveCameraTo(pos);
        }

        public Vector3 GetCameraPos()
        {
            return mapCamera.position;
        }

        public void MoveCameraTo(Vector3 pos, float time)
        {
            // 顶视图下要求"平滑移动到某处"，先立即结束顶视图，否则缓动和平滑移动会抢同一组参数
            CancelTopView();

            moveDest = pos;
            if (mapCamera != null)
                moveStart = mapCamera.position;
            if (time <= 0) time = 0.3f;
            smoothMoveTime = time;
            currentMoveTime = 0;
            smoothMove = true;
        }

        public void SetCamera(Vector3 pos, Vector3 rot, float dis)
        {
            // 直接设定视角同样要退出顶视图，否则顶视图缓动会在下一帧把参数改回去
            CancelTopView();

            mapCamera.position = pos;
            mapCamera.lookRotate = rot;
            mapCamera.distance = dis;
        }

        // ---------------- 顶视图（全局俯瞰） ----------------

        /// <summary>切换顶视图 / 普通视角。</summary>
        public void ToggleTopView()
        {
            mapTopView?.Toggle();
        }

        /// <summary>进入顶视图：相机缓动到地图正上方垂直俯视，并隐藏地形之外的所有渲染。</summary>
        public void EnterTopView()
        {
            mapTopView?.Enter();
        }

        /// <summary>退出顶视图：相机缓动回进入前的姿态，缓动结束后恢复地形之外的渲染。</summary>
        public void ExitTopView()
        {
            mapTopView?.Exit();
        }

        /// <summary>立即结束顶视图（不缓动）。用于"定位到某处""直接设定视角"这类需要当前帧接管相机的操作。</summary>
        public void CancelTopView()
        {
            if (mapTopView != null && mapTopView.isActive)
                mapTopView.Cancel();
        }

        /// <summary>是否处于顶视图（含进出缓动）。</summary>
        public bool IsTopView => mapTopView != null && mapTopView.isActive;

        /// <summary>立刻停止"镜头平滑移动到目标点"。</summary>
        public void StopSmoothMove()
        {
            smoothMove = false;
            currentMoveTime = 0f;
        }

        public void MoveCameraKeyBoard(bool[] keyFlags)
        {
            mapCamera.MoveCameraKeyBoard(keyFlags);
        }

        public void AddInstance(IMapManageObject obj)
        {
            mapModels?.AddInstance(obj);
        }

        public void RemoveInstance(IMapManageObject obj)
        {
            mapModels?.RemoveInstance(obj);

        }

        public bool IsInView(IMapManageObject obj)
        {
            return mapModels.IsInView(obj);

        }

        public float GetMaxVertexHeight(int x, int y)
        {
            float rs = -9999;
            int x_start = x * 4;
            int y_start = y * 4 + (x % 2) * 2;
            for (int j = 0; j < 5; j++)
            {
                for (int i = 0; i < 5; i++)
                {
                    int vIndex = j * 5 + i;
                    MapData.VertexData vertexData = mapData.GetVertexData(x_start + i, y_start + j);
                    rs = Mathf.Max(rs, vertexData.height * 0.5f);
                }
            }
            return rs;
        }

        /// <summary>
        /// 设置键盘移动速度
        /// </summary>
        /// <param name="m"></param>
        public void SetKeyBoardMoveSpeed(float m)
        {
            if (mapCamera == null) return;
            mapCamera.keyBoardMoveSpeed = m;
        }

        /// <summary>
        /// 设置最大视野
        /// </summary>
        /// <param name="m"></param>
        public void SetMaxViewDistance(float m)
        {
            if (mapCamera == null) return;
            Vector2 l = mapCamera.limitDistance;
            l.y = m;
            mapCamera.limitDistance = l;
        }

        public bool IsValid()
        {
            return mapRoot != null;
        }
    }

    /// <summary>
    /// 地图渲染相关的 Layer 索引缓存。
    ///
    /// 【为什么要这一层封装】
    /// Layer 名字在运行时只能用 <see cref="LayerMask.NameToLayer(string)"/> 查，每次查都是字符串哈希；
    /// 而地图 LOD 切换、射线拾取这类路径会频繁用到，缓存成静态只读字段最省事。
    ///
    /// 【NoOutLine 层是干什么的】
    /// URP Renderer（<c>Assets/GameAssets/URP/URP Asset_Renderer.asset</c>）里配了两个 RenderObjects
    /// Feature 用来画描边，它们都按 <b>LayerMask</b> 过滤：
    ///   · OutlineOpaque：LayerMask = Map(7) + Building(9)，对不透明对象画描边（当前 m_Active = 0）；
    ///   · RenderObjects：LayerMask = Building(9) + Tree(11)，对透明队列对象画描边。
    /// 相机拉远后描边在屏幕上根本看不出来，却仍要付一整趟 pass 的代价，
    /// 所以把地形块 / 建筑实例切到 <see cref="MapNoOutLine"/> / <see cref="BuildingNoOutLine"/> 层，
    /// 让上面两个 Feature 的 LayerMask 匹配不上，直接跳过描边 pass。拉近时再切回去。
    ///
    /// 【注意】切层会改变 <c>Physics.Raycast</c> 的命中结果，
    /// 所有按 "Map" 层拾取地形的代码必须改用 <see cref="TerrainHitMask"/>，否则相机拉远后点不到地图。
    /// </summary>
    public static class MapLayers
    {
        /// <summary>地形块所在层（带描边）</summary>
        public static readonly int Map = Resolve("Map", 7);

        /// <summary>地形块所在层（不带描边，远距离 LOD 用）</summary>
        public static readonly int MapNoOutLine = Resolve("MapNoOutLine", 21);

        /// <summary>建筑实例所在层（带描边）</summary>
        public static readonly int Building = Resolve("Building", 9);

        /// <summary>建筑实例所在层（不带描边，远距离 LOD 用）</summary>
        public static readonly int BuildingNoOutLine = Resolve("BuildingNoOutLine", 20);

        /// <summary>
        /// 地形拾取用的 LayerMask：Map + MapNoOutLine。
        /// 地形块随时可能在两层之间切换，射线拾取必须同时接受两者。
        /// </summary>
        public static readonly int TerrainHitMask = (1 << Map) | (1 << MapNoOutLine);

        /// <summary>
        /// 取 Layer 索引；项目里没有配置该层时回退到 <paramref name="fallback"/>。
        /// 回退值取自 TagManager 里的固定顺序，保证即使 Layer 被改名也不会整片失效。
        /// </summary>
        /// <param name="name">Layer 名</param>
        /// <param name="fallback">回退索引</param>
        /// <returns>Layer 索引</returns>
        static int Resolve(string name, int fallback)
        {
            int index = LayerMask.NameToLayer(name);
            if (index >= 0)
                return index;

            Sango.Log.Warning($"*[地图渲染] 未找到 Layer「{name}」，回退为 {fallback}，请检查 ProjectSettings/TagManager.asset");
            return fallback;
        }
    }
}
