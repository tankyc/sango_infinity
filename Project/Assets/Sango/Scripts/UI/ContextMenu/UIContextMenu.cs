using System.Collections.Generic;
using UnityEngine;

using Sango.Core;
using Unity.VisualScripting;

namespace Sango.UI
{
    public class UIContextMenu : UGUIWindow
    {
        //public RectTransform[] returnButtons;

        public RectTransform[] menuRoot;
        public UIMenuItem[] menuItem;
        public RectTransform menuLine;
        private List<UIMenuItem>[] nodePool = new List<UIMenuItem>[] { new List<UIMenuItem>(), new List<UIMenuItem>(), new List<UIMenuItem>() };
        //private List<GameObject> linePool = new List<GameObject>();

        private CreatePool<UIMenuItem>[] menuPools;
        private CreatePool<RectTransform> linePool;

        protected override void Awake()
        {
            menuPools = new CreatePool<UIMenuItem>[menuItem.Length];
            for (int i = 0; i < menuItem.Length; i++)
                menuPools[i] = new CreatePool<UIMenuItem>(menuItem[i]);
            linePool = new CreatePool<RectTransform>(menuLine);

#if UNITY_ANDROID || UNITY_IPHONE
            for (int i = 0; i < menuRoot.Length; i++)
            {
                menuRoot[i].localScale = Vector3.one * 1.2f;
            }

            //            for(int i = 0; i < returnButtons.Length; ++i)
            //            {
            //                returnButtons[i].gameObject.SetActive(true);
            //            }
            //#else
            //            for(int i = 0; i < returnButtons.Length; ++i)
            //            {
            //                returnButtons[i].gameObject.SetActive(false);
            //            }
#endif

        }

        public bool IsOverUI(Vector3 point)
        {
            for (int i = 0; i <= showDepth; i++)
            {
                RectTransform root = menuRoot[showDepth];
                if (root != null)
                {
                    if (RectTransformUtility.RectangleContainsScreenPoint(root, point, Game.Instance.UICamera))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private UIMenuItem CreteNode(int index)
        {
            return menuPools[index].Create();
        }
        private void Recycle(int index, UIMenuItem obj)
        {
            menuPools[index].Recycle(obj);
        }

        private RectTransform CreteLine()
        {
            return linePool.Create();
        }
        private void Recycle(int index, RectTransform obj)
        {
            linePool.Recycle(obj);
        }

        public int showDepth = -1;
        public void Show(Vector2 screenPoint, int depth, List<ContextMenuItem> menuItems)
        {
            if (depth == 0)
                GameMedia.Instance.PlayButtonSfx();
            showDepth = depth;
            RectTransform root = menuRoot[showDepth];
            if (root != null)
            {
                root.gameObject.SetActive(true);
                if (depth > 0)
                {
                    UIMenuItem srcObj = menuItem[showDepth - 1];
                    RectTransform rectTransform = srcObj.GetComponent<RectTransform>();
                    screenPoint += new Vector2(rectTransform.sizeDelta.x - 2, 0);
                }
                root.anchoredPosition = screenPoint;
                for (int i = 0; i < menuItems.Count; i++)
                {
                    ContextMenuItem contextMenuData = menuItems[i];
                    if (!string.IsNullOrEmpty(contextMenuData.title))
                    {
                        UIMenuItem obj = CreteNode(showDepth);
                        obj.SetParent(root.transform).SetValid(contextMenuData.valid).SetTitle(contextMenuData.title).SetListener(() =>
                        {
                            contextMenuData.OnClick(obj);
                        });
                    }
                    else
                    {
                        RectTransform obj = CreteLine();
                        obj.SetParent(root.transform, false);
                    }
                }
            }
        }

        public bool Close()
        {
            if (showDepth >= 0)
            {
                RectTransform rectTransform = menuRoot[showDepth];
                int childCount = rectTransform.childCount;
                for (int i = childCount - 1; i >= 0; i--)
                {
                    Transform trns = rectTransform.GetChild(i);
                    if (trns != null && trns.gameObject.activeSelf)
                    {
                        UIMenuItem uIMenuItem = trns.GetComponent<UIMenuItem>();
                        if (uIMenuItem != null)
                        {
                            Recycle(showDepth, uIMenuItem);
                        }
                        else
                        {
                            Recycle(showDepth, trns.GetComponent<RectTransform>());
                        }
                    }
                }
                menuRoot[showDepth].gameObject.SetActive(false);
                showDepth--;
                GameMedia.Instance.PlayCancelSfx();
            }
            return showDepth < 0;
        }

        public bool Close(int toDepth)
        {
            while (showDepth >= toDepth)
                Close();

            return showDepth < 0;
        }

        //public void OnReturn()
        //{
        //    GameController.Instance.OnCancel();
        //}
    }
}
