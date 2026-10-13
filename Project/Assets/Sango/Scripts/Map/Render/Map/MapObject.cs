
using Sango.Core;
using Sango.Loader;
using Sango.Tools;
using System.Collections;
using UnityEngine;

namespace Sango.Render
{
    public class MapObject : Behaviour, IMapManageObject
    {
        public delegate void ModelLoadedCallback(GameObject obj);
        public delegate void ModelVisibleChange(MapObject obj);
        public delegate void ModelPointCall();
        public delegate string ModelAssetCheck(int currentModelId, string currentAsset);

        public ModelLoadedCallback onModelLoadedCallback;
        public ModelVisibleChange onModelVisibleChange;
        public ModelAssetCheck onModelAssetCheck;
        public ModelPointCall onClick;

        static int buildingLayer = -1;
        Transform transCache;
        bool _visible = false;
        bool _selectable = false;
        public MapRender manager { get; set; }
        public bool remainInView { get; set; }
        /// <summary>
        /// 是否可保存至地图
        /// </summary>
        public virtual bool canSave { get; set; } = true;
        public Tools.Rect bounds { get; set; }
        /// <summary>
        /// 用于绑定物件的ID
        /// </summary>
        public int objId { get; set; }
        public bool isAdded { get; set; }

        /// <summary>
        /// 绑定Id
        /// </summary>
        public int bindId { get; set; }

        /// <summary>
        /// 物件的类型
        /// </summary>
        public int objType { get; set; }
        /// <summary>
        /// 模型ID
        /// </summary>
        public int modelId { get; set; }
        /// <summary>
        /// 模型资源
        /// </summary>
        public string modelAsset { get; set; }

        /// <summary>
        ///  实例化标记
        /// </summary>
        public bool instanceFlag { get; set; }


        public GameObject loadedModel;
        private bool isLoading = false;
        public bool editorShow = true;		//添加了public
        public bool isStatic { get; set; }
        public bool visible
        {
            get { return _visible; }
            set
            {
                if (value != _visible)
                {
                    _visible = value;
                    OnVisibleChange();
                }
            }
        }

        public bool selectable
        {
            get { return _selectable; }
            set
            {
                if (value != _selectable)
                {
                    _selectable = value;
                    OnSelectableChange();
                }
            }
        }

        public Vector3 position
        {
            get { return transCache.position; }
            set { transCache.position = value; }
        }
        public Vector3 rotation
        {
            get { return transCache.localRotation.eulerAngles; }
            set { transCache.localRotation = Quaternion.Euler(value); }
        }

        public Vector3 forward
        {
            get { return transCache.forward; }
            set { transCache.forward = value; }
        }

        public Vector3 scale
        {
            get { return transCache.localScale; }
            set { transCache.localScale = value; }
        }
        public Vector2Int coords { get; set; }

        public Sango.Tools.Rect worldBounds
        {
            get
            {
                Vector3 pos = position;
                return new Tools.Rect(pos.z + bounds.x - bounds.width / 2,
                pos.x + bounds.y - bounds.height / 2,
                bounds.width, bounds.height);
            }
        }

        public bool doNotRadiusCheck { get; set; }

        public static MapObject Create()
        {
            if (buildingLayer == -1)
                buildingLayer = LayerMask.NameToLayer("Building");
            GameObject modelObj = new GameObject();
            modelObj.transform.SetParent(MapRender.modelRoot);
            //modelObj.layer = buildingLayer;
            return modelObj.AddComponent<MapObject>();
        }

        public static MapObject Create(string name)
        {
            if (buildingLayer == -1)
                buildingLayer = LayerMask.NameToLayer("Building");
            GameObject modelObj = new GameObject(name);
            modelObj.transform.SetParent(MapRender.modelRoot);
            //modelObj.layer = buildingLayer;
            return modelObj.AddComponent<MapObject>();
        }

        /// <summary>
        /// 模型名称缓存，下标即 modelId。
        /// GameObject 的名字来自这里：如果每个物件都调一次 modelId.ToString()，
        /// 十几万个物件就会产生十几万个内容相同的新字符串（约 5MB 托管内存 + 同等规模的 GC 垃圾）。
        /// 按 modelId 缓存后，全流程只会产生模型种类数（十几个）的字符串。
        /// </summary>
        private static readonly string[] modelNameCache = new string[128];

        /// <summary>
        /// 以模型 ID 创建地图物件（名字走 modelNameCache，避免重复分配字符串）。
        /// </summary>
        /// <param name="modelId">模型 ID</param>
        /// <returns>地图物件</returns>
        public static MapObject Create(int modelId)
        {
            if (modelId < 0 || modelId >= modelNameCache.Length)
                return Create(modelId.ToString());

            string name = modelNameCache[modelId];
            if (name == null)
            {
                name = modelId.ToString();
                modelNameCache[modelId] = name;
            }
            return Create(name);
        }

        public static MapObject Create(string name, string layerName)
        {
            if (buildingLayer == -1)
                buildingLayer = LayerMask.NameToLayer("Building");
            GameObject modelObj = new GameObject(name);
            modelObj.transform.SetParent(MapRender.modelRoot);
            modelObj.layer = LayerMask.NameToLayer(layerName);
            return modelObj.AddComponent<MapObject>();
        }

        public static MapObject Create(string name, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            if (buildingLayer == -1)
                buildingLayer = LayerMask.NameToLayer("Building");
            GameObject modelObj = new GameObject(name);
            modelObj.transform.SetParent(MapRender.modelRoot);
            //modelObj.layer = buildingLayer;
            modelObj.transform.position = pos;
            modelObj.transform.rotation = Quaternion.Euler(rot);
            modelObj.transform.localScale = scale;
            return modelObj.AddComponent<MapObject>();
        }

        public static MapObject Create(string name, string layerName, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            if (buildingLayer == -1)
                buildingLayer = LayerMask.NameToLayer("Building");
            GameObject modelObj = new GameObject(name);
            modelObj.transform.SetParent(MapRender.modelRoot);
            modelObj.layer = LayerMask.NameToLayer(layerName);
            modelObj.transform.position = pos;
            modelObj.transform.rotation = Quaternion.Euler(rot);
            modelObj.transform.localScale = scale;
            return modelObj.AddComponent<MapObject>();
        }

        public virtual void Awake()
        {
            transCache = transform;
        }

        public virtual void OnSelectableChange()
        {
            if (selectable)
            {
                MeshFilter filter = gameObject.GetComponentInChildren<MeshFilter>();
                if (filter != null)
                {
                    BoxCollider boxCollider = transCache.GetComponent<BoxCollider>();
                    if (boxCollider == null)
                    {
                        boxCollider = transCache.gameObject.AddComponent<BoxCollider>();
                        Bounds b = filter.sharedMesh.bounds;
                        boxCollider.size = b.size;
                        boxCollider.center = b.center;
                    }
                }
            }
            else
            {
                MeshFilter filter = gameObject.GetComponentInChildren<MeshFilter>();
                if (filter != null)
                {
                    BoxCollider boxCollider = transCache.GetComponent<BoxCollider>();
                    if (boxCollider == null)
                    {
                        GameObject.Destroy(boxCollider);
                    }
                }
            }
        }

        public virtual void OnVisibleChange()
        {
            if (editorShow)
            {
                if (visible)
                {
                    ReLoadModels();
                }
                else
                {
                    if (!Tools.MapEditor.IsEditOn)
                        ClearModels();
                }
            }
            onModelVisibleChange?.Invoke(this);
        }

        public bool Overlaps(Sango.Tools.Rect rect)
        {
            return worldBounds.Overlaps(rect);
        }


        public virtual void OnClick()
        {
        }
        public virtual void OnPointerEnter()
        {
        }
        public virtual void OnPointerExit()
        {
        }

        public virtual void SetOutlineShow(Material material)
        {

        }
        protected virtual void OnModelLoaded(UnityEngine.Object obj, object customData)
        {
            OnModelLoaded(obj, customData, true);
        }

        protected virtual void OnModelLoaded(UnityEngine.Object obj, object customData, bool dontAsyncCall)
        {
            isLoading = false;
            GameObject model = null;
            if (obj != null)
            {
                object pool_key = modelId;
                if (!string.IsNullOrEmpty(modelAsset))
                    pool_key = modelAsset;
                if (!PoolManager.Add(pool_key, obj))
                {
                    //GameObject.DestroyImmediate(obj, true);
                }
                if (!visible) return;
                model = PoolManager.Get(pool_key);
            }

            OnModelInit(model, dontAsyncCall);

            OnSelectableChange();
        }

        protected virtual void OnModelInit(GameObject model, bool dontAsyncCall = true)
        {
            //if(loadedModel != null)
            //    GameObject.Destroy(loadedModel);
            if (model != null)
            {
                model.SetActive(true);
                model.transform.SetParent(transCache, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                if(MapEditor.IsEditOn)
                {
                    UnityTools.SetLayer(gameObject, model.layer);
                    BoxCollider box = gameObject.GetComponent<BoxCollider>();
                    if (box == null)
                        box = gameObject.AddComponent<BoxCollider>();
                    BoxCollider box_model = model.GetComponent<BoxCollider>();
                    if(box_model != null)
                    {
                        box.size = box_model.size;
                        box.center = box_model.center;
                        box_model.enabled = false;
                    }
                }
                // model.transform.localScale = Vector3.one;
                //UnityTools.SetLayer(model, gameObject.layer);

                //if (MapRender.Instance != null)
                //{
                //    Renderer[] rs = model.transform.GetComponentsInChildren<Renderer>();
                //    if (rs != null)
                //    {
                //        foreach (Renderer r in rs)
                //        {
                //            Material[] mats = r.sharedMaterials;
                //            if (mats.Length == 1)
                //            {
                //                if ( mats[0] != null)
                //                {
                //                    Material c = new Material(MapRender.Instance.terrainOutlineMat);
                //                    c.mainTexture = mats[0].mainTexture;
                //                    c.SetFloat("_OutlineWidth", 0.04f);
                //                    mats = new Material[2] { mats[0], c };
                //                    r.sharedMaterials = mats;
                //                }
                //            }
                //            else
                //            {
                //                if (mats[0] != null)
                //                    mats[1].mainTexture = mats[0].mainTexture;
                //            }
                //        }
                //    }
                //}
                onModelLoadedCallback?.Invoke(model);
            }
            loadedModel = model;
        }
        public void CreateModel(string meshFile, string textureFile, string shaderName, bool isShareMat = true)
        {
            isLoading = true;
            ModelLoader.LoadFromFile(meshFile,
                textureFile,
                true,
                shaderName,
                isShareMat,
                null,
                OnModelLoaded);
        }
        public void CreateModel(string packagePath, string assetName)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                UnityEngine.Object obj = Resources.Load(assetName);
                OnModelLoaded(obj, null, false);
                return;
            }

            int packageIndex = AssetBundleManager.Load(packagePath);
            if (packageIndex >= 0)
            {
                UnityEngine.Object obj = AssetBundleManager.LoadAsset(packageIndex, assetName);
                OnModelLoaded(obj, null, false);
                return;
            }

            OnModelInit(null, false);
        }
        public void CreateModel(string assetName)
        {
            GameObject obj = ObjectLoader.LoadObject<GameObject>(assetName);
            OnModelLoaded(obj, null, false);
        }

        public void CreateModel(UnityEngine.Object modelObj)
        {
            OnModelLoaded(modelObj, null, false);
        }

        public void ChangeModel(string newAsset)
        {
            if (newAsset == null)
            {
                return;
            }

            if (modelAsset == null)
            {
                return;
            }



            if (modelAsset.Equals(newAsset)) return;
            ClearModels();
            modelAsset = newAsset;
            if (visible)
            {
                ReLoadModels(false);
            }
        }

        public void ReLoadModels(bool checkAsset = true)
        {
            if (instanceFlag)
            {
                manager.mapModels.AddInstance(this);
                return;
            }


            if (loadedModel != null || isLoading)
            {
                return;
            }

            if (checkAsset && onModelAssetCheck != null)
                modelAsset = onModelAssetCheck(modelId, modelAsset);

            if (!string.IsNullOrEmpty(modelAsset))
            {
                GameObject obj = PoolManager.Get(modelAsset);
                if (obj != null)
                {
                    OnModelInit(obj);
                    return;
                }
                CreateModel(modelAsset);
            }
            else
            {
                GameObject obj = PoolManager.Get(modelId);
                if (obj != null)
                {
                    OnModelInit(obj);
                    return;
                }

                if (isLoading) return;

                isLoading = true;
                if (manager != null)
                {
                    manager.LoadModel(this);
                }
            }
        }

        public void ClearModels()
        {
            if (instanceFlag)
            {
                manager.mapModels.RemoveInstance(this);
                return;
            }

            isLoading = false;
            if (loadedModel == null) return;
            PoolManager.Recycle(loadedModel);
            loadedModel = null;
        }

        public void ReCheckVisible()
        {
            if (manager == null) return;
            _visible = false;
            visible = manager.mapModels.IsInView(this);
        }

        public void EditorShow(bool b)
        {
            if (!b)
            {
                ClearModels();
            }
            editorShow = b;
        }

        public Transform GetTransform() { return transCache; }
        public void SetParent(Transform parent) { transCache.SetParent(parent); }
        public void SetParent(Transform parent, bool worldPositionStays) { transCache.SetParent(parent, worldPositionStays); }

        public void OnDisable()
        {
            if(MapEditor.IsEditOn)
            {
                if(isAdded && manager != null)
                {
                    if(isStatic)
                    {
                        manager.Remove(this);
                    }
                    else
                    {
                        manager.Remove(this);
                    }
                }
            }
        }

        public void OnEnable()
        {
            if (MapEditor.IsEditOn)
            {
                if (!isAdded && manager != null)
                {
                    if (isStatic)
                    {
                        manager.AddStatic(this);
                    }
                    else
                    {
                        manager.AddDynamic(this);
                    }
                }
            }
        }

        public void AddToMap(bool isStatic)
        {
            if (manager == null)
            {
                manager = MapRender.Instance;
                if (isStatic)
                {
                    manager.AddStatic(this);
                }
                else
                {
                    manager.AddDynamic(this);
                }
            }
        }
        public void Clear()
        {
            if (manager != null)
            {
                if (isStatic)
                    manager.Remove(this);
                else
                    manager.Remove(this);
            }

            ClearModels();
        }

        public void Destroy()
        {
            Clear();
            if (gameObject != null)
                GameObject.Destroy(gameObject);
        }
        public GameObject GetGameObject()
        {
            return gameObject;
        }
    }
}