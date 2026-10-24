using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMenuItem : MonoBehaviour
    {
        public Text title;
        public Button button;

        public UIMenuItem SetTitle(string t)
        {
            title.text = t;
            return this;
        }

        /// <summary>
        /// 设置可用状态；不可用时置灰、禁用点击。
        /// </summary>
        /// <param name="b">是否可用</param>
        /// <param name="reason">
        /// 置灰原因（可选）。非空时追加到标题末尾，形如「出征（出阵位已满）」，
        /// 对应 Open311 的 `set_command_error_text` —— 让玩家知道为什么点不了。
        /// <para>**必须在 <see cref="SetTitle"/> 之后调用**，否则拼不到原标题上。</para>
        /// </param>
        public UIMenuItem SetValid(bool b, string reason = null)
        {
            if (!b && !string.IsNullOrEmpty(reason))
                title.text = $"{title.text}（{reason}）";
            title.color = b ? Color.white : Color.gray;
            button.interactable = b;
            return this;
        }

        public UIMenuItem SetParent(Transform p)
        {
            transform.SetParent(p, false);
            transform.SetAsLastSibling();
            return this;
        }

        public UIMenuItem SetListener(UnityAction call)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(call);
            return this;
        }
    }
}
