namespace Sango.MapGen
{
    /// <summary>
    /// 真实三国城池表：原版 200×200 中国图的格坐标与相邻关系。
    ///
    /// 坐标系：
    ///   原版中国图是 200×200 六角格，被放在 256×256 的世界网格里，起点为 (28, 28)。
    ///   本表的 lo 为"向东"方向、hi 为"向南"方向的原版格坐标，
    ///   与剧本 Scenario.json 的城市坐标满足 x = lo + 28、y = hi + 28（已交叉验证 42/42 完全一致）。
    ///
    /// 换算到本图坐标由 MapGenContext.CellFromSource() 完成（把原版中国区域等比铺满本图）。
    /// </summary>
    public static class ChinaCityTable
    {
        /// <summary>原版中国图在 256×256 世界网格中的起点偏移</summary>
        public const int SourceOffset = 28;

        /// <summary>原版中国图的格数</summary>
        public const int SourceSpan = 200;

        /// <summary>一座真实城池</summary>
        public struct Entry
        {
            /// <summary>城市ID(1 起，与剧本 citySet 的键一致)</summary>
            public int id;
            /// <summary>城池名称</summary>
            public string name;
            /// <summary>原版中国图格坐标(向东)</summary>
            public int lo;
            /// <summary>原版中国图格坐标(向南)</summary>
            public int hi;
            /// <summary>相邻城市ID(原版邻接表)</summary>
            public int[] neighbors;
        }

        /// <summary>42 座真实城池(按ID升序)</summary>
        public static readonly Entry[] All = new Entry[]
        {
            new Entry { id = 1, name = "襄平", lo = 174, hi = 16, neighbors = new int[] { 2 } },
            new Entry { id = 2, name = "北平", lo = 149, hi = 20, neighbors = new int[] { 1, 3, 4 } },
            new Entry { id = 3, name = "蓟", lo = 122, hi = 16, neighbors = new int[] { 2, 4, 6 } },
            new Entry { id = 4, name = "南皮", lo = 138, hi = 37, neighbors = new int[] { 2, 3, 5, 7 } },
            new Entry { id = 5, name = "平原", lo = 129, hi = 54, neighbors = new int[] { 4, 7, 8, 12 } },
            new Entry { id = 6, name = "晋阳", lo = 91, hi = 39, neighbors = new int[] { 3, 7 } },
            new Entry { id = 7, name = "邺", lo = 109, hi = 53, neighbors = new int[] { 4, 5, 6, 12, 13 } },
            new Entry { id = 8, name = "北海", lo = 160, hi = 51, neighbors = new int[] { 5, 9 } },
            new Entry { id = 9, name = "下邳", lo = 156, hi = 77, neighbors = new int[] { 8, 10, 11 } },
            new Entry { id = 10, name = "小沛", lo = 136, hi = 77, neighbors = new int[] { 9, 11, 12, 13 } },
            new Entry { id = 11, name = "寿春", lo = 140, hi = 96, neighbors = new int[] { 9, 10, 15, 23, 26 } },
            new Entry { id = 12, name = "濮阳", lo = 120, hi = 71, neighbors = new int[] { 5, 7, 10, 13 } },
            new Entry { id = 13, name = "陈留", lo = 108, hi = 79, neighbors = new int[] { 7, 10, 12, 14, 16, 17 } },
            new Entry { id = 14, name = "许昌", lo = 99, hi = 92, neighbors = new int[] { 13, 15, 16, 17 } },
            new Entry { id = 15, name = "汝南", lo = 113, hi = 107, neighbors = new int[] { 11, 14, 29 } },
            new Entry { id = 16, name = "洛阳", lo = 79, hi = 76, neighbors = new int[] { 13, 14, 17, 18 } },
            new Entry { id = 17, name = "宛", lo = 77, hi = 92, neighbors = new int[] { 14, 16, 18, 19, 29, 13 } },
            new Entry { id = 18, name = "长安", lo = 53, hi = 73, neighbors = new int[] { 16, 17, 20, 37 } },
            new Entry { id = 19, name = "上庸", lo = 54, hi = 102, neighbors = new int[] { 17, 29, 37 } },
            new Entry { id = 20, name = "安定", lo = 28, hi = 55, neighbors = new int[] { 18, 21, 22 } },
            new Entry { id = 21, name = "天水", lo = 10, hi = 71, neighbors = new int[] { 20, 22, 37 } },
            new Entry { id = 22, name = "武威", lo = 9, hi = 41, neighbors = new int[] { 20, 21 } },
            new Entry { id = 23, name = "建业", lo = 162, hi = 104, neighbors = new int[] { 11, 24, 26, 27 } },
            new Entry { id = 24, name = "吴", lo = 182, hi = 109, neighbors = new int[] { 23, 25 } },
            new Entry { id = 25, name = "会稽", lo = 184, hi = 125, neighbors = new int[] { 24 } },
            new Entry { id = 26, name = "庐江", lo = 146, hi = 118, neighbors = new int[] { 11, 23, 27, 28 } },
            new Entry { id = 27, name = "柴桑", lo = 126, hi = 133, neighbors = new int[] { 23, 26, 28, 31, 32 } },
            new Entry { id = 28, name = "江夏", lo = 115, hi = 121, neighbors = new int[] { 26, 27, 30, 31, 32 } },
            new Entry { id = 29, name = "新野", lo = 87, hi = 109, neighbors = new int[] { 15, 17, 19, 30 } },
            new Entry { id = 30, name = "襄阳", lo = 77, hi = 117, neighbors = new int[] { 28, 29, 31 } },
            new Entry { id = 31, name = "江陵", lo = 82, hi = 137, neighbors = new int[] { 27, 28, 30, 32, 33, 36 } },
            new Entry { id = 32, name = "长沙", lo = 108, hi = 164, neighbors = new int[] { 27, 28, 31, 33, 34 } },
            new Entry { id = 33, name = "武陵", lo = 69, hi = 155, neighbors = new int[] { 31, 32, 35, 36 } },
            new Entry { id = 34, name = "桂阳", lo = 99, hi = 188, neighbors = new int[] { 32, 35 } },
            new Entry { id = 35, name = "零陵", lo = 70, hi = 185, neighbors = new int[] { 33, 34 } },
            new Entry { id = 36, name = "永安", lo = 46, hi = 128, neighbors = new int[] { 31, 33, 39 } },
            new Entry { id = 37, name = "汉中", lo = 27, hi = 89, neighbors = new int[] { 18, 19, 21, 38 } },
            new Entry { id = 38, name = "梓潼", lo = 16, hi = 110, neighbors = new int[] { 37, 39, 40 } },
            new Entry { id = 39, name = "江州", lo = 30, hi = 147, neighbors = new int[] { 36, 38, 40, 41 } },
            new Entry { id = 40, name = "成都", lo = 10, hi = 132, neighbors = new int[] { 38, 39, 41 } },
            new Entry { id = 41, name = "建宁", lo = 27, hi = 174, neighbors = new int[] { 40, 42, 39 } },
            new Entry { id = 42, name = "云南", lo = 5, hi = 191, neighbors = new int[] { 41 } },
        };

        /// <summary>按ID取城池；不存在时返回 null。</summary>
        /// <param name="id">城市ID</param>
        /// <returns>城池条目</returns>
        public static Entry? Get(int id)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].id == id)
                    return All[i];
            }
            return null;
        }
    }
}
