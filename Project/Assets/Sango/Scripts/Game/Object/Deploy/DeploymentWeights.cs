namespace Sango.Core
{
    /// <summary>
    /// 一种岗位的能力权重（可 JSON 覆盖）。
    /// </summary>
    public class PositionWeights
    {
        /// <summary>统率</summary>
        public float command;
        /// <summary>武力</summary>
        public float strength;
        /// <summary>智力</summary>
        public float intelligence;
        /// <summary>政治</summary>
        public float politics;
        /// <summary>魅力</summary>
        public float glamour;
        /// <summary>搬运特性</summary>
        public float transport;

        /// <summary>转成紧凑向量。</summary>
        public PostFit ToFit()
        {
            PostFit fit;
            fit.command = command;
            fit.strength = strength;
            fit.intelligence = intelligence;
            fit.politics = politics;
            fit.glamour = glamour;
            fit.transport = transport;
            return fit;
        }
    }

    /// <summary>
    /// AI 人才部署的全部权重与阈值。
    ///
    /// 通过 <c>Data/Common/AIConfig.json</c> 的 <c>"deployment"</c> 节点做部分覆盖
    /// （未出现的字段沿用这里的默认值）。
    /// </summary>
    public class DeploymentWeights
    {
        /// <summary>总开关：关闭后影子模式与执行都不运行</summary>
        public bool enabled = true;

        /// <summary>
        /// 影子模式：只计算并输出部署计划，**不执行任何调动**。
        /// 【Phase C】默认 false —— 由新执行层真正调人（旧调人逻辑已删除）。
        /// 临时想"只看不动"，把它设回 true 即可（此时不会有人调人）。
        /// </summary>
        public bool shadowOnly = false;

        /// <summary>是否输出影子报告（仅编辑器 / 开发包生效）</summary>
        public bool shadowLogEnabled = true;

        /// <summary>影子报告的势力 id（0 = 只报告玩家势力）</summary>
        public int shadowLogForceId = -1;

        /// <summary>影子报告间隔（按势力回合数计；0 = 每回合）</summary>
        public int shadowLogIntervalTurns = 3;

        /// <summary>影子报告的军团 id（0 = 不按军团过滤；&gt;0 = 只报告该军团。仅军团级调度有意义）</summary>
        public int shadowLogCorpsId = 0;

        // ==================== 军团边界（只约束玩家势力） ====================

        /// <summary>
        /// 是否强制"军团内调动"边界：源城与目标城必须属于同一军团，**没有例外方向**。
        ///
        /// **只对玩家势力生效**：玩家军团之间不互相调人（跨团会打乱玩家自己排好的部署），
        /// 第一军团由玩家直辖、AI 完全不介入。
        /// 非玩家（AI）势力以**势力**为边界 —— 势力内可以跨军团调人，不受此开关约束。
        /// </summary>
        public bool enforceCorpsBoundary = true;

        // ==================== 编制：通用 ====================

        /// <summary>基础席位：按城池等级 Id-1 取（小 / 中 / 大 / 巨城）—— 军事岗位的上限</summary>
        public int[] levelSeat = new int[] { 3, 4, 5, 6 };
        /// <summary>前线（borderLine==0）额外军事岗位</summary>
        public int borderRingBonus = 2;
        /// <summary>次前线（borderLine==1）额外军事岗位</summary>
        public int subFrontRingBonus = 1;
        /// <summary>高威胁阈值（对 threatLevel）</summary>
        public int threatHighAt = 2000;
        /// <summary>中威胁阈值（对 threatLevel）</summary>
        public int threatMidAt = 800;
        /// <summary>高威胁额外军事岗位</summary>
        public int threatHighBonus = 2;
        /// <summary>中威胁额外军事岗位</summary>
        public int threatMidBonus = 1;
        /// <summary>港关守备最大常驻人数（最低 0；决策③ 放开到 5）</summary>
        public int portGateMaxSeat = 5;

        // ------------------------------------------------------------------
        // 港关：**不常驻武将**（只在有军情时才派人守 / 作为进攻出发点）
        //
        // 判定口径与资源调度同一份数据（<see cref="CitySituation.hasForeignNeighbor"/> 等）：
        //     有军情 = 被围 || 境内有敌军 || 附近有敌军 || 邻接外势力城（含无主城）
        // 无军情的港关只是"主城伸出去的仓储点"：不放守备 / 军事岗，只留一个**运输岗**
        // （把金 / 粮运往归属主城；资源调度那一侧同样只让无军情港关往主城送货）。
        // ------------------------------------------------------------------

        /// <summary>
        /// 港关的"军情"是否把**邻接外势力城（含无主城）**也算进去。
        /// true（默认）= 贴着敌境的渡口 / 关隘算有军情、照常派人驻守；
        /// false = 只看被围 / 境内有敌军 / 附近有敌军。
        /// </summary>
        public bool portGateMilitaryNeighbor = true;

        /// <summary>无军情港关保留的运输岗人数（把金 / 粮运往主城；0 = 一个都不留）</summary>
        public int portGateQuietTransportSeat = 1;

        // ==================== 特技组合搭配（第一步） ====================

        /// <summary>
        /// 候选能带来"本城驻军还没有的特技"时的加分（只作用于军事 / 守备岗）。
        /// 目的：避免一座城堆满同类特技、无法组成互补的多人队伍。
        /// 0 = 关闭。匹配分本身量级约 1~2，0.35 已有明显区分度。
        /// </summary>
        public float featureNoveltyBonus = 0.35f;
        /// <summary>是否把港关纳入编制（关闭则回退旧行为：港关不接收人）</summary>
        public bool enablePortGateDistribute = true;

        /// <summary>
        /// **推荐队伍加成**：候选是某支推荐队伍的成员（固定武将命中，或持有该队伍的固定特技），
        /// 而目标城的圈层正好是这支队伍要去的圈层（`ring` 匹配）→ 加分。
        ///
        /// 目的：让"该上前线的人"被优先调到前线，配合 AI 出征的推荐队伍组队
        /// （否则人可能被后方城的开发岗截走，前线组不起队伍）。
        /// 只作用于军事 / 守备岗；0 = 关闭。匹配分本身量级约 1~2，0.25 已有区分度。
        /// </summary>
        public float recommendedTeamBonus = 0.25f;

        // ==================== 军事岗位：按"预想兵力"（抗抖动） ====================

        /// <summary>
        /// 预想兵力的容量部分：`兵力上限 × 该比例`。
        /// 军事编制用它而不是"当前兵力"，避免被 出征 / 灾害 / 被攻击 带得乱跳。
        /// </summary>
        public float troopExpectFill = 0.8f;

        /// <summary>兵力观察值的慢速均值系数（每回合），用于长期修正预想兵力</summary>
        public float troopObserveAlpha = 0.25f;

        /// <summary>当前兵力高于预想值时采用当前值（只上不下）</summary>
        public bool useCurrentTroopsWhenHigher = true;

        /// <summary>预想兵力是否按经济状况（粮草 / 金钱）修正</summary>
        public bool troopEcoAdjust = true;

        /// <summary>经济系数基数（粮钱全空时的最低系数来源）</summary>
        public float troopEcoBase = 0.5f;

        /// <summary>粮草填充率对经济系数的权重</summary>
        public float troopEcoFoodWeight = 0.3f;

        /// <summary>金钱填充率对经济系数的权重</summary>
        public float troopEcoGoldWeight = 0.2f;

        /// <summary>经济系数下限（再穷也保留的这个比例）</summary>
        public float troopEcoFloor = 0.4f;

        /// <summary>经济系数的慢速均值系数（每回合），避免灾害/围城造成编制抖动</summary>
        public float troopEcoAlpha = 0.25f;

        // ==================== 预想资源值（资金 / 粮草 / 兵装） ====================

        /// <summary>预想资金的容量比：`金库上限 × 该值` 作为下限基线（再与长期观察值取大）</summary>
        public float goldExpectFill = 0.7f;

        /// <summary>预想粮草的容量比：`粮仓上限 × 该值` 作为下限基线</summary>
        public float foodExpectFill = 0.7f;

        /// <summary>资源观察值的慢速均值系数（每回合）—— 运输搬走粮草、赏赐花掉金钱都不会让编制抖动</summary>
        public float resourceObserveAlpha = 0.25f;

        // ==================== 开发修正（离边境距离 + 资源富裕度） ====================

        /// <summary>
        /// 离边境距离系数表，下标 = 圈层 `borderLine`（0 = 边境），超出取最后一项。
        /// 越靠近边境越不适合大搞内政（资源优先军需、随时可能失守），越纵深越适合发展。
        /// </summary>
        public float[] devRingFactor = new float[] { 0.5f, 0.8f, 1.0f, 1.15f };

        /// <summary>资源富裕度对开发修正的贡献下限（穷城保留的比例）</summary>
        public float devResourceBase = 0.7f;

        /// <summary>开发修正下限</summary>
        public float devModMin = 0.3f;

        /// <summary>开发修正上限</summary>
        public float devModMax = 1.5f;

        // ==================== 军事岗位：主将带兵上限 ====================

        /// <summary>
        /// 无太守时每 1 名军事将领分摊的兵力；有太守时用 <c>city.Leader.TroopsLimit</c>
        /// （主将带兵上限，见 Troop.cs:1075）。军事岗位不会超过"能编成的队数"。
        /// </summary>
        public int defaultTroopsPerGeneral = 10000;

        /// <summary>
        /// 军事 / 守备岗的**能力下限**：`统率 + 武力` 之和低于此值的人不派去带兵（0 = 不限制）。
        ///
        /// 口径是两项原始属性之和（0~200）。默认 80 ≈ 两项平均 40，用来挡住"纯文官 / 文弱武将当将军"
        /// （例如把大乔派去军事岗）。**硬性军事岗不受此限制** —— 前线必备岗宁可要个弱将也不能空缺。
        /// </summary>
        public int militaryMinAbility = 80;

        /// <summary>前线城即使没兵也保留的军事岗位（"前线人数一定要保证"）</summary>
        public int minMilitarySeatAtBorder = 3;

        /// <summary>
        /// **非港关城市的最低人数**（统一口径，默认 3）。
        ///
        /// 同一数值作用于四件事，保证"任何非港关城市都不会少于 3 人"：
        ///   ① 源城闸门：非港关城被抽到 ≤ 此值就拒绝调出（<see cref="DeploymentExecutor"/>）；
        ///   ② 编制保底：人数低于此值的城，军事岗编足此数（哪怕是弱将，也不能空城）；
        ///   ③ 硬性岗：人数低于此值的城，第一个军事岗是**硬性岗** ——
        ///      绕过 <c>militaryMinAbility</c>(80) 的能力下限，否则 0~2 人的城
        ///      会因为"池里只剩文官"而永远收不到人（报告里表现为"池中无可用人选"）；
        ///   ④ 排序优先：这类城的岗位排在最前拿人（人手极缺比"更靠边境"更急）。
        /// 港关不在此列（它们的保底是 <see cref="minPortGateGuard"/>）。
        /// </summary>
        public int minCityPersons = 3;

        /// <summary>
        /// 最低运转保底（圈层 ≥ <see cref="rearFloorMinRing"/>），按城池等级。
        ///
        /// 默认 **3/3/3/3** —— 与 <see cref="minMilitarySeatAtBorder"/>(前线 3) 统一，
        /// 即"非港关城市一律保底 3 人"（旧默认 2/2/3/3 让小/中城只能保 2 人，
        /// 与"最低 3 人"的口径不一致）。
        ///
        /// 目的：保住"运输"与"战略资源积累"（开发），不让前线把后方抽空。
        /// 同一口径作用于两处：
        ///   ① 编制保底（<see cref="CityEstablishment"/>：后方城的运输/开发席位不低于此值）；
        ///   ② 调动闸门（<see cref="DeploymentExecutor"/>：源城为后方城时，不得抽到低于此值）。
        /// </summary>
        public int[] minSeatAtRearByLevel = new int[] { 3, 3, 3, 3 };

        /// <summary>
        /// 最低运转保底生效的圈层下限：圈层 ≥ 此值时受 <see cref="minSeatAtRearByLevel"/> 约束。
        /// 1 = 次前线 + 后方（边境圈层 0 由 <see cref="minMilitarySeatAtBorder"/> 军事保底负责）。
        /// </summary>
        public int rearFloorMinRing = 1;

        /// <summary>是否用"人均城数"动态推算后方保底（关闭则用 minSeatAtRearByLevel 固定值）</summary>
        public bool enableDynamicRearFloor = true;

        /// <summary>
        /// 后方城应保留的"人均城数"比例：`人均城数 × 此值` 作为动态保底目标。
        /// 例：人均 21.8 × 0.3 ≈ 6.5 → 巨城保底 7。
        /// </summary>
        public float rearShareRatio = 0.3f;

        /// <summary>后方保底的等级上限（防止后方囤人：人再多也不超过此值）</summary>
        public int[] maxSeatAtRearByLevel = new int[] { 3, 4, 6, 8 };

        // ==================== 开发岗位：按内政建设情况 ====================

        /// <summary>
        /// 每多少个"内政点数"配 1 名开发将。
        /// 内政点数 = 城内内政设施（<c>BuildingType.IsIntrior</c>）的等级之和（等级≤0 按 1 计）。
        /// </summary>
        public float interiorPointsPerDevSeat = 3f;

        /// <summary>是否要求城内有内政设施才给"缺口加成"（无内政地 = 无处开发）</summary>
        public bool devSeatNeedsInterior = true;

        /// <summary>开发缺口达低档时追加的开发岗位</summary>
        public int devGapBonusLow = 1;

        /// <summary>开发缺口达高档时追加的开发岗位</summary>
        public int devGapBonusHigh = 2;

        /// <summary>开发缺口低档阈值（0..1）</summary>
        public float devGapLow = 0.35f;

        /// <summary>开发缺口高档阈值（0..1）</summary>
        public float devGapHigh = 0.60f;

        /// <summary>开发岗位数上限（基准值：人力紧张的势力按此保底）</summary>
        public int devSeatMax = 3;

        // ==================== 人力丰裕度（弹性岗位随人力放开） ====================

        /// <summary>
        /// 是否让弹性岗位（开发）上限随"人力丰裕度"放宽。
        /// 丰裕度 = 势力人均城数 / <see cref="staffPerCityBaseline"/>，落在 [1, staffAbundanceMax]。
        /// 例：9 城 196 人 → 21.8 人/城 → ×2.5 → 开发上限 3 → 7；
        /// 28 城 50 人 → 1.8 人/城 → ×1.0 → 维持 3（不误伤人力紧张的势力）。
        /// </summary>
        public bool staffAbundanceScaling = true;

        /// <summary>人力丰裕度基准：人均城数达到此值时，编制按 1.0 倍计</summary>
        public float staffPerCityBaseline = 8f;

        /// <summary>人力丰裕度上限（人多时弹性岗位最多放大的倍数）</summary>
        public float staffAbundanceMax = 2.5f;

        // ==================== ① 各类弹性岗位的独立上限（决策：比例不同） ====================

        /// <summary>训练岗位上限（游戏内训练最多 3 人）</summary>
        public int trainSeatMax = 3;

        /// <summary>搜索岗位上限（最多 5 人）</summary>
        public int searchSeatMax = 5;

        /// <summary>每多少名待发现/在野人才配 1 名搜索将（决定搜索岗人数）</summary>
        public float hiddenPerSearchSeat = 25f;

        /// <summary>后勤岗位上限（治安 1 + 粮草 1），最多 2 人</summary>
        public int logisticsSeatMax = 2;

        /// <summary>是否生成"登用"岗位（招揽在野武将；与"搜索"是两个独立命令）</summary>
        public bool recruitPersonSlotEnabled = true;

        /// <summary>每多少名**在野**武将配 1 名登用将（决定登用岗人数）</summary>
        public float wildPerRecruitSeat = 8f;

        /// <summary>登用岗位上限</summary>
        public int recruitPersonSeatMax = 5;

        // ==================== 前期加成：开局 N 回合内优先登用 ====================

        /// <summary>前期窗口：开局前 N 个势力回合内启用"优先登用"加成</summary>
        public int earlyGameTurns = 10;

        /// <summary>城内"在野武将"达到此数，才触发前期登用加成（"在野武将多"的判据）</summary>
        public int earlyRecruitWildThreshold = 4;

        /// <summary>
        /// 前期优先登用时的登用岗优先级（数值越小越先拿人）。
        /// 平时是 9（排在后勤之后），前期提到 3（紧随军备，早于训练/搜索/运输/开发/后勤）。
        /// </summary>
        public int earlyRecruitPriority = 3;

        /// <summary>
        /// 前期优先登用时的"每多少名在野配 1 名登用将"。
        /// 平时 wildPerRecruitSeat = 8，前期收紧到 4 → 同城登用人手翻倍。
        /// </summary>
        public float earlyWildPerRecruitSeat = 4f;

        /// <summary>运输岗位上限（人力充裕时 2 人，否则 1 人）</summary>
        public int transportSeatMax = 2;

        /// <summary>运输岗位扩容到 2 人所需的人力丰裕度</summary>
        public float transportExtraSeatAbundance = 1.5f;

        // ==================== ② 军事岗位放开 ====================

        /// <summary>
        /// 每队编制兵力（口径：**5000 人一队**）。军事岗位 = 预想兵力 ÷ 此值 = 队数。
        /// 例：9 万兵 → 18 队；再乘 (1 + deputyPerUnit) → 27 人（"前线人数一定要保证"）。
        /// </summary>
        public int troopsPerUnit = 5000;

        /// <summary>
        /// 每队平均副将数（多人队伍：主将 + 副将）。军事岗位 = 队数 × (1 + 此值)。
        /// 例：18 队 × 1.5 = 27 人。0 = 只算主将；0.5~1.0 = 常见多人队伍配置。
        /// </summary>
        public float deputyPerUnit = 0.5f;

        /// <summary>
        /// 军事岗是否**只在"需要用兵"的城**按预想兵力折算队数。
        ///
        /// true（默认）= 只有下列城池按 `预想兵力 ÷ troopsPerUnit × (1 + deputyPerUnit)` 编军事岗：
        ///   · 边境城（圈层 0）—— 随时可能接敌；
        ///   · 次前线（圈层 1）—— 作为**支援前线**的兵力池；
        ///   · 威胁达标的城（threatLevel ≥ threatMidAt，任意圈层）。
        /// 其余（圈层 ≥ 2 且威胁不高）是纯后方，军事岗取 <see cref="rearMilitarySeat"/>（默认 0 = 不设）。
        ///
        /// 为什么：后方兵多 ≠ 需要一堆将军在城里待命（将军是给部队带队用的）。
        /// 旧行为会让"吴 79761 兵 → 24 个军事岗"这类虚高编制把本城武将全锁成"在岗"，吃空抽调池。
        /// false = 旧行为（所有非港关城都按预想兵力膨胀）。
        /// </summary>
        public bool troopSeatOnlyWhenThreatened = true;

        /// <summary>
        /// **纯后方（圈层 ≥ 2）且威胁未达标**的城的军事岗位数，默认 **0 = 不设军事岗**。
        ///
        /// 这类城的人力应该去做常备的运输 / 战略储备（开发），
        /// 而不是被"军事岗"记账占住、在报告里显得很忙却没有产出。
        /// 想让后方城留几位将军待命就把它调大；
        /// 威胁一旦升到 <c>threatMidAt</c>，无论该值多少都会自动改回"按预想兵力折算队数"。
        /// </summary>
        public int rearMilitarySeat = 0;

        // ==================== 军事岗：按"兵力充实度"收缩 ====================

        /// <summary>
        /// 军事岗"免缩放额度"的系数 α：**额度 C = α × 势力人均人数**
        /// （人均 = 势力总人数 ÷ 都市数；<c>Force.CityCount</c> 只数 <c>IsCity()</c>，天然不含港关）。
        ///
        /// 收缩公式（只压**超过额度**的部分，额度内与保底都不动）：
        ///     军事岗 = min(原值, C) + max(0, 原值 − C) × 当前兵力 / 预想兵力
        ///
        /// 【为什么按人均取额度】人力越丰裕，保留越多常备军事岗才合理。
        /// 固定阈值（例如 15）在"人均 29"和"人均 53"的两个存档里力度差一倍；
        /// 相对值则自动适配 —— 人多的势力少砍、人少的势力多砍。
        ///
        /// 取值参考：0.3 ≈ 人均 29 的势力给 9 个免缩放额度（本次采用）；
        /// 0 = 关闭本收缩（回到旧行为，"预想兵力"决定全部军事岗）。
        /// </summary>
        public float militarySeatFillShare = 0.3f;

        /// <summary>
        /// 是否叠加"本城人力因子"：免缩放额度再乘 <c>min(1, 本城在册人数 / 人均人数)</c>。
        ///
        /// 作用：治"空城占编制" —— 一座没有（或极少）在册武将的城，
        /// 即使兵力上限很大，也不该编出几十个没人可派的将军岗。
        /// 因子为 0 时额度归零，该城军事岗整段按"当前兵力"折算。
        /// </summary>
        public bool militarySeatFillUseLocalFactor = true;

        /// <summary>
        /// "当前兵力 / 预想兵力"的下限（0 = 用原始比例，不做下限）。
        ///
        /// 比例直接反映战损，会带来抖动：前线刚被打残 → 军事岗立刻缩水 →
        /// 将军被调走（还要撞 2 回合调动防抖）→ 增援回来兵力恢复 → 又要调回来。
        /// 想让编制更稳就设成 0.5 之类（牺牲一部分收缩力度换稳定性）。
        /// </summary>
        public float militarySeatFillRatioFloor = 0f;

        /// <summary>
        /// 军事岗位硬顶。放开后军事岗取三者较大值：
        ///   ① 按主将带兵上限折算的队数（例：永安 97k 兵 / 7040 → 14 队）；
        ///   ② 基础编制 + 圈层 + 威胁；
        ///   ③ 前线保底。
        /// 不再被"基础编制 levelSeat"压死 —— 这是"前线武将太少"的根因。
        /// </summary>
        public int militarySeatHardMax = 60;

        // ==================== ④ 前线有权与内陆交换武将 ====================

        /// <summary>跨圈层调动成本的方向系数：调向**更前线**（ring 变小）时乘此值（便宜 = 鼓励前送）</summary>
        public float frontwardCostFactor = 0.25f;

        /// <summary>跨圈层调动成本的方向系数：调向**更后方**（ring 变大）时乘此值（贵 = 抑制回撤）</summary>
        public float backwardCostFactor = 1.5f;

        // ==================== 征兵 ====================

        /// <summary>是否生成征兵岗位</summary>
        public bool recruitSlotEnabled = true;
        /// <summary>兵力低于上限的该比例时，需要征兵</summary>
        public float recruitFillTarget = 0.8f;
        /// <summary>兵力低于上限的该比例时，视为严重缺兵（+1 个征兵岗）</summary>
        public float recruitSevereFill = 0.5f;

        // ==================== 军备（兵装 / 器械 / 船） ====================

        /// <summary>是否生成军备岗位</summary>
        public bool armamentSlotEnabled = true;
        /// <summary>兵装（枪戟弩马）总量 ÷ 兵力 达到该比例即视为充足</summary>
        public float armamentCoverTarget = 1.0f;
        /// <summary>器械（冲车 / 投石）目标数量（有工坊时才要求）</summary>
        public int machineCountTarget = 2;
        /// <summary>是否生成船岗（有船厂且是港口/有附属港口时才要求）</summary>
        public bool boatSlotEnabled = true;
        /// <summary>兵装岗是否要求城内有锻冶 / 马厩（无设施则无处打造）</summary>
        public bool armamentNeedsFacility = true;

        // ==================== 士气（训练） ====================

        /// <summary>是否生成训练岗位</summary>
        public bool trainSlotEnabled = true;
        /// <summary>士气低于上限的该比例时，需要训练</summary>
        public float moraleFillTarget = 0.9f;

        // ==================== 搜索 ====================

        /// <summary>是否生成搜索岗位</summary>
        public bool searchSlotEnabled = true;

        // ==================== 运输 ====================

        /// <summary>是否生成运输岗位</summary>
        public bool transportSlotEnabled = true;
        /// <summary>运输源城所需最低兵力</summary>
        public int transportMinTroops = 10000;
        /// <summary>运输源城所需最低粮草</summary>
        public int transportMinFood = 20000;

        // ==================== 后勤 ====================

        /// <summary>治安低于该比例时生成后勤岗（后方城）</summary>
        public float logisticsSecurityTarget = 0.7f;
        /// <summary>粮草低于该比例时生成后勤岗（后方城）</summary>
        public float logisticsFoodFillTarget = 0.3f;

        // ==================== 执行（Phase B） ====================

        /// <summary>禁止重复调动：同一武将在 debounceTurns 个势力回合内不再被调走</summary>
        public int debounceTurns = 2;

        /// <summary>
        /// 同一座城每回合最多接收多少名外调武将（基准额度）。
        ///
        /// 默认 10 —— 这是"缺口最大的城能不能一回合补完"的主要瓶颈：
        /// 调出端已经放宽（源城可借净富余的一半、全局额度按在册人数的 25% 缩放），
        /// 若接收端还是 2~4 人/回合，一座缺 9~10 个岗的城仍要磨 3~5 回合，
        /// 而这期间"源城净富余"还在变（人一忙，可借额度就缩回去了）。
        ///
        /// 前线 / 高威胁城在这之上再叠加 <see cref="frontlineExtraReceiveSeat"/>（默认 12 人/回合）。
        /// 真正的总量约束是全局额度（<see cref="maxTransferPerTurn"/>）与源城净富余 ——
        /// 接收额度只是防止"一次性把某座城塞满、别的城排队"。
        /// </summary>
        public int maxTransferPerCityPerTurn = 10;

        /// <summary>同一座城每回合最多向外调出多少名武将（基准额度）</summary>
        public int maxTransferFromCityPerTurn = 1;

        /// <summary>
        /// 外调最低匹配分门槛：候选的匹配分低于此值时**不做跨城调动**。
        ///
        /// 匹配分 = 能力适配 − 行程成本 − 跨圈层成本（<c>DeploymentSolver.Score</c>），
        /// 负分意味着"搬过去反而更差"（白耗行程、打断武将现有内政、还触发防抖）。
        /// 0 = 不设门槛（旧行为）；硬性岗位（<c>Post.required</c>）不受此门槛约束，避免前线必备岗空缺。
        /// </summary>
        public float minTransferScore = 0.1f;

        /// <summary>
        /// 抢占"本城在岗"人选的额外成本。
        ///
        /// 外调挑人分两轮：第一轮只看**真正的机动人力**（没被"在岗"记账占用的人），
        /// 只有第一轮挑不出达标人选时，才进入第二轮"抢占"（分数扣本值）。
        /// 这样人力优先来自真空闲，只有在明显更划算时才去动别人城里的"在岗"记账。
        /// 0 = 不区分（抢占与机动人力同权）。
        /// </summary>
        public float stealLocalCost = 0.4f;

        /// <summary>
        /// 势力每回合最多执行的跨城调动**总人数**（全局额度）——这是**下限**：
        /// 实际额度取 <c>max(本值, 在册总人数 × maxTransferPerTurnRatio)</c>，
        /// 即"势力越大、一次能调整的幅度越大"，小势力则保持本值不被缩小。
        /// 目的：一次调整幅度可控；≤0 = 不限制（此时也不做缩放）。与"每城额度"是"与"关系。
        /// </summary>
        public int maxTransferPerTurn = 12;

        /// <summary>
        /// 全局调动额度按**在册总人数**缩放的比例（见 <see cref="maxTransferPerTurn"/>）。
        ///
        /// 默认 0.25 —— 也就是"100 人至少能调动 25 人"：兵力 / 人口上百的势力，
        /// 一回合 12 人的固定额度会把"全势力上百个岗位缺口"卡上几十回合；
        /// 按比例缩放后，调动节奏与势力规模自洽（在册人数含在部队 / 有任务的人，
        /// 即"势力的总盘子"，不因当期忙闲忽大忽小）。
        /// ≤0 = 不缩放（只按 <see cref="maxTransferPerTurn"/>）。
        /// </summary>
        public float maxTransferPerTurnRatio = 0.25f;

        /// <summary>
        /// 前线 / 高威胁城每回合**额外**的接收额度（叠加在 <see cref="maxTransferPerCityPerTurn"/> 上）。
        /// 让"缺口最大、最吃紧"的城优先补齐，而不是被后方城的小缺口平均分摊掉额度。
        /// </summary>
        public int frontlineExtraReceiveSeat = 2;

        /// <summary>
        /// 源城调出额度的**斜率**：每多少个"净富余"多允许调出 1 人。
        ///
        /// 净富余 = 本城空闲人数 − 本城未填岗位数（见 <c>DeploymentSolver.SendLimitFor</c>），
        /// 也就是"本城自己的缺口填满之后，多出来的人才算真富余"。
        ///
        /// 【为什么不用固定阈值】旧实现是"空闲人数 ≥ 阈值(30) 才算超载，否则每城只准调 1 人"，
        /// 而正常存档里单城空闲很少到 30（例：曹操存档最大 25）→ 每座城恒等于基准额度，
        /// 洛阳"空闲 23 / 空缺 2"也只能送 1 人，全势力上百个缺口被这个额度卡死。
        /// 相对判据在"汉中空闲 301"与"洛阳空闲 23"两种存档里都成立。
        ///
        /// 默认 2 —— 额度 = 净富余 ÷ 2 + 1，即**一次最多借走净富余的一半**：
        /// 净富余 1 → 1 人；2~3 → 2 人；8 → 5 人；20 → 11 人。
        /// （净富余 1 也能借 1 人，是因为公式的 +1 基准；不会把一座城抽空。）
        /// </summary>
        public int sendSurplusPerSeat = 2;

        /// <summary>
        /// 源城每回合调出额度的**硬顶**（<see cref="sendSurplusPerSeat"/> 算出的额度不超过它）。
        ///
        /// 默认 **0 = 不限制** —— 因为"一次最多借净富余的一半"本身就是安全阀，
        /// 而 5 的硬顶会把"一半"直接截断（净富余 20 时想要 11 人、只放 5 人）。
        /// 真正兜住突发的是另外两道：目标城的接收额度（<see cref="maxTransferPerCityPerTurn"/>）
        /// 与全局额度（<see cref="maxTransferPerTurn"/>）。
        /// 想要"单城每次最多动 N 人"就把本值配上。
        /// </summary>
        public int maxSendPerCityPerTurn = 0;

        /// <summary>港关至少保留的守备人数（低于此值不允许把人调走）</summary>
        public int minPortGateGuard = 1;

        /// <summary>是否启用增量编制：城况未变的城复用上回合岗位表（失效驱动）</summary>
        public bool enableIncrementalPlan = true;

        /// <summary>
        /// 编制总量上限（× 势力总人数）。岗位总数超过它时，**按城轮转裁撤**：
        /// 每轮每座城各裁掉 1 个"该城最不优先"的岗位，直到削到预算内；
        /// 硬性岗（`Post.required`：每城第一个军事岗 / 港关守备）**永不裁撤**，
        /// 所以每座城至少保住 1 个军事岗、每个港关保住守备。
        ///
        /// 为什么轮转而不是"整体排序后截尾"：后者等于让 city id 当最后的裁决 ——
        /// 同样在前线、同样兵力的城，id 排最后的会被砍到只剩硬性岗（报告里的"1 人守城"）。
        /// 总量不足时应该"各城等比缩减"，不该由 id 决定生死。
        ///
        /// 取值参考：0.8 = 编制约为势力人力的八成（偏紧，会明显裁撤）；
        /// 1.4 = 允许编制略多于人力，空缺席位属正常。
        /// </summary>
        public float maxPostsPerPerson = 1.1f;


        // ==================== 求解 ====================

        /// <summary>单城岗位数占势力总人数比例上限</summary>
        public float maxSeatSharePerCity = 0.4f;
        /// <summary>跨圈层调动额外成本</summary>
        public float crossRingCost = 1.5f;
        /// <summary>行程成本系数（每回合位移折算）</summary>
        public float costPerTurn = 1.0f;
        /// <summary>一回合多少天（用于把 DistanceDays 折成回合）</summary>
        public int turnDays = 10;
        // ==================== 岗位权重 ====================

        /// <summary>军事岗位权重</summary>
        public PositionWeights military = new PositionWeights
        {
            command = 1.0f, strength = 0.8f, intelligence = 0.6f, politics = 0.1f, glamour = 0.1f, transport = 0.0f
        };
        /// <summary>征兵岗位权重（魅力为主）</summary>
        public PositionWeights recruitTroops = new PositionWeights
        {
            command = 0.4f, strength = 0.2f, intelligence = 0.3f, politics = 0.4f, glamour = 1.0f, transport = 0.0f
        };
        /// <summary>军备岗位权重（智力 / 政治为主）</summary>
        public PositionWeights armament = new PositionWeights
        {
            command = 0.2f, strength = 0.2f, intelligence = 0.8f, politics = 0.8f, glamour = 0.1f, transport = 0.0f
        };
        /// <summary>训练岗位权重（统率为主）</summary>
        public PositionWeights trainTroops = new PositionWeights
        {
            command = 1.0f, strength = 0.6f, intelligence = 0.3f, politics = 0.2f, glamour = 0.2f, transport = 0.0f
        };
        /// <summary>搜索岗位权重（政治 / 智力 / 魅力）</summary>
        public PositionWeights search = new PositionWeights
        {
            command = 0.1f, strength = 0.0f, intelligence = 0.6f, politics = 1.0f, glamour = 0.6f, transport = 0.0f
        };
        /// <summary>开发岗位权重</summary>
        public PositionWeights develop = new PositionWeights
        {
            command = 0.0f, strength = 0.0f, intelligence = 0.6f, politics = 1.2f, glamour = 0.2f, transport = 0.0f
        };
        /// <summary>后勤岗位权重</summary>
        public PositionWeights logistics = new PositionWeights
        {
            command = 0.2f, strength = 0.0f, intelligence = 0.3f, politics = 0.8f, glamour = 0.6f, transport = 0.4f
        };
        /// <summary>运输岗位权重</summary>
        public PositionWeights transport = new PositionWeights
        {
            command = 0.2f, strength = 0.0f, intelligence = 0.2f, politics = 0.4f, glamour = 0.3f, transport = 1.0f
        };
        /// <summary>登用岗位权重（登用看魅力/政治 —— CityRecruit 的候选排序就是 SortByGlamour）</summary>
        public PositionWeights recruitPerson = new PositionWeights
        {
            command = 0.0f, strength = 0.0f, intelligence = 0.6f, politics = 0.8f, glamour = 1.2f, transport = 0.0f
        };
        /// <summary>港关守备岗位权重</summary>
        public PositionWeights garrison = new PositionWeights
        {
            command = 0.9f, strength = 0.8f, intelligence = 0.5f, politics = 0.1f, glamour = 0.1f, transport = 0.0f
        };

        /// <summary>按岗位类型取权重。</summary>
        public PostFit GetFit(PostKind kind)
        {
            switch (kind)
            {
                case PostKind.RecruitTroops: return recruitTroops.ToFit();
                case PostKind.Armament: return armament.ToFit();
                case PostKind.TrainTroops: return trainTroops.ToFit();
                case PostKind.Search: return search.ToFit();
                case PostKind.Develop: return develop.ToFit();
                case PostKind.Logistics: return logistics.ToFit();
                case PostKind.RecruitPerson: return recruitPerson.ToFit();
                case PostKind.Transport: return transport.ToFit();
                case PostKind.Garrison: return garrison.ToFit();
                default: return military.ToFit();
            }
        }
    }
}
