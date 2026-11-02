/*
 * 文件名：TelemetryRunner.cs
 * 描述：上报协程宿主 + HTTP 发送工具。
 *
 * 【为什么要一个宿主】
 *   UnityWebRequest 只能在主线程创建与驱动，而日志回调可能在子线程；
 *   所以采集侧只入队，由这个常驻 MonoBehaviour 在 Update 里定时取批发送。
 *   组队上报（TroopTelemetry）也借它的 StartCoroutine 发请求，避免各自造宿主。
 *
 * 【失败处理】
 *   发送失败（断网、服务端没开）时整批塞回队头，按 FlushInterval 天然退避重试；
 *   队列有上限，不会无限增长。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Sango.Core.Telemetry
{
    /// <summary>
    /// 上报协程宿主。由 BugReporter.Bootstrap 自动创建，DontDestroyOnLoad 常驻。
    /// </summary>
    public class TelemetryRunner : MonoBehaviour
    {
        /// <summary>全局实例（其它模块借协程用）</summary>
        public static TelemetryRunner Instance { get; private set; }

        /// <summary>下一次允许发送的时间点（Time.realtimeSinceStartup）</summary>
        private float nextFlushAt;

        /// <summary>是否有一批正在发送</summary>
        private bool sending;

        /// <summary>
        /// 取得（必要时创建）常驻宿主。
        /// BugReporter 与 TroopTelemetry 都可能在对方之前初始化，统一走这里，
        /// 避免两边各造一个 GameObject（重复的那个会在 Awake 里自杀，白创建一次）。
        /// </summary>
        /// <returns>可用的宿主实例</returns>
        public static TelemetryRunner Ensure()
        {
            if (Instance != null) return Instance;

            GameObject host = new GameObject("[TelemetryRunner]");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideInHierarchy;
            return host.AddComponent<TelemetryRunner>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            nextFlushAt = Time.realtimeSinceStartup + TelemetryConfig.FlushInterval;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (sending) return;
            if (!TelemetryConfig.IsConfigured) return;
            if (Time.realtimeSinceStartup < nextFlushAt) return;

            if (BugReporter.PendingCount == 0)
            {
                // 空队列只顺延检查时间，不做任何网络动作
                nextFlushAt = Time.realtimeSinceStartup + TelemetryConfig.FlushInterval;
                return;
            }

            StartCoroutine(FlushBugs());
        }

        /// <summary>借用宿主跑一个协程（组队上报用）</summary>
        public void Run(IEnumerator routine)
        {
            if (routine == null) return;
            if (!isActiveAndEnabled) return;
            StartCoroutine(routine);
        }

        /// <summary>把队列里的错误分批发出去</summary>
        private IEnumerator FlushBugs()
        {
            sending = true;

            while (true)
            {
                List<BugEntry> batch = BugReporter.TakeBatch(TelemetryConfig.MaxBatchPerRequest);
                if (batch.Count == 0) break;

                BugBatch payload = new BugBatch();
                payload.items = batch;

                string json = JsonUtility.ToJson(payload);
                bool ok = false;

                yield return TelemetrySender.Post("/bugs", json, result => ok = result);

                if (!ok)
                {
                    // 失败：塞回队头，等下一轮（FlushInterval 即为退避间隔）
                    BugReporter.ReturnToQueue(batch);
                    break;
                }
            }

            nextFlushAt = Time.realtimeSinceStartup + TelemetryConfig.FlushInterval;
            sending = false;
        }
    }

    /// <summary>
    /// 极简 HTTP 上报。统一带上 X-Api-Key，服务端据此拒掉无关请求。
    /// </summary>
    public static class TelemetrySender
    {
        /// <summary>
        /// POST 一段 JSON。
        /// @param path  以 / 开头的接口路径，如 "/bugs"
        /// @param json  请求体
        /// @param onDone 回调（主线程），true 表示 HTTP 2xx
        /// </summary>
        public static IEnumerator Post(string path, string json, Action<bool> onDone)
        {
            if (!TelemetryConfig.IsConfigured)
            {
                onDone?.Invoke(false);
                yield break;
            }

            string url = TelemetryConfig.IngestUrl.TrimEnd('/') + path;

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] body = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Api-Key", TelemetryConfig.ApiKey);
                request.timeout = 15;

                yield return request.SendWebRequest();

                bool ok = request.result == UnityWebRequest.Result.Success;
                if (!ok)
                {
                    Sango.Log.Warning($"[Telemetry] 上报失败（{path}）：{request.responseCode} {request.error}");
                }

                onDone?.Invoke(ok);
            }
        }
    }
}
