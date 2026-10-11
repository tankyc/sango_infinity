namespace Sango.MapGen
{
    /// <summary>
    /// 六边形地格的邻居工具。
    ///
    /// 邻居偏移通过引擎的立方体坐标(Hexagon.Coord)换算得到，
    /// 保证与游戏逻辑中的邻居定义完全一致（odd-q 排列，偏移依赖列奇偶）。
    /// </summary>
    public static class HexNeighbors
    {
        /// <summary>六边形邻居方向数</summary>
        public const int DirectionCount = 6;

        /// <summary>邻居偏移表：[列奇偶, 方向, {dx, dy}]</summary>
        private static readonly int[,,] OffsetTable = BuildTable();

        /// <summary>构建邻居偏移表</summary>
        private static int[,,] BuildTable()
        {
            int[,,] table = new int[2, DirectionCount, 2];

            for (int parity = 0; parity < 2; parity++)
            {
                Sango.Hexagon.Hex cube = Sango.Hexagon.Coord.OffsetToCube(parity, 0);
                for (int d = 0; d < DirectionCount; d++)
                {
                    Sango.Hexagon.Hex neighbor = cube.Neighbor(d);
                    Sango.Hexagon.Coord offset = Sango.Hexagon.Coord.OffsetFromCube(neighbor);
                    table[parity, d, 0] = offset.col - parity;
                    table[parity, d, 1] = offset.row;
                }
            }

            return table;
        }

        /// <summary>
        /// 取得指定方向在给定列奇偶下的偏移量。
        /// </summary>
        /// <param name="parity">列奇偶(0 偶列 / 1 奇列)</param>
        /// <param name="dir">方向(0~5)</param>
        /// <param name="dx">输出的 X 偏移</param>
        /// <param name="dy">输出的 Y 偏移</param>
        public static void GetOffset(int parity, int dir, out int dx, out int dy)
        {
            dx = OffsetTable[parity, dir, 0];
            dy = OffsetTable[parity, dir, 1];
        }

        /// <summary>
        /// 计算邻居地格的线性下标；越界时返回 -1。
        /// </summary>
        /// <param name="x">当前地格 X</param>
        /// <param name="y">当前地格 Y</param>
        /// <param name="dir">方向(0~5)</param>
        /// <param name="cellsX">地格宽度</param>
        /// <param name="cellsY">地格高度</param>
        /// <param name="nx">输出的邻居 X</param>
        /// <param name="ny">输出的邻居 Y</param>
        /// <returns>邻居线性下标(x * cellsY + y)，越界返回 -1</returns>
        public static int GetNeighbor(int x, int y, int dir, int cellsX, int cellsY, out int nx, out int ny)
        {
            int parity = x & 1;
            nx = x + OffsetTable[parity, dir, 0];
            ny = y + OffsetTable[parity, dir, 1];
            if (nx < 0 || ny < 0 || nx >= cellsX || ny >= cellsY)
                return -1;
            return nx * cellsY + ny;
        }

        /// <summary>
        /// 计算两个地格之间的六边形距离(格数)。
        /// </summary>
        /// <param name="ax">起点 X</param>
        /// <param name="ay">起点 Y</param>
        /// <param name="bx">终点 X</param>
        /// <param name="by">终点 Y</param>
        /// <returns>六边形距离</returns>
        public static int Distance(int ax, int ay, int bx, int by)
        {
            Sango.Hexagon.Hex a = Sango.Hexagon.Coord.OffsetToCube(ax, ay);
            Sango.Hexagon.Hex b = Sango.Hexagon.Coord.OffsetToCube(bx, by);
            return a.Distance(b);
        }
    }
}
