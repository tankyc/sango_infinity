using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System.IO;
using LuaInterface;

namespace Sango.Tools
{
    public class EditorFreeCamera : MonoBehaviour
    {
        public Render.MapRender newMap;
        public static Camera viewCamera;
        public Transform lookAt;
        public int beginSeason = 0;
        public float keyBoardMoveSpeed = 300f;
        public Vector3 lookRotate;
        public float rotSpeed = 0.1f;
        public float zoomSpeed = 400.0f;
        public bool changed = false;
        public float curDistance = 500;
        public Vector2 distanceMax = new Vector2(100, 1500);
        public Vector2 angleMax = new Vector2(22.5f, 70);
        static Plane viewPlane;
        private int rayCastLayer;
        Ray ray;
        bool isMouseMoving = false;
        private Vector3 oldMousePos;
        private Vector3 newMosuePos;
        bool isMousePressed = false;
        bool isPressedUI = false;
        private Vector3 oldDragPos;
        public LuaFunction OnClickCall;
        private void Awake()
        {
            rayCastLayer = LayerMask.GetMask(new string[] { "Map", "Troops", "Building" });
            viewPlane = new Plane(Vector3.up, Vector3.zero);
        }
        private void Start()
        {
            if (newMap != null)
            {

                UpdateCamera();
                newMap.ChangeSeason(beginSeason);
            }
            if (lookAt == null)
                lookAt = new GameObject("lookAt").transform;
        }

        /// <summary>
        /// 在对象启用时调用，获取当前组件挂载的相机对象，赋值给静态的viewCamera视图相机变量，方便后续使用
        /// </summary>
        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
        }

        /// <summary>
        /// 在每帧渲染的最后阶段调用，处理键盘控制相机移动、鼠标缩放、鼠标操作（旋转和拖动）等功能，以及依据相机状态变化更新其位置和旋转角度等信息，同时记录当前帧鼠标位置作为下一帧的旧位置
        /// </summary>
        void LateUpdate()
        {
            MoveCameraKeyBoard();
            ZoomCamera();
            SuperViewMouse();

            if (changed)
            {
                changed = false;

                transform.rotation = Quaternion.Euler(lookRotate);
                transform.position = lookAt.position - transform.forward * curDistance;
                transform.LookAt(lookAt);

                //if (newMap != null) {
                //    newMap.UpdateByCamera(viewCamera, lookAt.position, curDistance);
                //}
            }
            oldMousePos = Input.mousePosition;
        }

        bool gridShow = true;
		
        /// <summary>
        /// 根据指定方向和速度移动相机看向的目标对象，进而间接移动相机，然后更新相机状态
        /// </summary>
        public void MoveCamera(int dir, float speed)
        {
            if (dir == 0)
            {
                lookAt.position += -transform.right * speed;
                UpdateCamera();
            }
            else if (dir == 1)
            {
                lookAt.position += transform.right * speed;
                UpdateCamera();
            }
            else if (dir == 2)
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * speed;
                UpdateCamera();
            }
            else if (dir == 3)
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * -speed;
                UpdateCamera();
            }
        }

        /// <summary>
        /// 通过传入偏移量来移动相机看向的目标对象，进而间接移动相机，然后更新相机状态
        /// </summary>
        public void OffsetCamera(Vector3 offset)
        {
            lookAt.position += offset;
            UpdateCamera();
        }


        private void MoveCameraKeyBoard()
        {
            if (/*Input.GetKey(KeyCode.A) || */Input.GetKey(KeyCode.LeftArrow))//(Input.GetAxis("Horizontal")<0)
            {
                lookAt.position += -transform.right * keyBoardMoveSpeed * Time.deltaTime;
                UpdateCamera();
            }
            if (/*Input.GetKey(KeyCode.D) ||*/ Input.GetKey(KeyCode.RightArrow))
            {
                lookAt.position += transform.right * keyBoardMoveSpeed * Time.deltaTime;
                UpdateCamera();
            }
            if (/*Input.GetKey(KeyCode.W) ||*/ Input.GetKey(KeyCode.UpArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * keyBoardMoveSpeed * Time.deltaTime;
                UpdateCamera();

            }
            if (/*Input.GetKey(KeyCode.S) ||*/ Input.GetKey(KeyCode.DownArrow))
            {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * -keyBoardMoveSpeed * Time.deltaTime;
                UpdateCamera();
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (newMap != null)
                {
                    newMap.ChangeSeason(beginSeason++);
                }
                else
                {
                    Debug.LogWarning("newMap 引用为空，无法切换季节");
                }
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                gridShow = !gridShow;
                if (newMap != null)
                {
                    newMap.ShowGrid(gridShow);
                }
                else
                {
                    Debug.LogWarning("newMap 引用为空，无法显示/隐藏网格");
                }
            }
        }

        private void ZoomCamera()
        {
            float offset = Input.GetAxis("Mouse ScrollWheel");
            if (offset != 0)
            {
                offset *= zoomSpeed;
                curDistance -= offset;
                if (curDistance < distanceMax.x)
                    curDistance = distanceMax.x;
                else if (curDistance > distanceMax.y)
                    curDistance = distanceMax.y;
                UpdateCamera();
            }
        }

        public void ZoomCamera(float delta)
        {
            curDistance += delta;
            if (curDistance < distanceMax.x)
                curDistance = distanceMax.x;
            else if (curDistance > distanceMax.y)
                curDistance = distanceMax.y;
            UpdateCamera();
        }

        public void RotateCamera(Vector2 offset)
        {
            float angleX = offset.x;
            float angleY = offset.y;
            //Debug.Log(string.Format("angleX:{0} angleY:{1} Time.deltaTime{2}", angleX, angleY, Time.deltaTime));
            lookRotate.x -= angleY;
            if (lookRotate.x < angleMax.x)
                lookRotate.x = angleMax.x;
            else if (lookRotate.x > angleMax.y)
                lookRotate.x = angleMax.y;

            lookRotate.y += angleX;
            UpdateCamera();
        }

        private void SuperViewMouse()
        {
            if (Input.GetMouseButton(1) && !isPressedUI)
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
                    float angleX = dis.x * rotSpeed ;
                    float angleY = dis.y * rotSpeed ;
                    //Debug.Log(string.Format("angleX:{0} angleY:{1} Time.deltaTime{2}", angleX, angleY, Time.deltaTime));
                    lookRotate.x -= angleY;
                    if (lookRotate.x < angleMax.x)
                        lookRotate.x = angleMax.x;
                    else if (lookRotate.x > angleMax.y)
                        lookRotate.x = angleMax.y;

                    lookRotate.y += angleX;

                    UpdateCamera();
                }
            }
            else if (Input.GetMouseButtonUp(1))
            {
                isPressedUI = false;
                if (isMouseMoving)
                {
                    isMouseMoving = false;
                    return;
                }
                //if (OnClickCall != null) {
                //    OnClickCall.BeginPCall();
                //    OnClickCall.Push(3);
                //    OnClickCall.PCall();
                //    OnClickCall.EndPCall();
                //}
            }

            if (/*Input.GetKey(KeyCode.Space) &&*/ Input.GetMouseButton(2) && !isPressedUI)
            {

                if (Input.GetMouseButtonDown(2))
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
                        UpdateCamera();
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

        /// <summary>
        /// 标记相机相关属性有变化，触发后续在合适时机（如LateUpdate中）对相机位置、旋转等的更新操作
        /// </summary>
        public void UpdateCamera()
        {
            changed = true;
        }

        /// <summary>
        /// 用于存储相机视平面四个角点坐标的数组（三维向量数组），用于计算可视区域获取相机视口矩形等操作
        /// </summary>
        private static Vector3[] corners = new Vector3[4];

        /// <summary>
        /// 平截头体：静态方法，获取相机可视矩形区域的坐标信息（坐标及宽高）。通过传入限制长度，尝试获取平面的角点信息，如果成功获取则返回true，并通过输出参数返回矩形的x、y坐标以及宽度和高度；若获取失败则返回false并将输出参数设为0
        /// 若获取成功，则计算并返回可视矩形在世界坐标下的最小坐标（x、y）以及宽（w）和高（h），同时返回true；
        /// 若获取平面角点信息失败，则将输出参数（x、y、w、h）都设为0，并返回false
        /// </summary>
        public static bool GetViewRect(float limitLen, out float x, out float y, out float w, out float h)
        {
            if (CameraPlaneView.GetPlaneCorners(ref viewPlane, viewCamera, limitLen, ref corners))
            {
                Vector3 min = viewCamera.transform.position;
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
    }
}