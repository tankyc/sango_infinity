using Sango.Core;
using Sango.Render;
using System.Collections.Generic;
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
    /// · 关闭按钮 —— 关闭窗口（**遮罩不再关闭窗口**，只有关闭按钮能关）。
    ///
    /// <para>
    /// 【据点标记】`panel/mapArea` 下的 `city` / `port` / `gate` 三个节点是**模板**，
    /// 运行时按据点的 `BuildingKindType` 各取其一复制到 `mapArea` 下并摆到对应格子上：
    /// 名称取 <see cref="SangoObject.Name"/>，颜色取势力旗色，位置与
    /// <see cref="OnClickMap"/> 的换算严格互逆（同一张图、同一套 uv → 世界坐标映射）。
    /// 模板本体保持隐藏，不参与显示。
    /// </para>
    /// </summary>
    public class UIStrategyMapWindow : UGUIWindow
    {
        /// <summary>显示势力图的 RawImage</summary>
        public RawImage mapImage;
        public RawImage mapBgImage;

        /// <summary>据点标记的父节点（prefab 里的 <c>panel/mapArea</c>）</summary>
        public RectTransform mapArea;

        /// <summary>城池标记模板（prefab 里的 <c>panel/mapArea/city</c>）</summary>
        public RectTransform cityTemplate;

        /// <summary>港口标记模板（prefab 里的 <c>panel/mapArea/port</c>）</summary>
        public RectTransform portTemplate;

        /// <summary>关隘标记模板（prefab 里的 <c>panel/mapArea/gate</c>）</summary>
        public RectTransform gateTemplate;

        /// <summary>已生成的据点标记（模板本身不在其中）</summary>
        readonly List<CityNode> cityNodes = new List<CityNode>();

        /// <summary>上一次布局时的地图显示尺寸（尺寸变化要重新摆位）</summary>
        Vector2 lastMapSize;

        /// <summary>一个据点标记：实例 + 对应据点</summary>
        class CityNode
        {
            public RectTransform rect;
            public City city;
        }

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

            // 遮罩不再关闭窗口：只清掉可能残留的监听，保持它纯拦截点击。
            // （需求：点 mask 不关界面，只有「关闭」按钮能关）
            if (maskButton != null)
            {
                maskButton.onClick.RemoveAllListeners();
                maskButton.transition = Selectable.Transition.None;
            }

            // 三个据点模板只是"模具"，运行时一律隐藏 —— 真正的标记由 BuildCityNodes 复制生成。
            // 放在 Awake 里做，是为了覆盖"Initialize 因战略图层缺失提前返回"时模板露在画面上的情况。
            if (cityTemplate != null)
                cityTemplate.gameObject.SetActive(false);
            if (portTemplate != null)
                portTemplate.gameObject.SetActive(false);
            if (gateTemplate != null)
                gateTemplate.gameObject.SetActive(false);

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

            // 据点标记：城 / 港 / 关
            ResolveCityTemplates();
            BuildCityNodes();

            RenderTexture rt = strategyMap.FactionMapRT;
            Log.Info($"大地图预览已打开: 贴图={(rt != null ? rt.width + "x" + rt.height : "未生成")}, 地表叠加={strategyMap.IsVisible}, 地域边界={strategyMap.showAreaBorder}, 据点标记={cityNodes.Count}");
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

            // 地图显示区域尺寸变了（窗口缩放 / 宽高比适配）→ 据点标记要重新摆位
            if (cityNodes.Count > 0 && mapImage != null)
            {
                Vector2 size = mapImage.rectTransform.rect.size;
                if (size != lastMapSize)
                {
                    lastMapSize = size;
                    LayoutCityNodes();
                }
            }
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

        /// <summary>
        /// 点击地图：换算成世界坐标并跳转。
        ///
        /// <para>
        /// 预览图与世界的对应关系（与 <see cref="LayoutCityNodes"/> 严格互逆）：
        /// u（横）= 格列，世界 Z 随列走；v（纵）= 格行，世界 X 随行走且上下翻转。
        /// 世界上这两轴是**转置**的 —— <c>HexWorld.CoordsToPosition</c> 返回
        /// <c>new Vector3(p.y, 0, p.x)</c>（Framework/Hex/Hex.cs:312-316），
        /// 即世界 X 是行方向、世界 Z 是列方向。
        /// </para>
        /// </summary>
        void OnClickMap()
        {
            if (mapImage == null)
                return;

            RectTransform rect = mapImage.rectTransform;
            Camera uiCamera = Game.Instance.UICamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, uiCamera, out Vector2 local))
                return;

            Vector2 size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;

            MapRender mapRender = MapRender.Instance;
            MapGrid grid = mapRender != null ? mapRender.mapGrid : null;
            if (grid == null || grid.bounds.x <= 0 || grid.bounds.y <= 0)
                return;

            // 战略图贴图一格一像素，按格数归一化即与贴图逐像素对齐
            float u = Mathf.Clamp01((local.x + size.x * 0.5f) / size.x);
            float v = Mathf.Clamp01((size.y * 0.5f - local.y) / size.y);

            float worldZ = u * grid.bounds.x * grid.gridSize;
            float worldX = v * grid.bounds.y * grid.gridSize;

            mapRender.MoveCameraTo(new Vector3(worldX, 0f, worldZ));

            //OnCloseClick();
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

        /// <summary>
        /// 取三个据点模板：优先用 Inspector 上拖的引用，没拖就按固定路径兜底查找
        /// （`panel/mapArea/city` / `port` / `gate`），保证 prefab 忘绑也能跑。
        /// </summary>
        void ResolveCityTemplates()
        {
            if (mapArea == null)
            {
                Transform areaT = transform.Find("panel/mapArea");
                mapArea = areaT as RectTransform;
            }

            RectTransform root = mapArea != null ? mapArea : transform as RectTransform;
            if (root == null)
                return;

            if (cityTemplate == null)
                cityTemplate = root.Find("city") as RectTransform;
            if (portTemplate == null)
                portTemplate = root.Find("port") as RectTransform;
            if (gateTemplate == null)
                gateTemplate = root.Find("gate") as RectTransform;
        }

        /// <summary>
        /// 生成据点标记：遍历全地图据点，按类型选模板复制到 <see cref="mapArea"/> 下。
        ///
        /// <para>
        /// 只处理 `IsCityBase()` 的据点（城 / 港 / 关）；内政设施、障碍物等不在这张图上标。
        /// 模板本体 `SetActive(false)` —— 它是"模具"，不能作为多出来的一个点留在画面上。
        /// </para>
        /// </summary>
        void BuildCityNodes()
        {
            ClearCityNodes();

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.citySet == null)
                return;

            if (cityTemplate != null)
                cityTemplate.gameObject.SetActive(false);
            if (portTemplate != null)
                portTemplate.gameObject.SetActive(false);
            if (gateTemplate != null)
                gateTemplate.gameObject.SetActive(false);

            RectTransform parent = mapArea != null
                ? mapArea
                : (mapImage != null ? mapImage.rectTransform.parent as RectTransform : null);
            if (parent == null)
                return;

            int count = scenario.citySet.Count;
            for (int i = 0; i < count; i++)
            {
                City city = scenario.citySet[i];
                if (city == null || !city.IsAlive || !city.IsCityBase())
                    continue;

                RectTransform template = city.IsPort() ? portTemplate
                    : (city.IsGate() ? gateTemplate : cityTemplate);
                if (template == null)
                    continue;

                RectTransform node = Instantiate(template, parent);
                node.gameObject.SetActive(true);
                node.name = "node_" + city.Id;
                ApplyCityNode(node, city);

                CityNode item = new CityNode();
                item.rect = node;
                item.city = city;
                cityNodes.Add(item);
            }

            LayoutCityNodes();
            lastMapSize = mapImage != null ? mapImage.rectTransform.rect.size : Vector2.zero;
        }

        /// <summary>
        /// 填一个据点标记的内容：颜色 = 势力旗色，名字 = 据点名。
        /// 子物体名固定为 <c>color</c> / <c>name</c>（与 prefab 模板一致），找不到就跳过。
        /// </summary>
        /// <param name="node">标记实例</param>
        /// <param name="city">据点</param>
        static void ApplyCityNode(RectTransform node, City city)
        {
            Transform colorT = node.Find("color");
            Image colorImage = colorT != null ? colorT.GetComponent<Image>() : null;
            if (colorImage != null)
                colorImage.color = city.BelongForce != null ? city.BelongForce.Color : Color.gray;

            Transform nameT = node.Find("name");
            Text nameText = nameT != null ? nameT.GetComponent<Text>() : null;
            if (nameText != null)
                nameText.text = city.Name;

            // 标记不拦截点击：点在城/港/关上等同点地图同一格 ——
            // 否则名字那一块会把点击吃掉，出现"点地图某些位置没反应"
            Graphic[] graphics = node.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        /// <summary>
        /// 把据点摆到地图上：与 <see cref="OnClickMap"/> 的换算**严格互逆**，
        /// 保证"点在标记上"与"点到地图同一点"落到同一个世界坐标。
        ///
        /// <para>
        /// 贴图约定（见 <see cref="MapStrategyMap"/>）：一格一像素，纹理 u 轴是格列
        /// <c>city.x</c>，v 轴翻转后是格行 <c>city.y</c>。
        /// 世界坐标则是**转置**的：世界 Z 随列走、世界 X 随行走，奇数列还多半个格的错位
        /// （出处：Framework/Hex/Hex.cs:270-275 的 <c>HexToPixel</c> 与
        /// Hex.cs:312-316 的 <c>CoordsToPosition</c>）。所以横向用 <c>city.x</c>、
        /// 纵向用 <c>city.y</c> —— 与 <see cref="UIScenarioCityMap"/> 的摆法一致。
        /// </para>
        /// </summary>
        void LayoutCityNodes()
        {
            if (mapImage == null || cityNodes.Count == 0)
                return;

            MapRender mapRender = MapRender.Instance;
            MapGrid grid = mapRender != null ? mapRender.mapGrid : null;
            if (grid == null)
                return;

            // 贴图宽高 = 地格列数 / 行数，所以直接按格数归一化
            int cellsX = grid.bounds.x;
            int cellsY = grid.bounds.y;
            if (cellsX <= 0 || cellsY <= 0)
                return;

            RectTransform imageRect = mapImage.rectTransform;
            Vector2 size = imageRect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;

            // 标记挂在 mapArea 下，而 mapImage 上有 AspectRatioFitter 会改变自身尺寸与位置，
            // 所以用坐标变换把"贴图上的点"换算到父节点空间，不能直接拿 anchoredPosition 相加。
            RectTransform parentRect = mapArea != null ? mapArea : imageRect.parent as RectTransform;
            if (parentRect == null)
                return;

            for (int i = 0; i < cityNodes.Count; i++)
            {
                CityNode node = cityNodes[i];
                if (node.rect == null || node.city == null)
                    continue;

                int col = node.city.x;
                int row = node.city.y;

                // u：0~1 左→右（格列）；v：0~1 上→下（格行，含奇数列的半格错位）
                float u = (col + 0.5f) / cellsX;
                float v = (row + (col & 1) * 0.5f + 0.5f) / cellsY;

                Vector2 local = new Vector2(
                    u * size.x - size.x * 0.5f,
                    size.y * 0.5f - v * size.y);

                if (parentRect == imageRect)
                {
                    node.rect.anchoredPosition = local;
                }
                else
                {
                    Vector3 worldPoint = imageRect.TransformPoint(new Vector3(local.x, local.y, 0f));
                    Vector3 inParent = parentRect.InverseTransformPoint(worldPoint);
                    node.rect.localPosition = new Vector3(inParent.x, inParent.y, node.rect.localPosition.z);
                }
            }
        }

        /// <summary>销毁已生成的据点标记（模板本身不动）</summary>
        void ClearCityNodes()
        {
            for (int i = 0; i < cityNodes.Count; i++)
            {
                if (cityNodes[i].rect != null)
                    Destroy(cityNodes[i].rect.gameObject);
            }
            cityNodes.Clear();
        }

        /// <summary>关闭窗口时清掉运行时标记，避免池化复用时残留</summary>
        public override void OnClose()
        {
            base.OnClose();
            ClearCityNodes();
        }

        /// <summary>关闭窗口</summary>
        void OnCloseClick()
        {
            Window.Instance.Close("window_strategy_map");
        }
    }
}
