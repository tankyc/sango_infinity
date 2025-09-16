using UnityEngine;
using System.Collections;
using Sango.Render;

namespace Sango
{
    public class BillboardUI : MonoBehaviour
    {
        public Vector3 initScale = new Vector3(-1f, 1f, 1f);
        public Vector2 scaleFactor = Vector2.one;
        private float curDistance = 1;
        private Vector2 distanceMax = new Vector2(0, 1);
        private Transform cacheTrans;
        bool isInit = false;
        private void Start()
        {
            isInit = false;
            cacheTrans = Camera.main.transform;
        }
        public void Update()
        {
            if(cacheTrans == null)
            {
                cacheTrans = Camera.main.transform;
            }

            transform.LookAt(transform.position + cacheTrans.rotation * Vector3.back, cacheTrans.rotation * Vector3.up);
            float factor = 1;
            MapRender map = MapRender.Instance;
            if (map != null)
            {
                factor = (map.mapCamera.cur_distance -
                    map.mapCamera.limitDistance.x) / (map.mapCamera.limitDistance.y - map.mapCamera.limitDistance.x);
                transform.localScale = Vector3.Lerp(initScale * scaleFactor.x, initScale * scaleFactor.y, factor);
            }
        }
    }
}
