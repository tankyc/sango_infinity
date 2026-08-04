
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

        public class ModelInstanceData
        {
            public Matrix4x4[] _matrixes = new Matrix4x4[1];
            public Mesh mesh;
            public Material material;
            public Material[] seasonMaterials = new Material[4];
            public int showCount;
            public MaterialPropertyBlock _mpb = new MaterialPropertyBlock();
            public List<IMapManageObject> drawObject = new List<IMapManageObject>();
            public UnityEngine.Object @object;

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

            public void UpdateMatrixes()
            {
                showCount = drawObject.Count;
                if (showCount >= _matrixes.Length)
                {
                    int destLen = _matrixes.Length;
                    while (destLen < showCount)
                        destLen *= 2;
                    _matrixes = new Matrix4x4[destLen];
                    for (int i = 0; i < destLen; i++)
                        _matrixes[i] = new Matrix4x4();
                }

                for (int i = 0; i < showCount; i++)
                {
                    var mx = _matrixes[i];
                    IMapManageObject mapManageObject = drawObject[i];
                    Transform node = mapManageObject.transform;
                    mx.SetTRS(
                        node.position,
                        node.rotation,
                        node.lossyScale
                        );
                    _matrixes[i] = mx;
                }
            }

            public void UpdateSeason(int season)
            {
                Material mat = seasonMaterials[season];
                if (mat != null)
                    material = mat;
            }

            public void DrawMesh()
            {
                if (needUpdateMatrixes)
                {
                    UpdateMatrixes();
                    needUpdateMatrixes = false;
                }

                if (mesh == null || showCount <= 0) return;

                if (SystemInfo.supportsInstancing)
                {
                    Graphics.DrawMeshInstanced(mesh, 0, material, _matrixes, showCount, _mpb, ShadowCastingMode.On, true, 0);
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
            MapObject obj = MapObject.Create(modelId.ToString());
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

        public void InitModel(int id, Vector2Int coords, float height, Vector3 rot, Vector3 scale)
        {

        }

        public void ClearAllModels()
        {
            foreach (IMapManageObject o in staticObjects)
            {
                staticObjectsQuadTree.Remove(o, o.worldBounds);
                o.visible = false;
            }
            staticObjects.Clear();
            dynamicObjects.Clear();
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

            for (int i = 0; i < modelInstanceDatas.Length; i++)
            {
                modelInstanceDatas[i].DrawMesh();
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
