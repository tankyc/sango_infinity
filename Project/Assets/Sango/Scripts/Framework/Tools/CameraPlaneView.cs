using UnityEngine;
using System.Collections.Generic;
using System;

public class CameraPlaneView : MonoBehaviour
{
    #region for debug
    public Camera viewCamera;
    private static float slimitLen = 0;
    private static Plane splane = new Plane(Vector3.up, Vector3.zero);
    private static List<Rect> rectList = new List<Rect>();
#if UNITY_EDITOR
    //void Update()
    //{


    //    Vector3[] corners;
    //    if (GetPlaneCorners(Vector3.up, Vector3.zero, viewCamera, out corners))
    //    {
    //        Debug.DrawLine(corners[0], corners[1], Color.green);    // bottom
    //        Debug.DrawLine(corners[1], corners[2], Color.green);    // right
    //        Debug.DrawLine(corners[2], corners[3], Color.green);    // top
    //        Debug.DrawLine(corners[3], corners[0], Color.green);    // left
            

    //    }

    //    if (CameraPlaneView.GetFarPlaneCorners(ref splane, viewCamera, slimitLen, ref corners))
    //    {
    //        Vector3 min = viewCamera.transform.position;
    //        Vector3 max = min;
    //        for (int i = 0; i < 2; ++i)
    //        {
    //            Vector3 c = corners[i];
    //            min.x = Mathf.Min(min.x, c.x);
    //            min.z = Mathf.Min(min.z, c.z);
    //            max.x = Mathf.Max(max.x, c.x);
    //            max.z = Mathf.Max(max.z, c.z);
    //        }
    //        corners[0] = new Vector3(min.x, 0, min.z);
    //        corners[1] = new Vector3(min.x, 0, max.z);
    //        corners[2] = new Vector3(max.x, 0, max.z);
    //        corners[3] = new Vector3(max.x, 0, min.z);


    //        Debug.DrawLine(corners[0], corners[1], Color.red);    // bottom
    //        Debug.DrawLine(corners[1], corners[2], Color.red);    // right
    //        Debug.DrawLine(corners[2], corners[3], Color.red);    // top
    //        Debug.DrawLine(corners[3], corners[0], Color.red);    // left
    //    }

    //    foreach (Rect r in rectList)
    //    {
    //        DrawRect(r);
    //    }
       
    //}
#endif

    public static void DrawRect(Rect r)
    {
        Vector3 r1 = new Vector3(r.min.x, 0, r.min.y);
        Vector3 r2 = new Vector3(r.min.x, 0, r.max.y);
        Vector3 r3 = new Vector3(r.max.x, 0, r.max.y);
        Vector3 r4 = new Vector3(r.max.x, 0, r.min.y);
        Debug.DrawLine(r1, r2, Color.blue);    // bottom
        Debug.DrawLine(r2, r3, Color.blue);    // right
        Debug.DrawLine(r3, r4, Color.blue);    // top
        Debug.DrawLine(r4, r1, Color.blue);    // left
    }
    public static void AddDrawRect(Rect r)
    {
        rectList.Add(r);
    }
    public static void AddDrawRect(float x, float y, float w, float h)
    {
        rectList.Add(new Rect(x, y, w, h));
    }
    #endregion


    /// <summary>
    /// 获取摄像机在平面上的视野范围的4个角的顶点
    /// </summary>
    /// <param name="normal">平面法线</param>
    /// <param name="planePoint">平面上的一点</param>
    /// <param name="camera">摄像机</param>
    /// <param name="corners">返回4个角的顺序：左下、右下、右上、左上</param>
    /// <returns>摄像机与平面是否完全相交</returns>
    public static bool GetPlaneCorners(Vector3 normal, Vector3 planePoint, Camera camera, out Vector3[] corners)
    {
        Plane plane = new Plane(normal, planePoint);
        return GetPlaneCorners(ref plane, camera, out corners);
    }

    /// <summary>
    /// 获取摄像机在平面上的视野范围的4个角的顶点
    /// </summary>
    /// <param name="plane">平面结构体</param>
    /// <param name="camera">摄像机</param>
    /// <param name="corners">返回4个角的顺序：左下、右下、右上、左上</param>
    /// <returns>摄像机与平面是否完全相交</returns>
    public static bool GetPlaneCorners(ref Plane plane, Camera camera, out Vector3[] corners)
    {
        Ray rayBL = camera.ViewportPointToRay(new Vector3(0, 0, 1));     // bottom left
        Ray rayBR = camera.ViewportPointToRay(new Vector3(1, 0, 1));     // bottom right
        Ray rayTL = camera.ViewportPointToRay(new Vector3(0, 1, 1));     // top left
        Ray rayTR = camera.ViewportPointToRay(new Vector3(1, 1, 1));     // top right

        corners = new Vector3[4];
        if (!GetRayPlaneIntersection(ref plane, rayBL, out corners[0])
            || !GetRayPlaneIntersection(ref plane, rayBR, out corners[1])
            || !GetRayPlaneIntersection(ref plane, rayTR, out corners[2])
            || !GetRayPlaneIntersection(ref plane, rayTL, out corners[3]))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public static bool GetPlaneCorners(ref Plane plane, Camera camera, float limitLen, ref Vector3[] corners)
    {
#if UNITY_EDITOR
        slimitLen = limitLen;
#endif

        Ray rayBL = camera.ViewportPointToRay(new Vector3(0, 0, 1));     // bottom left
        Ray rayBR = camera.ViewportPointToRay(new Vector3(1, 0, 1));     // bottom right
        Ray rayTL = camera.ViewportPointToRay(new Vector3(0, 1, 1));     // top left
        Ray rayTR = camera.ViewportPointToRay(new Vector3(1, 1, 1));     // top right

        if (!GetRayPlaneIntersection(ref plane, rayBL, limitLen, out corners[0])
            || !GetRayPlaneIntersection(ref plane, rayBR, limitLen, out corners[1])
            || !GetRayPlaneIntersection(ref plane, rayTR, limitLen, out corners[2])
            || !GetRayPlaneIntersection(ref plane, rayTL, limitLen, out corners[3])) {
            return false;
        }
        else {
            return true;
        }
    }
    public static bool GetFarPlaneCorners(ref Plane plane, Camera camera, float limitLen, ref Vector3[] corners)
    {
#if UNITY_EDITOR
        slimitLen = limitLen;
#endif

        Ray rayTL = camera.ViewportPointToRay(new Vector3(0, 1, 1));     // top left
        Ray rayTR = camera.ViewportPointToRay(new Vector3(1, 1, 1));     // top right

        if (!GetRayPlaneIntersection(ref plane, rayTR, limitLen, out corners[0])
            || !GetRayPlaneIntersection(ref plane, rayTL, limitLen, out corners[1]))
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    public static bool GetRayPlaneIntersection(ref Plane plane, Ray ray, float limitLen, out Vector3 intersection)
    {

        float dis;
        if (!plane.Raycast(ray, out dis))
        {
            intersection = Vector3.zero;
            return false;
        }

        // 下面是获取t的公式
        // 注意，你需要先判断射线与平面是否平行，如果平面和射线平行，那么平面法线和射线方向的点积为0，即除数为0.
        //float t = (Vector3.Dot(normal, planePoint) - Vector3.Dot(normal, ray.origin)) / Vector3.Dot(normal, ray.direction.normalized);
        if (dis >= 0)
        {
            intersection = ray.GetPoint(dis);
            Vector3 begin = ray.origin;
            begin.y = 0;
            Vector3 dir = intersection - begin;
            float magnitude = dir.magnitude;
            if (magnitude > limitLen)
            {
                intersection = begin + dir.normalized * limitLen;
            }
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    /// <summary>
    /// 获取平面与射线的交点
    /// </summary>
    /// <param name="plane">平面结构体</param>
    /// <param name="ray">射线</param>
    /// <param name="intersection">返回交点</param>
    /// <returns>是否相交</returns>
    public static bool GetRayPlaneIntersection(ref Plane plane, Ray ray, out Vector3 intersection)
    {
        float enter;
        if (!plane.Raycast(ray, out enter))
        {
            intersection = Vector3.zero;
            return false;
        }

        // 下面是获取t的公式
        // 注意，你需要先判断射线与平面是否平行，如果平面和射线平行，那么平面法线和射线方向的点积为0，即除数为0.
        //float t = (Vector3.Dot(normal, planePoint) - Vector3.Dot(normal, ray.origin)) / Vector3.Dot(normal, ray.direction.normalized);
        if (enter >= 0)
        {
            intersection = ray.origin + enter * ray.direction.normalized;
            return true;
        }
        else
        {
            intersection = Vector3.zero;
            return false;
        }
    }

    /// <summary>
    /// 获取平面与射线的交点
    /// </summary>
    /// <param name="normal">平面法线</param>
    /// <param name="planePoint">平面上的一点</param>
    /// <param name="ray">射线</param>
    /// <param name="intersection">返回交点</param>
    /// <returns>是否相交</returns>
    public static bool GetRayPlaneIntersection(Vector3 normal, Vector3 planePoint, Ray ray, out Vector3 intersection)
    {
        Plane plane = new Plane(normal, planePoint);
        return GetRayPlaneIntersection(ref plane, ray, out intersection);
    }
}