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

        void PlaySFX()
        {
            GameMedia.Instance.PlaySfx(sfxId);
        }
    }
}
