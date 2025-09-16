using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using System.IO;
using LuaInterface;
using Sango.Render;

namespace Sango
{
    public class FreeCamera : MonoBehaviour
    {
        public Transform lookAt;
        public Vector2 distanceMax;
        public Vector2 angleMax;
        public int beginSeason = 0;
        public float curDistance;
        public Vector3 lookRotate;
        public bool changed = false;
        public LuaFunction OnClickCall;
        private int rayCastLayer;
        bool isPressedUI = false;
        bool isMouseMoving = false;
        bool isMousePressed = false;

        public void MoveCamera(int dir, float speed)
        {
            if (dir == 0) {
                lookAt.position += -transform.right * speed;
                UpdateCamera();
            }
            else if (dir == 1) {
                lookAt.position += transform.right * speed;
                UpdateCamera();
            }
            else if (dir == 2) {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * speed;
                UpdateCamera();
            }
            else if (dir == 3) {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * -speed;
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

        public void OffsetCamera(Vector3 offset)
        {
            lookAt.position += offset;
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

        public void UpdateCamera()
        {


            changed = true;


        }

        [ContextMenu("Save png")]
        private void SaveTextureToFile()
        {
            if (Camera.main.activeTexture != null) {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = Camera.main.activeTexture;

                Texture2D png = new Texture2D(Camera.main.activeTexture.width, Camera.main.activeTexture.height, TextureFormat.ARGB32, false);
                png.ReadPixels(new UnityEngine.Rect(0, 0, Camera.main.activeTexture.width, Camera.main.activeTexture.height), 0, 0);
                byte[] bytes = png.EncodeToPNG();
                string path = string.Format("Dump/raw {0}.png", Random.Range(0, 65536).ToString("X"));
                FileStream file = System.IO.File.Open(path, FileMode.Create);

                BinaryWriter writer = new BinaryWriter(file);
                writer.Write(bytes);
                file.Close();

                Texture2D.Destroy(png);
                png = null;

                RenderTexture.active = prev;
            }

        }

        private Vector3 oldMousePos;
        private Vector3 newMosuePos;

        private Vector3 oldDragPos;

        public float zoomSpeed = 30.0f;
        public float keyBoardMoveSpeed = 1f;
        public float rotSpeed = 0.05f;

        public static Camera viewCamera;
        static Plane viewPlane;


        private void Awake()
        {
            rayCastLayer = LayerMask.GetMask(new string[] { "Map", "Troops", "Building" });
            viewPlane = new Plane(Vector3.up, Vector3.zero);
            if (lookAt == null)
                lookAt = new GameObject("lookAt").transform;
        }

        private void Start()
        {
            //if (newMap != null) {

            //    UpdateCamera();
            //    newMap.ChangeSeason(beginSeason);
            //}
        }

        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            MoveCameraKeyBoard();
            //ZoomCamera();
            //SuperViewMouse();

            if (changed) {
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
        private void MoveCameraKeyBoard()
        {
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))//(Input.GetAxis("Horizontal")<0)
            {
                lookAt.position += -transform.right * keyBoardMoveSpeed;
                UpdateCamera();
            }
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
                lookAt.position += transform.right * keyBoardMoveSpeed;
                UpdateCamera();
            }
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * keyBoardMoveSpeed;
                UpdateCamera();

            }
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
                Vector3 forward = transform.forward;
                forward.y = 0;
                forward.Normalize();
                lookAt.position += forward * -keyBoardMoveSpeed;
                UpdateCamera();
            }
            if (Input.GetKeyDown(KeyCode.E)) {
                //newMap.ChangeSeason(beginSeason++);
            }
            if (Input.GetKeyDown(KeyCode.R)) {
                gridShow = !gridShow;
                //newMap.ShowGrid(gridShow);
            }
        }

        private void ZoomCamera()
        {
            float offset = Input.GetAxis("Mouse ScrollWheel");
            if (offset != 0) {
                offset *= zoomSpeed;
                curDistance -= offset;
                if (curDistance < distanceMax.x)
                    curDistance = distanceMax.x;
                else if (curDistance > distanceMax.y)
                    curDistance = distanceMax.y;
                UpdateCamera();
            }
        }
        Ray ray;
        private void SuperViewMouse()
        {
            if (Input.GetMouseButton(1) && !isPressedUI) {

                if (Input.GetMouseButtonDown(1)) {
                    isMouseMoving = false;
                    newMosuePos = Input.mousePosition;
                    oldMousePos = Input.mousePosition;
                }
                else {
                    if (oldMousePos == Input.mousePosition) {
                        return;
                    }
                    isMouseMoving = true;

                    newMosuePos = Input.mousePosition;
                    Vector3 dis = newMosuePos - oldMousePos;
                    oldMousePos = Input.mousePosition;
                    float angleX = dis.x * rotSpeed;
                    float angleY = dis.y * rotSpeed;
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
            else if (Input.GetMouseButtonUp(1)) {
                isPressedUI = false;
                if (isMouseMoving) {
                    isMouseMoving = false;
                    return;
                }
                if (OnClickCall != null) {
                    OnClickCall.BeginPCall();
                    OnClickCall.Push(3);
                    OnClickCall.PCall();
                    OnClickCall.EndPCall();
                }
            }



            if (Input.GetMouseButton(0) && !isPressedUI) {

                if (Input.GetMouseButtonDown(0)) {
                    isMouseMoving = false;

                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;

                    if (EventSystem.current.IsPointerOverGameObject())
                        isPressedUI = true;

                    if (viewPlane.Raycast(ray, out dis)) {
                        oldDragPos = ray.GetPoint(dis);
                    }
                }
                else {

                    if (oldMousePos == Input.mousePosition) {
                        return;
                    }

                    isMouseMoving = true;

                    ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    float dis;

                    if (viewPlane.Raycast(ray, out dis)) {
                        Vector3 offset = oldDragPos - ray.GetPoint(dis);
                        lookAt.position += offset;
                        UpdateCamera();
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0)) {
                isPressedUI = false;
                if (isMouseMoving) {
                    isMouseMoving = false;
                    return;
                }

                if (EventSystem.current.IsPointerOverGameObject())
                    return;

                ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 2000, rayCastLayer)) {
                    MapObject mapObjcet = hit.collider.gameObject.GetComponentInParent<MapObject>();
                    if (OnClickCall != null) {
                        //if (mapObjcet != null) {
                        //    Debug.LogError(string.Format("mapObject: {0}, {1}", mapObjcet.type, mapObjcet.id));

                        //    OnClickCall.BeginPCall();
                        //    OnClickCall.Push(1);
                        //    OnClickCall.Push(mapObjcet.type);
                        //    OnClickCall.Push(mapObjcet.id);
                        //    OnClickCall.PCall();
                        //    OnClickCall.EndPCall();
                        //}
                        //else {

                        //    Debug.LogError(string.Format("terrain: {0}, {1}", hit.point.z, hit.point.x));

                        //    OnClickCall.BeginPCall();
                        //    OnClickCall.Push(2);
                        //    OnClickCall.Push(hit.point.z);
                        //    OnClickCall.Push(hit.point.x);
                        //    OnClickCall.PCall();
                        //    OnClickCall.EndPCall();
                        //}
                    }
                }
            }
        }

        private static Vector3[] corners = new Vector3[4];
        public static bool GetViewRect(float limitLen, out float x, out float y, out float w, out float h)
        {
            if (CameraPlaneView.GetPlaneCorners(ref viewPlane, viewCamera, limitLen, ref corners)) {
                Vector3 min = viewCamera.transform.position;
                Vector3 max = min;
                for (int i = 0; i < corners.Length; ++i) {
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