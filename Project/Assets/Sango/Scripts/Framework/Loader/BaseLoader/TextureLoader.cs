using LuaInterface;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Sango;

namespace Sango.Loader
{
    public class TextureLoader : ObjectLoader
    {
        private static TextureLoader _instance;
        public static TextureLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TextureLoader();
                }
                return _instance;
            }
        }

        private static void OnLoaded(Texture2D texture, LoadData loadData)
        {
            if (texture != null)
            {
                if (loadData != null)
                {
                    UnityEngine.Object finalObj = AssetStore.Instance.StoreAsset(loadData.filePath, texture);
                    loadData.rsObject = finalObj;
                    loadData.Call();
                }

            }
        }

        private static void LoadFromFile(string filePath, object customData, bool textureNeedCompress, bool needMipmap, LuaFunction onLoadedFunc, OnObjectLoaded onCharpLoadedFunc = null)
        {
            CheckHelper();

            if (string.IsNullOrEmpty(filePath)) return;

            LoadData loadData = CheckExistLoader(filePath);
            if (loadData != null)
            {
                loadData.AddCall(onLoadedFunc, customData);
                loadData.AddCall(onCharpLoadedFunc, customData);
                return;
            }

            Texture obj = AssetStore.Instance.CheckAsset<Texture>(filePath);
            if (reusedQueue.Count > 0)
            {
                loadData = reusedQueue.Dequeue();
                loadData.filePath = filePath;
                loadData.matName = null;
                loadData.rsObject = obj;
                loadData.textureNeedCompress = textureNeedCompress;
                loadData.textureNeedMipmap = needMipmap;
                loadData.shareMaterial = true;
            }
            else
            {
                loadData = new LoadData
                {
                    filePath = filePath,
                    texturePath = null,
                    matName = null,
                    rsObject = obj,
                    textureNeedCompress = textureNeedCompress,
                    textureNeedMipmap = needMipmap,
                    shareMaterial = true
                };
            }

            loadData.AddCall(onLoadedFunc, customData);
            loadData.AddCall(onCharpLoadedFunc, customData);

            usingList.Add(loadData);

            if (obj != null)
            {
                ObjectLoader.rsQueue.Enqueue(loadData);
                return;
            }

            Sango.Game.Game.Instance.StartCoroutine(LoadImage(filePath, OnLoaded, loadData));

        }
        public static void LoadFromFile(string filePath, object customData, LuaFunction onLoadedFunc, bool textureNeedCompress = true, bool needMipmap = true)
        {
            LoadFromFile(filePath, customData, textureNeedCompress, needMipmap, onLoadedFunc, null);
        }
        public static void LoadFromFile(string filePath, object customData, OnObjectLoaded onLoadedFunc, bool textureNeedCompress = true, bool needMipmap = true)
        {
            LoadFromFile(filePath, customData, textureNeedCompress, needMipmap, null, onLoadedFunc);
        }

        protected static IEnumerator LoadImage(string filePath, Action<Texture2D, LoadData> loadEnd, LoadData loadData)
        {
            //Debug.LogError(filePath);
            filePath = Path.FindFile(filePath);
            //Debug.LogError(filePath);
            if (string.IsNullOrEmpty(filePath)) yield return null;

#if UNITY_ANDROID && !UNITY_EDITOR
            filePath = "file://" + filePath;
#endif
            Debug.Log("LoadImage : " + filePath);
            UnityWebRequest uwr = UnityWebRequest.Get(filePath);
            DownloadHandlerTexture downloadTexture = new DownloadHandlerTexture(true);
            uwr.downloadHandler = downloadTexture;
            yield return uwr.SendWebRequest();
            if (string.IsNullOrEmpty(uwr.error))
            {
                Texture2D t = downloadTexture.texture;
                if (t != null)
                {
                    //if (loadData.textureNeedMipmap)
                    //{
                    //    Texture2D texture2 = new Texture2D(t.width, t.height, TextureFormat.ARGB32, true);
                    //    texture2.SetPixels(t.GetPixels());
                    //    texture2.Apply(true, true);
                    //    //GameObject.DestroyImmediate(t);
                    //    t = texture2;
                    //}
                    if (loadData.textureNeedCompress)
                        t.Compress(true);
                    t.Apply(loadData.textureNeedMipmap, true);
                    loadEnd.Invoke(t, loadData);
                }
            }
            else
            {
                Sango.Log.Error(uwr.error);
                if (uwr.downloadHandler != null)
                    Sango.Log.Error(uwr.downloadHandler.error);
            }

        }

        /// <summary>
        /// 同步加载贴图
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="customData"></param>
        /// <param name="textureNeedCompress"></param>
        /// <param name="needMipmap"></param>
        /// <param name="onLoadedFunc"></param>
        /// <param name="onCharpLoadedFunc"></param>
        public static Texture LoadFromFileSync(string filePath, bool textureNeedCompress, bool needMipmap)
        {
            filePath = Path.FindFile(filePath);
            if (string.IsNullOrEmpty(filePath)) return null;
            Texture obj = AssetStore.Instance.CheckAsset<Texture>(filePath);
            if (obj == null)
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2); // 创建一个空的Texture2D对象，这里的2, 2只是为了初始化，实际尺寸应从图片获取
                texture.LoadImage(fileData); // 使用LoadImage加载图
                if (textureNeedCompress)
                    texture.Compress(true);
                texture.Apply(needMipmap, true);
                obj = AssetStore.Instance.StoreAsset(filePath, texture) as Texture;
                return obj;
            }
            return obj;
        }
    }
}
