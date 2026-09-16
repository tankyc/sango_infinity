/*
 * 文件名：GameVersion.cs
 * 描述：游戏版本更新器，启动时从远端地址拉取最新版本信息，成功获取后抛出事件通知界面弹出更新提示
 * 创建日期：2026-09-16
 * 最后修改：2026-09-16
 */

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Sango.Core
{
    /// <summary>
    /// 远端版本信息数据结构
    /// 字段与服务端下发的 json 一一对应，请勿随意改名
    /// json 示例：{ "version": "1.2.3", "description": "修复若干问题", "url": "https://xxx/Sango.exe" }
    /// </summary>
    [Serializable]
    public class VersionInfo
    {
        /// <summary>
        /// 最新版本号，格式为主版本号.次版本号.修订号，例如 1.2.3
        /// </summary>
        public string version;

        /// <summary>
        /// 更新说明文本，用于在弹窗中展示本次更新的内容
        /// </summary>
        public string description;

        /// <summary>
        /// 新版本安装包的下载地址
        /// </summary>
        public string url;

        /// <summary>
        /// 远端版本号是否高于本地当前版本（仅在 VersionInfo 有效时可信）
        /// 该字段不参与 json 序列化，由 GameVersion 在解析完成后赋值
        /// </summary>
        [NonSerialized]
        public bool hasNewVersion;

        /// <summary>
        /// 版本号文本，version 为空时返回空串
        /// </summary>
        public string Version => version ?? string.Empty;

        /// <summary>
        /// 更新说明文本，description 为空时返回空串
        /// </summary>
        public string Description => description ?? string.Empty;

        /// <summary>
        /// 下载地址，url 为空时返回空串
        /// </summary>
        public string Url => url ?? string.Empty;

        /// <summary>
        /// 是否存在可用的下载地址
        /// </summary>
        public bool HasUrl => !string.IsNullOrEmpty(url);
    }

    /// <summary>
    /// 游戏版本更新器
    /// 职责：
    /// 1. 初始化时从 VersionInfoUrl 获取版本信息 json
    /// 2. 任何原因导致获取失败（无网络/超时/数据非法/解析异常）都直接跳出版本检查，不做任何处理
    /// 3. 成功拿到信息后解析并抛出 GameEvent.OnVersionInfoUpdate 事件，由界面层监听并弹窗
    /// </summary>
    public class GameVersion : Singleton<GameVersion>
    {
        /// <summary>
        /// 默认的版本信息 json 地址
        /// </summary>
        public const string DEFAULT_VERSION_INFO_URL = "https://gitcode.com/gametank/sango_infinity/releases/download/version/version.json";

        /// <summary>
        /// 版本信息 json 的下载地址，可在运行时修改以切换检查源
        /// </summary>
        public string VersionInfoUrl = DEFAULT_VERSION_INFO_URL;

        /// <summary>
        /// 单次版本检查的请求超时时间（秒）
        /// </summary>
        public float Timeout = 10f;

        /// <summary>
        /// 本地当前版本号，取自 Application.version
        /// </summary>
        public string CurrentVersion => UnityEngine.Application.version;

        /// <summary>
        /// 最近一次成功获取到的远端版本信息，未获取成功时为 null
        /// </summary>
        public VersionInfo LatestVersionInfo { get; private set; }

        /// <summary>
        /// 是否正在进行版本检查，用于避免重复发起请求
        /// </summary>
        public bool IsChecking { get; private set; }

        /// <summary>
        /// 是否已经执行过初始化，保证只初始化一次
        /// </summary>
        private bool inited = false;

        /// <summary>
        /// 初始化版本更新器，由 Game.Init 调用
        /// 内部启动协程异步请求版本信息，不会阻塞主线程
        /// </summary>
        public void Init()
        {
            if (inited)
                return;
            inited = true;
            CheckUpdate();
        }

        /// <summary>
        /// 主动发起一次版本检查
        /// 若地址为空或正处于检查中则直接跳过
        /// </summary>
        public void CheckUpdate()
        {
            if (IsChecking)
            {
                Sango.Log.Warning("版本检查正在进行中，本次检查已跳过", Sango.Log.LogType.Network);
                return;
            }

            if (string.IsNullOrEmpty(VersionInfoUrl))
            {
                Sango.Log.Warning("版本信息地址为空，跳过版本检查", Sango.Log.LogType.Network);
                return;
            }

            App app = App.Instance;
            if (app == null)
            {
                Sango.Log.Warning("游戏框架尚未初始化，跳过版本检查", Sango.Log.LogType.Network);
                return;
            }

            IsChecking = true;
            app.StartCoroutine(RequestVersionInfo());
        }

        /// <summary>
        /// 请求版本信息 json 的协程
        /// 任何异常都会在此被捕获并静默结束，不会向外抛出
        /// </summary>
        /// <returns>协程迭代器</returns>
        private IEnumerator RequestVersionInfo()
        {
            string json = null;

            using (UnityWebRequest request = UnityWebRequest.Get(VersionInfoUrl))
            {
                request.timeout = Mathf.CeilToInt(Timeout);
                yield return request.SendWebRequest();

                // 网络错误、协议错误、连接错误等任何失败情况都直接放弃本次版本检查
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Sango.Log.Warning("获取版本信息失败，跳过版本检查：" + request.error, Sango.Log.LogType.Network);
                    IsChecking = false;
                    yield break;
                }

                json = request.downloadHandler != null ? request.downloadHandler.text : null;
            }

            // 数据为空或解析失败同样视为没有拿到信息，不做任何处理
            VersionInfo info = ParseVersionInfo(json);
            if (info == null)
            {
                IsChecking = false;
                yield break;
            }

            // 标记远端版本是否比本地版本新，供界面层判断是否弹窗
            info.hasNewVersion = CompareVersion(info.Version, CurrentVersion) > 0;
            LatestVersionInfo = info;

            Sango.Log.Info("版本检查完成，本地版本：" + CurrentVersion + "，远端版本：" + info.Version
                + "，是否存在新版本：" + info.hasNewVersion, Sango.Log.LogType.Network);

            IsChecking = false;

            // 抛出事件，界面层监听该事件后自行弹窗展示更新说明与下载地址
            GameEvent.OnVersionInfoUpdate?.Invoke(info);
            Window.Instance.Open("window_update");
        }

        /// <summary>
        /// 解析版本信息 json
        /// 解析结果为 null 时表示数据无效
        /// </summary>
        /// <param name="json">远端下发的 json 文本</param>
        /// <returns>解析成功返回版本信息，失败返回 null</returns>
        private VersionInfo ParseVersionInfo(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Sango.Log.Warning("版本信息内容为空，跳过版本检查", Sango.Log.LogType.Network);
                return null;
            }

            VersionInfo info = null;
            try
            {
                info = JsonUtility.FromJson<VersionInfo>(json);
            }
            catch (Exception e)
            {
                Sango.Log.Warning("版本信息解析异常，跳过版本检查：" + e.Message, Sango.Log.LogType.Network);
                return null;
            }

            if (info == null || string.IsNullOrEmpty(info.Version))
            {
                Sango.Log.Warning("版本信息数据无效，跳过版本检查", Sango.Log.LogType.Network);
                return null;
            }

            return info;
        }

        /// <summary>
        /// 比较两个版本号的大小
        /// 支持形如 1.2.3 的点分数字格式，位数不足的按 0 补齐
        /// </summary>
        /// <param name="left">左侧版本号</param>
        /// <param name="right">右侧版本号</param>
        /// <returns>left 大于 right 返回正数，相等返回 0，小于返回负数</returns>
        public static int CompareVersion(string left, string right)
        {
            string leftText = string.IsNullOrEmpty(left) ? "0" : left.Trim();
            string rightText = string.IsNullOrEmpty(right) ? "0" : right.Trim();

            string[] leftParts = leftText.Split('.');
            string[] rightParts = rightText.Split('.');
            int count = System.Math.Max(leftParts.Length, rightParts.Length);

            for (int i = 0; i < count; i++)
            {
                int leftValue = GetVersionPart(leftParts, i);
                int rightValue = GetVersionPart(rightParts, i);
                if (leftValue != rightValue)
                    return leftValue > rightValue ? 1 : -1;
            }

            return 0;
        }

        /// <summary>
        /// 取版本号中指定下标的数字段，下标越界或不是数字时按 0 处理
        /// </summary>
        /// <param name="parts">按点分割后的版本号数组</param>
        /// <param name="index">要取的下标</param>
        /// <returns>该段的整数值</returns>
        private static int GetVersionPart(string[] parts, int index)
        {
            if (parts == null || index < 0 || index >= parts.Length)
                return 0;

            int value = 0;
            return int.TryParse(parts[index], out value) ? value : 0;
        }
    }
}
