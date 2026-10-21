using Sango.Core;
using Sango.Render;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 大地图预览窗口：把 <see cref="MapStrategyMap"/> 烘焙出的势力归属 RenderTexture
    /// 直接铺到 RawImage 上，用于整体查看势力分布、势力边界与地域（城池辖区）边界。
    ///
    /// 交互：
    /// · 点击地图任意位置 —— 相机跳转到对应世界坐标并关闭窗口；
    /// · 「地域边界」按钮 —— 开关城池辖区界线（会重建贴图）；
    /// · 「地表叠加」按钮 —— 开关在三维地表上叠加同一张势力图；
    /// · 遮罩 / 关闭按钮 —— 关闭窗口。
    /// </summary>
    public class UIStrategyMapWindow : UGUIWindow
    {
        /// <summary>显示势力图的 RawImage</summary>
        public RawImage mapImage;
        public RawImage mapBgImage;

        /// <summary>地域边界开关按钮</summary>
        public Button areaBorderButton;

        /// <summary>地域边界按钮文字</summary>
        public Text areaBorderLabel;

        /// <summary>地表叠加开关按钮</summary>
        public Button overlayButton;

        /// <summary>地表叠加按钮文字</summary>
        public Text overlayLabel;

        /// <summary>关闭按钮</summary>
        public Button closeButton;

        /// <summary>全屏遮罩按钮（点击关闭）</summary>
        public Button maskButton;

        /// <summary>底部提示文字</summary>
        public Text hintLabel;

        /// <summary>当前绑定的战略地图图层</summary>
        MapStrategyMap strategyMap;

        /// <summary>贴图未就绪时的占位纹理（避免 RawImage 无纹理时画出一整块白色）</summary>
        static Texture2D placeholderTexture;

        /// <summary>取得深色占位纹理（懒创建，全局共用一张）</summary>
        static Texture2D Placeholder
        {
            get
            {
                if (placeholderTexture == null)
                {
                    placeholderTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    placeholderTexture.name = "StrategyMapPlaceholder";
                    placeholderTexture.SetPixel(0, 0, new Color(0.06f, 0.07f, 0.08f, 1f));
                    placeholderTexture.Apply();
                }
                return placeholderTexture;
            }
        }

        /// <summary>绑定按钮回调（只绑一次，避免窗口复用后重复触发）</summary>
        protected override void Awake()
        {
            base.Awake();

            if (areaBorderButton != null)
            {
                areaBorderButton.onClick.RemoveAllListeners();
                areaBorderButton.onClick.AddListener(OnToggleAreaBorder);
            }

            if (overlayButton != null)
            {
                overlayButton.onClick.RemoveAllListeners();
                overlayButton.onClick.AddListener(OnToggleOverlay);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseClick);
            }

            if (maskButton != null)
            {
                maskButton.onClick.RemoveAllListeners();
                maskButton.onClick.AddListener(OnCloseClick);
            }

            if (mapImage != null)
            {
                // 点击地图即跳转；RawImage 自身作为按钮的目标图形，不需要额外的高亮样式
                Button mapButton = mapImage.GetComponent<Button>();
                if (mapButton == null)
                    mapButton = mapImage.gameObject.AddComponent<Button>();
                mapButton.transition = Selectable.Transition.None;
                mapButton.targetGraphic = mapImage;
                mapButton.onClick.RemoveAllListeners();
                mapButton.onClick.AddListener(OnClickMap);
            }
        }

        /// <summary>
        /// 打开窗口（无参版本）。
        /// 注意：<c>Window.Instance.Open(name)</c> 走的是 <c>UGUIWindow.Open()</c>，
        /// 它调用的是**无参**的 <see cref="OnOpen()"/>；只覆写带 params 的版本不会被执行，
        /// 表现就是 RawImage 没绑到贴图、窗口一片白。
        /// </summary>
        public override void OnOpen()
        {
            base.OnOpen();
            Initialize();
        }

        /// <summary>打开窗口（带参版本，供 <c>Window.Open(name, args)</c> 使用）</summary>
        public override void OnOpen(params object[] objects)
        {
            base.OnOpen(objects);
            Initialize();
        }

        /// <summary>窗口初始化：确保贴图已生成并绑定到 RawImage</summary>
        void Initialize()
        {
            mapBgImage.texture = MapRender.LoadBoundTex();

            strategyMap = MapRender.Instance != null ? MapRender.Instance.mapStrategyMap : null;
            if (strategyMap == null)
            {
                Log.Error("大地图预览打开失败：战略地图图层不存在");
                return;
            }

            // 窗口可能是第一次打开，贴图尚未烘焙过
            if (strategyMap.FactionMapRT == null || strategyMap.IsDirty)
                strategyMap.Rebuild();

            BindTexture();
            RefreshLabels();

            RenderTexture rt = strategyMap.FactionMapRT;
            Log.Info($"大地图预览已打开: 贴图={(rt != null ? rt.width + "x" + rt.height : "未生成")}, 地表叠加={strategyMap.IsVisible}, 地域边界={strategyMap.showAreaBorder}");
        }

        /// <summary>贴图在窗口打开期间若被重建（城池易主），这里同步一次引用与宽高比</summary>
        void Update()
        {
            if (mapImage == null)
                return;

            // 地图渲染层可能晚于窗口就绪，这里补一次取引用
            if (strategyMap == null && MapRender.Instance != null)
                strategyMap = MapRender.Instance.mapStrategyMap;

            if (strategyMap != null && strategyMap.IsDirty)
                strategyMap.Rebuild();

            BindTexture();
        }

        /// <summary>把 RenderTexture 绑到 RawImage，并按贴图宽高比适配显示区域</summary>
        void BindTexture()
        {
            if (mapImage == null)
                return;

            RenderTexture rt = strategyMap != null ? strategyMap.FactionMapRT : null;
            if (rt == null)
            {
                // 贴图还没生成：用深色占位，避免 RawImage 无纹理时绘制纯白块
                if (mapImage.texture != Placeholder)
                    mapImage.texture = Placeholder;
                return;
            }

            if (mapImage.texture != rt)
            {
                mapImage.texture = rt;

                AspectRatioFitter fitter = mapImage.GetComponent<AspectRatioFitter>();
                if (fitter == null)
                {
                    fitter = mapImage.gameObject.AddComponent<AspectRatioFitter>();
                    fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                }
                fitter.aspectRatio = strategyMap.Aspect;
            }
        }

        /// <summary>刷新两个开关按钮的文字</summary>
        void RefreshLabels()
        {
            if (strategyMap == null)
                return;

            if (areaBorderLabel != null)
                areaBorderLabel.text = strategyMap.showAreaBorder ? "地域边界：开" : "地域边界：关";

            if (overlayLabel != null)
                overlayLabel.text = strategyMap.IsVisible ? "地表叠加：开" : "地表叠加：关";
        }

        /// <summary>点击地图：换算成世界坐标并跳转，随后关闭窗口</summary>
        void OnClickMap()
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null || mapImage == null)
                return;

            RectTransform rect = mapImage.rectTransform;
            Camera uiCamera = Game.Instance.UICamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, uiCamera, out Vector2 local))
                return;

            Vector2 size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;

            // 预览图与地图的对应关系：u（横）= 世界 z，v（纵）= 1 - 世界 x
            float worldWidth = scenario.Map.Width * scenario.Map.GridSize;
            float worldHeight = scenario.Map.Height * scenario.Map.GridSize;

            float z = (local.x + size.x * 0.5f) * worldWidth / size.x;
            float x = (size.y * 0.5f - local.y) * worldHeight / size.y;

            MapRender mapRender = MapRender.Instance;
            if (mapRender != null)
                mapRender.MoveCameraTo(new Vector3(x, 0f, z));

            OnCloseClick();
        }

        /// <summary>切换地域边界（城池辖区界线）</summary>
        void OnToggleAreaBorder()
        {
            if (strategyMap == null)
                return;

            strategyMap.SetAreaBorderVisible(!strategyMap.showAreaBorder);
            RefreshLabels();
        }

        /// <summary>切换是否在三维地表上叠加势力图</summary>
        void OnToggleOverlay()
        {
            if (strategyMap == null)
                return;

            strategyMap.SetVisible(!strategyMap.IsVisible);
            RefreshLabels();
        }

        /// <summary>关闭窗口</summary>
        void OnCloseClick()
        {
            Window.Instance.Close("window_strategy_map");
        }
    }
}
