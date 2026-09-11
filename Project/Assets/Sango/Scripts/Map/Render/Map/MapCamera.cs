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
            if(!MapEditor.IsEditOn)
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
            position = pos;
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

            if(versionCode >= 9)
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
        }

        public void OffsetCamera(Vector3 offset)
        {
            position += offset;
        }

        public void RotateCamera(Vector2 offset)
        {
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
            if (changed)
            {
                changed = false;
                transform.rotation = Quaternion.Euler(look_rotate);
                transform.position = lookAt.position - transform.forward * cur_distance;
                cameraDistanceFactor = (cur_distance - limitDistance.x) / (limitDistance.y - limitDistance.x);

                UniversalRenderPipelineAsset m_URPAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if (m_URPAsset != null)
                {
                    m_URPAsset.shadowDistance = Mathf.Lerp(500, 980, cameraDistanceFactor);
                }

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
