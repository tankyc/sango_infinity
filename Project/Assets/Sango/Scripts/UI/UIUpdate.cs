using Sango.Manager;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System.Collections.Generic;

namespace Sango.UI
{
    /// <summary>
    /// 更新说明窗口（window_update）。
    ///
    /// 【两个入口】
    ///   · 启动时版本检查发现新版本，GameVersion 自动打开本窗口；
    ///   · 开始界面点「更新说明」按钮，UIStart.OpenUpdate 打开本窗口。
    ///
    /// 【两种数据状态】
    ///   · 已经拿到远端版本信息 → 直接展示 description；
    ///   · 还没拿到（检查未完成 / 网络失败 / 启动时被跳过）→ 先显示等待文案，
    ///     并在窗口打开期间补发一次检查，拿到后由 GameEvent.OnVersionInfoUpdate 原地刷新，
    ///     不需要玩家关掉再点一次。
    ///
    /// 【为什么要分块】见 <see cref="LinesPerText"/>。
    /// </summary>
    public class UIUpdate : UGUIWindow
    {
        /// <summary>
        /// 更新说明正文。预制体上只有这一个 Text，它同时充当后续分块的**克隆模板**
        /// （字号 / 颜色 / 描边 / ContentSizeFitter 一并复制，保证分块后排版与原来完全一致）。
        /// </summary>
        public Text text;

        /// <summary>
        /// 单个 Text 最多承载的行数。
        ///
        /// 【为什么必须切块】UGUI 的 Text 会把整段文字生成到**一张** mesh 里，
        /// 顶点数超过 65535 就抛异常（典型报错 `Mesh.vertices is too small` /
        /// `ArgumentOutOfRangeException: Index was outside the bounds of the array`），
        /// 表现为更新说明窗口直接打不开。更新说明是"每条一行"的长文本，
        /// 几个版本累积下来很容易冲破上限。
        ///
        /// 【为什么按 20 行】按行切不会把一条更新说明从中间截断，行高与自动换行都不受影响；
        /// 20 行 × 每行约 60 字 ≈ 1200 字，单块顶点数只有几百，离上限有两个数量级的余量。
        /// </summary>
        const int LinesPerText = 20;

        /// <summary>
        /// 分块后的正文 Text 池。第 0 个固定是预制体上的 <see cref="text"/> 本身，
        /// 其余是它的克隆；多出来的分块隐藏，下次打开直接复用，不反复创建销毁。
        /// </summary>
        readonly List<Text> contentTexts = new List<Text>();

        public override void OnOpen()
        {
            base.OnOpen();

            // 先减后加：窗口可能被反复打开（按钮点击 / 版本检查自动弹），避免重复订阅
            GameEvent.OnVersionInfoUpdate -= OnVersionInfoUpdate;
            GameEvent.OnVersionInfoUpdate += OnVersionInfoUpdate;

            GameVersion version = GameVersion.Instance;
            RefreshContent(version != null ? version.LatestVersionInfo : null);

            // 没有数据时补发一次检查（IsChecking 为 true 表示已有请求在飞，不重复发起）
            if (version != null && version.LatestVersionInfo == null && !version.IsChecking)
            {
                Sango.Log.Info("更新说明窗口：尚未获取到版本信息，重新发起版本检查", Sango.Log.LogType.Network);
                version.CheckUpdate();
            }
        }

        public override void OnClose()
        {
            // 窗口关闭后不再需要刷新，必须解绑，否则会持有已关闭窗口的引用
            GameEvent.OnVersionInfoUpdate -= OnVersionInfoUpdate;
            base.OnClose();
        }

        /// <summary>
        /// 版本信息更新回调（GameVersion 拿到远端 json 后抛出）。
        /// </summary>
        /// <param name="info">远端版本信息，可能为 null</param>
        void OnVersionInfoUpdate(VersionInfo info)
        {
            RefreshContent(info);
        }

        /// <summary>
        /// 刷新正文：有数据展示说明，没数据展示等待文案。
        /// </summary>
        /// <param name="info">远端版本信息，可为 null</param>
        void RefreshContent(VersionInfo info)
        {
            if (text == null)
                return;

            if (info == null)
            {
                ShowContent("正在获取更新说明……");
                return;
            }

            ShowContent(string.IsNullOrEmpty(info.Description)
                ? "本次更新没有附带说明"
                : info.Description);
        }

        /// <summary>
        /// 把正文按 <see cref="LinesPerText"/> 行切块后分别塞进多个 Text。
        ///
        /// 【为什么不用 RichText 的 page 或者滚动条分段】分块只影响"由几个 Text 承载"，
        /// 版面上它们仍在同一个 VerticalLayoutGroup 里、依次垂直排列，
        /// 玩家看到的是一整段连续文字，与单个 Text 无差别。
        /// </summary>
        /// <param name="content">完整正文</param>
        void ShowContent(string content)
        {
            List<string> chunks = SplitIntoChunks(content, LinesPerText);
            EnsureTextCount(chunks.Count);

            for (int i = 0; i < contentTexts.Count; i++)
            {
                Text chunkText = contentTexts[i];
                if (chunkText == null)
                    continue;

                bool used = i < chunks.Count;
                if (chunkText.gameObject.activeSelf != used)
                    chunkText.gameObject.SetActive(used);

                if (!used)
                    continue;

                chunkText.text = chunks[i];
                // 顺序即分段顺序：布局组按 sibling index 从上到下排列
                chunkText.transform.SetSiblingIndex(i);
            }
        }

        /// <summary>
        /// 保证分块 Text 的数量足够（不够就克隆 <see cref="text"/>）。
        /// </summary>
        /// <param name="count">本次需要的分块数</param>
        void EnsureTextCount(int count)
        {
            // 模板引用变了（窗口被重建 / 克隆体被销毁）就重建池子，只在打开路径上跑一次
            if (contentTexts.Count == 0 || contentTexts[0] == null || contentTexts[0] != text)
            {
                contentTexts.Clear();
                contentTexts.Add(text);
            }

            while (contentTexts.Count < count)
            {
                // worldPositionStays = false：保持与模板相同的 local 坐标 / 尺寸
                Text clone = Instantiate(text, text.transform.parent, false);
                clone.name = text.name + "_" + contentTexts.Count;
                contentTexts.Add(clone);
            }
        }

        /// <summary>
        /// 把正文按"每 N 行"切成若干块。
        ///
        /// 【切点选择】只在换行符处切：更新说明是逐条一行的排版，
        /// 按字符数硬切会把一条说明拆到两个 Text 里，视觉上会多出断行；
        /// 按行切则与原文本完全等价。
        ///
        /// 【CRLF】切块时把行尾的 \r 丢掉，否则它会留在上一块末尾，
        /// UGUI 会把它当成一次额外换行，每块之间会多出一行空白。
        /// </summary>
        /// <param name="content">完整正文</param>
        /// <param name="linesPerChunk">每块最多行数</param>
        /// <returns>分块文本，至少一块</returns>
        static List<string> SplitIntoChunks(string content, int linesPerChunk)
        {
            List<string> chunks = new List<string>();
            if (string.IsNullOrEmpty(content))
            {
                chunks.Add(string.Empty);
                return chunks;
            }

            if (linesPerChunk < 1)
                linesPerChunk = 1;

            int lineStart = 0;
            int lineCount = 0;
            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] != '\n')
                    continue;

                lineCount++;
                if (lineCount < linesPerChunk)
                    continue;

                int end = i;
                if (end > lineStart && content[end - 1] == '\r')
                    end--;
                chunks.Add(content.Substring(lineStart, end - lineStart));
                lineStart = i + 1;
                lineCount = 0;
            }

            // 末尾剩余段（含"整篇不足 N 行"与"最后一行没有换行"两种情况）
            if (lineStart < content.Length || chunks.Count == 0)
                chunks.Add(content.Substring(lineStart));

            return chunks;
        }

        public void OnDownload()
        {
            VersionInfo info = GameVersion.Instance != null ? GameVersion.Instance.LatestVersionInfo : null;
            if (info == null || !info.HasUrl)
            {
                // 原实现对 LatestVersionInfo 直接解引用，版本检查失败时点下载会空引用崩溃
                Sango.Log.Warning("更新说明窗口：当前没有可用的下载地址", Sango.Log.LogType.Network);
                return;
            }

            Application.OpenURL(info.Url);
        }
    }
}
