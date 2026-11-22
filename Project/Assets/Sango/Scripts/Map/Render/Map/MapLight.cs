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

        /// <summary>关闭阴影前的原始阴影模式，恢复时用它还原，避免把 Soft 硬写成 Hard</summary>
        LightShadows cachedShadows = LightShadows.Soft;

        /// <summary>是否已经记下原始阴影模式</summary>
        bool shadowCached;

        /// <summary>
        /// 开关主光的实时阴影。
        ///
        /// 【为什么在这里关】远景降级时，阴影贴图等于把整个场景的 ShadowCaster 再渲一遍，
        /// 是拉远之后最贵的一项开销，而且远景下几乎看不出阴影细节。
        /// 直接改 <c>light.shadows</c> 比改 URP 资产的 shadowDistance 更彻底：
        /// 后者只是缩短投射距离，近处物体照样进 ShadowCaster pass。
        /// </summary>
        /// <param name="enable">true 恢复原始阴影模式；false 关闭阴影</param>
        public void SetShadowEnable(bool enable)
        {
            if (light == null)
                return;

            if (!shadowCached)
            {
                cachedShadows = light.shadows;
                shadowCached = true;
            }

            LightShadows target = enable ? cachedShadows : LightShadows.None;
            if (light.shadows != target)
                light.shadows = target;
        }

        public MapLight(MapRender map) : base(map)
        {
        }

        public override void Init()
        {
            base.Init();

            // 【空引用防护】GameObject.Find 只返回**激活**的物体：MainLight 被禁用 / 改名 /
            // 尚未加载时这里原本会直接 NRE，而本方法在 MapRender.CreateDatas 的初始化链中间
            // （MapRender.cs:270），一抛异常后面 mapFog / mapBaseColor / mapCamera / mapSkyBox
            // 全都不初始化 —— 表现就是"整张地图没有光照、雾和底图，地形发黑"。
            GameObject lightGameObject = GameObject.Find("MainLight");
            if (lightGameObject != null)
                light = lightGameObject.GetComponent<UnityEngine.Light>();

            // 回退①：环境光设置里指定的太阳
            if (light == null)
                light = RenderSettings.sun;

            // 回退②：场景里任意一盏启用的平行光
            if (light == null)
            {
                UnityEngine.Light[] lights = UnityEngine.Object.FindObjectsOfType<UnityEngine.Light>();
                for (int i = 0; i < lights.Length; i++)
                {
                    UnityEngine.Light candidate = lights[i];
                    if (candidate != null && candidate.type == LightType.Directional && candidate.isActiveAndEnabled)
                    {
                        light = candidate;
                        break;
                    }
                }
            }

            // 回退③：一盏都没有 —— 只报警不抛异常，让地图其余子系统照常初始化。
            // UpdateRender 内部对 light == null 已有守卫，不会继续崩。
            if (light == null)
            {
                Log.Error("未找到主光（MainLight / RenderSettings.sun / 任意启用的平行光），"
                    + "地图光照将保持场景默认值，请检查场景光源是否被禁用或改名", Log.LogType.World);
            }

            //
            //light.type = LightType.Directional;
            //light.shadows = LightShadows.Soft;
            UpdateRender();
        }

        public override void Clear()
        {
            base.Clear();
            if (light == null) return;
            //GameObject.Destroy(light.gameObject);
            light = null;
        }

        internal override void OnSave(BinaryWriter writer)
        {
            for (int i = 0; i < light_direction.Length; ++i)
            {
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
            for (int i = 0; i < light_direction.Length; ++i)
            {
                light_direction[i] = new Vector3((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                light_color[i] = new Color((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                light_intensity[i] = reader.ReadSingle();
                shadow_color[i] = new Color((float)reader.ReadSingle(), (float)reader.ReadSingle(), (float)reader.ReadSingle());
                shadow_strength[i] = reader.ReadSingle();
            }

            UpdateRender();
        }

        public override void UpdateRender()
        {
            if (light == null) return;
            //light.transform.rotation = Quaternion.Euler(light_direction[curSeason]);
            light.color = light_color[curSeason];
            //light.intensity = light_intensity[curSeason];
            //light.intensity = 1.2f;
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
