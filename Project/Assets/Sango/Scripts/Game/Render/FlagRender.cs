using Sango.Core;
using UnityEngine;

namespace Sango.Render
{
    public class FlagRender : MonoBehaviour
    {
        public float flagW = 25;
        public float flagH = 19;
        public int xCount = 10;
        public float flagTexWidth = 256;
        public float flagTexHeight = 256;
        public int flagId = 40;
        public bool needText = true;
        public void Init(Force force)
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            Material baseMat = renderer.material;
            if (needText)
            {
                baseMat.SetTexture("_TextTex", TextFactory.Instance.GetTexture(force.Name.Substring(0, 1), 40));
            }
            else
            {
                baseMat.SetTexture("_TextTex", Texture2D.blackTexture);
            }

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);

            int final_flag_id = flagId + force.mFlag.Id % 6;

            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;
            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight));
            baseMat.SetTextureOffset("_BaseMap", textOffset);
            baseMat.SetColor("_Color", force.mFlag.color);
        }

        public void Init(Troop troop)
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            Material baseMat = renderer.material;

            if (needText)
            {
                baseMat.SetTexture("_TextTex", TextFactory.Instance.GetTexture(troop.Name.Substring(0, 1), 40));
            }
            else
            {
                baseMat.SetTexture("_TextTex", Texture2D.blackTexture);
            }

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);
            int final_flag_id = flagId + troop.BelongForce.mFlag.Id % 6;

            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;

            baseMat.SetColor("_Color", troop.BelongForce.mFlag.color);

            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight));
            baseMat.SetTextureOffset("_BaseMap", textOffset);

        }

        public void Init(Building building)
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            Material baseMat = renderer.material;


            baseMat.SetTexture("_TextTex", Texture2D.blackTexture);

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);
            int final_flag_id = flagId + building.mForce.mFlag.Id % 6;

            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;

            baseMat.SetColor("_Color", building.mForce.mFlag.color);

            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight));
            baseMat.SetTextureOffset("_BaseMap", textOffset);
        }

        [ContextMenu("test")]
        public void Test()
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            Material outlineMat = renderer.sharedMaterials[0];
            Material baseMat = renderer.sharedMaterials[1];

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            outlineMat.SetTextureScale("_BaseMap", textScale);
            baseMat.SetTextureScale("_BaseMap", textScale);
            int x = flagId % xCount;
            int y = flagId / xCount;

            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight) - 0.075f);
            outlineMat.SetTextureOffset("_BaseMap", textOffset);
            baseMat.SetTextureOffset("_BaseMap", textOffset);

        }
    }
}
