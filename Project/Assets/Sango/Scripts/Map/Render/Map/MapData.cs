using Sango.Data;
using System;
using System.IO;
using UnityEngine;
using Sango;
using static Sango.Render.MapGrid;

namespace Sango.Render
{
    public class MapData : MapProperty
    {
        public static Vector2Int[] NeighborVertex = new Vector2Int[] {
            new Vector2Int(1, 0),
            new Vector2Int(1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, -1),
            new Vector2Int(0, -1),
        };

        public struct VertexData
        {
            public byte height;
            public byte textureIndex;
            public byte water;
            public byte waterIndex;
        }
        private Vector2Int _bounds;
        public int quadSize = 5;
        public VertexData[][] vertexDatas;
        public string bin_path = "";

        public MapData(MapRender map) : base(map)
        {

        }
        public override void Init()
        {
            base.Init();
            Create(map.mapWidth, map.mapHeight);
        }

        public override void Clear()
        {
            base.Clear();
            vertexDatas = null;
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(quadSize);

            //writer.Write(_bounds.x);
            //writer.Write(_bounds.y);

            for (int x = 0; x < vertexDatas.Length; x++)
            {
                VertexData[] yTable = vertexDatas[x];
                for (int y = 0; y < yTable.Length; y++)
                {
                    VertexData data = yTable[y];
                    writer.Write(data.height);
                    writer.Write(data.textureIndex);
                    writer.Write(data.water);
                }
            }
        }

        internal override void OnSaveScale(BinaryWriter writer, int scale)
        {
            writer.Write(quadSize);

            //writer.Write(_bounds.x);
            //writer.Write(_bounds.y);

            int x_scale_length = ((vertexDatas.Length - 1) * scale + 1);
            for (int x = 0; x < x_scale_length; x++)
            {
                VertexData[] yTable = vertexDatas[x/2];
                int y_scale_length = ((yTable.Length - 1) * scale + 1);
                for (int y = 0; y < y_scale_length; y++)
                {
                    VertexData data = yTable[y/2];
                    writer.Write(data.height);
                    writer.Write(data.textureIndex);
                    writer.Write(data.water);
                }
            }
        }


        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            if(versionCode <= 2) {
                quadSize = 5;
            }
            else {
                quadSize = (int)reader.ReadInt32();
            }
           
            for (int x = 0; x < vertexDatas.Length; x++)
            {
                VertexData[] yTable = vertexDatas[x];
                for (int y = 0; y < yTable.Length; y++)
                {
                    VertexData data = new VertexData()
                    {
                        height = reader.ReadByte(),
                        textureIndex = reader.ReadByte(),
                        water = reader.ReadByte(),
                    };
                    //TODO: 测试完删除
                    //data.height = 0;
                    //TODO: 测试完删除
                    //data.textureIndex = 0;

                    yTable[y] = data;
                }
            }
            UpdateRender();
        }

        public static int[] color_map = new int[]
                {
                129,190,225,
                170,22,255,
                127,115,78,
                164,149,108,
                186,176,130,
                39,146,130,
                142,142,142,
                147,142,129,
                171,171,157,
                36,95,123,
                73,122,140,
                215,95,43,
                123,36,114,
                212,204,149,
                221,147,3,
                59,185,65,
                161,98,88,
                255,128,128,
                191,148,123,
                212,208,203,
                255,179,167,
                166,100,152,
                91,90,85,
                95,157,66,
                100,140,160,
                249,73,62,
                146,74,132,
                175,134,64,
                228,188,84,
                179,218,143,
                35,120,18,
                195,173,118,
                133,174,107,
                254,255,190,
                254,255,230,
                0,0,0
                };

        public static byte color_is_layer(int r, int g, int b)
        {
            if (color_map == null) return 0;
            for (int i = 0, count = color_map.Length / 3; i < count; ++i)
            {
                int begin = i * 3;
                if (color_map[begin] == r && color_map[begin + 1] == g && color_map[begin + 2] == b)
                {
                    if (i == 35)
                    {
                        return 0;
                    }
                    return (byte)i;
                }
            }
            Debug.LogError(string.Format("没找到颜色索引!: r:{0}, g:{1}, b{2}", r, g, b));
            return 0;
        }
        public static Color32 get_layer_color(int layer)
        {
            if (color_map == null || layer >= color_map.Length) return Color.black;
            return new Color32((byte)color_map[layer * 3], (byte)color_map[layer * 3 + 1], (byte)color_map[layer * 3 + 2], 255);
        }
        public void LoadFromBMP(int w, int h, string heightFileName, string layerFileName, string waterFileName)
        {
            _bounds = new Vector2Int(w, h);
            if (w % 4 != 0 || h % 4 != 0)
            {
                return;
            }
            MapUVPiece = new Vector2(1.0f / vertex_width, 1.0f / vertex_height);
            vertex_x_max = w + 1;
            vertex_y_max = h + 1;
            vertexDatas = new VertexData[vertex_x_max][];
            for (int x = 0; x < vertex_x_max; x++)
            {
                vertexDatas[x] = new VertexData[vertex_y_max];
            }

            //// --- 检查数据
            BmpLoader.BmpInfo height_bmpInfo = new BmpLoader.BmpInfo(heightFileName);
            if (!height_bmpInfo.Valid() || height_bmpInfo.width != vertex_x_max || height_bmpInfo.height != vertex_y_max)
            {
                height_bmpInfo.Close();
                return;
            }

            BmpLoader.BmpInfo layer_bmpInfo = new BmpLoader.BmpInfo(layerFileName);
            if (!layer_bmpInfo.Valid() || layer_bmpInfo.width != vertex_x_max || layer_bmpInfo.height != vertex_y_max)
            {
                height_bmpInfo.Close();
                layer_bmpInfo.Close();
                return;
            }

            BmpLoader.BmpInfo water_bmpInfo = new BmpLoader.BmpInfo(waterFileName);
            if (!water_bmpInfo.Valid() || water_bmpInfo.width != bounds.x || water_bmpInfo.height != bounds.y)
            {
                height_bmpInfo.Close();
                layer_bmpInfo.Close();
                water_bmpInfo.Close();
                return;
            }

            height_bmpInfo.BeginRead();
            layer_bmpInfo.BeginRead();
            water_bmpInfo.BeginRead();
            int rw = 255;
            // 地图信息是左上0,0. unity BMP信息是
            for (int y = vertex_y_max - 1; y >= 0; y--)
            {
                for (int x = 0; x < vertex_x_max; x++)
                {

                    int rh;
                    height_bmpInfo.ReadB(out rh);
                    int rl, gl, bl, al;
                    layer_bmpInfo.ReadColor(out rl, out gl, out bl, out al);
                    if (x < vertex_x_max - 1 && y > 0)
                    {
                        water_bmpInfo.ReadB(out rw);
                    }

                    VertexData vetex = new VertexData()
                    {
                        height = (byte)(255 - rh),
                        water = (byte)(255 - rw),
                        //TODO:优化贴图数据，用单通道表示，最大图层255个
                        textureIndex = color_is_layer(rl, gl, bl),
                    };
                    vertexDatas[x][y] = vetex;
                }

                height_bmpInfo.SkipUnusedData();
                layer_bmpInfo.SkipUnusedData();
                water_bmpInfo.SkipUnusedData();
            }
            height_bmpInfo.Close();
            layer_bmpInfo.Close();
            water_bmpInfo.Close();
        }
        public void LoadHeight()
        {
            string[] path = WindowDialog.OpenFileDialog("贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
                string fName = path[0];
                LoadHeight(fName);
            }
        }
        public void LoadHeight(string heightFileName)
        {
            //// --- 检查数据
            BmpLoader.BmpInfo height_bmpInfo = new BmpLoader.BmpInfo(heightFileName);
            if (!height_bmpInfo.Valid())
            {
                height_bmpInfo.Close();
                return;
            }
            int bmpWidth = height_bmpInfo.width;
            int bmpHeight = height_bmpInfo.height;
            int maxWidth = Math.Min(vertex_x_max, height_bmpInfo.width);
            int maxHeight = Math.Min(vertex_y_max, height_bmpInfo.height);
            height_bmpInfo.BeginRead();
            int rw = 255;
            // 地图信息是左上0,0. unity BMP信息是
            for (int y = bmpHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < bmpWidth; x++)
                {

                    int rh;
                    height_bmpInfo.ReadB(out rh);
                    if (x < maxWidth && y < maxHeight)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        vetex.height = (byte)(255 - rh);
                        vertexDatas[x][y] = vetex;
                    }
                }

                height_bmpInfo.SkipUnusedData();
            }
            height_bmpInfo.Close();
            map.mapTerrain.Rebuild();

        }
        public void SaveHeight(string heightFileName)
        {

            string path = WindowDialog.SaveFileDialog("height.bmp", "贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
#if UNITY_STANDALONE_WIN

                System.Drawing.Bitmap bitmapSrc = new System.Drawing.Bitmap(vertex_x_max, vertex_y_max);//获取的位图大小
                for (int y = vertex_y_max - 1; y >= 0; y--)
                {
                    for (int x = 0; x < vertex_x_max; x++)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        byte h = (byte)(255 - vetex.height);
                        bitmapSrc.SetPixel(x, y, System.Drawing.Color.FromArgb(h, h, h));
                    }
                }
                bitmapSrc.Save(path);
#endif

            }
        }
        public void LoadLayer()
        {
            string[] path = WindowDialog.OpenFileDialog("贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
                string fName = path[0];
                LoadLayer(fName);
            }
        }
        public void LoadLayer(string layerFileName)
        {
            //// --- 检查数据
            BmpLoader.BmpInfo layer_bmpInfo = new BmpLoader.BmpInfo(layerFileName);
            if (!layer_bmpInfo.Valid())
            {
                layer_bmpInfo.Close();
                return;
            }
            int bmpWidth = layer_bmpInfo.width;
            int bmpHeight = layer_bmpInfo.height;
            int maxWidth = Math.Min(vertex_x_max, layer_bmpInfo.width);
            int maxHeight = Math.Min(vertex_y_max, layer_bmpInfo.height);
            layer_bmpInfo.BeginRead();
            int rw = 255;
            // 地图信息是左上0,0. unity BMP信息是
            for (int y = bmpHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < bmpWidth; x++)
                {

                    int rl, gl, bl, al;
                    layer_bmpInfo.ReadColor(out rl, out gl, out bl, out al);
                    if (x < maxWidth && y < maxHeight)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        vetex.textureIndex = color_is_layer(rl, gl, bl);
                        vertexDatas[x][y] = vetex;
                    }
                }

                layer_bmpInfo.SkipUnusedData();
            }
            layer_bmpInfo.Close();
            map.mapTerrain.Rebuild();

        }
        public void SaveLayer(string layerFileName)
        {
            string path = WindowDialog.SaveFileDialog("height.bmp", "贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
#if UNITY_STANDALONE_WIN

                System.Drawing.Bitmap bitmapSrc = new System.Drawing.Bitmap(vertex_x_max, vertex_y_max);//获取的位图大小
                for (int y = vertex_y_max - 1; y >= 0; y--)
                {
                    for (int x = 0; x < vertex_x_max; x++)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        Color32 color32 = get_layer_color(vetex.textureIndex);
                        bitmapSrc.SetPixel(x, y, System.Drawing.Color.FromArgb(color32.r, color32.g, color32.b));
                    }
                }
                bitmapSrc.Save(path);
#endif
            }
        }
        public void LoadWater()
        {
            string[] path = WindowDialog.OpenFileDialog("贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
                string fName = path[0];
                LoadWater(fName);
            }
        }
        public void LoadWater(string waterFileName)
        {
            //// --- 检查数据
            BmpLoader.BmpInfo water_bmpInfo = new BmpLoader.BmpInfo(waterFileName);
            if (!water_bmpInfo.Valid())
            {
                water_bmpInfo.Close();
                return;
            }
            int bmpWidth = water_bmpInfo.width;
            int bmpHeight = water_bmpInfo.height;
            int maxWidth = Math.Min(vertex_x_max, water_bmpInfo.width);
            int maxHeight = Math.Min(vertex_y_max, water_bmpInfo.height);
            water_bmpInfo.BeginRead();
            int rw = 255;
            // 地图信息是左上0,0. unity BMP信息是
            for (int y = bmpHeight - 1; y >= 0; y--)
            {
                for (int x = 0; x < bmpWidth; x++)
                {

                    if (x < vertex_x_max - 1 && y > 0)
                    {
                        water_bmpInfo.ReadB(out rw);
                    }
                    if (x < maxWidth && y < maxHeight)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        vetex.water = (byte)(255 - rw);
                        vertexDatas[x][y] = vetex;
                    }
                }

                water_bmpInfo.SkipUnusedData();
            }
            water_bmpInfo.Close();
            map.mapTerrain.Rebuild();
        }
        public void SaveWater(string waterFileName)
        {
            string path = WindowDialog.SaveFileDialog("height.bmp", "贴图文件(*.bmp)|*.bmp\0");
            if (path != null)
            {
#if UNITY_STANDALONE_WIN

                System.Drawing.Bitmap bitmapSrc = new System.Drawing.Bitmap(vertex_x_max, vertex_y_max);//获取的位图大小
                for (int y = vertex_y_max - 1; y >= 0; y--)
                {
                    for (int x = 0; x < vertex_x_max; x++)
                    {
                        VertexData vetex = vertexDatas[x][y];
                        if (x < vertex_x_max - 1 && y > 0)
                        {
                            byte h = (byte)(255 - vetex.water);
                            bitmapSrc.SetPixel(x, y, System.Drawing.Color.FromArgb(h, h, h));
                        }
                    }
                }
                bitmapSrc.Save(path);
#endif
            }
        }
        public int vertex_width
        {
            get { return bounds.x; }
        }
        public int vertex_height
        {
            get { return bounds.y; }
        }
        public int world_width
        {
            get { return bounds.x * quadSize; }
        }
        public int world_height
        {
            get { return bounds.y * quadSize; }
        }
        public Vector2Int bounds
        {
            get { return _bounds; }
            set
            {
                _bounds = value;
                Create(value.x, value.y);
            }
        }
        Vector2 MapUVPiece;
        int vertex_x_max;
        int vertex_y_max;
        public void Create(int w, int h, byte initHeight)
        {
            if (w % 4 != 0 || h % 4 != 0)
            {
                return;
            }
            _bounds = new Vector2Int(w, h);
            MapUVPiece = new Vector2(1.0f / vertex_width, 1.0f / vertex_height);
            vertex_x_max = w + 1;
            vertex_y_max = h + 1;

            vertexDatas = new VertexData[vertex_x_max][];
            for (int x = 0; x < vertex_x_max; x++)
            {
                vertexDatas[x] = new VertexData[vertex_y_max];
                for (int y = 0; y < vertex_y_max; y++)
                {
                    vertexDatas[x][y] = new VertexData()
                    {
                        height = initHeight,
                        water = 0,
                        //TODO:优化贴图数据，用单通道表示，最大图层255个
                        textureIndex = 0//color_is_layer(rl, gl, bl),
                    };
                }
            }
            UpdateRender();

        }

        public void Create(int w, int h)
        {
            Create(w, h, 0);
        }

        public Vector2 VertexUV(VertexData data, int x, int y)
        {
            return new Vector2(x * MapUVPiece.x, y * MapUVPiece.y);
        }
        public Vector3 VertexPosition(VertexData data, int x, int y)
        {
            return new Vector3(y * quadSize, data.height * 0.5f, x * quadSize);
        }
        public Vector3 VertexWaterPosition(VertexData data, int x, int y)
        {
            if (data.water == 0)
            {
                int lx = x - 1;
                if (lx > 0)
                {
                    VertexData d = vertexDatas[lx][y];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }
                }
                int uy = y - 1;
                if (uy > 0)
                {
                    VertexData d = vertexDatas[x][uy];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (lx > 0)
                    {
                        d = vertexDatas[lx][uy];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }

                }
                int rx = x + 1;
                if (rx < vertex_x_max)
                {
                    VertexData d = vertexDatas[rx][y];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (uy > 0)
                    {
                        d = vertexDatas[rx][uy];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }
                }
                int dy = y + 1;
                if (dy < vertex_y_max)
                {
                    VertexData d = vertexDatas[x][dy];
                    if (d.water > 0)
                    {
                        return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                    }

                    if (rx < vertex_x_max)
                    {
                         d = vertexDatas[rx][dy];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }

                    if (lx > 0)
                    {
                        d = vertexDatas[lx][dy];
                        if (d.water > 0)
                        {
                            return new Vector3(y * quadSize, d.water * 0.5f, x * quadSize);
                        }
                    }
                }
            }
            return new Vector3(y * quadSize, data.water * 0.5f, x * quadSize);
        }
        public Vector3 VertexNormal(VertexData data, int x, int y)
        {
            Vector3 vdPos = VertexPosition(data, x, y);
            Vector3 normal = Vector3.zero;

            for (int z = 0; z < 6; z++)
            {
                int next = z + 1;
                if (next == 6)
                    next = 0;

                Vector2Int neighbor_z = NeighborVertex[z];
                int neighbor_z_x = neighbor_z.x + x;
                int neighbor_z_y = neighbor_z.y + y;
                Vector2Int neighbor_next = NeighborVertex[next];
                int neighbor_next_x = neighbor_next.x + x;
                int neighbor_next_y = neighbor_next.y + y;

                if (neighbor_z_x >= 0 && neighbor_z_x < vertex_x_max && neighbor_z_y >= 0 && neighbor_z_y < vertex_y_max &&
                    neighbor_next_x >= 0 && neighbor_next_x < vertex_x_max && neighbor_next_y >= 0 && neighbor_next_y < vertex_y_max)
                {
                    VertexData n_z = vertexDatas[neighbor_z_x][neighbor_z_y];
                    Vector3 pos_z = VertexPosition(n_z, neighbor_z_x, neighbor_z_y);

                    VertexData n_next = vertexDatas[neighbor_next_x][neighbor_next_y];
                    Vector3 pos_n_next = VertexPosition(n_next, neighbor_next_x, neighbor_next_y);

                    normal += Vector3.Cross(pos_z - vdPos, pos_n_next - vdPos);
                }
            }
            normal.Normalize();
            return normal;
        }

        public override void UpdateRender()
        {
            Shader.SetGlobalFloat("_MapWidth", vertex_width * quadSize);
            Shader.SetGlobalFloat("_MapHeight", vertex_height * quadSize);

        }

        public float GetHeight(int x, int y)
        {
            if (x < 0 || x > vertex_width || y < 0 || y > vertex_height)
                return 0;
            return vertexDatas[x][y].height * 0.5f;
        }

        public VertexData GetVertexData(float x, float y)
        {
            int xCount = (int)MathF.Floor((x + 0.5f * quadSize) / (float)quadSize);
            int yCount = (int)MathF.Floor((y + 0.5f * quadSize) / (float)quadSize);
            if (xCount < 0)
                xCount = 0;
            if (xCount > vertex_width)
                xCount = vertex_width;
            if (yCount < 0)
                yCount = 0;
            if (yCount > vertex_height)
                yCount = vertex_height;
            return vertexDatas[xCount][yCount];
        }

        public float GetWorldHeight(Vector3 worldPos)
        {
            VertexData data = GetVertexData(worldPos.z, worldPos.x);
            return data.height * 0.5f;
        }

        public float GetWorldWaterHeight(Vector3 worldPos)
        {
            VertexData data = GetVertexData(worldPos.z, worldPos.x);
            return data.water * 0.5f;
        }
    }
}
