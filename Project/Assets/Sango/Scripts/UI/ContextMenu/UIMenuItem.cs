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
        /// <para>
        /// 只改颜色与可点性，**不往标题里追加任何说明文案**：
        /// 菜单按钮宽度是固定的，追加"（原因）"会把标题撑出按钮外；
        /// 而且置灰本身已经表达了"当前不可用"。
        /// 不可用原因仍由 <see cref="ContextMenuItem.disabledReason"/> 保存，
        /// 需要时（如悬浮提示）再单独读取，不占用按钮文本。
        /// </para>
        /// </summary>
        /// <param name="b">是否可用</param>
        public UIMenuItem SetValid(bool b)
        {
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
