using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力研究完成强提示窗口（<c>window_ability_complete</c>）。
    ///
    /// 与技法研究完成（<see cref="UITechniqueComplete"/> / <c>window_technique_complete</c>）同一套套路：
    /// 由 <c>RenderEvent.WindowEvent</c> 打开，播一遍"研究完成"动画，播完自动关闭，
    /// 关闭时回调 <see cref="UGUIWindow.OnCloseAction"/>，RenderEvent 才继续往下走。
    ///
    /// 【美术接入清单】在 prefab 根节点挂本脚本（参数为 <see cref="Ability"/>）：
    /// <code>
    /// 字段名        控件类型    用途
    /// ──────────────────────────────────────────────
    /// nameLabel     Text       能力名（如「统+5中」）
    /// kindLabel     Text       分类（基础 / 适性 / 特技）
    /// descLabel     Text       能力说明（可为空）
    /// animation     Animation  入场动画，留空则按固定时长停留后关闭
    /// </code>
    /// </summary>
    public class UIAbilityComplete : UGUIWindow
    {
        /// <summary>能力名</summary>
        public Text nameLabel;

        /// <summary>分类（基础 / 适性 / 特技）</summary>
        public Text kindLabel;

        /// <summary>能力说明</summary>
        public Text descLabel;

        /// <summary>入场动画（留空则用兜底时长）</summary>
        public Animation animation;

        /// <summary>没有动画时的停留时长（秒）</summary>
        const float FallbackSeconds = 2.5f;

        public override void OnOpen(params object[] ps)
        {
            base.OnOpen(ps);

            Ability ability = ps != null && ps.Length > 0 ? ps[0] as Ability : null;
            if (nameLabel != null)
                nameLabel.text = ability != null ? ability.Name : "新能力";
            if (kindLabel != null)
                kindLabel.text = ability != null ? AbilityTreeBuilder.GetCategoryName(ability) : string.Empty;
            if (descLabel != null)
                descLabel.text = ability != null ? ability.desc : string.Empty;

            // 动画播完自动关闭；窗口关闭会触发 OnCloseAction，RenderEvent.WindowEvent 据此结束
            float seconds = FallbackSeconds;
            if (animation != null && animation.clip != null)
            {
                animation.Play();
                seconds = animation.clip.length;
            }

            // 窗口可能被复用：先撤掉上一次排的关闭，避免提前关掉这一次的表演
            CancelInvoke("Close");
            Invoke("Close", seconds);
        }
    }
}
