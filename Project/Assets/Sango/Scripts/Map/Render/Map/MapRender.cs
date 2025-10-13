using Sango.Data;
using System;
using System.IO;
using UnityEngine;
using Sango;
using LuaInterface;
using System.Collections.Generic;
using Sango.Game;
using Sango.Loader;

namespace Sango.Render
{
    public class MapRender : System<MapRender>
    {
        public delegate void OnMapLoadedCall();
        // 2022/10/20 add to 4
        // 2025/3/8 add to 5
        // 2025/3/14 add to 6
        public const int VERSION = 6;
        static internal string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };

        public int mapWidth;
        public int mapHeight;
        public OnMapLoadedCall OnMapLoaded;
        public delegate void OnSeasonChange(int curSeason);
        public Material terrainOutlineMat;
        float _outlineWidth = 3.5f;
        bool _outlineShow = true;

        public float showLimitLength = 1200;
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
        public OnSeasonChange onSeasonChange;

        public string FileName { set; get; }
        public string WorkContent { set; get; }
        public string DefaultContentName { get { return "Default"; } }

        private LuaFunction loadModelFunc;
        private LuaFunction bindModelFunc;
        private LuaFunction initGridFunc;
        private LuaFunction bindGridFunc;

        public static Transform terrainRoot;
        public static Transform modelRoot;
        public static GameObject mapRoot;
        public static int ScaleNX = 1;
        public MapRender()
        {

        }

        public void Init()
        {
            WorkContent = "Default";
            if (mapRoot == null)
            {
                mapRoot = new GameObject("Map");
                mapRoot.AddComponent<MapLooper>();

                GameObject tempGo = new GameObject("Terrain");
                tempGo.transform.SetParent(mapRoot.transform);
                terrainRoot = tempGo.transform;
                tempGo = new GameObject("Model");
                tempGo.transform.SetParent(mapRoot.transform);
                modelRoot = tempGo.transform;
            }
        }

        protected void OnDestroy()
        {
            //if (Instance == this)
            //    Instance = null;

            if (terrainRoot != null && terrainRoot.parent != null)
                GameObject.Destroy(terrainRoot.parent.gameObject);
        }

        protected void OnInitFunctions()
        {
            loadModelFunc = GetFunction("OnCreateModel");
            bindModelFunc = GetFunction("OnModelBind");
            initGridFunc = GetFunction("OnGridInit");
            bindGridFunc = GetFunction("OnGridBind");
        }

        public void LoadModel(MapObject obj)
        {
            if (loadModelFunc != null)
            {
                CallMethod(loadModelFunc, obj);
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
            terrainOutlineMat = new Material(Shader.Find("Sango/outline_urp"));
            terrainOutlineMat.renderQueue = 2500;
            terrainOutlineMat.SetFloat("_OutlineWidth", _outlineWidth);
        }

        public void NewMap(int w, int h)
        {
            InitMat();
            mapWidth = w;
            mapHeight = h;
            CreateDatas();
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
            mapModels = new MapModels(this);

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

        public Vector3 CoordsToPosition(int c, int r)
        {
            Vector3 pos = mapGrid.CoordsToPosition(c, r);
            pos.y = mapGrid.GetGridHeight(c, r);
            return pos;
        }
        public Vector2Int PositionToCoords(float x, float y)
        {
            return mapGrid.PositionToCoords(x, y);
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
            if (mapCamera != null)
                mapCamera.Clear();
            if (mapSkyBox != null)
                mapSkyBox.Clear();
            if (mapGrid != null)
                mapGrid.Clear();

            if(mapRoot != null)
            {
                GameObject.Destroy(mapRoot);
                mapRoot = null;
                modelRoot = null;
                terrainRoot = null;
            }    
        }

        public void LoadMap(string filename)
        {

            Clear();

            Init();

            FileName = filename;

            InitMat();

            FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
            BinaryReader binr = new BinaryReader(fs);
            int versionCode = binr.ReadInt32();
            Debug.Log("地图版本:" + versionCode);
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
            if (versionCode > 5)
                mapGrid.OnLoad(versionCode, binr);
            mapData.OnLoad(versionCode, binr);
            mapLayer.OnLoad(versionCode, binr);
            mapTerrain.OnLoad(versionCode, binr);
            mapLight.OnLoad(versionCode, binr);
            mapFog.OnLoad(versionCode, binr);
            mapBaseColor.OnLoad(versionCode, binr);
            mapSkyBox.OnLoad(versionCode, binr);
            if (versionCode <= 5)
                mapGrid.OnLoad(versionCode, binr);
            mapCamera.OnLoad(versionCode, binr);
            mapModels.OnLoad(versionCode, binr);
            fs.Flush();
            binr.Close();
            fs.Close();

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
            fs.Flush();
            binr.Close();
            fs.Close();
        }

        public Texture CreateTexture(string textureName, string extensions = ".png")
        {
            string destPath = $"Assets/Map/{WorkContent}/{textureName}";
            if (!textureName.EndsWith(extensions))
                destPath = destPath + extensions;
            Texture texture = ObjectLoader.LoadObject<Texture>(destPath);
            if(texture == null)
            {
                destPath = $"Assets/Map/{DefaultContentName}/{textureName}{extensions}";
                texture = ObjectLoader.LoadObject<Texture>(destPath);
                if (texture == null)
                {
                    texture = Texture2D.whiteTexture;
                }
            }
            return texture;
        }

        private Tools.Rect ViewRectCache;
        public void Update()
        {
            if (mapCamera != null)
            {
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

//#if UNITY_EDITOR
            if (mapGrid != null)
                mapGrid.Update(ViewRectCache);
//#endif

            if (mapSkyBox != null)
                mapSkyBox.Update();

            if (mapLayer != null)
                mapLayer.Update();
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
            if (bindModelFunc != null)
            {
                CallMethod(bindModelFunc, mapObject.bindId, mapObject);
            }
        }

        internal void OnInitGrid()
        {
            if (initGridFunc != null)
            {
                CallMethod(initGridFunc, mapGrid.gridSize, mapGrid.bounds.x, mapGrid.bounds.y);
            }
        }

        internal void OnBindGridData(int x, int y, MapGrid.GridData data)
        {
            if (bindGridFunc != null)
            {
                CallMethod(bindGridFunc, x, y, data);
            }
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

        public void OffsetCamera(Vector3 offset)
        {
            mapCamera.OffsetCamera(offset);
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

        public void ZoomCamera(float delta)
        {
            mapCamera.ZoomCamera(delta);
        }

        public void RotateCamera(Vector2 offset)
        {
            mapCamera.RotateCamera(offset);
        }

        public byte GetTerrainType(int x, int y)
        {
            return 0;
        }

        public void MoveCameraTo(Vector3 pos)
        {
            mapCamera.MoveCameraTo(pos);
        }
    }
}
