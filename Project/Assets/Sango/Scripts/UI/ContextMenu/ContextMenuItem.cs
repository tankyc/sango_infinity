using System;
using System.Collections.Generic;
using UnityEngine;

using Sango.Core; namespace Sango.UI
{
    public class ContextMenuItem : IContextMenuItem
    {
        public string title;
        public int order;
        public int depth;
        public Action<ContextMenuItem> action;
        public object CustomData { get; set; }
        public List<ContextMenuItem> children = new List<ContextMenuItem>();
        public bool valid;
        /// <summary>
        /// 置灰原因（仅 `valid == false` 时有意义）。
        ///
        /// 对齐 Open311 `set_command_error_text`：原版每条 `can_*_command` 在返回 false 前
        /// 都会把"为什么不能执行"写进错误文案。
        /// <para>
        /// **本字段不参与按钮文本**：菜单按钮宽度固定，早前由 <c>UIMenuItem.SetValid</c>
        /// 拼成「出征（出阵位已满）」会把标题撑出按钮外，已去掉该拼接。
        /// 这里只保留数据，供将来的悬浮提示 / 调试面板读取。
        /// </para>
        /// </summary>
        public string disabledReason;
        public UIMenuItem targetItem;

        public void SetTitle(string t)
        {
            if (targetItem == null) return;
            targetItem.SetTitle(t);
        }

        public void OnClick(UIMenuItem item)
        {
            targetItem = item;
            if (action == null)
            {
                GameMedia.Instance.PlayMenuClickSfx();
                RectTransform rect = item.GetComponent<RectTransform>();
                Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(Game.Instance.UICamera, rect.position);
                ContextMenu.Show(children, screenPos);
                return;
            }
            GameMedia.Instance.PlaySubMenuClickSfx();
            action.Invoke(this);
        }
    }
}
