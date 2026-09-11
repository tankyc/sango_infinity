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

        public float fps = 20f;
        public bool autoDestroy = true;
        public bool billboard = true;
        public bool billboardFull = true;

        SpriteRenderer mRenderer;
        readonly List<UnityEngine.Sprite> mSprites = new List<UnityEngine.Sprite>();
        float timer;
        int index;
        bool startPlay;

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
            effect.LoadSheet(sheetPath, cols, rows, frameCount, fps, worldSize);
            return effect;
        }

        void LoadSheet(string sheetPath, int cols, int rows, int frameCount, float playFps, float worldSize)
        {
            mRenderer = gameObject.AddComponent<SpriteRenderer>();
            mRenderer.sortingOrder = 1000;

            Shader s = Shader.Find("Mobile/Particles/Additive");
            if (s == null) s = Shader.Find("Legacy Particles/Additive");
            if (s == null) s = Shader.Find("Sprites/Default");

            Material mat = new Material(s);
            mat.name = "SpriteSequenceEffect_Additive";
            mRenderer.sharedMaterial = mat;

            fps = playFps;

            Texture2D sheet = LoadTexture(sheetPath);
            if (sheet == null)
            {
                Debug.LogError($"SpriteSequenceEffect: 图集加载失败 {sheetPath}");
                Destroy(gameObject);
                return;
            }

            int frameW = sheet.width / cols;
            int frameH = sheet.height / rows;
            float ppu = Mathf.Min(frameW, frameH) / 6f;

            for (int i = 0; i < frameCount; i++)
            {
                int col = i % cols;
                int row = i / cols;
                // Sprite.Create 的 Y 轴从底部开始,图集行从顶部开始,需要翻转
                float x = col * frameW;
                float y = (rows - 1 - row) * frameH;
                Rect rect = new Rect(x, y, frameW, frameH);
                UnityEngine.Sprite sp = UnityEngine.Sprite.Create(sheet, rect,
                                          new Vector2(0.5f, 0.5f), ppu, 0u, SpriteMeshType.FullRect);
                mSprites.Add(sp);
            }

            if (mSprites.Count == 0)
            {
                Debug.LogError($"SpriteSequenceEffect: 切帧失败 sheet={sheetPath} cols={cols} rows={rows}");
                Destroy(gameObject);
                return;
            }

            float worldW = frameW / ppu;
            if (worldSize > 0f)
            {
                float targetScale = worldSize / worldW;
                transform.localScale = Vector3.one * targetScale;
            }

            mRenderer.sprite = mSprites[0];
            startPlay = true;
            index = 0;
            timer = 0f;
            AlignBillboard();
            Debug.Log($"SpriteSequenceEffect: OK 切{mSprites.Count}帧(图集{sheet.width}x{sheet.height} {cols}x{rows}) " +
                      $"Shader={s.name} 世界宽≈{worldW * transform.localScale.x:F1}单位");
        }

        static Texture2D LoadTexture(string path)
        {
            Texture2D tex = TextureLoader.LoadFromFileSync(path, false, false) as Texture2D;
            if (tex == null)
                tex = PackageManager.Instance.LoadAssets(DefaultPackage, path, typeof(Texture2D)) as Texture2D;
            if (tex != null && !tex.isReadable)
                tex = ToReadable(tex);
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

            timer += Time.deltaTime;
            float interval = 1f / Mathf.Max(0.1f, fps);
            if (timer < interval) return;
            timer -= interval;
            index++;
            if (index >= mSprites.Count)
            {
                if (autoDestroy) Destroy(gameObject);
                return;
            }
            mRenderer.sprite = mSprites[index];
        }
    }
}
