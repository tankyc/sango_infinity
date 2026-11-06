using UnityEngine;

namespace Sango.Render.Terrain314
{
    /// <summary>
    /// 314 六边形地格的坐标换算。
    ///
    /// 【为什么不是正方格】
    ///   Terrain314 的地形数据（高度场、control、植被密度）都是正方采样网格，
    ///   但 314 的世界地格是 flat-top 六边形，两者网格粒度与原点都不同。
    ///   要"对齐 314 的渲染"，必须用原版那套常量与判定分支，不能按正方格近似。
    ///
    /// 【常量出处】
    ///   San14.Data/Rendering/Terrain/CeHexWorldCoordinates.cs:14-21
    ///     Width = 220, Height = 220          // 48400 个物理地格
    ///     OriginX = 37594.0
    ///     ColumnStep = 702.0                 // = 1.5 * 468，flat-top 的水平间距
    ///     InitializedOriginZ = 13335.2998046875
    ///     RowStep = 810.5997924804688        // = sqrt(3) * 468，竖直间距
    ///     EvenColumnRowOffset = 0.5          // 偶数列额外下移半行（odd-q 布局）
    ///
    /// 【几何推导】
    ///   flat-top 六边形外接圆半径 R = 468：
    ///     水平间距 = 1.5R = 702          ✅ 与 ColumnStep 一致
    ///     竖直间距 = sqrt(3)R = 810.5998 ✅ 与 RowStep 一致
    ///   所以地格中心就是 <see cref="Project"/> 的返回值，本类的 GetCorners 按 R 展开。
    ///
    /// 【坐标系】
    ///   与 Terrain314 其余部分一致：世界 +X 为南、+Z 为东，y 由高度场提供。
    ///   注意这套六边形网格只覆盖地形中部的一块（x 约 [37594, 192034]、z 约 [13335, 191667]），
    ///   不是整张 204800 见方的地形。
    /// </summary>
    public static class Terrain314HexGrid
    {
        /// <summary>地格列数。</summary>
        public const int Width = 220;
        /// <summary>地格行数。</summary>
        public const int Height = 220;
        /// <summary>物理地格总数（48400）。</summary>
        public const int CellCount = Width * Height;

        /// <summary>第 0 列中心的世界 X。</summary>
        public const float OriginX = 37594f;
        /// <summary>列间距（= 1.5 × 六边形半径）。</summary>
        public const float ColumnStep = 702f;
        /// <summary>第 0 行中心的世界 Z（偶数列还要再加半行）。</summary>
        public const float OriginZ = 13335.2998046875f;
        /// <summary>行间距（= √3 × 六边形半径）。</summary>
        public const float RowStep = 810.5997924804688f;
        /// <summary>六边形外接圆半径。</summary>
        public const float Radius = 468f;

        /// <summary>列号 → 行号，按 314 的 row-major 物理槽位。</summary>
        public static int GetHexId(int column, int row) => row * Width + column;

        /// <summary>物理槽位 → 列号。</summary>
        public static int GetColumn(int hexId) => hexId % Width;

        /// <summary>物理槽位 → 行号。</summary>
        public static int GetRow(int hexId) => hexId / Width;

        /// <summary>物理槽位是否落在 220×220 表内。</summary>
        public static bool IsValid(int hexId) => (uint)hexId < CellCount;

        /// <summary>
        /// 物理槽位 → 地格中心世界坐标（y 恒为 0，需要高度请配高度场查询）。
        ///
        /// 严格照 314 的 CeHexWorldCoordinates.ProjectIndex 的运算顺序：
        ///   x = column * 702 + 37594
        ///   z = row * sqrt(3)*468 + (column 偶 ? sqrt(3)*468*0.5 : 0) + zOrigin
        ///   zOrigin = sqrt(3)*0.5*468 + 12930 = InitializedOriginZ
        /// </summary>
        public static Vector3 Project(int hexId)
        {
            if (!IsValid(hexId))
                throw new System.ArgumentOutOfRangeException(nameof(hexId),
                    $"六边形槽位越界: {hexId}（有效范围 0..{CellCount - 1}）");

            int column = hexId % Width;
            int row = hexId / Width;

            float x = column * ColumnStep;
            x += OriginX;

            float evenOffset = (column & 1) == 0 ? RowStep * 0.5f : 0f;
            float z = RowStep * row;
            z += evenOffset;
            z += OriginZ;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 世界坐标 → 物理槽位，对齐 314 的 CeHexWorldCoordinates.TryPick。
        ///
        /// 原版不是"取最近中心"，而是把点周围的三个候选角点比距离后再按奇偶列换算槽位，
        /// 并列时的取舍顺序（left ≤ right、upper ≤ left）也是原版写死的，这里照抄。
        /// </summary>
        public static bool TryPick(float worldX, float worldZ, out int hexId)
        {
            hexId = -1;
            float x = worldX - OriginX;
            float z = worldZ - OriginZ;
            float rowStep = RowStep;

            float row = Mathf.Floor(z / rowStep) + 1f;
            float column = Mathf.Floor(x / ColumnStep) + 1f;

            uint r = unchecked((uint)(int)row);
            uint c = unchecked((uint)(int)column);
            if (r > Height || c > Width) return false;

            float leftX = unchecked((int)(c - 1)) * ColumnStep;
            float lowerZ = unchecked((int)(r - 1)) * rowStep;
            float rightX = leftX + ColumnStep;
            bool odd = (c & 1) != 0;
            float leftZ = odd ? rowStep * 0.5f + lowerZ : lowerZ;
            float rightZ = odd ? lowerZ : rowStep * 0.5f + lowerZ;
            float upperX = odd ? rightX : leftX;

            float rightDx = rightX - x, rightDz = rightZ - z;
            float leftDx = leftX - x, leftDz = leftZ - z;
            float upperDx = upperX - x, upperDz = lowerZ + rowStep - z;

            float rightDistance = rightDz * rightDz + rightDx * rightDx;
            float leftDistance = leftDz * leftDz + leftDx * leftDx;
            float upperDistance = upperDz * upperDz + upperDx * upperDx;

            int candidate = leftDistance <= rightDistance
                ? (upperDistance <= leftDistance ? 2 : 1)
                : (rightDistance < upperDistance ? 0 : 2);

            uint basis = r * Width + c;
            uint slot;
            if (candidate == 0)
            {
                if (c == Width) return false;
                slot = unchecked(basis - Width);
            }
            else if (candidate == 1)
            {
                if (c == 0) return false;
                slot = unchecked(basis - Width - 1);
            }
            else if (odd)
            {
                slot = basis;
            }
            else
            {
                if (c == 0) return false;
                slot = unchecked(basis - 1);
            }

            if (slot >= CellCount) return false;
            hexId = (int)slot;
            return true;
        }

        /// <summary>
        /// flat-top 六边形的 6 个角点（世界 XZ，y = 0）。
        ///
        /// flat-top 的顶点落在 0°/60°/…/300°，所以东西方向是尖角、南北方向是平边，
        /// 与 ColumnStep = 1.5R、RowStep = √3R 的间距自洽。
        /// </summary>
        public static void GetCorners(Vector3 center, Vector3[] corners)
        {
            if (corners == null || corners.Length < 6)
                throw new System.ArgumentException("角点数组长度至少为 6", nameof(corners));

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60f * i);
                corners[i] = new Vector3(
                    center.x + Radius * Mathf.Cos(angle),
                    center.y,
                    center.z + Radius * Mathf.Sin(angle));
            }
        }

        /// <summary>
        /// 取与给定格相邻的 6 个槽位（odd-q 布局）。越界的邻居直接跳过。
        /// </summary>
        /// <param name="hexId">中心槽位。</param>
        /// <param name="neighbors">输出缓冲，至少 6 项。</param>
        /// <returns>实际写入的邻居数量。</returns>
        public static int GetNeighbors(int hexId, int[] neighbors)
        {
            if (neighbors == null || neighbors.Length < 6)
                throw new System.ArgumentException("邻居数组长度至少为 6", nameof(neighbors));
            if (!IsValid(hexId)) return 0;

            int column = hexId % Width;
            int row = hexId / Width;
            bool even = (column & 1) == 0;

            // flat-top / odd-q：偶数列的上下邻居在 row-1 与 row+1，奇数列相反
            int[,] offsets = even
                ? new int[,] { { +1, 0 }, { -1, 0 }, { 0, -1 }, { -1, -1 }, { 0, +1 }, { -1, +1 } }
                : new int[,] { { +1, 0 }, { -1, 0 }, { +1, -1 }, { 0, -1 }, { +1, +1 }, { 0, +1 } };

            int count = 0;
            for (int i = 0; i < 6; i++)
            {
                int nc = column + offsets[i, 0];
                int nr = row + offsets[i, 1];
                if (nc < 0 || nc >= Width || nr < 0 || nr >= Height) continue;
                neighbors[count++] = nr * Width + nc;
            }
            return count;
        }
    }
}
