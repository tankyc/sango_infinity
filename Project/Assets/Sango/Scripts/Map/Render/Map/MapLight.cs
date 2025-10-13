using System.IO;
using UnityEngine;

namespace Sango.Render
{
    // 光照
    public class MapLight : MapProperty
    {
        public Vector3[] light_direction = { new Vector3(45f, 270f, 0f), new Vector3(45f, 270f, 0f), new Vector3(45f, 270f, 0f), new Vector3(45f, 270f, 0f) };
        public Color[] light_color = { new Color(1, 1, 1), new Color(1, 1, 1), new Color(1, 1, 1), new Color(1, 1, 1) };
        public float[] light_intensity = { 1, 1, 1, 1 };
        public Color[] shadow_color = { new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f), new Color(0.5f, 0.5f, 0.5f) };
        public float[] shadow_strength = { 1, 1, 1, 1 };


        private Light light;

        public MapLight(MapRender map) : base(map)
        {
        }

        public override void Init()
        {
            base.Init();
            GameObject lightGameObject = new GameObject("MainLight");
            light = lightGameObject.AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional;
            UpdateRender();
        }

        public override void Clear()
        {
            base.Clear();
            GameObject.Destroy(light.gameObject);
        }

        internal override void OnSave(BinaryWriter writer)
        {
            for (int i = 0; i < light_direction.Length; ++i) {
                writer.Write(light_direction[i].x);
                writer.Write(light_direction[i].y);
                writer.Write(light_direction[i].z);
                writer.Write(light_color[i].r);
                writer.Write(light_color[i].g);
                writer.Write(light_color[i].b);
                writer.Write(light_intensity[i]);
                writer.Write(shadow_color[i].r);
                writer.Write(shadow_color[i].g);
                writer.Write(shadow_color[i].b);
                writer.Write(shadow_strength[i]);
            }

        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            for (int i = 0; i < light_direction.Length; ++i) {
                light_direction[i] = new Vector3((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                light_color[i] = new Color((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                light_intensity[i] = reader.ReadSingle();
                light_intensity[i] = 1.3f;
                shadow_color[i] = new Color((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                shadow_strength[i] = reader.ReadSingle();
            }

            UpdateRender();
        }

        public override void UpdateRender()
        {
            if (light == null) return;
            light.transform.rotation = Quaternion.Euler(light_direction[curSeason]);
            light.color = light_color[curSeason];
            light.intensity = light_intensity[curSeason];
            Shader.SetGlobalColor("_ShadowColor", shadow_color[curSeason]);
            Shader.SetGlobalFloat("_ShadowStrength", shadow_strength[curSeason]);
        }

        public Vector3 lightDirection
        {
            get { return light_direction[curSeason]; }
            set
            {
                light_direction[curSeason] = value;
                if (light == null) return;
                light.transform.rotation = Quaternion.Euler(light_direction[curSeason]);
            }
        }
        public Color lightColor
        {
            get { return light_color[curSeason]; }
            set
            {
                light_color[curSeason] = value;
                if (light == null) return;
                light.color = light_color[curSeason];
            }
        }
        public float lightIntensity
        {
            get { return light_intensity[curSeason]; }
            set
            {
                light_intensity[curSeason] = value;
                if (light == null) return;
                light.intensity = light_intensity[curSeason];
            }
        }
        public Color shadowColor
        {
            get { return shadow_color[curSeason]; }
            set
            {
                shadow_color[curSeason] = value;
                Shader.SetGlobalColor("_ShadowColor", shadow_color[curSeason]);
            }
        }
        public float shadowStrength
        {
            get { return shadow_strength[curSeason]; }
            set
            {
                shadow_strength[curSeason] = value;
                Shader.SetGlobalFloat("_ShadowStrength", shadow_strength[curSeason]);
            }
        }

    }
}
