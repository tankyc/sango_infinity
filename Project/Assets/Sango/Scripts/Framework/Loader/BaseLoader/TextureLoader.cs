
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

        private static void LoadFromFile(string filePath, object customData, bool textureNeedCompress, bool needMipmap, OnObjectLoaded onCharpLoadedFunc = null)
        {
            CheckHelper();

            if (string.IsNullOrEmpty(filePath)) return;

            LoadData loadData = CheckExistLoader(filePath);
            if (loadData != null)
            {
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

            loadData.AddCall(onCharpLoadedFunc, customData);

            usingList.Add(loadData);

            if (obj != null)
            {
                ObjectLoader.rsQueue.Enqueue(loadData);
                return;
            }

            App.Instance.StartCoroutine(LoadImage(filePath, OnLoaded, loadData));

        }
        public static void LoadFromFile(string filePath, object customData, OnObjectLoaded onLoadedFunc, bool textureNeedCompress = true, bool needMipmap = true)
        {
            LoadFromFile(filePath, customData, textureNeedCompress, needMipmap, onLoadedFunc);
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
                    {
                        try
                        {
                            t.Compress(true);
                        }
                        catch (Exception e)
                        {
                            // 压缩失败通常是因为贴图宽高不满足压缩要求(宽高需能被4整除)
                            // 这里将宽高缩放为距离最近且能被4整除的尺寸后重新压缩
                            t = ResizeToSizeDivisibleByFour(t, loadData.textureNeedMipmap);
                            try
                            {
                                t.Compress(true);
                            }
                            catch (Exception ex)
                            {
                                Sango.Log.Warning(ex);
                            }
                        }
                    }
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
            string srcPath = filePath;
            if (!System.IO.Path.HasExtension(srcPath))
            {
                string[] all_fs = new string[]
                {
                    srcPath + ".png",
                    srcPath + ".jpg",
                    srcPath + ".jpeg"
                };

                filePath = Path.FindFile(all_fs);
            }
            else
            {
                filePath = Path.FindFile(srcPath);
            }
            if (string.IsNullOrEmpty(filePath)) return null;
            Texture obj = AssetStore.Instance.CheckAsset<Texture>(filePath);
            if (obj == null)
            {
                byte[] fileData = File.ReadAllBytes(filePath);
                Texture2D texture = new Texture2D(2, 2); // 创建一个空的Texture2D对象，这里的2, 2只是为了初始化，实际尺寸应从图片获取
                texture.LoadImage(fileData); // 使用LoadImage加载图
                if (textureNeedCompress)
                {
                    try
                    {
                        texture.Compress(true);
                    }
                    catch (Exception e)
                    {
                        // 压缩失败通常是因为贴图宽高不满足压缩要求(宽高需能被4整除)
                        // 这里将宽高缩放为距离最近且能被4整除的尺寸后重新压缩
                        texture = ResizeToSizeDivisibleByFour(texture, needMipmap);
                        try
                        {
                            texture.Compress(true);
                        }
                        catch (Exception ex)
                        {
                            Sango.Log.Warning(ex);
                        }
                    }
                }
                texture.Apply(needMipmap, true);
                obj = AssetStore.Instance.StoreAsset(filePath, texture) as Texture;
                return obj;
            }
            return obj;
        }

        /// <summary>
        /// 将贴图的宽高缩放为距离最近且能被4整除的尺寸
        /// 贴图压缩(如DXT)要求宽高必须是4的倍数，否则压缩会抛出异常
        /// </summary>
        /// <param name="texture">原始贴图</param>
        /// <param name="needMipmap">是否需要生成Mipmap</param>
        /// <returns>缩放后的新贴图；若原始尺寸已满足要求则返回原贴图</returns>
        private static Texture2D ResizeToSizeDivisibleByFour(Texture2D texture, bool needMipmap)
        {
            // 原始贴图为空时直接返回
            if (texture == null) return null;

            // 计算距离原始宽高最近且能被4整除的尺寸，最小尺寸不低于4
            int width = Mathf.Max(4, Mathf.RoundToInt(texture.width / 4f) * 4);
            int height = Mathf.Max(4, Mathf.RoundToInt(texture.height / 4f) * 4);

            // 尺寸未发生变化，说明原本就满足要求，无需缩放
            if (width == texture.width && height == texture.height)
                return texture;

            // 创建目标尺寸的新贴图
            Texture2D resized = new Texture2D(width, height, TextureFormat.ARGB32, needMipmap);
            Color[] pixels = new Color[width * height];

            // 逐像素使用双线性采样，保证缩放后的图片质量
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 取像素中心点进行采样，避免缩放时产生边缘偏移
                    pixels[y * width + x] = texture.GetPixelBilinear((x + 0.5f) / width, (y + 0.5f) / height);
                }
            }

            resized.SetPixels(pixels);
            resized.Apply(needMipmap, false);

            // 释放原始贴图，避免内存泄漏
            UnityTools.DeleteObjImmediate(texture);

            return resized;
        }
    }
}
