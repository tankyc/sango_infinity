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
        /// 旗面文字是否使用"全场共享文字图集"。
        ///
        /// 【为什么这样可以合批】
        /// 原先每面旗调用 TextFactory.GetTexture(字, 40)，而 TextFactory 对每个字符串
        /// 都新建一张 64×64 的 RenderTexture —— 几百面旗就有几百张不同的贴图，
        /// 于是每面旗必然是一次独立的 SetPass，无论材质怎么优化都不可能合批。
        ///
        /// 改成共享图集后，所有旗帜的 _TextTex 指向同一张纹理；每面旗仍是自己的材质实例
        /// （_Color / _BaseMap_ST / _TextTex_ST 各不相同），但 SRP Batcher 是**按 shader**
        /// 合并批次的，材质实例不同并不妨碍它们进入同一个 "SRP Batch"。
        ///
        /// 【注意】这里刻意不去改旗帜网格的 uv / 顶点色：模型网格是导入资源，
        /// 默认 isReadable = false，任何 Mesh.uv / SetUVs / colors 的读写都会直接报错。
        /// 所有 per-flag 差异都通过材质属性表达，零网格操作。
        ///
        /// false 时回到旧路径（每个字一张 RT），用于快速回滚。
        /// </summary>
        public static bool UseSharedTextAtlas = true;

        /// <summary>
        /// 上一次写入时的部队ID / 势力ID。
        /// 【刷新治理】SetColor、SetTexture 每次都会把材质标脏，而表现层刷新调用很频繁，
        /// 所以这里做"值没变就不写"的守卫。
        /// </summary>
        int lastTroopId = -1;
        int lastForceId = -1;

        /// <summary>已提示过"势力名 / 旗号缺失"的势力 Id，避免地图上每面旗各刷一条日志。</summary>
        static readonly HashSet<int> warnedForceIds = new HashSet<int>();

        /// <summary>缓存的渲染器（可能是 SkinnedMeshRenderer，也可能是普通 MeshRenderer）</summary>
        Renderer cachedRenderer;

        Renderer GetRenderer()
        {
            if (cachedRenderer == null)
                cachedRenderer = GetComponentInChildren<Renderer>(true);
            return cachedRenderer;
        }

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
        /// 这里统一降级：文字缺失写黑格、旗号缺失用 0 号偏移与白色，绝不抛异常。
        /// </summary>
        /// <param name="force">旗帜颜色/图集来源势力（可为 null）</param>
        /// <param name="textName">旗面文字来源名称（null 表示不写字，写黑格）</param>
        void ApplyFlag(Force force, string textName)
        {
            Renderer renderer = GetRenderer();
            if (renderer == null)
                return;

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
            bool hasText = needText && !string.IsNullOrEmpty(textName);

            Material baseMat = renderer.material;

            ApplyTextTexture(baseMat, hasText ? textName : null);

            // 旗号图集（与原实现逐字一致）
            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);

            int x = final_flag_id % xCount;
            int y = final_flag_id / xCount;
            Vector2 textOffset = new Vector2(x * (flagW / flagTexWidth) - 0.003f, -y * (flagH / flagTexHeight));
            baseMat.SetTextureOffset("_BaseMap", textOffset);
            baseMat.SetColor("_Color", flagColor);
        }

        /// <summary>
        /// 设置旗面文字层。
        ///
        /// 共享图集路径：_TextTex 指向 1024×1024 的文字图集，用 _TextTex_ST 把 0~1 的
        /// 旗面 uv 映射到该字符所占的那一格（shader 侧是 TRANSFORM_TEX(v.uv, _TextTex)）。
        /// 未写字时指向图集末尾预清好的纯黑格，保持与旧实现（blackTexture）一致的观感。
        ///
        /// 旧路径：每个字一张 64×64 RenderTexture，_TextTex_ST 复位成 (1,1,0,0)。
        /// </summary>
        void ApplyTextTexture(Material baseMat, string textChar)
        {
            // 【无文字】与原始实现完全一致：整面用 1×1 纯黑贴图。
            // 刻意不让它去图集里找"黑格"—— 图集里根本没有黑格，
            // 任何一次格子内容/UV 出错都会变成"满地图黑板"，这条路径必须与图集彻底隔离。
            if (string.IsNullOrEmpty(textChar))
            {
                baseMat.SetTexture("_TextTex", Texture2D.blackTexture);
                baseMat.SetTextureScale("_TextTex", Vector2.one);
                baseMat.SetTextureOffset("_TextTex", Vector2.zero);
                return;
            }

            if (UseSharedTextAtlas)
            {
                Texture atlas = FlagTextAtlas.Atlas;
                if (atlas != null)
                {
                    Vector4 rect = FlagTextAtlas.GetRect(textChar);
                    baseMat.SetTexture("_TextTex", atlas);
                    baseMat.SetTextureScale("_TextTex", new Vector2(rect.z, rect.w));
                    baseMat.SetTextureOffset("_TextTex", new Vector2(rect.x, rect.y));
                    return;
                }
            }

            // 回退路径：每个字一张 64×64 RenderTexture
            if (TextFactory.Instance != null)
                baseMat.SetTexture("_TextTex", TextFactory.Instance.GetTexture(textChar.Substring(0, 1), 40));
            else
                baseMat.SetTexture("_TextTex", Texture2D.blackTexture);

            // 旧路径是"整张字图铺满旗面"，必须把可能残留的图集偏移清掉
            baseMat.SetTextureScale("_TextTex", Vector2.one);
            baseMat.SetTextureOffset("_TextTex", Vector2.zero);
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
            Renderer renderer = GetRenderer();
            if (renderer == null) return;

            Material[] mats = renderer.sharedMaterials;
            if (mats == null || mats.Length == 0) return;
            Material baseMat = mats[0];

            Vector2 textScale = new Vector2(flagW / flagTexWidth, flagH / flagTexHeight);
            baseMat.SetTextureScale("_BaseMap", textScale);
            int x = flagId % xCount;
            int y = flagId / xCount;

            Vector2 textOffset = new Vector2(
                x * (flagW / flagTexWidth) - 0.003f,
                -y * (flagH / flagTexHeight) - 0.075f);
            baseMat.SetTextureOffset("_BaseMap", textOffset);
        }
    }
}
