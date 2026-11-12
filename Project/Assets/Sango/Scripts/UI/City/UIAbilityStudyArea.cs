using Sango.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.UI
{
    /// <summary>
    /// 能力路径图区域：**直接按能力数据里的六边形坐标摆放节点**，不再做任何自动布局与连线。
    ///
    /// <para>数据驱动，三段分工（改版后最重要的三条）：</para>
    /// <list type="number">
    /// <item>位置：读 <see cref="Ability.q"/> / <see cref="Ability.r"/>（轴向坐标，
    ///       中心格 = (0,0)，三轴满足 q + r + s = 0），换算成像素坐标；</item>
    /// <item>依赖：不再画线。前置关系由节点模板自带的六个方向箭头
    ///       <c>node_template/dir/1~6</c> 表示，箭头方向是该能力**向下解锁**的相邻格方位
    ///       （即箭头指向子节点、不指向前置；前置在哪一侧由父节点自己画箭头指出本节点）；</item>
    /// <item>方向：显示哪几个箭头完全看 <see cref="Ability.dir"/> 这个方向编号数组
    ///       （如 <c>[1, 6]</c> = 右上与左上各一个箭头），UI 只按数组显示，不做任何推导。</item>
    /// </list>
    ///
    /// <para>
    /// 六边形几何采用"尖顶六边形"（pointy-top）行排布：同一排的节点横向差一个格宽，
    /// 相邻两排横向错开半个格宽。因此像素坐标为
    /// <c>x = (q + r * 0.5) * 格宽</c>、<c>y = r * 格高</c>，
    /// 格宽/格高由 <see cref="nodeSize"/> + <see cref="spacing"/> 决定。
    /// </para>
    ///
    /// <para>
    /// 之所以放弃旧的"依赖深度分列 + 蜂窝错位 + 正交连线"算法：能力图是多父 DAG，
    /// 自动布局既要防交叉又要防重叠，节点一动整张图就重排；改成数据坐标后，
    /// 版面就是一张固定的六边形地图，加一条能力只需在 Json 里写 q/r/dir 三个数。
    /// </para>
    ///
    /// <para>
    /// 【隐藏能力可见性】对齐原版：隐藏能力（<see cref="Ability.hiddenSlot"/> ≥ 0）只以"槽内容"的身份出现，
    /// **未进槽的那一格不生成节点**（原版 `get_ability_list` 跳过所有隐藏能力，
    /// `get_researchable_ability_list` 只把槽里的那条加回来）。因此界面上能看到的隐藏位数量
    /// 等于当前势力已激活的槽数，而不是固定的 10 个。
    /// </para>
    ///
    /// <para>
    /// 【隐藏能力出现演出】势力新激活的隐藏槽（<see cref="Force.IsHiddenAbilityNew"/>）在**下一次重建图时**
    /// 播一次"从格子里冒出来"的动画（<see cref="UIAbilityStudyNode.PlayAppearAnimation"/>），播完立刻清位，
    /// 保证同一次激活只演一次。标记是存档字段：激活发生时玩家没开界面也不会丢，
    /// 下次打开界面（研究 / 培育任一张能力图）才播。
    /// </para>
    ///
    /// 【美术接入清单】在滚动视图的 Content 上挂本脚本，并按字段名拖控件：
    /// <code>
    /// 字段名              控件类型       用途
    /// ──────────────────────────────────────────────────
    /// content             RectTransform  节点容器（留空则用本物体）；anchor/pivot 建议左上 (0,1)
    /// nodePrefab          GameObject     节点模板（挂 UIAbilityStudyNode，内含 dir/1~6）
    /// nodeSize            Vector2        节点尺寸（会写入每个节点的 sizeDelta）
    /// spacing             Vector2        节点间距（格宽 = nodeSize.x + spacing.x，格高同理）
    /// padding             Vector2        内容左上留白，建议取节点宽高的一半
    /// </code>
    /// </summary>
    public class UIAbilityStudyArea : MonoBehaviour
    {
        /// <summary>节点容器（留空则用本物体）</summary>
        public RectTransform content;

        /// <summary>节点模板（挂 <see cref="UIAbilityStudyNode"/>，内部需有 dir/1~6 六个方向箭头）</summary>
        public GameObject nodePrefab;

        /// <summary>
        /// 中心「研究开始」节点（prefab 里已放在 Content 下的 <c>center</c>）。
        /// 位置固定为六边形原点 (0,0)，六个方向箭头按"入口能力"的方位点亮。
        /// 它不参与能力节点的生成与销毁，<see cref="Show"/> 只负责摆位与下发方向。
        /// </summary>
        public UIAbilityStudyNode centerNode;

        /// <summary>节点尺寸</summary>
        public Vector2 nodeSize = new Vector2(70f, 50f);

        /// <summary>节点间距：格宽 = 节点宽 + spacing.x，格高 = 节点高 + spacing.y</summary>
        public Vector2 spacing = new Vector2(32f, 18f);

        /// <summary>
        /// 内容左上留白。
        /// 节点坐标以"中心"为准且最外圈从 0 起算，若不留白，
        /// 最外圈的半个节点会被 Viewport 的 RectMask2D 裁掉。
        /// </summary>
        public Vector2 padding = new Vector2(35f, 25f);

        /// <summary>点击某个能力节点（窗口订阅后转发给指令）</summary>
        public event Action<Ability> onSelect;

        /// <summary>多个隐藏槽同时出现时的错峰间隔（秒）</summary>
        const float AppearStaggerSeconds = 0.12f;

        /// <summary>已生成的节点（按能力表顺序；隐藏能力按槽位只生成一条）</summary>
        readonly List<UIAbilityStudyNode> nodes = new List<UIAbilityStudyNode>();

        /// <summary>待播"出现"动画所对应的势力（槽位标记记在它身上；null 表示没有待播）</summary>
        Force appearForce;

        /// <summary>是否已经排好了下一帧的播放入口（防止重复启动协程）</summary>
        bool appearScheduled;

        /// <summary>
        /// 图的版本号：每次重建递增。
        /// 协程醒来时版本不符就直接退出，避免"旧图排的播放"落到新图的节点上。
        /// </summary>
        int appearVersion;

        /// <summary>能力 Id → 节点</summary>
        readonly Dictionary<int, UIAbilityStudyNode> nodeMap = new Dictionary<int, UIAbilityStudyNode>();

        /// <summary>已生成的节点数</summary>
        public int NodeCount => nodes.Count;

        /// <summary>
        /// 重建整张图：读能力数据的六边形坐标摆放节点，并按下发方向位显示箭头。
        ///
        /// 传参而不是内部读 <see cref="Scenario.Cur"/>：便于在编辑器里用假数据调样式，
        /// 也让"读哪张能力表"由调用方一处决定。
        /// </summary>
        /// <param name="abilities">能力表（<c>Scenario.Cur.CommonData.Abilities</c>，即 Abilities.json）</param>
        /// <param name="force">当前势力（判"已研究"）</param>
        /// <param name="person">当前武将（判"该武将已学"；研究界面传 null）</param>
        /// <param name="learnable">当前可选的能力（培育 / 研究候选）</param>
        public void Show(SangoObjectSet<Ability> abilities, Force force, Person person, List<Ability> learnable)
        {
            Clear();
            if (nodePrefab == null || abilities == null)
                return;

            // 依赖关系仍走树构建器：位置不用它，但隐藏槽过滤与中心节点入口判定要用父子关系
            List<AbilityTreeNode> layout = AbilityTreeBuilder.Build(abilities);
            if (layout.Count == 0)
                return;

            // 隐藏能力：一个槽位只画一条
            List<AbilityTreeNode> display = FilterHiddenSlots(layout, force);
            if (display.Count == 0)
                return;

            HashSet<int> learnableIds = new HashSet<int>();
            if (learnable != null)
            {
                for (int i = 0; i < learnable.Count; i++)
                {
                    if (learnable[i] != null)
                        learnableIds.Add(learnable[i].Id);
                }
            }

            RectTransform root = content != null ? content : transform as RectTransform;
            if (root == null)
                return;

            // prefab 里的模板本体要藏起来，否则它会作为"多出来的一个节点"留在画面上
            // （模板是 content 的子物体时才有这个问题；直接引用独立 prefab 资源时不受影响）
            if (nodePrefab.activeSelf && nodePrefab.transform.IsChildOf(root.transform))
                nodePrefab.SetActive(false);

            float stepX = Mathf.Max(1f, nodeSize.x + spacing.x);
            float stepY = Mathf.Max(1f, nodeSize.y + spacing.y);

            // ---- ① 六边形坐标 → 以中心格为原点的像素偏移，同时统计包围盒 ----
            List<Vector2> offsets = new List<Vector2>(display.Count);
            Dictionary<long, Ability> occupied = new Dictionary<long, Ability>();
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            for (int i = 0; i < display.Count; i++)
            {
                Ability ability = display[i].ability;
                if (ability == null)
                {
                    offsets.Add(Vector2.zero);
                    continue;
                }

                // 尖顶六边形行排布：横坐标含 r 的半个格宽错位，纵坐标只由 r 决定
                Vector2 offset = new Vector2(
                    stepX * (ability.q + ability.r * 0.5f),
                    stepY * ability.r);
                offsets.Add(offset);

                if (offset.x < minX) minX = offset.x;
                if (offset.x > maxX) maxX = offset.x;
                if (offset.y < minY) minY = offset.y;
                if (offset.y > maxY) maxY = offset.y;

                long key = Key(ability.q, ability.r);
                Ability exist;
                if (occupied.TryGetValue(key, out exist))
                {
                    // 同一隐藏槽的候选能力本来就共用一格（界面上只显示槽里那一条），不算冲突
                    bool sameHiddenSlot = ability.hiddenSlot >= 0 && ability.hiddenSlot == exist.hiddenSlot;
                    if (!sameHiddenSlot)
                    {
                        Log.Warning($"能力「{ability.Name}」(Id={ability.Id}) 的六边形坐标 q={ability.q}, r={ability.r}"
                            + $" 与能力「{exist.Name}」(Id={exist.Id}) 重复，两个节点会叠在同一格上", Log.LogType.UI);
                    }
                }
                else
                {
                    occupied.Add(key, ability);
                }
            }

            if (float.IsInfinity(minX))
            {
                minX = maxX = minY = maxY = 0f;
            }

            // 中心「研究开始」站在原点 (0,0)：把原点并入包围盒，
            // 否则它会被算到 padding 之外（负坐标），被 Viewport 的 RectMask2D 裁掉
            if (centerNode != null)
            {
                if (0f < minX) minX = 0f;
                if (0f > maxX) maxX = 0f;
                if (0f < minY) minY = 0f;
                if (0f > maxY) maxY = 0f;
            }

            // ---- ② 节点：按数据坐标摆放，方向箭头按数组显示 ----
            for (int i = 0; i < display.Count; i++)
            {
                Vector2 position = new Vector2(
                    padding.x + nodeSize.x * 0.5f + (offsets[i].x - minX),
                    -(padding.y + nodeSize.y * 0.5f + (offsets[i].y - minY)));
                CreateNode(root, display[i], position, force, person, learnableIds);
            }

            // ---- ②' 方向箭头只保留"对面那格确实有节点"的方向 ----
            // 隐藏槽未激活时那一格不生成节点，所以指向它的箭头默认不亮；
            // 槽被激活、节点出现后，箭头自然跟着亮起来，不需要额外开关。
            ApplyDirectionVisibility(occupied);

            // ---- ②'' 中心「研究开始」节点：固定在原点 (0,0)，箭头指向各入口能力 ----
            SetupCenterNode(display, minX, minY);

            // ---- ③ 内容尺寸（决定 ScrollRect 的滚动范围） ----
            root.sizeDelta = new Vector2(
                padding.x * 2f + nodeSize.x + (maxX - minX),
                padding.y * 2f + nodeSize.y + (maxY - minY));

            // ---- ④ 隐藏能力出现演出：只播"刚激活、还没播过"的那些槽 ----
            CollectAppearTargets(force);

            // ---- ⑤ 正在研究的那条能力：闪烁提示 ----
            ApplyResearchingBlink(force);
        }

        /// <summary>
        /// 摆放中心「研究开始」节点并下发它的方向箭头。
        ///
        /// <para>
        /// 位置固定为六边形原点 (0,0)：换算公式与普通节点完全一致（同一套 padding / 包围盒最小偏移），
        /// 因此它永远落在整张图的几何中心，不会随内容增减漂移。
        /// </para>
        /// <para>
        /// 箭头语义是"从这里能走向哪"：取所有**没有前置**的能力（树的入口，如各兵种 A 级、各属性 +5 低），
        /// 按它们在原点周围的方位去重后点亮 —— 所以原版中心节点并不是恒亮 6 个箭头。
        /// </para>
        /// <para>
        /// 【为什么按 display 而不是全表算】隐藏槽未激活时那一格根本不生成节点，
        /// 若拿全表算就会点亮一个"指向空位"的箭头。改成只看**当前实际显示的节点**：
        /// 隐藏能力未激活 → 不参与；激活进槽（有前置）→ 也不算入口；激活且无前置 → 才算入口。
        /// </para>
        /// </summary>
        /// <param name="display">当前实际显示的节点（<see cref="FilterHiddenSlots"/> 过滤后）</param>
        /// <param name="minX">包围盒最小横向偏移</param>
        /// <param name="minY">包围盒最小纵向偏移</param>
        void SetupCenterNode(List<AbilityTreeNode> display, float minX, float minY)
        {
            if (centerNode == null)
                return;

            RectTransform rt = centerNode.rect != null ? centerNode.rect : centerNode.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(
                    padding.x + nodeSize.x * 0.5f + (0f - minX),
                    -(padding.y + nodeSize.y * 0.5f + (0f - minY)));
            }

            centerNode.PrepareAsCenter(this);

            HashSet<int> directions = new HashSet<int>();
            for (int i = 0; i < display.Count; i++)
            {
                AbilityTreeNode tree = display[i];
                Ability ability = tree != null ? tree.ability : null;
                if (ability == null)
                    continue;
                if (HasRequiredAbility(ability))
                    continue;

                int direction = AbilityHex.GetDirection(0, 0, ability.q, ability.r);
                if (direction > 0)
                    directions.Add(direction);
            }

            int[] order = new int[directions.Count];
            directions.CopyTo(order);
            Array.Sort(order);
            centerNode.SetDirections(order);
        }

        /// <summary>
        /// 方向箭头的可见性收口：只有"该方向紧邻的那一格确实有节点"时才保留这个箭头。
        ///
        /// <para>
        /// 【为什么需要】<see cref="Ability.dir"/> 是策划数据，写的是"这条能力向下解锁的方向"，
        /// 但数据不知道某条下游能力当前有没有出现在图上。最典型的就是**隐藏能力**：
        /// 槽位未激活时那一格不生成节点，若仍按数据点亮箭头，就会出现一根指向空位的箭头。
        /// </para>
        /// <para>
        /// 判定用"已显示的格子索引"（<paramref name="occupied"/> 本来就是按 <c>display</c> 建的），
        /// 因此隐藏槽激活后节点出现，那根箭头会自动亮起，无需额外标记。
        /// </para>
        /// </summary>
        /// <param name="occupied">已显示节点的格子索引（key 由 <see cref="Key"/> 生成）</param>
        void ApplyDirectionVisibility(Dictionary<long, Ability> occupied)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                UIAbilityStudyNode node = nodes[i];
                if (node == null || node.Ability == null)
                    continue;

                int[] directions = node.Ability.dir;
                if (directions == null || directions.Length == 0)
                    continue;

                List<int> visible = new List<int>(directions.Length);
                for (int d = 0; d < directions.Length; d++)
                {
                    int direction = directions[d];
                    if (direction < 1 || direction > AbilityHex.MaxDirection)
                        continue;

                    int deltaQ, deltaR;
                    AbilityHex.GetNeighborOffset(direction, out deltaQ, out deltaR);
                    long key = Key(node.Ability.q + deltaQ, node.Ability.r + deltaR);
                    if (occupied.ContainsKey(key))
                        visible.Add(direction);
                }

                if (visible.Count != directions.Length)
                    node.SetDirections(visible.ToArray());
            }
        }

        /// <summary>该能力是否有任意一个前置（有前置就不是树的入口）</summary>
        /// <param name="ability">能力</param>
        /// <returns>有前置返回 true</returns>
        static bool HasRequiredAbility(Ability ability)
        {
            if (ability.requiredAbilityIds == null)
                return false;
            for (int i = 0; i < ability.requiredAbilityIds.Length; i++)
            {
                if (ability.requiredAbilityIds[i] > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 记下本次重建后的待播势力，并排队在下一帧播放隐藏能力出现动画。
        ///
        /// <para>
        /// 不能在 <see cref="Show"/> 里当场播：窗口常见流程是"先 Show 后 SetActive"，
        /// 物体还没激活时 <c>StartCoroutine</c> 会直接报错、节点动画也跑不起来。
        /// 因此统一延后一帧，等层级激活完成后再播（见 <see cref="PlayPendingAppearRoutine"/>）。
        /// </para>
        /// </summary>
        /// <param name="force">本次图对应的势力，可为 null</param>
        void CollectAppearTargets(Force force)
        {
            appearForce = force;
            appearVersion++;
            appearScheduled = false;
            ScheduleAppear();
        }

        /// <summary>是否还有"待播出现动画"的隐藏槽</summary>
        /// <param name="force">势力</param>
        /// <returns>有则为 true</returns>
        bool HasPendingAppear(Force force)
        {
            if (force == null)
                return false;

            for (int i = 0; i < nodes.Count; i++)
            {
                UIAbilityStudyNode node = nodes[i];
                if (node == null || node.Ability == null)
                    continue;
                int slot = node.Ability.hiddenSlot;
                if (slot >= 0 && force.IsHiddenAbilityNew(slot))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 启动"下一帧播放"的入口。
        /// 物体未激活或没有待播项时直接返回；<see cref="OnEnable"/> 会再补一次，
        /// 覆盖"Show 时窗口还没激活"的情况。
        /// </summary>
        void ScheduleAppear()
        {
            if (appearScheduled || appearForce == null)
                return;
            // 编辑器非播放模式（预览节点）不跑协程：Unity 不会执行 edit mode 的 StartCoroutine
            if (!Application.isPlaying)
                return;
            if (!isActiveAndEnabled)
                return;     // 未激活：StartCoroutine 会报错，等 OnEnable
            if (!HasPendingAppear(appearForce))
                return;

            appearScheduled = true;
            StartCoroutine(PlayPendingAppearRoutine(appearVersion));
        }

        /// <summary>窗口从关闭变为打开时补一次播放入口（配合"先 Show 后激活"的调用顺序）</summary>
        void OnEnable()
        {
            ScheduleAppear();
        }

        /// <summary>
        /// 延后一帧播放全部待播的隐藏能力出现动画，并逐个清位。
        ///
        /// <para>
        /// 清位时机在"播放的这一刻"而不是"播完"：节点可能在演出途中被下一次 Show 销毁重建，
        /// 若等到播完再清，标记会残留，下次打开界面又冒一次。
        /// </para>
        /// <para>
        /// 若某个节点所在层级仍未激活（父级还是关闭状态），跳过它且**不清位**，
        /// 保证标记留到下次真正能看见的那一次。
        /// </para>
        /// </summary>
        /// <param name="version">发起时的图版本号</param>
        System.Collections.IEnumerator PlayPendingAppearRoutine(int version)
        {
            // 等一帧：让窗口的 SetActive / 布局在这一帧内完成
            yield return null;

            if (version != appearVersion)
                yield break;    // 期间又重建过图，交给新的协程

            appearScheduled = false;

            Force force = appearForce;
            if (force == null)
                yield break;

            int order = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                UIAbilityStudyNode node = nodes[i];
                if (node == null || node.Ability == null)
                    continue;

                int slot = node.Ability.hiddenSlot;
                if (slot < 0 || !force.IsHiddenAbilityNew(slot))
                    continue;
                if (!node.gameObject.activeInHierarchy)
                    continue;

                force.ClearHiddenAbilityNew(slot);  // 同一次激活只演一次
                node.PlayAppearAnimation(order * AppearStaggerSeconds);
                order++;
                Log.Info($"*[能力研究]{force.Name} 隐藏能力「{node.Ability.Name}」出现在图上（槽位 {slot}）",
                    Log.LogType.UI);
            }
        }

        /// <summary>
        /// 过滤隐藏能力：一个隐藏槽位只保留一条，避免同槽多条候选叠在同一格。
        ///
        /// <para>对齐原版机制：<c>Force.hiddenAbilityIds[10]</c>（Open311 s11_scen.h:905）**一个槽只装一条**，
        /// 未进槽的隐藏能力不可研究（<c>is_ability_researchable</c>，s11_sys_scen_ability.cpp:12-13）。</para>
        ///
        /// <para>关键对齐点（严格照原版，不做占位）：原版 `get_ability_list`
        /// （s11_sys_scen_ability.cpp:69-83）**无条件跳过所有 `hidden_slot >= 0` 的能力**，
        /// `get_researchable_ability_list`（同文件 :102-125）再把"每个槽当前装的那一条"加回来 ——
        /// 也就是说隐藏能力只以"槽内容"的身份进入界面，**槽里没有它，界面上就没有这一格**。
        /// 所以本项目同样只在"槽里装的是它"时才生成节点，槽空则该位置空着。</para>
        ///
        /// <para>判定顺序：</para>
        /// <list type="bullet">
        /// <item>普通能力（<c>hiddenSlot &lt; 0</c>）—— 全部保留；</item>
        /// <item>有势力 —— 只保留"该槽当前装的那一条"（未进槽 / 槽空一律不生成节点）；</item>
        /// <item>无势力（编辑器预览）—— 每槽保留遍历到的第一条，保证 10 个槽位在版面上都看得到。</item>
        /// </list>
        /// </summary>
        /// <param name="layout">完整依赖树（按 Id 升序）</param>
        /// <param name="force">当前势力，可为 null</param>
        /// <returns>要显示的依赖树节点</returns>
        static List<AbilityTreeNode> FilterHiddenSlots(List<AbilityTreeNode> layout, Force force)
        {
            List<AbilityTreeNode> result = new List<AbilityTreeNode>(layout.Count);
            HashSet<int> shownSlots = new HashSet<int>();

            for (int i = 0; i < layout.Count; i++)
            {
                AbilityTreeNode node = layout[i];
                if (node == null || node.ability == null)
                    continue;

                Ability ability = node.ability;
                if (ability.hiddenSlot < 0)
                {
                    result.Add(node);
                    continue;
                }

                if (force != null)
                {
                    // 槽里装的就是它 → 显示（这是该隐藏位上唯一会出现的节点）；
                    // 槽空或装了别的 → 这一格不生成节点，与原版列表口径一致
                    if (force.GetHiddenAbilityId(ability.hiddenSlot) == ability.Id)
                        result.Add(node);
                    continue;
                }

                if (shownSlots.Add(ability.hiddenSlot))
                    result.Add(node);
            }
            return result;
        }

        /// <summary>
        /// 生成一个节点。
        /// </summary>
        /// <param name="root">节点容器</param>
        /// <param name="tree">依赖树节点（取 Ability 与父子关系）</param>
        /// <param name="position">节点中心相对容器的 anchoredPosition</param>
        /// <param name="force">当前势力</param>
        /// <param name="person">当前武将</param>
        /// <param name="learnableIds">当前可选的 Ability Id 集合</param>
        /// <returns>生成的节点；模板缺组件时返回 null</returns>
        UIAbilityStudyNode CreateNode(
            RectTransform root,
            AbilityTreeNode tree,
            Vector2 position,
            Force force,
            Person person,
            HashSet<int> learnableIds)
        {
            GameObject go = Instantiate(nodePrefab, root);
            go.SetActive(true);

            UIAbilityStudyNode item = go.GetComponent<UIAbilityStudyNode>();
            if (item == null)
            {
                DestroyNode(go);
                return null;
            }

            Ability ability = tree.ability;
            if (ability == null)
            {
                // 数据异常兜底：依赖树里出现空能力时不生成节点，避免后面取 Id 空引用
                DestroyNode(go);
                return null;
            }

            item.Setup(this, ability);

            RectTransform rt = item.rect != null ? item.rect : go.transform as RectTransform;
            if (rt != null)
            {
                rt.sizeDelta = nodeSize;
                rt.anchoredPosition = position;
            }

            bool researched = force != null && force.HasAbility(ability.Id);
            bool learned = person != null && person.HasLearnedAbility(ability.Id);
            bool canLearn = learnableIds.Contains(ability.Id);
            item.ApplyState(researched, learned, canLearn);
            item.SetDirections(ability.dir);

            nodes.Add(item);
            nodeMap[ability.Id] = item;
            return item;
        }

        /// <summary>
        /// 刷新选中态（不重建节点，避免滚动位置跳动）。
        ///
        /// 只切换节点自身的选中框：**不再点亮方向箭头**。
        /// 前置关系已经由节点边缘的箭头静态表达（<see cref="UIAbilityStudyNode.SetDirections"/>），
        /// 点击时再整链染色会让整张图在同一时刻出现两种箭头颜色，反而看不清"当前点了哪一条"。
        /// </summary>
        /// <param name="ability">当前选中的能力</param>
        public void SetSelected(Ability ability)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                UIAbilityStudyNode node = nodes[i];
                if (node != null)
                    node.SetSelected(node.Ability == ability);
            }
        }

        /// <summary>
        /// 给"正在研究"的那条能力加闪烁提示（<c>type_1</c> 层做 alpha 呼吸）。
        ///
        /// <para>
        /// 数据来源是 <see cref="Force.researchingAbilityId"/>（Open311
        /// <c>get_researching_ability_id()</c>）：-1 表示空闲，此时所有节点都停止闪烁。
        /// 同一势力同时只能研究一条能力，因此最多只有一个节点在闪。
        /// </para>
        /// </summary>
        /// <param name="force">当前势力</param>
        void ApplyResearchingBlink(Force force)
        {
            int researchingId = force != null ? force.researchingAbilityId : -1;
            for (int i = 0; i < nodes.Count; i++)
            {
                UIAbilityStudyNode node = nodes[i];
                if (node == null || node.Ability == null)
                    continue;
                node.SetResearching(researchingId >= 0 && node.Ability.Id == researchingId);
            }
        }

        /// <summary>
        /// 按能力 Id 取已生成的节点（外部定位 / 二次高亮用）。
        /// </summary>
        /// <param name="abilityId">能力 Id</param>
        /// <returns>节点；未生成返回 null</returns>
        public UIAbilityStudyNode GetNode(int abilityId)
        {
            UIAbilityStudyNode node;
            return nodeMap.TryGetValue(abilityId, out node) ? node : null;
        }

        /// <summary>清空全部节点</summary>
        public void Clear()
        {
            // 节点都没了，排着的出现动画也一起作废（版本号递增让旧协程醒来即退出）
            appearForce = null;
            appearScheduled = false;
            appearVersion++;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    DestroyNode(nodes[i].gameObject);
            }
            nodes.Clear();
            nodeMap.Clear();
        }

        /// <summary>节点点击透传（由 <see cref="UIAbilityStudyNode"/> 调用）</summary>
        /// <param name="ability">被点击的能力</param>
        public void NotifySelect(Ability ability)
        {
            onSelect?.Invoke(ability);
        }

        /// <summary>
        /// 销毁节点。
        ///
        /// 编辑器非播放模式下 <c>Object.Destroy</c> 不生效且每帧刷
        /// "Destroy may not be called from edit mode"，因此编辑态改用 DestroyImmediate，
        /// 方便在编辑器里直接实例化 prefab 调 <see cref="Show"/> 预览版面。
        /// </summary>
        /// <param name="go">待销毁的节点</param>
        static void DestroyNode(GameObject go)
        {
            if (go == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(go);
                return;
            }
#endif
            Destroy(go);
        }

        /// <summary>
        /// 轴向坐标 (q, r) → 字典键。
        /// 用 long 拼而不是 ValueTuple，避免旧版 IL2CPP 下的装箱与相等性陷阱。
        /// </summary>
        static long Key(int q, int r)
        {
            return (long)q * 100000L + r;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 【编辑器预览】能力表 Json 的路径，相对 <c>Application.dataPath</c>。
        /// 默认 <c>&lt;项目根&gt;/Build/Content/Data/Common/Abilities.json</c>。
        /// </summary>
        public string editorPreviewDataPath = "../../Build/Content/Data/Common/Abilities.json";

        /// <summary>
        /// 【编辑器预览】不进游戏、不进播放模式，直接把 Abilities.json 里的能力全部铺成节点，
        /// 用来调 <see cref="spacing"/> / <see cref="nodeSize"/> / <see cref="padding"/> 是否合适。
        ///
        /// <para>
        /// 用法：选中挂了本脚本的 content 物体（场景或 Prefab 预览都行）→
        /// Inspector 里组件标题右上角的「⋮」菜单 → 「生成全部节点（编辑器预览）」。
        /// 调完间距再点一次即按新间距重建；不满意点「清除全部节点（编辑器预览）」还原。
        /// </para>
        /// <para>
        /// 注意：预览确实会改动当前实例（隐藏模板、生成节点），调完务必清除，
        /// **不要把预览出来的节点保存进 prefab**。
        /// </para>
        /// </summary>
        [UnityEngine.ContextMenu("生成全部节点（编辑器预览）")]
        public void EditorPreviewAllNodes()
        {
            SangoObjectSet<Ability> abilities = LoadAbilitiesForPreview();
            if (abilities == null)
                return;

            // force 传 null：隐藏能力按"每槽一条"显示，10 个槽位在版面上都会占到位
            Show(abilities, null, null, null);

            float width = content != null ? content.sizeDelta.x : 0f;
            float height = content != null ? content.sizeDelta.y : 0f;
            Log.Info($"[能力图预览] 已生成 {NodeCount} 个节点；content = {width} x {height}；"
                + $"格宽 {nodeSize.x + spacing.x}、格高 {nodeSize.y + spacing.y}", Log.LogType.UI);
        }

        /// <summary>【编辑器预览】清除预览生成的节点</summary>
        [UnityEngine.ContextMenu("清除全部节点（编辑器预览）")]
        public void EditorPreviewClearNodes()
        {
            Clear();
            Log.Info("[能力图预览] 已清除全部节点", Log.LogType.UI);
        }

        /// <summary>
        /// 【编辑器预览】从磁盘读能力表。
        /// 走的是项目自己的 <see cref="SangoObjectSetConverter{T}"/>，
        /// 保证预览所见与运行时读档完全一致（同样的 Json 结构、同样的默认值规则）。
        /// </summary>
        /// <returns>能力表；失败返回 null</returns>
        SangoObjectSet<Ability> LoadAbilitiesForPreview()
        {
            string fullPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, editorPreviewDataPath));
            if (!System.IO.File.Exists(fullPath))
            {
                Log.Error($"[能力图预览] 找不到能力表：{fullPath}", Log.LogType.UI);
                return null;
            }

            try
            {
                Newtonsoft.Json.Linq.JObject root = Newtonsoft.Json.Linq.JObject.Parse(
                    System.IO.File.ReadAllText(fullPath, System.Text.Encoding.UTF8));
                Newtonsoft.Json.Linq.JToken node = root["Abilities"];
                if (node == null)
                {
                    Log.Error("[能力图预览] Abilities.json 里没有 Abilities 段", Log.LogType.UI);
                    return null;
                }

                SangoObjectSet<Ability> result = new SangoObjectSet<Ability>();
                SangoObjectSetConverter<Ability> converter = new SangoObjectSetConverter<Ability>();
                using (Newtonsoft.Json.JsonTextReader reader =
                    new Newtonsoft.Json.JsonTextReader(new System.IO.StringReader(node.ToString())))
                {
                    reader.Read();
                    converter.ReadJson(reader, typeof(SangoObjectSet<Ability>), result,
                        Newtonsoft.Json.JsonSerializer.Create());
                }
                return result;
            }
            catch (System.Exception e)
            {
                Log.Error($"[能力图预览] 读取能力表失败：{e.Message}", Log.LogType.UI);
                return null;
            }
        }
#endif
    }
}
