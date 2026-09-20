/*
 * 文件名：AIConfig.cs
 * 描述：AI 参数集中配置。所有 AI 决策阈值 / 权重集中于此，
 *       支持通过 Data/Common/AIConfig.json 覆盖（同样支持 Mod），未配置项使用内置默认值。
 */

using System.IO;
using Sango.Mod;
using TKNewtonsoft.Json;

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

        #region 城市防守

        /// <summary>判定"兵临城下"的距离（格）。此范围内存在存活敌人时，城市优先出城防守</summary>
        public int cityDefenseAlertRange = 15;
        /// <summary>实时扫描城池周边以兜底检测敌人的范围（格）。用于预计算敌人信息未覆盖的场景（如敌军刚抵达、正在攻击本城建筑）</summary>
        public int cityDefenseScanRange = 10;

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

        /// <summary>进攻他城的基础出兵上限（支部队）</summary>
        public int attackBaseTroopCount = 12;
        /// <summary>目标城池每多少兵力追加 1 支出兵</summary>
        public int attackTroopsPerTargetTroops = 3000;
        /// <summary>进攻无主白城的出兵上限</summary>
        public int attackWhiteCityTroopCount = 2;

        #endregion

        #region 城市内政

        /// <summary>建设 / 升级建筑所需的城池最低金钱</summary>
        public int buildMinGold = 500;
        /// <summary>治安 / 训练等内政作业所需的城池最低金钱</summary>
        public int internalMinGold = 400;
        /// <summary>造兵装所需的最低金钱</summary>
        public int createItemsMinGold = 1000;
        /// <summary>造船 / 造器械所需的最低金钱</summary>
        public int createMachineMinGold = 1500;
        /// <summary>买粮所需保留的最低金钱（低于此值不买粮）</summary>
        public int tradeFoodKeepGold = 2000;
        /// <summary>运输队所需的本城最低兵力</summary>
        public int transportMinTroops = 10000;
        /// <summary>运输队所需的本城最低粮草</summary>
        public int transportMinFood = 20000;
        /// <summary>向所属城运输时的金钱警戒线（低于该值改为迁人）</summary>
        public int belongTransportGoldLine = 2500;
        /// <summary>向所属城运输时的粮草警戒线（低于该值改为迁人）</summary>
        public int belongTransportFoodLine = 20000;
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
        /// <summary>招募在野武将的单回合人数上限</summary>
        public int recruitPersonMaxPerTurn = 1;
        /// <summary>士气低于该值时必定训练</summary>
        public int cityMoraleLow = 50;
        /// <summary>训练概率的基准士气值（与当前士气之差线性折算概率）</summary>
        public int trainMoraleBase = 95;

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

        #region 城池命令优先级（动态排序）

        /// <summary>是否启用动态城池命令排序（关闭则回退到各内政系统内的硬编码顺序）</summary>
        public bool useDynamicCityOrder = true;
        /// <summary>
        /// 城池命令优先级的全部权重与阈值（基础分 / 态势阈值 / 评分分值 / 个性修正 / 紧急覆盖）。
        /// 可通过 Data/Common/AIConfig.json 的 "cityOrder" 节点做部分覆盖。
        /// </summary>
        public CityOrderWeights cityOrder = new CityOrderWeights();

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
