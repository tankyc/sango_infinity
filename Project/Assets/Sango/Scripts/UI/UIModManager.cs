using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sango.Mod;

using Sango.Core;
namespace Sango.UI
{
    /// <summary>
    /// MOD管理界面
    /// </summary>
    public class UIModManager : UGUIWindow
    {
        public Text modInfoText;
        public Text modDescriptionText;
        public Text authorText;
        public RawImage modPosterImg;
        public Button backButton;
        public Button applyButton;

        public InputField urlInput;
        public GameObject loadingNode;
        public GameObject rootNode;
        public Text loandingTxt;

        public RectTransform sliderRect;
        public Scrollbar scrollbar;
        public UIModItem[] uIModItems;
        UIModItem currentSelectItem;
        RectTransform[] uIObjectListItemsRect;
        protected int startIndex = 0;
        protected int itemCount = 0;
        protected int totalCount = 0;
        protected int selectedIndex = 0;

        /// <summary>
        /// 封面请求序号：远程封面是异步下载的，回调回来时用户可能已经切到别的模组了，
        /// 用自增的序号判断"这次结果是否还属于当前选中项"，避免串图
        /// </summary>
        int posterRequestId = 0;



        List<Mod.Mod> allMods = new List<Mod.Mod>();
        List<Mod.Mod> enabledMods = new List<Mod.Mod>();

        protected override void Awake()
        {
            itemCount = uIModItems.Length;
            uIObjectListItemsRect = new RectTransform[uIModItems.Length];
            for (int i = 0; i < uIModItems.Length; i++)
                uIObjectListItemsRect[i] = uIModItems[i].GetComponent<RectTransform>();
        }

        public override void OnOpen()
        {
            rootNode.SetActive(false);
            loandingTxt.text = $"加载中.....0%";
            loadingNode.SetActive(true);
            ModManager.Instance.LoadMarketAsync(
                () =>
                {
                    rootNode.SetActive(true);
                    loadingNode.SetActive(false);
                    InitUI();
                },
                (f) =>
                {
                    loandingTxt.text = $"加载中.....{(int)(f * 100)}%";
                });
        }

        void InitUI()
        {
            LoadModList();
            selectedIndex = 0;
            ShowModInfo(-1);
            BindEvents();
            totalCount = allMods.Count;
            if (totalCount < itemCount)
            {
                sliderRect.gameObject.SetActive(false);
            }
            else
            {
                sliderRect.gameObject.SetActive(true);
                scrollbar.size = System.Math.Max(0.1f, (float)itemCount / (float)totalCount);
                scrollbar.SetValueWithoutNotify(0);
            }
            startIndex = 0;
            OnScrollBarValueChange(0);
            GameEvent.OnModUpdate -= OnModUpdate;
            GameEvent.OnModUpdate += OnModUpdate;
            GameEvent.OnModDownloadFailed -= OnModDownloadFailed;
            GameEvent.OnModDownloadFailed += OnModDownloadFailed;
        }

        public override void OnClose()
        {
            GameEvent.OnModUpdate -= OnModUpdate;
            GameEvent.OnModDownloadFailed -= OnModDownloadFailed;
            base.OnClose();
        }

        /// <summary>
        /// 模组下载失败时的界面反馈
        /// 失败原因必须弹给玩家看:否则进度条一消失就什么都没了,
        /// 玩家会以为"点了下载没有任何反应"(正式包还会把 Warning 日志整句编译剔除)
        /// </summary>
        /// <param name="mod">下载失败的模组</param>
        /// <param name="reason">失败原因(中文,可直接展示)</param>
        void OnModDownloadFailed(Mod.Mod mod, string reason)
        {
            string modName = mod == null ? "" : (string.IsNullOrEmpty(mod.Name) ? mod.Id : mod.Name);
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                $"模组下载失败: {modName}\n{reason}",
                () => { });
        }

        void OnModUpdate(Mod.Mod mod)
        {
            if (!ModManager.Instance.HasMod(mod))
            {
                allMods.Remove(mod);
                enabledMods.Remove(mod);
                totalCount = allMods.Count;
            }
            // 有模组被删除时,选中下标可能越界,这里做一次收敛
            if (totalCount <= 0)
                selectedIndex = 0;
            else if (selectedIndex >= totalCount)
                selectedIndex = totalCount - 1;
            UpdateItemStartIndex(startIndex);
        }

        private void LoadModList()
        {
            allMods.Clear();
            enabledMods.Clear();

            // 获取所有MOD
            foreach (var mod in ModManager.Instance.mModMap.Values)
            {
                allMods.Add(mod);
            }

            // 获取已启用的MOD
            string[] enabledModNames = ModManager.Instance.LoadModList();
            if (enabledModNames != null)
            {
                for (int i = 0; i < enabledModNames.Length; i++)
                {
                    string modName = enabledModNames[i];
                    Mod.Mod mod = allMods.Find(x => x.Id == modName);
                    if (mod != null)
                    {
                        enabledMods.Add(mod);
                    }
                }
            }

            SortMod();
        }

        private void BindEvents()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnReturn);
            }

            if (applyButton != null)
            {
                applyButton.onClick.RemoveAllListeners();
                applyButton.onClick.AddListener(OnApply);
            }
        }

        public void OnSelectMod(int index)
        {
            ShowModInfo(index);
        }

        /// <summary>
        /// 勾选/取消勾选某个模组
        /// 勾选时:自动把该模组的所有前置依赖一并勾选;
        /// 取消时:自动把依赖该模组的已启用模组一并取消,保证依赖关系始终完整
        /// </summary>
        public void OnToggleMod(int index, bool isEnabled)
        {
            if (index < 0 || index >= allMods.Count)
                return;

            Mod.Mod mod = allMods[index];
            if (isEnabled)
                EnableModWithDepends(mod);
            else
                DisableModWithDependents(mod);

            // 依赖关系变化后重新排序(依赖在前),并让选中项跟随
            RefreshModListOrder();
        }

        /// <summary>
        /// 勾选模组时,连同它的前置依赖一起勾选(递归)
        /// 尚未下载的依赖会单独提示玩家,不会写进启用列表
        /// </summary>
        void EnableModWithDepends(Mod.Mod mod)
        {
            List<Mod.Mod> chain = ModManager.Instance.CollectDepends(mod);
            List<string> notDownloaded = new List<string>();
            for (int i = 0; i < chain.Count; i++)
            {
                Mod.Mod dep = chain[i];
                if (dep == mod)
                    continue;

                // 依赖还没下载时不能启用,记下来提示玩家先下载
                if (!dep.IsValidMod())
                {
                    notDownloaded.Add(string.IsNullOrEmpty(dep.Name) ? dep.Id : dep.Name);
                    continue;
                }

                if (!enabledMods.Contains(dep))
                    enabledMods.Add(dep);
            }

            if (!enabledMods.Contains(mod))
                enabledMods.Add(mod);

            if (notDownloaded.Count > 0)
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                    $"以下前置模组尚未下载,下载后依赖会自动勾选:\n{string.Join("\n", notDownloaded)}",
                    () => { });
            }
        }

        /// <summary>
        /// 取消勾选模组时,把所有(直接或间接)依赖它的已启用模组一并取消
        /// </summary>
        void DisableModWithDependents(Mod.Mod mod)
        {
            // 先收集被牵连的模组(含自身)
            List<Mod.Mod> affected = new List<Mod.Mod>();
            affected.Add(mod);

            // 反复扫描,直到没有新的模组被牵连为止(处理多层依赖)
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < enabledMods.Count; i++)
                {
                    Mod.Mod candidate = enabledMods[i];
                    if (IsModInList(affected, candidate))
                        continue;

                    string[] depIds = ModManager.ParseDepends(candidate.Depends);
                    for (int j = 0; j < depIds.Length; j++)
                    {
                        if (IsModIdInList(affected, depIds[j]))
                        {
                            affected.Add(candidate);
                            changed = true;
                            break;
                        }
                    }
                }
            }

            // 统一取消勾选
            for (int i = 0; i < affected.Count; i++)
                enabledMods.Remove(affected[i]);
        }

        /// <summary>
        /// 判断模组是否在列表中(按引用比较)
        /// </summary>
        static bool IsModInList(List<Mod.Mod> list, Mod.Mod mod)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == mod)
                    return true;
            return false;
        }

        /// <summary>
        /// 判断模组Id是否在列表中
        /// </summary>
        static bool IsModIdInList(List<Mod.Mod> list, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].Id == id)
                    return true;
            return false;
        }

        /// <summary>
        /// 重新排列模组列表:
        /// 1. 已启用的模组排到前面,并按依赖做拓扑排序(被依赖者在前);
        /// 2. 未启用的模组保持原有相对顺序排在后面;
        /// 3. 重排后让当前选中项继续保持选中,并滚动到可见区域
        /// </summary>
        void RefreshModListOrder()
        {
            Mod.Mod selectedMod = (selectedIndex >= 0 && selectedIndex < allMods.Count) ? allMods[selectedIndex] : null;

            SortMod();
            totalCount = allMods.Count;

            if (selectedMod != null)
            {
                int newIndex = allMods.IndexOf(selectedMod);
                if (newIndex >= 0)
                    selectedIndex = newIndex;
            }
            if (selectedIndex < 0 || selectedIndex >= allMods.Count)
                selectedIndex = 0;

            ScrollToSelected();
            UpdateItemStartIndex(startIndex);
        }

        /// <summary>
        /// 对已启用的模组做依赖拓扑排序,并把未启用的模组接在后面
        /// </summary>
        void SortMod()
        {
            // 已启用的模组先按依赖拓扑排序,保证"被依赖的模组"排在前面(加载顺序满足依赖)
            List<Mod.Mod> mods = SortModsByDepends(enabledMods);
            for (int i = 0; i < allMods.Count; i++)
            {
                Mod.Mod dest = allMods[i];
                if (!enabledMods.Contains(dest))
                    mods.Add(dest);
            }
            allMods = mods;
        }

        /// <summary>
        /// 对模组集合做依赖拓扑排序(深度优先,后序输出)
        /// 结果中:被依赖的模组在前,依赖它们的模组在后;无依赖关系的模组尽量保持原相对顺序;
        /// 遇到循环依赖时自动跳过,避免死循环
        /// </summary>
        /// <param name="mods">待排序的模组集合</param>
        /// <returns>依赖优先的模组列表</returns>
        List<Mod.Mod> SortModsByDepends(List<Mod.Mod> mods)
        {
            List<Mod.Mod> sorted = new List<Mod.Mod>();
            HashSet<Mod.Mod> visited = new HashSet<Mod.Mod>();
            HashSet<Mod.Mod> visiting = new HashSet<Mod.Mod>();
            for (int i = 0; i < mods.Count; i++)
                VisitModForSort(mods[i], mods, sorted, visited, visiting);
            return sorted;
        }

        /// <summary>
        /// 拓扑排序的单次深度优先访问
        /// </summary>
        /// <param name="mod">当前访问的模组</param>
        /// <param name="mods">本次排序的模组集合(只在其中查找依赖,避免把未启用的模组混进来)</param>
        /// <param name="sorted">排序结果</param>
        /// <param name="visited">已排好的模组</param>
        /// <param name="visiting">正在访问路径上的模组(用于检测循环依赖)</param>
        void VisitModForSort(Mod.Mod mod, List<Mod.Mod> mods, List<Mod.Mod> sorted,
            HashSet<Mod.Mod> visited, HashSet<Mod.Mod> visiting)
        {
            if (mod == null || visited.Contains(mod) || visiting.Contains(mod))
                return;

            visiting.Add(mod);

            string[] depIds = ModManager.ParseDepends(mod.Depends);
            for (int i = 0; i < depIds.Length; i++)
            {
                Mod.Mod dep = null;
                for (int j = 0; j < mods.Count; j++)
                {
                    if (mods[j].Id == depIds[i])
                    {
                        dep = mods[j];
                        break;
                    }
                }
                if (dep != null)
                    VisitModForSort(dep, mods, sorted, visited, visiting);
            }

            visiting.Remove(mod);
            visited.Add(mod);
            sorted.Add(mod);
        }

        /// <summary>
        /// 上移:把当前选中项向上移动一位
        /// </summary>
        public void ModUp()
        {
            MoveSelectedBy(-1);
        }

        /// <summary>
        /// 下移:把当前选中项向下移动一位
        /// </summary>
        public void ModDown()
        {
            MoveSelectedBy(1);
        }

        /// <summary>
        /// 置顶:把当前选中项直接移到第一位
        /// </summary>
        public void UpToFront()
        {
            MoveSelectedTo(0);
        }

        /// <summary>
        /// 置底:把当前选中项直接移到最后一位
        /// </summary>
        public void DownToEnd()
        {
            MoveSelectedTo(allMods.Count - 1);
        }

        /// <summary>
        /// 当前选中项相对移动(offset 为 -1 表示上移一位,1 表示下移一位)
        /// </summary>
        /// <param name="offset">相对偏移量</param>
        void MoveSelectedBy(int offset)
        {
            if (!HasSelected())
                return;
            MoveTo(selectedIndex, selectedIndex + offset);
        }

        /// <summary>
        /// 当前选中项移动到指定下标
        /// </summary>
        /// <param name="targetIndex">目标下标</param>
        void MoveSelectedTo(int targetIndex)
        {
            if (!HasSelected())
                return;
            MoveTo(selectedIndex, targetIndex);
        }

        /// <summary>
        /// 是否存在有效的选中项
        /// </summary>
        bool HasSelected()
        {
            return selectedIndex >= 0 && selectedIndex < allMods.Count;
        }

        /// <summary>
        /// 把第 fromIndex 个模组移动到 toIndex 位置(中间模组依次顺移,而不是两两交换)
        /// 移动后:
        ///   1. 选中项跟随被移动的模组;
        ///   2. 同步启用列表的顺序,避免下次勾选/取消模组时被重新排序把手动顺序冲掉;
        ///   3. 让选中项保持在可视区域内(位于当前界面第一位/最后一位时,列表同步位移显示)
        /// </summary>
        /// <param name="fromIndex">被移动模组当前所在下标</param>
        /// <param name="toIndex">目标下标</param>
        void MoveTo(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= allMods.Count)
                return;
            if (toIndex < 0 || toIndex >= allMods.Count)
                return;
            if (fromIndex == toIndex)
                return;

            Mod.Mod mod = allMods[fromIndex];
            allMods.RemoveAt(fromIndex);
            allMods.Insert(toIndex, mod);

            // 选中项跟随被移动的模组
            selectedIndex = toIndex;

            SyncEnabledOrder();

            // 被移动到显示区外时(原本在界面第一位/最后一位),同步滚动让选中项继续可见
            ScrollToSelected();
            UpdateItemStartIndex(startIndex);
        }

        /// <summary>
        /// 让已启用列表 enabledMods 的顺序与界面列表 allMods 中已启用模组的顺序保持一致
        /// 否则下次勾选/取消模组触发 SortMod 重排时,会按旧的启用顺序把玩家手动调整好的顺序冲掉
        /// </summary>
        void SyncEnabledOrder()
        {
            List<Mod.Mod> ordered = new List<Mod.Mod>(enabledMods.Count);
            for (int i = 0; i < allMods.Count; i++)
            {
                Mod.Mod mod = allMods[i];
                if (enabledMods.Contains(mod))
                    ordered.Add(mod);
            }
            enabledMods = ordered;
        }

        /// <summary>
        /// 让当前选中项保持在可视区域内(必要时调整 startIndex)
        /// </summary>
        void ScrollToSelected()
        {
            if (totalCount <= itemCount)
            {
                startIndex = 0;
            }
            else
            {
                int maxStart = totalCount - itemCount;
                if (selectedIndex < startIndex)
                    startIndex = selectedIndex;
                else if (selectedIndex >= startIndex + itemCount)
                    startIndex = selectedIndex - itemCount + 1;
                startIndex = UnityEngine.Mathf.Clamp(startIndex, 0, maxStart);
            }
            SyncScrollbar();
        }

        /// <summary>
        /// 把当前滚动位置同步到滚动条(不触发滚动条回调)
        /// </summary>
        void SyncScrollbar()
        {
            if (scrollbar != null && totalCount > itemCount)
                scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
        }

        public void ShowModInfo(int index)
        {
            // 切换选中项即作废上一次的封面请求（远程封面是异步下载的，回来后可能已经不属于当前项）
            posterRequestId++;

            if (index < 0 || index >= allMods.Count)
            {
                modInfoText.text = "";
                modDescriptionText.text = "";
                if (modPosterImg != null)
                {
                    modPosterImg.texture = null;
                    modPosterImg.enabled = false;
                }
                return;
            }

            Mod.Mod mod = allMods[index];
            string modVersion = mod.IsValidMod() ? mod.Version : mod.UrlVersion;
            // 带上模组 ID：它就是前置依赖要填的那个 ID，玩家在游戏里能直接看到、抄给别人
            modInfoText.text = string.IsNullOrEmpty(mod.Id)
                ? $"{mod.Name} v{modVersion}"
                : $"{mod.Name} v{modVersion}   [{mod.Id}]";
            modDescriptionText.text = mod.Description;
            authorText.text = $"作者: {mod.Author}";
            if (modPosterImg != null)
            {
                if (string.IsNullOrEmpty(mod.Poster))
                {
                    // 没有封面：清掉上一张，避免显示成上一个模组的图
                    modPosterImg.texture = null;
                    modPosterImg.enabled = false;
                }
                else if (ModPosterLoader.IsRemote(mod.Poster))
                {
                    // 创意工坊上传的封面：包内 mod.info 里记的是封面的 http(s) 地址，需要联网取图。
                    // 先显示占位图，下载完成后（若期间没切走）再替换。
                    modPosterImg.enabled = true;
                    modPosterImg.texture = ModPosterLoader.EmptyTexture();

                    int requestId = posterRequestId;
                    ModPosterLoader.LoadRemote(this, mod.Poster, texture =>
                    {
                        if (modPosterImg == null || requestId != posterRequestId) return;
                        modPosterImg.texture = texture != null ? texture : ModPosterLoader.EmptyTexture();
                    });
                }
                else
                {
                    // 包内文件名：老包与手工打包的包都是这种，保持原来的同步本地加载
                    modPosterImg.enabled = true;
                    modPosterImg.texture = ModPosterLoader.LoadLocal(mod.GetFullPath(mod.Poster));
                }
            }
        }

        public void OnReturn()
        {
            Window.Instance.Open("window_start");
            Window.Instance.Close("window_mod_manager");
        }

        public void OnApply()
        {

            List<Mod.Mod> enabled_list = new List<Mod.Mod>();
            foreach (Mod.Mod mod in allMods)
            {
                if (enabledMods.Contains(mod))
                    enabled_list.Add(mod);
            }

            // 保存前再做一次依赖拓扑排序,保证"前置依赖"永远排在"依赖它的模组"之前(先加载)
            List<Mod.Mod> sorted_list = SortModsByDepends(enabled_list);

            // 保存MOD列表
            ModManager.Instance.SaveModList(sorted_list.ToArray());
            //// 重新初始化MOD
            //ModManager.Instance.InitMods(enabledMods.ToArray());
            // 显示提示，需要重启游戏才能生效
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, "修改MOD后需要重启游戏才能生效，点击确定重启游戏。", () =>
            {
                // 重启游戏(按平台分别处理,不是简单的退出)
                Sango.Platform.RestartGame();
            });
        }

        public void OnScrollBarValueChange(float value)
        {
            startIndex = (int)UnityEngine.Mathf.Lerp(0, totalCount - itemCount, value);
            UpdateItemStartIndex(startIndex);
        }

        public void UpdateItemStartIndex(int startIndex)
        {
            int enabledCount = enabledMods.Count;
            for (int i = 0; i < itemCount; i++)
            {
                UIModItem listItem = uIModItems[i];
                int destIndex = i + startIndex;
                listItem.targetIndex = destIndex;
                if (destIndex < totalCount)
                {
                    Mod.Mod mod = allMods[destIndex];
                    listItem.SetSelected(selectedIndex == destIndex).
                        SetEnabled(enabledMods.Contains(mod)).
                        SetArrowInteractable(destIndex > 0, destIndex < totalCount - 1);
                    listItem.BindToggleCall(OnToggleMod);
                    // 上移/下移统一由本窗口处理,列表项只持有窗口引用
                    listItem.SetManager(this);
                    listItem.SetMod(mod);
                }
                else
                {
                    listItem.SetMod(null);
                }
            }
        }

        public void UpShow()
        {
            if (startIndex > 0)
                startIndex--;
            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
        }

        public void DownShow()
        {
            if (startIndex < totalCount - itemCount)
                startIndex++;

            UpdateItemStartIndex(startIndex);
            scrollbar.SetValueWithoutNotify((float)startIndex / (totalCount - itemCount));
        }

        public void OnObjectListItemPressDown(UIModItem item)
        {
            item.SetPressd(true);
            currentSelectItem = item;
        }

        public void OnObjectListItemPressUp(UIModItem item)
        {
            item.SetPressd(false);
            for (int i = 0; i < itemCount; i++)
            {
                RectTransform itemRect = uIObjectListItemsRect[i];
                UIModItem listItem = uIModItems[i];
                if (listItem == currentSelectItem && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    OnItemSelected(item);
                    break;
                }
            }
        }

        public void OnObjectListItemPointEnter(UIModItem item)
        {
            item.SetOver(true);
            ShowModInfo(item.targetIndex);
        }
        public void OnObjectListItemPointExit(UIModItem item)
        {
            item.SetOver(false);
        }

        public virtual void OnItemSelected(UIModItem item)
        {
            if (item.targetIndex >= totalCount)
                return;

            int destIndex = item.targetIndex;
            if (destIndex == selectedIndex)
                return;

            for (int i = 0; i < itemCount; i++)
            {
                UIModItem listItem = uIModItems[i];
                if (listItem.targetIndex == selectedIndex)
                {
                    listItem.SetSelected(false);
                    break;
                }
            }
            selectedIndex = destIndex;
            item.SetSelected(true);
            OnSelectMod(selectedIndex);
        }

        public void RefreshModList()
        {
            OnOpen();
        }

        bool CheckUrlValid(string url)
        {
            if (!url.StartsWith("http"))
                return false;
            else if (!url.EndsWith("/mod_list.txt"))
                return false;
            return true;
        }

        public void OnAddUrlModMarket()
        {
            string txt = urlInput.text;
            AddUrlMarket(txt);
        }

        void AddUrlMarket(string url)
        {
            if (CheckUrlValid(url))
            {
                loandingTxt.text = $"加载中.....0%";
                loadingNode.SetActive(true);
                ModManager.Instance.AddMarketFromUrl(
                    url, () =>
                    {
                        loadingNode.SetActive(false);
                        InitUI();
                    },
                    (f) =>
                    {
                        loandingTxt.text = $"加载中.....{(int)(f * 100)}%";
                    });
            }
        }

        public void OpenWorkshop()
        {
            Application.OpenURL("http://139.155.98.66/browse");
        }

        /// <summary>
        /// 清理市场数据和所有模组(按钮入口)
        /// 流程: 先弹对话框询问玩家,点击确定才真正清理,清理完成后提示玩家重启游戏
        /// </summary>
        public void OnClearAllMods()
        {
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "清理将删除市场数据和所有已下载的模组,清理完成后需要重启游戏才能生效。确定要清理吗?",
                OnConfirmClearAllMods);
        }

        /// <summary>
        /// 玩家点击确定后: 清理市场数据和所有模组,然后提示重启游戏
        /// </summary>
        void OnConfirmClearAllMods()
        {
            ModManager.Instance.ClearAll();
            GameDialog.Instance.Open(GameDialog.DialogStyle.Normal,
                "清理完成,点击确定重启游戏。",
                () =>
                {
                    // 与修改模组后一致:模组环境需要重启游戏重新初始化
                    Sango.Platform.RestartGame();
                });
        }
    }
}
