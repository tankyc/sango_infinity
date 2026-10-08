using Sango.Core;
using Sango.Core.Player;
using UnityEngine;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectSelector : UIObjectDisplay
    {
        /// <summary>
        /// 搜索框（预制体上的 root/search）。输入关键词后按对象名过滤显示列表。
        /// 允许留空：留空时本窗口不带搜索功能，不影响复用本脚本的其它窗口。
        /// </summary>
        public InputField searchInput;

        RectTransform[] uIObjectListItemsRect;
        ObjectSelectSystem objectSelectSystem;
        bool dragFlag = false;
        UIObjectListItem currentSelectItem;

        protected override void Awake()
        {
            base.Awake();
            uIObjectListItemsRect = new RectTransform[uIObjectListItems.Length];
            for (int i = 0; i < uIObjectListItems.Length; i++)
            {
                uIObjectListItemsRect[i] = uIObjectListItems[i].GetComponent<RectTransform>();
            }
        }

        public override void OnOpen(params object[] objects)
        {
            this.objectSelectSystem = objects[0] as ObjectSelectSystem;
            base.OnOpen(objectSelectSystem);
        }

        /// <summary>
        /// 初始化：基类先把列表 / 滚动条 / 表头准备好，最后接上搜索框。
        /// 每次打开都把搜索状态清零，避免上一次的关键词串到下一次使用。
        /// </summary>
        /// <param name="objectSelectSystem">选择数据源</param>
        public override void Init(ObjectsDisplaySystem objectSelectSystem)
        {
            base.Init(objectSelectSystem);

            // 基类持有的是私有基类引用，这里同步一份强类型引用供搜索回调使用
            this.objectSelectSystem = objectSelectSystem as ObjectSelectSystem;

            // 上一次遗留的关键词在这里被清掉（若确实过滤过，会把列表一并还原）
            if (this.objectSelectSystem != null)
                this.objectSelectSystem.ResetSearchKeyword();

            if (searchInput == null)
                return;

            searchInput.onValueChanged.RemoveListener(OnSearchInputValueChanged);
            searchInput.onValueChanged.AddListener(OnSearchInputValueChanged);
            // onEndEdit 一并兜底：输入法组合结束 / 失焦时的最终文本一定会上报一次
            searchInput.onEndEdit.RemoveListener(OnSearchInputValueChanged);
            searchInput.onEndEdit.AddListener(OnSearchInputValueChanged);
            // 用 WithoutNotify：清空输入框不再回触发一次多余的过滤重建
            searchInput.SetTextWithoutNotify(string.Empty);
        }

        /// <summary>
        /// 搜索框文本变化：把关键词交给选择系统做显示过滤。
        /// </summary>
        /// <param name="keyword">输入框当前文本</param>
        void OnSearchInputValueChanged(string keyword)
        {
            if (objectSelectSystem == null)
                return;
            objectSelectSystem.SetSearchKeyword(keyword);
        }

        /// <summary>
        /// 每帧把搜索框文本对齐给选择系统（兜底）。
        /// 输入法 / 事件系统偶尔会让 onValueChanged、onEndEdit 都收不到最终文本，
        /// 轮询一次可以保证"框里有什么字就按什么字过滤"；文本未变化时是一次字符串比较，开销可忽略。
        /// </summary>
        public override void Update()
        {
            base.Update();

            if (searchInput == null || objectSelectSystem == null)
                return;
            objectSelectSystem.SetSearchKeyword(searchInput.text);
        }

        public override void UpdateItemStartIndex(int startIndex)
        {
            for (int i = 0; i < itemCount; i++)
            {
                UIObjectListItem listItem = uIObjectListItems[i];
                int destIndex = i + startIndex;
                listItem.index = destIndex;
                if (destIndex < objectSelectSystem.Objects.Count)
                {
                    SangoObject sango = objectSelectSystem.Objects[destIndex];
                    bool isSelected = objectSelectSystem.selected.Contains(sango);
                    // 已派去登用的目标武将：画黄色圈标记。
                    // 仅一键登庸系统活跃期间（CityAllRecruit.IsTargetDispatchedQuery 非空）生效，
                    // 配属等其他选择场景不受影响。
                    bool dispatched = sango is Person && CityAllRecruit.IsTargetDispatchedQuery != null
                        && CityAllRecruit.IsTargetDispatchedQuery((Person)sango);
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, sortTitle.GetValueStr(sango));
                    }
                    listItem.SetSelected(isSelected);
                    listItem.SetMark(dispatched);
                }
                else
                {
                    // 超出数据范围的空行(候选不足或已被过滤掉), 必须把上一次的文本清掉
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        listItem.Set(j, "");
                    }
                    listItem.SetSelected(false);
                    listItem.SetMark(false);
                }

            }
        }

        public void OnSure()
        {
            objectSelectSystem.OnSure();
        }

        public void OnPersonListItemPressDown(UIObjectListItem item)
        {
            item.SetPressd(true);
            currentSelectItem = item;
            dragFlag = !item.IsSelected();
        }

        public void OnPersonListItemPressUp(UIObjectListItem item)
        {
            item.SetPressd(false);
            if (Input.GetMouseButtonUp(1))
                return;

            for (int i = 0; i < itemCount; i++)
            {
                RectTransform itemRect = uIObjectListItemsRect[i];
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem == currentSelectItem && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    OnPersonListSelected(item);
                    break;
                }
            }
        }

        public void OnPersonListItemPointEnter(UIObjectListItem item)
        {
            item.SetOver(true);
        }
        public void OnPersonListItemPointExit(UIObjectListItem item)
        {
            item.SetOver(false);
        }

        public void OnPersonListSelected(UIObjectListItem item)
        {
            if (item.index >= objectSelectSystem.Objects.Count)
                return;

            if (!item.IsSelected() && objectSelectSystem.IsPersonLimit())
            {
                int lastIndex = objectSelectSystem.RemoveFront();
                if (lastIndex >= 0)
                {
                    for (int i = 0; i < itemCount; i++)
                    {
                        UIObjectListItem listItem = uIObjectListItems[i];
                        int destIndex = i + startIndex;
                        if (destIndex == lastIndex)
                        {
                            listItem.SetSelected(false);
                            break;
                        }
                    }
                }
                item.SetSelected(true);
                objectSelectSystem.Add(item.index);
                return;
            }

            item.SetSelected(!item.IsSelected());
            if (item.IsSelected())
            {
                objectSelectSystem.Add(item.index);
            }
            else
            {
                objectSelectSystem.Remove(item.index);
            }
        }

        public void OnDragPersonListSelected(UIObjectListItem item)
        {
            if (dragFlag && objectSelectSystem.IsPersonLimit())
                return;

            if (!dragFlag && objectSelectSystem.IsPersonEmpty())
                return;

            if (item.index >= objectSelectSystem.Objects.Count)
                return;

            if (item.IsSelected() && !dragFlag)
            {
                item.SetSelected(false);
                objectSelectSystem.Remove(item.index);
            }
            else if (!item.IsSelected() && dragFlag)
            {
                item.SetSelected(true);
                objectSelectSystem.Add(item.index);
            }

            for (int i = 0; i < itemCount; i++)
            {
                RectTransform itemRect = uIObjectListItemsRect[i];
                UIObjectListItem listItem = uIObjectListItems[i];
                if (listItem != item && RectTransformUtility.RectangleContainsScreenPoint(itemRect, Input.mousePosition, Sango.Core.Game.Instance.UICamera))
                {
                    if (listItem.IsSelected() && !dragFlag)
                    {
                        listItem.SetSelected(false);
                        objectSelectSystem.Remove(listItem.index);
                    }
                    else if (!listItem.IsSelected() && dragFlag)
                    {
                        listItem.SetSelected(true);
                        objectSelectSystem.Add(listItem.index);
                    }
                }
            }
        }

    }
}
