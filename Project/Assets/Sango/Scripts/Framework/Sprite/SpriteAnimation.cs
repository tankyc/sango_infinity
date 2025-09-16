using UnityEngine;

namespace Sango.Sprite
{

    public class SpriteAnimation : MonoBehaviour
    {
        public UnityEngine.Sprite[] sprites;
        public SpriteRenderer spriteRenderer;
        public float speed;
        private int index = 0;
        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            InvokeRepeating("UpdateRender", speed, speed);
        }

        private void UpdateRender()
        {
            index++;
            if (index >= sprites.Length)
                index = 0;
            spriteRenderer.sprite = sprites[index];
        }
    }
}
