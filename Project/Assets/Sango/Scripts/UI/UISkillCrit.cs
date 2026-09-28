using Sango.Render;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    public class UISkillCrit : UGUIWindow
    {
        // 暴击图显示组件
        public RawImage critImage;
        // 暴击文本显示组件
        public Text critText;
        // 暴击事件数据
        private TroopSpellSkillCriticalEvent _criticalEvent;

        float safeCloseTime = 4;
        /// <summary>
        /// 打开窗口并传递参数
        /// </summary>
        /// <param name="objects">参数</param>
        public override void Open(params object[] objects)
        {
            base.Open(objects);
            safeCloseTime = 5;
            if (objects != null && objects.Length > 0)
            {
                _criticalEvent = objects[0] as TroopSpellSkillCriticalEvent;
                if (_criticalEvent != null)
                {
                    // 初始化窗口显示
                    InitCritDisplay();
                }
            }
        }

        /// <summary>
        /// 初始化暴击显示
        /// </summary>
        private void InitCritDisplay()
        {
            if (_criticalEvent == null) return;

            // 设置暴击文本
            if (critText != null)
            {
                critText.text = $"暴击！倍数：{_criticalEvent.criticalFactor}x";
            }

            // 设置暴击图
            if (critImage != null && _criticalEvent.troop != null && _criticalEvent.troop.Leader != null)
            {
                // 使用部队首领的 imageID 加载对应的暴击图
                critImage.texture = Sango.Core.GameRenderHelper.LoadCriticalImage(_criticalEvent.troop.Leader.image);
                critImage.SetNativeSize();
            }
        }

        /// <summary>
        /// 刷新窗口显示
        /// </summary>
        public override void Refresh()
        {
            base.Refresh();
            InitCritDisplay();
        }

        public void Update()
        {
            safeCloseTime -= Time.deltaTime;
            if (safeCloseTime <= 0)
            {
                Close();
            }
        }
    }
}