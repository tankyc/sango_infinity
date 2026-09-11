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
                }
            }
        }
    }
}
