/*
 * 文件名：LoadingProgress.cs
 * 描述：剧本加载进度报告器，把当前加载阶段与百分比显示到 window_loading 界面的文本上
 * 创建日期：2026-10-13
 * 最后修改：2026-10-13
 */

using UnityEngine;
using UnityEngine.UI;

namespace Sango
{
    /// <summary>
    /// 剧本加载进度报告器：
    /// 把当前加载阶段与百分比显示到 window_loading 界面的文本上，
    /// 使玩家在加载过程中能看到具体进度，而不是一张静止的"加载中"图。
    /// 注意：各加载阶段之间必须由调用方分帧（yield return null），
    /// 否则文本虽然被更新，但不会渲染到屏幕上。
    /// </summary>
    public static class LoadingProgress
    {
        /// <summary>loading 窗口名称</summary>
        const string LoadingWindowName = "window_loading";

        /// <summary>进度文本节点名称（与 window_loading.prefab 中的节点保持一致）</summary>
        const string TextNodeName = "Text (Legacy)";

        /// <summary>进度文本组件缓存（窗口对象由 PoolManager 管理，销毁后 Unity 判空会自动失效）</summary>
        static Text cachedText;

        /// <summary>
        /// 报告当前加载进度，更新 loading 界面上的文本。
        /// </summary>
        /// <param name="stage">阶段名（中文），传 null 表示恢复默认文案</param>
        /// <param name="percent">进度百分比，范围 0~1</param>
        public static void Report(string stage, float percent)
        {
            Text text = FindLoadingText();
            if (text == null)
                return;

            if (string.IsNullOrEmpty(stage))
            {
                text.text = "加载中...";
            }
            else
            {
                text.text = string.Format("{0}... {1}%", stage, System.Math.Round(percent * 100f));
            }
        }

        /// <summary>
        /// 加载结束时调用：恢复默认文案并清除文本组件缓存。
        /// </summary>
        public static void Reset()
        {
            cachedText = null;
            Report(null, 0f);
        }

        /// <summary>
        /// 查找 loading 窗口中的进度文本组件（带缓存）。
        /// </summary>
        /// <returns>进度文本组件，窗口未打开或节点不存在时返回 null</returns>
        static Text FindLoadingText()
        {
            // 缓存有效（未被销毁）时直接复用
            if (cachedText != null)
                return cachedText;

            Window.WindowInterface win = Window.Instance.GetWindow(LoadingWindowName);
            if (win == null || win.ugui_instance == null)
                return null;

            Transform node = win.ugui_instance.transform.Find(TextNodeName);
            if (node == null)
                return null;

            cachedText = node.GetComponent<Text>();
            return cachedText;
        }
    }
}
