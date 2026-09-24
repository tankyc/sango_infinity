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

        // ==================== 特技组合搭配（第一步） ====================

        /// <summary>
        /// 候选能带来"本城驻军还没有的特技"时的加分（只作用于军事 / 守备岗）。
        /// 目的：避免一座城堆满同类特技、无法组成互补的多人队伍。
        /// 0 = 关闭。匹配分本身量级约 1~2，0.35 已有明显区分度。
        /// </summary>
        public float featureNoveltyBonus = 0.35f;
        /// <summary>是否把港关纳入编制（关闭则回退旧行为：港关不接收人）</summary>
        public bool enablePortGateDistribute = true;

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

        /// <summary>每支部队配几名将（1.0 = 一将一队；&gt;1 表示允许副将分摊）</summary>
        public float generalsPerTroop = 1.0f;

        /// <summary>前线城即使没兵也保留的军事岗位（"前线人数一定要保证"）</summary>
        public int minMilitarySeatAtBorder = 3;

        /// <summary>
        /// 最低运转保底（圈层 ≥ <see cref="rearFloorMinRing"/>），按城池等级：**小/中 2、大/巨 3**。
        /// 目的：保住"运输"与"战略资源积累"（开发），不让前线把后方抽空。
        /// 同一口径作用于两处：
        ///   ① 编制保底（<see cref="CityEstablishment"/>：后方城的运输/开发席位不低于此值）；
        ///   ② 调动闸门（<see cref="DeploymentExecutor"/>：源城为后方城时，不得抽到低于此值）。
        /// </summary>
        public int[] minSeatAtRearByLevel = new int[] { 2, 2, 3, 3 };

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

        /// <summary>同一座城每回合最多接收多少名外调武将</summary>
        public int maxTransferPerCityPerTurn = 2;

        /// <summary>同一座城每回合最多向外调出多少名武将</summary>
        public int maxTransferFromCityPerTurn = 1;

        /// <summary>港关至少保留的守备人数（低于此值不允许把人调走）</summary>
        public int minPortGateGuard = 1;

        /// <summary>是否启用增量编制：城况未变的城复用上回合岗位表（失效驱动）</summary>
        public bool enableIncrementalPlan = true;

        /// <summary>
        /// 编制总量上限（× 势力总人数）。岗位总数超过它时，按"硬性 → 优先级 → 军事岗边境优先"
        /// 排序**裁掉尾部**（最先被裁的是深后方的后勤/开发/运输岗位）。
        /// 军事放开后单城军事岗可达 20~30，1.0 会把内政岗全部挤掉，
        /// 故默认 1.4（允许编制略多于人力，空缺席位属正常）。
        /// </summary>
        public float maxPostsPerPerson = 1.4f;


        // ==================== 求解 ====================

        /// <summary>单城岗位数占势力总人数比例上限</summary>
        public float maxSeatSharePerCity = 0.4f;
        /// <summary>跨圈层调动额外成本</summary>
        public float crossRingCost = 1.5f;
        /// <summary>行程成本系数（每回合位移折算）</summary>
        public float costPerTurn = 1.0f;
        /// <summary>一回合多少天（用于把 DistanceDays 折成回合）</summary>
        public int turnDays = 10;
        /// <summary>军事能力阈值（旧硬编码 350，这里参数化）</summary>
        public int milFitThreshold = 350;
        /// <summary>圈层 → 军事槽位权重基数（前线）</summary>
        public float ringWeightBorder = 3f;
        /// <summary>次前线军事槽位权重基数</summary>
        public float ringWeightSubFront = 2f;
        /// <summary>后方军事槽位权重基数</summary>
        public float ringWeightRear = 1f;
        /// <summary>势力个性对槽位权重的影响幅度</summary>
        public float personalityWeightScale = 0.3f;

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
