using LuaInterface;
using UnityEngine;

namespace Sango.Loader
{
    public class ModelLoader : ObjectLoader
    {
        private static ModelLoader _instance;
        public static ModelLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ModelLoader();
                }
                return _instance;
            }
        }
        //private static void OnModelFullyLoad(AssetLoaderContext assetLoaderContext)
        //{
        //    if (assetLoaderContext.RootGameObject != null)
        //    {
        //        LoadData loadData = assetLoaderContext.CustomData as LoadData;
        //        if (loadData != null)
        //        {
        //            UnityEngine.Object finalObj = AssetStore.Instance.StoreAsset(loadData.filePath, assetLoaderContext.RootGameObject);
        //            loadData.rsObject = GameObject.Instantiate(finalObj);
        //            loadData.Call();
        //        }
        //    }
        //}

        //private static void OnError(IContextualizedError contextualizedError)
        //{
        //    Debug.LogError($"There was an error loading your model: {contextualizedError}");
        //}


        protected class ModelLoadData
        {
            public string texturePath;
            public bool textureNeedCompress;
            public string matName;
            public bool shareMaterial;
            public Material mat;
            public LoadData lastLoadData;
            public LuaFunction onLoadedFunc;
            public OnObjectLoaded onCSharpLoadedFunc;
            public object customData;
            public UnityEngine.Object rsObject;
        }


        public static void OnModelObjectLoaded(UnityEngine.Object obj, object customData)
        {
            ModelLoadData data = (ModelLoadData)customData;
            data.rsObject = obj;
            Material mat;
            GameObject go = obj as GameObject;
            if (go != null)
            {
                Renderer r = go.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    if (r.sharedMaterial == null)
                    {
                        mat = MaterialLoader.LoadMaterial(data.matName, data.shareMaterial);
                        if (mat != null)
                        {
                            data.mat = mat;
                            r.sharedMaterial = mat;
                        }
                    }
                    else
                    {
                        string rshaderName = r.sharedMaterial.shader.name + ".shader";
                        if (!rshaderName.Equals(data.matName))
                        {
                            Debug.LogError(r.sharedMaterial.shader.name);
                            Debug.LogError(data.matName);
                            obj = GameObject.Instantiate(obj);
                            data.rsObject = obj;
                            go = obj as GameObject;
                            if (go != null)
                            {
                                r = go.GetComponentInChildren<Renderer>();
                                if (r != null)
                                {
                                    mat = MaterialLoader.LoadMaterial(data.matName, data.shareMaterial);
                                    if (mat != null)
                                    {
                                        data.mat = mat;
                                        r.sharedMaterial = mat;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(data.texturePath))
            {
                TextureLoader.LoadFromFile(data.texturePath, data, (tex, custom) =>
                {
                    ModelLoadData _data = (ModelLoadData)custom;
                    if (tex != null && _data.mat != null)
                    {
                        _data.mat.SetTexture("_MainTex", tex as Texture);
                    }
                    //Debug.Log("Model successfully loaded.");
                    if (_data.onLoadedFunc != null)
                    {
                        _data.onLoadedFunc.BeginPCall();
                        _data.onLoadedFunc.Push(_data.rsObject);
                        if (_data.customData != null)
                            _data.onLoadedFunc.Push(_data.customData);
                        _data.onLoadedFunc.PCall();
                        _data.onLoadedFunc.EndPCall();
                    }

                    if (_data.onCSharpLoadedFunc != null)
                    {
                        _data.onCSharpLoadedFunc(_data.rsObject, _data.customData);
                    }
                },
              data.textureNeedCompress
              );
            }
            else
            {
                if (data.onLoadedFunc != null)
                {
                    data.onLoadedFunc.BeginPCall();
                    data.onLoadedFunc.Push(data.rsObject);
                    if (data.customData != null)
                        data.onLoadedFunc.Push(data.customData);
                    data.onLoadedFunc.PCall();
                    data.onLoadedFunc.EndPCall();
                }

                if (data.onCSharpLoadedFunc != null)
                {
                    data.onCSharpLoadedFunc(data.rsObject, data.customData);
                }
            }
        }

        private static ModelLoadData CreateModelLoadData(string texturePath, bool textureNeedCompress, string matName, bool shareMaterial, object customData, LuaFunction onLoadedFunc, OnObjectLoaded onCSharpLoadedFunc)
        {
            return new ModelLoadData
            {
                texturePath = texturePath,
                textureNeedCompress = textureNeedCompress,
                matName = matName,
                shareMaterial = shareMaterial,
                customData = customData,
                onLoadedFunc = onLoadedFunc,
                onCSharpLoadedFunc = onCSharpLoadedFunc,
            };
        }

        private static void LoadFromFile(string filePath, string texturePath, bool textureNeedCompress, string matName, bool shareMaterial, object customData, LuaFunction onLoadedFunc, OnObjectLoaded onCSharpLoadedFunc = null)
        {
            CheckHelper();

            LoadData loadData = CheckExistLoader(filePath);
            if (loadData != null)
            {
                loadData.c_customData.Add(CreateModelLoadData(texturePath, textureNeedCompress, matName, shareMaterial, customData, onLoadedFunc, onCSharpLoadedFunc));
                loadData.onCSharpLoadedFuncs.Add(OnModelObjectLoaded);
                return;
            }

            GameObject obj = AssetStore.Instance.CheckAsset<GameObject>(filePath);
            if (obj != null)
            {
                GameObject rsObject = GameObject.Instantiate(obj);
                OnModelObjectLoaded(rsObject, CreateModelLoadData(texturePath, textureNeedCompress, matName, shareMaterial, customData, onLoadedFunc, onCSharpLoadedFunc));
                return;
            }

            string finalPath = Path.FindFile(filePath);
            if (finalPath == null) return;

            if (reusedQueue.Count > 0)
            {
                loadData = reusedQueue.Dequeue();
                loadData.filePath = filePath;
                loadData.texturePath = texturePath;
                loadData.matName = matName;
                loadData.rsObject = obj;
                loadData.textureNeedCompress = textureNeedCompress;
                loadData.shareMaterial = shareMaterial;
            }
            else
            {
                loadData = new LoadData
                {
                    filePath = filePath,
                    texturePath = texturePath,
                    matName = matName,
                    rsObject = obj,
                    textureNeedCompress = textureNeedCompress,
                    shareMaterial = shareMaterial
                };
            }

            loadData.AddCall(OnModelObjectLoaded,
                CreateModelLoadData(texturePath, textureNeedCompress, matName, shareMaterial, customData, onLoadedFunc, onCSharpLoadedFunc));

            usingList.Add(loadData);


           // AssetLoaderContext context = AssetLoader.LoadModelFromFile(finalPath, null, OnModelFullyLoad, null, OnError, null, null, loadData);

        }
        public static void LoadFromFile(string filePath, string texturePath, bool textureNeedCompress, string matName, bool shareMaterial, object customData, LuaFunction onLoadedFunc)
        {
            LoadFromFile(filePath, texturePath, textureNeedCompress, matName, shareMaterial, customData, onLoadedFunc, null);
        }
        public static void LoadFromFile(string filePath, string texturePath, bool textureNeedCompress, string matName, bool shareMaterial, object customData, OnObjectLoaded onLoadedFunc)
        {
            LoadFromFile(filePath, texturePath, textureNeedCompress, matName, shareMaterial, customData, null, onLoadedFunc);
        }
        public static void LoadFromFile(string filePath, string matName, bool shareMaterial, object customData, LuaFunction onLoadedFunc)
        {
            LoadFromFile(filePath, "", false, matName, shareMaterial, customData, onLoadedFunc, null);
        }
        public static void LoadFromFile(string filePath, string matName, bool shareMaterial, object customData, OnObjectLoaded onLoadedFunc)
        {
            LoadFromFile(filePath, "", false, matName, shareMaterial, customData, null, onLoadedFunc);
        }

        public static GameObject LoadFromFileSync(string filePath, string texturePath, bool textureNeedCompress, string matName, bool shareMaterial)
        {
            UnityEngine.Object obj = AssetStore.Instance.CheckAsset<GameObject>(filePath);
            if (obj == null)
            {
                string finalPath = Path.FindFile(filePath);
                if (finalPath == null) return null;

                //AssetLoaderContext context = AssetLoader.LoadModelFromFileNoThread(finalPath, OnError, null, null, null);
                //if (context.RootGameObject != null)
                //{
                //    obj = AssetStore.Instance.StoreAsset(filePath, context.RootGameObject);
                //}
            }

            if (obj != null)
            {
                // 装载材质球和贴图
                GameObject go = GameObject.Instantiate(obj) as GameObject;
                if (go != null)
                {
                    Renderer r = go.GetComponentInChildren<Renderer>();
                    if (r != null)
                    {
                        Material material = MaterialLoader.LoadMaterial(matName, shareMaterial);
                        r.sharedMaterial = material;
                        if (!string.IsNullOrEmpty(texturePath))
                        {
                            Texture texture = TextureLoader.LoadFromFileSync(texturePath, textureNeedCompress, true);
                            if (texture != null)
                            {
                                material.SetTexture("_MainTex", texture);
                            }
                        }
                    }
                    return go;
                }
            }

            return obj as GameObject;


        }

    }
}
