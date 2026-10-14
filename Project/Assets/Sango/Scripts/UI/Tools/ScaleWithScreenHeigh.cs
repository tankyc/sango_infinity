using UnityEngine;

namespace Sango.Core.Player
{

    public class ScaleWithScreenHeigh : MonoBehaviour
    {
        public bool scaleable = false;
        public bool scaleable_new = false;
        private void OnEnable()
        {
            if (scaleable_new)
            {
                float s = Game.Instance.CanvasScalerFactor;
                if (s < 1)
                    transform.localScale = new Vector3(s, s, s);
            }
        }
    }
}
