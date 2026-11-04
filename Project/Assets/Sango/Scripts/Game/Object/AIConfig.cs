/*
 * 文件名：AIConfig.cs
 * 描述：AI 参数集中配置。所有 AI 决策阈值 / 权重集中于此，
 *       支持通过 Data/Common/AIConfig.json 覆盖（同样支持 Mod），未配置项使用内置默认值。
 */

using System.Collections.Generic;
using System.IO;
using Sango.Mod;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// AI 参数集中配置。
    ///
    /// 使用方式：
    /// 1. 代码中通过 <c>AIConfig.Instance.xxx</c> 读取参数；
    /// 2. 如需调整，在 <c>Data/Common/AIConfig.json</c> 中**只写要覆盖的字段**即可，
    ///    未出现的字段自动沿用内置默认值（反序列化会先执行字段初始化器）；
    /// 3. 该文件同样受 Mod 覆盖机制管理，便于制作 AI 调优 Mod。
    /// </summary>
    public class AIConfig
    {
        static AIConfig instance;

        /// <summary>
        /// 全局配置实例（首次访问时自动加载）。
        /// </summary>
        public static AIConfig Instance
        {
            get
            {
                if (instance == null)
                    instance = Load();
                return instance;
            }
        }

        /// <summary>
        /// 重置配置，下次访问时重新加载（用于剧本切换或调试）。
        /// </summary>
        public static void Reset()
        {
            instance = null;
        }

        #region 外交

        // 【为什么单独抽出来】Open311 的 AI 外交（force_diplomacy）没有"全局频率"，
        // 而是七项命令各自判概率与错峰（s11_game_ai_force_cmd_dip.cpp:5-74）。
        // 本项目原先把这些数字写死在 ForceAI.cs 里，改一次热度要动 9 处代码，
        // 现统一收到这里 —— 只写要覆盖的字段即可（Data/Common/AIConfig.json）。
        //
        // 【默认值已整体降温】相对原版（30% / %5 / %21 / %6 / 第 9 回合起手 / 第 36 回合劝降）：
        //   结盟、送礼、求援 30%→18%；破盟 30%→20%；送礼错峰 %5→%8；
        //   战争求和 %21→%24；预防性停战 %6→%9；起手回合 9→12；劝降起手 36→48。
        //   实测口径：同等局势下 AI 发起外交的次数约为原来的 4 成。

        /// <summary>AI 开始外交的回合数（原版 force_diplomacy 的 get_turn() &lt; 9 不外交）</summary>
        public int diplomacyStartTurn = 12;

        /// <summary>AI 开始执行"劝降/吞并"的回合数（原版 force_annex 的 turn &lt; 36）</summary>
        public int diplomacyAnnexStartTurn = 48;

        /// <summary>结盟 / 送礼 / 请求援军 的每回合触发概率（%，原版 rand_bool(30)）</summary>
        public int diplomacyActionChance = 18;

        /// <summary>破弃同盟的触发概率（%，原版 force_discard 的 rand_bool(30)）</summary>
        public int diplomacyDiscardChance = 20;

        /// <summary>送礼的错峰周期：turn % N == force.Id % N 才轮到（原版 %5）</summary>
        public int diplomacyDonateStagger = 8;

        /// <summary>战时求和的错峰周期（原版 turn % 21，只对"打进来的那家"求和）</summary>
        public int diplomacyCeasefireWarStagger = 21;

        /// <summary>
        /// 预防性停战的外层错峰周期（原版 `s11_game_ai_force_cmd_dip.cpp:596` 的
        /// `turn % 21 == force_id % 21`，仅在"没有在进攻任何人"时生效）。
        /// </summary>
        public int diplomacyCeasefirePeaceStagger = 21;

        /// <summary>
        /// 被 ≥3 家相邻敌势力围住时，预防性停战再加一道错峰（原版 `:632` 的 `turn % 6`）。
        /// </summary>
        public int diplomacyCeasefireManyEnemyStagger = 6;

        #endregion

        #region 城市防守

        /// <summary>判定"兵临城下"的距离（格）。此范围内存在存活敌人时，城市优先出城防守</summary>
        public int cityDefenseAlertRange = 8;
        /// <summary>实时扫描城池周边以兜底检测敌人的范围（格）。用于预计算敌人信息未覆盖的场景（如敌军刚抵达、正在攻击本城建筑）</summary>
        public int cityDefenseScanRange = 6;

        #endregion

        #region 城市支援

        /// <summary>派兵支援所需的本城最低兵力</summary>
        public int reinforceMinTroops = 10000;
        /// <summary>派兵支援所需的本城最低粮草</summary>
        public int reinforceMinFood = 8000;
        /// <summary>派兵支援所需的本城最低空闲武将数</summary>
        public int reinforceMinFreePersons = 2;
        /// <summary>单个城市最多同时派出的支援部队数量</summary>
        public int reinforceMaxTroopPerCity = 2;

        #endregion

        #region 城市出兵

        /// <summary>
        /// 【已停用】进攻他城的基础出兵上限（支部队）。
        ///
        /// 原版没有"支数"这个概念：<c>kyoten_attack</c> 用"在外累计兵力 &gt; 所需兵力"决定停不停
        /// （s11_game_ai_kyoten_cmd_mil_attack.cpp:266-270），
        /// 支数上限另由 min(行动力/单次出阵成本, 可下令人数, 据点人数−最少留守人数) 与
        /// DeployInfo::MaxUnitCount(16) 钳制。该字段仅为兼容旧配置保留，不再参与任何判定。
        /// </summary>
        public int attackBaseTroopCount = 12;
        /// <summary>【已停用】目标城池每多少兵力追加 1 支出兵，见 <see cref="attackBaseTroopCount"/></summary>
        public int attackTroopsPerTargetTroops = 3000;
        /// <summary>【已停用】进攻无主白城的出兵上限，见 <see cref="attackBaseTroopCount"/></summary>
        public int attackWhiteCityTroopCount = 2;

        /// <summary>好战个性每回合跳过进攻的概率（%）。越低越积极出兵（原硬编码 50）</summary>
        public int attackSkipChanceAggressive = 18;
        /// <summary>均衡个性每回合跳过进攻的概率（%）。越低越积极出兵（原硬编码 70）</summary>
        public int attackSkipChanceBalanced = 25;
        /// <summary>谨慎个性每回合跳过进攻的概率（%）。越低越积极出兵（原硬编码 80）</summary>
        public int attackSkipChanceDefensive = 35;

        /// <summary>
        /// 好战个性出兵所需的最低城内兵力（原硬编码 8000，见 CityAI.AICanAttack）。
        /// 与外置的跳过概率配合调整，单改一项容易被另一项卡住。
        /// </summary>
        public int attackMinTroopsAggressive = 6000;
        /// <summary>均衡个性出兵所需的最低城内兵力（原硬编码 10000）</summary>
        public int attackMinTroopsBalanced = 7000;
        /// <summary>谨慎个性出兵所需的最低城内兵力（原硬编码 12000）</summary>
        public int attackMinTroopsDefensive = 9000;

        /// <summary>
        /// 进入"目标打分"所需的最低城内兵力（原硬编码 20000，见 CityAI.DecideAttackTarget）。
        ///
        /// 【为什么必须一起调】AICanAttack 只决定"这回合出不出兵"，
        /// 真正挑选目标的是 DecideAttackTarget，它开头还有一道 20000 的硬门槛；
        /// 只放宽 AICanAttack 的话，城内兵力在 [minTroops, 20000) 区间时
        /// 候选队列恒为空（count = 0），AI 依然不会出兵 —— 表现为"概率调了没效果"。
        /// </summary>
        public int attackTargetMinTroops = 12000;

        /// <summary>
        /// 进攻目标打分时，若目标势力是玩家势力，额外增加的权重（默认 1500）。
        ///
        /// 【语义】加法项，叠加在"兵力比 × 城池耐久 × 外交关系 × 距离"之后，不是百分比：
        /// 正值让 AI 更倾向进攻玩家，负值则回避玩家（可用于做"AI 不打玩家"的休战档）。
        /// 只影响城级 AI 的选目标（<c>CityAI.DecideAttackTarget</c>），不影响部队级评分。
        ///
        /// 【原版无对应实现】Open311 的据点进攻目标排序（<c>ai_table_get_attack_dest_building_list</c>）
        /// 只按距离与战力给候选，不区分玩家 / AI。此项是本项目自创的"针对玩家施压"调节项。
        /// </summary>
        public int attackTargetPlayerBonus = 3500;

        /// <summary>
        /// AI 单次出阵（Attack 命令）消耗的据点行动力，默认 5。
        ///
        /// 【为什么需要单独配】实际扣减在 <c>CityAI.AIAttack</c>，
        /// 调度器预估在 <c>CityAIOutpost.GetOrderActionPointCost(AIOrderId.Attack)</c>，
        /// 两处必须同源：只改一处会让"排队预算"和"真实扣减"错位 ——
        /// 轻则 Attack 排不上队，重则预算没花完却判定执行完毕。
        /// 统一走 <see cref="GetAttackActionPointCost"/>，不要各自取值。
        ///
        /// 配成 &lt;= 0 时退回命令表成本（<c>City.GetJobCostAP(MakeTroop)</c>，含军事府减半），
        /// 可用于对照原版手感。
        /// </summary>
        public int aiAttackActionPointCost = 5;

        /// <summary>
        /// 取 AI 出阵的行动力成本。实际扣减与调度预估都必须调这里，保证同源。
        /// </summary>
        /// <param name="city">据点（可为 null，此时不含军事府减半）</param>
        /// <returns>单次出阵消耗的行动力</returns>
        public int GetAttackActionPointCost(City city)
        {
            if (aiAttackActionPointCost > 0)
                return aiAttackActionPointCost;

            return city != null ? city.GetJobCostAP(CityJobType.MakeTroop)
                                : JobType.GetJobCostAP((int)CityJobType.MakeTroop);
        }

        /// <summary>
        /// 是否输出据点进攻决策日志（默认 false）。
        /// 打开后 Console 会打印 AICanAttack 各否决原因、DecideAttackTarget 选中/未选中目标，
        /// 用于排查"某座城为什么不出兵"。仅在临时排查时打开。
        /// </summary>
        [JsonIgnore]
        public bool logAttackAI = true;

        #endregion

        #region 城市内政

        /// <summary>
        /// 【已停用】建筑**升级**的最低据点可用金下限，现仅保留字段以兼容旧配置，AI 逻辑不再读取。
        ///
        /// 原版 <c>kyoten_merge</c>（s11_game_ai_kyoten_cmd_dev_merge.cpp:19-21）确实有
        /// <code>
        /// // 금 여유 없음
        /// if (gold_ &lt; 500)
        ///     return true;
        /// </code>
        /// 但那是**合成**（多座同类设施合成一座高级设施）的费用余量，本项目没有合成这一步
        /// （见 <c>CityAIOutpostCommand.AIMerge</c> 注释），升级的真实支出是 <c>nextBuildingType.cost</c>。
        /// 照搬 500 金门槛的实际效果是"城池资金 0~499 时永远不升级"，
        /// 叠加 <c>OutpostRuntime.gold</c> 的负收入折抵（s11_game_ai_kyoten.cpp:30-32）后门槛更高，
        /// 故升级路径改为**逐座**判断买不买得起（<c>CityAI.UpgradeOneBuilding</c>），
        /// 扣款前由 <c>City.JobUpgradeBuilding</c>（City.cs:3217）最终复核。
        /// <para>
        /// 新建内政建筑同样没有最低金门槛 —— 原版 <c>kyoten_develop</c>
        /// （s11_game_ai_kyoten_cmd.cpp:96-98）只要求据点可用金非负（<c>gold_ &lt; 0</c>）。
        /// </para>
        /// </summary>
        [System.Obsolete("已停用：升级改由 CityAI.UpgradeOneBuilding 逐座按造价判断，本字段不再生效")]
        public int buildMinGold = 500;
        /// <summary>治安 / 训练等内政作业所需的城池最低金钱</summary>
        public int internalMinGold = 400;
        /// <summary>造兵装所需的最低金钱</summary>
        public int createItemsMinGold = 1000;
        /// <summary>造船 / 造器械所需的最低金钱</summary>
        public int createMachineMinGold = 1500;
        /// <summary>买粮所需保留的最低金钱（低于此值不买粮）</summary>
        public int tradeFoodKeepGold = 2000;
        /// <summary>褒奖武将所需保留的最低金钱</summary>
        public int rewardGoldKeep = 500;
        /// <summary>褒奖武将的忠诚上限（低于该值才褒奖）</summary>
        public int rewardLoyaltyThreshold = 90;
        /// <summary>褒奖武将的单回合人数上限（避免一击耗尽金钱）</summary>
        public int rewardMaxPersonPerTurn = 1;
        /// <summary>升级建筑允许的最大工期（回合），超过则暂缓升级</summary>
        public int buildUpgradeMaxTurn = 6;
        /// <summary>搜索行动的基础概率（%）</summary>
        public int searchBaseChance = 60;
        /// <summary>每名未发现武将额外增加的搜索概率（%），体现"未发现越多越勤于搜索"</summary>
        public int searchInvisibleChancePerPerson = 15;
        /// <summary>招募在野武将的单回合人数上限</summary>
        public int recruitPersonMaxPerTurn = 1;
        /// <summary>士气低于该值时必定训练</summary>
        public int cityMoraleLow = 70;
        /// <summary>训练概率的基准士气值（与当前士气之差线性折算概率）</summary>
        public int trainMoraleBase = 100;

        #endregion

        #region 补给部队

        /// <summary>组建补给队所需的城池最低兵力</summary>
        public int supplyMinCityTroops = 20000;
        /// <summary>组建补给队所需的城池最低粮草</summary>
        public int supplyMinCityFood = 60000;
        /// <summary>单个城市同时派出的补给队上限（战场级，通常一支即可覆盖整条战线）</summary>
        public int supplyMaxPerCity = 1;
        /// <summary>配比门槛：附近至少这么多支待补给友军时，才值得派出一支补给队（避免一对一）</summary>
        public int supplyMinNeedyTroops = 3;
        /// <summary>每支补给队携带的兵力上限（需能支撑一个战场）</summary>
        public int supplyTroopAmount = 30000;
        /// <summary>补给队粮草可支撑的回合数（按携带兵力 × 每兵粮耗估算携带量）</summary>
        public int supplyFoodTurnCount = 30;
        /// <summary>每支补给队携带粮草的硬上限</summary>
        public int supplyFoodAmount = 400000;
        /// <summary>友军粮草低于「兵力 × 该系数」时视为需要补给</summary>
        public int supplyFoodThresholdFactor = 3;
        /// <summary>补给队搜索待补给友军的最大距离（格）</summary>
        public int supplySearchRange = 20;
        /// <summary>补给队威胁扫描范围（格）。该范围内没有敌人则视为安全，可直接贴近友军</summary>
        public int supplyThreatScanRange = 6;
        /// <summary>前线部队的兵装覆盖率低于该百分比时视为"兵装不足"，值得派出补给队（%）</summary>
        public int supplyNeedyItemPercent = 50;
        /// <summary>补给队出征的最低随队兵力；可装备兵力（兵装数）低于该值时取消出征</summary>
        public int supplyMinCarryTroops = 3000;

        #endregion

        #region 威胁部队驱逐

        /// <summary>驱逐威胁部队的距离（格）。敌方部队进入本城该范围且位于本势力领地内时，派兵歼灭</summary>
        public int driveOutThreatRange = 25;
        /// <summary>单城同时派出的驱逐部队上限</summary>
        public int driveOutMaxTroopPerCity = 3;

        #endregion

        #region 部队 AI 模式

        /// <summary>
        /// 【严格对齐原版】是否用 Open311 的 <c>unit_update_order</c> 直译
        /// （<see cref="Troop.UpdateAIOrder"/>，对应 s11_game_ai_unit.cpp:99-315）
        /// 替换本项目的启发式行动前准备（<see cref="Troop.AIPrepare"/>）。
        ///
        /// true（默认）= 严格模式：只按"出征意图 + 断粮/兵少/器械气力"决定继续 / 改派 / 撤退，
        ///               不再有态势档位撤退、概率撤退、求援、随军增筑；
        /// false = 旧行为：态势分档 + 概率撤退 + 求援 + 随军增筑。
        ///
        /// 注意：评分环节（态势/角色/性格倍率）不受本开关影响，始终生效。
        /// </summary>
        public bool useStrictOpen311UnitAI = true;

        #endregion

        #region 部队撤退（保存实力）

        /// <summary>兵力低于该值时有概率撤退（就近入驻己方据点补给）</summary>
        public int retreatMinTroops = 300;
        /// <summary>兵力低于满编该百分比时，同样视为"兵力过低"并就近入驻补给（0 表示仅使用绝对阈值 retreatMinTroops）</summary>
        public int retreatTroopPercent = 25;
        /// <summary>士气低于该值时有概率撤退</summary>
        public int retreatMinMorale = 10;
        /// <summary>断粮时的撤退概率（%）</summary>
        public int retreatOutOfFoodChance = 80;
        /// <summary>兵力过低时的撤退概率（%）</summary>
        public int retreatFewTroopsChance = 60;
        /// <summary>士气过低时的撤退概率（%）</summary>
        public int retreatLowMoraleChance = 50;

        /// <summary>
        /// 出征回合数门槛：部队在野外待满该回合数之后，才允许做出撤退 / 回避决定。
        ///
        /// 用于抑制"刚出城就掉头回城"：刚出城的部队常被判定为劣势 / 危局（态势看的是局部战力比），
        /// 而"最近的己方据点"往往就是它刚出的那座城，于是被判撤退后立刻折返，
        /// 表现为部队在城池格上反复进出（观感：部队停在城池上不动）。
        ///
        /// 判定为 <c>liveDays &gt; retreatMinLiveDays</c>。0 或负数表示不限制（保持旧行为）。
        /// </summary>
        public int retreatMinLiveDays = 3;

        #endregion

        #region 部队态势分档

        /// <summary>是否启用战场态势分级策略（关闭则所有部队按"均势"档处理）</summary>
        public bool useTierStrategy = true;

        /// <summary>态势评估的搜索范围（格）。反映所考虑的"局部战场"大小</summary>
        public int tierScanRange = 12;
        /// <summary>我方兵力占比 ≥ 该值时判定为"碾压"（%）</summary>
        public int tierDecisivePercent = 65;
        /// <summary>我方兵力占比 ≥ 该值时判定为"优势"（%）</summary>
        public int tierAdvantagedPercent = 45;
        /// <summary>我方兵力占比 ≥ 该值时判定为"均势"（%）</summary>
        public int tierEvenPercent = 35;
        /// <summary>我方兵力占比 ≥ 该值时判定为"劣势"，低于该值则判定为"危局"（%）</summary>
        public int tierDisadvantagedPercent = 25;

        /// <summary>危局档权重：保存实力、脱离接触</summary>
        public TroopTierWeights tierCritical = new TroopTierWeights
        {
            attackScale = 50,
            counterPenaltyScale = 200,
            retreatChance = 80,
            bestN = 1,
        };
        /// <summary>劣势档权重：收缩防守</summary>
        public TroopTierWeights tierDisadvantaged = new TroopTierWeights
        {
            attackScale = 80,
            counterPenaltyScale = 150,
            retreatChance = 40,
            bestN = 1,
        };
        /// <summary>均势档权重：谨慎试探</summary>
        public TroopTierWeights tierEven = new TroopTierWeights
        {
            attackScale = 100,
            counterPenaltyScale = 100,
            retreatChance = 10,
            bestN = 5,
        };
        /// <summary>优势档权重：主动进攻</summary>
        public TroopTierWeights tierAdvantaged = new TroopTierWeights
        {
            attackScale = 120,
            counterPenaltyScale = 80,
            retreatChance = 0,
            bestN = 3,
        };
        /// <summary>碾压档权重：强攻追击</summary>
        public TroopTierWeights tierDecisive = new TroopTierWeights
        {
            attackScale = 140,
            counterPenaltyScale = 50,
            retreatChance = 0,
            bestN = 1,
        };

        /// <summary>是否允许部队因"态势不利"主动撤退（关闭则仅保留原有的断粮 / 兵少 / 士气撤退）</summary>
        public bool useTierRetreat = true;

        #endregion

        #region 部队领队性格（框架参数）

        /// <summary>是否启用领队性格对部队策略的影响（性格数值定义在 Personality 数据中）</summary>
        public bool useLeaderPersonality = true;
        /// <summary>是否允许性格覆盖任务推导出的部队角色</summary>
        public bool useLeaderRoleOverride = true;
        /// <summary>是否允许性格影响计略技能的释放偏好</summary>
        public bool useLeaderSkillPreference = true;

        /// <summary>性格层倍率的下限（%）</summary>
        public int leaderScaleMin = 50;
        /// <summary>性格层倍率的上限（%）</summary>
        public int leaderScaleMax = 150;

        /// <summary>合成后攻击倍率的下限（%），防止 tier × role × 性格 三层连乘失控</summary>
        public int finalAttackScaleMin = 40;
        /// <summary>合成后攻击倍率的上限（%）</summary>
        public int finalAttackScaleMax = 180;
        /// <summary>合成后反击倍率的下限（%）</summary>
        public int finalCounterScaleMin = 40;
        /// <summary>合成后反击倍率的上限（%）</summary>
        public int finalCounterScaleMax = 180;

        /// <summary>性格好战指数（|troopAggression|）达到该值时，性格角色覆盖任务角色</summary>
        public int leaderRoleOverrideThreshold = 20;

        /// <summary>行动择优池的全局上限（防止性格修正后池过大）</summary>
        public int bestNMax = 8;
        /// <summary>行动择优池的全局下限</summary>
        public int bestNMin = 1;

        /// <summary>计略专精每点折算的收益倍率（%）</summary>
        public int leaderSkillTendencyScalePerPoint = 5;
        /// <summary>计略偏好倍率的下限（%）</summary>
        public int leaderSkillScaleMin = 75;
        /// <summary>计略偏好倍率的上限（%）</summary>
        public int leaderSkillScaleMax = 150;

        /// <summary>危局档的撤退不受性格影响（防止"莽将送死"）</summary>
        public bool leaderIgnoreRetreatInCritical = true;

        #endregion

        #region 部队角色

        /// <summary>是否启用部队角色分级（关闭则所有部队按"通用"处理，不做角色修正）</summary>
        public bool useTroopRole = true;

        /// <summary>攻坚：敢吃反击、强打目标</summary>
        public TroopRoleWeights roleAssault = new TroopRoleWeights
        {
            attackScale = 120,
            counterPenaltyScale = 60,
            missionFocusScale = 130,
        };
        /// <summary>防守：重视规避反击与地形</summary>
        public TroopRoleWeights roleDefender = new TroopRoleWeights
        {
            attackScale = 90,
            counterPenaltyScale = 150,
            missionFocusScale = 110,
        };
        /// <summary>骚扰：专注任务、打完就跑</summary>
        public TroopRoleWeights roleHarasser = new TroopRoleWeights
        {
            attackScale = 110,
            counterPenaltyScale = 80,
            missionFocusScale = 140,
        };
        /// <summary>游击：任务专注度低，容易被眼前目标吸引</summary>
        public TroopRoleWeights roleSkirmisher = new TroopRoleWeights
        {
            attackScale = 100,
            counterPenaltyScale = 120,
            missionFocusScale = 80,
        };
        /// <summary>护卫：尽量避免接战</summary>
        public TroopRoleWeights roleEscort = new TroopRoleWeights
        {
            attackScale = 70,
            counterPenaltyScale = 180,
            missionFocusScale = 100,
        };
        /// <summary>守备：驻守据点，不主动出击</summary>
        public TroopRoleWeights roleGuard = new TroopRoleWeights
        {
            attackScale = 80,
            counterPenaltyScale = 150,
            missionFocusScale = 70,
        };
        /// <summary>工兵：携带资金修建前线辅助建筑，极度规避接战</summary>
        public TroopRoleWeights roleEngineer = new TroopRoleWeights
        {
            attackScale = 40,
            counterPenaltyScale = 250,
            missionFocusScale = 150,
        };

        #endregion

        #region 部队技能评分

        /// <summary>被反击惩罚系数（越大越规避"贴脸打高反击单位"）</summary>
        public int counterAttackPenaltyFactor = 10;
        /// <summary>被反击惩罚对攻击收益的最大削弱比例（%），防止 AI 过度保守而完全不行动</summary>
        public int counterAttackPenaltyFloorPercent = 50;

        // ---------- 基础收益公式的常数（原为代码内字面量，现全部可配） ----------

        /// <summary>技能基础收益的总系数</summary>
        public int scoreSkillBaseFactor = 150;
        /// <summary>远程技能加成</summary>
        public int scoreRangeBonus = 15;
        /// <summary>单体技能加成</summary>
        public int scoreSingleBonus = 5;
        /// <summary>带额外效果技能加成</summary>
        public int scoreEffectBonus = 5;
        /// <summary>目标处于被控制状态（混乱 / 眩晕）时的加成</summary>
        public int scoreControlBonus = 30;
        /// <summary>策略技能成功率的最低门槛，低于该值直接放弃施放</summary>
        public int scoreStrategySuccessThreshold = 75;
        /// <summary>策略技能成功率超出门槛时，每点折算的施法距离加成</summary>
        public int scoreStrategySuccessBonus = 2;

        // ---------- 剑兵计略倾向（剑兵只有"近战普攻"，物理输出孱弱，AI 更愿意用计略弥补）----------

        /// <summary>是否启用"剑兵倾向计略"。关闭则剑兵与其它兵种一视同仁</summary>
        public bool useSwordStrategyPreference = true;
        /// <summary>剑兵的计略收益倍率（%）。&gt;100 表示比其它兵种更愿意释放计略；设为 100 等于关闭倾向</summary>
        public int swordStrategyScoreScale = 200;
        /// <summary>剑兵的计略成功率门槛（%）。低于全局门槛，让智力平平的剑兵队也敢赌计略</summary>
        public int swordStrategySuccessThreshold = 50;
        /// <summary>攻防差值的基数（避免攻防差为负时整体收益归零）</summary>
        public int scoreAttackDefenceBase = 200;
        /// <summary>技能能耗的最小值（防止除零并限制低耗技能的过高收益）</summary>
        public int scoreMinCostEnergy = 10;

        /// <summary>攻打建筑时的伤害倍数</summary>
        public int scoreBuildingDamageFactor = 4;
        /// <summary>攻打建筑：单体技能的收益倍数</summary>
        public int scoreBuildingSingleFactor = 15;
        /// <summary>攻打建筑：多体技能的收益倍数</summary>
        public int scoreBuildingMultiFactor = 10;
        /// <summary>攻打建筑：远程技能的施法距离系数</summary>
        public int scoreBuildingRangeFactor = 150;
        /// <summary>攻打建筑：近战技能的施法距离系数</summary>
        public int scoreBuildingMeleeFactor = 100;
        /// <summary>误伤友方建筑的收益倍数（负数表示惩罚）</summary>
        public int scoreFriendlyBuildingFactor = -8;

        /// <summary>最终分数的缩放除数（把内部大数压到可比较的量级）</summary>
        public int scoreFinalDivisor = 100;

        /// <summary>
        /// 【严格对齐原版】是否用"真实伤害预演"决定这一击值不值得打。
        ///
        /// 对应原版 get_attack_damage_score（D:\open311\s11\s11_sys_unit_action.cpp:1780-1834）：
        ///   · 对部队：伤害打不掉敌兵力一半 → 分值 0（直接放弃该目标）；
        ///   · 对据点（城/关/港）：既不能一击破守军、也不能一击破耐久，
        ///     且兵力伤害不足守军 1/3、耐久伤害不足剩余耐久 1/3 → 0；
        ///   · 对陷阱 / 障碍 → 0；
        ///   · 对普通设施：一击打不掉耐久 → 0。
        ///
        /// 打开后 AI 不再"硬啃打不动的目标"，代价是评分阶段每个候选多一次伤害计算。
        /// 关闭则完全回到改造前的启发式（用 Attack-Defence 近似，从不放弃）。
        /// </summary>
        public bool useRealDamageScore = true;

        /// <summary>
        /// 【严格对齐原版】攻击目标时，若目标周围 1~2 格内有**本方建筑**，额外加的分值。
        ///
        /// 对应原版 get_target_score（D:\open311\s11\s11_sys_unit_action.cpp:1742-1754）：
        /// 原版固定 <c>score += 1375</c>，语义是"在自家设施火力/补给覆盖下作战更有利"。
        ///
        /// 【为什么默认是 4950 而不是 1375——原版公式逐项比对】
        ///
        /// 原版一次攻击动作的总分（add_attack_action，s11_sys_unit_action.cpp:404-425）：
        ///     param.attack_score（统率+武力）        ≈ 160
        ///   + target_score  敌对目标                + 5000
        ///   + 攻击动作常量                          + 1000
        ///   + get_attack_damage_score（默认档）     + 2000
        ///   + 伤害分命中常量                        + 1000
        ///   ─────────────────────────────────────────────
        ///   基线合计（不含本项）                    ≈ 9160
        ///   + 本项 1375 → 10535，相对提升 15.0%
        ///
        /// 本项目的单次评分（CalcSkillBasePriority，典型枪兵普攻）：
        ///     (skill.atk 10 + bonus 0) × 150 / energy 10 × (Attack 100 - Defence 80 + 200)
        ///   = 10 × 15 × 220 = 33000
        ///   → 要取得同样的 15.0% 相对提升，需要 33000 × 0.150 ≈ 4950
        ///
        /// 所以这里取等效值 4950（**结果对齐**），而不是照抄 1375（**数值对齐但结果偏弱**）。
        /// 若要严格照抄原版数值，把它改成 1375 即可。
        ///
        /// 【注意】33000 只是典型值。实际 baseScore 随兵种/技能/攻防差在 2.7 万~8 万波动，
        /// 因此固定分值无法在所有场景都保持 15% 的权重；若要做成"恒定百分比"，
        /// 应改为乘性系数（如 score × 115 / 100），但那已不是原版的加法语义。
        /// </summary>
        public int scoreNearFriendlyBuildingBonus = 4950;

        // ---------- Open311 原版评分体系（严格对齐用，全部可配） ----------

        /// <summary>
        /// 是否启用 **Open311 原版评分体系**（离散加项制）替代本项目的连续分制。
        ///
        /// true（默认）= 完全按原版 get_target_score / get_attack_damage_score /
        ///               add_attack_action 的分值结构算分，下表中所有 o311* 项生效；
        /// false = 回到本项目的 (skill.atk + bonus) × 系数 连续分制。
        ///
        /// 两者不可混用：一旦启用，<see cref="scoreSkillBaseFactor"/> /
        /// <see cref="scoreRangeBonus"/> / <see cref="scoreAttackDefenceBase"/> 等连续分制的常数不再参与。
        /// </summary>
        public bool useOpen311ScoreFormula = true;

        /// <summary>敌方目标加分 —— 原版 get_target_score:1694-1695</summary>
        public int o311TargetHostile = 5000;
        /// <summary>友方目标加分 —— 原版 get_target_score:1696-1697</summary>
        public int o311TargetFriendly = 1500;

        /// <summary>
        /// 误伤己方目标的扣分（以正值配置，代码里取负）。
        ///
        /// 【为什么需要这一项】canDamageTeam = true 的技能（妖术、落雷这类真 AOE）在执行层是
        /// **故意**放行误伤的：
        /// <code>
        /// SkillInstance.cs:760  troop.IsEnemy(beAtkTroop) || canDamageTeam
        /// SkillInstance.cs:862  troop.IsEnemy(beAtkBuildingBase) || canDamageTeam
        /// </code>
        /// 这是给**玩家手动施法**准备的取舍（范围技的代价），但 AI 也会照打 ——
        /// 挑"以自家市场 / 农田为中心"的落点放妖术，一次扣 5 点耐久。
        /// 原版 can_attack 对友军建筑 / 部队一律 false（s11_scen_obj_unit.cpp:329-354），
        /// AI 根本拿不到这种落点，所以本项目必须用分值把它压下去。
        ///
        /// 【取值依据】原版的收益结构里，一个普通敌人目标约
        /// o311TargetHostile(5000) + o311NormalOnUnit(2000) = 7000；
        /// 若目标还是任务主目标再 +o311TargetMain(10000)。要让"打敌人顺带砸自家"不划算，
        /// 扣分必须大于单敌人收益，默认取 12000。
        ///
        /// 【区别于 -1】原版 get_target_score 用 -1 表示"该目标无效"，
        /// 聚合时会被直接跳过（不参与累加）。本项是**真实惩罚**，
        /// 在 TroopAIUtility.PriorityAction 的聚合里按
        /// <c>p &lt;= o311InvalidTargetMark</c> 与 -1 区分开、照常累加。
        /// </summary>
        public int o311SelfDamagePenalty = 12000;

        /// <summary>
        /// "该目标无效"的标记值（原版 get_target_score 的 -1）。
        ///
        /// 聚合多个命中格时，<c>p</c> 落在这个区间内的按"无效目标"处理、直接跳过，
        /// 不做负分累加 —— 否则几十个无效目标会把整条候选拖到负分，
        /// 让"顺便命中一片空地"变成劣势。真正该扣分的误伤走
        /// <see cref="o311SelfDamagePenalty"/>（它的绝对值远大于本阈值）。
        /// </summary>
        public int o311InvalidTargetMark = -100;
        /// <summary>攻城兵器（冲车~木兽）打建筑加分 —— 原版 :1701-1707</summary>
        public int o311SiegeOnBuilding = 2000;
        /// <summary>普通兵装打部队加分 —— 原版 :1719-1720</summary>
        public int o311NormalOnUnit = 2000;
        /// <summary>命中任务主目标加分 —— 原版 :1724-1727</summary>
        public int o311TargetMain = 10000;
        /// <summary>目标周围 1~2 格有本方建筑加分 —— 原版 :1742-1754</summary>
        public int o311NearFriendlyBuilding = 1375;
        /// <summary>异族 / 贼根城（FacilityId_Base/Den，本项目 kind=15/16）加分 —— 原版 :1770-1773</summary>
        public int o311BaseDenBonus = 200;

        /// <summary>攻击动作常量 —— 原版 add_attack_action:410</summary>
        public int o311AttackActionBonus = 1000;
        /// <summary>可被反击惩罚 —— 原版 add_attack_action:411-412</summary>
        public int o311CounterPenalty = 150;
        /// <summary>伤害分命中常量 —— 原版 add_attack_action:415</summary>
        public int o311DamageHitBonus = 1000;

        /// <summary>伤害分：打掉敌兵力一半以上（未击破）—— 原版 get_attack_damage_score:1795-1796</summary>
        public int o311DamagePartial = 1000;
        /// <summary>伤害分：默认档（击破 / 建筑伤害足够）—— 原版 :1833</summary>
        public int o311DamageDefault = 2000;
        /// <summary>伤害分：一击破据点（近战）—— 原版 :1806-1807</summary>
        public int o311DamageKillMelee = 10000;
        /// <summary>伤害分：一击破据点（远程）—— 原版 :1806-1807</summary>
        public int o311DamageKillRanged = 2000;

        /// <summary>战法动作常量 —— 原版 add_tactic_action（撞飞/乱射/二段突/熊手等各不同，取基准 949）</summary>
        public int o311TacticActionBonus = 949;
        /// <summary>计略基础常量 —— 原版 add_strategy_action:894 的 <c>base_score += ... + 500</c></summary>
        public int o311StrategyBaseBonus = 500;

        /// <summary>计略成功率门槛（%）：低于该值不评估目标相关加减（原版 `chance >= 35`）</summary>
        public int o311StrategyChanceThreshold = 35;
        /// <summary>消火：自己脚下（无「火神」）时的分值 —— 原版 :909-912</summary>
        public int o311StrategyShoukaSelf = 20000;
        /// <summary>消火：友方据点 / 已行动部队 —— 原版 :927 / :950</summary>
        public int o311StrategyShoukaKyoten = 375;
        /// <summary>消火：友方阵类 —— 原版 :932</summary>
        public int o311StrategyShoukaCamp = 250;
        /// <summary>消火：友方其它设施 —— 原版 :935</summary>
        public int o311StrategyShoukaOther = 125;
        /// <summary>伏兵动作常量 —— 原版 :1165</summary>
        public int o311StrategyFukuheiBonus = 474;
        /// <summary>同讨动作常量 —— 原版 :1181</summary>
        public int o311StrategyDoushiuchiBonus = 250;
        /// <summary>同讨 + 「神算」/「深谋」加成 —— 原版 :1183-1184</summary>
        public int o311StrategyDoushiuchiSkillBonus = 750;
        /// <summary>计略动作常量 —— 原版 add_strategy_action 各分支基准值（消火 20000 / 建筑 375·250·125 …）</summary>
        public int o311StrategyActionBonus = 849;

        /// <summary>
        /// 陷阱"炸不到人"时的罚分 —— 原版 add_tactic_action / add_strategy_action 里的硬编码 10000
        /// （s11_sys_unit_action.cpp:636 / :848 / :868 / :1022）。
        ///
        /// 用于战法与火计：<c>get_trap_score</c> ≤ 0（爆炸范围内没有敌人 / 只炸到友军 /
        /// 自伤，或陷阱尚未建成）时从候选分数里扣掉这么多，使其基本不可能被选中。
        /// 普攻 / 包围不用这个罚分 —— 原版对已建成陷阱是**直接不生成候选**（:389 / :408）。
        /// </summary>
        public int o311TrapScorePenalty = 10000;

        /// <summary>
        /// Open311 模式下的行动择优池大小。
        ///
        /// **1（默认）= 总是取评分最高的动作**，等价于原版
        /// action_list_remove_by_chance（s11_sys_unit_action.cpp:1355-1386）的效果 ——
        /// 该函数只从第 2 名开始按动作类型概率移除，第一名永远保留。
        ///
        /// **&gt;1 = 从评分最高的前 N 个里加权随机**（本项目自创的"动态择优池"，
        /// 会让同一局面重载后决策不同）。设为 N 即"最高随机数量"。
        ///
        /// 仅在 <see cref="useOpen311ScoreFormula"/> 为真时生效；
        /// 为假时走 <see cref="bestN"/> 相关的一整套 Tier/Role/性格合成逻辑。
        /// </summary>
        public int o311BestN = 1;

        // ---------- 位置分（get_pos_score，s11_sys_unit_action.cpp:1452-1588） ----------

        /// <summary>砂地位置分（原版 TerrainId_Sand）</summary>
        public int o311PosTerrainSand = 40;
        /// <summary>湿地位置分（原版 TerrainId_Wetland）</summary>
        public int o311PosTerrainWetland = 30;
        /// <summary>毒泉位置分（原版 TerrainId_Poison）</summary>
        public int o311PosTerrainPoison = -10000;
        /// <summary>毒泉 + 「解毒」特技的位置分（原版 SkillId_Gedoku）</summary>
        public int o311PosTerrainPoisonWithFeat = 150;
        /// <summary>森林位置分（原版 TerrainId_Forest）</summary>
        public int o311PosTerrainForest = 80;
        /// <summary>栈道位置分（原版 TerrainId_Cliffroad）</summary>
        public int o311PosTerrainCliffroad = -250;
        /// <summary>栈道 + 「难所行军」科技 / 「藤甲」特技的位置分</summary>
        public int o311PosTerrainCliffroadWithFeat = 70;
        /// <summary>每有一个相邻友军部队的惩罚（原版 -100/个）</summary>
        public int o311PosNeighborOwnUnitPenalty = 100;
        /// <summary>所在格着火时的惩罚（原版 -750）</summary>
        public int o311PosFirePenalty = 750;

        // ---------- 设施分（get_pos_around_facilities_score，s11_sys_unit_action.cpp:1473-1565） ----------

        /// <summary>周围有己方据点的基础分（原版 +550）</summary>
        public int o311PosOwnKyotenBase = 550;
        /// <summary>每个己方据点的额外分（原版 +20/个）</summary>
        public int o311PosOwnKyotenPerCount = 20;
        /// <summary>太鼓台 / 军乐台系数（原版 100 × (太鼓台×3 + 军乐台×1)）</summary>
        public int o311PosDrumBandFactor = 100;
        /// <summary>阵 / 箭楼系数（原版 350 × (阵×4 + 箭楼×1)）</summary>
        public int o311PosCampTowerFactor = 350;
        /// <summary>投石台系数（原版 450 × 投石台数）</summary>
        public int o311PosCatapultFactor = 450;
        /// <summary>敌方据点的额外基础分（原版 +4 × 275）</summary>
        public int o311PosEnemyKyotenExtraBase = 275;
        /// <summary>敌方据点额外分随数量增长（原版 +4 × (5/个)）</summary>
        public int o311PosEnemyKyotenExtraPerCount = 5;

        // ---------- 任务权重（倍率，%）：替代原先 500000 / 5 / 1000000 / 30000 / 50000 等硬编码 ----------

        /// <summary>任务主目标的收益倍率（%）。越高越执着于既定任务目标</summary>
        public int taskPrimaryTargetWeight = 5000;
        /// <summary>任务非目标的收益倍率（%）。远低于主目标，使部队不会偏离任务</summary>
        public int taskOtherTargetWeight = 5;
        /// <summary>原地施法加成（%）：无需移动即可攻击时的额外收益</summary>
        public int taskStayBonus = 200;
        /// <summary>近战贴脸加成（%）：非远程部队移动到敌前时的额外收益</summary>
        public int taskMeleeCloseBonus = 50;
        /// <summary>攻打任务目标城池 / 建筑的额外倍率（%）</summary>
        public int taskBuildingWeight = 100;
        /// <summary>攻打"兵力远大于我方"敌人的额外加成（%）</summary>
        public int taskBigTroopBonus = 30;
        /// <summary>"大部队"的判定倍率（%）：目标兵力超过我方该比例时视为大部队</summary>
        public int taskBigTroopPercent = 150;

        /// <summary>保护城池时，"威胁城池"的判定距离（格）</summary>
        public int protectCityThreatRange = 5;
        /// <summary>保护城池时，敌人每靠近城池 1 格的攻击收益加成（%）</summary>
        public int protectCityNearEnemyBonusPerCell = 40;

        /// <summary>
        /// 盟友城协防的最远距离（格）：超过该距离直接召回最近的自有城，不再往盟友城下跑。
        /// 口径来源：玩家反馈"结盟后 AI 队伍扛着同字标堵到家门口"，见 TroopAIFallback。
        /// </summary>
        public int allySupportMaxDistance = 12;
        /// <summary>
        /// 盟友城协防的最低兵力比（%）：(部队兵力 + 城内兵力) / 围城敌军兵力 低于该值就不去送死，直接召回。
        /// </summary>
        public int allySupportMinTroopRatio = 70;

        #endregion

        #region 部队求援与撤退

        /// <summary>部队状态（兵力 / 满编）低于该百分比时视为"状态不佳"，可能发起求援（%）</summary>
        public int askSupplyHealthPercent = 30;
        /// <summary>搜索可求援补给队的最远距离（格）</summary>
        public int askSupplySearchRange = 12;

        /// <summary>补给队兵力低于该值时返回据点补充（0 表示不限制）</summary>
        public int supplyReturnTroops = 3000;
        /// <summary>补给队粮草低于该值时返回据点补充（0 表示不限制）</summary>
        public int supplyReturnFood = 6000;
        /// <summary>补给队兵装低于该值时返回据点补充（0 表示不限制）</summary>
        public int supplyReturnItems = 100;
        /// <summary>补给队与主力部队保持的最小距离（按"回合"计，乘以其移动力）</summary>
        public int supplyKeepDistanceTurns = 1;
        /// <summary>我方战力占比低于该百分比时视为"大劣"，补给队立刻撤退（%）</summary>
        public int supplyRetreatForcePercent = 35;
        /// <summary>补给队与最近敌方部队保持的安全距离（格，约等于一个回合的移动行程）。低于该距离则向己方据点后撤</summary>
        public int supplySafeDistance = 3;

        /// <summary>求援结束后是否恢复原任务（如继续进攻 / 继续防守）</summary>
        public bool askSupplyRestoreMission = true;

        #endregion

        #region 前线战略建筑

        /// <summary>
        /// 是否启用"前线修建战略辅助建筑"（军乐台 / 太鼓台 / 砦 / 箭楼等）。
        /// 关闭时将回退到旧逻辑：只在城池辖区（areaCellList）内随机选点建造。
        /// </summary>
        public bool useFrontBuilding = true;

        /// <summary>前线候选建址的搜索半径（格）。以城池为中心向外扩展，突破"本城辖区"的限制</summary>
        public int frontBuildSearchRange = 15;

        /// <summary>每个城池同时派出的工程队上限</summary>
        public int frontBuildMaxPerCity = 1;

        /// <summary>派出工程队所需的本城最低兵力</summary>
        public int frontBuildMinTroops = 10000;
        /// <summary>派出工程队所需的本城最低粮草</summary>
        public int frontBuildMinFood = 10000;
        /// <summary>派出工程队所需的本城最低金钱</summary>
        public int frontBuildMinCityGold = 2000;

        /// <summary>每回合尝试派遣工程队的概率（%）。避免每座城每回合都出兵</summary>
        public int frontBuildChance = 40;

        /// <summary>
        /// 工程队携带的资金（金）。0 表示按"目标建筑造价 × frontBuildGoldCostPercent%"自动计算。
        /// 给足余量以便一次出行连续建造多座建筑。
        /// </summary>
        public int frontBuildBudget = 0;
        /// <summary>自动计算携带资金时的造价倍率（%）：如 300 表示按三倍造价携带，可连建约 3 座</summary>
        public int frontBuildGoldCostPercent = 300;

        /// <summary>候选建址的最低评分门槛，低于该分不值得派遣工程队</summary>
        public int frontBuildSiteMinScore = 100;

        /// <summary>覆盖范围内己方兵力低于该值时，该建址视为"无人受益"而放弃</summary>
        public int frontBuildMinCoverTroops = 3000;

        /// <summary>建址与最近敌人的最小允许距离（格）。过近会被敌人迅速拆除，直接排除</summary>
        public int frontBuildSafeMinDist = 2;
        /// <summary>建址与敌人的理想距离带下限（格）</summary>
        public int frontBuildIdealMinDist = 3;
        /// <summary>建址与敌人的理想距离带上限（格）。超出该距离视为"太靠后、用不上"</summary>
        public int frontBuildIdealMaxDist = 12;

        /// <summary>建址威胁扫描半径（格）：用于评估该点会被敌人多快攻击</summary>
        public int frontBuildThreatRange = 6;

        /// <summary>
        /// 前线建址"战略要道"判定半径（格）：该范围内的城池 / 港口 / 关隘视为扼守要冲，
        /// 会导向箭楼 / 连弩楼 / 投石台这类自动输出建筑。
        ///
        /// 【必须与建造间距解耦】建址的间距校验会排除"<c>BuildingSpace</c> 格内已有建筑"的格子，
        /// 若这里也用同样的 2 格，就与间距校验直接冲突、判定恒不成立（箭楼类建筑因此永远选不到）。
        /// 该值必须明显大于 <c>BuildingSpace</c>，默认 6。
        /// </summary>
        public int frontChokeScanRange = 6;

        /// <summary>前线价值评分中"己方覆盖收益"的权重（%）</summary>
        public int frontCoverWeight = 100;
        /// <summary>前线价值评分中"战略要道（邻近关 / 港 / 城池）"的权重（%）</summary>
        public int frontChokeWeight = 80;
        /// <summary>前线价值评分中"敌方威胁惩罚"的权重（%）</summary>
        public int frontThreatWeight = 120;
        /// <summary>前线价值评分中"距理想距离带偏离"的惩罚权重（%）</summary>
        public int frontDistanceWeight = 60;

        /// <summary>覆盖范围内己方部队平均气力低于该百分比时，优先修建军乐台（%）</summary>
        public int frontBuildLowMoralePercent = 60;

        /// <summary>
        /// 军乐台"去重"半径（格）：建址该半径内若已有己方军乐台，
        /// 说明这片区域的回气力需求已被覆盖，不再重复修建（军乐台效果是范围性的），
        /// 转而按其它战场态势判定该建什么。配 0 关闭去重。
        /// </summary>
        public int frontMoraleDedupeRange = 4;

        /// <summary>覆盖范围内粮草紧张的己方部队占比超过该百分比时，优先修建省粮建筑（%）</summary>
        public int frontBuildLowFoodPercent = 40;

        /// <summary>工程队完成一座建筑后是否继续寻找下一个建址（关闭则建完即回城）</summary>
        public bool frontBuildContinueAfterDone = true;
        /// <summary>工程队连续建造的最大次数（防止其长期在外）</summary>
        public int frontBuildMaxContinuous = 3;

        /// <summary>
        /// 兜底选型权重表（按建筑 kind 计权，与 <see cref="frontFallbackWeights"/> 一一对应）。
        ///
        /// 【为什么要按权重随机】军乐台 needTech=0 恒可建，若兜底仍按"优先序列取第一个可建"，
        /// 就等价于永远建 军乐台——这正是"AI 只建军乐台"的直接成因。
        /// 改成权重随机后，同一片战场会自然出现箭楼 / 太鼓台 / 阵等不同类型的建筑。
        /// 默认：军乐台(13) 30 / 太鼓台(12) 25 / 阵(4) 25 / 箭楼(7) 20。
        /// </summary>
        public int[] frontFallbackKinds = new int[] { 13, 12, 4, 7 };
        /// <summary>兜底选型权重（与 <see cref="frontFallbackKinds"/> 一一对应）</summary>
        public int[] frontFallbackWeights = new int[] { 30, 25, 25, 20 };
        /// <summary>未列在 <see cref="frontFallbackKinds"/> 中的 kind，在兜底选型时的权重</summary>
        public int frontFallbackDefaultWeight = 10;

        /// <summary>
        /// 前线建址优先建筑类型（按 kind），仅作为"权重表里没有任何可建项"时的次级兜底顺序。
        /// 默认：军乐台(13) → 太鼓台(12) → 阵(4) → 箭楼(7)。
        /// </summary>
        public int[] frontBuildPreferredTypes = new int[] { 13, 12, 4, 7 };

        /// <summary>是否让前线作战部队就地增筑辅助建筑（B3：随军增筑）</summary>
        public bool useFieldBuilding = true;
        /// <summary>作战部队就地增筑所需的兵力占满编的最低比例（%），低于此值优先保命而非施工</summary>
        public int fieldBuildMinHealthPercent = 60;
        /// <summary>作战部队就地增筑时的资金门槛（金），需不低于目标建筑造价</summary>
        public int fieldBuildMinGold = 0;
        /// <summary>作战部队就地增筑的触发概率（%）</summary>
        public int fieldBuildChance = 20;

        /// <summary>
        /// 【已停用】作战部队出征携款上限。
        ///
        /// 出征携款已改为严格对齐原版（s11_game_ai_kyoten_cmd_mil_attack.cpp:296-334）：
        /// 敌势力据点且非攻城兵器时，据点可用金 &gt;= 10000 → 75% 带 1500~2000；
        /// &gt;= 2000 → 50% 带 1000；空关隘/港口必带"随军俸禄 × 8"。
        /// 该字段仅为兼容旧配置保留，不再参与计算。
        /// </summary>
        public int troopCarryGold = 1500;
        /// <summary>
        /// 【已停用】旧公式按"占总兵力比例(%)"设计，但除数误用 10000（应为 100），
        /// 导致 8000 兵只带 4 金，随军增筑 / 前线建陷阱整条链路失效。
        /// 现由原版概率携款取代，见 <see cref="troopCarryGold"/>。
        /// </summary>
        public int troopCarryGoldPerTroops = 5;

        /// <summary>补给队可转运的资金比例（%）。0 表示不转运金钱（沿用旧行为）</summary>
        public int supplyTransferGoldPercent = 30;
        /// <summary>补给时友军金钱低于该值才视为"缺钱"（金）</summary>
        public int supplyNeedGoldThreshold = 800;

        #endregion

        #region 城池命令优先级（动态排序）

        /// <summary>
        /// 是否启用原版据点状态调度（OutpostState 四态 + 军团行动力预算）。
        ///
        /// 对齐出处：Open311 的 district_ai / 据点主流程 三层调度。
        /// true（默认）：Force 先采集据点态势 → 军团按优先级切分行动力 → 每个据点按状态
        ///               查固定命令表逐条执行（见 ForceAIOutpost.cs / CorpsAIDistrict.cs / CityAIOutpost.cs）。
        /// false：回退到旧实现（CorpsAI.AICities 直接遍历自己的城逐个 DoAI，
        ///        城市命令由 CityAIOrderPlanner 打分排序）。
        /// 保留开关是为了出问题时能一行配置回滚。
        /// </summary>
        public bool useOutpostStateOrder = true;

        /// <summary>
        /// 是否输出据点 AI 调度日志（默认关闭）。
        /// 打开后在 Console 里能看到每个军团的行动力分配、每个据点的状态与命令队列，
        /// 对齐原版 s11_game_ai_kyoten_cmd.cpp:15-70 的 debug 日志（原版由 s11_log 宏控制）。
        /// 仅在排查"某座城为什么不出兵 / 不打仗"时临时打开。
        /// </summary>
        [JsonIgnore]
        public bool logOutpostAI = false;

        /// <summary>
        /// 是否允许 AI 自动拆分 / 重编军团（对齐原版 force_districts）。
        /// 关闭后 AI 势力只保留玩家设定的军团结构；开启后 AI 会在"某个军团城数明显偏多"时
        /// 把它的城拆一半出去，新建一个军团（新都督取新军团里功绩最高的太守）。
        /// 默认开启以对齐原版行为，出现异常时可用一行配置关掉。
        /// </summary>
        public bool useAutoCorpsSplit = true;

        /// <summary>
        /// 是否让 AI 按 Open311 的据点内政权重表决定"下一个建什么"。
        ///
        /// 对齐出处：s11_game_ai_kyoten_cmd_dev_ai.cpp:372-653 dev_ai_select_facility_id。
        /// true（默认）：按"城特产 + 据点四态 + 硬上限 + 金"加权随机，
        ///               原版判"不该建"时不建（不再回退到缺口驱动）。
        /// false：回退到 <c>BuildingPlanner.FindBestType</c> 的粮 / 金缺口驱动。
        /// </summary>
        public bool useOutpostBuildRule = true;

        /// <summary>
        /// 是否在<span>原版权重表</span>之上叠加"需求缺口修正"（仅作用于市场 / 农场）。
        ///
        /// 融合思路：原版权重表负责**硬约束**（max 上限、特产加权、后方 / 港口 / 前线禁建），
        /// 缺口驱动负责**软偏好**（缺粮时农场加权、缺金时市场加权、富余时降权）。
        /// 因为原版市场 / 农场无数量上限、权重恒为 10，是整张表里最粗的一处，
        /// 用缺口去细分它不会破坏原版的结构性设计。
        /// 关闭后回到"原版权重表原样"（市场 / 农场永远等权）。
        /// </summary>
        public bool useDemandCorrection = true;

        /// <summary>是否启用动态城池命令排序（关闭则回退到各内政系统内的硬编码顺序）</summary>
        public bool useDynamicCityOrder = true;
        /// <summary>
        /// 城池命令优先级的全部权重与阈值（基础分 / 态势阈值 / 评分分值 / 个性修正 / 紧急覆盖）。
        /// 可通过 Data/Common/AIConfig.json 的 "cityOrder" 节点做部分覆盖。
        /// </summary>
        public CityOrderWeights cityOrder = new CityOrderWeights();

        #endregion

        // 【已删除】人才部署（deployment / DeploymentWeights）与资源调度（resourceDispatch /
        // ResourceDispatchWeights）两组配置字段随各自系统一并移除。
        // 人员调动与物资运输回到原版命令：移动命令 / 召唤命令 / district_move /
        // 运输命令 / 支援命令，其判定参数读原版同源的数据（据点状态、最小人数、
        // 收支储备线），不再有独立的"部署权重"表。

        #region 内政建筑规划（动态目标构成）

        /// <summary>
        /// AI 内政建筑规划的全部权重与阈值（份额权重 / 港口修正 / 排除名单）。
        /// 可通过 <c>Data/Common/AIConfig.json</c> 的 <c>"buildingPlan"</c> 节点做部分覆盖。
        ///
        /// 取代原来的固定建造队列（<c>CityAI.CityBuildingTemplate</c>）：按"逐格贪心 + 边际递减"
        /// 动态决定下一个建什么，使各类建筑的产出比例随势力需求 / 城池条件漂移。
        /// </summary>
        public BuildingPlanWeights buildingPlan = new BuildingPlanWeights();

        #endregion

        /// <summary>
        /// 取得指定态势档位的权重集合。
        /// </summary>
        /// <param name="tier">态势档位</param>
        /// <returns>档位权重（永不为 null）</returns>
        public TroopTierWeights GetTierWeights(TroopBattleTier tier)
        {
            switch (tier)
            {
                case TroopBattleTier.Decisive: return tierDecisive;
                case TroopBattleTier.Advantaged: return tierAdvantaged;
                case TroopBattleTier.Even: return tierEven;
                case TroopBattleTier.Disadvantaged: return tierDisadvantaged;
                default: return tierCritical;
            }
        }

        /// <summary>
        /// 取各个态势档位权重字段中的最小值（用于配置校验，防止某档缺失导致空引用）。
        /// </summary>
        /// <returns>是否存在为 null 的档位权重</returns>
        public bool HasNullTierWeights()
        {
            return tierCritical == null || tierDisadvantaged == null || tierEven == null
                || tierAdvantaged == null || tierDecisive == null;
        }

        /// <summary>
        /// 从 <c>Data/Common/AIConfig.json</c> 加载覆盖参数。
        /// 文件不存在或解析失败时保持内置默认值。
        /// </summary>
        /// <returns>配置实例</returns>
        static AIConfig Load()
        {
            AIConfig config = new AIConfig();
            try
            {
                ModManager.Instance.LoadFile("Data/Common/AIConfig.json", file =>
                {
                    string json = File.ReadAllText(file);
                    AIConfig loaded = JsonConvert.DeserializeObject<AIConfig>(json);
                    if (loaded != null)
                        config = loaded;
                });
            }
            catch (System.Exception e)
            {
                Sango.Log.Warning("AIConfig 加载失败，使用内置默认参数：" + e.Message);
            }
            return config;
        }
    }
}
