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
                            string fileName = $"{dir}/BaseTex/BaseMap{i}.png";
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
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            if (versionCode > 5)
            {
                for (int i = 0; i < 4; i++)
                {
                    string p = $"BaseTex/BaseMap{i}";
                    Texture tex = map.CreateTexture(p);
                    texture[i] = tex;
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
                if(tex != Texture2D.whiteTexture)
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
