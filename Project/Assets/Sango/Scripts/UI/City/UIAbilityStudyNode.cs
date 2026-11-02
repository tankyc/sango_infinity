using Sango.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    /// <summary>
    /// 能力路径图上的一个节点（<see cref="UIAbilityStudyArea"/> 的节点模板）。
    ///
    /// 视觉参照科技界面的 <c>UITechniqueItem</c>：一个色块 + 名称 + 若干状态覆盖层。
    /// 能力表没有图标资源，节点底色由 <see cref="AbilityTreeBuilder.GetNodeColor"/> 按
    /// "大类 + 维度"推导，因此**不需要美术出图**，色块 + 文字即可。
    ///
    /// <para>
    /// 【方向箭头】节点边缘的六个箭头（<c>dir/1~6</c>）用于代替连线表示前置关系：
    /// 数据里 <see cref="Ability.dir"/> 是方向编号数组（如 <c>[1, 6]</c>），
    /// 数组里出现几号就点亮几号子物体。六个子物体名必须是纯数字 "1"~"6"（本脚本按名字查找，不按顺序）。
    /// </para>
    ///
    /// 【美术接入清单】在节点 prefab 上挂本脚本，并按字段名拖控件（除 button 外都可为空）：
    /// <code>
    /// 字段名          控件类型     用途
    /// ─────────────────────────────────────────────────
    /// rect            RectTransform 节点本体（布局时改 anchoredPosition）
    /// bg              Image        底色块（自动填大类色）
    /// type            Image        种类底图（椭圆，按 statKind 取图，所有节点都显示）
    /// typeBlink       Image        种类底图的闪烁层（prefab 里名 <c>type_1</c>，sprite 与 type 相同）
    /// nameLabel       Text         能力名（如「统+5中」）
    /// categoryLabel   Text         分类短名（基础/适性/特技），可为空
    /// dirRoot         Transform    方向箭头容器，其下子物体名 1~6 对应六个方向；留空则自动找名为 dir 的子物体
    /// lockMask        Image        未研究遮罩（半透明黑），可为空
    /// researchedMark  Image        已研究标记（如打勾底纹），可为空
    /// learnableMark   Image        当前武将可学高亮框，可为空
    /// learnedMark     Image        当前武将已学标记，可为空
    /// selectedMark    Image        选中框，可为空
    /// button          Button       点击（可整块透明按钮）
    /// </code>
    /// </summary>
    public class UIAbilityStudyNode : MonoBehaviour
    {
        /// <summary>节点本体</summary>
        public RectTransform rect;

        /// <summary>底色块</summary>
        public Image bg;

        /// <summary>
        /// 种类底图（椭圆）。**所有节点都显示**，按能力的显示类型取图
        /// （见 <see cref="AbilityTypeSprite.GetKind"/>）；它只表达种类，不表达"是否隐藏 / 是否可研究"。
        /// </summary>
        public Image type;

        /// <summary>
        /// 种类底图的闪烁层（prefab 里的 <c>type_1</c>，sprite 与 <see cref="type"/> 完全一致）。
        ///
        /// <para>
        /// 只服务于「正在研究」的 alpha 呼吸：平时 alpha 恒为 0（prefab 里已是 0），
        /// 只有 <see cref="SetResearching"/> 为 true 时才由协程在 0 与
        /// <see cref="BlinkMaxAlpha"/> 之间来回渐变。
        /// 用独立一层而不是直接改 <see cref="type"/>，是为了不打断 type 自身的静态显示。
        /// </para>
        /// </summary>
        public Image typeBlink;

        /// <summary>
        /// 种类底图，下标 = <see cref="AbilityStatKind"/>（0 武力 / 1 统率 / 2 政治 / 3 智力）。
        /// Inspector 里配了优先用，没配由 <see cref="AbilityTypeSprite"/> 兜底加载
        /// （与研究界面、培育界面共用同一套图与缓存）。
        /// </summary>
        public UnityEngine.Sprite[] typeSprites = new UnityEngine.Sprite[AbilityTypeSprite.Count];

        /// <summary>能力名</summary>
        public Text nameLabel;

        /// <summary>分类短名</summary>
        public Text categoryLabel;

        /// <summary>方向箭头容器（子物体名 1~6 对应六个方向）</summary>
        public Transform dirRoot;

        /// <summary>未研究遮罩</summary>
        public Image lockMask;

        /// <summary>已研究标记</summary>
        public Image researchedMark;

        /// <summary>当前武将可学高亮</summary>
        public Image learnableMark;

        /// <summary>当前武将已学标记</summary>
        public Image learnedMark;

        /// <summary>选中框</summary>
        public Image selectedMark;

        /// <summary>点击按钮</summary>
        public Button button;

        /// <summary>本节点代表的能力</summary>
        public Ability Ability { get; private set; }

        /// <summary>方向箭头图片（下标 0 = 方向 1，未找到为 null）</summary>
        readonly Image[] dirArrows = new Image[AbilityHex.MaxDirection];

        /// <summary>闪烁协程（null = 当前没在闪）</summary>
        Coroutine blinkRoutine;

        /// <summary>
        /// 期望的闪烁状态。<see cref="SetResearching"/> 可能早于层级激活被调用
        /// （窗口常见流程是"先 Show 后 SetActive"），此时不能 StartCoroutine，
        /// 先记下来，等 <see cref="OnEnable"/> 再补启动。
        /// </summary>
        bool wantResearching;

        /// <summary>闪烁一次完整呼吸的时长（秒）</summary>
        const float BlinkPeriod = 1.2f;

        /// <summary>闪烁时的最高不透明度（0~1）</summary>
        const float BlinkMaxAlpha = 0.85f;

        /// <summary>所属区域</summary>
        UIAbilityStudyArea owner;

        /// <summary>
        /// 绑定数据（每个实例只调一次）。
        /// </summary>
        /// <param name="area">所属区域</param>
        /// <param name="ability">能力</param>
        public void Setup(UIAbilityStudyArea area, Ability ability)
        {
            owner = area;
            Ability = ability;

            if (rect == null)
                rect = GetComponent<RectTransform>();

            if (nameLabel != null)
                nameLabel.text = ability != null ? ability.Name : string.Empty;
            if (categoryLabel != null)
                categoryLabel.text = AbilityTreeBuilder.GetCategoryName(ability);
            // 不再给 bg 染色：种类完全由 type 底图表达（美术要求），bg 保持 prefab 原图原色
            ResolveBlinkLayer();
            ApplyTypeSprite();

            CollectDirArrows();
            SetDirections(ability != null ? ability.dir : null);

            // 新节点默认不闪；"正在研究"的那条由区域在 Show 末尾统一开启
            SetResearching(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        /// <summary>
        /// 以"中心开始节点"的身份初始化：不绑能力数据，只接管点击回调并收集六个方向箭头。
        ///
        /// <para>
        /// 中心节点（prefab 里的 <c>content/center</c>）不是能力节点，没有 <see cref="Ability"/>，
        /// 因此不能走 <see cref="Setup"/>（那会把名字改成能力名、并按 dir 数组重设箭头）。
        /// 它固定待在六边形原点，箭头由区域按"入口能力"的方位算好后调 <see cref="SetDirections"/> 下发。
        /// </para>
        /// </summary>
        /// <param name="area">所属区域</param>
        public void PrepareAsCenter(UIAbilityStudyArea area)
        {
            owner = area;
            if (rect == null)
                rect = GetComponent<RectTransform>();

            ResolveBlinkLayer();
            CollectDirArrows();

            // 中心节点不参与能力状态，永远不闪
            SetResearching(false);

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        /// <summary>
        /// 收集六个方向箭头并记住原色。
        ///
        /// 先在 <see cref="dirRoot"/> 下按名字找，未指派时再递归找名为 <c>dir</c> 的子物体 ——
        /// 这样 prefab 里忘拖字段也不会整块箭头消失。
        /// </summary>
        void CollectDirArrows()
        {
            for (int i = 0; i < dirArrows.Length; i++)
                dirArrows[i] = null;

            if (dirRoot == null)
            {
                Transform[] all = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i] != transform && all[i].name == "dir")
                    {
                        dirRoot = all[i];
                        break;
                    }
                }
            }
            if (dirRoot == null)
                return;

            for (int i = 0; i < dirArrows.Length; i++)
            {
                Transform child = dirRoot.Find((i + 1).ToString());
                if (child == null)
                    continue;

                dirArrows[i] = child.GetComponent<Image>();
            }
        }

        /// <summary>
        /// 取闪烁层：Inspector 拖了就用，没拖按固定名字 <c>type_1</c> 兜底查找
        /// （与 <c>dir</c> 的兜底口径一致，防止 prefab 忘绑导致"正在研究却不闪"）。
        /// </summary>
        void ResolveBlinkLayer()
        {
            if (typeBlink != null)
                return;

            Transform child = transform.Find("type_1");
            if (child != null)
                typeBlink = child.GetComponent<Image>();
        }

        /// <summary>
        /// 设置种类底图：按 <see cref="Ability.displayKind"/> 取 <c>4846-34_6~9</c> 之一，**所有节点都显示**。
        ///
        /// <para>
        /// 颜色只表达"种类"，不表达"是否隐藏 / 是否可研究"：后者由 lockMask / learnableMark 等子物体负责。
        /// </para>
        /// </summary>
        void ApplyTypeSprite()
        {
            if (type == null)
                return;

            // type 永远显示：它承载的是"种类"，不是"是否隐藏"的标记
            type.enabled = true;

            UnityEngine.Sprite sprite = GetTypeSprite(GetAbilityKind(Ability));
            if (sprite != null)
            {
                if (type.sprite != sprite)
                    type.sprite = sprite;
                // 闪烁层必须与种类底图同图，否则呼吸时露出的是另一张图（美术要求两者一致）
                if (typeBlink != null && typeBlink.sprite != sprite)
                    typeBlink.sprite = sprite;
            }
        }

        /// <summary>
        /// 取能力的显示种类（决定 type 底图）。
        ///
        /// <para>
        /// 首选数据字段 <see cref="Ability.displayKind"/>（1~98 已按原版节点底色逐条录入，
        /// 0 武力红 / 1 统率紫 / 2 政治绿 / 3 智力黄）。
        /// 未配置（-1）时兜底：隐藏槽按槽位种类，其余按 <see cref="Ability.statKind"/>（与 displayKind 同一套编码）。
        /// </para>
        /// </summary>
        /// <param name="ability">能力</param>
        /// <returns>种类下标；无法判定返回 -1（此时保留 prefab 默认底图，但不禁用）</returns>
        static int GetAbilityKind(Ability ability)
        {
            // 取图口径统一在 AbilityTypeSprite 里，培育界面节点用的是同一套
            return AbilityTypeSprite.GetKind(ability);
        }

        /// <summary>
        /// 取某种类的底图：Inspector / prefab 配了优先用，没配按文件名兜底加载
        /// （与 <c>CardDebateView.TopicLabelSprite</c> 同一套口径，取不到返回 null）。
        /// </summary>
        /// <param name="kind">种类（<see cref="AbilityStatKind"/>）</param>
        /// <returns>底图；种类越界或加载失败返回 null</returns>
        UnityEngine.Sprite GetTypeSprite(int kind)
        {
            return AbilityTypeSprite.Get(kind, typeSprites);
        }

        /// <summary>
        /// 按方向数组显示方向箭头：数组里出现哪个编号就点亮哪个子物体。
        /// </summary>
        /// <param name="directions">方向编号数组（<see cref="Ability.dir"/>，1~6，可为 null / 空）</param>
        public void SetDirections(int[] directions)
        {
            for (int i = 0; i < dirArrows.Length; i++)
            {
                Image arrow = dirArrows[i];
                if (arrow == null)
                    continue;
                arrow.enabled = AbilityHex.HasDirection(directions, i + 1);
            }
        }

        /// <summary>
        /// 开启 / 停止「正在研究」的闪烁：让 <see cref="typeBlink"/> 的 alpha 在
        /// 0 与 <see cref="BlinkMaxAlpha"/> 之间做正弦呼吸。
        ///
        /// <para>
        /// 停止时必须把 alpha 归零，不能只停协程 —— 否则会留下半透明的残影，
        /// 这条能力研究完之后会一直"灰一块"。
        /// </para>
        /// </summary>
        /// <param name="researching">true = 开始闪；false = 停止并归零</param>
        public void SetResearching(bool researching)
        {
            wantResearching = researching;
            ResolveBlinkLayer();
            if (typeBlink == null)
                return;

            if (researching)
            {
                // 未激活时 StartCoroutine 会直接报错（物体 inactive），交给 OnEnable 补
                if (!isActiveAndEnabled || blinkRoutine != null)
                    return;
                blinkRoutine = StartCoroutine(BlinkRoutine());
            }
            else
            {
                StopBlink();
            }
        }

        /// <summary>节点被激活时补启动闪烁（覆盖"先 SetResearching 后 SetActive"的调用顺序）</summary>
        void OnEnable()
        {
            if (wantResearching && blinkRoutine == null && typeBlink != null)
                blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        /// <summary>节点被隐藏时停掉协程，避免隐藏期间持续改颜色</summary>
        void OnDisable()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }
        }

        /// <summary>停止闪烁协程并把闪烁层恢复为全透明</summary>
        void StopBlink()
        {
            if (blinkRoutine != null)
            {
                StopCoroutine(blinkRoutine);
                blinkRoutine = null;
            }
            SetBlinkAlpha(0f);
        }

        /// <summary>只改 alpha，保留 prefab 上配的 RGB（与 type 同图同色）</summary>
        /// <param name="alpha">0~1</param>
        void SetBlinkAlpha(float alpha)
        {
            if (typeBlink == null)
                return;
            Color color = typeBlink.color;
            color.a = alpha;
            typeBlink.color = color;
        }

        /// <summary>
        /// 呼吸协程：用 <c>1 - cos</c> 做 0→1→0 的平滑往返，比线性往返少一次突变。
        /// 计时用 <see cref="Time.unscaledDeltaTime"/>，暂停/慢放时界面仍然可读。
        /// </summary>
        System.Collections.IEnumerator BlinkRoutine()
        {
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = 0.5f - 0.5f * Mathf.Cos(elapsed / BlinkPeriod * Mathf.PI * 2f);
                SetBlinkAlpha(k * BlinkMaxAlpha);
                yield return null;
            }
        }

        /// <summary>
        /// 应用状态：已研究 / 该武将已学 / 当前可选。
        ///
        /// 四态互斥优先级：已学 &gt; 可学/可研究 &gt; 已研究 &gt; 锁定。
        /// 注意遮罩条件是 <c>!researched &amp;&amp; !learnable</c>：
        /// "可研究但还没研究"的节点必须保持亮色，只有"前置未满足 / 隐藏未发现"才压暗。
        /// </summary>
        /// <param name="researched">势力已研究</param>
        /// <param name="learned">该武将已学过</param>
        /// <param name="learnable">当前可选（可培育 / 可研究）</param>
        public void ApplyState(bool researched, bool learned, bool learnable)
        {
            if (lockMask != null)
                lockMask.enabled = !researched && !learnable;
            if (researchedMark != null)
                researchedMark.enabled = researched;
            if (learnableMark != null)
                learnableMark.enabled = learnable && !learned && !researched;
            if (learnedMark != null)
                learnedMark.enabled = learned;
        }

        /// <summary>设置选中态</summary>
        /// <param name="selected">是否选中</param>
        public void SetSelected(bool selected)
        {
            if (selectedMark != null)
                selectedMark.enabled = selected;
        }

        /// <summary>点击 → 通知区域</summary>
        void OnClick()
        {
            if (owner != null && Ability != null)
                owner.NotifySelect(Ability);
        }

        /// <summary>
        /// 播放"隐藏能力出现"动画：从 0.3 倍放大到 1.15 倍再回弹到 1 倍，同时淡入。
        /// <para>
        /// 只在**刚激活、还没播过**的时候由 <see cref="UIAbilityStudyArea"/> 调一次；
        /// 播完节点恢复原状（scale = 1、alpha = 1），不影响后续布局。
        /// </para>
        /// </summary>
        /// <param name="delay">延迟秒数（多个槽同时出现时错开一点）</param>
        /// <param name="duration">动画时长秒数</param>
        public void PlayAppearAnimation(float delay = 0f, float duration = 0.45f)
        {
            if (!gameObject.activeInHierarchy)
                return;
            StartCoroutine(AppearRoutine(delay, duration));
        }

        /// <summary>出现动画的协程实现</summary>
        /// <param name="delay">延迟秒数</param>
        /// <param name="duration">时长秒数</param>
        System.Collections.IEnumerator AppearRoutine(float delay, float duration)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            transform.localScale = Vector3.one * 0.3f;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 用不受 timeScale 影响的计时：界面里可能处于暂停/慢放状态
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                // 过冲曲线：sin 峰在 k=0.5，视觉上"弹一下"
                float overshoot = 1f + Mathf.Sin(k * Mathf.PI) * 0.15f;
                float scale = Mathf.Lerp(0.3f, overshoot, Mathf.SmoothStep(0f, 1f, k));
                transform.localScale = Vector3.one * scale;
                group.alpha = k;
                yield return null;
            }

            transform.localScale = Vector3.one;
            group.alpha = 1f;
        }
    }
}
