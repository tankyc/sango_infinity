/*
 * 文件名：AbilityTree.cs
 * 描述：能力路径图的**依赖关系**数据 —— 由 Abilities.json 的 requiredAbilityIds 推导。
 *
 * 【现状（六边形坐标改造后）】
 *   节点位置已改由数据字段 Ability.q / Ability.r 直接给出，界面上不再做自动布局，
 *   也不再画连线（前置改用节点模板的 dir/1~6 方向箭头表示）。
 *   本文件保留的 col / row / ring / ringSlot 等推算结果目前**没有消费者**，
 *   仅作为"依赖深度 / 最近根距离"的工具信息保留；UI 只用到 parents / children。
 *
 * 【为什么不能直接复用科技（Technique）的布局】
 *   · 科技是**单父树**：Technique.needTech 只有一个父，ForceTechnique.FillChildren /
 *     UpdateY 可以递归算 x/y，父节点天然居中于子节点。
 *   · 能力是**多父 DAG**：Ability.requiredAbilityIds 最多 3 个前置，且实测有跨类前置
 *     （例：基础能力「统+5中」前置是特技「金刚」）。树形递归会重复计数，
 *     因此这里改用"按最长前置链分列 + 列内按父行重心排序"的桑基式布局：
 *     列号 = 依赖深度，行号 = 列内位置，连线对**每一条**前置都画。
 *
 * 与科技界面的字段对应：
 *   Technique.level / ForceTechnique.GetY()  ←→  AbilityTreeNode.col / .row
 *   Technique.tabColor                       ←→  AbilityTreeBuilder.GetNodeColor(ability)
 *   ForceTechnique.parent（单父画线）         ←→  AbilityTreeNode.parents（多父全画）
 *
 * 纯数据 + 纯算法，不依赖任何 prefab / 场景对象，便于单独验证。
 */

using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 能力路径图上的一个节点（布局结果）。
    /// </summary>
    public class AbilityTreeNode
    {
        /// <summary>对应的能力数据</summary>
        public Ability ability;

        /// <summary>列号（依赖深度，0 = 无前置的根）</summary>
        public int col;

        /// <summary>行号（重心迭代后的整数结果，0 起）</summary>
        public int row;

        /// <summary>迭代过程中的浮点纵向坐标（布局内部用，最终结果见 <see cref="row"/>）</summary>
        public float y;

        /// <summary>全部有效前置（可能为空 = 根节点）</summary>
        public List<AbilityTreeNode> parents = new List<AbilityTreeNode>();

        /// <summary>全部后继</summary>
        public List<AbilityTreeNode> children = new List<AbilityTreeNode>();

        /// <summary>
        /// 距根的 BFS 最短距离（径向布局的"环号"，0 = 中心）。
        ///
        /// 与 <see cref="col"/> 的区别：<c>col</c> 是最长前置链长度（决定"研究顺序"），
        /// <c>ring</c> 是到任意根的最短路（决定"离中心多远"）。多父节点取最短，
        /// 因此环内可能出现"父比自己更外环"的情况 —— 这正是放弃深度分层换来的紧凑。
        /// </summary>
        public int ring;

        /// <summary>环内槽位序号（0 起，按父节点角度排序后赋值）</summary>
        public int ringSlot;

        /// <summary>该环节点总数（决定角度步长 = 2π / ringSlots）</summary>
        public int ringSlots;

        /// <summary>
        /// 理想角度（父节点角度的平均值，弧度）。
        /// 用于环内排序与槽位分配：子节点越贴近父的角度，父子连线越接近直线。
        /// </summary>
        public float idealAngle;

        /// <summary>
        /// 环上最终角度（弧度，0 = 正右，顺时针）。
        /// 由 <c>UIAbilityStudyArea</c> 在知道环半径后计算（需要按弦长算最小角间距）。
        /// 单父节点会尽量与父同角度，使整条链成为一条笔直的径向线 —— 这是消除连线交叉的关键。
        /// </summary>
        public float angle;

        /// <summary>是否为根（没有任何有效前置）</summary>
        public bool IsRoot => parents.Count == 0;
    }

    /// <summary>
    /// 能力路径图的布局构建器与主题色。
    /// </summary>
    public static class AbilityTreeBuilder
    {
        // ==================== 主题色 ====================
        //
        // 能力表（Abilities.json）没有颜色字段，科技表（Techniques.json）才有 tabColor；
        // 因此这里按"大类 + 维度"推导一组固定色，保证同类同色、跨存档稳定。
        // 只用于节点底色，不参与任何数值计算。

        /// <summary>基础能力：统率</summary>
        public static readonly Color32 ColorCommand = new Color32(196, 92, 74, 255);
        /// <summary>基础能力：武力</summary>
        public static readonly Color32 ColorStrength = new Color32(176, 116, 60, 255);
        /// <summary>基础能力：智力</summary>
        public static readonly Color32 ColorIntelligence = new Color32(74, 116, 176, 255);
        /// <summary>基础能力：政治</summary>
        public static readonly Color32 ColorPolitics = new Color32(96, 148, 108, 255);
        /// <summary>基础能力：魅力</summary>
        public static readonly Color32 ColorGlamour = new Color32(150, 96, 160, 255);

        /// <summary>兵种适性：枪 / 戟 / 弩 / 骑 / 器械 / 水军</summary>
        public static readonly Color32[] ColorAptitude =
        {
            new Color32(160, 132, 72, 255),   // 枪
            new Color32(150, 120, 88, 255),   // 戟
            new Color32(112, 140, 96, 255),   // 弩
            new Color32(132, 104, 140, 255),  // 骑
            new Color32(112, 112, 128, 255),  // 器械
            new Color32(76, 132, 168, 255),   // 水军
        };

        /// <summary>特技：按 Ability.statKind（0 武力 / 1 统率 / 2 政治 / 3 智力）</summary>
        public static readonly Color32[] ColorSkill =
        {
            new Color32(176, 116, 60, 255),   // 武力系
            new Color32(196, 92, 74, 255),    // 统率系
            new Color32(96, 148, 108, 255),   // 政治系
            new Color32(74, 116, 176, 255),   // 智力系
        };

        /// <summary>未知分类的兜底色</summary>
        public static readonly Color32 ColorUnknown = new Color32(120, 120, 120, 255);

        /// <summary>
        /// 取某条能力的节点底色。
        /// </summary>
        /// <param name="ability">能力</param>
        /// <returns>节点底色</returns>
        public static Color GetNodeColor(Ability ability)
        {
            if (ability == null)
                return ColorUnknown;

            if (ability.IsBaseStat)
            {
                switch ((AbilityStatKind)ability.personStatType)
                {
                    case AbilityStatKind.Leadership: return ColorCommand;
                    case AbilityStatKind.Strength: return ColorStrength;
                    case AbilityStatKind.Intelligence: return ColorIntelligence;
                    case AbilityStatKind.Politics: return ColorPolitics;
                    case AbilityStatKind.Charisma: return ColorGlamour;
                }
                return ColorUnknown;
            }

            if (ability.IsTroopAptitude)
            {
                // troopKind 从 1 开始（AbilityType：1 枪 … 6 水军）
                int index = ability.troopKind - 1;
                if (index >= 0 && index < ColorAptitude.Length)
                    return ColorAptitude[index];
                return ColorUnknown;
            }

            if (ability.statKind >= 0 && ability.statKind < ColorSkill.Length)
                return ColorSkill[ability.statKind];
            return ColorUnknown;
        }

        /// <summary>
        /// 取能力的分类短名（节点角标 / 详情用）。
        /// </summary>
        /// <param name="ability">能力</param>
        /// <returns>「基础」「适性」「特技」之一</returns>
        public static string GetCategoryName(Ability ability)
        {
            if (ability == null)
                return string.Empty;
            if (ability.IsBaseStat)
                return "基础";
            if (ability.IsTroopAptitude)
                return "适性";
            if (ability.IsSkill)
                return "特技";
            return "未知";
        }

        // ==================== 布局 ====================

        /// <summary>
        /// 构建能力路径图。
        ///
        /// 步骤：
        /// ① 建节点、按 requiredAbilityIds 连边（忽略 <= 0 与不存在的 Id）；
        /// ② 列号 = 最长前置链长度（迭代松弛，带环保护）；
        /// ③ 行号 = 列内按"父行重心"排序后依次占位，尽量贴近父节点以减少连线斜跨。
        /// </summary>
        /// <param name="abilities">能力表（scenario.CommonData.Abilities）</param>
        /// <returns>全部节点；表为空时返回空列表</returns>
        public static List<AbilityTreeNode> Build(SangoObjectSet<Ability> abilities)
        {
            List<AbilityTreeNode> all = new List<AbilityTreeNode>();
            if (abilities == null)
                return all;

            // ---- ① 建节点 ----
            Dictionary<int, AbilityTreeNode> map = new Dictionary<int, AbilityTreeNode>();
            // 【性能】原 ForEach(lambda) 捕获 all / map，改为按下标遍历底层数组。
            Ability[] abilityArray = abilities.objects;
            for (int i = 1; i < abilityArray.Length; i++)
            {
                Ability ability = abilityArray[i];
                if (ability == null)
                    continue;
                AbilityTreeNode node = new AbilityTreeNode();
                node.ability = ability;
                all.Add(node);
                map[ability.Id] = node;      // Id 重复时以最后一条为准（数据异常兜底）
            }
            if (all.Count == 0)
                return all;

            // ---- ① 连边 ----
            for (int i = 0; i < all.Count; i++)
            {
                AbilityTreeNode node = all[i];
                int[] required = node.ability.requiredAbilityIds;
                if (required == null)
                    continue;
                for (int r = 0; r < required.Length; r++)
                {
                    int parentId = required[r];
                    if (parentId <= 0 || parentId == node.ability.Id)
                        continue;
                    AbilityTreeNode parent;
                    if (!map.TryGetValue(parentId, out parent))
                        continue;
                    node.parents.Add(parent);
                    parent.children.Add(node);
                }
            }

            ComputeColumns(all);
            ComputeRows(all);
            ComputeRings(all);
            ComputeRingSlots(all);
            return all;
        }

        /// <summary>
        /// 计算 BFS 环号：从所有根同时出发做一次多源 BFS，节点取"到最近根的距离"。
        ///
        /// 多源而不是单源：能力表里有多个无前置的根（统率/武力/智力/政治/魅力各一支），
        /// 单源会把其他根排到很外圈，白白浪费一圈半径。
        /// </summary>
        static void ComputeRings(List<AbilityTreeNode> all)
        {
            for (int i = 0; i < all.Count; i++)
                all[i].ring = -1;

            Queue<AbilityTreeNode> queue = new Queue<AbilityTreeNode>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].parents.Count == 0)
                {
                    all[i].ring = 0;
                    queue.Enqueue(all[i]);
                }
            }

            // 数据异常（整张图成环、找不到根）：随便挑一个当根，保证仍能铺开
            if (queue.Count == 0 && all.Count > 0)
            {
                all[0].ring = 0;
                queue.Enqueue(all[0]);
            }

            while (queue.Count > 0)
            {
                AbilityTreeNode current = queue.Dequeue();
                int next = current.ring + 1;
                for (int i = 0; i < current.children.Count; i++)
                {
                    AbilityTreeNode child = current.children[i];
                    if (child.ring >= 0 && child.ring <= next)
                        continue;       // 已经有更短路径
                    child.ring = next;
                    queue.Enqueue(child);
                }
            }

            // 兜底：BFS 到不了的节点放到最外环
            int maxRing = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ring > maxRing)
                    maxRing = all[i].ring;
            }
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ring < 0)
                    all[i].ring = maxRing + 1;
            }
        }

        /// <summary>
        /// 环内排序与槽位：同一环的节点按"父节点在上一环的平均角度"排序，
        /// 让同一分支的子节点在环上聚成一段（否则会散布整圈，连线全交叉）。
        /// </summary>
        static void ComputeRingSlots(List<AbilityTreeNode> all)
        {
            if (all.Count == 0)
                return;

            int maxRing = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].ring > maxRing)
                    maxRing = all[i].ring;
            }

            List<List<AbilityTreeNode>> rings = new List<List<AbilityTreeNode>>(maxRing + 1);
            for (int r = 0; r <= maxRing; r++)
                rings.Add(new List<AbilityTreeNode>());
            for (int i = 0; i < all.Count; i++)
                rings[all[i].ring].Add(all[i]);

            for (int r = 0; r <= maxRing; r++)
            {
                List<AbilityTreeNode> ring = rings[r];
                ring.Sort((a, b) =>
                {
                    float aa = ParentRingAngle(a);
                    float bb = ParentRingAngle(b);
                    int cmp = aa.CompareTo(bb);
                    return cmp != 0 ? cmp : a.ability.Id.CompareTo(b.ability.Id);
                });

                for (int i = 0; i < ring.Count; i++)
                {
                    ring[i].ringSlot = i;
                    ring[i].ringSlots = ring.Count;
                }
            }
        }

        /// <summary>
        /// 父节点在环上的平均角度（归一化到 0~1，-1 表示没有可用父，排到环首）。
        /// 父节点必须已经算过 ringSlots，否则跳过。
        /// </summary>
        static float ParentRingAngle(AbilityTreeNode node)
        {
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < node.parents.Count; i++)
            {
                AbilityTreeNode parent = node.parents[i];
                if (parent == null || parent.ringSlots <= 0)
                    continue;
                sum += (parent.ringSlot + 0.5f) / parent.ringSlots;
                count++;
            }
            return count > 0 ? sum / count : -1f;
        }

        /// <summary>
        /// 列号 = 最长前置链长度。
        /// 迭代松弛而不是拓扑排序：数据里若出现环（互相前置），松弛会在 guard 次后收敛，
        /// 不会死循环；此时列号略大但布局仍然有效。
        /// </summary>
        static void ComputeColumns(List<AbilityTreeNode> all)
        {
            int guard = all.Count + 1;
            for (int pass = 0; pass < guard; pass++)
            {
                bool changed = false;
                for (int i = 0; i < all.Count; i++)
                {
                    AbilityTreeNode node = all[i];
                    for (int p = 0; p < node.parents.Count; p++)
                    {
                        int want = node.parents[p].col + 1;
                        if (want > node.col && want < guard)
                        {
                            node.col = want;
                            changed = true;
                        }
                    }
                }
                if (!changed)
                    break;
            }
        }

        /// <summary>
        /// 行号分配：多轮重心迭代（科技树"父居中于子"在多父 DAG 上的推广）。
        ///
        /// 科技树是单父树，可以递归让父节点落在子节点的正中间；
        /// 能力是多父 DAG，递归会重复计数，因此改用迭代逼近（Sugiyama 分层布局的标准做法）：
        ///   ① 初始：每列节点在该列的纵向跨度上均匀铺开（不同列处于同一 y 尺度）；
        ///   ② 正向扫描：每个节点的 y = 其父节点 y 的均值；
        ///   ③ 反向扫描：每个节点的 y = 其子节点 y 的均值；
        ///   ④ 反复 ②③ 若干轮；
        ///   ⑤ 收尾：按列排序、强制相邻行至少差 1（不重叠），再整体归一化成整数行号。
        ///
        /// 这样父节点自然落在子节点的中间，连线基本是短折线，交叉显著少于"列内紧凑堆叠"。
        /// </summary>
        static void ComputeRows(List<AbilityTreeNode> all)
        {
            int maxCol = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].col > maxCol)
                    maxCol = all[i].col;
            }

            List<List<AbilityTreeNode>> columns = new List<List<AbilityTreeNode>>(maxCol + 1);
            for (int c = 0; c <= maxCol; c++)
                columns.Add(new List<AbilityTreeNode>());
            for (int i = 0; i < all.Count; i++)
                columns[all[i].col].Add(all[i]);

            int maxCount = 1;
            for (int c = 0; c <= maxCol; c++)
            {
                if (columns[c].Count > maxCount)
                    maxCount = columns[c].Count;
            }

            // ① 初始铺开：以"最大列节点数"为总跨度，各列均匀分布，保证不同列在同一 y 尺度上
            float span = System.Math.Max(1, maxCount - 1);
            for (int c = 0; c <= maxCol; c++)
            {
                List<AbilityTreeNode> column = columns[c];
                int n = column.Count;
                for (int i = 0; i < n; i++)
                    column[i].y = n > 1 ? span * i / (n - 1f) : span * 0.5f;
            }

            // ②③ 交替扫描
            const int passes = 8;
            for (int pass = 0; pass < passes; pass++)
            {
                bool useParents = (pass % 2) == 0;
                int start = useParents ? 1 : maxCol - 1;
                int end = useParents ? maxCol : 0;
                int step = useParents ? 1 : -1;

                for (int c = start; step > 0 ? c <= end : c >= end; c += step)
                {
                    List<AbilityTreeNode> column = columns[c];
                    for (int i = 0; i < column.Count; i++)
                    {
                        AbilityTreeNode node = column[i];
                        List<AbilityTreeNode> refs = useParents ? node.parents : node.children;
                        float sum = 0f;
                        int count = 0;
                        for (int k = 0; k < refs.Count; k++)
                        {
                            AbilityTreeNode reference = refs[k];
                            if (reference == null)
                                continue;
                            sum += reference.y;
                            count++;
                        }
                        if (count > 0)
                            node.y = sum / count;
                    }
                }
            }

            // ⑤ 列内去重 + 归一化
            float minY = float.MaxValue;
            for (int c = 0; c <= maxCol; c++)
            {
                List<AbilityTreeNode> column = columns[c];
                column.Sort((a, b) =>
                {
                    int cmp = a.y.CompareTo(b.y);
                    return cmp != 0 ? cmp : a.ability.Id.CompareTo(b.ability.Id);
                });

                float prev = float.MinValue;
                for (int i = 0; i < column.Count; i++)
                {
                    AbilityTreeNode node = column[i];
                    if (i > 0 && node.y < prev + 1f)
                        node.y = prev + 1f;
                    prev = node.y;
                    if (node.y < minY)
                        minY = node.y;
                }
            }

            if (minY == float.MaxValue)
                minY = 0f;

            for (int i = 0; i < all.Count; i++)
            {
                float value = all[i].y - minY;
                all[i].row = value <= 0f ? 0 : (int)(value + 0.5f);
            }
        }


    }
}
