/*
 * 文件名：CityOrderWeights.cs
 * 描述：城池 AI 命令优先级的全部权重与阈值配置。
 *       可通过 Data/Common/AIConfig.json 中的 "cityOrder" 节点进行部分覆盖
 *       （反序列化会先执行字段初始化器，因此未在 JSON 中出现的字段保持默认值）。
 */

namespace Sango.Core
{
    /// <summary>
    /// 城池命令优先级权重配置。
    ///
    /// 分组说明：
    /// 1. 基础权重（base*）      —— 每条命令的起始分；
    /// 2. 态势阈值（*Fill / *Critical ...） —— 评分规则里用到的判定边界；
    /// 3. 评分分值（score*）     —— 态势规则命中时叠加的分值；
    /// 4. 个性修正（personality*） —— 不同势力个性的倾向；
    /// 5. 紧急覆盖（siege*/crisis*/peace*） —— 战时 / 危机 / 和平态的强制偏移。
    /// </summary>
    public class CityOrderWeights
    {
        // ==================================================================
        // 一、基础权重（每条命令的起始分）
        // ==================================================================

        /// <summary>军事（进攻/防守/驱逐/夺回港关）基础分</summary>
        public int baseAIAttack = 60;
        /// <summary>支援邻城基础分</summary>
        public int baseAIReinforce = 40;
        /// <summary>买粮基础分</summary>
        public int baseAITradeFood = 30;
        /// <summary>内政建设基础分</summary>
        public int baseAIIntrior = 50;
        /// <summary>治安基础分</summary>
        public int baseAISecurity = 30;
        /// <summary>训练基础分</summary>
        public int baseAITrainTroop = 25;
        /// <summary>褒奖武将基础分</summary>
        public int baseAIRewardPerson = 20;
        /// <summary>募兵基础分</summary>
        public int baseAIRecruitTroop = 40;
        /// <summary>造兵装基础分</summary>
        public int baseAICreateItems = 40;
        /// <summary>造器械基础分</summary>
        public int baseAICreateMachine = 20;
        /// <summary>造船基础分</summary>
        public int baseAICreateBoat = 20;
        /// <summary>搜索基础分</summary>
        public int baseAISearching = 25;
        /// <summary>招募武将基础分</summary>
        public int baseAIRecruitPerson = 25;
        /// <summary>跨城运输基础分</summary>
        public int baseAITransfrom = 30;
        /// <summary>向所属城运输基础分</summary>
        public int baseAITransfromToBelongCity = 35;
        /// <summary>科技研发基础分</summary>
        public int baseAIResearch = 25;
        /// <summary>组建补给队基础分</summary>
        public int baseAIMakeSupplyTroop = 30;

        // ==================================================================
        // 二、态势阈值
        // ==================================================================

        /// <summary>充盈度阈值：严重不足</summary>
        public float fillCritical = 0.2f;
        /// <summary>充盈度阈值：偏低</summary>
        public float fillLow = 0.5f;
        /// <summary>充盈度阈值：充足</summary>
        public float fillHigh = 0.7f;
        /// <summary>充盈度阈值：已满</summary>
        public float fillFull = 0.9f;
        /// <summary>治安阈值：崩坏</summary>
        public int securityCritical = 30;
        /// <summary>治安阈值：偏低</summary>
        public int securityLow = 50;
        /// <summary>治安阈值：尚可</summary>
        public int securityOk = 70;
        /// <summary>士气阈值：低落</summary>
        public int moraleLow = 40;
        /// <summary>士气阈值：尚可</summary>
        public int moraleOk = 60;
        /// <summary>兵力阈值：低位（不足）</summary>
        public float troopLowFill = 0.3f;
        /// <summary>兵力阈值：中位（偏低）</summary>
        public float troopMidFill = 0.6f;
        /// <summary>兵力阈值：高位（充足）</summary>
        public float troopHighFill = 0.7f;
        /// <summary>兵装目标覆盖率（兵装 / 兵力），低于该比例视为兵装不足</summary>
        public float weaponCoverRatio = 0.5f;
        /// <summary>褒奖武将所需的金钱门槛</summary>
        public int rewardGoldThreshold = 1000;
        /// <summary>研发科技所需的金钱门槛</summary>
        public int researchGoldThreshold = 5000;
        /// <summary>器械库存"已足够"的判定值</summary>
        public int machineEnough = 60;
        /// <summary>兵装枯竭危机判定所需的最低兵力</summary>
        public int weaponCrisisTroops = 1000;
        /// <summary>断粮危机判定的充盈度阈值</summary>
        public float crisisFoodFill = 0.15f;

        // ==================================================================
        // 三、评分分值
        // ==================================================================

        /// <summary>AIAttack：边境城市加成</summary>
        public int scoreAttackBorderBonus = 40;
        /// <summary>AIAttack：兵力充盈加成</summary>
        public int scoreAttackTroopHighBonus = 30;
        /// <summary>AIAttack：兵力空虚惩罚</summary>
        public int scoreAttackTroopLowPenalty = 40;
        /// <summary>AIAttack：无将可用惩罚</summary>
        public int scoreAttackNoPersonPenalty = 50;
        /// <summary>AIAttack：被围时的降权（避免与紧急覆盖叠加过度）</summary>
        public int scoreAttackSiegePenalty = 30;

        /// <summary>AIReinforce：每个被围邻城的加成</summary>
        public int scoreReinforcePerNeighbor = 50;
        /// <summary>AIReinforce：兵力有余的加成</summary>
        public int scoreReinforceTroopBonus = 20;
        /// <summary>AIReinforce：本城被围时的重罚</summary>
        public int scoreReinforceSelfSiegePenalty = 100;

        /// <summary>AITradeFood：严重缺粮加成</summary>
        public int scoreFoodCriticalBonus = 80;
        /// <summary>AITradeFood：粮草偏少加成</summary>
        public int scoreFoodLowBonus = 40;
        /// <summary>AITradeFood：粮草正常时的默认加成</summary>
        public int scoreFoodDefaultBonus = 15;

        /// <summary>AIIntrior：基础加成</summary>
        public int scoreInternalDefault = 10;
        /// <summary>AIIntrior：被围惩罚</summary>
        public int scoreInternalSiegePenalty = 60;
        /// <summary>AIIntrior：边境惩罚</summary>
        public int scoreInternalBorderPenalty = 20;
        /// <summary>AIIntrior：资源充裕加成</summary>
        public int scoreInternalRichBonus = 20;

        /// <summary>AISecurity：治安崩坏加成</summary>
        public int scoreSecurityCriticalBonus = 70;
        /// <summary>AISecurity：治安偏低加成</summary>
        public int scoreSecurityLowBonus = 40;

        /// <summary>AITrainTroop：士气低落加成</summary>
        public int scoreMoraleLowBonus = 60;
        /// <summary>AITrainTroop：士气尚可加成</summary>
        public int scoreMoraleOkBonus = 35;
        /// <summary>AITrainTroop：默认加成</summary>
        public int scoreMoraleDefaultBonus = 5;

        /// <summary>AIRewardPerson：基础加成</summary>
        public int scoreRewardBase = 10;
        /// <summary>AIRewardPerson：金钱充裕加成</summary>
        public int scoreRewardGoldBonus = 10;

        /// <summary>AIRecruitTroop：兵力严重不足加成</summary>
        public int scoreRecruitTroopCriticalBonus = 70;
        /// <summary>AIRecruitTroop：兵力偏低加成</summary>
        public int scoreRecruitTroopLowBonus = 40;
        /// <summary>AIRecruitTroop：治安偏低惩罚</summary>
        public int scoreRecruitSecurityPenalty = 30;

        /// <summary>AICreateItems：兵装严重不足加成</summary>
        public int scoreWeaponCriticalBonus = 50;
        /// <summary>AICreateItems：兵装偏低加成</summary>
        public int scoreWeaponLowBonus = 25;

        /// <summary>AICreateMachine：器械充足惩罚</summary>
        public int scoreMachineEnoughPenalty = 30;
        /// <summary>AICreateMachine：正常加成</summary>
        public int scoreMachineBonus = 40;

        /// <summary>AICreateBoat：拥有港口加成</summary>
        public int scoreBoatPortBonus = 15;
        /// <summary>AICreateBoat：无港口惩罚</summary>
        public int scoreBoatPenalty = 10;

        /// <summary>AISearching：无人可派惩罚</summary>
        public int scoreSearchingNoPersonPenalty = 50;
        /// <summary>AISearching：正常加成</summary>
        public int scoreSearchingBonus = 15;

        /// <summary>AIRecruitPerson：无人可派惩罚</summary>
        public int scoreRecruitPersonNoPersonPenalty = 30;
        /// <summary>AIRecruitPerson：正常加成</summary>
        public int scoreRecruitPersonBonus = 20;

        /// <summary>AITransfrom：有富余加成</summary>
        public int scoreTransfromRichBonus = 40;
        /// <summary>AITransfrom：默认加成</summary>
        public int scoreTransfromDefaultBonus = 5;
        /// <summary>AITransfromToBelongCity：固定加成</summary>
        public int scoreTransfromToBelongCityBonus = 30;

        /// <summary>AIResearch：被围惩罚</summary>
        public int scoreResearchSiegePenalty = 60;
        /// <summary>AIResearch：金钱充裕加成</summary>
        public int scoreResearchGoldBonus = 30;
        /// <summary>AIResearch：默认加成</summary>
        public int scoreResearchDefaultBonus = 10;

        /// <summary>AIMakeSupplyTroop：满足配比门槛加成</summary>
        public int scoreSupplyBonus = 60;
        /// <summary>AIMakeSupplyTroop：不满足门槛惩罚</summary>
        public int scoreSupplyPenalty = 50;

        // ==================================================================
        // 四、个性修正
        // ==================================================================

        /// <summary>侵略型：军事 / 募兵加成</summary>
        public int personalityAggressiveMilitaryBonus = 20;
        /// <summary>侵略型：内政降权</summary>
        public int personalityAggressiveInternalPenalty = 20;
        /// <summary>防御型：军事 / 支援加成</summary>
        public int personalityDefensiveMilitaryBonus = 15;
        /// <summary>防御型：内政加成</summary>
        public int personalityDefensiveInternalBonus = 10;
        /// <summary>经济型：内政 / 经济加成</summary>
        public int personalityEconomicInternalBonus = 25;
        /// <summary>经济型：军事降权</summary>
        public int personalityEconomicMilitaryPenalty = 25;
        /// <summary>外交型：人事加成</summary>
        public int personalityDiplomaticPersonBonus = 20;

        // ==================================================================
        // 五、紧急覆盖
        // ==================================================================

        /// <summary>战时·本城被围：军事命令加成</summary>
        public int siegeDefenseBias = 200;
        /// <summary>战时·本城被围：内政命令整体降权</summary>
        public int siegeInternalPenalty = 80;
        /// <summary>存在被围邻城：支援命令加成</summary>
        public int reinforceBias = 80;
        /// <summary>存在威胁部队：军事命令加成</summary>
        public int threatBias = 100;
        /// <summary>下属港关丢失：军事命令加成</summary>
        public int subCityBias = 60;
        /// <summary>断粮危机：买粮命令加成</summary>
        public int crisisFoodBias = 150;
        /// <summary>治安危机：治安命令加成</summary>
        public int crisisSecurityBias = 100;
        /// <summary>兵装枯竭：造兵装命令加成</summary>
        public int crisisWeaponBias = 80;
        /// <summary>和平发展：内政 / 运输 / 科技加成</summary>
        public int peaceInternalBias = 30;
        /// <summary>和平发展：军事命令降权</summary>
        public int peaceMilitaryPenalty = 40;
    }
}
