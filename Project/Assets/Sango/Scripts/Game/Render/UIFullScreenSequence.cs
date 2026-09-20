using System;
using System.Collections.Generic;
using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.Render
{
    /// <summary>
    /// 全屏序列帧覆盖特效:在 Screen-Space Overlay 画布上叠加多个图层(可全屏铺满或按屏幕比例居中定位),
    /// 每层可为一串序列帧或单张静态图,支持淡入淡出、缩放动画、起始延迟、按帧切换 uvRect。
    /// </summary>
    public class UIFullScreenSequence : MonoBehaviour
    {
        /// <summary>单个全屏图层配置(可序列化,支持在预制件/Inspector 中可视编辑)</summary>
        [Serializable]
        public class Layer
        {
            [SerializeField] public string sheetPath;      // 图集PNG路径(兜底); 
            [SerializeField] public Texture2D sheet;       // 直接拖入图集贴图引用; 
            [SerializeField] public int cols = 1;
            [SerializeField] public int rows = 1;
            [SerializeField] public int frameCount = 1;
            [SerializeField] public float duration = 1f;   // 整个序列播完的秒数(每帧 = duration/frameCount)
            [SerializeField] public float startDelay = 0f; // 相对组合开始的延迟秒数
            [SerializeField] public float fadeIn = 0f;     // 淡入时长
            [SerializeField] public float fadeOut = 0f;    // 淡出时长
            [SerializeField] public float holdAfterEnd = 0f; // 播完后保持最后一帧多久再淡出

            [SerializeField] public bool useSolidColor = false; // true=整层为纯色(配合 stretch=true 铺满全屏)
            [SerializeField] public Color solidColor = new Color(0f, 0f, 0f, 0f); // 纯色层的颜色(useSolidColor=true时生效)
            [SerializeField] public bool additive = true;  // true=Additive(加色混合)使黑色变透明;false=普通Alpha混合(UI/Default)
            [SerializeField] public bool stretch = false;  // true=铺满全屏
            [SerializeField] public float heightFrac = 0.2f; // stretch=false: 精灵相对屏幕高度的占比
            [SerializeField] public float posX = 0f;       // stretch=false: 相对屏幕中心的水平偏移(屏幕宽度比例)
            [SerializeField] public float posY = 0f;       // stretch=false: 相对屏幕中心的垂直偏移(屏幕高度比例)
            [SerializeField] public float scaleStart = 1f; // 缩放起始倍率(叠加在 heightFrac 基准上)
            [SerializeField] public float expandTime = 0f; // 先放大阶段的时长(秒), t∈[0,expandTime] 由 scaleStart 线性放大到 scalePeak
            [SerializeField] public float scalePeak = 1f;  // 放大阶段到达的最大倍率
            [SerializeField] public float holdScaleTime = 0f; // 放大到 scalePeak 后保持该尺寸的时长
            [SerializeField] public float scaleEnd = 1f;   // 缩放到结束倍率, t∈[expandTime+holdScaleTime,duration] 由 scalePeak 线性过渡到 scaleEnd
            [SerializeField] public float alphaStart = 1f; // 播放开始时的透明度(0~1),与 fadeIn 叠加;用于起始就半透明的层
            [SerializeField] public float alphaEnd = 1f;   // 播放结束时(duration末尾)的透明度(0~1),实现播放过程中渐隐
            [SerializeField] public int[] frameIndexMap;   // 可选:自定义帧序映射。第i个播放帧取图集第 frameIndexMap[i] 帧(0起)。null=默认自底向上递增
            [SerializeField] public int order = 0;         // 图层层级:数值越大越在上方(覆盖)显示;同order保持创建顺序。默认0=底部
        }

        static readonly string DefaultPackage = "Content";

        // 按路径加载过的贴图缓存:同一路径只从磁盘读一次,避免每次播放特效都同步读盘卡顿
        static readonly Dictionary<string, Texture2D> sTexCache = new Dictionary<string, Texture2D>();

        // 材质缓存:全屏特效各层按混合类型复用同一材质(Additive/Alpha/纯色),避免每次播放 new 十几个材质
        static readonly Dictionary<string, Material> sMatCache = new Dictionary<string, Material>();
        static Texture2D sWhiteTex; // 1x1 白色像素图(纯色层用),复用避免每次 new

        readonly List<Layer> mLayers = new List<Layer>();
        readonly List<RawImage> mImages = new List<RawImage>();
        readonly List<Texture2D> mTextures = new List<Texture2D>();
        readonly List<bool> mIsSolid = new List<bool>(); // true=整层纯色(非Additive,直接铺色)
        readonly List<float> mCanvasSize = new List<float>(2);
        float mTimer;
        int mFinished;

        /// <summary>
        /// 播放一组全屏序列帧覆盖特效
        /// </summary>
        public static UIFullScreenSequence Play(params Layer[] layers)
        {
            GameObject go = new GameObject("UIFullScreenSequence");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000; // 置于通用 UI 之上

            UIFullScreenSequence seq = go.AddComponent<UIFullScreenSequence>();
            if (layers != null) seq.mLayers.AddRange(layers);
            seq.Init();
            return seq;
        }

        void Init()
        {
            mCanvasSize.Clear();
            mCanvasSize.Add(Screen.width);
            mCanvasSize.Add(Screen.height);

            for (int i = 0; i < mLayers.Count; i++)
            {
                Layer layer = mLayers[i];
                Texture2D tex;
                Material mat;
                if (layer.useSolidColor)
                {
                    // 纯色层:1x1白图 + 普通混合材质(不叠加,直接铺色)
                    tex = GetWhiteTexture();
                    mat = GetMaterial("Solid");
                    mIsSolid.Add(true);
                }
                else
                {
                    // 优先用 Inspector 直接拖入的贴图引用(零IO); 为null时才走路径加载
                    Texture2D refTex = layer.sheet;
                    tex = refTex != null ? refTex : LoadTexture(layer.sheetPath);
                    if (tex == null)
                    {
                        Debug.LogError($"UIFullScreenSequence: 加载失败 {layer.sheetPath}");
                        mTextures.Add(null);
                        mImages.Add(null);
                        mIsSolid.Add(false);
                        continue;
                    }
                    // 多帧图集 或 加色混合层:强制点采样(无双线性过滤),防止帧/纹理边缘像素渗透出半透明方框
                    if (layer.cols > 1 || layer.rows > 1 || layer.additive)
                    {
                        tex.filterMode = FilterMode.Point;
                        tex.wrapMode = TextureWrapMode.Clamp;
                    }
                    if (layer.additive)
                    {
                        mat = GetMaterial("Additive");
                    }
                    else
                    {
                        mat = GetMaterial("Alpha");
                    }
                    mIsSolid.Add(false);
                }
                mTextures.Add(tex);

                GameObject child = new GameObject("Layer_" + i);
                child.transform.SetParent(transform, false);

                RawImage img = child.AddComponent<RawImage>();
                img.texture = tex;
                img.material = mat;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, 0f); // 初始透明,由时间轴控制

                RectTransform rt = img.rectTransform;
                if (layer.stretch)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                else
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;
                }
                mImages.Add(img);
            }

            // 按 order 重排子物体层级:order 大者在上。选择排序保持同order相对顺序稳定,
            // 从 index 0 起,每次把剩余中最小的 order 提到当前位置。
            for (int i = 0; i < transform.childCount - 1; i++)
            {
                int minIdx = i;
                for (int j = i + 1; j < transform.childCount; j++)
                {
                    int io = j < mLayers.Count ? mLayers[j].order : 0;
                    int jo = minIdx < mLayers.Count ? mLayers[minIdx].order : 0;
                    if (io < jo) minIdx = j;
                }
                if (minIdx != i)
                    transform.GetChild(minIdx).SetSiblingIndex(i);
            }
        }

        void Update()
        {
            if (mLayers.Count == 0) return;

            float canvasW = mCanvasSize[0];
            float canvasH = mCanvasSize[1];
            mTimer += Time.deltaTime;
            mFinished = 0;

            for (int i = 0; i < mLayers.Count; i++)
            {
                RawImage img = mImages[i];
                Texture2D tex = mTextures[i];
                if (img == null || tex == null) { mFinished++; continue; }

                Layer layer = mLayers[i];
                bool isSolid = i < mIsSolid.Count && mIsSolid[i];
                float t = mTimer - layer.startDelay;
                if (t < 0f)
                {
                    img.color = Transparent();
                    continue;
                }

                float seqDur = Mathf.Max(0.01f, layer.duration);
                float endTime = seqDur + layer.holdAfterEnd;
                bool pastEnd = t > endTime;

                // 序列帧 uv 定位(图层行从顶部开始,uv 需从底部坐标换算);纯色层无需裁剪
                if (!isSolid)
                {
                    float progress = Mathf.Clamp01(t / seqDur);
                    int idx = Mathf.Min(layer.frameCount - 1, (int)(t / seqDur * layer.frameCount));
                    if (layer.frameIndexMap != null && layer.frameIndexMap.Length > 0)
                        idx = layer.frameIndexMap[Mathf.Clamp(idx, 0, layer.frameIndexMap.Length - 1)];
                    int col = idx % layer.cols;
                    int row = layer.rows - 1 - idx / layer.cols;
                    float u = (float)(col * (tex.width / layer.cols)) / tex.width;
                    float v = (float)(row * (tex.height / layer.rows)) / tex.height;
                    float w = (float)(tex.width / layer.cols) / tex.width;
                    float h = (float)(tex.height / layer.rows) / tex.height;
                    // UV内缩2px防止图集帧边缘渗透(加色混合下半透明边显示为方框)
                    float padU = 2f / tex.width;
                    float padV = 2f / tex.height;
                    img.uvRect = new Rect(u + padU, v + padV, w - padU * 2f, h - padV * 2f);
                }

                if (!layer.stretch)
                {
                    // 缩放动画:先放大到 scalePeak(白屏进入),保持,再由 scalePeak 缩小到 scaleEnd
                    float s;
                    float holdEnd = layer.expandTime + layer.holdScaleTime;
                    if (layer.expandTime > 0f && t <= layer.expandTime)
                    {
                        s = Mathf.Lerp(layer.scaleStart, layer.scalePeak, Mathf.Clamp01(t / layer.expandTime));
                    }
                    else if (layer.holdScaleTime > 0f && t <= holdEnd)
                    {
                        s = layer.scalePeak;
                    }
                    else
                    {
                        float rest = Mathf.Max(0.01f, seqDur - holdEnd);
                        float p = Mathf.Clamp01((t - holdEnd) / rest);
                        s = Mathf.Lerp(layer.scalePeak, layer.scaleEnd, p);
                    }
                    float th = layer.heightFrac * canvasH * Mathf.Max(0.001f, s);
                    float frameW = tex.width / layer.cols;
                    float frameH = tex.height / layer.rows;
                    float tw = th * (frameW / frameH);
                    img.rectTransform.sizeDelta = new Vector2(tw, th);
                    img.rectTransform.anchoredPosition = new Vector2(layer.posX * canvasW, layer.posY * canvasH);
                }

                // 透明度:基础=播放过程中从 alphaStart 线性过渡到 alphaEnd;再叠加淡入/淡出
                float alpha = Mathf.Lerp(layer.alphaStart, layer.alphaEnd, Mathf.Clamp01(t / seqDur));
                if (layer.fadeIn > 0f && t < layer.fadeIn)
                    alpha *= Mathf.Clamp01(t / layer.fadeIn);
                if (pastEnd && layer.fadeOut > 0f)
                    alpha *= Mathf.Clamp01(1f - (t - endTime) / layer.fadeOut);

                if (pastEnd && t >= endTime + layer.fadeOut)
                {
                    img.color = Transparent();
                    mFinished++;
                    continue;
                }
                
                Color c = isSolid ? layer.solidColor : Color.white;
                // 加色混合淡出:同时按 alpha 降低 RGB,使黑底边缘残留噪声随淡出一同消失,避免淡白色框
                // (仅对 additive 层有效;Alpha 混合层降 RGB 会变透明等同降 alpha,安全)
                float fade = Mathf.Clamp01(alpha);
                img.color = new Color(c.r * fade, c.g * fade, c.b * fade, c.a * fade);
            }

            if (mFinished >= mLayers.Count)
                Destroy(gameObject);
        }

        static Color Transparent() { return new Color(1f, 1f, 1f, 0f); }

        static Texture2D LoadTexture(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Texture2D cached;
            if (sTexCache.TryGetValue(path, out cached) && cached != null)
                return cached;
            Texture2D tex = TextureLoader.LoadFromFileSync(path, false, false) as Texture2D;
            if (tex == null)
                tex = PackageManager.Instance.LoadAssets(DefaultPackage, path, typeof(Texture2D)) as Texture2D;
            if (tex != null)
                sTexCache[path] = tex;
            return tex;
        }

        // 按混合类型复用材质(Additive/Alpha/纯色),避免每次播放 new 十几个材质
        static Material GetMaterial(string key)
        {
            Material mat;
            if (sMatCache.TryGetValue(key, out mat) && mat != null)
                return mat;
            Shader sh;
            switch (key)
            {
                case "Additive":
                    sh = Shader.Find("Mobile/Particles/Additive");
                    if (sh == null) sh = Shader.Find("Legacy Particles/Additive");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    break;
                case "Alpha":
                    // UGUI RawImage 普通Alpha混合必须用 UI/Default
                    sh = Shader.Find("UI/Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    break;
                default: // Solid:纯色层普通混合
                    sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Unlit/Color");
                    break;
            }
            if (sh == null)
            {
                Debug.LogError($"UIFullScreenSequence: 找不到可用 Shader,key={key}");
                return null;
            }
            mat = new Material(sh);
            mat.name = "UIFullScreenSequence_" + key;
            sMatCache[key] = mat;
            return mat;
        }

        // 1x1 白色像素图(纯色层用),复用避免每次 new
        static Texture2D GetWhiteTexture()
        {
            if (sWhiteTex == null)
            {
                sWhiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                sWhiteTex.SetPixel(0, 0, Color.white);
                sWhiteTex.Apply(false, true);
            }
            return sWhiteTex;
        }
    }
}