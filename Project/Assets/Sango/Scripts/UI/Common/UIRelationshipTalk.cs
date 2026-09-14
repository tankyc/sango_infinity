using Sango.Core;
using System.Collections.Generic;
using static Sango.Core.GameDialog;

namespace Sango.UI
{
    /// <summary>
    /// 仲介(结婚/结义)的图片对话弹窗:
    /// 上方一张事件大图, 下方文字板逐句显示旁白与武将应答,
    /// 点击推进到下一句, 全部说完后再点击即关闭窗口
    ///
    /// OnOpen参数: [0]事件大图路径(string) [1]台词列表(List&lt;TalkData&gt;)
    /// </summary>
    public class UIRelationshipTalk : UIPopBase
    {
        /// <summary>
        /// 每句台词自己播音效, 开窗不播
        /// </summary>
        protected override int DefaultSfx => 0;

        /// <summary>
        /// 对话窗要立刻能点着往下走, 不用等出场动画
        /// </summary>
        protected override bool WaitAnimationBeforeClick => false;

        List<TalkData> talks;
        int index;

        public override void OnOpen(params object[] ps)
        {
            talks = null;
            index = 0;

            string imagePath = null;
            if (ps != null)
            {
                if (ps.Length > 0)
                    imagePath = ps[0] as string;
                if (ps.Length > 1)
                    talks = ps[1] as List<TalkData>;
            }

            // 文案由台词队列逐句填充, 这里先把大图和出场动画处理好
            Show(null, imagePath, 0);
            ShowCurrentTalk();
        }

        /// <summary>
        /// 点击时还有下一句就往下走, 最后一句点完就关闭
        /// </summary>
        protected override bool OnClickMask()
        {
            if (!HasMoreTalk) return false;
            index++;
            ShowCurrentTalk();
            return true;
        }

        /// <summary>
        /// 当前正在显示的这句之后是否还有台词
        /// </summary>
        bool HasMoreTalk
        {
            get { return talks != null && index + 1 < talks.Count; }
        }

        void ShowCurrentTalk()
        {
            if (talks == null || index >= talks.Count)
            {
                SetContent(string.Empty);
                return;
            }

            TalkData data = talks[index];
            SetContent(Format(data));
            if (data.sound > 0)
                GameMedia.Instance.PlaySfx(data.sound);
        }

        /// <summary>
        /// 图上没有头像和名字的位置, 说话人直接写在台词前面
        /// </summary>
        static string Format(TalkData data)
        {
            if (data.person == null)
                return data.text;
            return $"{data.person.Name}：{data.text}";
        }
    }
}
