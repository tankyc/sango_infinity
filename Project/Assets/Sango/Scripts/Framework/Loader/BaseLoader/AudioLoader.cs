using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Sango.Core;

namespace Sango.Loader
{
    public class AudioLoader : ObjectLoader
    {
        /// <summary>
        /// 从文件路径同步加载音频文件（支持OGG、MP3、WAV等格式）
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="is3D">是否为3D音效</param>
        /// <returns>加载的AudioClip</returns>
        public static AudioClip LoadFromFileSync(string filePath, bool is3D = false)
        {
            UnityEngine.Object obj = AssetStore.Instance.CheckAsset<AudioClip>(filePath);
            if (obj != null)
            {
                return obj as AudioClip;
            }
            filePath = Path.FindFile(filePath);
            if (filePath == null)
            {
                return null;
            }
            try
            {
                // 获取文件扩展名并转换为小写
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                AudioType audioType = GetAudioTypeFromExtension(extension);

                // 使用UnityWebRequest加载音频文件，自动处理解码
                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{filePath}", audioType))
                {
                    www.SendWebRequest();

                    // 同步等待加载完成
                    while (!www.isDone)
                    {
                        // 等待加载完成
                    }

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                        if (audioClip != null)
                        {
                            audioClip.name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                            return audioClip;
                        }
                    }
                    else
                    {
                        Debug.LogError($"加载音频文件失败: {www.error}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"加载音频文件失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 根据文件扩展名获取AudioType
        /// </summary>
        /// <param name="extension">文件扩展名（带点号，如".ogg"）</param>
        /// <returns>对应的AudioType</returns>
        private static AudioType GetAudioTypeFromExtension(string extension)
        {
            switch (extension)
            {
                case ".ogg":
                    return AudioType.OGGVORBIS;
                case ".mp3":
                    return AudioType.MPEG;
                case ".wav":
                    return AudioType.WAV;
                case ".aiff":
                    return AudioType.AIFF;
                case ".aif":
                    return AudioType.AIFF;
                case ".mp2":
                    return AudioType.MPEG;
                case ".mp1":
                    return AudioType.MPEG;
                case ".mpg":
                    return AudioType.MPEG;
                case ".mpeg":
                    return AudioType.MPEG;
                case ".m4a":
                    return AudioType.MPEG;
                case ".aac":
                    return AudioType.MPEG;
                case ".wma":
                    Debug.LogWarning($"WMA格式(.wma)不受Unity支持，将使用默认格式");
                    return AudioType.UNKNOWN;
                case ".flac":
                    Debug.LogWarning($"FLAC格式(.flac)不受Unity支持，将使用默认格式");
                    return AudioType.UNKNOWN;
                default:
                    Debug.LogWarning($"未知的音频格式: {extension}，使用默认格式");
                    return AudioType.UNKNOWN;
            }
        }

        /// <summary>
        /// 从文件路径异步加载音频文件（支持OGG、MP3、WAV等格式）
        /// </summary>
        /// <param name="filePath">音频文件路径</param>
        /// <param name="callback">加载完成回调</param>
        /// <param name="is3D">是否为3D音效</param>
        /// <param name="customData">自定义数据</param>
        public static void LoadFromFileAsync(string filePath, OnObjectLoaded callback, bool is3D = false, object customData = null)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogError($"音频文件不存在: {filePath}");
                callback?.Invoke(null, customData);
                return;
            }

            try
            {
                // 获取文件扩展名并转换为小写
                string extension = System.IO.Path.GetExtension(filePath).ToLower();
                AudioType audioType = GetAudioTypeFromExtension(extension);

                // 使用UnityWebRequest异步加载音频文件
                UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{filePath}", audioType);

                // 使用Unity的协程系统处理异步加载
                if (App.Instance != null)
                {
                    App.Instance.StartCoroutine(LoadAudioCoroutine(www, filePath, callback, is3D, customData));
                }
                else
                {
                    // 如果没有Game实例，使用同步加载
                    www.SendWebRequest();
                    while (!www.isDone) { }
                    HandleWebRequestResult(www, filePath, callback, is3D, customData);
                    www.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"异步加载OGG文件失败: {ex.Message}");
                callback?.Invoke(null, customData);
            }
        }

        /// <summary>
        /// 协程加载音频文件
        /// </summary>
        private static IEnumerator LoadAudioCoroutine(UnityWebRequest www, string filePath, OnObjectLoaded callback, bool is3D, object customData)
        {
            yield return www.SendWebRequest();

            HandleWebRequestResult(www, filePath, callback, is3D, customData);
            www.Dispose();
        }

        /// <summary>
        /// 处理WebRequest结果
        /// </summary>
        private static void HandleWebRequestResult(UnityWebRequest www, string filePath, OnObjectLoaded callback, bool is3D, object customData)
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                if (audioClip != null)
                {
                    audioClip.name = System.IO.Path.GetFileNameWithoutExtension(filePath);

                    // 将结果放入队列处理
                    LoadData loadData = new LoadData();
                    loadData.rsObject = audioClip;
                    loadData.AddCall(callback, customData);
                    rsQueue.Enqueue(loadData);
                    return;
                }
            }
            else
            {
                Debug.LogError($"异步加载音频文件失败: {www.error}");
            }

            callback?.Invoke(null, customData);
        }
    }
}
