using Sango.Tools;
using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Sango.Render
{
    public class MapTerrain : MapProperty
    {
        public int cellSize = 64;
        public MapCell[] terrainCells;

        public MapTerrain(MapRender map) : base(map)
        {

        }

        public override void Init()
        {
            base.Init();
            int wCount = map.mapData.vertex_width / cellSize;
            int hCount = map.mapData.vertex_height / cellSize;
            terrainCells = new MapCell[wCount * hCount];

            for (int y = 0; y < hCount; y++)
            {
                for (int x = 0; x < wCount; x++)
                {
                    MapCell cell = new MapCell(map, x * cellSize, y * cellSize, cellSize, cellSize);
                    terrainCells[y * wCount + x] = cell;
                    map.AddStatic(cell);
                }
            }

            //GameObject terrainObject = new GameObject("Terrain");

            //terrainCells = new MapCell[1];
            //MapCell cell = new MapCell(map, 0, 0, map.mapData.vertex_width, map.mapData.vertex_height);
            //terrainCells[0] = cell;
            //map.AddStatic(cell);
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(cellSize);
        }
        // 必须在layer后面
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            cellSize = reader.ReadInt32();
            Rebuild();
        }

        public override void Clear()
        {
            base.Clear();
            if (terrainCells == null) return;
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                map.Remove(cell);
                cell.Clear();
            }
            terrainCells = null;
        }

        public override void UpdateRender()
        {

        }

        public void Rebuild()
        {
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;

                //if (!MapEditor.IsEditOn)
                //{
                //    cell.Init();
                //    //cell.PrepareDatas();
                //    //duration = Time.realtimeSinceStartup - start;
                //    //Debug.LogError($"cell.Init, PrepareDatas耗时：{duration:F4}秒");
                //    //start = Time.realtimeSinceStartup;

                //    Vector2Int startCoords = cell.startCoords;
                //    Vector2Int quadBounds = cell.quadBounds;
                //    Vector3 position = cell.position;
                //    Vector2 MapUVPiece = map.mapData.MapUVPiece;
                //    int vertex_x_max = map.mapData.vertex_x_max;
                //    int vertex_y_max = map.mapData.vertex_y_max;
                //    int maxLayer = map.mapLayer.layerDatas.Length;
                //    int lodL = 1 << lod;
                //    int quadSize = map.mapData.quadSize;

                //    int cell_vertex_x_max = quadBounds.x + 1;
                //    int cell_vertex_y_max = quadBounds.y + 1;
                //    int cellmaxCount = cell_vertex_x_max * cell_vertex_y_max;

                //    for (int j = 0; j < maxLayer; j++)
                //    {
                //        int layer = j;
                //        bool layerIsWater = (layer == (maxLayer - 1));

                //        NativeArray<Vector3> vertexCache = new NativeArray<Vector3>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<Color> colorCache = new NativeArray<Color>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<int> triangleCache = new NativeArray<int>(cellmaxCount * 6, Allocator.Persistent);
                //        NativeArray<Vector2> uvCache = new NativeArray<Vector2>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<Vector3> normalCache = new NativeArray<Vector3>(cellmaxCount, Allocator.Persistent);
                //        NativeArray<int> xIndexCache = new NativeArray<int>(2, Allocator.Persistent);
                //        NativeArray<int> yIndexCache = new NativeArray<int>(cell_vertex_y_max, Allocator.Persistent);
                //        NativeArray<bool> diffyIndexCache = new NativeArray<bool>(cell_vertex_y_max, Allocator.Persistent);
                //        NativeArray<int> cacheIndexs = new NativeArray<int>(5, Allocator.Persistent);

                //        var job = new LayerJob
                //        {
                //            vertexCache = vertexCache,
                //            colorCache = colorCache,
                //            triangleCache = triangleCache,
                //            uvCache = uvCache,
                //            normalCache = normalCache,
                //            xIndexCache = xIndexCache,
                //            yIndexCache = yIndexCache,
                //            diffyIndexCache = diffyIndexCache,
                //            layer = layer,
                //            lodL = lodL,
                //            maxLayer = maxLayer,
                //            layerIsWater = layerIsWater,
                //            vertexMap = map.mapData.vertexMap,
                //            startCoords = startCoords,
                //            quadBounds = quadBounds,
                //            position = position,
                //            MapUVPiece = MapUVPiece,
                //            vertex_x_max = vertex_x_max,
                //            vertex_y_max = vertex_y_max,
                //            quadSize = quadSize,
                //            cacheIndexs = cacheIndexs,
                //        };
                //        job.Schedule().Complete();

                //        Mesh mesh = cell.mesh[layer];

                //        mesh.SetVertices(vertexCache.ToArray(), 0, cacheIndexs[1], LayerMeshData.Flags);
                //        mesh.SetUVs(0, uvCache.ToArray(), 0, cacheIndexs[3], LayerMeshData.Flags);
                //        mesh.SetColors(colorCache.ToArray(), 0, cacheIndexs[4], LayerMeshData.Flags);
                //        mesh.SetNormals(normalCache.ToArray(), 0, cacheIndexs[2], LayerMeshData.Flags);
                //        mesh.SetTriangles(triangleCache.ToArray(), 0, cacheIndexs[0], 0, true);
                //        mesh.RecalculateBounds();

                //        if (cell.meshCollider[layer] == null)
                //        {
                //            MeshCollider co = cell.meshRenderer[layer].gameObject.AddComponent<MeshCollider>();
                //            co.sharedMesh = cell.mesh[layer];
                //            cell.meshCollider[layer] = co;
                //        }
                //        else
                //        {
                //            cell.meshCollider[layer].sharedMesh = cell.mesh[layer];
                //        }


                //        if (!cell.meshRenderer[layer].enabled)
                //            cell.meshRenderer[layer].enabled = cell.visible;

                //        vertexCache.Dispose();
                //        colorCache.Dispose();
                //        triangleCache.Dispose();
                //        uvCache.Dispose();
                //        normalCache.Dispose();
                //        xIndexCache.Dispose();
                //        yIndexCache.Dispose();
                //        diffyIndexCache.Dispose();

                //    }

                //    cell.threadBeginLayer = cell.mesh.Length;

                //}
                //else
                //{
                    if (cell.IsValid())
                    {
                        cell.PrepareDatas();
                    }
                //}
            }

            //duration = Time.realtimeSinceStartup - start;
            //Debug.LogError($"Schedule耗时：{duration:F4}秒");
        }



        /// <summary>
        /// 离开视野后网格再保留多久才释放（秒）。
        /// 留一段迟滞，避免镜头来回平移时反复重建（单块重建实测约 32ms）。
        /// 设为 &lt;= 0 可关闭空闲卸载。
        /// </summary>
        public float meshUnloadDelay = 6f;

        public override void Update()
        {
            if (terrainCells == null) return;

            float dt = Time.deltaTime;
            bool allowUnload = meshUnloadDelay > 0f;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                cell.Update();

                if (!allowUnload) continue;

                // 可见 / 正在后台分层构建的块不动
                if (cell.visible || cell.IsInThreadLoad())
                {
                    cell.idleTime = 0f;
                    continue;
                }
                if (!cell.HasMesh()) continue;

                cell.idleTime += dt;
                if (cell.idleTime >= meshUnloadDelay)
                    cell.ReleaseMesh();
            }
        }

        public override void UpdateImmediate()
        {
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                cell.visible = true;
                cell.PrepareDatas();
            }
        }
    }


    //[BurstCompile]
    //public struct LayerJob : IJob
    //{
    //    public NativeArray<MapData.VertexData> vertexMap;

    //    public NativeArray<Vector3> vertexCache;
    //    public NativeArray<Color> colorCache;
    //    public NativeArray<int> triangleCache;
    //    public NativeArray<Vector2> uvCache;
    //    public NativeArray<Vector3> normalCache;

    //    public NativeArray<int> xIndexCache;
    //    public NativeArray<int> yIndexCache;
    //    public NativeArray<bool> diffyIndexCache;

    //    public NativeArray<int> cacheIndexs;

    //    public readonly static Vector2Int[] NeighborVertex = new Vector2Int[] {
    //        new Vector2Int(1, 0),
    //        new Vector2Int(1, 1),
    //        new Vector2Int(0, 1),
    //        new Vector2Int(-1, 0),
    //        new Vector2Int(-1, -1),
    //        new Vector2Int(0, -1),
    //    };

    //    public int layer;
    //    public int lodL;
    //    public int maxLayer;
    //    public bool layerIsWater;
    //    public Vector2Int startCoords;
    //    public Vector2Int quadBounds;
    //    public Vector3 position;
    //    public Vector2 MapUVPiece;
    //    public int vertex_x_max;
    //    public int vertex_y_max;
    //    public int quadSize;

    //    static readonly Color color_white = Color.white;
    //    static readonly Color color_black = new Color(0, 0, 0, 0);
    //    public int triangleCacheIndex;
    //    public int vertexCacheIndex;
    //    public int normalCacheIndex;
    //    public int uvCacheIndex;
    //    public int colorCacheIndex;

    //    public Vector2 VertexUV(MapData.VertexData data, int x, int y)
    //    {
    //        return new Vector2(x * MapUVPiece.x, y * MapUVPiece.y);
    //    }
    //    public Vector3 VertexPosition(MapData.VertexData data, int x, int y)
    //    {
    //        return new Vector3(y * quadSize, data.height * 0.5f, x * quadSize);
    //    }
    //    public Vector3 VertexWaterPosition(MapData.VertexData data, int x, int y)
    //    {
    //        if (data.water == 0)
    //        {
    //            int lx = x - 1;
    //            if (lx > 0)
    //            {
    //                MapData.VertexData d = vertexMap[lx + vertex_x_max * y];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }
    //            }
    //            int uy = y - 1;
    //            if (uy > 0)
    //            {
    //                MapData.VertexData d = vertexMap[x + vertex_x_max * uy];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (lx > 0)
    //                {
    //                    d = vertexMap[lx + vertex_x_max * uy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }

    //            }
    //            int rx = x + 1;
    //            if (rx < vertex_x_max)
    //            {
    //                MapData.VertexData d = vertexMap[rx + vertex_x_max * y];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (uy > 0)
    //                {
    //                    d = vertexMap[rx + vertex_x_max * uy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }
    //            }
    //            int dy = y + 1;
    //            if (dy < vertex_y_max)
    //            {
    //                MapData.VertexData d = vertexMap[x + vertex_x_max * dy];
    //                if (d.water > 0)
    //                {
    //                    return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                }

    //                if (rx < vertex_x_max)
    //                {
    //                    d = vertexMap[rx + vertex_x_max * dy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }

    //                if (lx > 0)
    //                {
    //                    d = vertexMap[lx + vertex_x_max * dy];
    //                    if (d.water > 0)
    //                    {
    //                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
    //                    }
    //                }
    //            }
    //        }
    //        return new Vector3(y * quadSize, data.water * 0.5f, x * quadSize);
    //    }
    //    public Vector3 VertexNormal(MapData.VertexData data, int x, int y)
    //    {
    //        Vector3 vdPos = VertexPosition(data, x, y);
    //        Vector3 normal = Vector3.zero;

    //        for (int z = 0; z < 6; z++)
    //        {
    //            int next = z + 1;
    //            if (next == 6)
    //                next = 0;

    //            Vector2Int neighbor_z = NeighborVertex[z];
    //            int neighbor_z_x = neighbor_z.x + x;
    //            int neighbor_z_y = neighbor_z.y + y;
    //            Vector2Int neighbor_next = NeighborVertex[next];
    //            int neighbor_next_x = neighbor_next.x + x;
    //            int neighbor_next_y = neighbor_next.y + y;

    //            if (neighbor_z_x >= 0 && neighbor_z_x < vertex_x_max && neighbor_z_y >= 0 && neighbor_z_y < vertex_y_max &&
    //                neighbor_next_x >= 0 && neighbor_next_x < vertex_x_max && neighbor_next_y >= 0 && neighbor_next_y < vertex_y_max)
    //            {
    //                MapData.VertexData n_z = vertexMap[neighbor_z_x + vertex_x_max * neighbor_z_y];
    //                Vector3 pos_z = VertexPosition(n_z, neighbor_z_x, neighbor_z_y);

    //                MapData.VertexData n_next = vertexMap[neighbor_next_x + vertex_x_max * neighbor_next_y];
    //                Vector3 pos_n_next = VertexPosition(n_next, neighbor_next_x, neighbor_next_y);

    //                normal += Vector3.Cross(pos_z - vdPos, pos_n_next - vdPos);
    //            }
    //        }
    //        normal.Normalize();
    //        return normal;
    //    }

    //    void AddVertex(ref MapData.VertexData vtex, int x, int y, int layer, int lowestLayer, bool layerIsWater)
    //    {
    //        // color
    //        if (vtex.textureIndex == layer || layer == lowestLayer || (layerIsWater && vtex.water > 0))
    //        {
    //            colorCache[cacheIndexs[colorCacheIndex]++] = (color_white);
    //        }
    //        else
    //        {
    //            colorCache[cacheIndexs[colorCacheIndex]++] = (color_black);
    //        }
    //        uvCache[cacheIndexs[uvCacheIndex]++] = (VertexUV(vtex, x, y));

    //        if (layerIsWater)
    //        {
    //            vertexCache[cacheIndexs[vertexCacheIndex]++] = (VertexWaterPosition(vtex, x, y) - position);
    //            normalCache[cacheIndexs[normalCacheIndex]++] = (Vector3.up);

    //        }
    //        else
    //        {
    //            vertexCache[cacheIndexs[vertexCacheIndex]++] = (VertexPosition(vtex, x, y) - position);
    //            normalCache[cacheIndexs[normalCacheIndex]++] = (VertexNormal(vtex, x, y));
    //        }
    //    }

    //    public void Execute()
    //    {
    //        triangleCacheIndex = 0;
    //        vertexCacheIndex = 1;
    //        normalCacheIndex = 2;
    //        uvCacheIndex = 3;
    //        colorCacheIndex = 4;

    //        int _vertex_x_max = quadBounds.x + 1;
    //        int _vertex_y_max = quadBounds.y + 1;
    //        int maxCount = _vertex_x_max * _vertex_y_max;

    //        byte bLayer = (byte)layer;
    //        int x_end = startCoords.x + quadBounds.x;
    //        int y_end = startCoords.y + quadBounds.y;
    //        int maxIndex = 0;
    //        for (int x = startCoords.x; x < x_end; x += lodL)
    //        {
    //            xIndexCache[0] = yIndexCache[0];
    //            xIndexCache[1] = 0;

    //            for (int k = 0; k < diffyIndexCache.Length; k++)
    //                diffyIndexCache[k] = false;

    //            for (int y = startCoords.y; y < y_end; y += lodL)
    //            {

    //                int xL = x + lodL;
    //                int yL = y + lodL;
    //                MapData.VertexData start0 = vertexMap[x + vertex_x_max * y];
    //                MapData.VertexData start1 = vertexMap[xL + vertex_x_max * y];
    //                MapData.VertexData start2 = vertexMap[xL + vertex_x_max * yL];
    //                MapData.VertexData start3 = vertexMap[x + vertex_x_max * yL];

    //                int normalY = y - startCoords.y;

    //                if ((!layerIsWater && start0.textureIndex != bLayer && start1.textureIndex != bLayer && start2.textureIndex != bLayer && start3.textureIndex != bLayer) ||
    //                    (layerIsWater && start0.water == 0 && start1.water == 0 && start2.water == 0 && start3.water == 0))
    //                {
    //                    xIndexCache[0] = 0;
    //                    xIndexCache[1] = 0;
    //                    continue;
    //                }

    //                int lowestLayer = layerIsWater ? 0 : Math.Min(Math.Min(Math.Min(start0.textureIndex, start1.textureIndex), start2.textureIndex), start3.textureIndex);

    //                int index0 = xIndexCache[0];
    //                if (index0 == 0)
    //                {
    //                    index0 = yIndexCache[normalY];
    //                    if (index0 == 0)
    //                    {
    //                        AddVertex(ref start0, x, y, layer, lowestLayer, layerIsWater);
    //                        index0 = maxIndex++;
    //                    }
    //                    else
    //                    {
    //                        if (lowestLayer == layer)
    //                        {
    //                            colorCache[index0] = color_white;
    //                        }
    //                    }
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index0] = color_white;
    //                    }
    //                }


    //                int index1 = xIndexCache[1];
    //                if (index1 == 0)
    //                {
    //                    AddVertex(ref start1, xL, y, layer, lowestLayer, layerIsWater);
    //                    index1 = maxIndex++;
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index1] = color_white;
    //                    }
    //                }

    //                yIndexCache[normalY] = index1;
    //                diffyIndexCache[normalY] = true;
    //                AddVertex(ref start2, xL, yL, layer, lowestLayer, layerIsWater);
    //                int index2 = maxIndex++;
    //                xIndexCache[1] = index2;


    //                int index3 = yIndexCache[normalY + lodL];
    //                if (index3 == 0)
    //                {
    //                    AddVertex(ref start3, x, yL, layer, lowestLayer, layerIsWater);
    //                    index3 = maxIndex++;
    //                }
    //                else
    //                {
    //                    if (lowestLayer == layer)
    //                    {
    //                        colorCache[index3] = color_white;
    //                    }
    //                }

    //                xIndexCache[0] = index3;
    //                yIndexCache[normalY + lodL] = index2;
    //                diffyIndexCache[normalY + lodL] = true;

    //                triangleCache[triangleCacheIndex++] = (index0);
    //                triangleCache[triangleCacheIndex++] = (index2);
    //                triangleCache[triangleCacheIndex++] = (index3);
    //                triangleCache[triangleCacheIndex++] = (index1);
    //                triangleCache[triangleCacheIndex++] = (index2);
    //                triangleCache[triangleCacheIndex++] = (index0);
    //            }

    //            for (int k = 0; k < diffyIndexCache.Length; ++k)
    //            {
    //                if (!diffyIndexCache[k])
    //                    yIndexCache[k] = 0;
    //            }
    //        }
    //    }
    //}

}
