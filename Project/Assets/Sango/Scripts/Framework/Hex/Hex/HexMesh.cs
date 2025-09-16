using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour
{

    private float SIN30 = Mathf.Sin(Mathf.Deg2Rad * 30);
    private float COS30 = Mathf.Cos(Mathf.Deg2Rad * 30);

    private List<HexCell> list = new List<HexCell>();
    private Dictionary<int, GameObject> hexMeshList = new Dictionary<int, GameObject>();


    public static HexMesh InitXHexMesh(int row, int col, float size, float x_off, float z_off, float y)
    {
        GameObject go = new GameObject("HexMesh");
        go.transform.localScale = Vector3.one;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localPosition = new Vector3(x_off, y, z_off);
        var xHexMesh = go.AddComponent<HexMesh>();
        xHexMesh.CreateHexMesh(row, col, size);
        return xHexMesh;
    }


    public void SetHexMat(int index, Material mat)
    {
        if (hexMeshList.ContainsKey(index))
        {
            hexMeshList[index].GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    public void Clear()
    {
        list.Clear();
        hexMeshList.Clear();
        Destroy(gameObject);
    }

    private void CreateHexMesh(int row, int col, float size)
    {
        for (int i = 0; i < row; i++)
        {
            for (int j = 0; j < col; j++)
            {

                int index = i * col + j;
                HexCell xHexCell = new HexCell(i, j, index, size);
                list.Add(xHexCell);
            }
        }

        for (int i = 0; i < list.Count; i++)
        {
            HexCell cell = list[i];
            string cellName = string.Format("{0}|({1},{2})", cell.index, cell.m_Col, cell.m_Row);
            GameObject go = new GameObject(cellName);

            go.transform.parent = transform;
            go.transform.localPosition = cell.m_Pos;
            go.transform.localScale = Vector3.one * 0.98f;
            int layer = LayerMask.NameToLayer("HexCell");
            if (layer >= 0)
                go.layer = layer;
            var meshFileter = go.AddComponent<MeshFilter>();
            var render = go.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            DrawHexagon(cell, size, mesh);
            meshFileter.mesh = mesh;
            MeshCollider meshCollider = go.AddComponent<MeshCollider>();

            hexMeshList[cell.index] = go;
        }

    }


    private void DrawHexagon(HexCell hexagon, float size, Mesh mesh)
    {

        float y = 0;

        List<Vector3> vector3s = new List<Vector3>();
        List<int> triangles = new List<int>();

        Vector3 center = Vector3.zero + y * Vector3.up;

        Vector3 po1 = new Vector3(center.x - size * COS30, y, center.z + size * SIN30);
        Vector3 po2 = new Vector3(center.x, y, center.z + size);
        Vector3 po3 = new Vector3(center.x + size * COS30, y, center.z + size * SIN30);
        Vector3 po4 = new Vector3(center.x + size * COS30, y, center.z - size * SIN30);
        Vector3 po5 = new Vector3(center.x, y, center.z - size);
        Vector3 po6 = new Vector3(center.x - size * COS30, y, center.z - size * SIN30);

        int count = 0;
        vector3s.Add(po1);
        vector3s.Add(po2);
        vector3s.Add(center);
        vector3s.Add(po2);
        vector3s.Add(po3);
        vector3s.Add(center);
        vector3s.Add(po3);
        vector3s.Add(po4);
        vector3s.Add(center);
        vector3s.Add(po4);
        vector3s.Add(po5);
        vector3s.Add(center);
        vector3s.Add(po5);
        vector3s.Add(po6);
        vector3s.Add(center);
        vector3s.Add(po6);
        vector3s.Add(po1);
        vector3s.Add(center);

        int count2 = vector3s.Count;


        for (int i = count; i < count2; i++)
        {
            triangles.Add(i);
        }
        mesh.vertices = vector3s.ToArray();
        mesh.triangles = triangles.ToArray();

    }
}