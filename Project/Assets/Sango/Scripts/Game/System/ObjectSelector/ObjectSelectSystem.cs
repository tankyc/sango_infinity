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

        public void Start(List<SangoObject> sangoObjects, List<SangoObject> resultList, int limit, Action<List<SangoObject>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            selectLimit = limit;
            Objects = new List<SangoObject>(sangoObjects);
            sureAction = action;
            selected = resultList;
            resultList.RemoveAll(x => x == null);
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
            selected.Remove(Objects[index]);
        }
        public int RemoveFront()
        {
            if (selected.Count == 0) return -1;
            SangoObject sangoObject = selected[0];
            selected.RemoveAt(0);
            return Objects.IndexOf(sangoObject);
        }

        /// <summary>
        /// 进入当前命令的时候触发
        /// </summary>
        public override void OnEnter()
        {
            donotFinishThisSystem = false;
            WindowInterface = Window.Instance.Open("window_object_selector", this);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.Close("window_object_selector");
            WindowInterface = Window.Instance.Open("window_object_selector", this);
        }
    }
}
