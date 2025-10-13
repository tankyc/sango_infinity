using System.IO;
using UnityEngine;

namespace Sango.Render
{

    public class MapLayer : MapProperty
    {
        private Material waterMat;
        public class LayerData
        {
            public MapLayer layer;
            public string[] diffuseTexName = new string[4];
            public string[] normalTexName = new string[4];
            public string[] maskTexName = new string[4];
            public bool isLit = false;
            public Texture[] diffuse = new Texture[4];
            public Texture[] normal = new Texture[4];
            public Texture[] mask = new Texture[4];
            public Material material;
            public Vector2 textureScale = Vector2.one;
            public LayerData(MapLayer layer)
            {
                this.layer = layer;
                diffuse = new Texture[4] { Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
                normal = new Texture[4] { Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
                mask = new Texture[4] { Texture2D.blackTexture, Texture2D.blackTexture, Texture2D.blackTexture, Texture2D.blackTexture };
                material = new Material(Shader.Find("Sango/terrain_urp"));
            }

            public LayerData(MapLayer layer, Material mat)
            {
                this.layer = layer;
                diffuse = new Texture[4] { Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
                normal = new Texture[4] { Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture };
                mask = new Texture[4] { Texture2D.blackTexture, Texture2D.blackTexture, Texture2D.blackTexture, Texture2D.blackTexture };
                material = mat;
            }

            public void AutoLoadDiffuse()
            {
                for (int i = 0; i < 4; ++i)
                {
                    string seasonName = MapRender.SeasonNames[i];

                    Texture tex = layer.map.CreateTexture($"Terrain/{seasonName}/{diffuseTexName[i]}");
                    diffuse[i] = tex;
                    textureScale = new Vector2(layer.map.mapData.vertex_width * 32 / tex.width,
                                  -layer.map.mapData.vertex_height * 32 / tex.height);
                    if (i == layer.curSeason)
                    {
                        material.SetTexture("_MainTex", tex);
                        material.SetTextureScale("_MainTex", textureScale);
                    }
                }
            }

            public Texture GetDiffuse(int season)
            {
                return diffuse[season];
            }
            public string GetDiffuseName(int season)
            {
                return diffuseTexName[season];
            }
            public string SetDiffuseName(int season)
            {
                return diffuseTexName[season];
            }

            public void UpdateDiffuse(int index, Texture tex, bool updateScale = true)
            {
                diffuse[index] = tex;
                textureScale = new Vector2(32768 / tex.width, -32768 / tex.height);
                if (index == layer.curSeason)
                    UpdateMaterial(layer.curSeason);
            }

            public void UpdateDiffuse(int index, Texture tex, string texName, bool updateScale = true)
            {
                diffuseTexName[index] = texName;
                diffuse[index] = tex;
                textureScale = new Vector2(32768 / tex.width, -32768 / tex.height);
                if (index == layer.curSeason)
                    UpdateMaterial(layer.curSeason);
            }

            public void UpdateLayerIndex(int index)
            {
                material.renderQueue = 2000 + index;
            }

            public void UpdateTextureScale(Vector2 scale)
            {
                textureScale = scale;
                if (diffuse != null)
                {
                    material.SetTextureScale("_MainTex", textureScale);
                }
                if (normal != null)
                {
                    material.SetTextureScale("_BumpMap", textureScale);
                }
                if (mask != null)
                {
                    material.SetTextureScale("_MaskMap", textureScale);
                }
            }


            public void UpdateMaterial(int season)
            {
                if (material == null) return;

                if (diffuse != null)
                {
                    material.SetTexture("_MainTex", diffuse[season]);
                    material.SetTextureScale("_MainTex", textureScale);
                }
                if (isLit)
                {
                    if (normal != null)
                    {
                        material.SetTexture("_BumpMap", normal[season]);
                        material.SetTextureScale("_BumpMap", textureScale);

                    }
                    if (mask != null)
                    {
                        material.SetTexture("_MaskMap", mask[season]);
                        material.SetTextureScale("_MaskMap", textureScale);
                    }
                }
            }
            public void SetLit(bool b)
            {
                isLit = b;
                if (b)
                {
                    // material.shader = Shader.Find("Universal Render Pipeline/Lit");
                    material.shader = Shader.Find("Sango/terrain_urp");
                }
                else
                {
                    material.shader = Shader.Find("Sango/terrain_urp");
                }
            }
            internal void OnSave(BinaryWriter writer)
            {
                writer.Write(isLit);
                writer.Write(textureScale.x);
                writer.Write(textureScale.y);
                for (int i = 0; i < diffuseTexName.Length; i++)
                {
                    if (string.IsNullOrEmpty(diffuseTexName[i]))
                        writer.Write("");
                    else
                        writer.Write(diffuseTexName[i]);
                    if (string.IsNullOrEmpty(normalTexName[i]))
                        writer.Write("");
                    else
                        writer.Write(normalTexName[i]);
                    if (string.IsNullOrEmpty(maskTexName[i]))
                        writer.Write("");
                    else
                        writer.Write(maskTexName[i]);
                }
            }
            internal void OnLoad(int versionCode, BinaryReader reader)
            {
                isLit = reader.ReadBoolean();
                textureScale.x = reader.ReadSingle();
                textureScale.y = reader.ReadSingle();
                for (int i = 0; i < diffuseTexName.Length; i++)
                {
                    diffuseTexName[i] = reader.ReadString();
                    string seasonName = MapRender.SeasonNames[i];

                    if (!string.IsNullOrEmpty(diffuseTexName[i]))
                    {

                        // 老版本名字修复
                        if (versionCode < 6)
                        {
                            string[] fixName = diffuseTexName[i].Split('_');
                            if (fixName.Length > 1)
                            {
                                if (diffuseTexName[i].StartsWith("water"))
                                {
                                    diffuseTexName[i] = "water_" + fixName[1];
                                }
                                else
                                    diffuseTexName[i] = "layer_" + fixName[1];
                            }
                            else
                            {
                                if (diffuseTexName[i] == "water")
                                    diffuseTexName[i] = "water_0";
                            }
                        }

                        Texture tex = layer.map.CreateTexture($"Terrain/{seasonName}/{diffuseTexName[i]}");
                        diffuse[i] = tex;
                        textureScale = new Vector2(layer.map.mapData.vertex_width * 32 / tex.width,
                                      -layer.map.mapData.vertex_height * 32 / tex.height);
                        if (i == layer.curSeason)
                        {
                            material.SetTexture("_MainTex", tex);
                            material.SetTextureScale("_MainTex", textureScale);
                        }
                    }

                    normalTexName[i] = reader.ReadString();
                    if (!string.IsNullOrEmpty(normalTexName[i]))
                    {

                        Texture tex = layer.map.CreateTexture($"Terrain/{seasonName}/{normalTexName[i]}");
                        normal[i] = tex;
                        if (i == layer.curSeason)
                        {
                            material.SetTexture("_BumpMap", tex);
                            material.SetTextureScale("_BumpMap", textureScale);
                        }
                    }
                    maskTexName[i] = reader.ReadString();
                    if (!string.IsNullOrEmpty(maskTexName[i]))
                    {
                        Texture tex = layer.map.CreateTexture($"Terrain/{seasonName}/{maskTexName[i]}");
                        mask[i] = tex;
                        if (i == layer.curSeason)
                        {
                            material.SetTexture("_MaskMap", tex);
                            material.SetTextureScale("_MaskMap", textureScale);
                        }
                    }
                }
            }
        }

        public LayerData[] layerDatas;
        public MapLayer(MapRender map) : base(map)
        {
            waterMat = new Material(Shader.Find("Sango/water_urp"));
            layerDatas = new LayerData[2];
            layerDatas[0] = new LayerData(this);
            layerDatas[1] = new LayerData(this, waterMat);
        }

        public override void Init()
        {
            base.Init();
        }

        internal override void OnSave(BinaryWriter writer)
        {
            if (layerDatas != null)
                writer.Write(layerDatas.Length);
            else
                writer.Write(0);


            for (int i = 0; i < layerDatas.Length; i++)
            {
                LayerData data = layerDatas[i];
                data.OnSave(writer);

            }
        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            int layerSize = reader.ReadInt32();
            layerDatas = new LayerData[layerSize];
            for (int i = 0; i < layerDatas.Length; i++)
            {
                if (i < layerDatas.Length - 1)
                    layerDatas[i] = new LayerData(this);
                else
                    layerDatas[i] = new LayerData(this, waterMat);

                layerDatas[i].OnLoad(versionCode, reader);
                layerDatas[i].UpdateLayerIndex(i);
            }
        }

        public void SetNormal(int index, Texture texture)
        {
            if (layerDatas == null || index < 0 || index >= layerDatas.Length)
                return;
            LayerData layerData = layerDatas[index];
            layerData.normal[curSeason] = texture;
            layerData.normalTexName[curSeason] = texture.name;
            layerData.UpdateMaterial(curSeason);
        }

        public void SetMask(int index, Texture texture)
        {
            if (layerDatas == null || index < 0 || index >= layerDatas.Length)
                return;
            LayerData layerData = layerDatas[index];
            layerData.mask[curSeason] = texture;
            layerData.maskTexName[curSeason] = texture.name;
            layerData.UpdateMaterial(curSeason);
        }

        public void SetDiffuse(int index, Texture texture)
        {
            if (layerDatas == null || index < 0 || index >= layerDatas.Length)
                return;
            LayerData layerData = layerDatas[index];
            layerData.diffuse[curSeason] = texture;
            layerData.diffuseTexName[curSeason] = texture.name;
            layerData.UpdateMaterial(curSeason);
        }

        public void SetDiffuse(int index, Texture texture, string texName)
        {
            if (layerDatas == null || index < 0 || index >= layerDatas.Length)
                return;
            LayerData layerData = layerDatas[index];
            layerData.diffuse[curSeason] = texture;
            layerData.diffuseTexName[curSeason] = texName;
            layerData.UpdateMaterial(curSeason);
        }

        public LayerData AddLayer()
        {
            LayerData layer = new LayerData(this);
            int oldLen = layerDatas.Length;
            System.Array.Resize(ref layerDatas, oldLen + 1);
            if (oldLen > 0)
            {
                layerDatas[oldLen] = layerDatas[oldLen - 1];
                layerDatas[oldLen - 1] = layer;
                layer.UpdateLayerIndex(oldLen - 1);
            }
            else
            {
                layerDatas[oldLen] = layer;
                layer.UpdateLayerIndex(oldLen);
            }
            return layer;
        }

        public virtual void UpdateLayerRenderQueue()
        {
            for (int i = 0; i < layerDatas.Length; i++)
            {
                LayerData data = layerDatas[i];
                data.UpdateLayerIndex(i);
            }
        }

        public LayerData RemoveLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= layerDatas.Length)
                return default(LayerData);

            LayerData[] newLayers = new LayerData[layerDatas.Length - 1];
            LayerData layer = layerDatas[layerIndex];
            System.Array.Copy(layerDatas, 0, newLayers, 0, layerIndex + 1);
            System.Array.Copy(layerDatas, layerIndex + 1, newLayers, layerIndex, layerDatas.Length - layerIndex - 1);
            UpdateLayerRenderQueue();
            return layer;
        }
        public LayerData GetLayer(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= layerDatas.Length)
                return default(LayerData);
            return layerDatas[layerIndex];
        }
        public bool SetLayer(int layerIndex, LayerData data)
        {
            if (layerIndex < 0 || layerIndex >= layerDatas.Length)
                return false;
            layerDatas[layerIndex] = data;
            return true;
        }
        public void SwapLayer(int layerIndex1, int layerIndex2)
        {
            if (layerIndex1 < 0 || layerIndex1 >= layerDatas.Length)
                return;
            if (layerIndex2 < 0 || layerIndex2 >= layerDatas.Length)
                return;
            LayerData layer = layerDatas[layerIndex1];
            layerDatas[layerIndex1] = layerDatas[layerIndex2];
            layerDatas[layerIndex2] = layer;
            UpdateLayerRenderQueue();
        }
        public override void UpdateRender()
        {
            for (int i = 0; i < layerDatas.Length; i++)
            {
                LayerData data = layerDatas[i];
                data.UpdateMaterial(curSeason);
                data.UpdateLayerIndex(i);
            }
        }

    }
}
