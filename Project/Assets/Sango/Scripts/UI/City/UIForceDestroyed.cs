using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 攻陷城池
    /// </summary>
    public class UIForceDestroyed : UGUIWindow
    {
        public Text cityName;
        public Animation animation;
        bool canClose = false;
        public override void OnOpen(params object[] ps)
        {
            base.OnOpen();
            canClose = false;
            cityName.text = (ps[0] as string);
            animation.Play();
            GameMedia.Instance.PlaySfx(44);
            Invoke("EnableClose", animation.clip.length + 2);
        }

        void EnableClose()
        {
            canClose = true;
            bool hasPlayer = false;
            Scenario.Cur.forceSet.ForEach(f =>
            {
                if (f.IsPlayer)
                    hasPlayer = true;
            });

            if(!hasPlayer)
            {
                Close();
            }
        }

        public void ClickClose()
        {
            if (canClose)
            {
                Close();
            }
        }

    }
}
