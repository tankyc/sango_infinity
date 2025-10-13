using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/Effects/Gradient")]
    public class Gradient : BaseMeshEffect
    {
        [SerializeField]
        private Color32 topColor = Color.white;

        public Color32 TopColor
        {
            get { return topColor; }
            set { topColor = value; }
        }
        [SerializeField]
        private Color32 bottomColor = Color.black;

        public Color32 BottomColor
        {
            get { return bottomColor; }
            set { bottomColor = value; }
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
            {
                return;
            }

            var count = vh.currentVertCount;
            if (count == 0)
                return;

            var vertexs = new List<UIVertex>();
            for (var i = 0; i < count; i++)
            {
                var vertex = new UIVertex();
                vh.PopulateUIVertex(ref vertex, i);
                vertexs.Add(vertex);
            }

            var topY = vertexs[0].position.y;
            var bottomY = vertexs[0].position.y;

            for (var i = 1; i < count; i++)
            {
                var y = vertexs[i].position.y;
                if (y > topY)
                {
                    topY = y;
                }
                else if (y < bottomY)
                {
                    bottomY = y;
                }
            }

            var height = topY - bottomY;
            for (var i = 0; i < count; i++)
            {
                var vertex = vertexs[i];

                //使用处理过后的颜色
                var color = Color32.Lerp(bottomColor, topColor, (vertex.position.y - bottomY) / height);
                //var color = CenterColor(bottomColor, topColor, (vertex.position.y - bottomY) / height);
                color.a = vertex.color.a;
                vertex.color = color;

                vh.SetUIVertex(vertex, i);
            }

        }

    }
}
