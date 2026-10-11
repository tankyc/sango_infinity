using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static Sango.Render.MapData;

namespace Sango.Map.Render
{
    [BurstCompile]
    public struct MapDataJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<VertexDataNative> Input;

        [ReadOnly]
        public NativeArray<Vector2Int> NeighborVertexs;

        [WriteOnly]
        public NativeArray<VertexDataNative> Output;

        [ReadOnly]
        public Vector2 MapUVPiece;

        [ReadOnly]
        public int vertex_x_max;

        [ReadOnly]
        public int vertex_y_max;

        [ReadOnly]
        public int quadSize;

        int XY2Index(int x, int y)
        {
            // 行跨度必须是「每行顶点数」= vertex_y_max；用 vertex_x_max 会让非正方形地图
            // 的下标发生碰撞（例如 (0,N) 与 (1,0) 同址），并可能越界。
            return x * vertex_y_max + y;
        }

        public void Execute(int index)
        {
            int x = index / vertex_y_max;
            int y = index % vertex_y_max;

            VertexDataNative data = Input[index];
            data.position = new Vector3(y * quadSize, data.height * 0.5f, x * quadSize);
            data.uv = new Vector2(x * MapUVPiece.x, y * MapUVPiece.y);
            data.waterPosition = VertexWaterPosition(data, x, y);
            Output[index] = data;
        }

        public Vector2 VertexUV(VertexDataNative data, int x, int y)
        {
            return new Vector2(x * MapUVPiece.x, y * MapUVPiece.y);
        }
        public Vector3 VertexPosition(VertexDataNative data, int x, int y)
        {
            return new Vector3(y * quadSize, data.height * 0.5f, x * quadSize);
        }
        public Vector3 VertexWaterPosition(VertexDataNative data, int x, int y)
        {
            if (data.water == 0)
            {
                int lx = x - 1;
                if (lx > 0)
                {
                    VertexDataNative d = Input[XY2Index(lx, y)];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }
                }
                int uy = y - 1;
                if (uy > 0)
                {
                    VertexDataNative d = Input[XY2Index(x, uy)];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (lx > 0)
                    {
                        d = Input[XY2Index(lx, uy)];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }

                }
                int rx = x + 1;
                if (rx < vertex_x_max)
                {
                    VertexDataNative d = Input[XY2Index(rx, y)];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (uy > 0)
                    {
                        d = Input[XY2Index(rx, uy)];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }
                }
                int dy = y + 1;
                if (dy < vertex_y_max)
                {
                    VertexDataNative d = Input[XY2Index(x, dy)];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (rx < vertex_x_max)
                    {
                        d = Input[XY2Index(rx, dy)];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }

                    if (lx > 0)
                    {
                        d = Input[XY2Index(lx, dy)];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }
                }
            }
            return new Vector3(y * quadSize, data.water * 0.5f, x * quadSize);
        }
    }

    [BurstCompile]
    public struct MapDataNormalJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<VertexDataNative> Input;

        [ReadOnly]
        public NativeArray<Vector2Int> NeighborVertexs;

        [WriteOnly]
        public NativeArray<VertexDataNative> Output;

        [ReadOnly]
        public Vector2 MapUVPiece;

        [ReadOnly]
        public int vertex_x_max;

        [ReadOnly]
        public int vertex_y_max;

        [ReadOnly]
        public int quadSize;

        int XY2Index(int x, int y)
        {
            // 行跨度必须是「每行顶点数」= vertex_y_max；用 vertex_x_max 会让非正方形地图
            // 的下标发生碰撞（例如 (0,N) 与 (1,0) 同址），并可能越界。
            return x * vertex_y_max + y;
        }

        public void Execute(int index)
        {
            int x = index / vertex_y_max;
            int y = index % vertex_y_max;
            VertexDataNative data = Input[index];
            data.normal = VertexNormal(data, x, y);
            Output[index] = data;
        }
        public Vector3 VertexNormal(VertexDataNative data, int x, int y)
        {
            Vector3 vdPos = data.position;
            Vector3 normal = Vector3.zero;

            for (int z = 0; z < 6; z++)
            {
                int next = z + 1;
                if (next == 6)
                    next = 0;

                Vector2Int neighbor_z = NeighborVertexs[z];
                int neighbor_z_x = neighbor_z.x + x;
                int neighbor_z_y = neighbor_z.y + y;
                Vector2Int neighbor_next = NeighborVertexs[next];
                int neighbor_next_x = neighbor_next.x + x;
                int neighbor_next_y = neighbor_next.y + y;

                if (neighbor_z_x >= 0 && neighbor_z_x < vertex_x_max && neighbor_z_y >= 0 && neighbor_z_y < vertex_y_max &&
                    neighbor_next_x >= 0 && neighbor_next_x < vertex_x_max && neighbor_next_y >= 0 && neighbor_next_y < vertex_y_max)
                {
                    VertexDataNative n_z = Input[XY2Index(neighbor_z_x, neighbor_z_y)];
                    Vector3 pos_z = n_z.position;

                    VertexDataNative n_next = Input[XY2Index(neighbor_next_x, neighbor_next_y)];
                    Vector3 pos_n_next = n_next.position;

                    normal += Vector3.Cross(pos_z - vdPos, pos_n_next - vdPos);
                }
            }
            normal.Normalize();
            return normal;
        }

    }


    [BurstCompile]
    public struct MapCellCreateLayer : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<VertexDataNative> Input;

        [ReadOnly]
        public NativeArray<Vector2Int> NeighborVertexs;

        [WriteOnly]
        public NativeArray<VertexDataNative> Output;

        [ReadOnly]
        public Vector2 MapUVPiece;

        [ReadOnly]
        public int vertex_x_max;

        [ReadOnly]
        public int vertex_y_max;

        [ReadOnly]
        public int quadSize;

        int XY2Index(int x, int y)
        {
            // 行跨度必须是「每行顶点数」= vertex_y_max；用 vertex_x_max 会让非正方形地图
            // 的下标发生碰撞（例如 (0,N) 与 (1,0) 同址），并可能越界。
            return x * vertex_y_max + y;
        }

        public void Execute(int index)
        {
            int x = index / vertex_y_max;
            int y = index % vertex_y_max;
            VertexDataNative data = Input[index];
            data.normal = VertexNormal(data, x, y);
            Output[index] = data;
        }
        public Vector3 VertexNormal(VertexDataNative data, int x, int y)
        {
            Vector3 vdPos = data.position;
            Vector3 normal = Vector3.zero;

            for (int z = 0; z < 6; z++)
            {
                int next = z + 1;
                if (next == 6)
                    next = 0;

                Vector2Int neighbor_z = NeighborVertexs[z];
                int neighbor_z_x = neighbor_z.x + x;
                int neighbor_z_y = neighbor_z.y + y;
                Vector2Int neighbor_next = NeighborVertexs[next];
                int neighbor_next_x = neighbor_next.x + x;
                int neighbor_next_y = neighbor_next.y + y;

                if (neighbor_z_x >= 0 && neighbor_z_x < vertex_x_max && neighbor_z_y >= 0 && neighbor_z_y < vertex_y_max &&
                    neighbor_next_x >= 0 && neighbor_next_x < vertex_x_max && neighbor_next_y >= 0 && neighbor_next_y < vertex_y_max)
                {
                    VertexDataNative n_z = Input[XY2Index(neighbor_z_x, neighbor_z_y)];
                    Vector3 pos_z = n_z.position;

                    VertexDataNative n_next = Input[XY2Index(neighbor_next_x, neighbor_next_y)];
                    Vector3 pos_n_next = n_next.position;

                    normal += Vector3.Cross(pos_z - vdPos, pos_n_next - vdPos);
                }
            }
            normal.Normalize();
            return normal;
        }

    }


}
