using UnityEngine;
using System.Collections;
using Sango.Render;

namespace Sango
{
    public class TerrainBillboard : MonoBehaviour
    {
        public Camera m_Camera;
        private bool updateTerrain = false;
        private bool init = false;
        private Transform cacheCameraTrans;
        private void Start()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
            cacheCameraTrans = m_Camera.transform;
        }

        private void OnEnable()
        {
            init = false;
        }

        void Update()
        {
            Vector3 pos = transform.position;
            if(!init || updateTerrain)
            {
                float height;
                if (MapRender.QueryHeight(pos, out height))
                {
                    init = true;
                    pos.y = height + 2.5f;
                    transform.position = pos;
                }
            }
            
            transform.LookAt(pos + cacheCameraTrans.rotation * Vector3.back,
                cacheCameraTrans.rotation * Vector3.up);

        }
    }
}
