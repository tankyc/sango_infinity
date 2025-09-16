using System.IO;
using UnityEngine;

namespace Sango.Render
{

    public class MapWater : MapLayer
    {
        public MapLayer.LayerData [] waterLayers;
        public MapWater(MapRender map) : base(map)
        {
            waterLayers = new MapLayer.LayerData[1];
            waterLayers[0] = new MapLayer.LayerData(map.mapLayer, new Material(Shader.Find("Sango/water_urp")));
        }

        public override void UpdateLayerRenderQueue()
        {
            // 获取地形的差
            int oldLen = map.mapLayer.layerDatas.Length;

            for (int i = 0; i < layerDatas.Length; i++) {
                LayerData data = layerDatas[i];
                data.UpdateLayerIndex(oldLen + i);
            }
        }

    }
}
