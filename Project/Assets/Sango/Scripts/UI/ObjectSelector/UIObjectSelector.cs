using Sango.Core;
using Sango.Core.Player;
using UnityEngine;
namespace Sango.UI
{
    public class UIObjectSelector : UIObjectDisplay
    {
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
                    for (int j = 0; j < sortItems.Count; j++)
                    {
                        ObjectSortTitle sortTitle = sortItems[j];
                        listItem.Set(j, sortTitle.GetValueStr(sango));
                    }
                    listItem.SetSelected(isSelected);
                }
                else
                {
                    listItem.SetSelected(false);
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
