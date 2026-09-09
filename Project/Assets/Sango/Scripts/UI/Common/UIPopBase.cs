using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 通用大图事件弹窗基类: 半透明遮罩 + 一张事件插图 + 一段文案, 点击任意处关闭
    ///
    /// OnOpen参数约定:
    /// [0] 文案(string)
    /// [1] 事件大图路径(string), 为空则用预制体自带的图
    /// [2] 音效id(int), 传0表示不播, 不传则用DefaultSfx
    ///
    /// 子类只想改文案来源/点击行为时, 可以重写OnOpen并直接调用Show(text, image, sfx)
    /// </summary>
    public abstract class UIPopBase : UGUIWindow
    {
        /// <summary>
        /// 文案节点
        /// </summary>
        public Text content;

        /// <summary>
        /// 出场动画
        /// </summary>
        public Animation animation;

        /// <summary>
        /// 背景大图所在的节点路径, 预制体结构不同时由子类重写
        /// </summary>
        protected virtual string ImageNodePath => "root/bg";

        /// <summary>
        /// 调用方没有传音效时使用的默认音效
        /// </summary>
        protected virtual int DefaultSfx => 44;

        /// <summary>
        /// 是否要等出场动画播完才响应点击
        /// </summary>
        protected virtual bool WaitAnimationBeforeClick => true;

        bool canClose;
        Image bgImage;
        UnityEngine.Sprite defaultSprite;

        /// <summary>
        /// 当前是否已经可以接受点击
        /// </summary>
        protected bool CanClick => canClose;

        public override void OnOpen(params object[] ps)
        {
            base.OnOpen();

            string text = null;
            string imagePath = null;
            int sfx = DefaultSfx;
            if (ps != null)
            {
                if (ps.Length > 0)
                    text = ps[0] as string;
                if (ps.Length > 1)
                    imagePath = ps[1] as string;
                if (ps.Length > 2 && ps[2] is int soundId)
                    sfx = soundId;
            }

            Show(text, imagePath, sfx);
        }

        /// <summary>
        /// 显示文案与大图, 播放出场动画和音效
        /// </summary>
        protected virtual void Show(string text, string imagePath, int sfx)
        {
            canClose = false;
            SetContent(text);

            // 窗口实例会被反复复用, 每次都要把图设回本次事件该有的那张
            SetBgImage(imagePath);

            if (animation != null)
            {
                animation.Play();
                if (WaitAnimationBeforeClick)
                    Invoke(nameof(EnableClose), animation.clip.length + 2);
                else
                    EnableClose();
            }
            else
            {
                EnableClose();
            }

            if (sfx > 0)
                GameMedia.Instance.PlaySfx(sfx);
        }

        /// <summary>
        /// 设置文案
        /// </summary>
        protected void SetContent(string text)
        {
            if (content != null)
                content.text = text;
        }

        /// <summary>
        /// 换掉事件大图, 路径为空则恢复预制体自带的图
        /// </summary>
        protected void SetBgImage(string imagePath)
        {
            Image image = GetBgImage();
            if (image == null) return;

            if (defaultSprite == null)
                defaultSprite = image.sprite;

            UnityEngine.Sprite sprite = string.IsNullOrEmpty(imagePath)
                ? null
                : Sango.Loader.ObjectLoader.LoadObject<UnityEngine.Sprite>(imagePath);
            if (sprite == null && !string.IsNullOrEmpty(imagePath))
                Log.Warning($"{name}: 无法加载事件大图 {imagePath}");
            image.sprite = sprite != null ? sprite : defaultSprite;
        }

        /// <summary>
        /// 取背景大图组件, 找不到节点时返回null(只影响换图, 不影响弹窗)
        /// </summary>
        protected Image GetBgImage()
        {
            if (bgImage == null)
            {
                Transform node = transform.Find(ImageNodePath);
                if (node != null)
                    bgImage = node.GetComponent<Image>();
            }
            return bgImage;
        }

        /// <summary>
        /// 允许接受点击
        /// </summary>
        protected virtual void EnableClose()
        {
            canClose = true;
            if (ShouldAutoClose())
                Close();
        }

        /// <summary>
        /// 出场动画播完后是否不用等玩家点击就自动关闭
        /// </summary>
        protected virtual bool ShouldAutoClose()
        {
            return false;
        }

        /// <summary>
        /// 遮罩上的按钮绑定本方法
        /// </summary>
        public void ClickClose()
        {
            if (!canClose) return;
            // 子类还有内容要展示时, 本次点击只消费不掉关闭
            if (OnClickMask()) return;
            Close();
        }

        /// <summary>
        /// 点击遮罩时先给子类处理的机会, 返回true表示本次点击已被消费(不关闭窗口)
        /// </summary>
        protected virtual bool OnClickMask()
        {
            return false;
        }
    }
}
