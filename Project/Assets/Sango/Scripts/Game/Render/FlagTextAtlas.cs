using System.Collections.Generic;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 旗面文字共享图集。
    ///
    /// 【为什么需要】
    /// 原先 <see cref="FlagRender"/> 每面旗都调用 <c>TextFactory.GetTexture(字, 40)</c>，
    /// 而 TextFactory 对"每个字符串"都会新建一张 64×64 的 RenderTexture。
    /// 地图上几百面旗 → 几百张不同的纹理 → 无论材质怎么优化都不可能合批，
    /// 每面旗都必然是一次独立的 SetPass。
    ///
    /// 这里把字符统一烘进一张 1024×1024 的图集，旗帜只需要记录"该字占哪一格"的 UV 矩形，
    /// 全场旗帜因此可以共享同一张 _TextTex，从而进入同一个 SRP Batcher 批次。
    ///
    /// 【为什么用 Texture2D 而不是 RenderTexture】
    /// 1. 不能用 Camera 直接渲染到图集：Camera.rect 会把 Canvas 的像素尺寸一起改掉，字会变形错位。
    /// 2. 不能只清图集的一格：CommandBuffer.ClearRenderTarget 在实际图形 API 上并不严格受
    ///    SetViewport 约束，会把整张图集清成不透明黑 —— 表现就是全场旗面变成一块黑板。
    /// 3. CopyTexture 的 dstY 到底是从底部还是顶部算，在不同平台上有歧义，一旦搞反，
    ///    旗面就是"有图集但采样到空格子"。
    /// 所以这里走最笨但最确定的路：把单字 RT 读回 CPU（64×64），再用 SetPixels 写进 Texture2D
    /// 的指定区域。SetPixels 与 UV 使用同一套左下角原点约定，不存在任何方向歧义。
    ///
    /// 【代价】每新增一个不同的字要整张上传一次图集（1024×1024 half ≈ 8MB），
    /// 但字符集合很小（势力名/部队名首字，实际几十个），且全部发生在加载期，可以接受。
    /// </summary>
    public static class FlagTextAtlas
    {
        /// <summary>单格边长（与 TextFactory 内部创建的 RT 尺寸保持一致）</summary>
        public const int CellSize = 64;

        /// <summary>横向格数</summary>
        public const int Columns = 16;

        /// <summary>纵向格数</summary>
        public const int Rows = 16;

        /// <summary>总格数</summary>
        public const int TotalCells = Columns * Rows;

        /// <summary>共享文字图集（Texture2D，与 UV 同为左下原点约定）</summary>
        static Texture2D atlas;

        /// <summary>字符 → 图集 UV 矩形（xy = 偏移，zw = 尺寸）</summary>
        static readonly Dictionary<string, Vector4> rects = new Dictionary<string, Vector4>(128);

        /// <summary>尚未渲染完成的单字 RT → 目标格子</summary>
        static readonly Dictionary<RenderTexture, Vector4> pendingRects = new Dictionary<RenderTexture, Vector4>(32);

        /// <summary>回读单字 RT 用的中转纹理（64×64）</summary>
        static Texture2D readback;

        static bool initialized;

        /// <summary>只提示一次，避免刷屏</summary>
        static bool warnedFull;

        /// <summary>预留在图集末尾的纯白格，供 headbar 自绘的"纯色部件"采样（避免为此再开一张纹理）</summary>
        static Vector4 whiteRect;

        /// <summary>可用格数 = 总格数 - 1（最后一格留给纯白）</summary>
        const int UsableCells = TotalCells - 1;

        /// <summary>纯白格的 UV 矩形</summary>
        public static Vector4 WhiteRect
        {
            get
            {
                EnsureInit();
                return whiteRect;
            }
        }

        /// <summary>
        /// 取单个汉字/数字在图集中的 UV 矩形。
        /// headbar 自绘的文字是逐字生成四边形的，所以这里按"单个 char"取格，
        /// 与 <see cref="GetRect"/>（取首字）互不影响。
        /// </summary>
        public static Vector4 GetCharRect(char c)
        {
            EnsureInit();

            if (c == '\0' || c == ' ')
                return Vector4.zero;

            string key = c.ToString();
            Vector4 rect;
            if (rects.TryGetValue(key, out rect))
                return rect;

            return Reserve(key);
        }

        /// <summary>共享文字图集（首次访问时创建）</summary>
        public static Texture2D Atlas
        {
            get
            {
                EnsureInit();
                return atlas;
            }
        }

        /// <summary>
        /// 取某个字符在图集中的 UV 矩形（xy = 偏移，zw = 尺寸）。
        /// 未收录的字符会即时分配一格并触发 TextFactory 渲染。
        /// 空字符串返回零矩形（调用方应改用 Texture2D.blackTexture，不要用它采样）。
        /// </summary>
        /// <param name="text">旗面文字（只取首字，与调用方原有口径一致）</param>
        public static Vector4 GetRect(string text)
        {
            EnsureInit();

            if (string.IsNullOrEmpty(text))
                return Vector4.zero;

            string key = text.Substring(0, 1);
            Vector4 rect;
            if (rects.TryGetValue(key, out rect))
                return rect;

            return Reserve(key);
        }

        static Vector4 Reserve(string key)
        {
            if (rects.Count >= UsableCells)
            {
                if (!warnedFull)
                {
                    warnedFull = true;
                    Sango.Log.Warning($"*[旗帜]文字图集已满({UsableCells} 格)，后续旗面文字将不再显示");
                }
                return Vector4.zero;
            }

            int index = rects.Count;
            int cx = index % Columns;
            int cy = index / Columns;
            Vector4 rect = new Vector4(
                (float)cx / Columns,
                (float)cy / Rows,
                1f / Columns,
                1f / Rows);

            rects.Add(key, rect);

            if (TextFactory.Instance != null)
            {
                // TextFactory 的返回类型是 Texture，实际对象一定是它内部 new 出来的 RenderTexture。
                // 此刻它还没渲染（内容是未定义的），所以只登记、不拷贝，
                // 等 onTextureRendered 回来再写进图集。
                RenderTexture src = TextFactory.Instance.GetTexture(key, 40) as RenderTexture;
                if (src != null)
                    pendingRects[src] = rect;
            }

            return rect;
        }

        static void EnsureInit()
        {
            if (initialized)
                return;

            initialized = true;

            atlas = new Texture2D(Columns * CellSize, Rows * CellSize, TextureFormat.RGBAHalf, false, true);
            atlas.name = "FlagTextAtlas";

            // 【必须双线性】格子是 64px 的字形，而头顶条的文字通常只有 30px 上下，
            // 用 Point 采样等于每隔几行丢一行，汉字细笔画会被打断成一串点（实测就是这样）。
            // 图集没有 mipmap（有的话细笔画的纯白格会与邻格渗色），双线性足够覆盖 2 倍以内的缩小。
            atlas.filterMode = FilterMode.Bilinear;
            atlas.wrapMode = TextureWrapMode.Clamp;

            // 整张图集初始化为全透明（Color32 全 0 即 (0,0,0,0)，比 Color[] 省一半内存）
            atlas.SetPixels32(new Color32[atlas.width * atlas.height]);
            atlas.Apply(false);

            readback = new Texture2D(CellSize, CellSize, TextureFormat.RGBAFloat, false, true);

            // 预留最后一格填成不透明纯白：headbar 自绘里的"纯色部件"（底框、血条底、
            // 势力色块等）都采样这一格，再靠顶点色染成目标颜色，
            // 这样纯色部件与文字能共用同一张图集、进同一个 Draw。
            int whiteIndex = TotalCells - 1;
            int wx = whiteIndex % Columns;
            int wy = whiteIndex / Columns;
            whiteRect = new Vector4(
                (float)wx / Columns,
                (float)wy / Rows,
                1f / Columns,
                1f / Rows);

            Color[] white = new Color[CellSize * CellSize];
            for (int i = 0; i < white.Length; i++)
                white[i] = Color.white;
            atlas.SetPixels(wx * CellSize, wy * CellSize, CellSize, CellSize, white);
            atlas.Apply(false);

            TextFactory.onTextureRendered += OnTextRendered;

            Sango.Log.Info($"*[旗帜]文字图集已创建 {atlas.width}x{atlas.height} ({Columns}x{Rows}格)");
        }

        static void OnTextRendered(RenderTexture tex)
        {
            if (tex == null)
                return;

            Vector4 rect;
            if (pendingRects.TryGetValue(tex, out rect))
            {
                CopyCell(tex, rect);
                pendingRects.Remove(tex);
            }
        }

        /// <summary>
        /// 把单字 RT 读回 CPU 再写进图集指定格。
        /// 全程使用左下角原点约定：ReadPixels 的 rect、GetPixels 的顺序、SetPixels 的目标区域、
        /// 以及最终 shader 采样的 UV，四者一致。
        /// </summary>
        static void CopyCell(RenderTexture src, Vector4 rect)
        {
            if (atlas == null || readback == null || src == null)
                return;

            if (src.width != CellSize || src.height != CellSize)
            {
                Sango.Log.Warning($"*[旗帜]文字贴图尺寸异常({src.width}x{src.height})，期望 {CellSize}x{CellSize}，已跳过");
                return;
            }

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = src;

            readback.ReadPixels(new Rect(0, 0, CellSize, CellSize), 0, 0, false);
            readback.Apply(false);

            RenderTexture.active = prev;

            int cx = Mathf.RoundToInt(rect.x * Columns);
            int cy = Mathf.RoundToInt(rect.y * Rows);

            // 【不能把 RGB 刷成纯白】TextFactory 里那行 UI Text 上挂着描边组件
            // （m_OutlineColor = 黑色，m_OutlineWidth = 1），字形是"白字 + 黑描边"两套顶点。
            // 一旦强行刷白，描边也会变成白色，头顶条的字就成了胖一圈的白色块，
            // 与 uGUI 的"白字黑边"完全不同。
            // 现在原样保留：白字乘顶点色照常染色，黑描边乘任何颜色仍是黑色，正好与 uGUI 一致；
            // 旗面文字走的是 lerp(rgb, col.rgb, alpha)，保留原色也等于恢复图集化之前的观感。
            Color[] pixels = readback.GetPixels();

            atlas.SetPixels(cx * CellSize, cy * CellSize, CellSize, CellSize, pixels);
            atlas.Apply(false);
        }
    }
}
