using UnityEngine;
using UnityEngine.UI;

namespace Sango.Core.Player
{

    public class UIImageAnimation : MonoBehaviour
    {
        public UnityEngine.Sprite[] sprites;
        public Image image;
        public float speed;
        float curTime;
        private int index = 0;
        private void OnEnable()
        {
            image = GetComponent<Image>();
        }

        private void UpdateRender()
        {
            index++;
            if (index >= sprites.Length)
                index = 0;
            if (image != null)
            {
                UnityEngine.Sprite spr = sprites[index];
                image.enabled = (spr != null);
                image.sprite = spr;
            }
        }

        private void Update()
        {
            if (image != null)
            {
                curTime += Time.deltaTime;
                if (curTime > speed)
                {
                    curTime = 0;
                    UpdateRender();
                }
            }
        }
    }
}
