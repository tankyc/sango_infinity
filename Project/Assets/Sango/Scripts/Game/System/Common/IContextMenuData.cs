using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    public interface IContextMenuData
    {
        /// <summary>
        /// order越小越靠前,菜单链父级菜单以最小子级菜单order计算
        /// </summary>
        /// <param name="title"></param>
        /// <param name="order"></param>
        /// <param name="custom"></param>
        /// <param name="action"></param>
        /// <summary>
        /// 添加一个菜单项。
        /// </summary>
        /// <param name="title">标题（含父级路径，'/'-分隔）</param>
        /// <param name="order">排序值</param>
        /// <param name="custom">自定义数据</param>
        /// <param name="action">点击回调</param>
        /// <param name="valide">是否可用；false 时按钮置灰不可点</param>
        /// <param name="disabledReason">
        /// 置灰原因（可选）。非空时会在按钮标题后追加「（原因）」，
        /// 对应 Open311 的 `set_command_error_text` —— 让玩家知道为什么点不了。
        /// </param>
        public void Add(string title, int order, object custom, Action<IContextMenuItem> action,
            bool valide = true, string disabledReason = null);
        public void Add(string title, int order, object custom);
        public void Add(string title, int order);
        public void Add(string title);
        public void AddLine();
        public void Clear();
        public bool IsEmpty();
    }
}
