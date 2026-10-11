using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
#endif
namespace Sango.Render
{
    public class MapBaseColor : MapProperty
    {
        //public string[] baseTextrueName = new string[4];
        public Texture[] texture = new Texture[4];
        public MapBaseColor(MapRender map) : base(map)
        {
            texture = new Texture[4] { Texture2D.whiteTexture,
                Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
        }

        public override void Init()
        {
            base.Init();
        }
        public override void Clear()
        {
            base.Clear();
            texture = new Texture[4] { Texture2D.whiteTexture,
                Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
            //baseTextrueName = null;
            Shader.SetGlobalTexture("_BaseTex", Texture2D.whiteTexture);
        }
        internal override void OnSave(BinaryWriter writer)
        {
            for (int i = 0; i < texture.Length; i++)
            {
                if (texture[i] == null)
                    continue;
                else
                {
                    Texture2D texture2D = texture[i] as Texture2D;
                    if (texture2D != null)
                    {
                        //byte[] bytes = texture2D.EncodeToPNG();
                        //writer.Write(bytes.Length);
                        //writer.Write(texture2D.width);
                        //writer.Write(texture2D.height);
                        //writer.Write(bytes);
                        continue;
                    }
                    else
                    {
                        RenderTexture renderTexture = texture[i] as RenderTexture;
                        if (renderTexture != null)
                        {
                            int width = renderTexture.width;
                            int height = renderTexture.height;
                            texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
                            RenderTexture.active = renderTexture;
                            texture2D.ReadPixels(new UnityEngine.Rect(0, 0, width, height), 0, 0);
                            texture2D.Apply();
                            byte[] bytes = texture2D.EncodeToPNG();

                            string dir = Path.FindDirectory($"Assets/Map/{map.WorkContent}");
                            if (dir == null)
                            {
                                dir = $"{Path.ContentRootPath}/Assets/Map/{map.WorkContent}";
                            }

                            string fileName = $"{dir}/BaseTex/BaseMap{i}.png";
                            Sango.Directory.Create(fileName, false);
                            FileStream fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
                            fileStream.Write(bytes, 0, bytes.Length);
                            fileStream.Dispose();
                            fileStream.Close();

                            //writer.Write(bytes.Length);
                            //writer.Write(width);
                            //writer.Write(height);
                            //writer.Write(bytes);
                        }
                        else
                        {
                            //writer.Write(0);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 是否加载 BaseMap{i}_full.png（实际尺寸）。
        ///
        /// 默认 false：加载 BaseMap{i}.png，即「压缩尺寸」——最长边不超过 2048、两边对齐到 4。
        /// 底图只是地表的整体色调，缩到 2048 视觉上几乎无差别，而纹理走
        /// TextureLoader（LoadImage 原生尺寸 + Compress(true) 块压缩）：
        ///   压缩尺寸 2048x1136  -> 约 1.2MB/季，4 季约 4.7MB
        ///   实际尺寸 7200x4000  -> 约 14.4MB/季，4 季约 58MB，且压缩过程会有 ~115MB/张 的瞬时占用
        /// 需要高清底图（截图/宣传图）时再打开。
        /// </summary>
        public static bool useFullResBaseMap = false;

        /// <summary>按当前设置取某一季的底图；_full 缺失时自动回退到压缩尺寸。</summary>
        Texture LoadSeasonTexture(int i)
        {
            if (useFullResBaseMap)
            {
                Texture full = map.CreateTexture($"BaseTex/BaseMap{i}_full");
                if (full != null && full != Texture2D.whiteTexture)
                    return full;
            }
            return map.CreateTexture($"BaseTex/BaseMap{i}");
        }

        /// <summary>切换底图清晰度并立即重载（销毁旧的运行时纹理）。</summary>
        public void SetUseFullResBaseMap(bool full)
        {
            useFullResBaseMap = full;
            for (int i = 0; i < texture.Length; i++)
            {
                Texture old = texture[i];
                texture[i] = LoadSeasonTexture(i);
                if (old != null && old != Texture2D.whiteTexture)
                    UnityEngine.Object.Destroy(old);
            }
            UpdateRender();
        }

        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            if (versionCode > 5)
            {
                for (int i = 0; i < 4; i++)
                {
                    texture[i] = LoadSeasonTexture(i);
                    if (i == curSeason)
                    {
                        UpdateRender();
                    }
                }
                return;
            }

            if (versionCode == 5)
            {
                for (int i = 0; i < texture.Length; i++)
                {
                    int length = reader.ReadInt32();
                    if (length > 0)
                    {
                        int width = reader.ReadInt32();
                        int height = reader.ReadInt32();
                        byte[] bytes = reader.ReadBytes(length);
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
                        TextureFormat baseColorTextureFormat = TextureFormat.RGB24;
#else
                        TextureFormat baseColorTextureFormat = TextureFormat.ASTC_5x5;
#endif
                        Texture2D texture2D = new Texture2D(4, 4, baseColorTextureFormat, false);
                        if (texture2D.LoadImage(bytes, true))
                        {
                            texture2D.name = "00BaseMap" + i;
                            //texture2D.Apply(true, true);
                            texture[i] = texture2D;
                            if (i == curSeason)
                            {
                                UpdateRender();
                            }
                        }
                    }
                }
                return;
            }

            reader.ReadString();
            reader.ReadString();
            reader.ReadString();
            reader.ReadString();

            if (string.IsNullOrEmpty(map.FileName))
            {
                return;
            }

            string mapBinDir = System.IO.Path.GetDirectoryName(map.FileName);
            for (int i = 0; i < 4; i++)
            {
#if UNITY_EDITOR
                string destFile = $"{mapBinDir}/BaseMap{i}.png";
#else
                string destFile = $"{mapBinDir}/BaseMap{i}.png";
#endif

                Texture tex = map.CreateTexture(destFile);
                if (tex != Texture2D.whiteTexture)
                {
                    texture[i] = tex;
                    if (i == curSeason)
                    {
                        UpdateRender();
                    }
                }
                else
                {
                    string p = $"BaseTex/BaseMap{i}";
                    tex = map.CreateTexture(p);
                    texture[i] = tex;
                    if (i == curSeason)
                    {
                        UpdateRender();
                    }
                }
            }
        }



        public override void UpdateRender()
        {
            Shader.SetGlobalTexture("_BaseTex", texture[curSeason]);
        }

    }
}
