using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力培育列表中的一行（<see cref="UICityAbilityLearn"/> 的列表项模板）。
    ///
    /// 【美术接入清单】在列表项 prefab 上挂本脚本，并按字段名拖控件：
    /// <code>
    /// 字段名         控件类型     用途
    /// ──────────────────────────────────────
    /// nameLabel      Text         能力名（如「统率·上」）
    /// descLabel      Text         能力描述
    /// button         Button       整行点击
    /// selectedMark   GameObject   选中态高亮（可为空）
    /// </code>
    /// </summary>
    public class UIAbilityStudyItem : MonoBehaviour
    {
        /// <summary>能力名</summary>
        public Text nameLabel;

        /// <summary>能力描述</summary>
        public Text descLabel;

        /// <summary>整行点击按钮</summary>
        public Button button;

        /// <summary>选中态高亮（可为空）</summary>
        public GameObject selectedMark;

        /// <summary>本行代表的能力</summary>
        public Ability Ability { get; private set; }

        /// <summary>所属窗口</summary>
        UICityAbilityLearn owner;

        /// <summary>
        /// 绑定数据（每个实例只调一次）。
        /// </summary>
        /// <param name="owner">所属窗口</param>
        /// <param name="ability">能力</param>
        public void Setup(UICityAbilityLearn owner, Ability ability)
        {
            this.owner = owner;
            Ability = ability;

            if (nameLabel != null)
                nameLabel.text = ability != null ? ability.Name : string.Empty;
            if (descLabel != null)
                descLabel.text = ability != null ? ability.desc : string.Empty;

            SetSelected(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        /// <summary>点击整行 → 通知窗口</summary>
        void OnClick()
        {
            if (owner != null && Ability != null)
                owner.OnSelectAbility(Ability);
        }

        /// <summary>
        /// 设置选中态。
        /// </summary>
        /// <param name="selected">是否选中</param>
        public void SetSelected(bool selected)
        {
            if (selectedMark != null)
                selectedMark.SetActive(selected);
            // 选中项禁用按钮，避免重复点击造成"再次发起"的错觉
            if (button != null)
                button.interactable = !selected;
        }
    }
}
