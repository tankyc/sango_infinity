using Sango.Core;
using Sango.Manager;
using UnityEngine.UI;

namespace Sango.UI
{
    public class UISFXPlay : UGUIWindow
    {
        public int sfxId;
        private void Start()
        {
            Button button = GetComponent<Button>();
            if(button != null)
            {
                button.onClick.AddListener(PlaySFX);
            }
            else
            {
                PlaySFX();
            }
        }

        /// <summary>
        /// 对象销毁时移除监听,避免悬空回调
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
            Button button = GetComponent<Button>();
            if(button != null)
            {
                button.onClick.RemoveListener(PlaySFX);
            }
        }

        void PlaySFX()
        {
            GameMedia.Instance.PlaySfx(sfxId);
        }
    }
}
