using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Sango.UI;

namespace Sango.EditorTools
{
    /// <summary>
    /// 一次性编辑器工具：生成「授予宝物 / 没收宝物」窗口预制体
    /// <c>Assets/Mods/Content/Assets/UI/Prefab/window_item_grant.prefab</c>。
    ///
    /// 【为什么用脚本生成而不是手拖】
    ///   本窗口是"三栏 + 底部条"的固定结构（左珍品列表 / 中详情 / 右目标武将），
    ///   节点多且互相引用（UICityGrantItem 有 20 个引用字段），手拖极易漏绑。
    ///   用脚本生成可以一次成型、可重复执行，后续要加字段也能直接重跑。
    ///
    /// 【复用现有美术资源，不新造图】
    ///   · 窗框 / 标题 / 确定取消按钮：直接实例化 <c>Common/win_frame.prefab</c>
    ///   · 珍品格：直接实例化 <c>Common/itemTypeItem.prefab</c>（UIBuildingTypeItem，带选中态）
    ///   · 目标武将卡（含"选择武将"按钮）：直接实例化 <c>Common/personStatus_selectBtn.prefab</c>
    ///   · 背景贴图：AtlasTexture/4846-2_14、4846-4_8（与其它窗口同套图集）
    ///
    /// 【生成后自检】
    ///   生成完会逐个断言关键字段已绑定，并输出一根节点清单到控制台，
    ///   方便在 Unity 里直接核对（见 <see cref="Dump"/>）。
    /// </summary>
    public static class UIGrantWindowBuilder
    {
        const string PrefabDir = "Assets/Mods/Content/Assets/UI/Prefab/";
        const string WindowPrefabPath = PrefabDir + "window_item_grant.prefab";
        const string WinFramePath = PrefabDir + "Common/win_frame.prefab";
        const string ItemCellPath = PrefabDir + "Common/itemTypeItem.prefab";
        const string PersonCardPath = PrefabDir + "Common/personStatus_selectBtn.prefab";
        const string FontPath = "Assets/Resources/kaiti.ttf";
        const string AtlasDir = "Assets/Mods/Content/Assets/UI/AtlasTexture/";

        /// <summary>整窗尺寸（与列宽加总一致：300 + 12 + 300 + 12 + 424 = 1048，两侧各留 26 边距）</summary>
        static readonly Vector2 PanelSize = new Vector2(1100f, 640f);
        /// <summary>内容区四边内缩：左/右 26、上 92（让开标题）、下 80（让开确定取消按钮）</summary>
        static readonly Vector4 ContentPadding = new Vector4(26f, 80f, 26f, 92f);

        static Font uiFont;
        // 注意：Sango 下存在 Sprite 命名空间，这里必须写全 UnityEngine.Sprite
        static UnityEngine.Sprite bgSprite;
        static UnityEngine.Sprite labelSprite;

        [MenuItem("SangoTools/构建授予宝物窗口")]
        public static void Build()
        {
            uiFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            bgSprite = LoadSprite("4846-2_14");
            labelSprite = LoadSprite("4846-4_8");

            GameObject frameSrc = AssetDatabase.LoadAssetAtPath<GameObject>(WinFramePath);
            GameObject cellSrc = AssetDatabase.LoadAssetAtPath<GameObject>(ItemCellPath);
            GameObject personSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PersonCardPath);
            if (frameSrc == null || cellSrc == null || personSrc == null)
            {
                Debug.LogError($"[授予窗口]缺少基础预制体：frame={frameSrc != null}, cell={cellSrc != null}, person={personSrc != null}");
                return;
            }

            // ============ 根：Canvas + 射线 + 窗口脚本 ============
            GameObject root = new GameObject("window_item_grant", typeof(RectTransform));
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
            UICityGrantItem window = root.AddComponent<UICityGrantItem>();

            // 全屏遮罩：挡住背后操作，点击不关闭（由取消按钮关闭）
            GameObject mask = NewUI("mask", root.transform);
            Stretch(mask.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image maskImage = mask.AddComponent<Image>();
            maskImage.color = new Color(0f, 0f, 0f, 0.55f);
            maskImage.raycastTarget = true;

            // ============ 面板（win_frame 提供窗框 / 标题 / 按钮） ============
            GameObject panel = NewUI("panel", root.transform);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = PanelSize;

            GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(frameSrc, panel.transform);
            frame.name = "win_frame";
            RectTransform frameRect = frame.GetComponent<RectTransform>();
            Stretch(frameRect, 0f, 0f, 0f, 0f);

            Text title = frame.transform.Find("title") != null ? frame.transform.Find("title").GetComponent<Text>() : null;
            Button okButton = frame.transform.Find("button_ok") != null ? frame.transform.Find("button_ok").GetComponent<Button>() : null;
            Button cancelButton = frame.transform.Find("button_cancel") != null ? frame.transform.Find("button_cancel").GetComponent<Button>() : null;

            // ============ 内容区 ============
            GameObject content = NewUI("content", panel.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            Stretch(contentRect, ContentPadding.x, ContentPadding.y, ContentPadding.z, ContentPadding.w);

            float colHeight = PanelSize.y - ContentPadding.y - ContentPadding.w;

            // ---------- 左：珍品列表 ----------
            GameObject leftCol = NewUI("itemArea", content.transform);
            RectTransform leftRect = TopLeft(leftCol, 0f, 0f, 300f, colHeight);
            AddPanelBg(leftCol, null);

            Text leftTitle = AddText(leftCol, "list_title", "珍品", 24, TextAnchor.MiddleLeft, Color.white);
            TopLeft(leftTitle.gameObject, 12f, 6f, 276f, 28f);

            GameObject scroll = NewUI("itemScroll", leftCol.transform);
            RectTransform scrollRect = scroll.GetComponent<RectTransform>();
            Stretch(scrollRect, 8f, 8f, 8f, 42f);
            ScrollRect scrollRectComp = scroll.AddComponent<ScrollRect>();
            scrollRectComp.horizontal = false;
            scrollRectComp.vertical = true;
            scrollRectComp.movementType = ScrollRect.MovementType.Clamped;
            scrollRectComp.scrollSensitivity = 30f;

            GameObject viewport = NewUI("Viewport", scroll.transform);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect, 0f, 0f, 0f, 0f);
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject listContent = NewUI("Content", viewport.transform);
            RectTransform listContentRect = listContent.GetComponent<RectTransform>();
            listContentRect.anchorMin = new Vector2(0f, 1f);
            listContentRect.anchorMax = new Vector2(1f, 1f);
            listContentRect.pivot = new Vector2(0.5f, 1f);
            listContentRect.anchoredPosition = Vector2.zero;
            listContentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup listLayout = listContent.AddComponent<VerticalLayoutGroup>();
            listLayout.padding = new RectOffset(2, 2, 2, 2);
            listLayout.spacing = 6f;
            listLayout.childAlignment = TextAnchor.UpperCenter;
            listLayout.childControlWidth = false;
            listLayout.childControlHeight = false;
            listLayout.childForceExpandWidth = false;
            listLayout.childForceExpandHeight = false;
            ContentSizeFitter listFitter = listContent.AddComponent<ContentSizeFitter>();
            listFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRectComp.viewport = viewportRect;
            scrollRectComp.content = listContentRect;

            // 珍品格模板：直接用 Common/itemTypeItem.prefab，游戏里由 CreatePool 克隆
            GameObject cell = (GameObject)PrefabUtility.InstantiatePrefab(cellSrc, listContent.transform);
            cell.name = "itemCell";
            cell.SetActive(false);
            UIBuildingTypeItem cellItem = cell.GetComponent<UIBuildingTypeItem>();

            Text emptyLabel = AddText(leftCol, "emptyLabel", "暂无可操作的珍品", 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            Stretch(emptyLabel.GetComponent<RectTransform>(), 12f, 12f, 12f, 12f);

            // ---------- 中：珍品详情 ----------
            GameObject centerCol = NewUI("detailArea", content.transform);
            TopLeft(centerCol, 312f, 0f, 300f, colHeight);
            AddPanelBg(centerCol, null);

            GameObject detailIcon = NewUI("detailIcon", centerCol.transform);
            TopLeft(detailIcon, 95f, 12f, 110f, 110f);
            RawImage detailIconImage = detailIcon.AddComponent<RawImage>();
            detailIconImage.color = Color.white;
            detailIconImage.raycastTarget = false;

            Text detailName = AddText(centerCol, "detailName", "", 26, TextAnchor.MiddleCenter, Color.white);
            TopLeft(detailName.gameObject, 10f, 130f, 280f, 34f);

            Text detailGrade = AddText(centerCol, "detailGrade", "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f));
            TopLeft(detailGrade.gameObject, 10f, 166f, 280f, 28f);

            Text detailBonus = AddText(centerCol, "detailBonus", "", 22, TextAnchor.UpperCenter, new Color(0.7f, 0.95f, 0.75f));
            TopLeft(detailBonus.gameObject, 10f, 198f, 280f, 66f);

            Text detailFeature = AddText(centerCol, "detailFeature", "", 22, TextAnchor.UpperCenter, new Color(0.75f, 0.85f, 1f));
            TopLeft(detailFeature.gameObject, 10f, 266f, 280f, 54f);

            Text detailDesc = AddText(centerCol, "detailDesc", "", 20, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
            TopLeft(detailDesc.gameObject, 10f, 322f, 280f, 86f);

            Text loyaltyChange = AddText(centerCol, "loyaltyChange", "", 24, TextAnchor.MiddleCenter, Color.white);
            TopLeft(loyaltyChange.gameObject, 10f, 412f, 280f, 42f);

            // ---------- 右：目标武将 ----------
            GameObject rightCol = NewUI("personArea", content.transform);
            TopLeft(rightCol, 624f, 0f, 424f, colHeight);
            AddPanelBg(rightCol, null);

            Text rightTitle = AddText(rightCol, "person_title", "目标武将", 24, TextAnchor.MiddleLeft, Color.white);
            TopLeft(rightTitle.gameObject, 12f, 6f, 400f, 28f);

            // 武将卡：复用 Common/personStatus_selectBtn.prefab（含人物像、五维、"选择武将"按钮）
            GameObject personCard = (GameObject)PrefabUtility.InstantiatePrefab(personSrc, rightCol.transform);
            personCard.name = "personCard";
            TopLeft(personCard, 0f, 40f, 424f, 196f);
            UIPersonItem personItem = personCard.transform.Find("person_1") != null
                ? personCard.transform.Find("person_1").GetComponent<UIPersonItem>() : null;
            Button selectButton = personCard.transform.Find("item_select_person_btn") != null
                ? personCard.transform.Find("item_select_person_btn").GetComponent<Button>() : null;

            Text personLoyalty = AddText(rightCol, "personLoyalty", "", 22, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.6f));
            TopLeft(personLoyalty.gameObject, 12f, 244f, 400f, 26f);

            Text personEquip = AddText(rightCol, "personEquip", "", 20, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.85f));
            TopLeft(personEquip.gameObject, 12f, 274f, 400f, 76f);

            Text blockLabel = AddText(rightCol, "blockReason", "", 20, TextAnchor.UpperLeft, new Color(1f, 0.42f, 0.35f));
            TopLeft(blockLabel.gameObject, 12f, 356f, 400f, 48f);

            Text hintLabel = AddText(rightCol, "hint", "", 20, TextAnchor.UpperLeft, new Color(1f, 0.88f, 0.45f));
            TopLeft(hintLabel.gameObject, 12f, 406f, 400f, 54f);

            // ---------- 底部：行动力（贴在右下，避开 win_frame 左下的确定/取消） ----------
            GameObject actionValue = NewUI("action_value", panel.transform);
            RectTransform actionRect = actionValue.GetComponent<RectTransform>();
            actionRect.anchorMin = new Vector2(1f, 0f);
            actionRect.anchorMax = new Vector2(1f, 0f);
            actionRect.pivot = new Vector2(1f, 0.5f);
            actionRect.anchoredPosition = new Vector2(-40f, 50f);
            actionRect.sizeDelta = new Vector2(150f, 26f);
            UITextField actionField = actionValue.AddComponent<UITextField>();
            Text actionLabel = AddText(actionValue, "label", "0/0", 22, TextAnchor.MiddleRight, Color.white);
            Stretch(actionLabel.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            actionField.label = actionLabel;

            Text actionTitle = AddText(panel, "action_title", "行动力", 22, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.8f));
            RectTransform actionTitleRect = TopLeft(actionTitle.gameObject, 0f, 0f, 90f, 26f);
            actionTitleRect.anchorMin = new Vector2(1f, 0f);
            actionTitleRect.anchorMax = new Vector2(1f, 0f);
            actionTitleRect.pivot = new Vector2(1f, 0.5f);
            actionTitleRect.anchoredPosition = new Vector2(-198f, 50f);

            // ============ 绑定窗口字段 ============
            window.windowTitle = title;
            window.itemListTip = leftTitle;
            window.itemCellTemplate = cellItem;
            window.itemScrollRect = scrollRectComp;
            window.emptyItemLabel = emptyLabel;
            window.detailIcon = detailIconImage;
            window.detailName = detailName;
            window.detailGrade = detailGrade;
            window.detailBonus = detailBonus;
            window.detailFeature = detailFeature;
            window.detailDesc = detailDesc;
            window.loyaltyChangeLabel = loyaltyChange;
            window.targetPersonItem = personItem;
            window.targetPersonSelectBtn = selectButton;
            window.targetPersonLoyalty = personLoyalty;
            window.targetPersonEquip = personEquip;
            window.action_value = actionField;
            window.blockReasonLabel = blockLabel;
            window.hintLabel = hintLabel;
            window.sureButton = okButton;
            window.cancelButton = cancelButton;

            if (title != null)
                title.text = "授予宝物";

            // ============ 保存 ============
            System.IO.Directory.CreateDirectory(PrefabDir);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath, out bool success);
            // 注意：Sango 下存在 Object 类型，这里必须写全 UnityEngine.Object
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[授予窗口]生成{(success && saved != null ? "成功" : "失败")}：{WindowPrefabPath}\n{Dump(saved)}");
        }

        /// <summary>生成结果自检：打印关键字段是否绑上，便于在 Unity 控制台核对</summary>
        static string Dump(GameObject prefab)
        {
            if (prefab == null)
                return "预制体为空";

            UICityGrantItem window = prefab.GetComponent<UICityGrantItem>();
            if (window == null)
                return "预制体根节点上没有 UICityGrantItem";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("字段自检：");
            sb.Append("title=").Append(window.windowTitle != null);
            sb.Append(" cellTemplate=").Append(window.itemCellTemplate != null);
            sb.Append(" scroll=").Append(window.itemScrollRect != null);
            sb.Append(" detailIcon=").Append(window.detailIcon != null);
            sb.Append(" detailName=").Append(window.detailName != null);
            sb.Append(" loyalty=").Append(window.loyaltyChangeLabel != null);
            sb.Append(" personItem=").Append(window.targetPersonItem != null);
            sb.Append(" personBtn=").Append(window.targetPersonSelectBtn != null);
            sb.Append(" action=").Append(window.action_value != null);
            sb.Append(" ok=").Append(window.sureButton != null);
            sb.Append(" cancel=").Append(window.cancelButton != null);
            return sb.ToString();
        }

        // ==================== 小工具 ====================

        static UnityEngine.Sprite LoadSprite(string fileName)
        {
            UnityEngine.Sprite sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(AtlasDir + fileName + ".png");
            if (sprite != null)
                return sprite;

            string[] guids = AssetDatabase.FindAssets(fileName + " t:Sprite");
            if (guids != null && guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));

            Debug.LogWarning($"[授予窗口]找不到贴图 {fileName}，将使用纯色块。");
            return null;
        }

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

        /// <summary>左上角对齐摆放（x 向右、y 向下，符合本窗口"从左上往下排"的排版习惯）</summary>
        static RectTransform TopLeft(GameObject go, float x, float y, float width, float height)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        /// <summary>列背景（用图集里的窗口底图，Sliced 拉伸）</summary>
        static void AddPanelBg(GameObject parent, UnityEngine.Sprite sprite)
        {
            GameObject bg = NewUI("bg", parent.transform);
            Stretch(bg.GetComponent<RectTransform>(), 0f, 0f, 0f, 0f);
            Image image = bg.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : bgSprite;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 0.92f);
            image.raycastTarget = false;
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
