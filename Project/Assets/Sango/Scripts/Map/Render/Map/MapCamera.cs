using Sango.Tools;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sango.Render
{
    // 雾效
    public class MapCamera : MapProperty
    {
        Transform lookAt;
        Transform transform;
        public Camera camera;

        public float fov = 30f;
        public float near_clip = 0.3f;
        public float far_clip = 950;
        public float cameraDistanceFactor = 1;

        public Vector3 look_position = new Vector3(1407, 0, 796);
        public Vector2 limitDistance = new Vector2(200f, 630f);
        public Vector2 limitAngle = new Vector2(22.5f, 70f);
        public float cur_distance = 300f;
        public Vector3 look_rotate = new Vector3(45f, -90f, 0f);
        public float zoomSpeed = 30;
        public float keyBoardMoveSpeed = 300f;
        public float rotSpeed = 0.1f;

        public float safeBoder = 560f;

        bool changed = false;

        /// <summary>
        /// 上一次写入 URP 资产的阴影距离（&lt;0 表示尚未写入过）。
        ///
        /// URP 资产是**全局共享**对象，改它的 <c>shadowDistance</c> 会让 URP 重新计算所有相机的
        /// 阴影参数，属于全局副作用。旧实现只要相机有变化就无条件写入（哪怕算出来的值和上次一样），
        /// 在连续拖拽 / 缩放的帧里会反复触发。这里缓存后判脏：只有目标值发生实质变化才写。
        /// </summary>
        float lastShadowDistance = -1f;

        /// <summary>
        /// 上一次应用视距参数时的镜头距离（&lt;0 表示尚未应用过），用于判脏。
        ///
        /// 【为什么需要判脏】雾效 / 远裁剪面必须永远跟着 <see cref="cur_distance"/> 走，
        /// 但改距离的路径不止滚轮：顶视图缓动走 <see cref="SetPoseRaw"/>，
        /// 读档 / 剧本定位会直接赋 <c>cur_distance</c>，都不经过 <see cref="ZoomCamera"/>。
        /// 只在 ZoomCamera 里调 <see cref="ApplyViewDistance"/> 会漏掉这些路径 ——
        /// 表现就是"读档后镜头在远景，雾还是近景的浓度、远裁剪面还是近景的值"。
        /// </summary>
        float lastViewDistance = -1f;

        /// <summary>强制下一帧重算视距参数（地图重建 / 雾参数被整份覆盖后置位）</summary>
        bool viewDistanceDirty = true;

        /// <summary>
        /// 视距参数的基准镜头距离，等于 <see cref="cur_distance"/> 的默认值。
        ///
        /// 【口径】距离不超过它时 factor 恒为 1 —— 近景完全保持美术调好的雾效与裁剪面；
        /// 超过后按 <c>distance / 基准</c> 线性放大。
        /// 旧写法是 <c>Math.Max(630, distance) / 630</c>，在 200~630 的常规缩放区间里恒等于 1，
        /// 等于"永远不放大"：拉到 630 时远处地形会被 fogEnd(1068) 糊成纯白、被 farClip(950) 切掉。
        /// </summary>
        public float viewDistanceBase = 300f;

        public MapCamera(MapRender map) : base(map)
        {
            viewPlane = new Plane(Vector3.up, Vector3.zero);
        }

        private Plane viewPlane;
        private Vector3[] corners = new Vector3[4];
        public bool GetViewRect(float limitLen, out float x, out float y, out float w, out float h)
        {
            if (CameraPlaneView.GetPlaneCorners(ref viewPlane, camera, limitLen, ref corners))
            {
                Vector3 min = camera.transform.position;
                Vector3 max = min;
                for (int i = 0; i < corners.Length; ++i)
                {
                    Vector3 c = corners[i];
                    min.x = Mathf.Min(min.x, c.x);
                    min.z = Mathf.Min(min.z, c.z);
                    max.x = Mathf.Max(max.x, c.x);
                    max.z = Mathf.Max(max.z, c.z);
                }
                x = min.z;
                y = min.x;
                w = max.z - min.z;
                h = max.x - min.x;
                return true;
            }
            x = 0; y = 0; w = 0; h = 0;
            return false;
        }
        public override void Init()
        {
            base.Init();

            camera = Camera.main;
            if (camera == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camera = camObj.AddComponent<Camera>();
            }
            transform = camera.transform;
            camera.fieldOfView = fov;
            camera.nearClipPlane = near_clip;
            if (!MapEditor.IsEditOn)
                camera.farClipPlane = far_clip;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.clearFlags = CameraClearFlags.Skybox;

            // if (lookAt == null) {
            lookAt = new GameObject("lookAt").transform;
            //  }

            lookAt.position = look_position;

            enabled = true;
            NeedUpdateCamera();
        }

        public void MoveCameraTo(Vector3 pos)
        {
            // 顶视图下"定位到某处"说明玩家想离开顶视图：立即结束（不缓动）再移动，
            // 否则顶视图的缓动会在同一帧把位置改回去，表现为"点了没反应"
            if (map != null && map.mapTopView != null && map.mapTopView.isActive)
                map.mapTopView.Cancel();
            position = pos;
        }

        /// <summary>
        /// 输入拦截器：返回 true 表示本次镜头输入已被接管（顶视图用），各输入入口应立即 return。
        ///
        /// 【为什么用回调而不是 bool 字段】顶视图需要"一有输入就开始恢复，同时吞掉这次输入"，
        /// 判断与副作用必须在同一处完成；散在 MapCamera 里会变成两套状态容易不同步。
        /// </summary>
        public Func<bool> interceptInput;

        /// <summary>是否由外部（顶视图）接管本次输入。</summary>
        private bool InterceptInput()
        {
            return interceptInput != null && interceptInput();
        }

        /// <summary>
        /// 直接写入相机姿态，跳过 <see cref="position"/> 的 safeBoder 边界钳制。
        ///
        /// 【为什么需要】顶视图要把观察点放到地图正中心，而 position 的 setter 会把观察点钳在
        /// [safeBoder, world - safeBoder]；小地图（边长 &lt; 2×safeBoder）时中心会被推偏，顶视图就装不满屏幕。
        /// 缓动每帧也走这里，避免插值中途被钳制打断。
        /// </summary>
        /// <param name="lookPos">观察点世界坐标</param>
        /// <param name="dist">相机到观察点的距离</param>
        /// <param name="rot">欧拉角（x = 俯仰，y = 朝向）</param>
        public void SetPoseRaw(Vector3 lookPos, float dist, Vector3 rot)
        {
            look_position = lookPos;
            if (lookAt != null)
                lookAt.position = lookPos;
            cur_distance = dist;
            look_rotate = rot;
            NeedUpdateCamera();
        }

        /// <summary>读取当前姿态快照（顶视图用来记录 / 恢复）。</summary>
        public void GetPose(out Vector3 lookPos, out float dist, out Vector3 rot)
        {
            lookPos = look_position;
            dist = cur_distance;
            rot = look_rotate;
        }

        public override void Clear()
        {
            base.Clear();
            if (lookAt != null)
            {
                GameObject.Destroy(lookAt.gameObject);
            }
        }

        internal override void OnSave(BinaryWriter writer)
        {
            //writer.Write(fov);
            //writer.Write(near_clip);
            //writer.Write(far_clip);
            //writer.Write(look_position.x);
            //writer.Write(look_position.y);
            //writer.Write(look_position.z);
            //writer.Write(limitDistance.x);
            //writer.Write(limitDistance.y);
            //writer.Write(limitAngle.x);
            //writer.Write(limitAngle.y);
            //writer.Write(cur_distance);
            //writer.Write(look_rotate.x);
            //writer.Write(look_rotate.y);
            //writer.Write(look_rotate.z);
            //writer.Write(zoomSpeed);
            //writer.Write(keyBoardMoveSpeed);
            //writer.Write(rotSpeed);
            writer.Write(safeBoder);
        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            if (versionCode <= 2)
            {
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
                reader.ReadSingle();
            }

            if (versionCode >= 9)
            {
                safeBoder = reader.ReadSingle();
            }

            //fov = reader.ReadSingle();
            //near_clip = reader.ReadSingle();
            //far_clip = reader.ReadSingle();

            //position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            //limitDistance = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            //limitAngle = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            //cur_distance = reader.ReadSingle();
            //lookRotate = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            //zoomSpeed = reader.ReadSingle();
            //keyBoardMoveSpeed = reader.ReadSingle();
            //rotSpeed = reader.ReadSingle();

        }

        public bool enabled
        {
            get; set;
        }
        public Vector3 position
        {
            get { return look_position; }
            set
            {
                if (value.x < safeBoder)
                    value.x = safeBoder;
                if (value.z < safeBoder)
                    value.z = safeBoder;
                if (value.x > map.mapData.world_height - safeBoder)
                    value.x = map.mapData.world_height - safeBoder;
                if (value.z > map.mapData.world_width - safeBoder)
                    value.z = map.mapData.world_width - safeBoder;

                look_position = value;
                lookAt.position = look_position;
                NeedUpdateCamera();
            }
        }

        public float distance
        {
            get { return cur_distance; }
            set
            {
                cur_distance = value;
                NeedUpdateCamera();
            }
        }

        public Vector3 lookRotate
        {
            get { return look_rotate; }
            set
            {
                look_rotate = value;
                NeedUpdateCamera();
            }
        }

        public Transform GetCenterTransform()
        {
            return lookAt;
        }


        public void MoveCamera(int dir, float speed)
        {
            if (InterceptInput()) return;

            if (dir == 0)
            {
                position += -transform.right * speed * Time.deltaTime; ;
            }
            else if (dir == 1)
            {
                position += transform.right * speed * Time.deltaTime; ;
            }
            else if (dir == 2)
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * speed * Time.deltaTime; ;
            }
            else if (dir == 3)
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * -speed * Time.deltaTime; ;
            }
        }

        public void ZoomCamera(float delta)
        {
            if (InterceptInput()) return;

            float near = limitDistance.x;
            float far = limitDistance.y;
            if (MapEditor.IsEditOn)
            {
                near = 0.1f;
                far = 3000f;
            }
            distance -= delta * zoomSpeed;
            if (distance < near)
                distance = near;
            else if (distance > far)
                distance = far;

            cameraDistanceFactor = (cur_distance - near) / (far - near);

            // 根据当前镜头距离动态调整雾效等视距参数
            ApplyViewDistance();

        }

        /// <summary>
        /// 根据当前镜头距离动态调整雾效起始/结束距离、远裁剪面、天空盒缩放等视距参数。
        /// 切换季节或修改雾效参数后会写入原始值,需要重新调用本方法按视距修正。
        /// </summary>
        public void ApplyViewDistance()
        {
            // 先记状态再算：即使下面因为对象缺失提前返回，也不要在下一帧反复重试同一个距离
            viewDistanceDirty = false;
            lastViewDistance = cur_distance;

            if (map == null)
                return;

            float baseDistance = viewDistanceBase > 0.01f ? viewDistanceBase : 300f;
            float factor = Mathf.Max(1f, distance / baseDistance);

            map.showLimitLength = factor * 1100;

            // 雾效：按"基准值 × factor + 额外视距"放大，保证远景不再被 fogEnd 糊白
            if (map.mapFog != null)
            {
                UnityEngine.RenderSettings.fogStartDistance = factor * map.mapFog.fogStart + 300 * (factor - 1);
                UnityEngine.RenderSettings.fogEndDistance = factor * map.mapFog.fogEnd + 500 * (factor - 1);
            }

            // 【必须写 camera 字段，不能写 Camera.main】
            // Init 里有"Camera.main 为空就自己 new 一个相机"的兜底，而 new 出来的相机没有
            // MainCamera 标签，此时 Camera.main 依然是 null —— 旧写法会把远裁剪面写到
            // 一个不存在 / 不相干的相机上，表现就是"远景时地形被远裁剪面切掉"。
            Camera cam = camera != null ? camera : Camera.main;
            if (cam != null)
                cam.farClipPlane = factor * far_clip + 300 * (factor - 1);

            if (map.mapSkyBox != null && map.mapSkyBox.skyTrans != null)
                map.mapSkyBox.skyTrans.localScale = Vector3.one * factor;
        }

        /// <summary>
        /// 标记视距参数需要重算（下一帧 <see cref="Update"/> 生效）。
        /// 用于地图重建 / 雾参数被整份覆盖之后 —— 此时距离可能没变，靠距离判脏抓不到。
        /// </summary>
        public void NeedUpdateViewDistance()
        {
            viewDistanceDirty = true;
        }

        public void OffsetCamera(Vector3 offset)
        {
            if (InterceptInput()) return;
            position += offset;
        }

        public void RotateCamera(Vector2 offset)
        {
            if (InterceptInput()) return;

            float angleX = offset.x * rotSpeed;
            float angleY = offset.y * rotSpeed;
            //Debug.Log(string.Format("angleX:{0} angleY:{1} Time.deltaTime{2}", angleX, angleY, Time.deltaTime));
            float xl = look_rotate.x - angleY;
            if (xl < limitAngle.x)
                xl = limitAngle.x;
            else if (xl > limitAngle.y)
                xl = limitAngle.y;

            lookRotate = new Vector3(xl, look_rotate.y + angleX, lookRotate.z);
        }

        public void MoveCameraKeyBoard(bool[] keyFlags)
        {
            if (InterceptInput()) return;

            // 编辑模式下鼠标位于UI(含GUI窗口)上时不响应镜头操作
            if (MapEditor.IsEditOn && IsOverUI())
            {
                return;
            }
            if (keyFlags[0])//(Input.GetAxis("Horizontal")<0)
            {
                position += -transform.right * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (keyFlags[1])
            {
                position += transform.right * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (keyFlags[2])
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (keyFlags[3])
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * -keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
        }

        private void MoveCameraKeyBoard()
        {
            if (InterceptInput()) return;

            // 编辑模式下鼠标位于UI(含GUI窗口)上时不响应镜头移动操作
            if (MapEditor.IsEditOn && IsOverUI())
            {
                return;
            }
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))//(Input.GetAxis("Horizontal")<0)
            {
                position += -transform.right * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                position += transform.right * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * -keyBoardMoveSpeed * Time.unscaledDeltaTime;
            }
        }

        private void ZoomCamera()
        {
            // 编辑模式下鼠标位于UI(含GUI窗口)上时不响应镜头缩放操作
            if (MapEditor.IsEditOn && IsOverUI())
            {
                return;
            }
            Vector2 scrollWheel = Input.mouseScrollDelta;
            if (scrollWheel.y != 0)
            {
                ZoomCamera(scrollWheel.y);
            }
        }


        bool IsOverUI()
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || (MapEditor.IsEditOn && EditorWindow.IsPointOverUI())  /* || FairyGUI.Stage.isTouchOnUI*/;
        }

        Ray ray;
        bool isMouseMoving = false;
        bool isMouseRotate = false;
        Vector3 oldMousePos;
        Vector3 newMosuePos;

        private void RotateCamera()
        {
            // 编辑模式下鼠标位于UI(含GUI窗口)上时不响应镜头旋转操作
            if (MapEditor.IsEditOn && IsOverUI())
            {
                return;
            }
            if (Input.GetMouseButton(1) && !IsOverUI() && !isMouseMoving)
            {

                if (Input.GetMouseButtonDown(1))
                {
                    isMouseRotate = false;
                    newMosuePos = Input.mousePosition;
                    oldMousePos = Input.mousePosition;
                }
                else
                {
                    if (oldMousePos == Input.mousePosition)
                    {
                        return;
                    }
                    isMouseRotate = true;

                    newMosuePos = Input.mousePosition;
                    Vector3 dis = newMosuePos - oldMousePos;
                    oldMousePos = Input.mousePosition;
                    float angleX = dis.x;
                    float angleY = dis.y;

                    RotateCamera(new Vector2(angleX, angleY));
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                if (isMouseRotate)
                {
                    isMouseRotate = false;
                    return;
                }
            }
        }


        public void SetCamera(Camera cam)
        {
            camera = cam;
        }

        public void NeedUpdateCamera()
        {
            changed = true;
        }

        public override void UpdateRender()
        {
        }

        public override void Update()
        {
            if (enabled == false) return;
            if (Sango.Tools.MapEditor.IsEditOn)
            {

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
                MoveCameraKeyBoard();
                ZoomCamera();
                RotateCamera();
                MouseDragWorld();
#else
            MoveCameraKeyBoard();
            ZoomCameraMobile();
            RotateCameraMobile();
            MouseDragWorldMobile();
#endif
            }
            // 视距同步：距离变了 / 被强制置脏就重算雾效与远裁剪面。
            // 刻意放在 changed 判断之外 —— 顶视图缓动与读档定位可能只改 cur_distance、
            // 不改朝向，old 逻辑（只在 ZoomCamera 里调）会把这类路径全漏掉。
            if (viewDistanceDirty || Mathf.Abs(cur_distance - lastViewDistance) > 0.01f)
                ApplyViewDistance();

            if (changed)
            {
                changed = false;
                transform.rotation = Quaternion.Euler(look_rotate);
                transform.position = lookAt.position - transform.forward * cur_distance;
                cameraDistanceFactor = (cur_distance - limitDistance.x) / (limitDistance.y - limitDistance.x);

                UniversalRenderPipelineAsset m_URPAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (m_URPAsset != null)
                {
                    float factor = Math.Max(630, distance) / 630;
                    float max = 980 * factor;
                    float targetShadowDistance = Mathf.Lerp(500, max, cameraDistanceFactor);

                    // 判脏后再写：URP 资产是全局共享对象，无变化时重复赋值会让所有相机重算阴影参数。
                    // 0.5 的容差是为了过滤拖拽 / 缩放时的亚像素抖动，避免每帧都触发。
                    if (Mathf.Abs(targetShadowDistance - lastShadowDistance) > 0.5f)
                    {
                        lastShadowDistance = targetShadowDistance;
                        m_URPAsset.shadowDistance = targetShadowDistance;
                    }
                }

                // 俯仰接近 90°（顶视图）时 forward 与 LookAt 的默认 up 反平行，叉积趋近 0，
                // LookAt 的结果会抖动甚至翻转。留 2° 余量：88° 以上一律保留上面由
                // Euler(look_rotate) 算出的朝向，不再让 LookAt 覆盖。
                // 正常视角的俯仰上限是 limitAngle.y = 70，不受影响。
                if (look_rotate.x < 88f)
                    transform.LookAt(lookAt);

                map.onValueChanged?.Invoke(this);
            }

            oldMousePos = Input.mousePosition;
        }
        private Vector3 oldDragPos;
        bool isPressedUI = false;
        private void MouseDragWorld()
        {
            // 编辑模式下鼠标位于UI(含GUI窗口)上时不响应镜头拖拽操作
            if (MapEditor.IsEditOn && IsOverUI())
            {
                return;
            }

            if (!Input.GetKey(KeyCode.LeftControl) && /*Input.GetKey(KeyCode.Space) &&*/ Input.GetMouseButton(2) && !isPressedUI && !isMouseRotate)
            {

                if (Input.GetMouseButtonDown(2))
                {
                    isMouseMoving = false;

                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;

                    if (IsOverUI())
                        isPressedUI = true;

                    if (viewPlane.Raycast(ray, out dis))
                    {
                        oldDragPos = ray.GetPoint(dis);
                    }
                }
                else
                {

                    if (oldMousePos == Input.mousePosition)
                    {
                        return;
                    }

                    isMouseMoving = true;

                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;

                    if (viewPlane.Raycast(ray, out dis))
                    {
                        Vector3 offset = oldDragPos - ray.GetPoint(dis);
                        lookAt.position += offset;
                        position += offset;
                        NeedUpdateCamera();
                    }
                }
            }
            else if (Input.GetMouseButtonUp(2))
            {
                isPressedUI = false;
                if (isMouseMoving)
                {
                    isMouseMoving = false;
                    return;
                }

                //if (EventSystem.current.IsPointerOverGameObject())
                //    return;

                //ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                //RaycastHit hit;
                //if (Physics.Raycast(ray, out hit, 2000, rayCastLayer)) {
                //    MapObject mapObjcet = hit.collider.gameObject.GetComponentInParent<MapObject>();
                //    if (OnClickCall != null) {
                //        if (mapObjcet != null) {
                //            Debug.LogError(string.Format("mapObject: {0}, {1}", mapObjcet.type, mapObjcet.id));

                //            OnClickCall.BeginPCall();
                //            OnClickCall.Push(1);
                //            OnClickCall.Push(mapObjcet.type);
                //            OnClickCall.Push(mapObjcet.id);
                //            OnClickCall.PCall();
                //            OnClickCall.EndPCall();
                //        }
                //        else {

                //            Debug.LogError(string.Format("terrain: {0}, {1}", hit.point.z, hit.point.x));

                //            OnClickCall.BeginPCall();
                //            OnClickCall.Push(2);
                //            OnClickCall.Push(hit.point.z);
                //            OnClickCall.Push(hit.point.x);
                //            OnClickCall.PCall();
                //            OnClickCall.EndPCall();
                //        }
                //    }
                //}
            }
        }

        int mobileControlType = 0;
        int moveFingerId = -1;
        int rotateFingerId1 = -1;
        int rotateFingerId2 = -1;
        int zoomFingerId1 = -1;
        int zoomFingerId2 = -1;
        int zoomFingerId3 = -1;

        public void ZoomCameraMobile()
        {
            //if (Input.touchCount == 3)
            //{
            //    Touch touch = Input.GetTouch(2);
            //    Touch touch1 = Input.GetTouch(1);
            //    Touch touch0 = Input.GetTouch(0);
            //    if ((touch.phase == TouchPhase.Began || touch.fingerId != zoomFingerId3 || touch0.fingerId != zoomFingerId1 || touch1.fingerId != zoomFingerId2) && !IsOverUI())
            //    {
            //        mobileControlType = 2;
            //        zoomFingerId1 = touch0.fingerId;
            //        zoomFingerId2 = touch1.fingerId;
            //        zoomFingerId3 = touch.fingerId;
            //    }
            //    else if (touch.fingerId == zoomFingerId3)
            //    {
            //        if (touch.phase == TouchPhase.Moved)
            //        {
            //            isMouseRotate = true;
            //            ZoomCamera(touch.deltaPosition.y / 500f);
            //        }
            //        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            //        {
            //            mobileControlType = 0;
            //            zoomFingerId1 = -1;
            //            zoomFingerId2 = -1;
            //            zoomFingerId3 = -1;
            //            return;
            //        }
            //    }
            //}
        }

        private Vector2[] touchPos = new Vector2[2];
        private int[] touchFinger = new int[2];

        // 两只手指一起动则为镜头旋转, 一只指头动,另一只指头不动则为镜头缩放
        public void RotateCameraMobile()
        {
            if (Input.touchCount == 2)
            {
                Touch touch1 = Input.GetTouch(0);
                Touch touch2 = Input.GetTouch(1);

                if (touch1.phase == TouchPhase.Began || touch2.phase == TouchPhase.Began)
                {
                    touchFinger[0] = touch1.fingerId;
                    touchPos[0] = touch1.position;
                    touchFinger[1] = touch2.fingerId;
                    touchPos[1] = touch2.position;
                    return;
                }

                Vector2 touch1Dirction = touch1.position - touchPos[0];
                Vector2 touch2Dirction = touch2.position - touchPos[1];
                float dotAngle = Vector2.Dot(touch1Dirction, touch2Dirction);
                if (dotAngle > 0)
                {
                    // rotate
                    RotateCamera(touch1.deltaPosition);
                    mobileControlType = 2;
                }
                else
                {
                    float len = (touch1.position - touch2.position).sqrMagnitude;
                    float srcLen = (touchPos[0] - touchPos[1]).sqrMagnitude;
                    float delta = Mathf.Max(Mathf.Abs(touch2.deltaPosition.x), Mathf.Abs(touch1.deltaPosition.x));
                    if (len < srcLen)
                        delta = -delta;
                    ZoomCamera(delta / 500f);
                    mobileControlType = 2;

                }
                touchPos[0] = touch1.position;
                touchPos[1] = touch2.position;
                //touchPos[0] = touch1.position;
                //touchPos[1] = touch2.position;

                //Touch touch = Input.GetTouch(1);
                //Touch touch0 = Input.GetTouch(0);
                //if ((touch.phase == TouchPhase.Began || touch.fingerId != rotateFingerId2 || touch0.fingerId != rotateFingerId1) && !IsOverUI())
                //{
                //    mobileControlType = 2;
                //    rotateFingerId1 = touch0.fingerId;
                //    rotateFingerId2 = touch.fingerId;
                //}
                //else if (touch.fingerId == rotateFingerId2)
                //{
                //    if (touch0.phase == TouchPhase.Moved && touch.phase == TouchPhase.Moved)
                //    {
                //        isMouseRotate = true;
                //        RotateCamera(touch.deltaPosition);
                //    }
                //    else if (touch0.phase == TouchPhase.Stationary && touch.phase == TouchPhase.Moved)
                //    {
                //        ZoomCamera(touch.deltaPosition.y / 500f);
                //    }
                //    else if (touch0.phase == TouchPhase.Moved && touch.phase == TouchPhase.Stationary)
                //    {
                //        ZoomCamera(touch0.deltaPosition.y / 500f);
                //    }
                //    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                //    {
                //        mobileControlType = 0;
                //        rotateFingerId1 = -1;
                //        rotateFingerId2 = -1;
                //        return;
                //    }
                //}
            }
        }
        public void MouseDragWorldMobile()
        {
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if ((touch.phase == TouchPhase.Began || touch.fingerId != moveFingerId) && !IsOverUI())
                {
                    mobileControlType = 1;
                    moveFingerId = touch.fingerId;
                    ray = Camera.main.ScreenPointToRay(touch.position);
                    float dis;

                    if (viewPlane.Raycast(ray, out dis))
                    {
                        oldDragPos = ray.GetPoint(dis);
                    }
                }
                else if (touch.fingerId == moveFingerId)
                {
                    if (mobileControlType != 1)
                    {
                        mobileControlType = 1;
                        ray = Camera.main.ScreenPointToRay(touch.position);
                        float dis;

                        if (viewPlane.Raycast(ray, out dis))
                        {
                            oldDragPos = ray.GetPoint(dis);
                        }
                    }

                    if (touch.phase == TouchPhase.Moved)
                    {
                        isMouseMoving = true;

                        ray = Camera.main.ScreenPointToRay(touch.position);
                        float dis;

                        if (viewPlane.Raycast(ray, out dis))
                        {
                            Vector3 offset = oldDragPos - ray.GetPoint(dis);
                            lookAt.position += offset;
                            position += offset;
                            NeedUpdateCamera();
                        }
                    }
                    else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        mobileControlType = 0;
                        moveFingerId = -1;
                        return;
                    }
                }
            }
        }

    }
}
