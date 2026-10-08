using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    public class ObjectSelectSystem : ObjectsDisplaySystem
    {
        public List<SangoObject> selected = new List<SangoObject>();
        protected Action<List<SangoObject>> sureAction;
        public int selectLimit = 0;
        public bool donotFinishThisSystem = false;
        protected Window.WindowInterface WindowInterface { set; get; }

        /// <summary>
        /// 视图过滤: 根据当前已勾选的对象决定其他人是否显示, 返回false则暂时从列表移除;
        /// 为null时不过滤, 所有调用方的既有行为完全不变
        /// </summary>
        public Func<SangoObject, bool> displayFilter;

        // 注：完整候选集 allObjects 已上移到基类 ObjectsDisplaySystem，
        // 由 Objects 的赋值统一刷新，避免各选择器的 Start 各写一遍（历史上有大量选择器漏写）。

        /// <summary>
        /// 勾选状态已变化, 等待重新应用过滤。
        /// 延后到Update里处理是因为界面会在一次点击里连续调用RemoveFront/Add,
        /// 期间会用点击前拿到的索引访问Objects, 当场重建会让索引失效
        /// </summary>
        protected bool filterDirty;

        public void Start(List<SangoObject> sangoObjects, List<SangoObject> resultList, int limit, Action<List<SangoObject>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            selectLimit = limit;
            Objects = new List<SangoObject>(sangoObjects);
            sureAction = action;
            selected = resultList;
            resultList.RemoveAll(x => x == null);
            displayFilter = null;
            filterDirty = false;
            allObjects = new List<SangoObject>(Objects);
            customSortItems = customSortTitles;
            this.customSortTitleName = cutomSortTitleName;
            GameSystemManager.Instance.Push(this);
        }

        public override void OnExit()
        {
            base.OnExit();
            if (ClickMode)
            {
                selected.Clear();
            }
        }

        public void OnSure()
        {
            sureAction?.Invoke(selected);
            if (!donotFinishThisSystem)
                Back();
        }

        public bool IsPersonLimit()
        {
            return selectLimit <= selected.Count;
        }

        public bool IsPersonEmpty()
        {
            return selected.Count <= 0;
        }

        public void Add(int index)
        {
            if (index < 0 || index >= Objects.Count)
            {
                return;
            }

            if (!selected.Contains(Objects[index]))
            {
                selected.Add(Objects[index]);
                MarkFilterDirty();
            }

            // 点选模式
            if (ClickMode)
            {
                OnSure();
            }
        }

        public void Remove(int index)
        {
            if (index < 0 || index >= Objects.Count) { return; }
            if (selected.Remove(Objects[index]))
            {
                MarkFilterDirty();
            }
        }
        public int RemoveFront()
        {
            if (selected.Count == 0) return -1;
            SangoObject sangoObject = selected[0];
            selected.RemoveAt(0);
            MarkFilterDirty();
            return Objects.IndexOf(sangoObject);
        }

        /// <summary>
        /// 勾选内容发生变化, 下一帧重新应用视图过滤
        /// </summary>
        public void MarkFilterDirty()
        {
            if (displayFilter != null)
                filterDirty = true;
        }

        /// <summary>
        /// 按"调用方的视图过滤 + 搜索关键词"重建显示列表。
        /// 两者都为空（无任何过滤）时**无条件**从完整候选集还原，
        /// 不依赖"数量是否变化"的启发式 —— 否则清空搜索后可能残留过滤态。
        /// </summary>
        public void ApplyDisplayFilter()
        {
            if (allObjects == null)
                allObjects = new List<SangoObject>();

            // 【兜底】完整候选集为空但显示列表里有人：说明快照没被正确建立（异常时序 / 老路径），
            // 直接把当前列表当作完整候选集。没有这一步，搜索会拿空集去过滤 → 列表瞬间空白。
            if (allObjects.Count == 0 && Objects != null && Objects.Count > 0)
                allObjects = new List<SangoObject>(Objects);

            bool hasCallerFilter = displayFilter != null;
            if (!hasCallerFilter && !HasSearchKeyword)
            {
                RestoreAllObjects();
                return;
            }

            List<SangoObject> visible = new List<SangoObject>();
            for (int i = 0; i < allObjects.Count; i++)
            {
                SangoObject obj = allObjects[i];
                if (obj == null)
                    continue;

                // 搜索关键词是**硬过滤**：不命中的一律隐藏，连已勾选项也不例外。
                // 否则"之前勾选过的武将"会混进搜索结果里，玩家看到的就不是"所有含该字的武将"。
                // （要对已勾选项取消勾选时，把搜索框清空即可。）
                if (!MatchSearchKeyword(obj))
                    continue;

                // 调用方的视图过滤：已勾选项始终保留, 否则玩家无法取消勾选
                if (selected.Contains(obj) || !hasCallerFilter || displayFilter(obj))
                    visible.Add(obj);
            }

            Objects.Clear();
            Objects.AddRange(visible);
            RefreshDisplay();
        }

        /// <summary>
        /// 按表头点击排序：完整候选集与当前显示列表一起排序。
        /// 目的是让"清空搜索后"的顺序与刚才看到的一致，不会跳回旧顺序。
        /// </summary>
        /// <param name="comparison">排序比较器（表头 ObjectSortTitle.Sort）</param>
        /// <param name="ascending">是否升序，false 表示反转</param>
        public void SortObjects(System.Comparison<SangoObject> comparison, bool ascending)
        {
            if (comparison == null)
                return;
            if (allObjects != null)
            {
                allObjects.Sort(comparison);
                if (!ascending) allObjects.Reverse();
            }
            if (Objects != null)
            {
                Objects.Sort(comparison);
                if (!ascending) Objects.Reverse();
            }
        }

        /// <summary>
        /// 从完整候选集还原显示列表（无任何过滤条件时使用）。
        /// </summary>
        void RestoreAllObjects()
        {
            if (Objects == null)
                Objects = new List<SangoObject>();
            Objects.Clear();
            Objects.AddRange(allObjects);
            RefreshDisplay();
        }

        /// <summary>
        /// 搜索关键词变化：关键词同样是显示列表的过滤条件，按它重建列表。
        /// </summary>
        protected override void OnSearchKeywordChanged()
        {
            ApplyDisplayFilter();
        }

        /// <summary>
        /// 取消调用方的视图过滤并恢复列表（搜索关键词不受影响，仍按当前关键词过滤）。
        /// </summary>
        public void ClearDisplayFilter()
        {
            displayFilter = null;
            filterDirty = false;
            ApplyDisplayFilter();
        }

        /// <summary>
        /// 仅解除过滤委托而不重绘(供发起方指令结束时清理, 下一次Start会重建全部数据)
        /// </summary>
        public void ReleaseDisplayFilter()
        {
            displayFilter = null;
            filterDirty = false;
        }

        void RefreshDisplay()
        {
            // 优先用窗口自己绑定进来的显示层实例：即便 WindowInterface 尚未赋值（例如
            // 选择窗口的打开路径没有走 ObjectSelectSystem.OnEnter），重绘也不会被静默丢掉 ——
            // 这正是"搜索框输入后列表毫无反应"的根因。
            Sango.UI.UIObjectDisplay display = boundDisplay;
            if (display == null && WindowInterface != null)
                display = WindowInterface.ugui_instance as Sango.UI.UIObjectDisplay;

            if (display != null)
                display.RefreshByFilter();
            else if (WindowInterface != null)
                WindowInterface.Refresh();
        }

        /// <summary>
        /// 进入当前命令的时候触发
        /// </summary>
        public override void OnEnter()
        {
            donotFinishThisSystem = false;
            WindowInterface = Window.Instance.Open("window_object_selector", this);
        }

        public override void Update()
        {
            base.Update();
            if (!filterDirty) return;
            filterDirty = false;
            ApplyDisplayFilter();
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.Close("window_object_selector");
            WindowInterface = Window.Instance.Open("window_object_selector", this);
        }
    }
}
