/*
 * 文件名：Personality.cs
 * 描述：武将性格数据。集中承载"性格可能影响的一切"：
 *       计略抗性 / 计略专精 / 势力倾向 / 部队作战 / 单挑 / 内政 / 外交人事 / 成长忠诚。
 *
 * 设计约定：
 *   1. 全部使用**扁平具名字段**，便于 JSON 直读与编辑器绑定；
 *   2. 倍率类字段（*Scale）默认 100 = 不修正；加值类字段（*Add）默认 0 = 不修正；
 *      因此老数据或未填字段不会产生异常行为；
 *   3. 新增性格 = 新增一条 JSON 数据，**无需改动任何代码**。
 */

using Newtonsoft.Json;

namespace Sango.Core
{
    public class Personality : SangoObject
    {
        // ==================== 0. 基础标识 ====================

        /// <summary>
        /// 性格类型编号（1 胆小 / 2 冷静 / 3 刚胆 / 4 莽撞，新增性格顺延）。
        /// 仅用于分类与排序，行为完全由下方各字段决定。
        /// </summary>
        public int kind;

        /// <summary>
        /// 性格描述（UI 显示用，选填）。
        /// </summary>
        public string desc;

        // ==================== 1. 计略抗性（作为目标，6 项） ====================

        /// <summary>作为目标对[伪报]的成功率加成</summary>
        public int falseReportSuccessAdd;

        /// <summary>作为目标对[扰乱]的成功率加成</summary>
        public int disturbSuccessAdd;

        /// <summary>作为目标对[镇静]的成功率加成</summary>
        public int calmdownSuccessAdd;

        /// <summary>作为目标对[伏兵]的成功率加成</summary>
        public int ambushSuccessAdd;

        /// <summary>作为目标对[妖术]的成功率加成（技能表暂缺妖术，保留占位）</summary>
        public int sorcerySuccessAdd;

        /// <summary>作为目标对[内讧]的成功率加成</summary>
        public int infightingSuccessAdd;

        // ==================== 2. 计略专精（作为施法者，6 项） ====================

        /// <summary>作为主将释放[伪报]的暴击率加成</summary>
        public int falseReportCriticalAdd;

        /// <summary>作为主将释放[扰乱]的暴击率加成</summary>
        public int disturbCriticalAdd;

        /// <summary>作为主将释放[镇静]的暴击率加成</summary>
        public int calmdownCriticalAdd;

        /// <summary>作为主将释放[伏兵]的暴击率加成</summary>
        public int ambushCriticalAdd;

        /// <summary>作为主将释放[妖术]的暴击率加成（技能表暂缺妖术，保留占位）</summary>
        public int sorceryCriticalAdd;

        /// <summary>作为主将释放[内讧]的暴击率加成</summary>
        public int infightingCriticalAdd;

        // ==================== 3. 势力倾向（8 项） ====================

        /// <summary>战争倾向加成（影响 ForceAI 的侵略性判定）</summary>
        public int warTendencyAdd;

        /// <summary>防御倾向加成（影响 ForceAI 的守备偏好判定）</summary>
        public int defenseTendencyAdd;

        /// <summary>外交倾向加成（影响外交官评分与结盟意愿）</summary>
        public int diplomacyTendencyAdd;

        /// <summary>经济发展倾向加成</summary>
        public int economicTendencyAdd;

        /// <summary>科技研发倾向加成</summary>
        public int technologyTendencyAdd;

        /// <summary>俘虏招降倾向加成</summary>
        public int recruitCaptiveTendencyAdd;

        /// <summary>俘虏释放倾向加成</summary>
        public int releaseCaptiveTendencyAdd;

        /// <summary>俘虏赎回倾向加成</summary>
        public int ransomCaptiveTendencyAdd;

        // ==================== 4. 部队作战（7 项） ====================

        /// <summary>部队攻击收益倍率（%，100 = 不修正）</summary>
        public int troopAttackScale = 100;

        /// <summary>部队反击惩罚倍率（%，越高越规避"贴脸打高反击单位"）</summary>
        public int troopCounterScale = 100;

        /// <summary>部队撤退概率修正（%，加到态势档位的撤退概率上）</summary>
        public int troopRetreatAdd;

        /// <summary>部队好战度（-100~100）：正值倾向覆盖为攻坚，负值倾向覆盖为防守</summary>
        public int troopAggression;

        /// <summary>部队行动择优池修正（负值 = 候选更少更果断，正值 = 更摇摆）</summary>
        public int troopBestNAdd;

        /// <summary>计略释放倾向倍率（%，影响部队选择计略技能的意愿）</summary>
        public int troopSkillScale = 100;

        /// <summary>部队角色覆盖阈值修正（加到全局阈值上，负值表示更易被性格覆盖）</summary>
        public int troopRoleOverrideAdd;

        // ==================== 5. 单挑（2 项） ====================

        /// <summary>单挑应战概率修正（%）</summary>
        public int duelAcceptAdd;

        /// <summary>单挑中脱战倾向（%），正值表示更倾向脱离单挑</summary>
        public int duelRetreatAdd;

        // ==================== 6. 内政专有参数（13 项） ====================
        // 对应 CityJobType 的各个内政项目，作用于 GameUtility.Method_XXX 的折算输出。
        // 多人执行时取所有执行武将该系数的平均值。

        /// <summary>农业开发效率（%）—— JobFarming</summary>
        public int domesticFarmingScale = 100;

        /// <summary>商业开发效率（%）—— JobDevelop</summary>
        public int domesticDevelopScale = 100;

        /// <summary>治安巡视效率（%）—— JobInspection</summary>
        public int domesticSecurityScale = 100;

        /// <summary>训练效率（%）—— JobTrainTroops</summary>
        public int domesticTrainScale = 100;

        /// <summary>搜索效率（%）—— JobSearching</summary>
        public int domesticSearchScale = 100;

        /// <summary>招募士兵效率（%）—— JobRecruitTroop</summary>
        public int domesticRecruitTroopScale = 100;

        /// <summary>招募武将效率（%）—— JobRecruitPerson</summary>
        public int domesticRecruitPersonScale = 100;

        /// <summary>生产兵装效率（%）—— JobCreateItems</summary>
        public int domesticCreateItemScale = 100;

        /// <summary>生产兵器（器械）效率（%）</summary>
        public int domesticCreateMachineScale = 100;

        /// <summary>生产船效率（%）</summary>
        public int domesticCreateBoatScale = 100;

        /// <summary>建造效率（%）—— JobBuild</summary>
        public int domesticBuildScale = 100;

        /// <summary>粮草交易效率（%）—— JobTradeFood</summary>
        public int domesticTradeScale = 100;

        /// <summary>修缮建筑效率（%）</summary>
        public int domesticRepairScale = 100;

        // ==================== 7. 外交与人事（4 项） ====================

        /// <summary>送礼效果加成（%）</summary>
        public int giftEffectAdd;

        /// <summary>交涉 / 谈判修正（%）</summary>
        public int negotiationAdd;

        /// <summary>俘虏处置修正（%）</summary>
        public int captiveDealAdd;

        /// <summary>忠诚漂移修正（），正值使忠诚更稳定</summary>
        public int loyaltyDriftAdd;

        // ==================== 8. 成长与忠诚（4 项） ====================

        /// <summary>属性成长速度（%）</summary>
        public int growthScale = 100;

        /// <summary>技能 / 经验学习速度（%）</summary>
        public int learnSpeedScale = 100;

        /// <summary>忠诚维持加成（降低掉忠诚的概率）</summary>
        public int loyaltyKeepAdd;

        /// <summary>叛乱风险（负值表示更安定）</summary>
        public int revoltRiskAdd;

        // ==================== 9. 预留（3 项） ====================

        /// <summary>预留加值位</summary>
        public int reserved1;

        /// <summary>预留加值位</summary>
        public int reserved2;

        /// <summary>预留倍率位（%）</summary>
        public int reservedScale = 100;

        // ==================== 访问器 ====================

        /// <summary>
        /// 取得本性格对指定计略的**抗性加成**（作为目标时）。
        /// </summary>
        /// <param name="type">计略类型</param>
        /// <returns>成功率加成值；不参与性格表的计略返回 0</returns>
        public int GetSkillResistAdd(PersonalitySkillType type)
        {
            switch (type)
            {
                case PersonalitySkillType.FalseReport: return falseReportSuccessAdd;
                case PersonalitySkillType.Disturb: return disturbSuccessAdd;
                case PersonalitySkillType.Calmdown: return calmdownSuccessAdd;
                case PersonalitySkillType.Ambush: return ambushSuccessAdd;
                case PersonalitySkillType.Sorcery: return sorcerySuccessAdd;
                case PersonalitySkillType.Infighting: return infightingSuccessAdd;
                default: return 0;
            }
        }

        /// <summary>
        /// 取得本性格对指定计略的**专精加成**（作为施法者时）。
        /// </summary>
        /// <param name="type">计略类型</param>
        /// <returns>暴击率加成值；不参与性格表的计略返回 0</returns>
        public int GetSkillMasteryAdd(PersonalitySkillType type)
        {
            switch (type)
            {
                case PersonalitySkillType.FalseReport: return falseReportCriticalAdd;
                case PersonalitySkillType.Disturb: return disturbCriticalAdd;
                case PersonalitySkillType.Calmdown: return calmdownCriticalAdd;
                case PersonalitySkillType.Ambush: return ambushCriticalAdd;
                case PersonalitySkillType.Sorcery: return sorceryCriticalAdd;
                case PersonalitySkillType.Infighting: return infightingCriticalAdd;
                default: return 0;
            }
        }

        /// <summary>
        /// 取得指定内政项目的效率倍率（%）。
        /// </summary>
        /// <param name="jobType">内政项目</param>
        /// <returns>效率倍率（%），未定义时返回 100</returns>
        public int GetDomesticScale(CityJobType jobType)
        {
            switch (jobType)
            {
                case CityJobType.Farming: return domesticFarmingScale;
                case CityJobType.Develop: return domesticDevelopScale;
                case CityJobType.Inspection: return domesticSecurityScale;
                case CityJobType.TrainTroops: return domesticTrainScale;
                case CityJobType.Searching: return domesticSearchScale;
                case CityJobType.RecruitTroops: return domesticRecruitTroopScale;
                case CityJobType.RecruitPerson: return domesticRecruitPersonScale;
                case CityJobType.CreateItems: return domesticCreateItemScale;
                case CityJobType.CreateMachine: return domesticCreateMachineScale;
                case CityJobType.CreateBoat: return domesticCreateBoatScale;
                case CityJobType.Build: return domesticBuildScale;
                case CityJobType.TradeFood: return domesticTradeScale;
                default: return 100;
            }
        }

        /// <summary>
        /// 取得"修缮"专用的效率倍率（%，无对应 CityJobType 枚举项）。
        /// </summary>
        public int RepairScale { get { return domesticRepairScale; } }
    }
}
