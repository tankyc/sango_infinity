using UnityEngine;
using System.Collections;

namespace Sango
{
    public class PoolLife : MonoBehaviour
    {
        public float life;
        void Update()
        {
            if (life > 0)
            {
                life -= Time.deltaTime;
                if (life <= 0)
                {
                    PoolManager.Recycle(gameObject);
                    enabled = false;
                }
            }
        }

        public static void AutoRelease(GameObject gameObject, float time)
        {
            PoolLife poolLife = gameObject.GetComponent<PoolLife>();
            if (poolLife == null)
                poolLife = gameObject.AddComponent<PoolLife>();
            poolLife.life = time;
            poolLife.enabled = true;
        }
    }
}
