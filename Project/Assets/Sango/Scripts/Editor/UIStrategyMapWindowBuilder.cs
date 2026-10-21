using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Sango.UI;

namespace Sango.EditorTools
{
    /// <summary>
    /// 一次性编辑器工具：生成「大地图预览」窗口预制体
    /// <c>Assets/Mods/Content/Assets/UI/Prefab/window_strategy_map.prefab</c>。
    ///
    /// 【为什么用脚本生成】
    ///   窗口结构固定（遮罩 / 窗框 / 地图显示区 / 底部按钮条），
    ///   但有多处字段需要引用（RawImage + 三个按钮 + 两个按钮文字），手拖容易漏绑；
    ///   用脚本生成可重复执行，后续调整布局直接重跑。
    ///
    /// 【复用现有美术资源】
    ///   · 窗框 / 标题：<c>Common/win_frame.prefab</c>
    ///   · 按钮：<c>Common/button_1.prefab</c>（自带 lab 文本 + 描边）
    ///   · 字体：<c>Assets/Resources/kaiti.ttf</c>
    /// </summary>
    public static class UIStrategyMapWindowBuilder
    {
        const string PrefabDir = "Assets/Mods/Content/Assets/UI/Prefab/";
        const string WindowPrefabPath = PrefabDir + "window_strategy_map.prefab";
        const string WinFramePath = PrefabDir + "Common/win_frame.prefab";
        const string ButtonPath = PrefabDir + "Common/button_1.prefab";
        const string FontPath = "Assets/Resources/kaiti.ttf";

        /// <summary>整窗尺寸（1920x1080 参考分辨率下四周留边）</summary>
        static readonly Vector2 PanelSize = new Vector2(1700f, 950f);

        /// <summary>地图显示区四边内缩：左右 24、下 92（让开按钮条）、上 100（让开标题）</summary>
        static readonly Vector4 MapPadding = new Vector4(24f, 92f, 24f, 100f);

        /// <summary>地图底衬颜色（贴图未覆盖的空隙）</summary>
        static readonly Color MapBackColor = new Color(0.07f, 0.08f, 0.09f, 0.96f);

        static Font uiFont;

        [MenuItem("SangoTools/构建大地图预览窗口")]
        public static void Build()
        {
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (uiFont == null)
                Debug.LogWarning("[大地图预览]找不到字体 kaiti.ttf，文字可能不显示");

            GameObject frameSrc = AssetDatabase.LoadAssetAtPath<GameObject>(WinFramePath);
            GameObject buttonSrc = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if (frameSrc == null || buttonSrc == null)
            {
                Debug.LogError($"[大地图预览]缺少基础预制体：frame={frameSrc != null}, button={buttonSrc != null}");
                return;
            }

            // ============ 根：Canvas + 射线 + 窗口脚本 ============
            GameObject root = new GameObject("window_strategy_map", typeof(RectTransform));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0f, 0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;
            root.AddComponent<GraphicRaycaster>();
            UIStrategyMapWindow window = root.AddComponent<UIStrategyMapWindow>();

            // ============ 全屏遮罩：点击关闭 ============
            GameObject mask = NewUI("mask", root.transform);
            Stretch(mask.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image maskImage = mask.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.72f);
            maskImage.raycastTarget = true;
            Button maskButton = mask.AddComponent<Button>();
            maskButton.transition = Selectable.Transition.None;
            maskButton.targetGraphic = maskImage;

            // ============ 面板 ============
            GameObject panel = NewUI("panel", root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = PanelSize;

            GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(frameSrc, panel.transform);
            frame.name = "win_frame";
            Stretch(frame.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);

            Text title = frame.transform.Find("title") != null ? frame.transform.Find("title").GetComponent<Text>() : null;
            if (title != null)
                title.text = "势力图";

            // win_frame 自带的确定 / 取消按钮本窗口用不到，
            // 留着会和底部按钮条重叠，这里直接隐藏
            Transform okNode = frame.transform.Find("button_ok");
            if (okNode != null)
                okNode.gameObject.SetActive(false);
            Transform cancelNode = frame.transform.Find("button_cancel");
            if (cancelNode != null)
                cancelNode.gameObject.SetActive(false);

            // ============ 地图显示区 ============
            GameObject mapArea = NewUI("mapArea", panel.transform);
            Stretch(mapArea.GetComponent<RectTransform>(), MapPadding.x, MapPadding.y, MapPadding.z, MapPadding.w);

            GameObject mapBg = NewUI("bg", mapArea.transform);
            Stretch(mapBg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image mapBgImage = mapBg.AddComponent<Image>();
            mapBgImage.color = MapBackColor;
            mapBgImage.raycastTarget = false;

            GameObject mapObj = NewUI("mapImage", mapArea.transform);
            Stretch(mapObj.GetComponent<RectTransform>(), 4f, 4f, 4f, 4f);
            RawImage rawImage = mapObj.AddComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;
            AspectRatioFitter fitter = mapObj.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            // ============ 底部按钮条 ============
            GameObject bar = NewUI("buttonBar", panel.transform);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 24f);
            barRect.sizeDelta = new Vector2(760f, 56f);
            HorizontalLayoutGroup layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 24f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            Button areaBorderButton = AddButton(buttonSrc, bar.transform, "areaBorderButton", "地域边界：开");
            Button overlayButton = AddButton(buttonSrc, bar.transform, "overlayButton", "地表叠加：关");
            Button closeButton = AddButton(buttonSrc, bar.transform, "closeButton", "关闭");

            // ============ 左下角操作提示 ============
            Text hint = AddText(panel, "hintLabel", "点击地图跳转 · Ctrl+M 打开本窗口", 20, TextAnchor.MiddleLeft,
                new Color(0.86f, 0.82f, 0.70f));
            RectTransform hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(0f, 0f);
            hintRect.pivot = new Vector2(0f, 0.5f);
            hintRect.anchoredPosition = new Vector2(40f, 52f);
            hintRect.sizeDelta = new Vector2(520f, 40f);

            // ============ 绑定字段 ============
            window.mapImage = rawImage;
            window.areaBorderButton = areaBorderButton;
            window.areaBorderLabel = FindLabel(areaBorderButton.transform);
            window.overlayButton = overlayButton;
            window.overlayLabel = FindLabel(overlayButton.transform);
            window.closeButton = closeButton;
            window.maskButton = maskButton;
            window.hintLabel = hint;

            // ============ 保存 ============
            System.IO.Directory.CreateDirectory(PrefabDir);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath, out bool success);
            // 注意：Sango 下存在 Object 类型，这里必须写全 UnityEngine.Object
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[大地图预览]生成{(success && saved != null ? "成功" : "失败")}：{WindowPrefabPath}\n{Dump(saved)}");
        }

        /// <summary>生成结果自检：打印关键字段是否绑上，便于在控制台核对</summary>
        static string Dump(GameObject prefab)
        {
            if (prefab == null)
                return "预制体为空";

            UIStrategyMapWindow window = prefab.GetComponent<UIStrategyMapWindow>();
            if (window == null)
                return "根节点缺少 UIStrategyMapWindow 组件";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(" mapImage=").Append(window.mapImage != null);
            sb.Append(" areaBorder=").Append(window.areaBorderButton != null).Append('/').Append(window.areaBorderLabel != null);
            sb.Append(" overlay=").Append(window.overlayButton != null).Append('/').Append(window.overlayLabel != null);
            sb.Append(" close=").Append(window.closeButton != null);
            sb.Append(" mask=").Append(window.maskButton != null);
            sb.Append(" hint=").Append(window.hintLabel != null);
            return sb.ToString();
        }

        // ==================== 小工具 ====================

        static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>拉伸铺满父节点（left/bottom/right/top 为四边内缩像素）</summary>
        static RectTransform Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        /// <summary>实例化 button_1 并按布局规则设置尺寸与文字</summary>
        static Button AddButton(GameObject src, Transform parent, string name, string label)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.name = name;

            LayoutElement layout = go.GetComponent<LayoutElement>();
            if (layout == null)
                layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = 220f;
            layout.preferredHeight = 56f;
            layout.flexibleWidth = 0f;

            Text text = FindLabel(go.transform);
            if (text != null)
                text.text = label;

            return go.GetComponent<Button>();
        }

        /// <summary>取按钮实例里的文本子节点（button_1 约定子节点名 lab）</summary>
        static Text FindLabel(Transform root)
        {
            if (root == null)
                return null;

            Transform lab = root.Find("lab");
            return lab != null ? lab.GetComponent<Text>() : null;
        }

        static Text AddText(GameObject parent, string name, string content, int fontSize, TextAnchor anchor, Color color)
        {
            GameObject go = NewUI(name, parent.transform);
            Text text = go.AddComponent<Text>();
            text.font = uiFont;
            text.fontSize = fontSize;
            text.text = content;
            text.alignment = anchor;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
