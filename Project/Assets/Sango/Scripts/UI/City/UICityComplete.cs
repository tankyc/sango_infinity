using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
using Sango.Manager;

namespace Sango.UI
{
    /// <summary>
    /// 攻陷城池
    /// </summary>
    public class UICityComplete : UGUIWindow
    {
        public Text cityName;
        public Animation animation;

        public override void OnOpen(params object[] ps)
        {
            base.OnOpen();
            cityName.text = (ps[0] as string);
            animation.Play();
            GameMedia.Instance.PlaySfx(18);
            Invoke("Close", animation.clip.length);
        }
    }
}
