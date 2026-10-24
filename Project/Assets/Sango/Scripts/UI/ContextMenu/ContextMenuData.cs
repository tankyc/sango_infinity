using System;
using System.Collections.Generic;
using UnityEngine;

using Sango.Core; namespace Sango.UI
{
    public class ContextMenuData : IContextMenuData
    {
        public static ContextMenuData MenuData = new ContextMenuData();

        public List<ContextMenuItem> headList = new List<ContextMenuItem>();
        public Vector2 startPosition;

        /// <summary>
        /// order越小越靠前,菜单链父级菜单以最小子级菜单order计算
        /// </summary>
        /// <param name="title"></param>
        /// <param name="order"></param>
        /// <param name="custom"></param>
        /// <param name="action"></param>
        public void Add(string title, int order, object custom, Action<IContextMenuItem> action,
            bool valide = true, string disabledReason = null)
        {
            string[] menuPath = title.Split('/');
            List<ContextMenuItem> checkList = headList;
            ContextMenuItem checkItem;
            for (int i = 0; i < menuPath.Length; ++i)
            {
                string depthTitle = menuPath[i];
                bool isLeaf = i == menuPath.Length - 1;
                checkItem = checkList.Find(x => x.title == depthTitle);
                if (checkItem == null)
                {
                    ContextMenuItem contextMenuItem = new()
                    {
                        title = depthTitle,
                        order = order,
                        depth = i,
                        valid = valide,
                        // 原因只挂在叶子项上：父级是菜单链，自身没有"不可用"的语义
                        disabledReason = isLeaf ? disabledReason : null
                    };
                    if (isLeaf)
                    {
                        contextMenuItem.CustomData = custom;
                        contextMenuItem.action = action;
                    }
                    checkList.Add(contextMenuItem);
                    checkItem = contextMenuItem;
                    checkList = checkItem.children;
                }
                else
                {
                    checkItem.order = Math.Min(order, checkItem.order);
                    if (isLeaf)
                    {
                        // 【叶子项：后注册者覆盖，不做 OR】
                        // 同一路径被两个系统注册时，OR 会让"失效"被"有效"吞掉
                        //（先注册 false 后注册 true → 项变成可点，而原因还留着，二者不同源）。
                        // 叶子是"一个按钮"，最后一次注册的判定才代表它当前的真实状态。
                        // 另外补上 action / CustomData：节点可能先前是作为父级创建的
                        //（那种情况下 `:43-47` 不会赋 action），不补的话点击只会打开空子菜单。
                        checkItem.valid = valide;
                        checkItem.disabledReason = disabledReason;
                        if (action != null)
                            checkItem.action = action;
                        if (custom != null)
                            checkItem.CustomData = custom;
                    }
                    else
                    {
                        // 【父级：任一子项可用即可用】父级本身没有独立判定，
                        // 它的可用性完全由子项决定，因此 OR 是正确的语义。
                        checkItem.valid = checkItem.valid | valide;
                    }
                    checkList = checkItem.children;
                }
            }
        }

        public void Add(string title, int order, object custom)
        {
            Add(title, order, custom, null);
        }
        public void Add(string title, int order)
        {
            Add(title, order, null, null);
        }
        public void Add(string title)
        {
            Add(title, -1, null, null);
        }
        public void AddLine()
        {
            Add(null, -1, null, null);
        }
        public void Clear()
        {
            headList.Clear();
        }

        public bool IsEmpty()
        {
            return headList.Count == 0;
        }
    }
}
