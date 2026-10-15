/*
 * 文件名：BuildingPlanWeights.cs
 * 描述：AI 内政建筑规划的全部权重与阈值配置。
 *       可通过 Data/Common/AIConfig.json 中的 "buildingPlan" 节点进行部分覆盖
 *       （反序列化会先执行字段初始化器，因此未在 JSON 中出现的字段保持默认值；
 *         baseRatio / maxPerCity 这类字典按"逐键覆盖"合并，只写要改的项即可）。
 */

using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// AI 内政建筑规划的权重配置。
    ///
    /// 【设计口径】用「逐格贪心 + 边际递减」代替原来的固定建造队列
    /// （旧实现是 <c>CityAI.CityBuildingTemplate</c> 那一组 int[][]，坑位与内政地索引一一对应）：
    ///   · 每种建筑有一个**份额权重**（<see cref="baseRatio"/>），体量由它决定；
    ///   · 每次要建一格时，取「权重 ÷ (已建数 + 1)」最大的那种 —— 已建的越多越不划算，
    ///     于是各类建筑自然按权重比例铺开，**不需要事先算出总格数**，也不怕城规模变化或格子被拆；
    ///   · 权重乘上各种修正（港口 / 性格 / 态势）后比例就会随局势漂移：
    ///     粮荒时抬高农田权重，几格之后农田占比自然上去 —— 这就是"动态调整产出资源建筑比例"的落点。
    ///
    /// 【性能口径】求解是每城每格一次的小规模计算（候选建筑 20 种上下），直接遍历即可，
    /// 不引入跨回合缓存，也就没有缓存失效问题。
    /// </summary>
    public class BuildingPlanWeights
    {
        // ==================== 总开关 / 观察 ====================

        /// <summary>总开关：关闭后本规划器完全不介入，AI 回退到旧的固定模板</summary>
        public bool enabled = true;

        /// <summary>
        /// 影子模式：只求解、不真正建造（调用方会视为"没有目标"而回退旧模板）。
        /// 调参时用来看"AI 会怎么建"。
        /// </summary>
        public bool shadowOnly = false;

        /// <summary>是否输出规划报告（仅编辑器 / 开发包生效）</summary>
        public bool logEnabled = true;

        /// <summary>报告的势力 id（-1 = 全部；0 = 只报告玩家势力；&gt;0 = 只报告该势力）</summary>
        public int logForceId = -1;

        /// <summary>报告间隔（按城池回合数计；0 = 每城每次）</summary>
        public int logIntervalTurns = 30;

        // ==================== 建筑份额权重 ====================
        //
        // key = BuildingKindType 的整数值，value = 份额权重（相对值，不必归一化）。
        // 未列出的建筑按 defaultRatio 计。
        //
        // 【为什么军事府(42) / 人才府(43) 的权重可以留在这里】
        //   它们只在**工作制内政**下 canBuild = true（由 BuildingWorking.ScenarioInit/ScenarioClear
        //   翻转），经典内政下会被 BuildingType.CanBuildInteriorAt 直接过滤掉 ——
        //   所以保留权重是安全的：切到经典内政时它们自动出局，份额自然让给别的建筑，
        //   不需要在这里按内政方式分档。

        /// <summary>各建筑的基础份额权重（未列出的用 <see cref="defaultRatio"/>）</summary>
        public Dictionary<int, int> baseRatio = new Dictionary<int, int>
        {
            { 32, 10 },   // 市场（资金）
            { 33, 12 },   // 农田（粮草）
            { 34, 6 },    // 兵舍（征兵）
            { 35, 6 },    // 锻冶厂（兵装）
            { 36, 4 },    // 厩舍（军马）
            { 37, 3 },    // 工房（器械）
            { 38, 4 },    // 造船厂（舰船）
            { 39, 3 },    // 造币厂（资金，每城一座）
            { 40, 3 },    // 谷仓（粮草，每城一座）
            { 42, 4 },    // 军事府（巡查，仅工作制内政）
            { 43, 3 },    // 人才府（搜索，仅工作制内政）
            { 47, 2 },    // 大市场（需大都市）
            { 48, 3 },    // 鱼市（需港口）
            { 50, 4 },    // 军屯农（粮草，每城一座）
        };

        /// <summary>未在 <see cref="baseRatio"/> 中列出的建筑的默认份额权重（0 = 默认不新建）</summary>
        public int defaultRatio = 0;

        /// <summary>
        /// 永不作为新建目标的建筑。
        /// 默认排除黑市(49)：它没有任何数量限制（IsValid 拦不住），但会在**每年 1 月被强制拆除**
        /// （见 <c>City.OnYearStart</c>），AI 建它等于白扔一次建造成本。
        /// </summary>
        public int[] excludedKinds = new int[] { 49 };

        // ==================== 港口修正 ====================
        //
        // 【判定口径】用"本城是否**拥有下属港口**"（city.portList），**不能用 city.IsPort()** ——
        //   港关本身不参与都市内政：BuildingWorking / ClassicsCityWorking.OnCityAIPrepare 开头
        //   都有 if (!city.IsCity()) return;，所以"本城就是港口"这条分支永远不会命中。
        //   真正有意义的是"这个都市有没有港口可供水军部署"。

        /// <summary>受港口修正影响的建筑（造船厂、鱼市 —— 都需要水域才用得上）</summary>
        public int[] portBiasKinds = new int[] { 38, 48 };

        /// <summary>本城拥有下属港口时，港口类建筑的权重倍数</summary>
        public float portBias = 2.0f;

        /// <summary>本城没有下属港口时，港口类建筑的权重倍数（造出来也没水域可用，故压低）</summary>
        public float nonPortBias = 0.15f;

        // ==================== 性格修正 ====================
        //
        // 【数据来源】直接复用 Personality 已有的 13 项 domestic*Scale（默认 100 = 平均值）。
        //   那些字段本来就是"这个性格偏好哪类内政"的表达（农业 / 商业 / 治安 / 生产兵装 …），
        //   不需要为建筑再造一套性格数据。
        // 映射关系（哪个建筑用哪个字段）是**结构**，写在 BuildingPlanner.PersonalityScaleOf 里；
        // 这里只放数值参数。

        /// <summary>是否按性格修正建筑份额</summary>
        public bool personalityEnabled = true;

        /// <summary>太守（本城 Leader）性格的权重</summary>
        public float leaderPersonalityWeight = 0.6f;

        /// <summary>主公（势力君主）性格的权重</summary>
        public float governorPersonalityWeight = 0.4f;

        /// <summary>
        /// 性格偏离的放大倍数。domestic*Scale 的取值通常只在 80~120 之间，
        /// 直接当乘数（±20%）对建筑比例的影响太弱，故按本倍数放大后再限幅。
        /// </summary>
        public float personalitySensitivity = 2.0f;

        /// <summary>性格修正后的乘数下限（防止极端性格把某一类压到几乎不建）</summary>
        public float personalityFactorMin = 0.5f;

        /// <summary>性格修正后的乘数上限</summary>
        public float personalityFactorMax = 2.0f;

        // ==================== 势力资源水位修正 ====================
        //
        // 【要解决的问题】建筑规划是逐城求解的，"该多建农田还是市场"却取决于**整个势力**的
        //   钱粮余缺 —— 只看本城会得到局部最优、全局很糟的结论。
        //   数据取自 ForceBuildState（每回合每势力算一次，O(城数)/回合）。
        //
        // 【映射】农田(33)/谷仓(40)/军屯农(50) → 粮草水位；
        //         市场(32)/造币厂(39)/大市场(47)/鱼市(48) → 资金水位；
        //         锻冶厂(35)/厩舍(36) → 兵装水位。

        /// <summary>是否按势力资源水位修正建筑份额</summary>
        public bool supplyEnabled = true;

        /// <summary>水位低于该值（%）视为"紧缺"→ 抬高该类产出建筑的权重</summary>
        public int supplyLowFill = 30;

        /// <summary>水位高于该值（%）视为"富余"→ 压低该类产出建筑的权重</summary>
        public int supplyHighFill = 70;

        /// <summary>紧缺时的权重倍数</summary>
        public float supplyLowBias = 1.8f;

        /// <summary>富余时的权重倍数</summary>
        public float supplyHighBias = 0.6f;

        // ==================== 需求驱动 ====================
        //
        // 【为什么不再纯按权重铺开】
        //   · **农田与市场不设数量上限**，只按"能不能满足需求"决定建不建：
        //     粮产要能抵扣城内士兵消耗（每回合结算）、资金产出要覆盖城市消耗
        //     （每月结算：武将俸禄 + 俘虏关押）。绝对富余时一座都不该多建。
        //   · **产出建筑**（锻冶厂 / 厩舍 / 工房 / 造船厂）是"按需临时扩充"的性质：
        //     总数封顶，绝对富余时可以为 0。
        //   年化口径见 <see cref="CityEconomy"/>（一年 36 回合 = 12 月 = 4 季）。

        /// <summary>产出建筑的**总数上限**（锻冶厂 + 厩舍 + 工房 + 造船厂 合计，本城）</summary>
        public int produceBuildingMaxCount = 3;

        /// <summary>哪些建筑算"产出建筑"（受 <see cref="produceBuildingMaxCount"/> 限制）</summary>
        public int[] produceKinds = new int[] { 35, 36, 37, 38 };

        // ---- 每城数量上限 ----
        //
        // 【为什么必须有这道闸】建筑数据里只有**限量型**（limitType / buildNumLimit）才有上限，
        //   而兵舍这类"无限量型"建筑在数据上是 0 —— 若规划器不自己兜底，
        //   它会在"没有别的需求"时被无限堆叠（曾经的实际表现：开局造一堆兵舍）。
        //   所以这里给所有建筑一个默认上限，只把确有必要不限量的（农田 / 市场 / 产出建筑）显式放开。

        /// <summary>
        /// 每城数量上限（**0 = 不限制**）。未列出的用 <see cref="defaultMaxPerCity"/>。
        ///
        /// · 农田(33) / 市场(32)：必须为 0 —— 它们只按"需求缺口"决定数量，不该有硬上限；
        /// · 产出建筑(35/36/37/38)：也为 0 —— 由 <see cref="produceBuildingMaxCount"/> 统一封顶；
        /// · 其余建筑走 <see cref="defaultMaxPerCity"/>（默认 1 座）。
        /// </summary>
        public Dictionary<int, int> maxPerCity = new Dictionary<int, int>
        {
            { 32, 0 },   // 市场：按需求决定座数
            { 33, 0 },   // 农田：按需求决定座数
            { 35, 0 },   // 锻冶厂：由产出建筑总数上限统一限制
            { 36, 0 },   // 厩舍：同上
            { 37, 0 },   // 工房：同上
            { 38, 0 },   // 造船厂：同上
        };

        /// <summary>
        /// 未在 <see cref="maxPerCity"/> 中列出的建筑的默认每城上限。
        /// 默认 1 座 —— 建筑数据未声明限制的那些（如兵舍）靠它兜底，防止无限堆叠。
        /// </summary>
        public int defaultMaxPerCity = 1;

        /// <summary>兵装需求 = 守军兵力 × 该系数（1 兵 1 装为基准）</summary>
        public float armsCoverRatio = 1.0f;

        /// <summary>兵装缺口小于该值时视为"绝对富余"，不再为此扩充产出建筑</summary>
        public int armsNeedMinGap = 1000;

        // ---- 兵器（冲车 / 木兽 / 井阑 / 投石） ----
        //
        // 【注意：兵器 ≠ 兵装】
        //   · 兵装 = 枪 / 戟 / 弩 / 军马，1 兵 1 件，用于组建部队；
        //   · 兵器 = 冲车 / 木兽 / 井阑 / 投石，**按台**计，用于攻城，由工房产出。
        //   两者存在同一个 ItemStore 里但走不同的存取分组
        //   （冲车+木兽 → ItemStoreKindType.Helepolis(6)；井阑+投石 → Catapult(7)）。

        /// <summary>
        /// 后方城池的**兵器持有率** —— 相对**兵器库存上限**的比例。
        ///
        /// 【为什么以库存上限为基准】4 种兵器都带 <c>p1 = 10</c>，
        /// 即库存上限 = 仓库上限 × 10/1000 = **1%**（见 <c>ItemType.TransformLimit</c>）。
        /// 按这个上限的 10%~30% 备货就够用，不需要囤满 —— 囤满反而挤占资金。
        /// </summary>
        public float machineHoldRatioRear = 0.1f;

        /// <summary>边境（前线）城池的兵器持有率 —— 攻城消耗大，留高一档</summary>
        public float machineHoldRatioBorder = 0.3f;

        /// <summary>兵器缺口小于该值时不再为此建工房</summary>
        public int machineNeedMinGap = 1;

        /// <summary>
        /// **有港口**城池的舰船持有率 —— 相对**舰船库存上限**的比例。
        ///
        /// 【口径】舰船（楼船 / 斗舰）与兵器一样带 <c>p1 = 10</c>，
        /// 即库存上限 = 仓库上限 × 10/1000 = 1%。有港口的城要保持高保有率（70%），
        /// 才能随时拉出水军；没港口的城不备船（无水域可用）。
        /// </summary>
        public float boatHoldRatioPort = 0.7f;

        /// <summary>无港口城池的舰船持有率（默认 0：船没地方用）</summary>
        public float boatHoldRatioInland = 0f;

        /// <summary>舰船缺口小于该值时不再为此建造船厂</summary>
        public int boatNeedMinGap = 1;

        // ---- 硬性基础（打底产能） ----
        //
        // 【为什么需要硬性下限】纯按"需求缺口"驱动有一个盲区：粮草刚好够时一座农田都不建，
        //   一旦征兵、部队出征或人口增长，立刻断粮。所以给农田 / 市场各留一个**打底座数**：
        //   哪怕当前已经盈余，也要先把它补齐。

        /// <summary>每城硬性基础农田座数（即使粮草已盈余也要补齐）</summary>
        public int baseFarmCount = 2;

        /// <summary>每城硬性基础市场座数（即使资金已盈余也要补齐）</summary>
        public int baseMarketCount = 2;

        /// <summary>
        /// 硬性基础建筑的优先级（每差一座的得分）。
        /// 必须高于常规缺口分（农田的正常缺口分 = "还差几座"，通常个位数），
        /// 这样"打底"一定排在"按需扩张"之前。
        /// </summary>
        public float baseDemandPriority = 10f;

        /// <summary>
        /// 乘数型建筑（造币厂 → 市场 ×1.5、谷仓 → 农田 ×1.5）要求的**基础建筑最少座数**。
        /// 基础建筑太少时建它等于放大一个空架子，所以先不给分。
        /// </summary>
        public int multiplierMinBase = 2;

        /// <summary>
        /// "非产出、不参与粮金收支"的建筑（兵舍 / 军事府 / 人才府 …）的得分缩放系数。
        ///
        /// 【为什么需要】农田 / 市场的得分是"还差几座"（1 座 ≈ 1.0），而这类建筑走的是份额权重
        /// （兵舍 6、军事府 4 …）—— 量纲不同，会出现"粮荒还差 3 座农田"却被"军事府 4 分"
        /// 抢先建的情况。压到缺口类之下，让硬缺口优先，软需求（治安 / 搜人 / 征兵能力）随后补。
        /// </summary>
        public float minorScoreScale = 0.1f;

        // ---- 建设预留（计入资金需求） ----
        //
        // 【为什么资金需求必须含建设支出】建造与升级都要掏钱（City.JobBuildBuilding /
        //   JobUpgradeBuilding 直接扣 city.gold），但它们**不在这笔"每月固定账"里**。
        //   若不计入，AI 会以为资金有盈余 → 不建市场 → 钱全被自建工程吃光 → 再也升不起级。
        //   这里按"待建空地 + 待升级建筑"估一笔预算，摊成年度需求参与收支判断。

        // ---- 富余扩张（把闲置资金转成产能） ----
        //
        // 【为什么要这条】纯"需求驱动"在前期过于保守：粮 / 金刚好够就一座不建，
        //   资金积压在库里（钱不花等于没有）。所以在**需求已满足、还有空地、资金宽裕**时，
        //   仍允许继续建基础产能 —— 花到资金低于 keep 线就自然停。

        /// <summary>是否允许"需求已满足"时继续用富余资金扩张基础产能</summary>
        public bool expandEnabled = true;

        /// <summary>
        /// 扩张的资金保留线（金）。默认 **0 = 内政地未建满时不保留现金**，全部投入建设：
        /// 空着的内政地本身就是"没做完的投入"，把现金留在库里等于白白浪费发展时间。
        /// 建满之后本条自然失效，钱会攒下来用于升级 / 应急。想留一笔底金就把它调大。
        /// </summary>
        public int expandGoldKeep = 0;

        /// <summary>扩张得分（低于任何缺口分，高于"不建"）</summary>
        public float expandScore = 0.3f;

        /// <summary>
        /// 是否用"**权重随机**"挑选建筑（得分即权重），而不是"取最高分"。
        ///
        /// 【为什么用权重随机】
        ///   ① 取最高分在"得分相同"时会**永远选中遍历在前的那个**（市场 Id=32 总压过农田 33），
        ///      单靠"折半交替"只是打补丁；
        ///   ② 各类建筑的实际数量比例也不等于得分比例。
        ///   改成按权重随机后，**得分比例会自然收敛为数量比例**（农田 0.75 / 市场 0.75 → 各约一半），
        ///   平局问题随之消失，AI 也不会千城一面。
        ///
        /// 代价：行为带随机性 —— 偶尔会选中得分不是最高的那个，但概率与其得分成正比。
        /// 想让决策完全确定（便于复现问题）时把它关掉即可。
        /// </summary>
        public bool useWeightedRandom = true;

        /// <summary>
        /// 农田 / 市场之间的交替折半系数。
        ///
        /// 【为什么必须有】两者在扩张阶段的得分完全相同，而择优逻辑是"严格大于才替换"，
        /// 于是遍历在前的市场(BuildingKindType=32，小于农田的 33)**永远胜出** ——
        /// 实际表现就是市场越建越多、农田停滞。把数量多的一方折半，两者就会交替。
        /// </summary>
        public float capacityAlternateFactor = 0.5f;

        /// <summary>估算每格待建内政地的平均造价（金），用于建设预算</summary>
        public int buildCostEstimate = 300;

        /// <summary>建设预算分摊年数（把一次性造价摊成年度需求）</summary>
        public int buildReserveYears = 2;

        // ---- 兵装紧急扩产（一回合多次产出） ----
        //
        // 【不受行动点约束】Corps.ReduceActionPoint 对非玩家势力**直接 return**（见 Corps.cs），
        //   所以 AI 的行动点是空的、扣不动 —— 这里的次数上限是**为节奏与观感而设**，不是资源约束。
        //   真正的天然约束是"空闲锻冶厂 / 厩舍数"与"空闲武将数"（每人每回合只做一个行为）。

        /// <summary>是否允许兵装紧急扩产（一回合多次产出）</summary>
        public bool armsEmergencyEnabled = true;

        /// <summary>势力兵装水位低于该值（%）视为严重不足，触发扩产</summary>
        public int armsEmergencyArmsFill = 30;

        /// <summary>紧急时一回合额外最多再产几次（总次数 = 1 + 本值）</summary>
        public int armsEmergencyMaxExtra = 3;

        /// <summary>金钱门槛：现有金 ≥ createItemsMinGold × 该系数 才允许扩产</summary>
        public float armsEmergencyGoldFactor = 3f;

        // ==================== 更换（拆旧建新） ====================
        //
        // 【判定口径】拆除**不退钱**（Building.OnFall 只做移除 + 重算产出），所以更换的代价是
        //   "重建期内旧建筑产出的损失"。判定因此不看金钱，而看**需求**：
        //     ① 新建筑必须**确实有需求**（需求强度 ≥ replaceGainRatio）；
        //     ② 旧建筑所属类别必须已经**盈余 / 富余**（没有保留价值）；
        //     ③ 拆掉它之后，粮 / 金收支仍然不亏空
        //        （粮产还要能抵扣士兵消耗、资金产出还要能覆盖城市消耗
        //         —— CityEconomy.CanAffordRemoving）。
        //   三条同时满足才动手；再用"冷却"与"金钱富余门槛"限制频率与财力。

        /// <summary>是否允许 AI 主动拆旧建新</summary>
        public bool replaceEnabled = true;

        /// <summary>同一城池两次更换之间的最小间隔（回合）—— 防止需求轻微波动就反复拆建</summary>
        public int replaceCooldownTurns = 12;

        /// <summary>
        /// 需求强度门槛（单位：座）。新建筑至少要能补足相当于这么多座的需求量才值得拆建，
        /// 避免"只差一点点"就把一座建筑推倒重来（重建期的损失会超过那点收益）。
        /// </summary>
        public float replaceGainRatio = 1.5f;

        /// <summary>金钱富余门槛：现有金 ≥ 新建筑造价 × 该系数 才允许更换</summary>
        public float replaceGoldFactor = 3f;

        /// <summary>
        /// 允许被拆的建筑白名单（**只列无限量型**）。
        /// 限一座型（造币厂 / 谷仓 / 军屯农 / 大市场 / 鱼市 / 军事府 / 人才府）不在此列 ——
        /// 它们本来就只有一座，拆了等于自断一项功能；"新建时被 IsCapped 拦住"已经足够。
        /// </summary>
        public int[] replaceableKinds = new int[] { 32, 33, 34, 35, 36, 37, 38 };

        /// <summary>同一建筑小类至少保留的座数（拆后不得低于该值）</summary>
        public int replaceMinKeep = 1;
    }
}
