using System.IO;
using UnityEngine;

namespace Sango.Render
{
    // 雾效
    public class MapFog : MapProperty
    {
        public Color[] fog_color = { new Color(1, 1, 1), new Color(1, 1, 1), new Color(1, 1, 1), new Color(1, 1, 1) };
        public float[] fog_start = { 546.5f, 546.5f, 546.5f, 546.5f };
        public float[] fog_end = { 1068.8f, 1068.8f, 1068.8f, 1068.8f };
        public float[] fog_density = { 11f, 11f, 11f, 11f };
        public float fogAlpha = 1.0f;

        public MapFog(MapRender map) : base(map)
        {

        }
        public override void Init()
        {
            base.Init();
            UpdateRender();
        }

        internal override void OnSave(BinaryWriter writer)
        {
            for (int i = 0; i < fog_color.Length; i++) {
                writer.Write(fog_color[i].r);
                writer.Write(fog_color[i].g);
                writer.Write(fog_color[i].b);
                writer.Write(fog_start[i]);
                writer.Write(fog_end[i]);
                writer.Write(fog_density[i]);
            }

        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            for (int i = 0; i < fog_color.Length; i++) {
                fog_color[i] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                fog_start[i] = reader.ReadSingle();
                fog_end[i] = reader.ReadSingle();
                fog_density[i] = reader.ReadSingle();

            }
            UpdateRender();
        }

        public override void UpdateRender()
        {
            Shader.SetGlobalColor("_FogColor", fog_color[curSeason]);
            Shader.SetGlobalFloat("_MixBegin", fog_start[curSeason]);
            Shader.SetGlobalFloat("_MixEnd", fog_end[curSeason]);
            Shader.SetGlobalFloat("_MixPower", fog_density[curSeason]);
        }
        public Color fogColor
        {
            get { return fog_color[curSeason]; }
            set
            {
                fog_color[curSeason] = value;
                Shader.SetGlobalColor("_FogColor", fog_color[curSeason]);
            }
        }
        public float fogStart
        {
            get { return fog_start[curSeason]; }
            set
            {
                fog_start[curSeason] = value;
                Shader.SetGlobalFloat("_MixBegin", fog_start[curSeason]);
            }
        }
        public float fogEnd
        {
            get { return fog_end[curSeason]; }
            set
            {
                fog_end[curSeason] = value;
                Shader.SetGlobalFloat("_MixEnd", fog_end[curSeason]);
            }
        }
        public float fogDensity
        {
            get { return fog_density[curSeason]; }
            set
            {
                fog_density[curSeason] = value;
                Shader.SetGlobalFloat("_MixPower", fog_density[curSeason]);
            }
        }
        public bool fogEnabled
        {
            get { return fogAlpha > 0.0001f; }
            set
            {
                if(value)
                    fogAlpha = 1.0f;
                else
                    fogAlpha = 0.0f;

                fog_color[curSeason].a = fogAlpha;
                Shader.SetGlobalColor("_FogColor", fog_color[curSeason]);
            }
        }
    }
}
