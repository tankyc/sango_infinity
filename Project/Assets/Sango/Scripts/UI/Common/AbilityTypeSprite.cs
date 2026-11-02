using Sango.Core;
using UnityEngine;

namespace Sango.UI
{
    /// <summary>
    /// 能力"种类底图"的统一取图入口 —— 研究界面节点（<see cref="UIAbilityStudyNode"/>）与
    /// 培育界面节点（<see cref="UIAbilityLearnNode"/>）共用同一套编码，避免两处各写一份取图逻辑。
    ///
    /// <para>
    /// 【编码】种类下标与 <see cref="AbilityStatKind"/> 一致：
    /// 0 武力（洋红 <c>_8</c>）/ 1 统率（紫 <c>_6</c>）/ 2 政治（绿 <c>_7</c>）/ 3 智力（黄 <c>_9</c>）。
    /// 四张图取自原版研究界面隐藏位的底色调。
    /// </para>
    ///
    /// <para>
    /// 【取图优先级】Inspector / prefab 上配了 <c>typeSprites[kind]</c> 就用配的；
    /// 没配才按 <see cref="Folder"/> + <see cref="Files"/> 从 Mod Content 包内加载（同一张只加载一次）。
    /// </para>
    /// </summary>
    public static class AbilityTypeSprite
    {
        /// <summary>种类底图的张数（武力 / 统率 / 政治 / 智力 各一张）</summary>
        public const int Count = 4;

        /// <summary>种类底图目录（Mod Content 包内相对路径，与能力图集口径一致）</summary>
        public const string Folder = "Assets/UI/AtlasTexture/4846-34";

        /// <summary>
        /// 种类底图文件名，下标 = statKind：
        /// 0 武力 = <c>_8</c>（洋红）、1 统率 = <c>_6</c>（紫）、2 政治 = <c>_7</c>（绿）、3 智力 = <c>_9</c>（黄）。
        /// </summary>
        static readonly string[] Files = { "4846-34_8", "4846-34_6", "4846-34_7", "4846-34_9" };

        /// <summary>按种类加载后的缓存（静态是为了所有节点共享，同一张图只读一次）</summary>
        // 注意：项目里有名为 Sprite 的命名空间，类型必须写全名 UnityEngine.Sprite
        static readonly UnityEngine.Sprite[] Cache = new UnityEngine.Sprite[Count];

        /// <summary>
        /// 取能力的显示种类（决定 type 底图）。
        ///
        /// <para>
        /// 首选数据字段 <see cref="Ability.displayKind"/>（按原版节点底色逐条录入）；
        /// 未配置（-1）时兜底：隐藏槽按槽位种类，其余按 <see cref="Ability.statKind"/>（同一套编码）。
        /// </para>
        /// </summary>
        /// <param name="ability">能力</param>
        /// <returns>种类下标；无法判定返回 -1（此时保留 prefab 默认底图）</returns>
        public static int GetKind(Ability ability)
        {
            if (ability == null)
                return -1;
            if (ability.displayKind >= 0)
                return ability.displayKind;
            if (ability.hiddenSlot >= 0)
                return AbilityStudySystem.GetHiddenSlotStatKind(ability.hiddenSlot);
            return ability.statKind;
        }

        /// <summary>
        /// 取某种类的底图。
        /// </summary>
        /// <param name="kind">种类（<see cref="AbilityStatKind"/>）</param>
        /// <param name="overrides">Inspector / prefab 上手工配的图（可为 null）</param>
        /// <returns>底图；种类越界或加载失败返回 null</returns>
        public static UnityEngine.Sprite Get(int kind, UnityEngine.Sprite[] overrides)
        {
            if (kind < 0 || kind >= Count)
                return null;

            if (overrides != null && kind < overrides.Length && overrides[kind] != null)
                return overrides[kind];

            if (Cache[kind] == null)
                Cache[kind] = Sango.Loader.ObjectLoader.LoadObject<UnityEngine.Sprite>($"{Folder}/{Files[kind]}.png");
            return Cache[kind];
        }
    }
}
