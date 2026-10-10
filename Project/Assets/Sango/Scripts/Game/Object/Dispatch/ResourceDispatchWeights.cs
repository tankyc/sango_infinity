namespace Sango.Core
{
    /// <summary>
    /// AI 资源调度（城池之间的物资运输）的全部权重与阈值。
    ///
    /// 通过 <c>Data/Common/AIConfig.json</c> 的 <c>"resourceDispatch"</c> 节点做部分覆盖
    /// （未出现的字段沿用这里的默认值），与 <see cref="DeploymentWeights"/> 同一套机制。
    ///
    /// 【调度宗旨】以**势力**（非玩家）或**军团**（玩家）为域，按圈层水位把资源从纵深推向边境：
    ///   前线（圈层 0）防御 / 进攻 → 次前线（圈层 1）支援 → 后方（≥2）储备 + 产出。
    /// 【与人才调度的分工】资源调度**只提出运输需求**（哪天城有货要走、缺运输主将），
    /// 武将的实际调动仍唯一经 <see cref="DeploymentExecutor"/>，见 <see cref="ResourceDispatchState"/>。
    /// </summary>
    public class ResourceDispatchWeights
    {
        // ==================== 总开关 / 观察 ====================

        /// <summary>总开关：关闭后求解与执行都不运行（老的城池运输命令也已被移除）</summary>
        public bool enabled = true;

        /// <summary>
        /// 影子模式：只求解并输出计划，**不派任何运输队**。调参时用来看"AI 会怎么运"。
        /// </summary>
        public bool shadowOnly = false;

        /// <summary>是否输出调度报告（仅编辑器 / 开发包生效，见 ResourceDispatcher.LogEnabled）</summary>
        public bool logEnabled = true;

        /// <summary>报告的势力 id（-1 = 全部；0 = 只报告玩家势力；&gt;0 = 只报告该势力）</summary>
        public int logForceId = -1;

        /// <summary>报告的军团 id（0 = 不按军团过滤；&gt;0 = 只报告该军团。仅军团作用域有意义）</summary>
        public int logCorpsId = 0;

        /// <summary>报告间隔（按势力回合数计；0 = 每回合）</summary>
        public int logIntervalTurns = 5;

        // ==================== 港关（渡口 / 关隘）：主城的附属仓储点 ====================
        //
        // 设计口径（四条）：
        //   ① **只有"有军情"的港关才提资源需求** —— 被围 / 境内有敌军 / 附近有敌军 /
        //      邻接外势力城（含无主）。平时它们不参与"要货"，也就不用和主城抢运力；
        //   ② **无军情的港关只留底仓**：金 / 兵装 / 器械 / 船一律为 0（造出来就上交），
        //      粮只留守军口粮，兵按下面的保底值留；
        //   ③ **富余只往归属主城送**（主城再统一往前线转），港关之间不互相倒货；
        //   ④ **优先级低于主城**：同一圈层档里都市排在港关前面先被填满。
        //
        // 这样一来"港关"就是主城伸出去的手：平时把金 / 粮往主城喂，打起来（有军情）才要兵要粮守关口。

        /// <summary>无军情港关：粮草只留守军几回合口粮（1 粮养 10 兵；0 = 全上交主城）</summary>
        public float portGateQuietFoodTurns = 10f;

        /// <summary>无军情港关：金钱底仓（绝对值；0 = 全上交主城）</summary>
        public int portGateQuietKeepGold = 0;

        /// <summary>
        /// 无军情港关：兵力底仓（绝对值；0 = 全上交主城）。
        /// 默认 2000 —— 关口平时也要留点人扎住，不然遇袭当场就丢（那时才算"有军情"、才会要兵，
        /// 但兵赶过去是要时间的）。多出来的兵同样上交主城。
        /// </summary>
        public int portGateQuietKeepTroops = 2000;

        /// <summary>
        /// "有军情"是否把**邻接外势力城（含无主城）**也算进去。
        /// 计入时，贴着敌境的渡口 / 关隘（如潼关）算有军情、正常要货；
        /// 不计入则只看被围 / 境内有敌军 / 附近有敌军。
        /// </summary>
        public bool portGateMilitaryNeighbor = true;

        /// <summary>
        /// 无军情港关的富余是否**只**运往归属主城。
        /// true（默认）= 只喂主城，主城再统一往前线 / 其它城转（避免几十个渡口各自乱发货）；
        /// false = 与普通城一样按常规梯度发货。
        /// </summary>
        public bool portGateOnlyToParent = true;

        /// <summary>
        /// **有军情**的港关的水位缩放：它按主城那一档（通常是"前线 = 仓容 × 95%"）取水位，
        /// 这里再乘一道。默认 0.6 —— 关口是前进基地，守得住就行，不必像主城那样把仓库堆满
        /// （堆太满反而容易在被攻破时整仓资敌）。配 1.0 则与主城同口径。
        /// </summary>
        public float portGateFillFactor = 0.6f;

        // ==================== 圈层水位（**绝对值**） ====================
        //
        // 【为什么用绝对数量，而不是"仓容的百分比"】
        // 不少剧本改过金 / 粮 / 兵 / 兵装 / 器械 / 船的仓容上限（City 的 goldLimit / foodLimit /
        // troopsLimit / storeLimit 基础值，再叠加等级加成与剧本加成）。按百分比定水位会**跟着上限一起虚高**：
        // 后方城的"基本线"高到永远达不到 → 它永远处在缺货状态、一点富余都不肯外运，
        // 整个调度就空转了（同时前线那边显示着永远填不满的巨额缺口）。
        // 所以水位一律写成**绝对数量**。仓容只保留两个作用：
        //   ① 安全线保护：目标不超过 仓容 × safeMargin（免得被硬塞到溢出）；
        //   ② 收货容量：还能收多少 = 仓容 × safeMargin − 现有 − 在途。
        //
        // 【三档圈层】每类资源三个数：前线（圈层 0）/ 次前线（圈层 1）/ 后方（圈层 ≥2 与圈层未定）。
        // 但**前线一般不取这里的前线数**：前线按"能装多少装多少"（仓容 × frontFillRatio，默认 95%），
        // 这三个 front 值只在把 frontFillRatio 配成 0（改回绝对值口径）时才生效。
        // 默认值的量级参照常见额度（金库 10 万 / 粮仓 100 万 / 兵力上限 10 万 / 仓库 40 万）：
        //   · 金 / 粮 / 兵：见下面的 ①，绝对数量；
        //   · 兵装 / 器械 / 船：见下面的 ②，**每千兵件数**（随兵力水位缩放）。
        //   · 粮的取法：前线留够 40 回合口粮（1 粮养 10 兵，5 万兵 = 每回合 5000）且够给出征部队带粮；
        //   · 金的取法：够本城内政与生产门槛（createItemsMinGold / createMachineMinGold 都是绝对金额）。

        // ---- ① 绝对水位线：金 / 粮 / 兵（本城应当持有的数量）----

        /// <summary>金钱水位线（绝对数量）</summary>
        public WaterLine waterGold = new WaterLine(10000, 5000, 1000);
        /// <summary>粮草水位线（绝对数量）</summary>
        public WaterLine waterFood = new WaterLine(200000, 80000, 20000);
        /// <summary>兵力水位线（绝对数量）</summary>
        public WaterLine waterTroops = new WaterLine(50000, 30000, 8000);

        // ---- ② 军备水位线：兵装 / 器械 / 船 —— **按人头算**（每千兵多少件）----
        //
        // 本游戏的这三类都是"每千兵消耗多少件"，与兵装同一套换算
        //（TroopType.costItems 形如 [种类, 每千人消耗]，见 ItemStore.CheckCostMin：
        //  1 件装备 1 名士兵），**不是**三国志11 原版那种"一艘船 / 一辆冲车就是一个独立单位"。
        // 所以它们的存量要求必须随**兵力**走，写成固定的绝对件数是错的：
        //     水位 = 本城圈层的兵力水位 × 该值 / 1000
        // 举例：次前线兵力水位 30000、器械 20 → 次前线器械水位 = 600 件。
        // 后方配 0 = 一件不留，造出来全往前线送。

        /// <summary>兵装水位线（每千兵件数；1000 = 与兵力 1:1）</summary>
        public WaterLine armsPerThousand = new WaterLine(1000, 1000, 1000);
        /// <summary>器械水位线（每千兵件数；冲车 / 投石）</summary>
        public WaterLine machinePerThousand = new WaterLine(60, 20, 0);
        /// <summary>船水位线（每千兵艘数；内陆城由 GetTarget 直接判 0）</summary>
        public WaterLine boatPerThousand = new WaterLine(40, 15, 0);

        /// <summary>
        /// **前线（圈层 0）的水位口径**：<c>仓容 × 该比例</c>（默认 0.95，留 5% 缓冲）。
        ///
        /// 前线是全势力的资源终点 —— 宗旨是"能装多少装多少"，囤得越多越扛得住进攻，
        /// 所以它**不用绝对值**（绝对值是给次前线 / 后方的，那里才需要"只留底仓"）。
        /// 配 0 则前线改用 <c>water*.front</c> 那条绝对值线。
        /// </summary>
        public float frontFillRatio = 0.95f;

        /// <summary>
        /// 一条"水位线"：某类资源在三个圈层档位上应当持有的**绝对数量**。
        /// 用对象而不是三个平铺字段，是为了在 AIConfig.json 里能按资源整块覆盖
        /// （例如 <c>"waterFood": { "front": 300000 }</c>，未写的档位保持默认值）。
        /// </summary>
        public class WaterLine
        {
            /// <summary>前线（圈层 0）</summary>
            public int front;
            /// <summary>次前线（圈层 1）：作为前线的兵力 / 物资池</summary>
            public int subFront;
            /// <summary>后方（圈层 ≥2 与圈层未定）：只留底仓，其余全部往前线送</summary>
            public int rear;

            public WaterLine() { }
            public WaterLine(int front, int subFront, int rear)
            {
                this.front = front;
                this.subFront = subFront;
                this.rear = rear;
            }
        }

        /// <summary>前线城处于"进攻姿态"（本城已有部队出征）时，前线水位线乘上的倍数</summary>
        public float frontAttackFactor = 1.2f;

        // ---------- 战况加成 / 和平削减（水位不再只看圈层） ----------

        /// <summary>兵临城下时的水位加成（打起来要多备粮草 / 兵力 / 军械）</summary>
        public float threatWaterBonusHigh = 0.15f;
        /// <summary>战火就在旁边（邻接友城被围 / 附近有敌方部队）时的水位加成</summary>
        public float threatWaterBonusMid = 0.08f;
        /// <summary>彻底太平（非前线、无被围、无邻城被围、附近无敌军）的水位削减：少留底、多往前线送</summary>
        public float peacefulWaterCut = 0.1f;

        /// <summary>
        /// "紧急插队"的缺口阈值（对 <c>Ledger.deficitWeight</c>：各类资源缺失比例之和 × 100，最大 600）。
        /// 达到该阈值视为"快断粮"，在目标排序与发货排序里**按上一个圈层对待**，可以越级抢运输配额。
        ///
        /// 解决的问题：原本排序是"圈层绝对优先"，于是"圈层 0 里只差 1% 的城"会一直压住
        /// "圈层 1 里快断粮的城"，配额（每回合就那么几车）全被不着急的城吃掉。配 0 关闭插队。
        /// </summary>
        public int criticalDeficitWeight = 250;

        /// <summary>
        /// 产地（有锻冶 / 马厩 / 工坊 / 船厂）的**保底资金**：这类城的金钱水位不低于该值。
        ///
        /// 理由：造兵装 / 器械 / 船是按绝对金额设门槛的（<c>AIConfig.createItemsMinGold = 1000</c>、
        /// <c>createMachineMinGold = 1500</c>），若按后方水位（0.35）把金钱抽走，
        /// 金库小的产地会掉到门槛以下，生产当场停摆 —— 那比"少运点钱去前线"更亏。
        /// 配 0 关闭。
        /// </summary>
        public int productionGoldKeep = 1500;

        /// <summary>
        /// 安全余量：任何接收方都**不允许被送到**超过 <c>上限 × 该比例</c>。
        /// 用途：给"部队回城 / 月度产出 / 途中在途量"留出缓冲 ——
        /// 金 / 粮 / 兵超过库容会被 <c>City.AddGold</c> 直接丢掉；兵装 / 器械 / 船超过容器上限后
        /// 该城就再也造不出来（<c>CityCreateItems</c> 按 <c>ItemType.TransformLimit</c> 判定）。
        ///
        /// 【同时是水位上限】各处水位线（含进攻姿态加成后的前线线）都会被夹到这条线，
        /// 保证"想要多少"与"收货能收多少"是同一个数。因此当某条水位线高过 <c>仓容 × 0.95</c> 时，
        /// 实际水位就是 <c>safeMargin</c> —— 想真按 100% 堆满，要把这里设成 1.0
        /// （代价是下一次产出 / 部队回城就会溢出被丢掉）。
        /// </summary>
        public float safeMargin = 0.95f;

        /// <summary>
        /// 紧急抽调底线：前线仍缺货时，允许纵深城（圈层 ≥1）把自身降到
        /// "**自身水位线 × 该比例 × (1 − 前线紧急度)**"（绝不适用前线自身）。
        ///
        /// 乘的是**水位线**而不是仓容上限：水位线本身就是按剧本调过的绝对数量，
        /// 而仓容在改过上限的剧本里会高得离谱 —— 拿它算底线会一点货都抽不出来。
        ///
        /// 【为什么再乘 (1 − 前线紧急度)】前线越吃紧，后方越该让路：
        /// 前线只是"没堆满"时后方留 20% 底仓；前线真快断粮 / 快守不住时（紧急度 → 1）
        /// 底线直接压到 0 —— 也就是**允许把后方抽空**（见 <see cref="vitalFoodTurns"/> / <see cref="vitalFrontTroops"/>）。
        /// </summary>
        public float emergencyFloorRatio = 0.2f;

        /// <summary>
        /// 前线"快断粮"的判定：守军口粮还够撑几回合（1 粮养 10 兵，见 baseFoodCostInTroop）。
        /// 低于该回合数开始计入前线紧急度，为 0 时紧急度拉满（后方可被抽空）。
        /// </summary>
        public float vitalFoodTurns = 10f;

        /// <summary>
        /// 前线"快守不住"的判定：边境城兵力低于该绝对值即视为告急（紧急度按缺口比例上升）。
        /// 这是**守军底线**，与水位线无关 —— 水位线是"想囤多少"，这个是"低于就危险"。
        /// </summary>
        public int vitalFrontTroops = 10000;

        /// <summary>是否启用紧急抽调（关掉则纵深城只出"自身水位以上"的部分）</summary>
        public bool enableEmergencyDrain = true;

        // ==================== 参与调度的资源 ====================

        /// <summary>是否调运金钱</summary>
        public bool includeGold = true;
        /// <summary>是否调运粮草</summary>
        public bool includeFood = true;
        /// <summary>是否调运兵力</summary>
        public bool includeTroops = true;
        /// <summary>是否调运兵装（枪 / 戟 / 弩 / 马）</summary>
        public bool includeArms = true;
        /// <summary>是否调运器械（冲车 / 投石）</summary>
        public bool includeMachine = true;
        /// <summary>是否调运船（走舸 / 楼船 / 斗舰）</summary>
        public bool includeBoat = true;

        /// <summary>
        /// 兵装**紧缺程度**的需求基准：<c>兵力 × 该比例</c>（与"军备"岗同口径，1.0 = 每兵一件）。
        ///
        /// 注意它是给**紧缺程度**（排序 / 插队）用的；兵装的**水位线**本身也按人头算
        /// （见 <see cref="armsPerThousand"/>），两者口径一致但用途不同：水位线决定"留多少 / 要多少"，
        /// 这个基准决定"缺得多急"。
        /// 这个基准只回答"这座城市到底缺不缺兵装、缺得多急"（见 <c>ResourceBalance.SeverityOf</c>），
        /// 用来决定发货顺序与"紧急插队"：兵多而兵装少 → 排在前面；兵少的小城仓库空着也不算急。
        /// </summary>
        public float armamentCoverTarget = 1.0f;

        // ---------- 六类资源的水位口径（统一） ----------
        // 金 / 粮 / 兵 / 兵装 / 器械 / 船 一律按**圈层绝对值**（上面 water* 那六条线）：
        //     目标 = 本城圈层对应档位的绝对数量 × 战况/和平微调，再夹到 仓容 × safeMargin
        // 只有两处特例：① 船在内陆城（无港口 / 非港关）目标为 0；
        //              ② 产地（有锻冶 / 马厩 / 工坊 / 船厂）的金钱保留一笔启动资金。
        //
        // 【容器的上限怎么算】道具库按 storeKind 分格，单格上限 = 城池仓库上限 × 道具的 p1 千分比
        // （<c>ItemType.TransformLimit</c>；p1 == 0 表示占满整个仓库上限）。同 storeKind 的多个道具
        // （例：冲车 / 木兽共用 6 号格）取其中最大的比例；组级上限 = 组内各格之和：
        //   兵装 = 枪 / 戟 / 弩 / 马 四格，器械 = 冲车 / 投石 两格，船 = 一格。

        // ==================== 运兵的"护送粮" ====================
        //
        // 兵是要吃粮的，而且代价很硬：
        //   · 队伍每回合消耗 <c>兵力 × baseFoodCostInTroop(0.1) × 兵种粮耗倍率</c>（即"1 粮养 10 兵"）；
        //   · 断粮的队伍每回合直接掉 **30% 的兵**（<c>Troop.OnForceTurnStart</c>：不够 500 就整队消失）；
        //   · 就算活着到了，这支部队也变成**接收城要养的口粮负担**。
        // 所以运兵必须随车带干粮，而且要 > 路程消耗：一半路上吃掉、一半留给城里做缓冲。
        // 粮不够时不是硬发，而是**按粮缩兵**（宁可少运，也不能半路掉 30%）。

        /// <summary>
        /// 预估"一城跳"要多少回合脚程（运输队走得慢，默认 2 回合/跳）。
        /// 只用于估算路上要吃的粮，不必精确：真正兜底在执行层按实际粮食再夹一次。
        /// </summary>
        public float escortTurnsPerHop = 2f;

        /// <summary>
        /// **兵装要求**的规模门槛：单批运兵达到这个规模才要求"按兵配装"（1 件装备 1 兵）。
        ///
        /// 【注意】**兵粮不受这个门槛约束** —— 粮是"一定要保证"的：没粮的队伍每回合掉 30% 的兵，
        /// 而且进城后还会变成接收城的口粮负担，所以任何规模的运兵都必须带够护送粮
        /// （不够就按粮缩兵，见 <see cref="escortFoodFactor"/>）。
        /// 兵装不一样：小股调动没配装还能靠目标城自己的库存补上，不值得为它卡住运兵。
        /// 配 0 = 所有运兵都按门槛要求配装（只在 <see cref="requireArmsForTroops"/> 打开时有意义）。
        /// </summary>
        public int escortMinTroops = 1000;

        /// <summary>护送粮相对"路程消耗"的倍数（口径：必须 &gt; 2 倍路程消耗）</summary>
        public float escortFoodFactor = 2f;

        /// <summary>
        /// 护送粮是否只能来自"水位以上的富余"。
        ///
        /// 默认 false（可以从储备里拿）—— 理由：随兵离城的还有这些兵**往后每回合的口粮需求**，
        /// 抽走这笔粮对源城基本是中性的；而"运不出兵"的代价（前线拿不到援军）更实在。
        /// 配 true 则只有粮草富余的城才准运兵。
        /// </summary>
        public bool escortFoodMustBeSurplus = false;

        /// <summary>
        /// 运兵随行的兵装比例（件 / 每兵）—— **上限**，不是硬性配比。
        ///
        /// 与 <c>ItemStore.CheckCostMin</c> 同一口径：兵种 <c>costItems</c> 形如
        /// [兵装种类, 每千人消耗]，例如枪兵 [2, 1000] = "1 件枪装备 1 名士兵"。
        /// 默认 1，但实际随行量还要**按目标城的兵装富余来夹**：
        ///     带装量 = min(兵力 × 该比例, **目标城的兵装缺口**, 源城可动用兵装)
        /// 目标城兵装已达到水位（有富余）→ 一件不带；缺装 → 顺手把缺口撮上（同车省一趟车）。
        /// 见 ResourceBalance.Allocate 里的"兵装随行"一段。
        /// </summary>
        public float escortArmsPerTroop = 1f;

        /// <summary>
        /// 是否要求"兵装齐备才运兵"。
        ///
        /// **默认关闭**：兵装不保证 —— 兵与粮是"守不守得住"的硬需求，兵装只是"打得好不好"，
        /// 前线缺兵时不该因为"凑不齐装备"而不发援兵（装备随后由兵装调度那条线单独送）。
        /// 关闭时仍会**尽力捎带**：按目标城的兵装缺口随车带一部分（见
        /// <see cref="escortArmsPerTroop"/>），只是不因此缩兵、不卡单。
        /// 打开后：单批 ≥ <see cref="escortMinTroops"/> 的运兵若凑不齐该配的量，就**按兵装缩兵**。
        /// </summary>
        public bool requireArmsForTroops = false;

        // ---------- 兵装需求登记 → 城池 AI 命令优先级协调 ----------

        /// <summary>
        /// 兵装需求的判定比例：本城兵装 &lt; 兵力 × 该比例 时登记"缺兵装"。
        ///
        /// 口径对齐 <c>CityOrderWeights.weaponCriticalCoverRatio</c>（危机线，默认 0.5），
        /// 而**不是**目标线 <c>weaponCoverRatio</c>（默认 1.3）：
        /// 目标线用于"造兵装命令优先"（城池命令排序自己现算），而这里决定的是
        /// "要不要从运输队手里抢人留给生产端"，只在兵装严重枯竭时生效，
        /// 否则绝大多数城都会要求多留人，把运输队的人抽空。
        /// </summary>
        public float armsDemandCoverRatio = 0.5f;

        /// <summary>兵装缺口达到多少件才值得登记（缺口太小就交给原有的命令评分，不去动优先级）</summary>
        public int armsDemandMinGap = 2000;

        /// <summary>
        /// 登记了兵装缺口的城，抽运输主将时**额外保留**几名空闲武将。
        ///
        /// 原因：兵装生产（<c>CityAI.AICreateItems</c>）的前提是"本城还有空闲武将"。
        /// 资源调度自己就要抽人押车，若不留给生产端，就会出现"运输把人抽走 → 造不出兵装 →
        /// 运来的兵也装备不上"的自锁。
        /// </summary>
        public int armsDemandProtectPersons = 1;

        // ==================== 起运门槛（低于门槛不派车，省人员） ====================

        /// <summary>单次最少起运金钱</summary>
        public int minShipGold = 500;
        /// <summary>单次最少起运粮草</summary>
        public int minShipFood = 1000;
        /// <summary>单次最少起运兵力</summary>
        public int minShipTroops = 200;
        /// <summary>单次最少起运兵装</summary>
        public int minShipArms = 50;
        /// <summary>单次最少起运器械</summary>
        public int minShipMachine = 1;
        /// <summary>单次最少起运船</summary>
        public int minShipBoat = 1;

        /// <summary>
        /// **攒够一车再走**：一车至少要覆盖收货城该资源缺口的这个比例，否则这一趟不发 ——
        /// 货留在源城继续攒，下回合（或几回合后）**一次性运走**。
        ///
        /// 【为什么要这道闸】运输的代价是**一名武将押一车**（运输主将抵达后就并入目标城，
        /// 等于一次人员外流）。而"水位以上的每一分富余"每回合都会被判成可发 ——
        /// 于是出现碎片化运输：几十条单车小单（金几百、粮几千），后方武将全被抽去赶车，
        /// 前线每回合只收到一点零头。
        ///
        /// 【口径】
        ///   · 基准 = 收货城该资源的**剩余缺口**（水位 − 现有 − 在途）= "这车能解决它多少"；
        ///   · 门槛 = max(该资源的固定起运门槛 minShip*，缺口 × 本值)；
        ///   · **溢出安抚趟不压** —— 货已经压仓，必须走，否则烂在库里；
        ///   · **兵不压** —— 兵本来就是整批走（随车带护送粮 / 兵装），而它的缺口是按
        ///     被剧本改过的兵力上限算出来的（动辄十万），按比例压会直接停掉援军；
        ///   · 粮有**救急豁免**，见 <see cref="batchFoodCrisisTurns"/>。
        ///
        /// 0 = 关闭（回到"够固定门槛就发"的旧行为）。
        /// </summary>
        public float batchShipRatio = 0.3f;

        /// <summary>
        /// 粮的**救急豁免**：收货城口粮不足这么多回合时，粮草不压批次、立刻发。
        ///
        /// 缺粮是"下一回合就掉 30% 兵"的事（<c>Troop.OnForceTurnStart</c>：不够 500 整队消失），
        /// 而粮草缺口在改过上限的剧本里动辄几十万 —— 按比例压批次会让一车都凑不齐，
        /// 前线先饿死。所以口粮见底时不讲批次，有多少发多少（1 粮养 10 兵）。
        /// </summary>
        public float batchFoodCrisisTurns = 4f;

        // ==================== 运输队自身（车队） ====================

        /// <summary>
        /// 运输队**额外**要带的"10 天口粮"（默认 10，单位 = 回合）——**不是**总回合数。
        ///
        /// 【为什么必须有】<c>Troop.food</c> 是**货物与口粮共用的同一个池**，而"只运金 / 兵装 /
        /// 器械 / 船"的单子在求解层不带任何护送粮（护送粮是给"运兵"算的）→ <c>troop.food = 0</c>。
        /// 后果不是"慢一点"：<c>Troop.OnForceTurnStart</c> 里 <c>food ≤ 0</c> 且兵力 &lt; 500
        /// 就整队 <c>Clear()</c> —— 车队**当回合原地消失**，整车货与押车武将一起丢
        /// （表现就是"运输部队没有粮食也运输东西，还没到就消失了"）。
        ///
        /// 【硬口径】车队**不管运什么**，都必须携带：
        ///   <c>口粮 = ceil(每回合粮耗) × (行程回合数 + 本值)</c>
        /// 其中"行程回合数 = 跳数 × <c>escortTurnsPerHop</c>"（路上会吃掉的那部分），
        /// 本值是最上面那笔**额外**口粮（到城时还剩着的缓冲）。与货物粮**叠加**，
        /// 不是取大 —— 车队吃的是同一个池子，不额外带粮就等于吃掉货，目标城到手就少了。
        /// 每回合粮耗与游戏一致（<c>baseFoodCostInTroop × 兵力 × 运输兵种粮耗倍率</c>），
        /// **先向上取整到 1 再乘**：1 兵的车队每回合也要吃 1 粮（游戏就是 ceil），
        /// 直接乘小数会算出"0 粮"这种看着合理、实际必死的数。
        /// </summary>
        public float convoyProvisionTurns = 10f;

        /// <summary>
        /// 派车是否需要**不经水格的陆路**（默认 true）。
        ///
        /// 【为什么】调度侧的通路判定（<c>CityAI.IsPathClear</c>）与任务侧的准备
        /// （<c>TroopTransformGoodsToCity.Prepare</c>）都用 <c>Map.GetDirectPath</c> 的默认口径：
        /// 它只看地形的全局 <c>moveable</c> 标志，**水格也算可走**；而真正走位用的是按本队能力算的
        /// <c>Map.GetMoveRange</c>（进水格要 <c>waterMoveAbility</c>）。
        /// 两边口径不一致 → 跨河的路线被判成"通畅"，车队被派出去后在岸边原地转
        /// （<c>Troop.TryMoveToCell</c> 找不到落脚点时会**静默 return true 且不移动**）。
        /// 打开本值：要求一条"整条路径都不含水格"的通路，走不通就不派车（货留在城里，
        /// 比派一支注定到不了的车好）。若你的剧本里运输队确实能走水，把它关掉。
        /// </summary>
        public bool transportRequireLandRoute = true;

        /// <summary>
        /// 运输队"连续多少回合没挪过窝"就判定卡死并**原路返城**（默认 3；0 = 不判）。
        ///
        /// 兜底对象：陆路走不通（隔河 / 渡口被堵）、被敌建筑或 ZOC 卡住 —— 这类情况原实现里
        /// 没有任何超时，车队既不前进也不回家，最后被顺手吃掉，整车物资陪葬。
        /// 返城后货与人回到源城，下回合调度会重新评估（可能改走中转接力）。
        /// </summary>
        public int convoyStuckTurns = 3;

        // ==================== 玩家军团 ====================

        /// <summary>玩家军团是否参与资源调度（关闭则玩家势力完全手动）</summary>
        public bool enablePlayerCorps = true;

        /// <summary>
        /// 玩家**第一军团**（君主所在、玩家直辖）是否参与资源调度。
        /// 默认 false：第一军团的物资由玩家自己安排，AI 不插手。
        /// 打开后第一军团也会自动运货（资源运输不会打乱玩家排好的阵型，属于纯增益）。
        /// </summary>
        public bool includeCapitalCorps = false;

        /// <summary>
        /// 是否把"本回合自动发出的运输队"以消息形式告诉玩家（只对玩家势力，默认开）。
        ///
        /// 必要性：玩家军团的资源也会被自动搬运，而调度报告只在编辑器 / 开发包输出 ——
        /// 正式包里玩家会看到"资源莫名变少"却查不到原因。消息按**目的地**合并（点它可跳到那座城）。
        /// </summary>
        public bool notifyPlayer = true;

        /// <summary>每回合每个军团最多给玩家几条调度消息（多余的合并成"另有 N 座城"）</summary>
        public int notifyMaxCityPerTurn = 4;

        // ==================== 溢出安抚 ====================

        /// <summary>
        /// 是否启用仓位安抚（第二趟分配）：把"水位线到安全线之间的空余仓容"也用起来 ——
        /// 还缺货的城在这一趟放宽到 <c>上限 × safeMargin</c>，源城仍只出"自身水位以上"的部分。
        /// 用途：避免产出 / 部队回城把上限顶爆（金粮兵超出即丢；兵装器械船满仓后该城就再也造不出来）。
        /// </summary>
        public bool enableOverflowRelief = true;

        // ==================== 配额 ====================

        /// <summary>
        /// 源城至少保留多少空闲武将才允许发车（默认 2）。
        ///
        /// 必要性：运输主将抵达目标城后会**并入目标城**（<c>Troop.EnterCity</c>），
        /// 这实际上是一次"随车的人员外流"，而编制层的"后方保底 / 源城守备下限"管不到运输主将。
        /// 不留下限的话，一座城可能被连续几回合抽空空闲武将，征兵 / 内政当场停摆。
        /// 被这条挡住时会走"待发存量"通道请人才调度补人（见 ResourceDispatchState）。
        /// </summary>
        public int minSourceFreePersons = 2;

        /// <summary>每个作用域每回合最多实际派出多少支运输队（**下限**；人也是稀缺资源，不能一次全抽走）</summary>
        public int maxShipmentsPerDomain = 4;

        /// <summary>
        /// 配额随规模缩放：每多少座城追加 1 支车的额度（0 = 不缩放，固定用上面的下限）。
        ///
        /// 理由：固定的 4 车额度在小势力够用，但大帝国（20+ 城）会长期"想运的多、配额不够"，
        /// 被配额挡住的富余还会让产物端的背压误判。按城数放大后，战线越长、运力越足。
        /// </summary>
        public int shipmentsPerCityDivisor = 5;

        // ==================== 中转接力 ====================

        /// <summary>
        /// 直接投送的距离上限（跳数；0 = 不限，一律直接投送）。
        ///
        /// 超过这个距离就不把货直接运去前线，改为**先运到途中最靠近源城的己方城**，
        /// 下一回合由那座城转发（<see cref="enableRelayStaging"/>）。
        /// 好处：长距离直接投送会把物资压在路上好几回合，一旦半路被截整车全丢；
        /// 拆成一腿一腿走，前线能更早开始陆续到货，且每段路程都在己方纵深内。
        /// 找不到合适的中转城时**仍然直接投送**（不会把前线饿着）。
        /// </summary>
        public int maxDirectHops = 4;

        /// <summary>是否启用中转接力（关闭则一律直接投送）</summary>
        public bool enableRelayStaging = true;

        /// <summary>每座城每回合最多向外发几车（避免一座富城把城内空闲武将一次抽空）</summary>
        public int maxShipmentsPerSourceCity = 1;

        // ==================== 目标城失守判断（送不送 / 撤不撤） ====================

        /// <summary>
        /// 目标城"即将失守"的判定比例（0 = 不判断）：
        /// **兵临城下**（最近敌人 ≤ 6 格，与部署层 / 态势层的口径一致）且
        /// 敌方兵力 ≥ 我方守军 × 该比例时，视为这座城市很可能守不住。
        ///
        /// 两个用途（共用同一个判定，见 <see cref="ResourceBalance.IsTargetDoomed"/>）：
        ///   ① 调度侧：不再往这种城送货 —— 免得货跟着城池一起丢；
        ///   ② 运输侧：已经在路上的运输队立刻转回创建城（<c>TroopTransformGoodsToCity</c>）。
        /// </summary>
        public float doomedDefenseRatio = 2f;

        /// <summary>是否跳过"即将失守"的目标城（关闭则照常送，风险自负）</summary>
        public bool skipDoomedTarget = true;

        /// <summary>
        /// 被围攻的城是否停止向外发货。守城优先，而且运输队多半也出不了城
        /// —— 免得"把守军的粮草运走、城却打起来了"。
        /// </summary>
        public bool skipBesiegedSource = true;

        /// <summary>
        /// 派车前是否检查两城之间的直线通路（沿途有敌方建筑就不发车）。
        /// 与旧的城池运输命令同一判定（<c>CityAI.IsPathClear</c>）：不检查的话运输队会被半路吃掉、整车物资全丢。
        /// 前线的敌前建筑多，打开会明显减少发车量；关掉请自行承担损失。
        /// </summary>
        public bool requireClearPath = true;

        // ==================== 港关 ====================

        /// <summary>
        /// 港关与归属都市之间的调运成本优惠（按"少算几跳"处理）。
        /// 二者是同一个资源单元：港关富余优先上交归属都市、都市缺货优先从自家港关调。
        /// </summary>
        public int parentCityBias = 2;

        /// <summary>
        /// 军团委任"指定运输目标城"（<c>Corps.AppointContentType.Transport</c>）的优先级优惠。
        ///
        /// 语义：某军团指定了目标城 X 时，该军团的城池**优先**把货送给 X
        /// （而不只是"有货就近发"）。这是给玩家的一条人工干预通道，AI 势力的委任值恒为 0，不受影响。
        /// </summary>
        public int designatedTargetBias = 30;
    }
}
