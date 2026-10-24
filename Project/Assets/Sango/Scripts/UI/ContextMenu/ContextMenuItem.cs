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
        /// 都会把"为什么不能执行"写进错误文案，玩家点灰按钮能看到原因。
        /// 本项目此前只有 bool，置灰后玩家看不到任何提示。
        /// 现由 <see cref="UIMenuItem.SetValid"/> 把它拼进标题显示。
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
