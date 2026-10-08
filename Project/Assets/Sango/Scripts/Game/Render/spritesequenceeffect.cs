using System.Collections.Generic;
using Sango;
using Sango.Loader;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 序列帧特效组件:从1张精灵图集(Sheet)按行列切分帧,逐帧播放,播完自动销毁
    /// 使用 Mobile/Legacy Particles/Additive 加色混合 Shader——黑色像素自动透明(适合黑底特效素材)
    /// </summary>
    public class SpriteSequenceEffect : MonoBehaviour
    {
        static readonly string DefaultPackage = "Content";

        // 按路径加载过的贴图缓存:同一路径只从磁盘读一次,避免每次播放特效都同步读盘+GPU回读卡顿
        static readonly Dictionary<string, Texture2D> sTexCache = new Dictionary<string, Texture2D>();

        // ===== 预制体序列化配置(经 GameParticales/PoolManager 创建时由 prefab 写入) =====
        [SerializeField] string sheetPath = "";   // 图集PNG路径(兜底); 优先使用 sheet 直接引用贴图
        [SerializeField] Texture2D sheet;         // 直接拖入图集贴图引用; 非空时跳过运行时路径加载(消除卡顿/加载失败)
        [SerializeField] int cols = 1;            // 图集列数
        [SerializeField] int rows = 1;            // 图集行数
        [SerializeField] int frameCount = 1;      // 总帧数(按先行后列切分)
        [SerializeField] float playFps = 20f;     // 播放帧率
        [SerializeField] float worldSize = 0f;    // 最终世界单位宽度(>0自动算缩放)
        [SerializeField] float startDelay = 0f;   // 延迟多少秒后才开始播放
        [SerializeField] float frameFadeIn = 0f;  // 每帧淡入时长(秒):切换帧时从透明→不透明
        [SerializeField] float screenHeightFactor = 0f; // 屏幕关联系数:特效世界宽 = 相机可视高度 × 该系数(>0时启用,每帧随相机缩放自适应)
        [SerializeField] Vector2 pivot = new Vector2(0.5f, 0.5f); // 锚点:主体不在帧中心时用底部中心(0.5,0)钉住落点
        [SerializeField] int blendMode = 0; // 混合模式:0=Additive加色(黑底素材,黑自动透明), 1=Alpha正常混合(带alpha抠图素材,颜色精确还原原图)
        [SerializeField] Color tintColor = new Color(0.55f, 0.55f, 0.55f, 0.55f); // 老粒子 Shader 硬编码 2.0f: 0.55×2≈1.1(传统略提亮); 想精确还原素材设 0.5(2×0.5=1.0)

        // 分段缩放曲线:基于当前帧索引对 mTargetScale 再乘一个系数
        // 用途:全屏→正常→全屏 的三段式, 或任何自定义缩放节奏
        [SerializeField] float scaleStart = 1f;       // 起始缩放倍数(相对 worldSize)
        [SerializeField] float scalePeak = 1f;        // 中间段(正常大小)缩放倍数
        [SerializeField] float scaleEnd = 1f;         // 结束缩放倍数
        [SerializeField] int rampInFrames = 0;         // 前 N 帧: scaleStart → scalePeak 线性插值
        [SerializeField] int rampOutFrames = 0;       // 后 N 帧: scalePeak → scaleEnd 线性插值
        // 分段帧率:headFps=前N帧,tailFps=后N帧,中间段用 playFps;0 表示该段不分段
        // 边界 fpsHeadFrames/fpsTailFrames 为 0 时回退用 rampInFrames/rampOutFrames
        [SerializeField] float headFps = 0f;
        [SerializeField] float tailFps = 0f;
        [SerializeField] int fpsHeadFrames = 0;
        [SerializeField] int fpsTailFrames = 0;

        public float fps = 20f;
        public bool autoDestroy = true;
        public bool billboard = true;
        public bool billboardFull = true;

        SpriteRenderer mRenderer;
        readonly List<UnityEngine.Sprite> mSprites = new List<UnityEngine.Sprite>();
        float timer;
        int index;
        bool startPlay;
        bool sheetLoaded;        // 图集是否已加载(Awake 只加载一次)
        float delayLeft;         // 剩余延迟时间
        float frameTime;         // 当前帧已播放时长(用于每帧淡入)
        float mTargetScale = 1f; // 按 worldSize 计算的目标缩放(PlayEfect 会置 root scale=1,故需持久化覆盖)
        float mWorldW = 6f;      // 单帧原始世界宽(单位),LoadSheet 时计算,供复用重算缩放

        void Awake()
        {
            // 预制体模式:组件挂到 prefab 上,由 prefab 序列化字段配置,启动时加载图集(只做一次)
            // 有直接贴图引用(sheet) 或 路径(sheetPath) 任一配置即初始化
            if ((!string.IsNullOrEmpty(sheetPath) || sheet != null) && !sheetLoaded)
            {
                fps = playFps > 0f ? playFps : fps;
                LoadSheet(sheetPath, cols, rows, frameCount, fps, worldSize);
            }
        }

        void OnEnable()
        {
            // 对象池复用:每次激活时重置播放状态,从头播放
            ResetPlay();
        }

        /// <summary>
        /// 运行时覆盖延迟时间(对象池复用后 ResetPlay 会用此值重置 delayLeft)
        /// </summary>
        public void SetStartDelay(float delay)
        {
            startDelay = delay;
            if (sheetLoaded) delayLeft = delay;
        }

        void ResetPlay()
        {
            if (!sheetLoaded) return;
            // 屏幕关联模式优先:复用激活时按当前相机可视高度重算缩放,相机缩放时保持屏幕占比
            if (screenHeightFactor > 0f)
            {
                float visH = ScreenVisibleWorldHeight(transform.position);
                if (visH > 0f)
                {
                    mTargetScale = visH * screenHeightFactor / mWorldW;
                    transform.localScale = Vector3.one * mTargetScale;
                }
            }
            // 否则从序列化 worldSize 重算目标缩放(避免对象池缓存旧 worldSize 导致改尺寸无效)
            else if (worldSize > 0f)
            {
                mTargetScale = worldSize / mWorldW;
                transform.localScale = Vector3.one * mTargetScale * (rampInFrames > 0 ? scaleStart : scalePeak);
            }
            index = 0;
            timer = 0f;
            frameTime = 0f;
            delayLeft = startDelay;
            startPlay = true;
            if (mRenderer != null && mSprites.Count > 0)
            {
                mRenderer.sprite = mSprites[0];
                mRenderer.color = new Color(1f, 1f, 1f, 0f);
                if (mRenderer.sharedMaterial != null)
                {
                    mRenderer.sharedMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
                    mRenderer.sharedMaterial.SetInt("_ZWrite", 0);
                }
            }
        }

        /// <summary>
        /// 在世界坐标播放一段精灵图集序列帧特效
        /// </summary>
        /// <param name="sheetPath">图集PNG资源路径,如 "Assets/Effect/Sprite/Cyclone/cyclone_sheet.png"</param>
        /// <param name="cols">图集列数(如7)</param>
        /// <param name="rows">图集行数(如4)</param>
        /// <param name="frameCount">总帧数(如28),按先行后列顺序切分</param>
        /// <param name="worldPos">世界坐标放置点</param>
        /// <param name="fps">播放帧率</param>
        /// <param name="worldSize">最终世界单位宽度(>0时自动计算缩放)</param>
        public static SpriteSequenceEffect Play(string sheetPath, int cols, int rows, int frameCount,
                                                  Vector3 worldPos, float fps = 20f, float worldSize = 0f)
        {
            GameObject go = new GameObject("SpriteSequenceEffect");
            go.transform.position = worldPos;
            go.transform.localScale = Vector3.one;
            go.layer = 0;

            SpriteSequenceEffect effect = go.AddComponent<SpriteSequenceEffect>();
            effect.sheetPath = sheetPath;
            effect.LoadSheet(sheetPath, cols, rows, frameCount, fps, worldSize);
            return effect;
        }

        void LoadSheet(string sheetPath, int cols, int rows, int frameCount, float playFps, float worldSize)
        {
            mRenderer = gameObject.AddComponent<SpriteRenderer>();

            // 混合模式决定 Shader:两条独立回退链,都走老粒子 Shader(硬编码 2.0f)
            // tintColor × 2.0f = 最终亮度倍率; 默认 0.55 → 约 1.1(传统略提亮效果)
            Shader s;
            if (blendMode == 1)
            {
                s = Shader.Find("Sango/Particles/Alpha Blended");
                if (s == null) s = Shader.Find("Particles/Alpha Blended Premultiply");
                if (s == null) s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("UI/Default");
            }
            else
            {
                s = Shader.Find("ProjectX/Particles/Particles/Additive");
                if (s == null) s = Shader.Find("Mobile/Particles/Additive");
                if (s == null) s = Shader.Find("Legacy Particles/Additive");
                if (s == null) s = Shader.Find("Sprites/Default");
            }

            Material mat = new Material(s);
            mat.name = "SpriteSequenceEffect_FX";
            mat.SetColor("_TintColor", tintColor);
            // 强制忽略Z-buffer深度测试,确保特效始终绘制在terrain/单位/建筑之上
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.SetInt("_ZWrite", 0);
            mRenderer.sharedMaterial = mat;

            fps = playFps;

            Texture2D sheetTex = sheet != null ? sheet : LoadTexture(sheetPath);
            if (sheetTex == null)
            {
                Sango.Log.Error($"SpriteSequenceEffect: 图集加载失败 {sheetPath}", Sango.Log.LogType.World);
                Destroy(gameObject);
                return;
            }
            if (!sheetTex.isReadable)
                sheetTex = ToReadable(sheetTex);
            sheetTex.filterMode = FilterMode.Point;
            sheetTex.wrapMode = TextureWrapMode.Clamp;

            // 黑底素材净化:两种混合模式都要处理,但方式不同
            //  - Additive(0):近黑像素 RGB→0,保留原 alpha(加色下 RGB=0 即透明)
            //  - Alpha(1):    近黑像素 alpha→0,保留原 RGB(精确还原颜色,黑底变透明)
            sheetTex = PurgeNearBlack(sheetTex, blendMode);

            // 精确浮点帧宽高:不用整数除法避免frameW*cols != width导致的边界偏移
            float frameW_f = (float)sheetTex.width / cols;
            float frameH_f = (float)sheetTex.height / rows;
            // 对外暴露的整数帧宽(供 ppu/worldSize 换算使用)
            int frameW = Mathf.RoundToInt(frameW_f);
            float ppu = Mathf.Min(frameW_f, frameH_f) / 6f;
            mWorldW = Mathf.Max(0.001f, frameW_f / ppu);
            // UV padding:每帧向内收缩 3px,防止 filterMode.Point 下帧边界采到相邻帧或背景像素
            float pad = 3f;

            for (int i = 0; i < frameCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                // row 0 是最上面一行(Unity 纹理 y=0 是底部)
                float x = col * frameW_f + pad;
                float y = (rows - 1 - row) * frameH_f + pad;
                Rect rect = new Rect(x, y, frameW_f - pad * 2, frameH_f - pad * 2);
                UnityEngine.Sprite sp = UnityEngine.Sprite.Create(sheetTex, rect,
                                          pivot, ppu, 0u, SpriteMeshType.FullRect);
                mSprites.Add(sp);
            }

            if (mSprites.Count == 0)
            {
                Sango.Log.Error($"SpriteSequenceEffect: 切帧失败 sheet={sheetPath} cols={cols} rows={rows}", Sango.Log.LogType.World);
                Destroy(gameObject);
                return;
            }

            float worldW = frameW_f / ppu;
            if (screenHeightFactor > 0f)
            {
                // 屏幕关联模式:按当前相机可视高度计算缩放,相机缩放时自动保持屏幕占比
                float visH = ScreenVisibleWorldHeight(transform.position);
                if (visH > 0f)
                    mTargetScale = visH * screenHeightFactor / worldW;
            }
            else if (worldSize > 0f)
            {
                mTargetScale = worldSize / worldW;
                transform.localScale = Vector3.one * mTargetScale;
            }

            mRenderer.sprite = mSprites[0];
            mRenderer.color = new Color(1f, 1f, 1f, 0f); // 初始透明,由 Update 控制(延迟期间与淡入))
            startPlay = true;
            sheetLoaded = true;
            AlignBillboard();
        }

        /// <summary>
        /// 计算某世界坐标处,相机可视的高度范围(世界单位)。
        /// 透视相机:在目标位置沿相机视线方向的垂直截面上,可视高度 ≈ 2×d×tan(fov/2)。
        /// 相机拉近/拉远时该值随之变化,便于特效按屏幕占比自适应缩放。
        /// </summary>
        public static float ScreenVisibleWorldHeight(Vector3 worldPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return 0f;
            Vector3 diff = worldPos - cam.transform.position;
            float dist = Mathf.Max(0.1f, Mathf.Abs(Vector3.Dot(diff, cam.transform.forward)));
            if (cam.orthographic)
                return 2f * cam.orthographicSize;
            return 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Texture2D cached;
            if (sTexCache.TryGetValue(path, out cached) && cached != null)
                return cached;
            Texture2D tex = TextureLoader.LoadFromFileSync(path, false, false) as Texture2D;
            if (tex == null)
                tex = PackageManager.Instance.LoadAssets(DefaultPackage, path, typeof(Texture2D)) as Texture2D;
            if (tex != null && !tex.isReadable)
                tex = ToReadable(tex);
            // 缓存可读副本:同一路径只读盘+GPU回读一次
            if (tex != null)
                sTexCache[path] = tex;
            return tex;
        }

        static Texture2D ToReadable(Texture2D tex)
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        /// <summary>
        /// 黑底素材净化:把感知亮度低于阈值的背景像素处理成"透明"状态。
        /// 自动检测素材类型:alpha=0 占比 > 5% → 带 alpha 抠图(如 519/521/524),
        /// 已用 alpha 通道表示透明,跳过 Purge; alpha=0 占比极低 → 黑底素材(如 522),
        /// 全区域 alpha 都高,才需要 Purge 清暗像素。
        /// 两种混合模式处理方式不同:
        ///   - Additive(blendMode=0):RGB → 0,alpha 保留。加色公式 src+dst,RGB=0 即透明
        ///   - Alpha(blendMode=1):alpha → 0,RGB 保留。精确还原颜色,清 alpha 变透明
        /// 克隆原纹理再改,避免污染全局 sTexCache。
        /// </summary>
        static Texture2D PurgeNearBlack(Texture2D tex, int blendMode)
        {
            if (tex == null) return null;
            // 先扫一遍检测素材类型:alpha=0 占比 > 5% → 带 alpha 抠图,跳过 Purge
            Color[] src = tex.GetPixels();
            int total = src.Length;
            int transparentCount = 0;
            for (int i = 0; i < total; i++)
                if (src[i].a < 0.02f) transparentCount++;
            float transparentRatio = (float)transparentCount / total;

            if (transparentRatio > 0.05f)
            {
                // 带 alpha 抠图素材:本身已有 alpha 通道表示透明区域,不需要 Purge
                // 只克隆一份(后续可能还需要 filterMode 等处理,复用克隆逻辑)
                if (tex.filterMode != FilterMode.Point || tex.wrapMode != TextureWrapMode.Clamp)
                {
                    Texture2D clone = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                    clone.filterMode = FilterMode.Point;
                    clone.wrapMode = TextureWrapMode.Clamp;
                    Graphics.CopyTexture(tex, clone);
                    return clone;
                }
                return tex;
            }

            // 黑底素材:克隆并 Purge
            Texture2D work = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            work.filterMode = FilterMode.Point;
            work.wrapMode = TextureWrapMode.Clamp;
            Graphics.CopyTexture(tex, work);
            Color[] pixels = work.GetPixels();
            // Additive 需要彻底清黑:感知亮度 < 0.10
            // Alpha 需要精确只清纯黑:RGB 三通道都 < 0.02(避免误杀暗蓝/暗紫光效)
            const float lumThresh = 0.10f;
            const float rgbThresh = 0.02f;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                bool shouldPurge;
                if (blendMode == 1)
                    shouldPurge = c.r < rgbThresh && c.g < rgbThresh && c.b < rgbThresh;
                else
                    shouldPurge = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f < lumThresh;
                if (shouldPurge)
                {
                    if (blendMode == 1)
                        pixels[i] = new Color(c.r, c.g, c.b, 0f);
                    else
                        pixels[i] = new Color(0f, 0f, 0f, c.a);
                }
            }
            work.SetPixels(pixels);
            work.Apply();
            return work;
        }

        void AlignBillboard()
        {
            if (!billboard) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (billboardFull)
                transform.rotation = cam.transform.rotation;
        }

        void Update()
        {
            if (!startPlay) return;

            AlignBillboard();

            // 屏幕关联模式:每帧按相机可视高度重算缩放,相机拉近/拉远时特效保持屏幕占比不变
            if (screenHeightFactor > 0f)
            {
                float visH = ScreenVisibleWorldHeight(transform.position);
                if (visH > 0f)
                    mTargetScale = visH * screenHeightFactor / mWorldW;
            }

            // 分段缩放曲线:基于当前帧索引算一个倍数,乘到 mTargetScale 上
            float segScale = 1f;
            if (mSprites.Count > 0 && (rampInFrames > 0 || rampOutFrames > 0))
            {
                int total = mSprites.Count;
                if (rampInFrames > 0 && index < rampInFrames)
                {
                    float t = Mathf.Clamp01((float)index / rampInFrames);
                    segScale = Mathf.Lerp(scaleStart, scalePeak, t);
                }
                else if (rampOutFrames > 0 && index >= total - rampOutFrames)
                {
                    float t = Mathf.Clamp01((float)(index - (total - rampOutFrames)) / rampOutFrames);
                    segScale = Mathf.Lerp(scalePeak, scaleEnd, t);
                }
                else
                {
                    segScale = scalePeak;
                }
            }

            // 持久化按 worldSize 计算的缩放(PlayEfect 会把 root scale 重置为1,这里每帧覆盖保持正确尺寸)
            if (mTargetScale != 1f || segScale != 1f)
                transform.localScale = Vector3.one * mTargetScale * segScale;

            // 延迟阶段:全透明等待 startDelay 结束
            if (delayLeft > 0f)
            {
                delayLeft -= Time.deltaTime;
                mRenderer.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            // 正式播放段
            timer += Time.deltaTime;
            frameTime += Time.deltaTime;
            // 分段帧率:headFps=前N帧,tailFps=后N帧,中间用 playFps;边界为0回退 rampIn/rampOut
            float curFps = fps;
            if (mSprites.Count > 0)
            {
                int h = fpsHeadFrames > 0 ? fpsHeadFrames : rampInFrames;
                int t = fpsTailFrames > 0 ? fpsTailFrames : rampOutFrames;
                int tailStart = mSprites.Count - Mathf.Min(t, mSprites.Count);
                if (h > 0 && headFps > 0f && index < h)
                    curFps = headFps;
                else if (t > 0 && tailFps > 0f && index >= tailStart)
                    curFps = tailFps;
            }
            float interval = 1f / Mathf.Max(0.1f, curFps);

            // 每帧淡入:切到本帧时从透明逐渐到不透明(frameFadeIn),持续完整个帧时隙
            float a = frameFadeIn > 0f ? Mathf.Clamp01(frameTime / frameFadeIn) : 1f;
            mRenderer.color = new Color(1f, 1f, 1f, a);

            if (timer < interval) return;

            timer -= interval;
            frameTime = 0f;
            index++;
            if (index >= mSprites.Count)
            {
                if (autoDestroy) FinishPlay();
                return;
            }
            mRenderer.sprite = mSprites[index];
        }

        /// <summary>播放结束:优先回池(PoolManager.Recycle),失败则销毁</summary>
        void FinishPlay()
        {
            startPlay = false;
            if (!PoolManager.Recycle(gameObject))
                Destroy(gameObject);
        }
    }
}
