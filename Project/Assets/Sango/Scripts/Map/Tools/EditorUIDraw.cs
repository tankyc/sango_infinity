using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Sango.Render.MapLayer;

namespace Sango.Tools
{
    internal class EditorUIDraw
    {
        public static void OnGUI(Render.MapFog fog)
        {
            bool v = GUILayout.Toggle(fog.fogEnabled, "雾开关");
            if (fog.fogEnabled != v)
                fog.fogEnabled = v;

            if (fog.fogEnabled)
            {
                Tools.EditorUtility.ColorField(fog.fogColor, "雾颜色", (color) => { fog.fogColor = color; });
                fog.fogStart = Tools.EditorUtility.FloatField(fog.fogStart, "开始距离");
                fog.fogEnd = Tools.EditorUtility.FloatField(fog.fogEnd, "结束距离");
                fog.fogDensity = Tools.EditorUtility.FloatField(fog.fogDensity, "雾浓度");
            }
        }

        public static void OnGUI(Render.MapData data)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("地图大小");
            GUILayout.Label(data.vertex_width.ToString());
            GUILayout.Label(data.vertex_height.ToString());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("加载高度"))
            {
                data.LoadHeight();
            }
            if (GUILayout.Button("加载图层"))
            {
                data.LoadLayer();
            }
            if (GUILayout.Button("加载水"))
            {
                data.LoadWater();
            }
            GUILayout.EndHorizontal();
        }

        public static void OnGUI(Render.MapBaseColor data)
        {

        }

        public static void OnGUI(Render.MapGrid data)
        {
            GUI.changed = false;
            int size = Tools.EditorUtility.IntField(data.gridSize, "格子大小");
            if (GUI.changed)
            {
                data.gridSize = size;
            }
            if (GUILayout.Button("创建格子"))
            {
                data.Create(data.gridSize);
                data.SetGridTexture("grid");
            }
        }

        static Vector2 scrollPos_layer;
        public static void OnGUI(Render.MapLayer layer)
        {
            int count = layer.layerDatas.Length;
            scrollPos_layer = GUILayout.BeginScrollView(scrollPos_layer, GUILayout.Width(180), GUILayout.Height(87 * ((count < 5) ? count : 5)));
            for (int i = 0; i < count; i++)
            {
                Render.MapLayer.LayerData data = layer.layerDatas[i];
                GUILayout.Box("层: " + i, GUILayout.Width(168), GUILayout.Height(85));
                UnityEngine.Rect r = GUILayoutUtility.GetLastRect();
                OnGUI(data, r, i, i == layer.layerDatas.Length - 1);
            }
            GUILayout.EndScrollView();

            if (GUILayout.Button("添加"))
            {
                string[] path = WindowDialog.OpenFileDialog("贴图文件(*.png)|*.png\0", true);
                if (path != null)
                {
                    for (int i = 0; i < path.Length; ++i)
                    {
                        Render.MapLayer.LayerData data = layer.AddLayer();
                        string fileName = path[i];
                        data.diffuseTexName[layer.curSeason] = System.IO.Path.GetFileNameWithoutExtension(fileName);
                        Loader.TextureLoader.LoadFromFile(fileName, data, (UnityEngine.Object obj, object customData) =>
                        {
                            if (obj != null)
                            {
                                Texture tex = obj as Texture;
                                Render.MapLayer.LayerData ld = (Render.MapLayer.LayerData)customData;
                                ld.UpdateDiffuse(ld.layer.curSeason, tex);
                            }
                        });
                    }
                }
            }
            if (GUILayout.Button("覆盖贴图"))
            {
                string[] path = WindowDialog.OpenFileDialog("贴图文件(*.png)|*.png\0", true);
                if (path != null)
                {
                    for (int i = 0; i < path.Length; ++i)
                    {
                        Render.MapLayer.LayerData data = layer.GetLayer(i);
                        if (data == null) break;
                        string fileName = path[i];
                        Loader.TextureLoader.LoadFromFile(fileName, data, (UnityEngine.Object obj, object customData) =>
                        {
                            if (obj != null)
                            {
                                Texture tex = obj as Texture;
                                Render.MapLayer.LayerData ld = (Render.MapLayer.LayerData)customData;
                                ld.UpdateDiffuse(ld.layer.curSeason, tex, System.IO.Path.GetFileNameWithoutExtension(fileName));
                            }
                        });
                    }
                }
            }
        }

        public static int selectLayer = 0;
        public static void OnGUI(Render.MapLayer.LayerData layerData, UnityEngine.Rect r, int index, bool isWaterLayer)
        {
            int season = layerData.layer.curSeason;
            UnityEngine.Rect rect = r;
            rect.y += 16;
            rect.width = 64;
            rect.height = 64;
            if (GUI.Button(rect, layerData.GetDiffuse(season)))
            {
                if (UnityEngine.Event.current.button == 1)
                {
                    Tools.EditorUtility.OpenTexture("贴图文件(*.png)|*.png", layerData, (string fileName, UnityEngine.Object obj, object customData) =>
                    {
                        Render.MapLayer.LayerData ld = (Render.MapLayer.LayerData)customData;
                        if (obj != null)
                        {
                            Texture tex = obj as Texture;
                            ld.UpdateDiffuse(ld.layer.curSeason, tex, System.IO.Path.GetFileNameWithoutExtension(fileName));
                        }
                    });
                }
                else
                {
                    selectLayer = index;
                }
            }
            rect.x += 68;
            rect.height = 24;
            GUI.changed = false;
            GUI.enabled = false;
            Vector2 scale = Tools.EditorUtility.Vector2Field(rect, layerData.textureScale, "缩放", 30);
            if (GUI.changed)
            {
                layerData.UpdateTextureScale(scale);
            }
            GUI.enabled = true;
            rect.y += 28;
            if (isWaterLayer)
                GUI.Label(rect, "水面贴图 -> " + layerData.GetDiffuseName(season));
            else
                GUI.Label(rect, "贴图 -> " + layerData.GetDiffuseName(season));
        }

        public static void OnGUI(Render.MapLight light)
        {
            light.lightDirection = Tools.EditorUtility.Vector3Field(light.lightDirection, "灯光方向");
            Tools.EditorUtility.ColorField(light.lightColor, "灯光颜色", (color) => { light.lightColor = color; });
            light.lightIntensity = Tools.EditorUtility.FloatField(light.lightIntensity, "灯光强度");
            Tools.EditorUtility.ColorField(light.shadowColor, "阴影颜色", (color) => { light.shadowColor = color; });
            light.shadowStrength = Tools.EditorUtility.FloatField(light.shadowStrength, "阴影强度");

        }

        public static void OnGUI(Render.MapModels models)
        {

        }

        public static void OnGUI(Render.MapSkyBox skyBox)
        {
            if (GUILayout.Button("自动创建天空球区域"))
            {
                skyBox.curArea = null;
                skyBox.allAreas.Clear();
                for (int x = 0; x < 3; x++)
                {
                    for (int y = 0; y < 3; y++)
                    {
                        Render.MapSkyBox.SkyArea area = new Render.MapSkyBox.SkyArea(skyBox)
                        {
                            bounds = new UnityEngine.Rect(x * skyBox.map.mapData.world_width / 3, y * skyBox.map.mapData.world_height / 3,
                            skyBox.map.mapData.world_width / 3, skyBox.map.mapData.world_height / 3),
                        };
                        area.SetTextrueNames(new string[]
                        {
                            string.Format("4794山_{0}", y*3+x),
                            string.Format("4796山_{0}", y*3+x),
                            string.Format("4797山_{0}", y*3+x),
                            string.Format("4798山_{0}", y*3+x),
                        });

                        skyBox.allAreas.Add(area);
                    }
                }
            }
        }

        public static void OnGUI(Render.MapTerrain terrain)
        {

        }

        public static void OnGUI(Render.MapWater water)
        {

        }

        public static void OnGUI(Render.MapCamera camera)
        {
            camera.keyBoardMoveSpeed = Tools.EditorUtility.FloatField(camera.keyBoardMoveSpeed, "键盘移动速度");
            camera.limitDistance = Tools.EditorUtility.Vector2Field(camera.limitDistance, "相机距离");
        }

    }
}
