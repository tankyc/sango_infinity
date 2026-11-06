using Sango.Core;
using System.Collections.Generic;
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

        /// <summary>已提示过"势力名 / 旗号缺失"的势力 Id，避免地图上每面旗各刷一条日志。</summary>
        static readonly HashSet<int> warnedForceIds = new HashSet<int>();

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

        /// <summary>
        /// 三个 Init 重载的统一实现：写旗面文字贴图 + 旗帜图集偏移 + 势力色。
        ///
        /// 【为什么统一】原来三个重载各自裸解引用 <c>force.Name.Substring(0, 1)</c>、
        /// <c>force.mFlag.Id</c>、<c>troop.BelongForce.mFlag</c>、<c>building.BelongForce.mFlag</c>，
        /// 任意一项缺失就抛 NullReferenceException：
        ///   · 势力名为 null（自定义势力 / 异民族 / 数据缺失）→ 实测栈
        ///     BuildingBaseModel.Init → FlagRender.Init:50，表现为开图时地图刷新崩溃；
        ///   · 旗号 mFlag 为 null（旗号表缺失）→ 取 Id/color 同样崩；
        ///   · 旗帜模型自身没有 Renderer（预制体残缺）。
        /// 这里统一降级：文字缺失写黑图、旗号缺失用 0 号偏移与白色，绝不抛异常。
        /// </summary>
        /// <param name="force">旗帜颜色/图集来源势力（可为 null）</param>
        /// <param name="textName">旗面文字来源名称（null 表示不写字，写黑图）</param>
        void ApplyFlag(Force force, string textName)
        {
            Renderer renderer = GetComponentInChildren<Renderer>(true);
            if (renderer == null)
                return;
            Material baseMat = renderer.material;

            if (needText && !string.IsNullOrEmpty(textName) && TextFactory.Instance != null)
            {
                baseMat.SetTexture("_TextTex", TextFactory.Instance.GetTexture(textName.Substring(0, 1), 40));
            }
            else
            {
                baseMat.SetTexture("_TextTex", Texture2D.blackTexture);
            }

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);

            int flagOffsetIndex = 0;
            Color flagColor = Color.white;
            if (force != null && force.mFlag != null)
            {
                flagOffsetIndex = force.mFlag.Id % 6;
                flagColor = force.mFlag.color;
            }

            // 定位用：势力名/旗号缺失属于数据问题，每个势力只提示一次，不刷屏
            if (force != null && warnedForceIds.Add(force.Id))
            {
                if (string.IsNullOrEmpty(force.Name))
                    Sango.Log.Warning($"*[旗帜]势力{force.Id} 没有名称(Force.Name 为空)，旗面文字已退化为黑图");
                if (force.mFlag == null)
                    Sango.Log.Warning($"*[旗帜]势力{force.Id} 没有旗号(mFlag 为空)，旗帜颜色已退化为白色");
            }

            int final_flag_id = flagId + flagOffsetIndex;
            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;
            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight));
            baseMat.SetTextureOffset("_BaseMap", textOffset);
            baseMat.SetColor("_Color", flagColor);
        }

        /// <summary>
        /// 按势力刷新旗帜（旗面写字用势力名）。
        /// </summary>
        /// <param name="force">势力（可为 null）</param>
        public void Init(Force force)
        {
            ApplyFlag(force, force != null ? force.Name : null);
        }

        /// <summary>
        /// 按部队刷新旗帜（旗面写字用部队名，颜色/图集取所属势力）。
        /// </summary>
        /// <param name="troop">部队（可为 null）</param>
        public void Init(Troop troop)
        {
            ApplyFlag(troop != null ? troop.BelongForce : null, troop != null ? troop.Name : null);
        }

        /// <summary>
        /// 按建筑刷新旗帜（只上色，不写字）。
        /// </summary>
        /// <param name="building">建筑（可为 null）</param>
        public void Init(Building building)
        {
            ApplyFlag(building != null ? building.BelongForce : null, null);
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
