
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Sango;
using UnityEngine.Rendering;
using Sango.Core;
using Sango.Loader;
using Sango.Tools;

namespace Sango.Render
{
    // 光照
    public class MapModels : MapProperty
    {
        public List<IMapManageObject> dynamicObjects = new List<IMapManageObject>();
        public List<IMapManageObject> staticObjects = new List<IMapManageObject>();
        //public Dictionary<int, IMapManageObject> staticObjectMap = new Dictionary<int, IMapManageObject>();
        public Sango.Tools.QuadTree2D<IMapManageObject> staticObjectsQuadTree;
        private Tools.Rect curViewRect = new Tools.Rect();

        /// <summary>
        /// 植被的 objType。与 ModelBrush.objectTypeTitle 的下标一致：
        /// { 所有, 城, 关, 港, 内, 军, 植, 其他 }，6 = 植。
        /// </summary>
        public const int ObjTypePlant = 6;

        /// <summary>
        /// 植被实例的纯数据表示。
        ///
        /// 为什么不建 GameObject：
        ///   植被是纯静态装饰物——不可选中、无碰撞、不参与寻路与任何逻辑，它的全部作用就是"被画出来"。
        ///   而一个 GameObject 实例（GameObject + Transform + MonoBehaviour + 托管对象）在 64 位下要几百字节，
        ///   世界地图铺满植被后是十几万棵，仅此一项就要上百 MB，还会产生十几万个托管对象拖慢 GC。
        ///   这里只保留实例化渲染真正需要的几个量，单实例 24 字节，直接喂给
        ///   Graphics.DrawMeshInstanced（见 ModelInstanceData.DrawMesh），
        ///   渲染路径与原来完全一致，视觉上没有任何差别。
        ///
        /// 编辑态（MapEditor.IsEditOn）例外：笔刷要能选中/拖动/删除植被，也必须靠 GameObject 才能被存回地图，
        /// 所以编辑态下植被仍然走原来的 GameObject 路径，见 CreateModel。
        /// </summary>
        public struct PlantInstance
        {
            /// <summary>世界 X 坐标（对应格坐标的 y 方向，与 MapObject.worldBounds 的 Rect.y 同一轴）</summary>
            public float posX;
            /// <summary>世界 Y 坐标（地形高度）</summary>
            public float posY;
            /// <summary>世界 Z 坐标（对应格坐标的 x 方向，与 MapObject.worldBounds 的 Rect.x 同一轴）</summary>
            public float posZ;
            /// <summary>绕 Y 轴的朝向（角度制）</summary>
            public float rotY;
            /// <summary>等比缩放</summary>
            public float scale;
            /// <summary>模型 ID，取值 1..14，对应 modelInstanceDatas 的下标 modelId - 1</summary>
            public int modelId;
        }

        /// <summary>植被实例总数（纯数据实例，不含编辑态下的 GameObject 植被）</summary>
        public int plantCount { get; private set; }

        /// <summary>植被实例数据，按加载顺序紧凑排列</summary>
        private PlantInstance[] plantDatas;

        // ---------------- 植被视野剔除用的规则网格分桶 ----------------
        // 十几万棵植被若像 GameObject 那样逐个进 QuadTree，光树节点本身的内存就够呛，
        // 而且每个实例还得包一个 IMapManageObject。这里改用「规则网格 + 计数排序」，
        // 把实例下标压进连续数组，每帧只遍历视野覆盖到的桶：
        // 代价与视野内实例数成正比，与全图总量无关。
        /// <summary>分桶前缀和，长度 = 桶数 + 1；[b] 是桶 b 的起始下标，[b+1] 是结束下标</summary>
        private int[] plantBucketStart;
        /// <summary>按桶排好序的实例下标</summary>
        private int[] plantBucketItems;
        /// <summary>分桶边长（世界单位）</summary>
        private float plantBucketSize;
        /// <summary>分桶网格原点（世界 Z，对应 Rect.x）</summary>
        private float plantBucketOriginZ;
        /// <summary>分桶网格原点（世界 X，对应 Rect.y）</summary>
        private float plantBucketOriginX;
        /// <summary>分桶列数（世界 Z 方向）</summary>
        private int plantBucketCols;
        /// <summary>分桶行数（世界 X 方向）</summary>
        private int plantBucketRows;
        /// <summary>分桶索引是否需要在下次绘制前重建</summary>
        private bool plantBucketsDirty;

        /// <summary>
        /// 单轴向分桶数量。分得越细单帧遍历的无效实例越少，但前缀和数组本身也占内存；
        /// 128×128 = 16384 个桶时前缀和只占 64KB，而世界地图十几万棵植被平均每桶不到 10 棵。
        /// </summary>
        private const int PlantBucketCountPerAxis = 128;

        /// <summary>
        /// 植被逐个精测时的外扩余量。与 GameObject 路径下 obj.bounds = Rect(0, 0, 32, 32)
        /// 的半边长（16）对齐并取整，避免相机移动时桶边缘的植被在可见边界上闪烁。
        /// </summary>
        private const float PlantCullMargin = 32f;

        /// <summary>植被数据数组的初始容量，之后按 2 倍扩容</summary>
        private const int PlantInitialCapacity = 1024;

        public class ModelInstanceData
        {
            /// <summary>
            /// 本帧要绘制的实例矩阵缓冲。容量只与「同时可见的实例数」相关，与全图实例总数无关。
            /// 布局：[0, gameObjectCount) 是视野内的 GameObject 实例（城/关/港/建筑），
            /// 其后是视野内的植被实例（见 AppendPlant）。
            /// </summary>
            public Matrix4x4[] _matrixes = new Matrix4x4[1];
            public Mesh mesh;
            public Material material;
            public Material[] seasonMaterials = new Material[4];
            public int showCount;
            public MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
            public List<IMapManageObject> drawObject = new List<IMapManageObject>();
            public UnityEngine.Object @object;

            /// <summary>
            /// 挂在本模型上的植被实例总数（纯数据实例，见 MapModels.PlantInstance）。
            /// 大于 0 说明本模型每帧都要重建可见矩阵。
            /// </summary>
            public int plantTotal;

            bool needUpdateMatrixes = false;

            public void Add(IMapManageObject mapObject)
            {
                drawObject.Add(mapObject);
                needUpdateMatrixes = true;
            }

            public void Remove(IMapManageObject mapObject)
            {
                drawObject.Remove(mapObject);
                needUpdateMatrixes = true;
            }

            /// <summary>
            /// 确保矩阵缓冲至少能装下 need 个实例。
            /// 2 倍扩容：容量只与「同时可见的实例数」相关，不会因为全图有十几万棵植被就撑爆内存。
            /// 注意 Matrix4x4 是结构体，数组新建时元素已经是零矩阵，不需要再逐个 new 一遍。
            /// </summary>
            private void EnsureMatrixCapacity(int need)
            {
                if (need <= _matrixes.Length)
                    return;

                int destLen = _matrixes.Length > 0 ? _matrixes.Length : 1;
                while (destLen < need)
                    destLen *= 2;

                Matrix4x4[] grown = new Matrix4x4[destLen];
                System.Array.Copy(_matrixes, grown, showCount);
                _matrixes = grown;
            }

            /// <summary>
            /// 把视野内的 GameObject 实例写进矩阵缓冲。植被实例由 MapModels.DrawPlants 随后追加。
            /// </summary>
            /// <param name="force">是否强制重建（忽略脏标记）</param>
            public void UpdateMatrixes(bool force)
            {
                if (!force && !needUpdateMatrixes)
                    return;

                int total = drawObject.Count;
                EnsureMatrixCapacity(total);

                showCount = 0;
                for (int i = 0; i < total; i++)
                {
                    IMapManageObject mapManageObject = drawObject[i];
                    // 只提交视野内的实例。全量提交时 draw call 会随「全图实例数」线性膨胀：
                    // 世界地图 1:1 铺植被后单模型上万棵，会直接把帧率拖垮。
                    // visible 由 MapModels.Update(rect) 每帧按视野矩形刷新，且在同一次
                    // Update 内先于 DrawMesh 执行，所以这里读到的一定是本帧的结果。
                    if (!mapManageObject.visible)
                        continue;

                    Transform node = mapManageObject.transform;
                    Matrix4x4 mx = _matrixes[showCount];
                    mx.SetTRS(
                        node.position,
                        node.rotation,
                        node.lossyScale
                        );
                    _matrixes[showCount] = mx;
                    showCount++;
                }

                needUpdateMatrixes = false;
            }

            public void UpdateSeason(int season)
            {
                Material mat = seasonMaterials[season];
                if (mat != null)
                    material = mat;
            }

            /// <summary>Graphics.DrawMeshInstanced 单批上限，超出 Unity 会报错且整批不绘制。</summary>
            public const int MaxInstancesPerDraw = 1023;

            /// <summary>
            /// 超过单批上限时的中转缓冲。懒分配：只有真的需要分批（同时可见实例 &gt; 1023）时才申请，
            /// 否则 14 个模型光这一个缓冲就要白占 14 × 1023 × 64B ≈ 896KB 常驻内存。
            /// </summary>
            private Matrix4x4[] drawBatchBuffer;

            /// <summary>
            /// 本模型是否每帧都得重建可见矩阵。
            /// 只要挂了植被（MapModels.PlantInstance）就必须每帧重建——相机一移动可见集合就变了；
            /// GameObject 实例则只有超过一批上限时才需要，数量少的（城池、标注）沿用脏标记即可。
            /// </summary>
            private bool NeedRefreshEveryFrame
            {
                get { return plantTotal > 0 || drawObject.Count > MaxInstancesPerDraw; }
            }

            /// <summary>
            /// 本帧绘制准备：先把视野内的 GameObject 实例写进矩阵缓冲。
            /// 必须在 MapModels.DrawPlants 之前调用——它会重置 showCount 并从 [0] 开始写，
            /// 而植被是往缓冲尾部追加的。
            /// </summary>
            public void PrepareDraw()
            {
                UpdateMatrixes(NeedRefreshEveryFrame);
            }

            /// <summary>
            /// 追加一个植被实例的变换矩阵（由 MapModels.DrawPlants 调用）。
            /// 植被只绕 Y 轴旋转且等比缩放，所以每帧现算 TRS 比常驻存一整份 Matrix4x4（64B）更省内存：
            /// 十几万棵植被存矩阵要多花约 6MB，而视野内通常只有几百棵，现算的开销可以忽略。
            /// </summary>
            /// <param name="plant">植被实例数据</param>
            public void AppendPlant(PlantInstance plant)
            {
                EnsureMatrixCapacity(showCount + 1);

                _matrixes[showCount] = Matrix4x4.TRS(
                    new Vector3(plant.posX, plant.posY, plant.posZ),
                    Quaternion.Euler(0f, plant.rotY, 0f),
                    new Vector3(plant.scale, plant.scale, plant.scale));
                showCount++;
            }

            /// <summary>
            /// 提交本帧的实例化绘制。调用前必须已经备好矩阵（见 PrepareDraw / AppendPlant）。
            /// </summary>
            public void DrawMesh()
            {
                if (mesh == null || showCount <= 0) return;

                if (SystemInfo.supportsInstancing)
                {
                    for (int start = 0; start < showCount; start += MaxInstancesPerDraw)
                    {
                        int n = Mathf.Min(MaxInstancesPerDraw, showCount - start);
                        Matrix4x4[] batch = _matrixes;
                        if (start > 0)
                        {
                            // DrawMeshInstanced 没有起始偏移参数，只能拷一段出去
                            if (drawBatchBuffer == null || drawBatchBuffer.Length < n)
                                drawBatchBuffer = new Matrix4x4[n];
                            System.Array.Copy(_matrixes, start, drawBatchBuffer, 0, n);
                            batch = drawBatchBuffer;
                        }
                        Graphics.DrawMeshInstanced(mesh, 0, material, batch, n, _mpb, ShadowCastingMode.On, true, 0);
                    }
                }
                else
                {
                    for (int i = 0; i < showCount; i++)
                    {
                        Graphics.DrawMesh(mesh, _matrixes[i], material, 0);
                    }
                }
            }
        }

        public ModelInstanceData[] modelInstanceDatas;

        public void AddInstance(IMapManageObject obj)
        {
            ModelInstanceData modelInstanceData = modelInstanceDatas[obj.modelId - 1];
            modelInstanceData.Add(obj);
        }

        public void RemoveInstance(IMapManageObject obj)
        {
            ModelInstanceData modelInstanceData = modelInstanceDatas[obj.modelId - 1];
            modelInstanceData.Remove(obj);
        }

        public MapModels(MapRender map) : base(map)
        {

        }

        public override void Init()
        {
            base.Init();

            Tools.Rect bounds = new Tools.Rect(0, 0, map.mapData.world_width, map.mapData.world_height);

            // 初始化quadTree
            staticObjectsQuadTree = new Tools.QuadTree2D<IMapManageObject>(bounds, 8);

            // 植被数据清空。注意分桶网格不在这里构建：
            // Init() 跑在 mapData.OnLoad() 之前（见 MapRender.LoadMap），此时 quadSize 与世界尺寸
            // 还是上一张图甚至默认值，用它算出来的网格会盖不全本图植被。
            // 所以分桶推迟到全部植被读完之后，且范围取自实例的实际包围盒，见 BuildPlantBuckets。
            plantCount = 0;
            plantDatas = null;
            plantBucketStart = null;
            plantBucketItems = null;
            plantBucketsDirty = false;

            modelInstanceDatas = new ModelInstanceData[14];
            for (int i = 0; i < modelInstanceDatas.Length; i++)
            {
                ModelInstanceData instanceData = new ModelInstanceData();
                modelInstanceDatas[i] = instanceData;

                ModelConfig config = GameData.Instance.ModelConfigs.Get(i + 1);
                GameObject obj = ObjectLoader.LoadObject<GameObject>(config.model);
                if (obj != null)
                {
                    MeshFilter meshFilter = obj.GetComponentInChildren<MeshFilter>(true);
                    instanceData.mesh = meshFilter.sharedMesh;
                    MeshRenderer renderer = obj.GetComponentInChildren<MeshRenderer>(true);
                    instanceData.material = renderer.sharedMaterial;

                    string name = instanceData.material.name;
                    string seasonNameFix = name.Remove(name.LastIndexOf("_") + 1);
                    for (int j = 0; j < 4; j++)
                    {
                        string matName = $"Assets/Model/Materials/{seasonNameFix}{MapRender.SeasonNames[j].ToLower()}.mat";
                        instanceData.seasonMaterials[j] = ObjectLoader.LoadObject<Material>(matName);
                    }
                    instanceData.@object = obj;
                }
            }
        }

        public override void Clear()
        {
            base.Clear();
            ClearAllModels();
            foreach (IMapManageObject o in dynamicObjects)
            {
                o.visible = false;
            }
            dynamicObjects.Clear();
        }

        public void SetOutLineShow(Material terrainOutlineMat)
        {
            foreach (IMapManageObject obj in staticObjects)
            {
                obj.SetOutlineShow(terrainOutlineMat);
            }
            foreach (IMapManageObject obj in dynamicObjects)
            {
                obj.SetOutlineShow(terrainOutlineMat);
            }
        }

        public void AddDynamic(IMapManageObject obj)
        {
            obj.manager = map;
            obj.isStatic = false;
            obj.isAdded = true;
            dynamicObjects.Add(obj);
            obj.visible = obj.Overlaps(curViewRect);
        }

        public void AddStatic(IMapManageObject obj)
        {
            if (MapEditor.IsEditOn)
            {
                AddDynamic(obj);
                return;
            }

            //if (staticObjectMap.TryAdd(obj.objId, obj)) {
            staticObjects.Add(obj);
            obj.isStatic = true;
            obj.isAdded = true;
            obj.manager = map;
            staticObjectsQuadTree.Add(obj, obj.worldBounds);
            obj.visible = obj.Overlaps(curViewRect);
            //}
        }

        public void RemoveStatic(IMapManageObject obj)
        {
            if (MapEditor.IsEditOn)
            {
                RemoveDynamic(obj);
                return;
            }

            if (staticObjects.Remove(obj))
            {
                staticObjectsQuadTree.Remove(obj, obj.worldBounds);
                //staticObjectMap.Remove(obj.objId);
            }
        }

        public void Remove(IMapManageObject obj)
        {
            if (obj.isStatic)
            {
                if (staticObjects.Remove(obj))
                {
                    staticObjectsQuadTree.Remove(obj, obj.worldBounds);
                    //staticObjectMap.Remove(obj.objId);
                }
            }
            else
            {
                dynamicObjects.Remove(obj);
            }
        }
        public void RemoveDynamic(IMapManageObject obj)
        {
            dynamicObjects.Remove(obj);
        }

        public bool IsInView(IMapManageObject obj)
        {
            return obj.Overlaps(curViewRect);
        }

        public void CreateModel(int id, int objType, int bindId, int modelId, Vector3 position, Vector3 rot, Vector3 scale)
        {
            if (modelId == 0) return;

            // 植被走纯数据通道：不建 GameObject，直接进实例化渲染，见 PlantInstance 的注释。
            // 编辑态例外——笔刷要能选中/拖动/删除植被，也必须靠 GameObject 才能被存回地图，
            // 所以 MapEditor.IsEditOn 为真时保持原来的 GameObject 路径。
            if (objType == ObjTypePlant && !MapEditor.IsEditOn)
            {
                AddPlant(modelId, position, rot, scale);
                return;
            }

            MapObject obj = MapObject.Create(modelId);
            obj.objId = id;
            obj.objType = objType;
            obj.bindId = bindId;
            obj.modelId = modelId;
            obj.transform.position = position;
            obj.transform.rotation = Quaternion.Euler(rot);
            obj.transform.localScale = scale;
            obj.bounds = new Sango.Tools.Rect(0, 0, 32, 32);

            obj.instanceFlag = !MapEditor.IsEditOn;

            //map.LoadModel(obj);
            if (bindId > 0)
                map.BindModel(obj);

            AddStatic(obj);
        }

        /// <summary>
        /// 追加一棵植被实例（纯数据，不产生 GameObject）。
        /// 这里只做数据收集，分桶统一推迟到绘制前（见 BuildPlantBuckets），
        /// 避免 OnLoad 读十几万条记录的过程中反复重建索引。
        /// </summary>
        /// <param name="modelId">模型 ID</param>
        /// <param name="position">世界坐标</param>
        /// <param name="rot">欧拉角（只用 Y 分量）</param>
        /// <param name="scale">缩放（植被是等比缩放，只用 Y 分量）</param>
        private void AddPlant(int modelId, Vector3 position, Vector3 rot, Vector3 scale)
        {
            int index = modelId - 1;
            if (index < 0 || index >= modelInstanceDatas.Length)
                return;

            if (plantDatas == null)
                plantDatas = new PlantInstance[PlantInitialCapacity];
            else if (plantCount >= plantDatas.Length)
            {
                PlantInstance[] grown = new PlantInstance[plantDatas.Length * 2];
                System.Array.Copy(plantDatas, grown, plantCount);
                plantDatas = grown;
            }

            PlantInstance plant = new PlantInstance();
            plant.posX = position.x;
            plant.posY = position.y;
            plant.posZ = position.z;
            plant.rotY = rot.y;
            plant.scale = scale.y;
            plant.modelId = modelId;

            plantDatas[plantCount] = plant;
            plantCount++;
            plantBucketsDirty = true;

            modelInstanceDatas[index].plantTotal++;
        }

        /// <summary>
        /// 把植被数据数组收缩到实际数量。
        /// AddPlant 是 2 倍扩容，载入结束时最多会多留一半容量：世界地图十四万棵就是好几 MB 的纯浪费。
        /// 只在 OnLoad 结束时调用一次，避免运行期逐棵 Append 时反复扩容/收缩。
        /// </summary>
        private void TrimPlants()
        {
            if (plantDatas == null || plantCount == 0 || plantDatas.Length == plantCount)
                return;

            PlantInstance[] trimmed = new PlantInstance[plantCount];
            System.Array.Copy(plantDatas, trimmed, plantCount);
            plantDatas = trimmed;
        }

        /// <summary>释放全部植被实例数据与分桶索引。</summary>
        private void ClearPlants()
        {
            plantCount = 0;
            plantDatas = null;
            plantBucketStart = null;
            plantBucketItems = null;
            plantBucketsDirty = false;

            if (modelInstanceDatas == null) return;
            for (int i = 0; i < modelInstanceDatas.Length; i++)
                modelInstanceDatas[i].plantTotal = 0;
        }

        public void InitModel(int id, Vector2Int coords, float height, Vector3 rot, Vector3 scale)
        {

        }

        public void ClearAllModels()
        {
            foreach (IMapManageObject o in staticObjects)
            {
                o.visible = false;
            }

            // 整体重置四叉树：逐个 Remove 内部是 List.IndexOf，物件多时会退化成 O(n²)，
            // 换图/关图时会长时间卡死。
            staticObjectsQuadTree.Clear();
            staticObjects.Clear();
            dynamicObjects.Clear();

            ClearPlants();
        }

        internal override void OnSave(BinaryWriter writer)
        {
            List<IMapManageObject> validList = dynamicObjects.FindAll(x => x.canSave && (x.objId > 0 || x.objType > 0 || x.modelId > 0));
            writer.Write(validList.Count);
            for (int i = 0; i < validList.Count; ++i)
            {
                IMapManageObject obj = validList[i];
                writer.Write(obj.objId);
                writer.Write(obj.objType);
                writer.Write(obj.bindId);
                writer.Write(obj.modelId);
                writer.Write(obj.position.x);
                writer.Write(obj.position.y);
                writer.Write(obj.position.z);
                writer.Write(obj.rotation.x);
                writer.Write(obj.rotation.y);
                writer.Write(obj.rotation.z);
                writer.Write(obj.scale.x);
                writer.Write(obj.scale.y);
                writer.Write(obj.scale.z);
            }
        }

        internal override void OnSaveScale(BinaryWriter writer, int scale)
        {
            List<IMapManageObject> validList = dynamicObjects.FindAll(x => x.canSave && (x.objId > 0 || x.objType > 0 || x.modelId > 0));
            writer.Write(validList.Count);
            for (int i = 0; i < validList.Count; ++i)
            {
                IMapManageObject obj = validList[i];
                writer.Write(obj.objId);
                writer.Write(obj.objType);
                writer.Write(obj.bindId);
                writer.Write(obj.modelId);
                writer.Write(obj.position.x * scale);
                writer.Write(obj.position.y);
                writer.Write(obj.position.z * scale);
                writer.Write(obj.rotation.x * scale);
                writer.Write(obj.rotation.y);
                writer.Write(obj.rotation.z * scale);
                writer.Write(obj.scale.x);
                writer.Write(obj.scale.y);
                writer.Write(obj.scale.z);
            }
        }



        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            float x, y, w, h;
            if (map.mapCamera.GetViewRect(map.showLimitLength, out x, out y, out w, out h))
            {
                curViewRect.Set(x, y, w, h);
            }

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int objId = reader.ReadInt32();
                int objType = reader.ReadInt32();
                int bindId = 0;
                if (versionCode >= 4)
                    bindId = reader.ReadInt32();
                int modelId = reader.ReadInt32();
                Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 rot = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Vector3 scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                CreateModel(objId, objType, bindId, modelId, pos, rot, scale);
            }

            // 全部记录读完后收尾：收缩植被数组，并在首次绘制前一次性构建分桶索引
            TrimPlants();
            BuildPlantBuckets();
        }

        public override void UpdateRender()
        {
            for (int i = 0; i < modelInstanceDatas.Length; i++)
            {
                modelInstanceDatas[i].UpdateSeason(curSeason);
            }
        }

        public void EditorShow(bool b)
        {
            for (int i = 0; i < staticObjects.Count; ++i)
            {
                IMapManageObject obj = staticObjects[i];
                obj.EditorShow(b);
            }
        }

        private IMapManageObject[][] mapManageObjects = new IMapManageObject[2][]
        {
              new IMapManageObject[8096],
              new IMapManageObject[8096]
        };
        private int[] mapManageObjectsCount = { 0, 0 };
        private int manageLastIndex = 0;


        public void Update(Tools.Rect rect)
        {
            curViewRect = rect;

            int lastCount = mapManageObjectsCount[manageLastIndex];
            IMapManageObject[] lastList = mapManageObjects[manageLastIndex];
            for (int i = 0; i < lastCount; i++)
            {
                lastList[i].remainInView = false;
            }

            int newIndex = (manageLastIndex + 1) % 2;
            IMapManageObject[] newList = mapManageObjects[newIndex];

            int newCount = staticObjectsQuadTree.Find(rect, ref newList, true);
            // Find 内部用 Array.Resize 扩容时换的是新数组，必须把它写回缓冲区，
            // 否则下一帧又从 8096 重新分配、重新拷贝一遍。
            mapManageObjects[newIndex] = newList;
            mapManageObjectsCount[newIndex] = newCount;
            for (int i = 0; i < newCount; i++)
            {
                newList[i].remainInView = true;
            }

            for (int i = 0; i < lastCount; i++)
            {
                IMapManageObject obj = lastList[i];
                if (obj.manager != null && !obj.remainInView)
                {
                    obj.visible = false;
                }
            }

            for (int i = 0; i < newCount; i++)
            {
                newList[i].visible = true;
            }
            manageLastIndex = newIndex;

            for (int i = 0; i < dynamicObjects.Count; i++)
            {
                IMapManageObject dynamicObj = dynamicObjects[i];
                if (dynamicObj != null)
                {
                    dynamicObj.visible = dynamicObj.Overlaps(rect);
                }
            }

            // 顺序不能换：先由各模型把视野内的 GameObject 实例写进矩阵缓冲（从下标 0 开始写），
            // 再由植被往缓冲尾部追加，最后统一提交绘制。
            for (int i = 0; i < modelInstanceDatas.Length; i++)
            {
                modelInstanceDatas[i].PrepareDraw();
            }

            DrawPlants(rect);

            for (int i = 0; i < modelInstanceDatas.Length; i++)
            {
                modelInstanceDatas[i].DrawMesh();
            }

        }

        /// <summary>
        /// 把视野内的植被实例追加到对应模型的矩阵缓冲。
        ///
        /// 剔除方式：规则网格分桶 + 逐实例矩形精测。
        /// 网格分桶让每帧的代价只与「视野覆盖到的桶」成正比，与全图植被总量无关；
        /// 逐实例精测把桶边界上多出来的那部分剔除掉（桶是方的，视野不一定是）。
        /// </summary>
        /// <param name="rect">视野矩形（x 轴对应世界 Z，y 轴对应世界 X，与 MapObject.worldBounds 同一套平面）</param>
        private void DrawPlants(Tools.Rect rect)
        {
            if (plantCount == 0) return;

            if (plantBucketsDirty)
                BuildPlantBuckets();

            if (plantBucketItems == null || plantBucketItems.Length == 0) return;

            float minZ = rect.xMin - PlantCullMargin;
            float maxZ = rect.xMax + PlantCullMargin;
            float minX = rect.yMin - PlantCullMargin;
            float maxX = rect.yMax + PlantCullMargin;

            int col0 = PlantBucketClamp((minZ - plantBucketOriginZ) / plantBucketSize, plantBucketCols);
            int col1 = PlantBucketClamp((maxZ - plantBucketOriginZ) / plantBucketSize, plantBucketCols);
            int row0 = PlantBucketClamp((minX - plantBucketOriginX) / plantBucketSize, plantBucketRows);
            int row1 = PlantBucketClamp((maxX - plantBucketOriginX) / plantBucketSize, plantBucketRows);

            for (int row = row0; row <= row1; row++)
            {
                int rowBase = row * plantBucketCols;
                for (int col = col0; col <= col1; col++)
                {
                    int bucket = rowBase + col;
                    int end = plantBucketStart[bucket + 1];
                    for (int i = plantBucketStart[bucket]; i < end; i++)
                    {
                        PlantInstance plant = plantDatas[plantBucketItems[i]];
                        if (plant.posZ < minZ || plant.posZ > maxZ) continue;
                        if (plant.posX < minX || plant.posX > maxX) continue;

                        int index = plant.modelId - 1;
                        if (index < 0 || index >= modelInstanceDatas.Length) continue;
                        modelInstanceDatas[index].AppendPlant(plant);
                    }
                }
            }
        }

        /// <summary>把世界坐标映射到分桶下标，并夹紧到有效范围。</summary>
        private static int PlantBucketClamp(float value, int count)
        {
            int index = (int)value;
            if (index < 0) return 0;
            if (index >= count) return count - 1;
            return index;
        }

        /// <summary>
        /// 构建植被分桶索引（计数排序，O(n)）。
        ///
        /// 网格范围取自实例的实际包围盒，而不是地图尺寸：
        /// Init() 跑在 mapData.OnLoad() 之前，那会儿 quadSize / 世界尺寸还是上一张图甚至默认值，
        /// 用它算出来的网格会盖不全本图植被，边界外的实例会被整片丢掉。
        /// </summary>
        private void BuildPlantBuckets()
        {
            plantBucketsDirty = false;

            if (plantCount == 0)
            {
                plantBucketStart = null;
                plantBucketItems = null;
                return;
            }

            float minZ = float.MaxValue, maxZ = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < plantCount; i++)
            {
                PlantInstance plant = plantDatas[i];
                if (plant.posZ < minZ) minZ = plant.posZ;
                if (plant.posZ > maxZ) maxZ = plant.posZ;
                if (plant.posX < minX) minX = plant.posX;
                if (plant.posX > maxX) maxX = plant.posX;
            }

            // 分桶边长按较长的一边切，保证两个轴向的分桶数都不超过 PlantBucketCountPerAxis；
            // 两端各留一个实例半径的余量，避免最长边上的实例被算到网格外面。
            float extent = Mathf.Max(maxZ - minZ, maxX - minX) + PlantCullMargin * 2f;
            plantBucketSize = Mathf.Max(1f, extent / PlantBucketCountPerAxis);
            plantBucketOriginZ = minZ - PlantCullMargin;
            plantBucketOriginX = minX - PlantCullMargin;
            plantBucketCols = Mathf.Max(1, Mathf.FloorToInt((maxZ + PlantCullMargin - plantBucketOriginZ) / plantBucketSize) + 1);
            plantBucketRows = Mathf.Max(1, Mathf.FloorToInt((maxX + PlantCullMargin - plantBucketOriginX) / plantBucketSize) + 1);

            int bucketCount = plantBucketCols * plantBucketRows;
            if (plantBucketStart == null || plantBucketStart.Length != bucketCount + 1)
                plantBucketStart = new int[bucketCount + 1];
            else
                System.Array.Clear(plantBucketStart, 0, plantBucketStart.Length);
            plantBucketItems = new int[plantCount];

            // 计数排序：先统计每桶数量（写进 start[b + 1]），再前缀和得到每桶区间，最后按游标回填下标。
            for (int i = 0; i < plantCount; i++)
            {
                PlantInstance plant = plantDatas[i];
                int col = PlantBucketClamp((plant.posZ - plantBucketOriginZ) / plantBucketSize, plantBucketCols);
                int row = PlantBucketClamp((plant.posX - plantBucketOriginX) / plantBucketSize, plantBucketRows);
                plantBucketStart[row * plantBucketCols + col + 1]++;
            }
            for (int i = 1; i < plantBucketStart.Length; i++)
                plantBucketStart[i] += plantBucketStart[i - 1];

            int[] cursor = new int[bucketCount];
            System.Array.Copy(plantBucketStart, 0, cursor, 0, bucketCount);
            for (int i = 0; i < plantCount; i++)
            {
                PlantInstance plant = plantDatas[i];
                int col = PlantBucketClamp((plant.posZ - plantBucketOriginZ) / plantBucketSize, plantBucketCols);
                int row = PlantBucketClamp((plant.posX - plantBucketOriginX) / plantBucketSize, plantBucketRows);
                plantBucketItems[cursor[row * plantBucketCols + col]++] = i;
            }
        }

        public override void UpdateImmediate()
        {
            foreach (IMapManageObject obj in staticObjects)
            {
                obj.EditorShow(true);
                obj.visible = true;
            }
            foreach (IMapManageObject obj in dynamicObjects)
            {
                obj.EditorShow(true);
                obj.visible = true;
            }
        }
    }
}
