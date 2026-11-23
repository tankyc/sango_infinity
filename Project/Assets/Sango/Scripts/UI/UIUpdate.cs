using Sango.Manager;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using System.Collections;
using System;

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
    /// </summary>
    public class UIUpdate : UGUIWindow
    {
        /// <summary>更新说明正文</summary>
        public Text text;

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
                text.text = "正在获取更新说明……";
                return;
            }

            text.text = string.IsNullOrEmpty(info.Description)
                ? "本次更新没有附带说明"
                : info.Description;
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
