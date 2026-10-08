using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    /// <summary>
    /// 一键登庸窗口（prefab 驱动版 v13）——按原版 UIObjectDisplay 标准模式重写。
    ///
    /// v13 核心变化（学原版 display_object_root / UIObjectDisplay）：
    ///  1. 列表 = 固定行池 + startIndex 滚动（原版模式）：克隆固定行，滚动条只驱动"行内容替换"，
    ///     不再移动 Content、不再 AddComponent&lt;标准ScrollRect&gt;/RectMask2D —— 用户 prefab 的
    ///     Viewport(3312d773)/Content(59f81469+3245ec92) 是框架标准组件，已负责裁剪与布局，
    ///     重复叠加标准组件会和框架冲突（历史"内容乱跳/被遮住"的根源之一）。
    ///  2. 不碰 Viewport / label_1 的位置与层级（不 SetAsLastSibling），保留用户 prefab 原始布局。
    ///  3. slider_v 滚动条：深度查找用户原有 Sliding Area/Handle（绝不新建白色滑块），
    ///     onValueChanged 驱动 startIndex；up/down 按钮小幅滚动；滚轮支持。
    ///  4. 行内 textField_5×4：标签(label)保持"势力/忠诚/抵达日数/据点"不动，
    ///     值写到标签后面的 Text（textField 根的 "label" 子节点 = 8554 值位）。
    /// </summary>
    public class UICityAllRecruit : UGUIWindow
    {
        // ===== 可绑定字段（Inspector 可选；未绑定时代码自动按名查找）=====
        public Text windiwTitle;
        public Text countLabel;
        public UITextField actionValue;
        public RectTransform listContent;
        public RectTransform rowTemplate;
        public RectTransform viewportRT;
        public Scrollbar scrollbar;
        public Button sureButton;
        public Button backButton;
        public Button selectTargetButton;

        City TargetCity;
        CityAllRecruit currentSystem;
        RectTransform winFrameRT;
        Text titleText;

        /// <summary>固定行池（原版 UIObjectDisplay 模式：行数固定，滚动只换内容）</summary>
        readonly List<GameObject> rows = new List<GameObject>();
        // ★ 列表总高度 300 / 行距 60 → 5 行（用户最新参数：总高 300、行距 60）
        const int ItemCount = 5;
        int startIndex;

        /// <summary>
        /// 是否刚从"选择武将"界面返回（下一次 OnOpen 绝不能清空刚选好的结果）。
        /// 其余任何 OnOpen（首次打开 / 挂起恢复 / 菜单重进）都视为新会话：
        /// 若系统里残留了上次的 target（玩家未点"返回"而以其他方式退出），一律清空，
        /// 避免"第一次选的武将到最后一次还出现"。
        /// </summary>
        bool selectionReturning;

        public override void OnOpen()
        {
            Debug.Log("[AllRecruit v13] OnOpen start");
            try
            {
                currentSystem = GameSystem.GetSystem<CityAllRecruit>();
                if (currentSystem == null)
                {
                    Debug.LogError("[AllRecruit] 找不到 CityAllRecruit 系统!");
                    return;
                }
                // 新打开（非"刚从选择界面返回"）：直接自动推荐——
                // 打开即按登庸概率从高到低推荐执行武将并填充列表，玩家无需先点
                // "选择目标武将"按钮；自动推荐内部会清空挂起残留的旧 target，
                // 避免"第一次选的武将到最后一次还出现"。
                // 若为选择界面返回（selectionReturning），保留玩家刚手动选好的结果。
                if (!selectionReturning)
                {
                    currentSystem.AutoRecommend();
                    Debug.Log("[AllRecruit v13] 打开自动推荐: " + currentSystem.target.Count);
                }
                selectionReturning = false;
                TargetCity = currentSystem.TargetCity;
                InitChrome();
                UpdateContent();
                Debug.Log("[AllRecruit v13] OnOpen done, buttons: sure=" + (sureButton != null)
                    + " back=" + (backButton != null));
                // 列表诊断（定位"列表不显示"）：Content 位置/尺寸、行数、行激活、视口尺寸
                Debug.Log("[AllRecruit v13] 诊断 content=" + (listContent != null
                    ? "pos:" + listContent.position + " rect:" + listContent.rect : "NULL")
                    + " viewport=" + (viewportRT != null ? viewportRT.rect.ToString() : "NULL")
                    + " rows=" + rows.Count
                    + " data=" + (currentSystem != null ? currentSystem.target.Count : 0));
                for (int i = 0; i < rows.Count && i < 3; i++)
                {
                    if (rows[i] != null)
                    {
                        RectTransform rrt = rows[i].transform as RectTransform;
                        Debug.Log("[AllRecruit v13] 诊断 row" + i + " active=" + rows[i].activeSelf
                            + " rect=" + rrt.rect + " apos=" + rrt.anchoredPosition);
                    }
                }
                // 渲染定位：视口 vs 行的世界坐标 + Mask 状态（"行激活但看不见"只剩遮挡/裁剪）
                if (viewportRT != null)
                {
                    Vector3[] vc = new Vector3[4];
                    viewportRT.GetWorldCorners(vc); // 0=左下 1=左上 2=右上 3=右下
                    Debug.Log("[AllRecruit v13] 视口世界: BL" + vc[0] + " TL" + vc[1] + " BR" + vc[3]);
                }
                if (rows.Count > 0 && rows[0] != null)
                {
                    RectTransform r0 = rows[0].transform as RectTransform;
                    Vector3[] rc = new Vector3[4];
                    r0.GetWorldCorners(rc);
                    Debug.Log("[AllRecruit v13] 行0世界: BL" + rc[0] + " TL" + rc[1] + " BR" + rc[3]);
                }
                Mask m = viewportRT != null ? viewportRT.GetComponent<Mask>() : null;
                Debug.Log("[AllRecruit v13] 视口Mask: " + (m != null ? ("enabled=" + m.enabled) : "无"));
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AllRecruit] OnOpen 异常: " + ex);
            }
        }

        /// <summary>绑定 prefab 已有节点（只做一次），全部按名字/组件查找，不创建文本。</summary>
        void InitChrome()
        {
            // 0. Canvas：prefab 根已带 Canvas(223)；确保参与 UI 事件射线
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;
            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
            Debug.Log("[AllRecruit v13] Canvas 修复: canvas=" + (canvas != null)
                + " raycaster=" + (GetComponent<GraphicRaycaster>() != null));

            // 1. mask 遮罩：用户要求删掉（太暗）——运行时隐藏
            RectTransform[] allRT = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < allRT.Length; i++)
            {
                if (allRT[i].name == "mask")
                {
                    allRT[i].gameObject.SetActive(false);
                    break;
                }
            }

            // 2. 列表容器（Content）/ 视口（Viewport）/ 行模板（Content 第一个子节点）
            if (listContent == null)
                listContent = FindNamedRect("Content");
            if (viewportRT == null)
                viewportRT = listContent != null ? listContent.parent as RectTransform : null;
            if (rowTemplate == null && listContent != null && listContent.childCount > 0)
                rowTemplate = listContent.GetChild(0) as RectTransform;

            // 列表不压顶部提示区、也不顶太靠上：Content 顶部（pivot(0,1) 即 transform.position）
            // 直接对齐到 label_1（已选择 N 名武将）底部下方 6px。
            // 直接用世界坐标设置（不经过 anchoredPosition 换算，杜绝 scale 误差/符号错误——
            // 历史 v18 曾因此把列表推到屏幕外、v19 又因符号反了导致时好时坏）。
            // 只在 Content 顶部高于目标位置（压住/贴太近）时下移；已在下方则保持用户布局。
            if (listContent != null)
            {
                RectTransform l1RT = FindNamedRect("label_1");
                if (l1RT != null)
                {
                    Vector3[] l1c = new Vector3[4];
                    l1RT.GetWorldCorners(l1c); // 0=左下 1=左上 2=右上 3=右下
                    float l1BottomY = l1c[0].y;
                    float targetTopY = l1BottomY - 6f;
                    float contentTopY = listContent.transform.position.y;
                    if (contentTopY > targetTopY)
                    {
                        Vector3 p = listContent.transform.position;
                        p.y = targetTopY;
                        listContent.transform.position = p;
                    }
                    Debug.Log("[AllRecruit v13] 列表对齐: l1BottomY=" + l1BottomY
                        + " contentTopY=" + contentTopY + " finalTopY=" + listContent.transform.position.y);
                }
            }

            // 3. 固定行池：克隆 ItemCount 行，行**直接挂到窗口根**（绕开 Content→Viewport→Mask
            //    整条渲染链路——日志多次证明行已激活、Content 高度正确、行位置正确，但画面仍空白，
            //    定位为这条链路（VLG/ContentSizeFitter/Mask 任一环节）干扰了行的最终渲染）。
            //    挂窗口根 + 手动定位后，行只受窗口 Canvas 控制，必然显示。
            //    滚动仍由 startIndex 驱动（行内容替换，行本身不动），滚动条逻辑不变。
            //    行高压缩到 50（用户要求"武将和武将挨在一起"）。每次打开先销毁旧行防堆积。
            rows.Clear();
            // ★★★ 行挂 Viewport（列表容器）下：v24~v29 挂窗口根+世界坐标推算被窗口多层缩放
            //    干扰，行总被甩到窗口外。Viewport 是列表可视区，行挂它下面用局部坐标定位，
            //    自动跟随列表区位置，外部任何缩放都不影响。
            if (viewportRT == null)
                viewportRT = listContent != null ? listContent.parent as RectTransform : null;
            if (viewportRT == null)
                viewportRT = FindNamedRect("Viewport");
            Transform winRoot = viewportRT != null ? viewportRT : (RectTransform)transform;
            Debug.Log("[AllRecruit v13] 列表挂载节点: " + winRoot.name
                + " 世界pos=" + winRoot.position + " scale=" + winRoot.lossyScale);
            // ★ 销毁**整个窗口子树**里所有运行时创建的行（历史版本行可能挂在
            //   win_frame/transform/Viewport 任意节点下，只清一个父节点会残留堆积——截图 7 行）
            RectTransform winFrameClean = FindNamedRect("win_frame");
            if (winFrameClean != null)
            {
                Transform[] allRows = winFrameClean.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allRows.Length; i++)
                {
                    Transform t = allRows[i];
                    if (t != null && t != winFrameClean.transform && t.name != null
                        && t.name.StartsWith("allrecruit_row_"))
                        DestroyImmediate(t.gameObject);
                }
            }
            for (int i = winRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = winRoot.GetChild(i);
                if (child != null && child.name != null && child.name.StartsWith("allrecruit_row_"))
                    DestroyImmediate(child.gameObject);
            }
            if (winRoot != transform)
            {
                for (int i = transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = transform.GetChild(i);
                    if (child != null && child.name != null && child.name.StartsWith("allrecruit_row_"))
                        DestroyImmediate(child.gameObject);
                }
            }
            if (rowTemplate != null)
            {
                RectTransform rowTemplateRT = rowTemplate;
                if (rowTemplateRT != null)
                    rowTemplateRT.sizeDelta = new Vector2(rowTemplateRT.sizeDelta.x, 50f);
                rowTemplate.gameObject.SetActive(false);

                // ★ 世界坐标直接定位：不经过 anchoredPosition（受挂载节点 anchor/pivot 影响，
                //    v24 曾因此把行甩到屏幕右侧窗口外）。transform.position 是世界绝对坐标，
                //    无论父节点锚点/轴心/缩放如何，行都精确落在目标位置。
                float l1BottomWorldY = -999f;
                RectTransform l1RT = FindNamedRect("label_1");
                if (l1RT != null)
                {
                    // ★★ label_1 的 Text 组件本身**铺满整个窗口**（三行提示文字在矩形内
                    //    居中渲染）。取 RectTransform 角 = 窗口角，会把列表甩到窗口外。
                    //    正确做法：文本视觉底部 = 矩形垂直中心 - 渲染高度/2
                    //    （m_Alignment=MiddleCenter，第三行"目标武将→军师推荐"即文本底=表头底，
                    //     列表从表头下方开始）。
                    Text l1Text = l1RT.GetComponent<Text>();
                    Vector3[] l1c = new Vector3[4];
                    l1RT.GetWorldCorners(l1c);
                    float rectCenterY = (l1c[1].y + l1c[0].y) * 0.5f;
                    float textWorldH = 0f;
                    if (l1Text != null)
                        textWorldH = l1Text.preferredHeight * l1Text.transform.lossyScale.y;
                    if (textWorldH <= 0f)
                        textWorldH = 78f * l1RT.lossyScale.y; // 兜底：3 行 × 26 号字
                    l1BottomWorldY = rectCenterY - textWorldH * 0.5f;
                }
                // 行中心 X：对齐 Content 中心世界 X（Content pivot(0,1)，宽 414.69）
                float contentCenterWorldX = 0f;
                if (listContent != null)
                {
                    contentCenterWorldX = listContent.position.x
                        + listContent.rect.width * 0.5f * listContent.lossyScale.x;
                }
                float rowW = rowTemplateRT != null ? rowTemplateRT.rect.width : 423.88f;
                // 行0 顶部 = 表头（label_1 文本底部）下方 30px（下移，避免压住上面提示区），
                // 转 Viewport 局部坐标（anchor 顶部居中）
                float row0TopLocal = -6f; // 兜底：紧贴视口顶部
                if (l1BottomWorldY > -900f)
                {
                    Vector3 headerLocal = winRoot.InverseTransformPoint(
                        new Vector3(0f, l1BottomWorldY, 0f));
                    row0TopLocal = headerLocal.y - 30f; // 表头下方 30px（往下移）
                }
                for (int i = 0; i < ItemCount; i++)
                {
                    GameObject row = Instantiate(rowTemplate.gameObject);
                    row.name = "allrecruit_row_" + i;
                    row.transform.SetParent(winRoot, false);
                    RectTransform rrt = row.transform as RectTransform;
                    rrt.anchorMin = new Vector2(0.5f, 1f);
                    rrt.anchorMax = new Vector2(0.5f, 1f);
                    rrt.pivot = new Vector2(0.5f, 1f);
                    rrt.sizeDelta = new Vector2(rowW, 50f);
                    // 行 i 顶部：再往下 i 行（60 = 行距，行总高 5×60=300）
                    rrt.anchoredPosition = new Vector2(-60f, row0TopLocal - i * 60f); // X 左移 60（用户校准中）
                    row.SetActive(true);
                    rows.Add(row);
                }
                if (rows.Count > 0 && rows[0] != null)
                {
                    RectTransform r0 = rows[0].transform as RectTransform;
                    Vector3[] rc = new Vector3[4];
                    r0.GetWorldCorners(rc);
                    Debug.Log("[AllRecruit v13] 列表定位: 行0世界BL=" + rc[0] + " TL=" + rc[1]
                        + " row0TopLocal=" + row0TopLocal
                        + " 挂载scale=" + winRoot.lossyScale);
                }
            }

            // 4. 滚动条：slider_v —— 深度查找用户原有 Sliding Area/Handle（保留原样式），
            //    只补 Scrollbar 组件（若无）并绑定 onValueChanged 驱动 startIndex。
            if (scrollbar == null)
            {
                RectTransform sbRT = FindNamedRect("slider_v");
                if (sbRT != null)
                {
                    // slider_v 是 Common/slider_v.prefab 实例：Scrollbar 组件在 "Scrollbar" 子容器上
                    // （容器带 Image + raycastTarget，能接收点击/拖动事件）。
                    // 直接复用原组件（原 prefab 已配置 HandleRect/TargetGraphic），只重绑 onValueChanged。
                    RectTransform sbBody = FindInChildren(sbRT, "Scrollbar");
                    if (sbBody == null)
                        sbBody = sbRT;
                    scrollbar = sbBody.GetComponent<Scrollbar>();
                    if (scrollbar == null)
                        scrollbar = sbBody.gameObject.AddComponent<Scrollbar>();
                    RectTransform slidingArea = FindInChildren(sbBody, "Sliding Area");
                    RectTransform handleRT = slidingArea != null ? FindInChildren(slidingArea, "Handle") : null;
                    if (handleRT == null)
                        handleRT = FindInChildren(sbBody, "Handle");
                    if (handleRT == null || handleRT.gameObject == null)
                        handleRT = CreateScrollbarHandle(sbBody); // 仅当用户真的没有 Handle 时才兜底
                    scrollbar.handleRect = handleRT;
                    scrollbar.direction = Scrollbar.Direction.BottomToTop;
                    Image hImg = handleRT != null ? handleRT.GetComponent<Image>() : null;
                    if (hImg != null)
                    {
                        hImg.raycastTarget = true;
                        scrollbar.targetGraphic = hImg;
                    }
                    // 滑道（Scrollbar 容器 Image）参与射线：点击滑道空白处也可跳转
                    Image trackImg = sbBody.GetComponent<Image>();
                    if (trackImg != null)
                        trackImg.raycastTarget = true;
                    scrollbar.onValueChanged.RemoveAllListeners();
                    scrollbar.onValueChanged.AddListener(OnScrollbarChanged);
                    // up（顶部方向）/down（底部方向）按钮：小幅滚动
                    Transform upT = FindInChildren(sbRT, "up");
                    Button ub = upT != null ? upT.GetComponent<Button>() : null;
                    if (ub != null)
                    {
                        ub.onClick.RemoveAllListeners();
                        ub.onClick.AddListener(() => { if (scrollbar != null) scrollbar.value = Mathf.Clamp01(scrollbar.value + 0.1f); });
                    }
                    Transform downT = FindInChildren(sbRT, "down");
                    Button db = downT != null ? downT.GetComponent<Button>() : null;
                    if (db != null)
                    {
                        db.onClick.RemoveAllListeners();
                        db.onClick.AddListener(() => { if (scrollbar != null) scrollbar.value = Mathf.Clamp01(scrollbar.value - 0.1f); });
                    }
                }
            }

            // 5. 行动力值：action_value 实例上的 UITextField
            if (actionValue == null)
            {
                RectTransform avRT = FindNamedRect("action_value");
                if (avRT != null)
                    actionValue = avRT.GetComponent<UITextField>();
            }

            // 6. 窗口标题：win_frame 内 title 文本
            if (winFrameRT == null)
                winFrameRT = FindNamedRect("win_frame");
            if (titleText == null && winFrameRT != null)
            {
                Transform titleNode = winFrameRT.Find("title");
                Text t = titleNode != null
                    ? titleNode.GetComponentInChildren<Text>(true)
                    : winFrameRT.GetComponentInChildren<Text>(true);
                if (t != null)
                    titleText = t;
            }
            if (titleText == null)
                titleText = windiwTitle;

            // 7. 计数文本：root 内 label_1（已选择 N 名武将 / 单次登庸消耗 X 行动力）
            if (countLabel == null)
            {
                RectTransform l1RT = FindNamedRect("label_1");
                if (l1RT != null)
                {
                    Text l1 = l1RT.GetComponentInChildren<Text>(true);
                    if (l1 != null)
                        countLabel = l1;
                }
            }

            // 8. 执行按钮（决定）/ 返回按钮（取消）：不改文字，只绑定
            if (sureButton == null)
            {
                sureButton = FindButtonByText("决定", "确定");
                if (sureButton != null)
                {
                    sureButton.onClick.RemoveAllListeners();
                    sureButton.onClick.AddListener(OnSure);
                }
            }
            if (backButton == null)
            {
                backButton = FindButtonByText("返回", "取消");
                if (backButton != null)
                {
                    backButton.onClick.RemoveAllListeners();
                    backButton.onClick.AddListener(OnCancel);
                }
            }

            // 9. 选择目标入口：文字含"对象/目标"的按钮
            if (selectTargetButton == null)
            {
                Button[] allBtns = GetComponentsInChildren<Button>(true);
                for (int i = 0; i < allBtns.Length; i++)
                {
                    if (allBtns[i] == null || allBtns[i] == sureButton || allBtns[i] == backButton)
                        continue;
                    Text label = allBtns[i].GetComponentInChildren<Text>(true);
                    if (label != null && (label.text.Contains("对象") || label.text.Contains("目标")))
                    {
                        selectTargetButton = allBtns[i];
                        break;
                    }
                }
            }
            if (selectTargetButton != null)
            {
                selectTargetButton.onClick.RemoveAllListeners();
                selectTargetButton.onClick.AddListener(OnSelectTargetPerson);
                selectTargetButton.gameObject.SetActive(true);
            }

            // 10. 按钮可点 + 提层（各自父内最上层）；滚动条 Handle 可拖。
            //     注意：不碰 Viewport / label_1 的位置与层级（保留用户 prefab 原始布局）。
            Button[] btns = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null)
                    continue;
                Graphic g = btns[i].GetComponent<Graphic>();
                if (g != null)
                    g.raycastTarget = true;
                RectTransform brt = btns[i].transform as RectTransform;
                if (brt != null)
                    brt.SetAsLastSibling();
            }
            Scrollbar[] sbs = GetComponentsInChildren<Scrollbar>(true);
            for (int i = 0; i < sbs.Length; i++)
            {
                if (sbs[i] == null || sbs[i].handleRect == null)
                    continue;
                Graphic hg = sbs[i].handleRect.GetComponent<Graphic>();
                if (hg != null)
                    hg.raycastTarget = true;
            }

            Debug.Log("[AllRecruit v13] InitChrome done. sureButton=" + (sureButton != null)
                + " backButton=" + (backButton != null) + " rows=" + rows.Count);
        }

        /// <summary>按文字查找 prefab 中的按钮</summary>
        Button FindButtonByText(params string[] keywords)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null)
                    continue;
                Text label = buttons[i].GetComponentInChildren<Text>(true);
                if (label == null)
                    continue;
                for (int k = 0; k < keywords.Length; k++)
                {
                    if (label.text.Contains(keywords[k]))
                        return buttons[i];
                }
            }
            return null;
        }

        /// <summary>按名字在窗口内查找 RectTransform（含隐藏节点）</summary>
        RectTransform FindNamedRect(string name)
        {
            RectTransform[] all = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == name)
                    return all[i];
            }
            return null;
        }

        /// <summary>深度查找子节点（slider_v 的 Sliding Area 在 Scrollbar 容器下，直接 Find 找不到）</summary>
        RectTransform FindInChildren(Transform root, string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child as RectTransform;
                RectTransform sub = FindInChildren(child, name);
                if (sub != null)
                    return sub;
            }
            return null;
        }

        /// <summary>在 scrollbar 下补一个铺满的 Handle（仅当用户真的没有 Handle 时兜底）</summary>
        RectTransform CreateScrollbarHandle(RectTransform sbRT)
        {
            GameObject hGo = new GameObject("Handle",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hGo.transform.SetParent(sbRT, false);
            RectTransform hrt = hGo.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            Image hImg = hGo.GetComponent<Image>();
            hImg.color = new Color(1f, 1f, 1f, 0.6f);
            hImg.raycastTarget = true;
            return hrt;
        }

        /// <summary>刷新整个界面：标题、计数、行动力、对应列表、滚动条、按钮可用性</summary>
        public void UpdateContent()
        {
            if (currentSystem == null)
                return;

            if (titleText != null)
                titleText.text = currentSystem.customTitleName;

            if (countLabel != null)
                // 三行格式（与 prefab label_1 默认一致）：已选择 / 单次消耗 / 表头。
                // ★ 必须保留第三行表头"目标武将 ← 军师推荐执行武将"：
                //   ① 它是列表的表头；② 行0 顶部以它（文本底）为基准，缺了这行行0 会盖住提示区。
                countLabel.text = $"已选择 {currentSystem.target.Count} 名武将\n单次登庸消耗{currentSystem.GetJobAP()}行动力\n目标武将 ← 军师推荐执行武将";

            // 总消耗行动力 = 单次 × 有执行武将的目标数
            if (actionValue != null)
                actionValue.text = $"{currentSystem.GetTotalAP()}/{TargetCity.BelongCorps.ActionPoint}";

            RefreshRows();
            // 强制立即重排：确保行按序排列、Content 高度正确（防"行叠在一起/行被挤出视口"）
            if (listContent != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (sureButton != null)
                sureButton.interactable = currentSystem.CanExecute();
        }

        /// <summary>
        /// 列表刷新（原版 UIObjectDisplay 模式）：
        /// 行数固定（ItemCount），滚动条滑块大小 = 可见行数/数据数，
        /// startIndex 决定每行显示哪条数据，Content 不移动。
        /// </summary>
        void RefreshRows()
        {
            int dataCount = currentSystem != null ? currentSystem.target.Count : 0;
            int maxStart = Mathf.Max(0, dataCount - ItemCount);
            startIndex = Mathf.Clamp(startIndex, 0, maxStart);

            // 滚动条：数据少时不显示（学 UIObjectDisplay）。
            // 注意：只隐藏 slider_v 自身，绝不 SetActive 它的父节点（父可能是 win_frame/root，
            // 隐藏父会把整个窗口一起藏掉 → 历史“窗口消失”根因）。
            // 滚动条始终显示（用户要求"滚动条谁让你删了"：之前数据≤行数时隐藏导致消失）。
            // 数据超过可见行数时可拖动；数据少（≤可见行数）时灰显（没有可滚空间）。
            if (scrollbar != null)
            {
                scrollbar.gameObject.SetActive(true);
                float ratio = dataCount > 0 ? (float)ItemCount / (float)dataCount : 1f;
                scrollbar.size = Mathf.Clamp(ratio, 0.05f, 1f);
                // ★ 手柄默认在顶部：按 direction 反转。
                //   TopToBottom：value=0=顶部；BottomToTop：value=0=底部 → 需反转成 1。
                //   否则 startIndex=0（列表开头）时手柄却停在底部（用户反馈"默认不在上"）。
                float vNorm = maxStart > 0 ? (float)startIndex / (float)maxStart : 0f;
                if (scrollbar.direction == Scrollbar.Direction.BottomToTop)
                    vNorm = 1f - vNorm;
                scrollbar.SetValueWithoutNotify(vNorm);
                // 始终可拖（用户"滚动条不能用"的观感来自灰显/不可交互）：
                // 数据少时滑块可拖动但列表无滚动空间；数据超过 6 行后拖动真正滚动。
                scrollbar.interactable = true;
            }

            UpdateItemStartIndex();
        }

        /// <summary>按 startIndex 填充固定行（超出数据的行隐藏；空数据时首行显示占位提示）</summary>
        void UpdateItemStartIndex()
        {
            int dataCount = currentSystem != null ? currentSystem.target.Count : 0;
            if (dataCount <= 0)
            {
                // 没有推荐：第一行显示"暂无推荐登庸武将"，其余行隐藏
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i] == null)
                        continue;
                    if (i == 0)
                    {
                        rows[i].SetActive(true);
                        ShowEmptyRow(rows[i]);
                    }
                    else
                    {
                        rows[i].SetActive(false);
                    }
                }
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                GameObject row = rows[i];
                if (row == null)
                    continue;
                int dataIndex = startIndex + i;
                if (dataIndex < dataCount)
                {
                    row.SetActive(true);
                    Person dest = currentSystem.target[dataIndex];
                    Person action = dataIndex < currentSystem.personList.Count
                        ? currentSystem.personList[dataIndex] : null;
                    FillRow(row, dest, action, dataIndex);
                }
                else
                {
                    row.SetActive(false);
                }
            }
        }

        /// <summary>空推荐占位：清空行内容，首格显示"暂无推荐登庸武将"</summary>
        void ShowEmptyRow(GameObject row)
        {
            UIPersonItem[] items = row.GetComponentsInChildren<UIPersonItem>(true);
            if (items.Length > 0)
            {
                items[0].SetPerson(null);
                if (items[0].name != null)
                {
                    items[0].name.text = "暂无推荐登庸武将";
                    items[0].name.color = new Color(0.72f, 0.72f, 0.72f, 1f);
                }
            }
            if (items.Length > 1)
            {
                items[1].SetPerson(null);
                if (items[1].name != null)
                    items[1].name.text = "";
            }
            UITextField[] fields = row.GetComponentsInChildren<UITextField>(true);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] == null || fields[i].label == null)
                    continue;
                Text v = FindValueText(fields[i]);
                if (v != null)
                    v.text = "";
                else
                    fields[i].text = fields[i].label.text;
            }
        }

        /// <summary>填充一行：目标武将(person_1) + 执行武将(person_2) + 属性(textField_9~12)
        /// 目标名字前显示登庸概率（xx%），让玩家对成功率一目了然</summary>
        void FillRow(GameObject row, Person dest, Person action, int dataIndex)
        {
            int prob = currentSystem != null && dataIndex >= 0
                && dataIndex < currentSystem.recruitProbabilities.Count
                ? currentSystem.recruitProbabilities[dataIndex] : 0;
            // 目标/执行武将条目（person_1 上的 UIPersonItem，[0]=目标 [1]=执行）
            UIPersonItem[] items = row.GetComponentsInChildren<UIPersonItem>(true);
            if (items.Length > 0)
            {
                if (dest != null)
                {
                    items[0].SetPerson(dest);
                    if (currentSystem != null && currentSystem.IsTargetDispatched(dest)
                        && items[0].name != null)
                    {
                        items[0].name.text = dest.Name + "（登庸中） " + prob + "%";
                        items[0].name.color = new Color(1f, 0.8f, 0.25f, 1f);
                    }
                    else if (items[0].name != null)
                    {
                        items[0].name.text = dest.Name + "  " + prob + "%";
                        items[0].name.color = Color.white;
                    }
                }
                else
                {
                    items[0].SetPerson(null);
                }
            }

            if (items.Length > 1)
            {
                if (action != null)
                {
                    items[1].SetPerson(action);
                }
                else
                {
                    items[1].SetPerson(null);
                    if (items[1].name != null)
                    {
                        items[1].name.text = "无可用执行武将";
                        items[1].name.color = new Color(1f, 0.42f, 0.42f, 1f);
                    }
                }
            }

            // 行内 textField_5×4：按标签文本匹配填值（势力/忠诚/抵达日数/据点）。
            // 标签是用户 prefab 固定的展示文本，读到哪个标签就填哪个值——
            // 不依赖 GetComponentsInChildren 的字段顺序，杜绝"值写错位"。
            if (dest != null)
            {
                UITextField[] fields = row.GetComponentsInChildren<UITextField>(true);
                City destCity = dest.BelongCity ?? dest.CurrentCity;
                int days = destCity != null && TargetCity != null ? destCity.Distance(TargetCity) * 10 : 0;
                for (int i = 0; i < fields.Length; i++)
                {
                    UITextField f = fields[i];
                    if (f == null || f.label == null)
                        continue;
                    string label = f.label.text;
                    if (string.IsNullOrEmpty(label))
                        continue;
                    if (label.Contains("势力"))
                        SetFieldValue(f, dest.BelongForce != null ? dest.BelongForce.Name : "--");
                    else if (label.Contains("忠诚"))
                        SetFieldValue(f, dest.loyalty.ToString());
                    else if (label.Contains("抵达") || label.Contains("日数"))
                        SetFieldValue(f, days + "日");
                    else if (label.Contains("据点"))
                        SetFieldValue(f, destCity != null ? destCity.Name : "--");
                }
            }
        }

        /// <summary>
        /// 把值写到 UITextField 标签后面的那个 Text。
        /// textField_5 结构：根 > [label(值位 Text)] + [title(UITextField) > label(标签位 Text = f.label)]。
        /// 即：f.transform 是 title，f.transform.parent 是 textField 根，根的"label"直接子节点 = 值位。
        /// </summary>
        void SetFieldValue(UITextField f, string value)
        {
            if (f == null)
                return;
            Text valueText = FindValueText(f);
            if (valueText == null)
            {
                // 保底：值位 Text 不存在时，把值追加到标签后（不覆盖标签，避免标签被破坏后下次匹配失败）
                f.text = f.text + " " + value;
                return;
            }
            valueText.text = value;
            // 值位布局修正：textField_5 的值位 label 高度只有 5.5（显示不下 20 号字，
            // 导致"0""平"等异常残影）。拉成 textField 内居中、垂直撑满，值完整显示。
            RectTransform vrt = valueText.rectTransform;
            if (vrt != null)
            {
                vrt.anchorMin = new Vector2(0.5f, 0.5f);
                vrt.anchorMax = new Vector2(0.5f, 0.5f);
                RectTransform rootRT = f.transform.parent as RectTransform;
                float w = rootRT != null ? rootRT.rect.width : 60f;
                vrt.sizeDelta = new Vector2(Mathf.Max(w, 30f), 22f);
                vrt.anchoredPosition = new Vector2(0f, 0f);
                valueText.alignment = TextAnchor.MiddleCenter;
                valueText.fontSize = Mathf.Max(valueText.fontSize, 18);
                valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
                valueText.verticalOverflow = VerticalWrapMode.Overflow;
            }
        }

        /// <summary>
        /// 找 textField 内的"值位"Text：根下除标签位(f.label / f.titleLabel)以外的第一个 Text。
        /// textField_5 结构：根 &gt; label(值位 Text) + title(UITextField, label=标签位)。
        /// </summary>
        Text FindValueText(UITextField f)
        {
            if (f == null)
                return null;
            Transform root = f.transform.parent;
            if (root == null)
                return null;
            Text[] all = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != f.label && all[i] != f.titleLabel)
                    return all[i];
            }
            return null;
        }

        /// <summary>滚动条值变化 → 只驱动 startIndex（原版 UIObjectDisplay 模式，Content 不移动）</summary>
        void OnScrollbarChanged(float v)
        {
            int dataCount = currentSystem != null ? currentSystem.target.Count : 0;
            int maxStart = Mathf.Max(0, dataCount - ItemCount);
            // BottomToTop：value=1 → Handle 在顶部 → 列表顶部(startIndex=0)；value=0 → 底部
            float vTop = scrollbar != null && scrollbar.direction == Scrollbar.Direction.TopToBottom
                ? v : 1f - v;
            startIndex = (int)Mathf.Lerp(0, maxStart, vTop);
            startIndex = Mathf.Clamp(startIndex, 0, maxStart);
            UpdateItemStartIndex();
        }

        /// <summary>滚轮滚动（学 UIObjectDisplay.Update）</summary>
        void Update()
        {
            if (scrollbar == null || currentSystem == null)
                return;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f)
                return;
            int dataCount = currentSystem.target.Count;
            int maxStart = Mathf.Max(0, dataCount - ItemCount);
            if (maxStart <= 0)
                return;
            float step = 1f / (float)maxStart;
            scrollbar.value = Mathf.Clamp01(scrollbar.value - wheel * step);
        }

        /// <summary>多选目标武将。上限 = min(可用执行武将数, 当前行动力可负担数量)。</summary>
        public void OnSelectTargetPerson()
        {
            Debug.Log("[AllRecruit v13] 点击对象武将");
            try
            {
                if (currentSystem == null)
                {
                    Debug.LogError("[AllRecruit] currentSystem 为空!");
                    return;
                }
                int ap = currentSystem.GetJobAP();
                if (ap <= 0)
                    ap = 1;
                int maxByAP = TargetCity.BelongCorps.ActionPoint / ap;
                int limit = Mathf.Min(currentSystem.TargetCity.freePersons.Count, maxByAP);
                if (limit < 1)
                    limit = 1;

                GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.targetList,
                    currentSystem.target, limit,
                    OnTargetPersonChange, currentSystem.customTargetTitleList, currentSystem.customTargetTitleName, -1);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AllRecruit] OnSelectTargetPerson 异常: " + ex);
            }
        }

        /// <summary>原"选择执行武将"入口：一键登庸自动推荐，无需手动选择。</summary>
        public void OnSelectActionPerson()
        {
        }

        /// <summary>目标选择变化后重新匹配执行武将并刷新界面</summary>
        public virtual void OnTargetPersonChange(List<Person> people)
        {
            try
            {
                selectionReturning = true; // 刚从选择界面返回：下一次 OnOpen 绝不清空刚选好的结果
                currentSystem.SetTargets(people);

                // 兜底：选择窗口关闭后，若本窗口不在可见状态，显式重新打开
                Window.WindowInterface win = Window.Instance.GetWindow(currentSystem.windowName);
                if (win != null && !win.IsVisible())
                    win.Open();

                startIndex = 0;
                UpdateContent();

                // 选择后诊断：确认行是否激活、Content 高度是否被 VLG 撑起
                Debug.Log("[AllRecruit v13] 选择后诊断: data=" + (currentSystem != null ? currentSystem.target.Count : 0)
                    + " contentH=" + (listContent != null ? listContent.rect.height : 0f)
                    + " row0active=" + (rows.Count > 0 && rows[0] != null ? rows[0].activeSelf : false)
                    + " row0pos=" + (rows.Count > 0 && rows[0] != null
                        ? (rows[0].transform as RectTransform).anchoredPosition.ToString() : "none"));

                // 关键：无论 win.Open() 是否触发 OnOpen，选择返回后本窗口的
                // "保护期"到此结束。否则 selectionReturning 残留为 true，
                // 下一次"退出后重开"（挂起恢复）就不会清空旧 target → 武将残留。
                selectionReturning = false;
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AllRecruit] OnTargetPersonChange: " + ex);
            }
        }

        public void OnSure()
        {
            Debug.Log("[AllRecruit v13] 点击决定(一键登庸)");
            try
            {
                selectionReturning = false; // 执行结束：下次打开视为新会话
                if (currentSystem != null)
                    currentSystem.DoJob();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AllRecruit] OnSure 异常: " + ex);
            }
        }

        public void OnCancel()
        {
            Debug.Log("[AllRecruit v13] 点击返回");
            try
            {
                selectionReturning = false; // 返回退出：下次打开视为新会话
                if (currentSystem != null)
                {
                    currentSystem.ClearSelection();
                    currentSystem.Exit();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[AllRecruit] OnCancel 异常: " + ex);
            }
        }
    }
}
