using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Sango.Render
{
    // 雾效
    public class MapCamera : MapProperty
    {
        Transform lookAt;
        Transform transform;
        Camera camera;

        public float fov = 25f;
        public float near_clip = 0.3f;
        public float far_clip = 3500f;
        public float cameraDistanceFactor = 1;

        public Vector3 look_position = new Vector3(1407, 0, 796);
        public Vector2 limitDistance = new Vector2(200f, 630f);
        public Vector2 limitAngle = new Vector2(22.5f, 70f);
        public float cur_distance = 300f;
        public Vector3 look_rotate = new Vector3(45f, -90f, 0f);
        public float zoomSpeed = 400f;
        public float keyBoardMoveSpeed = 1f;
        public float rotSpeed = 0.1f;

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
            lookAt.position = pos;
            NeedUpdateCamera();
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
            distance -= delta * zoomSpeed;
            if (distance < limitDistance.x)
                distance = limitDistance.x;
            else if (distance > limitDistance.y)
                distance = limitDistance.y;

            cameraDistanceFactor = (cur_distance - limitDistance.x) / (limitDistance.y - limitDistance.x);
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

        private void MoveCameraKeyBoard()
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))//(Input.GetAxis("Horizontal")<0)
            {
                position += -transform.right * keyBoardMoveSpeed;
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                position += transform.right * keyBoardMoveSpeed;
            }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * keyBoardMoveSpeed;
            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                position += forward * -keyBoardMoveSpeed;
            }
        }

        private void ZoomCamera()
        {
            float offset = Input.GetAxis("Mouse ScrollWheel");
            if (offset != 0)
            {
                ZoomCamera(offset);
            }
        }


        bool IsOverUI()
        {
            return (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())/* || FairyGUI.Stage.isTouchOnUI*/;
        }

        Ray ray;
        bool isMouseMoving = false;
        Vector3 oldMousePos;
        Vector3 newMosuePos;

        private void RotateCamera()
        {
            if (Input.GetMouseButton(1) && !IsOverUI())
            {

                if (Input.GetMouseButtonDown(1))
                {
                    isMouseMoving = false;
                    newMosuePos = Input.mousePosition;
                    oldMousePos = Input.mousePosition;
                }
                else
                {
                    if (oldMousePos == Input.mousePosition)
                    {
                        return;
                    }
                    isMouseMoving = true;

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
                if (isMouseMoving)
                {
                    isMouseMoving = false;
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

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
            MoveCameraKeyBoard();
            ZoomCamera();
            RotateCamera();
            MouseDragWorld();
#endif
            if (changed)
            {
                changed = false;
                transform.rotation = Quaternion.Euler(look_rotate);
                transform.position = lookAt.position - transform.forward * cur_distance;
                transform.LookAt(lookAt);
            }

            oldMousePos = Input.mousePosition;
        }
        private Vector3 oldDragPos;
        bool isPressedUI = false;
        private void MouseDragWorld()
        {
            if (!Input.GetKey(KeyCode.LeftControl) && /*Input.GetKey(KeyCode.Space) &&*/ Input.GetMouseButton(0) && !isPressedUI)
            {

                if (Input.GetMouseButtonDown(0))
                {
                    isMouseMoving = false;

                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;

                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
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
            else if (Input.GetMouseButtonUp(0))
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
    }
}
