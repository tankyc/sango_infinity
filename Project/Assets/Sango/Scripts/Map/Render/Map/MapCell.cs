using Sango.Core;
using Sango.Tools;
using System;
using System.Threading;
using UnityEngine;

namespace Sango.Render
{
    public class MapCell : MapProperty, IMapManageObject
    {
        //public static bool cellAlwaysVisible = false;
        public Vector2Int startCoords;
        public Vector2Int quadBounds;
        //public Vector3 position;
        public Mesh[] mesh;
        public MeshRenderer[] meshRenderer;
        public MeshCollider[] meshCollider;
        public Mesh[] lodMesh;
        public GameObject gameObject;
        public bool isAdded { get; set; }
        public Transform transform => gameObject.transform;

#if UNITY_ANDROID || UNITY_IPHONE
        public int lod = 1;
#else
        public int lod = 0;
#endif

        int[] xIndexCache;
        int[] yIndexCache;
        bool[] diffyIndexCache;

        LayerMeshData lmdCache;

        internal int threadBeginLayer = 0;
        bool threadLoadDone = false;

        static Color color_white = Color.white;
        static Color color_black = new Color(0, 0, 0, 0);

        //public Rect bounds;
        private bool _visible = false;
        /// <summary>
        /// 是否可保存至地图
        /// </summary>
        public bool canSave { get; set; } = false;

        public MapCell(MapRender map, int x, int y, int w, int h) : base(map)
        {
            startCoords = new Vector2Int(x, y);
            quadBounds = new Vector2Int(w, h);
            position = new Vector3(startCoords.y * map.mapData.quadSize, 0, startCoords.x * map.mapData.quadSize);
            this.bounds = new Sango.Tools.Rect(0, 0, quadBounds.x * map.mapData.quadSize, quadBounds.y * map.mapData.quadSize);
        }

        public MapCell(MapRender map, Vector2Int start, Vector2Int bounds) : base(map)
        {
            startCoords = start;
            quadBounds = bounds;
            position = new Vector3(startCoords.y * map.mapData.quadSize, 0, startCoords.x * map.mapData.quadSize);
            this.bounds = new Sango.Tools.Rect(0, 0, quadBounds.x * map.mapData.quadSize, quadBounds.y * map.mapData.quadSize);

        }

        public override void Init()
        {
            int maxLayer = map.mapLayer.layerDatas.Length;
            if (gameObject != null)
            {
                if (mesh != null && mesh.Length == maxLayer)
                {
                    // 已经建过：可能被 ReleaseMesh 释放了 Mesh 实体，这里只补齐缺的部分
                    EnsureMeshes(maxLayer);
                    threadBeginLayer = 0;
                    return;
                }
                GameObject.Destroy(gameObject);
                gameObject = null;
            }

            int layer = LayerMask.NameToLayer("Map");
            gameObject = new GameObject(string.Format("Map区域_{0}_{1}", startCoords.x, startCoords.y));
            gameObject.layer = layer;

            gameObject.transform.SetParent(MapRender.terrainRoot);

            mesh = new Mesh[maxLayer];
            meshRenderer = new MeshRenderer[maxLayer];
            meshCollider = new MeshCollider[maxLayer];
            int _vertex_y_max = quadBounds.y + 1;

            gameObject.transform.position = position;

            xIndexCache = new int[2];
            yIndexCache = new int[_vertex_y_max];
            diffyIndexCache = new bool[_vertex_y_max];

            // 只为本块真正出现的图层建 Mesh/GameObject。
            // CreateLayers 遇到本块没有该图层的顶点时会整块跳过，预先建好纯属浪费：
            // 全图 6944 块 x 49 层 = 34 万个 Mesh/GameObject，实测每块平均只有 ~16 层非空。
            bool[] usedLayer = ResolveUsedLayers(maxLayer);

            for (int i = 0; i < maxLayer; ++i)
            {
                if (!usedLayer[i]) continue;

                MapLayer.LayerData layerData = map.mapLayer.layerDatas[i];

                Mesh tempMesh = new Mesh();
                tempMesh.MarkDynamic();
                tempMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh[i] = tempMesh;

                GameObject go = new GameObject("Layer - " + i);
                go.transform.SetParent(gameObject.transform, false);
                go.layer = layer;

                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = tempMesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                if (i < maxLayer - 1 && map.outLineShow)
                {
                    Material[] mats = new Material[1]
                      {
                        layerData.material
                      };
                    renderer.sharedMaterials = mats;
                }
                else
                {
                    renderer.sharedMaterial = layerData.material;
                }
                meshRenderer[i] = renderer;
                renderer.enabled = false;
            }
            threadBeginLayer = 0;
        }

        /// <summary>
        /// 扫描本块覆盖的顶点，返回「本块真正需要建网格的图层」。
        /// 判定与 CreateLayers 里的跳过条件一致：某图层只要有一个顶点属于它就需要它的网格；
        /// 水面层（最后一层）只要有一个顶点 water &gt; 0 就需要。
        /// </summary>
        bool[] ResolveUsedLayers(int maxLayer)
        {
            bool[] used = new bool[maxLayer];
            MapData.VertexData[][] vm = map.mapData.vertexDatas;
            if (vm == null || vm.Length == 0) { used[0] = true; return used; }

            int vxMax = vm.Length;
            int vyMax = vm[0].Length;
            int waterLayer = maxLayer - 1;

            int xs = Math.Max(0, startCoords.x);
            int ys = Math.Max(0, startCoords.y);
            int xe = Math.Min(startCoords.x + quadBounds.x, vxMax - 1);
            int ye = Math.Min(startCoords.y + quadBounds.y, vyMax - 1);
            for (int x = xs; x <= xe; x++)
            {
                MapData.VertexData[] col = vm[x];
                for (int y = ys; y <= ye; y++)
                {
                    MapData.VertexData v = col[y];
                    int t = v.textureIndex;
                    if (t >= 0 && t < maxLayer)
                        used[t] = true;
                    if (v.water > 0)
                        used[waterLayer] = true;
                }
            }
            return used;
        }

        /// <summary>离开视野后的累计时长（秒），由 MapTerrain 驱动，用于空闲卸载。</summary>
        public float idleTime;

        /// <summary>本块当前是否持有已建好的网格/GameObject。</summary>
        public bool HasMesh()
        {
            return gameObject != null;
        }

        /// <summary>
        /// 补齐被 ReleaseMesh 释放掉的 Mesh 实体（GameObject / Renderer 都还留着）。
        /// </summary>
        void EnsureMeshes(int maxLayer)
        {
            bool[] used = ResolveUsedLayers(maxLayer);
            for (int i = 0; i < maxLayer; ++i)
            {
                if (!used[i] || mesh[i] != null)
                    continue;

                Mesh tempMesh = new Mesh();
                tempMesh.MarkDynamic();
                tempMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh[i] = tempMesh;

                MeshRenderer r = meshRenderer != null ? meshRenderer[i] : null;
                if (r != null)
                {
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf != null)
                        mf.sharedMesh = tempMesh;
                }
            }
        }

        /// <summary>
        /// 释放本块的网格实体（顶点数据仍在地图数据里），下次进入视野会自动重建。
        ///
        /// 只销毁 Mesh 本体、保留 GameObject：这样 MapObject.position / worldBounds
        /// （依赖 gameObject.transform）依然可用，不会在 MapModels.Remove / ClearAllModels
        /// 这类遍历里空引用。实测网格是每块内存占用的绝对大头（约 95%），
        /// 保留 GameObject 几乎不损失收益。
        /// </summary>
        public void ReleaseMesh()
        {
            // 后台分层构建中不要拆，否则回调会踩到半成品
            if (IsInThreadLoad() || newThrd != null)
                return;
            if (mesh == null)
                return;

            for (int i = 0; i < mesh.Length; ++i)
            {
                if (mesh[i] == null)
                    continue;

                if (meshRenderer != null && meshRenderer[i] != null)
                {
                    meshRenderer[i].enabled = false;
                    if (meshCollider != null && meshCollider[i] != null)
                        meshCollider[i].sharedMesh = null;
                }
                GameObject.Destroy(mesh[i]);
                mesh[i] = null;
            }

            if (lmdCache != null)
            {
                lmdCache.Clear();       // 归还 LayerMeshData 对象池
                lmdCache = null;
            }
            threadBeginLayer = 0;
            threadLoadDone = false;
            threadLoadStart = false;
            idleTime = 0f;
        }

        public override void Clear()
        {
            if (gameObject != null)
                GameObject.Destroy(gameObject);
            if (mesh != null)
            {
                for (int i = 0; i < mesh.Length; ++i)
                {
                    if (mesh[i] != null)
                        GameObject.Destroy(mesh[i]);
                }
            }
        }

        public void PrepareDatas(bool needInit = true)
        {
            if (needInit)
                Init();
            if (lmdCache == null)
            {
                int _vertex_x_max = quadBounds.x + 1;
                int _vertex_y_max = quadBounds.y + 1;
                int maxCount = _vertex_x_max * _vertex_y_max;
                lmdCache = LayerMeshData.Create();
                lmdCache.colorCount = maxCount;
                lmdCache.uvCount = maxCount;
                lmdCache.vertexCount = maxCount;
                lmdCache.normalCount = maxCount;
                lmdCache.triangleCount = maxCount * 6;
            }
            threadLoadStart = false;
            int maxLayer = map.mapLayer.layerDatas.Length;
            for (int i = 0; i < maxLayer; ++i)
            {
                if (mesh[i] == null) continue;
                threadBeginLayer = i;
                _PrepareDatas(null);
                OnAsyncPrepareDatasDone(threadBeginLayer, lmdCache);
            }

            threadBeginLayer = maxLayer;
            OnLoadDone();
        }
        void _PrepareDatas(object stateInfo)
        {
            threadLoadDone = false;
            CreateLayers(threadBeginLayer, lod);
            threadLoadDone = true;
        }

        Thread newThrd;
        bool threadLoadStart = false;

        public Tools.Rect bounds { get; set; }
        public Sango.Tools.Rect worldBounds
        {
            get
            {
                Vector3 pos = position;
                return new Tools.Rect(pos.z, pos.x, bounds.width, bounds.height);
            }
        }
        public int objId { get; set; }
        public int objType { get; set; }
        public int modelId { get; set; }
        public string modelAsset { get; set; }
        public int bindId { get; set; }
        public bool selectable { get; set; }
        public bool isStatic { get; set; }

        bool _visibleChanged = false;
        public bool visible
        {
            get
            {
                return _visible;
            }
            set
            {
                if (_visible != value)
                {
                    _visible = value;
                    _visibleChanged = true;
                }
            }
        }
        public Vector3 position { get; set; }
        public Vector3 rotation { get; set; }
        public Vector3 forward { get; set; }
        public Vector3 scale { get; set; }
        public Vector2Int coords { get; set; }
        public MapRender manager { get { return map; } set { map = value; } }
        public bool remainInView { get; set; }

        public void EditorShow(bool b) { }
        public void SetOutlineShow(Material material)
        {
            if (material == null)
            {
                for (int i = 0; i < meshRenderer.Length; ++i)
                {
                    MeshRenderer r = (MeshRenderer)meshRenderer[i];
                    if (i < meshRenderer.Length - 1)
                    {
                        MapLayer.LayerData layerData = map.mapLayer.layerDatas[i];
                        r.sharedMaterial = layerData.material;
                    }
                }
            }
            else
            {
                for (int i = 0; i < meshRenderer.Length; ++i)
                {
                    MeshRenderer r = (MeshRenderer)meshRenderer[i];
                    if (i < meshRenderer.Length - 1)
                    {
                        MapLayer.LayerData layerData = map.mapLayer.layerDatas[i];
                        r.sharedMaterials = new Material[2]{
                            layerData.material,
                            material
                          };
                    }
                }
            }

        }

        public void AsyncPrepareDatas()
        {
            if (lmdCache == null)
            {
                int _vertex_x_max = quadBounds.x + 1;
                int _vertex_y_max = quadBounds.y + 1;
                int maxCount = _vertex_x_max * _vertex_y_max;
                lmdCache = LayerMeshData.Create();
                lmdCache.colorCount = maxCount;
                lmdCache.uvCount = maxCount;
                lmdCache.vertexCount = maxCount;
                lmdCache.normalCount = maxCount;
                lmdCache.triangleCount = maxCount * 6;
            }

            threadLoadStart = true;
            ThreadPool.QueueUserWorkItem(new WaitCallback(_PrepareDatas));
        }

        public void SetLod(int l)
        {
            lod = l;

            //if (newThrd != null) {
            //    newThrd.Abort();
            //    newThrd = null;
            //}
            //threadBeginLayer = 0;
            //AsyncPrepareDatas();
        }

        //public void SetVisible(bool b)
        //{
        //    if (visible != b)
        //        visible = b;

        //    if (IsValid()) {
        //        for (int i = 0; i < mesh.Length; ++i) {
        //            MeshRenderer r = meshRenderer[i];
        //            if (r != null && r.enabled != b) {
        //                r.enabled = b;
        //            }
        //        }
        //    }
        //    else {
        //        if (!IsInThreadLoad()) {
        //            Init();
        //            AsyncPrepareDatas();
        //        }
        //    }
        //}

        public bool IsInThreadLoad()
        {
            return threadLoadStart;
        }

        public bool IsValid()
        {
            if (mesh == null) return false;
            return threadBeginLayer >= mesh.Length;
        }

        void OnLoadDone()
        {
            for (int i = 0; i < mesh.Length; ++i)
            {
                MeshRenderer r = meshRenderer[i];
                if (r != null && r.enabled != visible)
                {
                    r.enabled = visible;
                }
            }

            lmdCache.Clear();
            if (!MapEditor.IsEditOn)
                lmdCache = null;
        }

        void OnAsyncPrepareDatasDone(int layer, LayerMeshData lmd)
        {
            if (newThrd != null)
            {
                newThrd.Abort();
                newThrd = null;
            }

            MeshRenderer r = meshRenderer[layer];
            if (r == null) return;

            if (lmd.IsValid())
            {
                lmd.UpdateMesh(mesh[layer]);

                if (meshCollider[layer] == null)
                {
                    MeshCollider co = meshRenderer[layer].gameObject.AddComponent<MeshCollider>();
                    co.sharedMesh = mesh[layer];
                    meshCollider[layer] = co;
                }
                else
                {
                    meshCollider[layer].sharedMesh = mesh[layer];
                }


                if (r.enabled != visible)
                    r.enabled = visible;
                //meshRenderer[layer].gameObject.SetActive(true);
            }
            else
            {
                if (!MapEditor.IsEditOn)
                {
                    GameObject.DestroyImmediate(mesh[layer]);
                    GameObject.Destroy(r.gameObject);
                    mesh[layer] = null;
                    meshRenderer[layer] = null;
                    meshCollider[layer] = null;

                    //if (r.enabled)
                    //    r.enabled = false;
                    //meshRenderer[layer].gameObject.SetActive(false);
                }
            }

        }

        void AddVertex(ref MapData.VertexData vtex, int x, int y, int layer, int lowestLayer, bool layerIsWater)
        {
            // color
            if (vtex.textureIndex == layer || layer == lowestLayer || (layerIsWater && vtex.water > 0))
            {
                lmdCache.colorCache.Add(color_white);
            }
            else
            {
                lmdCache.colorCache.Add(color_black);
            }
            lmdCache.uvCache.Add(map.mapData.VertexUV(x, y));

            if (layerIsWater)
            {
                lmdCache.vertexCache.Add(map.mapData.VertexWaterPosition(vtex, x, y) - position);
                lmdCache.normalCache.Add(Vector3.up);

            }
            else
            {
                lmdCache.vertexCache.Add(map.mapData.VertexPosition(vtex.height, x, y) - position);
                lmdCache.normalCache.Add(map.mapData.VertexNormal(vtex, x, y));
            }
        }

        public void CreateLayers(int layer, int lod)
        {
            if (mesh[layer] == null) return;

            byte bLayer = (byte)layer;
            int x_end = startCoords.x + quadBounds.x;
            int y_end = startCoords.y + quadBounds.y;
            int maxLayer = mesh.Length;
            bool layerIsWater = (layer == (maxLayer - 1));
            if (layerIsWater)
                lod = 2;

            Array.Clear(xIndexCache, 0, xIndexCache.Length);
            Array.Clear(yIndexCache, 0, yIndexCache.Length);
            int lodL = 1 << lod;
            lmdCache.Reset();
            MapData.VertexData[][] vertexMap = map.mapData.vertexDatas;
            int maxIndex = 0;

            for (int x = startCoords.x; x < x_end; x += lodL)
            {
                xIndexCache[0] = yIndexCache[0];
                xIndexCache[1] = 0;
                Array.Clear(diffyIndexCache, 0, yIndexCache.Length);

                for (int y = startCoords.y; y < y_end; y += lodL)
                {

                    int xL = x + lodL;
                    int yL = y + lodL;


                    MapData.VertexData start0 = vertexMap[x][y];
                    MapData.VertexData start1 = vertexMap[xL][y];
                    MapData.VertexData start2 = vertexMap[xL][yL];
                    MapData.VertexData start3 = vertexMap[x][yL];

                    int normalY = y - startCoords.y;
                    if ((!layerIsWater && start0.textureIndex != bLayer && start1.textureIndex != bLayer && start2.textureIndex != bLayer && start3.textureIndex != bLayer) ||
                        (layerIsWater && start0.water == 0 && start1.water == 0 && start2.water == 0 && start3.water == 0))
                    {
                        xIndexCache[0] = 0;
                        xIndexCache[1] = 0;
                        continue;
                    }

                    int lowestLayer = layerIsWater ? 0 : Math.Min(Math.Min(Math.Min(start0.textureIndex, start1.textureIndex), start2.textureIndex), start3.textureIndex);

                    int index0 = xIndexCache[0];
                    if (index0 == 0)
                    {
                        index0 = yIndexCache[normalY];
                        if (index0 == 0)
                        {
                            AddVertex(ref start0, x, y, layer, lowestLayer, layerIsWater);
                            index0 = maxIndex++;
                        }
                        else
                        {
                            if (lowestLayer == layer)
                            {
                                lmdCache.colorCache.Change(index0, color_white);
                            }
                        }
                    }
                    else
                    {
                        if (lowestLayer == layer)
                        {
                            lmdCache.colorCache.Change(index0, color_white);
                        }
                    }


                    int index1 = xIndexCache[1];
                    if (index1 == 0)
                    {
                        AddVertex(ref start1, xL, y, layer, lowestLayer, layerIsWater);
                        index1 = maxIndex++;
                    }
                    else
                    {
                        if (lowestLayer == layer)
                        {
                            lmdCache.colorCache.Change(index1, color_white);
                        }
                    }

                    yIndexCache[normalY] = index1;
                    diffyIndexCache[normalY] = true;
                    AddVertex(ref start2, xL, yL, layer, lowestLayer, layerIsWater);
                    int index2 = maxIndex++;
                    xIndexCache[1] = index2;


                    int index3 = yIndexCache[normalY + lodL];
                    if (index3 == 0)
                    {
                        AddVertex(ref start3, x, yL, layer, lowestLayer, layerIsWater);
                        index3 = maxIndex++;
                    }
                    else
                    {
                        if (lowestLayer == layer)
                        {
                            lmdCache.colorCache.Change(index3, color_white);
                        }
                    }

                    xIndexCache[0] = index3;
                    yIndexCache[normalY + lodL] = index2;
                    diffyIndexCache[normalY + lodL] = true;

                    lmdCache.triangleCache.Add(index0);
                    lmdCache.triangleCache.Add(index2);
                    lmdCache.triangleCache.Add(index3);
                    lmdCache.triangleCache.Add(index1);
                    lmdCache.triangleCache.Add(index2);
                    lmdCache.triangleCache.Add(index0);
                }

                for (int k = 0; k < diffyIndexCache.Length; ++k)
                {
                    if (!diffyIndexCache[k])
                        yIndexCache[k] = 0;
                }
            }
        }

        public override void Update()
        {
            if (Tools.MapEditor.IsEditOn)
            {
                _visible = true;
                _visibleChanged = true;
            }

            if (_visibleChanged)
            {
                _visibleChanged = false;

                if (meshRenderer != null)
                {
                    for (int i = 0; i < meshRenderer.Length; ++i)
                    {
                        MeshRenderer r = meshRenderer[i];
                        if (r != null && r.enabled != _visible)
                        {
                            r.enabled = _visible;
                        }
                    }
                }

                if (!IsValid())
                {
                    if (_visible && !IsInThreadLoad())
                    {
                        Init();
                        AsyncPrepareDatas();
                    }
                }
            }

            if (!threadLoadStart) return;
            if (true == threadLoadDone)
            {
                if (threadBeginLayer < mesh.Length)
                {

                    OnAsyncPrepareDatasDone(threadBeginLayer, lmdCache);
                    threadBeginLayer++;
                    threadLoadDone = false;
                    if (threadBeginLayer < mesh.Length)
                        AsyncPrepareDatas();
                }
                else
                {
                    threadLoadDone = false;
                    threadLoadStart = false;
                    OnLoadDone();
                }
            }
        }

        public bool Overlaps(Sango.Tools.Rect rect)
        {
            return worldBounds.Overlaps(rect);
        }
        public void OnClick()
        {
            throw new NotImplementedException();
        }

        public void OnPointerEnter()
        {
            throw new NotImplementedException();
        }

        public void OnPointerExit()
        {
            throw new NotImplementedException();
        }

        public void SetParent(Transform parent) { gameObject.transform.SetParent(parent); }
        public void SetParent(Transform parent, bool worldPositionStays) { gameObject.transform.SetParent(parent, worldPositionStays); }
        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void Destroy()
        {
            Clear();
        }

        public void CreateModel(string meshFile, string textureFile, string shaderName, bool isShareMat = true)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(string packagePath, string assetName)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(string assetName)
        {
            throw new NotImplementedException();
        }

        public void CreateModel(UnityEngine.Object modelObj)
        {
            throw new NotImplementedException();
        }

        public void ChangeModel(string newAsset)
        {
            throw new NotImplementedException();
        }

        public void ReLoadModels(bool checkAsset = true)
        {
            throw new NotImplementedException();
        }

        public void ClearModels()
        {
            throw new NotImplementedException();
        }

        public void ReCheckVisible()
        {
            throw new NotImplementedException();
        }
    }
}
