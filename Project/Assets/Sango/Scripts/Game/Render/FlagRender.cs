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

        /// <summary>
        /// 上一次写入材质时的部队ID / 势力ID。
        /// 【刷新治理】SetColor、SetTexture 每次都会把材质标脏,而表现层刷新调用很频繁,
        /// 所以这里做"值没变就不写"的守卫,让刷新函数可以被高频调用而几乎零开销。
        /// </summary>
        int lastTroopId = -1;
        int lastForceId = -1;

        /// <summary>
        /// 刷新旗帜的所属势力(部队换旗时纠正颜色)。
        ///
        /// 背景:旗帜的旗帜色与旗面字之前只在模型加载时写一次(见 Init 的调用方 TroopModel.Init),
        /// 部队换势力后就会一直顶着旧势力的颜色,直到模型被重新加载才纠正 ——
        /// 也就是"登庸成功、部队却还是敌国颜色"的直接原因。
        /// </summary>
        /// <param name="troop">所属部队</param>
        public void UpdateForce(Troop troop)
        {
            if (troop == null) return;
            Force force = troop.BelongForce;
            if (force == null) return;
            if (lastTroopId == troop.Id && lastForceId == force.Id) return;

            lastTroopId = troop.Id;
            lastForceId = force.Id;
            Init(troop);
        }

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
            int final_flag_id = flagId + building.BelongForce.mFlag.Id % 6;

            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;

            baseMat.SetColor("_Color", building.BelongForce.mFlag.color);

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
