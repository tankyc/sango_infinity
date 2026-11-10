using Sango.Render;
using UnityEngine;

namespace Sango.UI
{
    public class BillboardUI : MonoBehaviour
    {
        public Vector3 initScale = new Vector3(1f, 1f, 1f);
        public Vector2 scaleFactor = Vector2.one;
        public Vector3 offsetFactor = new Vector3(0f, 0f, 0f);
        public Vector3 cacheOffset = new Vector3(0f, 0f, 0f);
        private Transform cacheTrans;
        float tempFactor;

        /// <summary>上一次写入朝向时的自身位置（LookAt 的输出只取决于它和相机朝向）</summary>
        Vector3 lastLookPos;

        /// <summary>上一次写入朝向时的相机朝向</summary>
        Quaternion lastLookRot;

        /// <summary>是否已经写过一次朝向（首个有效帧必须强制写）</summary>
        bool hasLookAt;

        /// <summary>
        /// 写入 billboard 朝向。
        ///
        /// 【为什么要判脏】挂本组件的对象基本都是 World Space Canvas（头顶条、地图标注）。
        /// 每写一次 <c>transform.rotation</c>，所属 Canvas 就会被标脏并重算批次；
        /// 相机静止时这就是纯浪费——地图一停，帧率还在往下掉，多半是它在作怪。
        /// LookAt 的结果只由"自身位置"和"相机朝向"决定，这两者不变就完全不用重写。
        /// </summary>
        void ApplyLookAt()
        {
            Vector3 selfPos = transform.position;
            Quaternion camRot = cacheTrans.rotation;

            if (hasLookAt && selfPos == lastLookPos && camRot == lastLookRot)
                return;

            hasLookAt = true;
            lastLookPos = selfPos;
            lastLookRot = camRot;

            transform.LookAt(selfPos + camRot * Vector3.back, camRot * Vector3.up);
        }


        private void Start()
        {
            CatchMainCamera();
            tempFactor = -1;
        }

        private void OnDisable()
        {
            tempFactor = -1;
            hasLookAt = false;
        }
        private void OnEnable()
        {
            tempFactor = -1;
            hasLookAt = false;
        }
        void CatchMainCamera()
        {
            if (cacheTrans == null)
                cacheTrans = Camera.main.transform;
        }

        public void Update()
        {
            CatchMainCamera();
            // 【空引用防御】切场景 / 域重载的瞬间，或相机尚未创建时，
            // cacheTrans 或 MapRender.mapCamera 都可能为 null。
            // 原写法每帧直接解引用 → 满屏 NullReferenceException（每帧一条）。
            if (cacheTrans == null)
                return;

            ApplyLookAt();

            MapRender mapRender = MapRender.Instance;
            if (mapRender == null || mapRender.mapCamera == null)
                return;

            float factor = mapRender.mapCamera.cameraDistanceFactor;
            if (factor != tempFactor)
            {
                tempFactor = factor;
                transform.localScale = Vector3.Lerp(initScale * scaleFactor.x, initScale * scaleFactor.y, factor);
                transform.localPosition = cacheOffset + Vector3.Lerp(offsetFactor, Vector3.zero, factor);
            }
        }
    }
}
