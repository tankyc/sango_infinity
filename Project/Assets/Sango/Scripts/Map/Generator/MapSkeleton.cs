using System;
using System.IO;

namespace Sango.MapGen
{
    /// <summary>
    /// 原版地图"骨架"：从已有的地图 .bin 中提取真实的中国地形形貌与逐顶点高度/水面，
    /// 作为程序化生成的"骨架"使用（地表细节、城市、道路、领地仍由生成器重绘）。
    ///
    /// 为什么可以直接用：原版中国图是 200×200 六角格，被放在 256×256 的世界网格里，
    /// 中国区域之外的格子地形值统一为 <see cref="OutsideTerrain"/>（"域外"哨兵值），
    /// 因此扫描非该值的边界框即可定位中国区域。
    ///
    /// 文件布局（与 MapRender.SaveMap / MapGrid.OnSave / MapData.OnSave 一一对应）：
    ///   VERSION(int) / WorkContent(string) / mapWidth(int) / mapHeight(int)
    ///   / gridSize(int) / 每格(terrainType byte, terrainState int, areaId ushort)
    ///   / gridTextureName(string)
    ///   / quadSize(int) / 每顶点(height byte, textureIndex byte, water byte)
    /// </summary>
    public class MapSkeleton
    {
        /// <summary>"域外"地形哨兵值(原版世界网格里中国区域之外的格子)</summary>
        public const int OutsideTerrain = 32;

        /// <summary>默认的 quadSize(顶点间距的一半)，用于把像素尺寸换算成格数</summary>
        private const int DefaultQuadSize = 5;

        /// <summary>是否解析成功</summary>
        public bool valid;

        /// <summary>失败原因或概要信息(中文)</summary>
        public string message = "";

        /// <summary>来源文件完整路径</summary>
        public string path = "";

        /// <summary>源地图格数</summary>
        public int srcCellsX;
        public int srcCellsY;

        /// <summary>源地图顶点数</summary>
        public int srcVertX;
        public int srcVertY;

        /// <summary>有效区域(中国区域)起点；自动探测得到</summary>
        public int offX;
        public int offY;

        /// <summary>有效区域尺寸</summary>
        public int spanX;
        public int spanY;

        /// <summary>有效区域内的地形类型，下标为 [cx - offX, cy - offY]</summary>
        public byte[,] terrain;

        /// <summary>源地图逐顶点高度字节，线性下标 x * srcVertY + y</summary>
        public byte[] height;

        /// <summary>源地图逐顶点水面高度字节，线性下标 x * srcVertY + y</summary>
        public byte[] water;

        /// <summary>
        /// 从 .bin 读取骨架。offX/offY 或 spanX/spanY 传 0 表示自动探测有效区域。
        /// </summary>
        /// <param name="filePath">文件路径；相对路径按 Content 目录解析</param>
        /// <param name="forceOffX">强制指定起点 X；0 表示自动</param>
        /// <param name="forceOffY">强制指定起点 Y；0 表示自动</param>
        /// <returns>骨架对象；失败时 valid=false 且 message 说明原因</returns>
        public static MapSkeleton Load(string filePath, int forceOffX = 0, int forceOffY = 0)
        {
            MapSkeleton sk = new MapSkeleton();
            if (string.IsNullOrEmpty(filePath))
            {
                sk.message = "未指定骨架地图路径";
                return sk;
            }

            string fullPath = ResolvePath(filePath);
            sk.path = fullPath;
            if (!File.Exists(fullPath))
            {
                sk.message = "骨架地图不存在：" + fullPath;
                return sk;
            }

            try
            {
                using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fs))
                {
                    int version = reader.ReadInt32();
                    string workContent = reader.ReadString();
                    int mapWidth = reader.ReadInt32();
                    int mapHeight = reader.ReadInt32();
                    int gridSize = reader.ReadInt32();

                    if (gridSize <= 0)
                    {
                        sk.message = "骨架地图的 gridSize 非法：" + gridSize;
                        return sk;
                    }

                    // 格数 = 世界尺寸 / gridSize，世界尺寸 = 像素尺寸 * quadSize(默认 5)
                    sk.srcCellsX = mapWidth * DefaultQuadSize / gridSize;
                    sk.srcCellsY = mapHeight * DefaultQuadSize / gridSize;
                    if (sk.srcCellsX <= 0 || sk.srcCellsY <= 0)
                    {
                        sk.message = "骨架地图尺寸非法";
                        return sk;
                    }

                    // 读取地格：terrainType(byte) + terrainState(int) + areaId(ushort)
                    long cellBytes = (long)sk.srcCellsX * sk.srcCellsY * 7;
                    byte[] gridRaw = reader.ReadBytes((int)cellBytes);
                    if (gridRaw.Length < cellBytes)
                    {
                        sk.message = "骨架地图地格数据不完整";
                        return sk;
                    }

                    reader.ReadString();          // gridTextureName
                    reader.ReadInt32();            // quadSize

                    sk.srcVertX = mapWidth + 1;
                    sk.srcVertY = mapHeight + 1;
                    long vertexCount = (long)sk.srcVertX * sk.srcVertY;

                    sk.height = new byte[vertexCount];
                    sk.water = new byte[vertexCount];
                    for (long i = 0; i < vertexCount; i++)
                    {
                        sk.height[i] = reader.ReadByte();
                        reader.ReadByte();        // textureIndex(骨架不使用，由生成器重绘)
                        sk.water[i] = reader.ReadByte();
                    }

                    // 用整张地图的地形做一个临时二维表，便于探测边界框
                    byte[,] full = new byte[sk.srcCellsX, sk.srcCellsY];
                    for (int x = 0; x < sk.srcCellsX; x++)
                    {
                        for (int y = 0; y < sk.srcCellsY; y++)
                        {
                            full[x, y] = gridRaw[(x * sk.srcCellsY + y) * 7];
                        }
                    }

                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
                    for (int x = 0; x < sk.srcCellsX; x++)
                    {
                        for (int y = 0; y < sk.srcCellsY; y++)
                        {
                            if (full[x, y] == OutsideTerrain)
                                continue;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }

                    bool detected = minX <= maxX && minY <= maxY;
                    sk.offX = forceOffX > 0 ? forceOffX : (detected ? minX : 0);
                    sk.offY = forceOffY > 0 ? forceOffY : (detected ? minY : 0);
                    sk.spanX = detected ? (maxX - minX + 1) : sk.srcCellsX;
                    sk.spanY = detected ? (maxY - minY + 1) : sk.srcCellsY;

                    // 约束到地图范围内
                    if (sk.offX + sk.spanX > sk.srcCellsX) sk.spanX = sk.srcCellsX - sk.offX;
                    if (sk.offY + sk.spanY > sk.srcCellsY) sk.spanY = sk.srcCellsY - sk.offY;
                    if (sk.spanX <= 0 || sk.spanY <= 0)
                    {
                        sk.message = "骨架地图的有效区域为空";
                        return sk;
                    }

                    sk.terrain = new byte[sk.spanX, sk.spanY];
                    for (int x = 0; x < sk.spanX; x++)
                    {
                        for (int y = 0; y < sk.spanY; y++)
                        {
                            sk.terrain[x, y] = full[sk.offX + x, sk.offY + y];
                        }
                    }

                    sk.valid = true;
                    sk.message = string.Format(
                        "骨架就绪：{0} v{1} {2}×{3}格 有效区域[{4},{5}] {6}×{7}",
                        System.IO.Path.GetFileName(fullPath), version, sk.srcCellsX, sk.srcCellsY,
                        sk.offX, sk.offY, sk.spanX, sk.spanY);
                    return sk;
                }
            }
            catch (Exception e)
            {
                sk.message = "骨架地图解析失败：" + e.Message;
                return sk;
            }
        }

        /// <summary>
        /// 取有效区域内某格的地形；越界返回 <see cref="OutsideTerrain"/>。
        /// </summary>
        /// <param name="cx">源地图格 X</param>
        /// <param name="cy">源地图格 Y</param>
        /// <returns>地形类型ID；域外为 32</returns>
        public int TerrainAt(int cx, int cy)
        {
            int x = cx - offX;
            int y = cy - offY;
            if (x < 0 || y < 0 || x >= spanX || y >= spanY)
                return OutsideTerrain;
            return terrain[x, y];
        }

        /// <summary>
        /// 取有效区域内某格中心的顶点高度字节(与引擎的格中心公式一致)；
        /// 越界返回 0。
        /// </summary>
        /// <param name="cx">源地图格 X</param>
        /// <param name="cy">源地图格 Y</param>
        /// <returns>高度字节</returns>
        public int VertexHeightAtCell(int cx, int cy)
        {
            if (cx < offX || cy < offY || cx >= offX + spanX || cy >= offY + spanY)
                return 0;
            int vx = cx * 4 + 2;
            int vy = cy * 4 + (cx & 1) * 2 + 2;
            if (vx >= srcVertX || vy >= srcVertY)
                return 0;
            return height[vx * srcVertY + vy];
        }

        /// <summary>
        /// 解析相对路径。依次尝试：Content 目录 → 其上一级(Build 根，mod 通常在这一级)。
        /// </summary>
        /// <param name="filePath">原始路径</param>
        /// <returns>第一个存在的完整路径；都不存在时返回 Content 目录下的候选路径</returns>
        private static string ResolvePath(string filePath)
        {
            if (System.IO.Path.IsPathRooted(filePath))
                return filePath;

            string root = Sango.Path.ContentRootPath;
            if (string.IsNullOrEmpty(root))
                return filePath;

            string candidate = root + "/" + filePath;
            if (File.Exists(candidate))
                return candidate;

            // mod 通常位于 Content 的上一级(Build 根)
            string parentCandidate = root + "/../" + filePath;
            if (File.Exists(parentCandidate))
                return System.IO.Path.GetFullPath(parentCandidate).Replace('\\', '/');

            return candidate;
        }
    }
}
