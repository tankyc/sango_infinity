using UnityEngine;
using System.Collections;
using Sango.Render;
using Sango.Game;

namespace Sango
{
    public class BillboardUI : MonoBehaviour
    {
        public Vector3 initScale = new Vector3(-1f, 1f, 1f);
        public Vector2 scaleFactor = Vector2.one;
        private Transform cacheTrans;
        float tempFactor;
        private void Start()
        {
            CatchMainCamera();
        }

        void CatchMainCamera()
        {
            if (cacheTrans == null)
                cacheTrans = Camera.main.transform;
        }

        public void Update()
        {
            CatchMainCamera();
            transform.LookAt(transform.position + cacheTrans.rotation * Vector3.back, cacheTrans.rotation * Vector3.up);
            float factor = MapRender.Instance.mapCamera.cameraDistanceFactor;
            if (factor != tempFactor)
            {
                tempFactor = factor;
                transform.localScale = Vector3.Lerp(initScale * scaleFactor.x, initScale * scaleFactor.y, factor);
            }
        }
    }
}
