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
        /// 已使用的容貌ID集合（当前部队/势力中已被占用的容貌）
        /// </summary>
        private HashSet<int> usedHeadIdSet = new HashSet<int>();

        #endregion

        #region 回调

        /// <summary>选择容貌回调，参数为选中的容貌ID</summary>
        public Action<int> OnHeadSelected;

        /// <summary>关闭窗口回调</summary>
        public Action OnWindowClosed;

        #endregion

        List<int> filteredHeadDataList;

        #region 生命周期

        public override void OnOpen(params object[] objs)
        {
            int headId = (int)(objs[0]);
            OnHeadSelected = (Action<int>)(objs[1]);
            filteredHeadDataList = GameCustomEdit.Instance.headDataList;
            // 绑定UI事件
            BindEvents();
            RefreshPreview(headId);
            // 重置为第一页
            currentPage = 0;
            SetSelectedHeadId(headId);
            RefreshPage();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnloadAllTextures();
        }

        #endregion

        #region 初始化方法

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
            //if (maleToggle != null)
            //    maleToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(0); });
            //if (femaleToggle != null)
            //    femaleToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(1); });
            //if (customToggle != null)
            //    customToggle.onValueChanged.AddListener((isOn) => { if (isOn) ApplySexFilter(2); });

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
                        Texture tex = LoadHeadTexture(headData, 2);
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
                        bool isUsed = usedHeadIdSet.Contains(headData);
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
        private Texture LoadHeadTexture(int headId, int type)
        {
            return GameRenderHelper.LoadHeadIcon(headId, type);
        }

        /// <summary>
        /// 清理当前页纹理缓存。
        /// 翻页时调用，释放上一页面容纹理。
        /// </summary>
        private void UnloadPageTextures()
        {
            //foreach (var kvp in loadedTextureCache)
            //{
            //    if (kvp.Value != null)
            //    {
            //        UnityEngine.Object.Destroy(kvp.Value);
            //    }
            //}
            //loadedTextureCache.Clear();
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
            RefreshPreview(selectedData);
        }

        /// <summary>
        /// 刷新预览区大图。
        /// </summary>
        /// <param name="headId">要预览的容貌ID</param>
        private void RefreshPreview(int headId)
        {
            if (previewImage != null)
            {
                Texture tex = LoadHeadTexture(headId, 1);
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
            Sango.Log.Info($"[UIPersonHeadSelect] 确认选择容貌，ID: {selectedData}");

            OnHeadSelected?.Invoke(selectedData);
            CloseWindow();
        }

        /// <summary>
        /// 返回/取消按钮点击。
        /// </summary>
        private void OnReturnClicked()
        {
            //Sango.Log.Info("[UIPersonHeadSelect] 取消选择，返回");
            //OnWindowClosed?.Invoke();
            //CloseWindow();
            OnConfirmClicked();
        }

        #endregion

        #region 公共接口

        /// <summary>
        /// 设置已使用的容貌ID集合。
        /// 被占用的容貌将在列表中显示"使用中"标签。
        /// </summary>
        /// <param name="usedIds">已使用的容貌ID集合</param>
        public void SetUsedHeadIds(HashSet<int> usedIds)
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
        public void SetSelectedHeadId(int headId)
        {
            // 在筛选链表中查找
            for (int i = 0; i < filteredHeadDataList.Count; i++)
            {
                if (filteredHeadDataList[i] == headId)
                {
                    currentSelectedIndex = i;
                    // 跳转到该容貌所在页
                    currentPage = i / PAGE_SIZE;
                    return;
                }
            }
        }

        /// <summary>
        /// 获取当前选中的容貌ID。
        /// </summary>
        /// <returns>选中的容貌ID，未选中时返回null</returns>
        public int GetSelectedHeadId()
        {
            if (currentSelectedIndex < 0 || currentSelectedIndex >= filteredHeadDataList.Count)
                return -1;
            return filteredHeadDataList[currentSelectedIndex];
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
            return filteredHeadDataList.Count;
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
