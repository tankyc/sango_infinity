using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace SangoTools.Editor
{
    /// <summary>
    /// 编辑器顶部菜单栏控制器
    /// 提供类似Unity编辑器的顶部菜单栏功能
    /// </summary>
    public class EditorTopMenuBar : MonoBehaviour
    {
        [Header("菜单配置")]
        [SerializeField] private List<MenuCategory> menuCategories = new List<MenuCategory>();

        [Header("布局按钮")]
        [SerializeField] private Button[] layoutButtons;
        [SerializeField] private int currentLayoutIndex = 0;

        [Header("右侧工具按钮")]
        [SerializeField] private Button searchButton;
        [SerializeField] private Button userButton;
        [SerializeField] private Button settingsButton;

        [Header("下拉菜单模板")]
        [SerializeField] private GameObject dropdownMenuPrefab;
        [SerializeField] private Transform dropdownContainer;

        [Header("搜索面板")]
        [SerializeField] private GameObject searchPanel;
        [SerializeField] private InputField searchInput;

        // 运行时状态
        private GameObject currentOpenMenu = null;
        private int currentOpenMenuIndex = -1;
        private List<GameObject> activeDropdowns = new List<GameObject>();

        // 事件回调
        public System.Action<string> OnMenuItemClicked;
        public System.Action<int> OnLayoutChanged;
        public System.Action OnSearchClicked;
        public System.Action OnUserClicked;
        public System.Action OnSettingsClicked;

        private void Start()
        {
            InitializeMenuButtons();
            InitializeLayoutButtons();
            InitializeRightTools();
            
            // 监听全局点击事件用于关闭菜单
            EventTriggerListener.Get(gameObject).onPointerClick = OnMenuBarClicked;
        }

        private void InitializeMenuButtons()
        {
            for (int i = 0; i < menuCategories.Count; i++)
            {
                var category = menuCategories[i];
                if (category.menuButton != null)
                {
                    int index = i; // 闭包捕获
                    category.menuButton.onClick.RemoveAllListeners();
                    category.menuButton.onClick.AddListener(() => OnMenuButtonClicked(index));
                }
            }
        }

        private void InitializeLayoutButtons()
        {
            if (layoutButtons != null)
            {
                for (int i = 0; i < layoutButtons.Length; i++)
                {
                    int index = i;
                    layoutButtons[i].onClick.RemoveAllListeners();
                    layoutButtons[i].onClick.AddListener(() => OnLayoutButtonClicked(index));
                }
                UpdateLayoutButtonStates();
            }
        }

        private void InitializeRightTools()
        {
            // 先清掉运行时监听再绑定,保证多次初始化时不会重复叠加
            if (searchButton != null)
            {
                searchButton.onClick.RemoveAllListeners();
                searchButton.onClick.AddListener(() => OnSearchClicked?.Invoke());
            }

            if (userButton != null)
            {
                userButton.onClick.RemoveAllListeners();
                userButton.onClick.AddListener(() => OnUserClicked?.Invoke());
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(() => OnSettingsClicked?.Invoke());
            }
        }

        private void OnMenuButtonClicked(int index)
        {
            if (index < 0 || index >= menuCategories.Count) return;

            // 如果点击的是已打开的菜单，则关闭
            if (currentOpenMenu != null && currentOpenMenuIndex == index)
            {
                CloseCurrentMenu();
                return;
            }

            // 关闭之前的菜单
            CloseCurrentMenu();

            // 打开新菜单
            OpenMenu(index);
        }

        private void OpenMenu(int index)
        {
            var category = menuCategories[index];
            if (category.menuButton == null || dropdownMenuPrefab == null) return;

            // 创建下拉菜单
            GameObject dropdown = Instantiate(dropdownMenuPrefab, dropdownContainer);
            currentOpenMenu = dropdown;
            currentOpenMenuIndex = index;

            // 设置菜单位置
            RectTransform buttonRect = category.menuButton.GetComponent<RectTransform>();
            RectTransform dropdownRect = dropdown.GetComponent<RectTransform>();
            
            Vector3[] corners = new Vector3[4];
            buttonRect.GetWorldCorners(corners);
            dropdownRect.position = new Vector3(corners[0].x, corners[0].y, 0);

            // 初始化菜单内容
            var menuComponent = dropdown.GetComponent<EditorDropdownMenu>();
            if (menuComponent != null)
            {
                menuComponent.Initialize(category.menuItems, OnMenuItemSelected);
            }

            activeDropdowns.Add(dropdown);
        }

        private void CloseCurrentMenu()
        {
            if (currentOpenMenu != null)
            {
                Destroy(currentOpenMenu);
                currentOpenMenu = null;
                currentOpenMenuIndex = -1;
            }
        }

        private void OnMenuItemSelected(MenuItemData item)
        {
            CloseCurrentMenu();
            OnMenuItemClicked?.Invoke(item.actionId);
        }

        private void OnLayoutButtonClicked(int index)
        {
            if (index == currentLayoutIndex) return;
            
            currentLayoutIndex = index;
            UpdateLayoutButtonStates();
            OnLayoutChanged?.Invoke(index);
        }

        private void UpdateLayoutButtonStates()
        {
            if (layoutButtons == null) return;

            for (int i = 0; i < layoutButtons.Length; i++)
            {
                var colors = layoutButtons[i].colors;
                colors.normalColor = (i == currentLayoutIndex) ? 
                    new Color(0.26f, 0.42f, 0.60f) : Color.white;
                layoutButtons[i].colors = colors;
            }
        }

        private void OnMenuBarClicked(PointerEventData eventData)
        {
            // 点击菜单栏空白处时关闭所有菜单
            if (eventData.pointerPress == null || eventData.pointerPress.transform.IsChildOf(transform))
            {
                // 检查是否点击的是菜单按钮本身
                if (currentOpenMenu != null)
                {
                    CloseCurrentMenu();
                }
            }
        }

        // 公开方法供外部调用

        /// <summary>
        /// 通过动作ID查找并触发菜单项
        /// </summary>
        public void TriggerMenuItem(string actionId)
        {
            foreach (var category in menuCategories)
            {
                foreach (var item in category.menuItems)
                {
                    if (item.actionId == actionId)
                    {
                        OnMenuItemClicked?.Invoke(actionId);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// 关闭所有打开的菜单
        /// </summary>
        public void CloseAllMenus()
        {
            CloseCurrentMenu();
        }

        /// <summary>
        /// 设置当前布局
        /// </summary>
        public void SetCurrentLayout(int index)
        {
            if (index >= 0 && index < layoutButtons.Length)
            {
                OnLayoutButtonClicked(index);
            }
        }
    }

    /// <summary>
    /// 菜单类别（对应一个菜单按钮）
    /// </summary>
    [System.Serializable]
    public class MenuCategory
    {
        public string categoryName;
        public Button menuButton;
        public List<MenuItemData> menuItems = new List<MenuItemData>();
    }

    /// <summary>
    /// 菜单项数据
    /// </summary>
    [System.Serializable]
    public class MenuItemData
    {
        public string displayName;
        public string actionId;
        public string shortcut; // 快捷键显示文本
        public bool isSeparator = false;
        public bool isToggle = false;
        public bool isToggleOn = false;
        public Sprite icon; // 可选的图标
    }

    /// <summary>
    /// 下拉菜单组件
    /// </summary>
    public class EditorDropdownMenu : MonoBehaviour
    {
        private List<MenuItemData> menuItems;
        private System.Action<MenuItemData> onItemSelected;
        private VerticalLayoutGroup layoutGroup;
        private ContentSizeFitter contentFitter;

        private void Awake()
        {
            layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.spacing = 2;
            layoutGroup.padding = new RectOffset(4, 4, 4, 4);
            layoutGroup.childAlignment = TextAnchor.MiddleLeft;
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.childForceExpandHeight = false;

            contentFitter = gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 添加背景
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);

            // 添加布局延迟
            StartCoroutine(DelayedLayout());
        }

        private System.Collections.IEnumerator DelayedLayout()
        {
            yield return null;
            LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
        }

        public void Initialize(List<MenuItemData> items, System.Action<MenuItemData> onSelected)
        {
            menuItems = items;
            onItemSelected = onSelected;

            // 清空现有内容
            foreach (Transform child in transform)
            {
                if (child.GetComponent<Image>() != null && child.childCount == 0)
                {
                    Destroy(child.gameObject);
                }
            }

            // 创建菜单项
            foreach (var item in items)
            {
                CreateMenuItem(item);
            }

            StartCoroutine(DelayedLayout());
        }

        private void CreateMenuItem(MenuItemData item)
        {
            if (item.isSeparator)
            {
                CreateSeparator();
                return;
            }

            // 创建菜单项按钮
            GameObject itemObj = new GameObject("MenuItem_" + item.displayName);
            itemObj.transform.SetParent(transform);

            var image = itemObj.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0);

            var button = itemObj.AddComponent<Button>();
            var eventTrigger = itemObj.AddComponent<EventTriggerListener>();
            
            // 悬停效果
            eventTrigger.onPointerEnter = (data) =>
            {
                image.color = new Color(0.23f, 0.42f, 0.60f, 1f);
            };
            eventTrigger.onPointerExit = (data) =>
            {
                image.color = new Color(1, 1, 1, 0);
            };
            eventTrigger.onPointerClick = (data) =>
            {
                onItemSelected?.Invoke(item);
            };

            // 水平布局
            var hLayout = itemObj.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 10;
            hLayout.padding = new RectOffset(8, 8, 4, 4);
            hLayout.childControlWidth = true;
            hLayout.childControlHeight = true;
            hLayout.childAlignment = TextAnchor.MiddleLeft;

            var layoutElement = itemObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 24;
            layoutElement.preferredHeight = 24;

            // 图标
            if (item.icon != null)
            {
                GameObject iconObj = new GameObject("Icon");
                iconObj.transform.SetParent(itemObj.transform);
                var iconImage = iconObj.AddComponent<Image>();
                iconImage.sprite = item.icon;
                iconImage.preserveAspect = true;
                
                var iconLayout = iconObj.AddComponent<LayoutElement>();
                iconLayout.preferredWidth = 16;
                iconLayout.preferredHeight = 16;
            }

            // 文本
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(itemObj.transform);
            var text = textObj.AddComponent<Text>();
            text.text = item.displayName;
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;

            // 快捷键
            if (!string.IsNullOrEmpty(item.shortcut))
            {
                GameObject shortcutObj = new GameObject("Shortcut");
                shortcutObj.transform.SetParent(itemObj.transform);
                var shortcutText = shortcutObj.AddComponent<Text>();
                shortcutText.text = item.shortcut;
                shortcutText.fontSize = 11;
                shortcutText.color = new Color(0.6f, 0.6f, 0.6f);
                shortcutText.alignment = TextAnchor.MiddleRight;

                var shortcutLayout = shortcutObj.AddComponent<LayoutElement>();
                shortcutLayout.preferredWidth = 80;
            }

            // Toggle标记
            if (item.isToggle)
            {
                GameObject toggleObj = new GameObject("Toggle");
                toggleObj.transform.SetParent(itemObj.transform);
                var toggleText = toggleObj.AddComponent<Text>();
                toggleText.text = item.isToggleOn ? "✓" : "";
                toggleText.fontSize = 12;
                toggleText.color = new Color(0.3f, 0.8f, 1f);
                toggleText.alignment = TextAnchor.MiddleCenter;

                var toggleLayout = toggleObj.AddComponent<LayoutElement>();
                toggleLayout.preferredWidth = 20;
            }
        }

        private void CreateSeparator()
        {
            GameObject sepObj = new GameObject("Separator");
            sepObj.transform.SetParent(transform);

            var sepImage = sepObj.AddComponent<Image>();
            sepImage.color = new Color(0.4f, 0.4f, 0.4f);

            var layoutElement = sepObj.AddComponent<LayoutElement>();
            layoutElement.minHeight = 8;
            layoutElement.preferredHeight = 8;
        }
    }

    /// <summary>
    /// 事件触发器监听器（简化版）
    /// </summary>
    public class EventTriggerListener : EventTrigger
    {
        public System.Action<PointerEventData> onPointerClick;
        public System.Action<PointerEventData> onPointerEnter;
        public System.Action<PointerEventData> onPointerExit;

        public static EventTriggerListener Get(GameObject go)
        {
            EventTriggerListener listener = go.GetComponent<EventTriggerListener>();
            if (listener == null)
                listener = go.AddComponent<EventTriggerListener>();
            return listener;
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            onPointerClick?.Invoke(eventData);
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            onPointerEnter?.Invoke(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            onPointerExit?.Invoke(eventData);
        }
    }
}
