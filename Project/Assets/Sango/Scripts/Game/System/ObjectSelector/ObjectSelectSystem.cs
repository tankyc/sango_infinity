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

        /// <summary>
        /// 未经过滤的完整候选集, 用于取消过滤后恢复显示
        /// </summary>
        protected List<SangoObject> allObjects = new List<SangoObject>();

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
        /// 按当前勾选状态重新生成显示列表
        /// </summary>
        public void ApplyDisplayFilter()
        {
            if (displayFilter == null)
                return;

            // 没有隐藏任何项时, 表头排序改的就是完整候选集, 把最新顺序同步回去
            if (Objects.Count == allObjects.Count)
                allObjects = new List<SangoObject>(Objects);

            List<SangoObject> visible = new List<SangoObject>();
            for (int i = 0; i < allObjects.Count; i++)
            {
                SangoObject obj = allObjects[i];
                // 已勾选的始终保留, 否则玩家无法取消勾选
                if (selected.Contains(obj) || displayFilter(obj))
                    visible.Add(obj);
            }

            Objects.Clear();
            Objects.AddRange(visible);
            RefreshDisplay();
        }

        /// <summary>
        /// 取消视图过滤并恢复完整列表
        /// </summary>
        public void ClearDisplayFilter()
        {
            if (displayFilter == null) return;
            displayFilter = null;
            filterDirty = false;
            if (Objects.Count != allObjects.Count)
            {
                Objects.Clear();
                Objects.AddRange(allObjects);
                RefreshDisplay();
            }
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
            if (WindowInterface == null) return;
            Sango.UI.UIObjectDisplay display = WindowInterface.ugui_instance as Sango.UI.UIObjectDisplay;
            if (display != null)
                display.RefreshByFilter();
            else
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
