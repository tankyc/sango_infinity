using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sango.Render
{

    public class MapSkyBox : MapProperty
    {
        public float sky_radius_len = 250;
        public float sky_height = 80;
        public float sky_offset = -2.9f;
        public string sky_material_shader = "Sango/skybox";
        public float sky_blend_start = -230f;
        public float sky_blend_end = -210f;
        public float sky_mix_begin = 700f;
        public float sky_mix_end = 900f;
        public float sky_mix_power = 7.5f;
        internal class SkyArea
        {
            public UnityEngine.Rect bounds;
            private MapSkyBox skyBox;
            public string[] seasonTextureNames = new string[4];
            public Texture[] seasonTextures = new Texture[4];

            public SkyArea(MapSkyBox master)
            {
                skyBox = master;
            }

            public void SetTextrueNames(string[] names)
            {
                if (names == null || names.Length != 4)
                    return;
                seasonTextureNames = names;
                for (int i = 0; i < seasonTextureNames.Length; ++i) {
                    string seasonName = MapRender.SeasonNames[i];

                    Texture tex = skyBox.map.CreateTexture($"Sky/{seasonName}/{seasonTextureNames[i]}");
                    seasonTextures[i] = tex;
                    if (this == skyBox.curArea)
                    {
                        skyBox.UpdateRender();
                    }
                }
            }

            public void SetTextrueNames(string[] names, Texture[] tex)
            {
                if (names == null || names.Length != 4)
                    return;

                if (tex == null || tex.Length != 4)
                    return;

                seasonTextureNames = names;
                seasonTextures = tex;
            }

            public Texture GetTexture(int season)
            {
                return seasonTextures[season];
            }

            public Texture GetTexture()
            {
                return seasonTextures[skyBox.curSeason];
            }

            internal void OnSave(BinaryWriter writer)
            {
                for (int i = 0; i < 4; ++i) {
                    if (!string.IsNullOrEmpty(seasonTextureNames[i]))
                        writer.Write(seasonTextureNames[i]);
                    else
                        writer.Write("");
                }
            }

            internal void OnLoad(int versionCode, BinaryReader reader)
            {
                if (versionCode < 2) return;

                for (int i = 0; i < 4; ++i) {
                    seasonTextureNames[i] = reader.ReadString();
                    string seasonName = MapRender.SeasonNames[i];
                    if (!string.IsNullOrEmpty(seasonTextureNames[i])) {

                        Texture tex = skyBox.map.CreateTexture($"Sky/{seasonName}/{seasonTextureNames[i]}");
                        seasonTextures[i] = tex;
                        if (this == skyBox.curArea)
                        {
                            skyBox.UpdateRender();
                        }
                    }
                }
            }
        }

        internal SkyArea curArea;
        internal List<SkyArea> allAreas = new List<SkyArea>();

        Material skyMat;
        Mesh[] skyMeshes;
        Transform skyTrans;

        public MapSkyBox(MapRender map) : base(map)
        {


        }

        public override void Init()
        {
            base.Init();

            Transform trans = Create();
            trans.SetParent(map.mapCamera.GetCenterTransform(), false);
        }
        public override void Clear()
        {
            base.Clear();
            allAreas.Clear();
            if (skyTrans != null)
            {
                GameObject.Destroy(skyTrans.gameObject);
                skyTrans = null;
            }
            GameObject.Destroy(skyMat);
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(sky_blend_start);
            writer.Write(sky_blend_end);

            writer.Write(allAreas.Count);
            for (int i = 0; i < allAreas.Count; i++) {
                SkyArea area = allAreas[i];
                writer.Write(area.bounds.x);
                writer.Write(area.bounds.y);
                writer.Write(area.bounds.width);
                writer.Write(area.bounds.height);

                for (int j = 0; j < 4; j++) {
                    string s = area.seasonTextureNames[j];
                    if (!string.IsNullOrEmpty(s))
                        writer.Write(s);
                    else
                        writer.Write("");
                }
            }

        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            curArea = null;
            allAreas.Clear();
            // 版本1的加载
            if (versionCode == 1) {

                blendStart = reader.ReadSingle();
                blendEnd = reader.ReadSingle();
                int length = reader.ReadInt32();
                //string[] texNames = new string[length];
                for (int i = 0; i < length; ++i) {
                    //texNames[i] = reader.ReadString();
                    reader.ReadString();
                }
                //skyTextures = texNames;
                //textrueIndex = reader.ReadInt32();
                reader.ReadInt32();
            }
            else {
                blendStart = reader.ReadSingle();
                blendEnd = reader.ReadSingle();
                int length = reader.ReadInt32();
                for (int i = 0; i < length; ++i) {
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float w = reader.ReadSingle();
                    float h = reader.ReadSingle();
                    string[] texNames = new string[4];
                    for (int j = 0; j < 4; ++j)
                        texNames[j] = reader.ReadString();

                    SkyArea area = new SkyArea(this)
                    {
                        bounds = new UnityEngine.Rect(x, y, w, h),
                    };
                    allAreas.Add(area);
                    area.SetTextrueNames(texNames);
                }
            }
        }

        public float blendStart
        {
            get; set;
        }
        public float blendEnd
        {
            get; set;
        }


        void _CreateMaterial()
        {
            skyMat = new Material(Shader.Find(sky_material_shader));
            skyMat.SetFloat("_BeginHeight", sky_blend_start);
            skyMat.SetFloat("_EndHeight", sky_blend_end);
            skyMat.SetFloat("_MixBegin", sky_mix_begin);
            skyMat.SetFloat("_MixEnd", sky_mix_end);
            skyMat.SetFloat("_MixPower", sky_mix_power);
            skyMat.renderQueue = 3001;
        }

        public Transform Create()
        {
            float height = 1;
            skyMeshes = new Mesh[4];
            Vector3[] vertex = new Vector3[85];
            Vector2[] uv = new Vector2[85];
            int[] indexs = new int[16 * 4 * 2 * 3];

            GameObject sky = new GameObject("skyBox");
            skyTrans = sky.transform;

            _CreateMaterial();

            skyTrans.localScale = new Vector3(sky_radius_len, sky_height, sky_radius_len);
            for (int i = 0; i < skyMeshes.Length; i++) {

                Mesh mesh = new Mesh();
                for (int j = 0; j < 17; ++j) {
                    Vector3 pos = Quaternion.Euler(0, 90.0f * j / 16.0f, 0) * Vector3.forward * 5;
                    for (int k = 0; k < 5; ++k) {
                        pos.y = k * height;
                        vertex[j * 5 + k] = pos;
                        uv[j * 5 + k] = new Vector2(j / 16.0f, (4 * height - (float)pos.y) * -0.25f);
                    }
                }

                int tIndex = 0;
                for (int j = 0; j < 16; ++j) {
                    for (int k = 0; k < 4; ++k) {
                        //k,k+1,(j+1)*5
                        indexs[tIndex++] = (j * 5) + k;
                        indexs[tIndex++] = (j * 5) + k + 1;
                        indexs[tIndex++] = (j + 1) * 5 + k;

                        indexs[tIndex++] = (j * 5) + k + 1;
                        indexs[tIndex++] = (j + 1) * 5 + k + 1;
                        indexs[tIndex++] = (j + 1) * 5 + k;
                    }
                }

                mesh.vertices = vertex;
                mesh.triangles = indexs;
                mesh.uv = uv;
                mesh.RecalculateBounds();
                skyMeshes[i] = mesh;

                GameObject sky2 = new GameObject("sky");
                sky2.transform.SetParent(sky.transform, false);
                MeshFilter mf = sky2.AddComponent<MeshFilter>();
                MeshRenderer mR = sky2.AddComponent<MeshRenderer>();
                mf.mesh = mesh;
                mR.sharedMaterial = skyMat;

                sky2.transform.localRotation = Quaternion.Euler(0, 90 * i, 0);
                sky2.transform.localPosition = new Vector3(0, sky_offset, 0);
            }

            SkyArea area = new SkyArea(this)
            {
                bounds = new UnityEngine.Rect(0, 0, map.mapData.world_width, map.mapData.world_height),
                seasonTextures = new Texture[4]
                {
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture,
                    Texture2D.whiteTexture
                },

            };

            curArea = area;
            allAreas.Add(curArea);

            skyMat.mainTexture = curArea.GetTexture();

            return skyTrans;
        }
        public void SetVisible(bool b)
        {
            if (skyTrans != null) {
                skyTrans.gameObject.SetActive(b);
            }
        }
        public override void UpdateRender()
        {
            skyMat.mainTexture = curArea.GetTexture();
        }

        public override void Update()
        {
            if (Time.frameCount % 2 == 0)
                return;

            // 优先判断后面的区域
            Vector3 centerPos = skyTrans.position;
            Vector2 point = new Vector2(centerPos.z, centerPos.x);
            if (curArea != null && curArea.bounds.Contains(point))
                return;

            for (int i = allAreas.Count - 1; i >= 0; i--) {
                SkyArea area = allAreas[i];
                if (area == curArea) continue;
                if (area.bounds.Contains(point)) {
                    if (area != curArea) {
                        curArea = area;
                        UpdateRender();
                        return;
                    }
                }
            }
        }
    }
}
