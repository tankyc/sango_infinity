using Sango;
using Sango.Core;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 容貌区间性别分类
    /// </summary>
    public enum HeadSexType
    {
        /// <summary>男性容貌</summary>
        Male = 0,
        /// <summary>女性容貌</summary>
        Female = 1,
        /// <summary>自定义容貌（新武将）</summary>
        Custom = 2
    }

    /// <summary>
    /// 容貌ID区间配置。
    /// 每个区间定义一段连续的容貌ID范围及其性别属性，
    /// 支持配置多段区间以覆盖不连续的自定义头像ID段。
    /// </summary>
    [System.Serializable]
    public class HeadIdRange
    {
        /// <summary>区间名称，如"标准男性"、"自定义女性"</summary>
        [Tooltip("区间名称，用于在编辑器中标识")]
        public string name;

        /// <summary>起始ID（包含）</summary>
        [Tooltip("区间起始ID，包含此ID")]
        public int startId;

        /// <summary>结束ID（包含）</summary>
        [Tooltip("区间结束ID，包含此ID")]
        public int endId;

        /// <summary>性别分类</summary>
        [Tooltip("该区间容貌的性别分类")]
        public HeadSexType sexType;
    }

    /// <summary>
    /// 容貌链表节点。
    /// 存储单个容貌ID及其元数据，通过索引互相引用。
    /// </summary>
    [System.Serializable]
    public class HeadItemData
    {
        /// <summary>容貌ID</summary>
        public string headId;

        /// <summary>在全局链表中的索引</summary>
        public int index;

        /// <summary>性别分类</summary>
        public HeadSexType sexType;

        /// <summary>所属区间名称</summary>
        public string rangeName;

        public HeadItemData(string headId, int index, HeadSexType sexType, string rangeName)
        {
            this.headId = headId;
            this.index = index;
            this.sexType = sexType;
            this.rangeName = rangeName;
        }
    }

    /// <summary>
    /// 武将容貌选择窗口。
    /// 参照三国志11容貌选择系统实现，支持按性别筛选、
    /// 分页浏览、大图预览等功能。
    /// 
    /// 数据特征：
    /// - 所有容貌ID合并为链表，以索引操作
    /// - 链表排列顺序为先男后女，女性起点由 <see cref="femaleStartIndex"/> 记录
    /// - 仅加载当前页面容貌，避免内存浪费
    /// </summary>
    public class UIPersonHeadSelect : UGUIWindow
    {
        #region 常量

        /// <summary>每页显示容貌数量（4行 x 7列 = 28个）</summary>
        private const int PAGE_SIZE = 28;

        /// <summary>每行显示容貌数量</summary>
        private const int COL_COUNT = 7;

        /// <summary>容貌资源路径，{0}为ID，{1}为类型</summary>
        private const string HEAD_PATH_FORMAT = "Assets/Face/{0}_{1}";

        /// <summary>容貌资源包名</summary>
        private const string HEAD_PACKAGE_NAME = "Face";

        /// <summary>容貌图片类型（2=头像）</summary>
        private const int HEAD_ICON_TYPE = 2;

        #endregion

        #region 序列化字段 — 配置

        [Header("=== 容貌区间配置 ===")]
        /// <summary>
        /// 容貌ID区间配置列表。
        /// 按配置顺序排列，先男后女。
        /// 默认：
        /// - 标准男性：0~99
        /// - 标准女性：100~168
        /// - 自定义男性：1001~1099
        /// - 自定义女性：1100~1199
        /// </summary>
        [Tooltip("容貌ID区间配置，按顺序排列（先男后女）")]
        public HeadIdRange[] headIdRanges;

        #endregion

        #region 序列化字段 — UI引用

        [Header("=== 容貌格子 ===")]
        /// <summary>当前页容貌格子数组（28个）</summary>
        [Tooltip("每页28个容貌格子RawImage")]
        public RawImage[] headItemIcons;

        /// <summary>容貌格子上的"使用中"标签文本</summary>
        [Tooltip("每个容貌格子的'使用中'标签Text")]
        public Text[] headItemUsedTexts;

        /// <summary>容貌格子按钮</summary>
        [Tooltip("每个容貌格子的Button组件")]
        public Button[] headItemButtons;

        [Header("=== 导航 ===")]
        /// <summary>上一页按钮</summary>
        public Button prevPageButton;

        /// <summary>下一页按钮</summary>
        public Button nextPageButton;

        /// <summary>页码显示文本</summary>
        public Text pageText;

        [Header("=== 筛选 ===")]
        /// <summary>男性筛选Toggle</summary>
        public Toggle maleToggle;

        /// <summary>女性筛选Toggle</summary>
        public Toggle femaleToggle;

        /// <summary>自定义容貌筛选Toggle</summary>
        public Toggle customToggle;

        [Header("=== 预览 ===")]
        /// <summary>大图预览RawImage</summary>
        public RawImage previewImage;

        /// <summary>当前选中ID显示文本</summary>
        public Text selectedIdText;

        [Header("=== 底部按钮 ===")]
        /// <summary>确认按钮</summary>
        public Button confirmButton;

        /// <summary>返回/取消按钮</summary>
        public Button returnButton;

        #endregion

        #region 私有数据

        /// <summary>
        /// 所有容貌数据链表。
        /// 按区间配置顺序生成，先男后女排列。
        /// </summary>
        private List<HeadItemData> headDataList = new List<HeadItemData>();

        /// <summary>
        /// 当前筛选后的容貌数据链表。
        /// 根据性别Toggle筛选后的子集。
        /// </summary>
        private List<HeadItemData> filteredHeadDataList = new List<HeadItemData>();

        /// <summary>
        /// 女性容貌起始索引。
        /// 在完整链表中第一个女性容貌的索引位置。
        /// -1 表示无女性容貌。
        /// </summary>
        private int femaleStartIndex = -1;

        /// <summary>
        /// 当前页码（0起始）
        /// </summary>
        private int currentPage = 0;

        /// <summary>
        /// 当前选中索引（在筛选后链表中的索引）
        /// </summary>
        private int currentSelectedIndex = -1;

        /// <summary>
        /// 已加载的容貌纹理缓存，key为ID，value为Texture。
        /// 仅缓存当前页加载的纹理，翻页时清理上一页纹理。
        /// </summary>
        private Dictionary<string, Texture> loadedTextureCache = new Dictionary<string, Texture>();

        /// <summary>
        /// 当前性别筛选模式。
        /// -1 = 全部，0 = 仅男性，1 = 仅女性，2 = 仅自定义
        /// </summary>
        private int currentSexFilter = -1;

        /// <summary>
        /// 已使用的容貌ID集合（当前部队/势力中已被占用的容貌）
        /// </summary>
        private HashSet<string> usedHeadIdSet = new HashSet<string>();

        #endregion

        #region 回调

        /// <summary>选择容貌回调，参数为选中的容貌ID</summary>
        public Action<string> OnHeadSelected;

        /// <summary>关闭窗口回调</summary>
        public Action OnWindowClosed;

        #endregion

        #region 生命周期

        protected override void Awake()
        {
            base.Awake();

            // 优先从JSON加载容貌区间配置，若失败则使用默认配置
            if (headIdRanges == null || headIdRanges.Length == 0)
            {
                if (!LoadFaceConfigFromJson())
                {
                    InitDefaultRanges();
                }
            }

            // 构建容貌数据链表
            BuildHeadDataList();

            // 绑定UI事件
            BindEvents();

            // 初始化显示
            ApplySexFilter(-1); // 默认显示全部
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnloadAllTextures();
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 从JSON配置文件加载容貌区间配置。
        /// 配置文件路径: {ContentRootPath}/Data/FaceConfig.json
        /// </summary>
        /// <returns>加载成功返回true，文件不存在或解析失败返回false</returns>
        private bool LoadFaceConfigFromJson()
        {
            string configPath = System.IO.Path.Combine(Path.ContentRootPath, "Data/FaceConfig.json");

            if (!File.Exists(configPath))
            {
                Sango.Log.Warning($"[UIPersonHeadSelect] 未找到容貌配置文件: {configPath}，将使用默认配置");
                return false;
            }

            try
            {
                string jsonContent = File.ReadAllText(configPath);
                headIdRanges = TKNewtonsoft.Json.JsonConvert.DeserializeObject<HeadIdRange[]>(jsonContent);

                if (headIdRanges != null && headIdRanges.Length > 0)
                {
                    Sango.Log.Info($"[UIPersonHeadSelect] 从JSON加载容貌配置成功，共{headIdRanges.Length}个区间，路径: {configPath}");
                    return true;
                }
                else
                {
                    Sango.Log.Warning("[UIPersonHeadSelect] JSON容貌配置解析为空，将使用默认配置");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Sango.Log.Error($"[UIPersonHeadSelect] 解析容貌配置文件失败: {ex.Message}，将使用默认配置");
                return false;
            }
        }

        /// <summary>
        /// 初始化默认容貌区间配置。
        /// 标准男性 0~99，标准女性 100~168，
        /// 自定义男性 1001~1099，自定义女性 1100~1199。
        /// </summary>
        private void InitDefaultRanges()
        {
            headIdRanges = new HeadIdRange[]
            {
                new HeadIdRange
                {
                    name = "标准男性",
                    startId = 0,
                    endId = 99,
                    sexType = HeadSexType.Male
                },
                new HeadIdRange
                {
                    name = "标准女性",
                    startId = 100,
                    endId = 168,
                    sexType = HeadSexType.Female
                },
                new HeadIdRange
                {
                    name = "自定义男性",
                    startId = 1001,
                    endId = 1099,
                    sexType = HeadSexType.Male
                },
                new HeadIdRange
                {
                    name = "自定义女性",
                    startId = 1100,
                    endId = 1199,
                    sexType = HeadSexType.Female
                }
            };
            Sango.Log.Info("[UIPersonHeadSelect] 使用默认容貌区间配置：标准男0~99，标准女100~168，自定义男1001~1099，自定义女1100~1199");
        }

        /// <summary>
        /// 根据区间配置构建容貌数据链表。
        /// 遍历所有区间，展开每个区间内的ID为链表节点。
        /// 链表顺序 = 区间配置顺序（先男后女）。
        /// </summary>
        private void BuildHeadDataList()
        {
            headDataList.Clear();
            femaleStartIndex = -1;
            bool femaleFound = false;

            foreach (var range in headIdRanges)
            {
                if (range == null) continue;

                for (int id = range.startId; id <= range.endId; id++)
                {
                    var item = new HeadItemData(
                        headId: id.ToString(),
                        index: headDataList.Count,
                        sexType: range.sexType,
                        rangeName: range.name
                    );
                    headDataList.Add(item);

                    // 记录第一个女性容貌的位置
                    if (!femaleFound && (range.sexType == HeadSexType.Female))
                    {
                        femaleStartIndex = item.index;
                        femaleFound = true;
                    }
                }
            }

            Sango.Log.Info(
                $"[UIPersonHeadSelect] 容貌链表构建完成，共{headDataList.Count}个容貌，" +
                $"女性起点索引:{femaleStartIndex}"
            );
        }

        /// <summary>
        /// 绑定所有UI控件的点击事件。
        /// </summary>
        private void BindEvents()
        {
            // 导航按钮
            if (prevPageButton != null)
                prevPageButton.onClick.AddListener(OnPrevPageClicked);
            if (nextPageButton != null)
                nextPageButton.onClick.AddListener(OnNextPageClicked);

            // 筛选项
            if (maleToggle != null)
                maleToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(0); });
            if (femaleToggle != null)
                femaleToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(1); });
            if (customToggle != null)
                customToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(2); });

            // 底部按钮
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
            if (returnButton != null)
                returnButton.onClick.AddListener(OnReturnClicked);

            // 容貌格子点击
            if (headItemButtons != null)
            {
                for (int i = 0; i < headItemButtons.Length; i++)
                {
                    if (headItemButtons[i] != null)
                    {
                        int idx = i; // 闭包捕获
                        headItemButtons[i].onClick.AddListener(() => OnHeadItemClicked(idx));
                    }
                }
            }
        }

        #endregion

        #region 筛选逻辑

        /// <summary>
        /// 应用性别筛选。
        /// </summary>
        /// <param name="filterMode">-1=全部, 0=仅男性, 1=仅女性, 2=仅自定义</param>
        private void ApplySexFilter(int filterMode)
        {
            currentSexFilter = filterMode;
            filteredHeadDataList.Clear();

            foreach (var item in headDataList)
            {
                // -1 显示全部
                if (filterMode == -1)
                {
                    filteredHeadDataList.Add(item);
                    continue;
                }

                HeadSexType targetSex = (HeadSexType)filterMode;
                if (item.sexType == targetSex)
                {
                    filteredHeadDataList.Add(item);
                }
            }

            // 重置为第一页
            currentPage = 0;
            currentSelectedIndex = -1;
            RefreshPage();
        }

        /// <summary>
        /// 获取当前筛选模式下的总页数。
        /// </summary>
        private int GetTotalPageCount()
        {
            if (filteredHeadDataList.Count == 0) return 1;
            return (int)Math.Ceiling((float)filteredHeadDataList.Count / PAGE_SIZE);
        }

        /// <summary>
        /// 获取当前页在筛选后链表中的起始索引。
        /// </summary>
        private int GetCurrentPageStartIndex()
        {
            return currentPage * PAGE_SIZE;
        }

        #endregion

        #region 页面刷新

        /// <summary>
        /// 刷新当前页面显示。
        /// 仅加载当前页面所需的容貌纹理，清理上一页的纹理缓存。
        /// </summary>
        private void RefreshPage()
        {
            UnloadPageTextures();
            RefreshPagination();
            RefreshHeadItems();
        }

        /// <summary>
        /// 刷新翻页控件状态。
        /// </summary>
        private void RefreshPagination()
        {
            int totalPages = GetTotalPageCount();
            if (prevPageButton != null)
                prevPageButton.interactable = currentPage > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = currentPage < totalPages - 1;
            if (pageText != null)
                pageText.text = $"{currentPage + 1}/{totalPages}";
        }

        /// <summary>
        /// 刷新当前页28个容貌格子。
        /// 仅加载当前页所需的容貌纹理。
        /// </summary>
        private void RefreshHeadItems()
        {
            int startIdx = GetCurrentPageStartIndex();
            int itemCount = (headItemIcons != null) ? headItemIcons.Length : 0;

            for (int i = 0; i < itemCount; i++)
            {
                int dataIdx = startIdx + i;
                bool hasData = (dataIdx >= 0 && dataIdx < filteredHeadDataList.Count);

                if (headItemIcons[i] != null)
                {
                    if (hasData)
                    {
                        headItemIcons[i].gameObject.SetActive(true);
                        var headData = filteredHeadDataList[dataIdx];
                        Texture tex = LoadHeadTexture(headData.headId);
                        if (tex != null)
                        {
                            headItemIcons[i].texture = tex;
                        }
                        // 高亮当前选中项
                        if (dataIdx == currentSelectedIndex)
                        {
                            headItemIcons[i].color = Color.white;
                        }
                        else
                        {
                            headItemIcons[i].color = new Color(0.7f, 0.7f, 0.7f, 1f);
                        }
                    }
                    else
                    {
                        headItemIcons[i].gameObject.SetActive(false);
                    }
                }

                // 刷新"使用中"标签
                if (headItemUsedTexts != null && i < headItemUsedTexts.Length && headItemUsedTexts[i] != null)
                {
                    if (hasData)
                    {
                        var headData = filteredHeadDataList[dataIdx];
                        bool isUsed = usedHeadIdSet.Contains(headData.headId);
                        headItemUsedTexts[i].gameObject.SetActive(isUsed);
                    }
                    else
                    {
                        headItemUsedTexts[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        #endregion

        #region 纹理加载与卸载

        /// <summary>
        /// 加载单个容貌纹理（带本地缓存）。
        /// 加载路径: Assets/Face/{id}_2，包名: Face。
        /// </summary>
        /// <param name="headId">容貌ID</param>
        /// <returns>容貌纹理，加载失败返回null</returns>
        private Texture LoadHeadTexture(string headId)
        {
            if (string.IsNullOrEmpty(headId)) return null;
            if (loadedTextureCache.TryGetValue(headId, out var cachedTex) && cachedTex != null)
            {
                return cachedTex;
            }

            Texture tex = GameRenderHelper.LoadHeadIcon(headId, HEAD_ICON_TYPE);
            if (tex != null)
            {
                loadedTextureCache[headId] = tex;
            }
            return tex;
        }

        /// <summary>
        /// 清理当前页纹理缓存。
        /// 翻页时调用，释放上一页面容纹理。
        /// </summary>
        private void UnloadPageTextures()
        {
            foreach (var kvp in loadedTextureCache)
            {
                if (kvp.Value != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value);
                }
            }
            loadedTextureCache.Clear();
        }

        /// <summary>
        /// 卸载所有已加载的纹理。
        /// 窗口销毁时调用。
        /// </summary>
        private void UnloadAllTextures()
        {
            UnloadPageTextures();
        }

        #endregion

        #region 交互事件

        /// <summary>
        /// 上一页按钮点击。
        /// </summary>
        private void OnPrevPageClicked()
        {
            if (currentPage > 0)
            {
                currentPage--;
                RefreshPage();
            }
        }

        /// <summary>
        /// 下一页按钮点击。
        /// </summary>
        private void OnNextPageClicked()
        {
            int totalPages = GetTotalPageCount();
            if (currentPage < totalPages - 1)
            {
                currentPage++;
                RefreshPage();
            }
        }

        /// <summary>
        /// 容貌格子点击。
        /// 选中该容貌并在预览区显示大图。
        /// </summary>
        /// <param name="itemIndex">格子本地索引（0~27）</param>
        private void OnHeadItemClicked(int itemIndex)
        {
            int dataIdx = GetCurrentPageStartIndex() + itemIndex;
            if (dataIdx < 0 || dataIdx >= filteredHeadDataList.Count) return;

            // 更新选中状态
            int oldSelectedIdx = currentSelectedIndex;
            currentSelectedIndex = dataIdx;

            // 刷新格子高亮
            // 取消旧选中高亮
            if (oldSelectedIdx >= 0)
            {
                int oldLocalIdx = oldSelectedIdx - GetCurrentPageStartIndex();
                if (oldLocalIdx >= 0 && oldLocalIdx < PAGE_SIZE &&
                    headItemIcons != null && oldLocalIdx < headItemIcons.Length &&
                    headItemIcons[oldLocalIdx] != null)
                {
                    headItemIcons[oldLocalIdx].color = new Color(0.7f, 0.7f, 0.7f, 1f);
                }
            }

            // 设置新选中高亮
            int newLocalIdx = itemIndex;
            if (headItemIcons != null && newLocalIdx < headItemIcons.Length &&
                headItemIcons[newLocalIdx] != null)
            {
                headItemIcons[newLocalIdx].color = Color.white;
            }

            // 更新预览大图
            var selectedData = filteredHeadDataList[dataIdx];
            RefreshPreview(selectedData.headId);
        }

        /// <summary>
        /// 刷新预览区大图。
        /// </summary>
        /// <param name="headId">要预览的容貌ID</param>
        private void RefreshPreview(string headId)
        {
            if (previewImage != null)
            {
                Texture tex = LoadHeadTexture(headId);
                if (tex != null)
                {
                    previewImage.texture = tex;
                }
            }
            if (selectedIdText != null)
            {
                selectedIdText.text = $"ID: {headId}";
            }
        }

        /// <summary>
        /// 确认按钮点击。
        /// 触发 <see cref="OnHeadSelected"/> 回调并关闭窗口。
        /// </summary>
        private void OnConfirmClicked()
        {
            if (currentSelectedIndex < 0 || currentSelectedIndex >= filteredHeadDataList.Count)
            {
                Sango.Log.Info("[UIPersonHeadSelect] 未选择任何容貌，无法确认");
                return;
            }

            var selectedData = filteredHeadDataList[currentSelectedIndex];
            Sango.Log.Info($"[UIPersonHeadSelect] 确认选择容貌，ID: {selectedData.headId}");

            OnHeadSelected?.Invoke(selectedData.headId);
            CloseWindow();
        }

        /// <summary>
        /// 返回/取消按钮点击。
        /// </summary>
        private void OnReturnClicked()
        {
            Sango.Log.Info("[UIPersonHeadSelect] 取消选择，返回");
            OnWindowClosed?.Invoke();
            CloseWindow();
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置已使用的容貌ID集合。
        /// 被占用的容貌将在列表中显示"使用中"标签。
        /// </summary>
        /// <param name="usedIds">已使用的容貌ID集合</param>
        public void SetUsedHeadIds(HashSet<string> usedIds)
        {
            if (usedIds != null)
            {
                usedHeadIdSet = usedIds;
            }
            else
            {
                usedHeadIdSet.Clear();
            }
            RefreshPage();
        }

        /// <summary>
        /// 设置当前选中的容貌ID。
        /// 在筛选后的链表中查找并高亮该ID。
        /// </summary>
        /// <param name="headId">要选中的容貌ID</param>
        public void SetSelectedHeadId(string headId)
        {
            if (string.IsNullOrEmpty(headId)) return;

            // 在筛选链表中查找
            for (int i = 0; i < filteredHeadDataList.Count; i++)
            {
                if (filteredHeadDataList[i].headId == headId)
                {
                    currentSelectedIndex = i;
                    // 跳转到该容貌所在页
                    currentPage = i / PAGE_SIZE;
                    return;
                }
            }

            // 如果筛选链表中没找到，尝试在完整链表中确认性别后切换筛选
            for (int i = 0; i < headDataList.Count; i++)
            {
                if (headDataList[i].headId == headId)
                {
                    var sexType = headDataList[i].sexType;
                    ApplySexFilter((int)sexType);

                    // 在新筛选链表中再次查找
                    for (int j = 0; j < filteredHeadDataList.Count; j++)
                    {
                        if (filteredHeadDataList[j].headId == headId)
                        {
                            currentSelectedIndex = j;
                            currentPage = j / PAGE_SIZE;
                            return;
                        }
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// 获取当前选中的容貌ID。
        /// </summary>
        /// <returns>选中的容貌ID，未选中时返回null</returns>
        public string GetSelectedHeadId()
        {
            if (currentSelectedIndex < 0 || currentSelectedIndex >= filteredHeadDataList.Count)
                return null;
            return filteredHeadDataList[currentSelectedIndex].headId;
        }

        /// <summary>
        /// 获取容貌总数（筛选后）。
        /// </summary>
        public int GetFilteredCount()
        {
            return filteredHeadDataList.Count;
        }

        /// <summary>
        /// 获取容貌总数（全部）。
        /// </summary>
        public int GetTotalCount()
        {
            return headDataList.Count;
        }

        /// <summary>
        /// 获取女性容貌起点索引。
        /// </summary>
        public int GetFemaleStartIndex()
        {
            return femaleStartIndex;
        }

        /// <summary>
        /// 关闭窗口。
        /// </summary>
        private void CloseWindow()
        {
            Window.Instance.Close("window_create_person_image");
        }

        #endregion
    }
}
