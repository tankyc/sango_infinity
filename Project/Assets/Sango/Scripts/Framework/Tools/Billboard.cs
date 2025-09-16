using UnityEngine;
using System.Collections;

namespace Sango
{
    public class Billboard : MonoBehaviour
    {
        public bool lockY = false;
        public Camera m_Camera;
        private void Start()
        {
            if (m_Camera == null)
                m_Camera = Camera.main;
        }
        void Update()
        {
            if(lockY)
            {
                Vector3 position = m_Camera.transform.position;
                position.y = transform.position.y;
                transform.LookAt(position,Vector3.up);
            }
            else
            {
                transform.LookAt(transform.position + m_Camera.transform.rotation * Vector3.back,
                m_Camera.transform.rotation * Vector3.up);
            }
            

        }
    }
}
