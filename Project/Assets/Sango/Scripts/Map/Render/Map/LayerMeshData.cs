using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sango.Render
{
    public class LayerMeshData
    {
        public MapCache<Vector3> vertexCache;
        public MapCache<Color> colorCache;
        public MapCache<int> triangleCache;
        public MapCache<Vector2> uvCache;
        public MapCache<Vector3> normalCache;
        public static MeshUpdateFlags Flags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds;

        protected static Queue<LayerMeshData> queue = new Queue<LayerMeshData>();
        public static LayerMeshData Create()
        {
            if (queue.Count == 0)
                return new LayerMeshData();
            else
                return queue.Dequeue();
        }
        public void Clear()
        {
            queue.Enqueue(this);
        }

        public int vertexCount
        {
            set
            {
                if (vertexCache == null)
                    vertexCache = new MapCache<Vector3>(value);
                else {
                    if(value > vertexCache.Size)
                        vertexCache = new MapCache<Vector3>(value);
                }
            }
        }
        public int colorCount
        {
            set
            {
                if (colorCache == null)
                    colorCache = new MapCache<Color>(value);
                else {
                    if (value > colorCache.Size)
                        colorCache = new MapCache<Color>(value);
                }
            }
        }

        public int triangleCount
        {
            set
            {
                if (triangleCache == null)
                    triangleCache = new MapCache<int>(value);
                else {
                    if (value > triangleCache.Size)
                        triangleCache = new MapCache<int>(value);
                }
            }
        }

        public int uvCount
        {
            set
            {
                if (uvCache == null)
                    uvCache = new MapCache<Vector2>(value);
                else {
                    if (value > uvCache.Size)
                        uvCache = new MapCache<Vector2>(value);
                }
            }
        }

        public int normalCount
        {
            set
            {
                if (normalCache == null)
                    normalCache = new MapCache<Vector3>(value);
                else {
                    if (value > normalCache.Size)
                        normalCache = new MapCache<Vector3>(value);
                }
            }
        }



        public void Reset()
        {
            if (vertexCache != null)
                vertexCache.Reset();
            if (colorCache != null)
                colorCache.Reset();
            if (triangleCache != null)
                triangleCache.Reset();
            if (uvCache != null)
                uvCache.Reset();
            if (normalCache != null)
                normalCache.Reset();
        }

        public bool IsValid()
        {
            return vertexCache.Count > 0;
        }

        public void UpdateMesh(Mesh mesh)
        {
            mesh.Clear();
            if (vertexCache != null) mesh.SetVertices(vertexCache.Values, 0, vertexCache.Count, Flags);
            if (uvCache != null) mesh.SetUVs(0, uvCache.Values, 0, uvCache.Count, Flags);
            if (colorCache != null) mesh.SetColors(colorCache.Values, 0, colorCache.Count, Flags);
            if (normalCache != null) mesh.SetNormals(normalCache.Values, 0, normalCache.Count, Flags);
            if (triangleCache != null) mesh.SetTriangles(triangleCache.Values, 0, triangleCache.Count, 0, true);
            mesh.RecalculateBounds();
            //mesh.RecalculateNormals();
        }

        public void Dispose()
        {
            if (vertexCache != null)
                vertexCache.Clear();
            if (colorCache != null)
                colorCache.Clear();
            if (triangleCache != null)
                triangleCache.Clear();
            if (uvCache != null)
                uvCache.Clear();
            if (normalCache != null)
                normalCache.Clear();
        }

        
    }
}