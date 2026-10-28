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
using System.Collections.Generic;

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
        /// 性格编号（0~3），对应原版 Open311 的 <c>seikaku_t</c>
        /// （s11_enum_scen.h:387-402：0 胆小 Shoushin / 1 冷静 Reisei / 2 刚胆 Goutan / 3 莽撞 Chototsu）。
        ///
        /// 【为什么放在性格表上】原版这是**武将**字段 <c>Person::seikaku_</c>（s11_scen.h:1763），
        /// 本项目沿"性格决定行为"的数据组织把它收到这里；武将侧通过
        /// <c>Person.Seikaku</c> 读取，未配置时按 <see cref="kind"/> 自动折算（kind 1~4 → seikaku 0~3）。
        ///
        /// 【谁在用】据点进攻 AI 的出征门槛（s11_game_ai_kyoten_cmd_mil_attack.cpp:38-70：
        /// 留守人数/兵力、所需兵力系数 <c>r = (3 - seikaku) * 15 + power_rank / 2</c>）
        /// 与跨区进攻概率（同文件 :136）。
        ///
        /// -1 = 未配置，走 kind 折算。
        /// </summary>
        public int seikaku = -1;

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

        /// <summary>
        /// 势力 AI 风格（<c>ForceAI.AIPersonalityType</c> 的数值）。
        ///
        /// -1 = 未指定 → 按 war/defense/diplomacy/economic 四个倾向字段的**最大值**自动推导（旧行为）；
        ///  0 侵略 / 1 防御 / 2 外交 / 3 经济 / 4 平衡 = 显式指定，优先于自动推导。
        ///
        /// 【为什么要它】旧实现是"取 4 个倾向的最大值，并列就算平衡"——
        /// 新增性格想表达"好战 + 重商"这种组合时无从下手（要么被最大值吃掉，要么并列变平衡）。
        /// 显式字段让性格数据自己决定风格，倾向字段退回纯数值权重。
        /// </summary>
        public int aiStyle = -1;

        /// <summary>战争倾向加成（影响 ForceAI 的侵略性判定）</summary>
        public int warTendencyAdd;

        /// <summary>防御倾向加成（影响 ForceAI 的守备偏好判定）</summary>
        public int defenseTendencyAdd;

        /// <summary>外交倾向加成（影响外交官评分与结盟意愿）</summary>
        public int diplomacyTendencyAdd;

        /// <summary>
        /// 劝降抗性档位（原版 try_annex 里的"目标性格"系数）。
        ///
        /// 【原版出处】s11_sys_scen_ai.cpp 的 try_annex 评分段：
        ///     g = 目标性格 × 7 - like / 4
        /// 原版性格是 0~3 的连续档位，值越大越难被劝降。
        ///
        /// 【本项目取值】不读性格枚举，直接在本字段填"档位值"：
        ///     胆小 = 0、冷静 = 1、刚胆 = 2、莽撞 = 3（与原版 Seikaku 0~3 等价）。
        /// 新增性格可自由填任意整数（甚至负数），不需要映射到四档。
        /// </summary>
        public int annexResistance;

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

        /// <summary>
        /// 部队好战度（-100~100）。现在**只用于"覆盖强度"**：
        /// 绝对值越大越容易覆盖任务角色（阈值见 AIConfig.leaderRoleOverrideThreshold）。
        /// 具体覆盖成什么角色由 <see cref="preferTroopRole"/> 决定，不再由本字段的正负号猜测。
        /// </summary>
        public int troopAggression;

        /// <summary>
        /// 性格倾向的部队角色（<see cref="TroopRole"/> 的数值）。
        ///
        /// -1 = 不指定（保持任务角色）；0 攻坚 / 1 防守 / 2 骚扰 / 3 游击 / 4 护卫 / 5 守备 / 6 工兵。
        ///
        /// 【为什么要它】旧实现是 `troopAggression > 0 ? 攻坚 : 防守` —— 把数值符号当成枚举，
        /// 想表达"好战但偏游击"或"中立好战"时无能为力。改成显式字段后，
        /// 任何新性格都能自由指定倾向角色。
        /// </summary>
        public int preferTroopRole = -1;

        // ==================== 原版 Seikaku 语义（用扩展字段表达，不认枚举值） ====================
        //
        // 原版 calc_strategy_chance / get_target_action 里按 SeikakuEnum（4 档：小心/冷静/强胆/猪突）
        // 查表。本项目**不引入这个枚举**，而是把"那一档带来的数值效果"直接变成可配字段：
        // 新增任何性格时，按需填这几个字段即可，不需要改代码、也不需要跟原版的四档一一对应。

        /// <summary>
        /// 计略倾向里**智力**的权重（百分比，100 = 加一次）。
        ///
        /// 【原版对应】s11_sys_unit_action.cpp:29-31，小心性格额外再加一次智力
        /// （即基础 100% + 额外 100% = 200%）。本项目把"基础"和"额外"合并成一个百分比：
        ///   小心 = 200、其它 = 100。
        /// </summary>
        public int strategyIntelligencePercent = 100;

        /// <summary>
        /// 普攻/战法倾向里**统率**的额外权重（百分比，0 = 不加）。
        ///
        /// 【原版对应】s11_sys_unit_action.cpp:32-34，猪突性格额外再加一次统率+武力。
        /// 猪突 = 100、其它 = 0。
        /// </summary>
        public int attackExtraCommandPercent;

        /// <summary>
        /// 普攻/战法倾向里**武力**的额外权重（百分比，0 = 不加）。
        ///
        /// 【原版对应】s11_sys_unit_action.cpp:36-39，强胆额外加一次武力；
        /// 猪突在 :32-34 里也包含武力，所以猪突 = 100、强胆 = 100、其它 = 0。
        /// </summary>
        public int attackExtraStrengthPercent;

        /// <summary>
        /// 是否"莽撞型"（原版 Seikaku_Goutan ~ Seikaku_Chototsu 区间的语义）。
        ///
        /// 【用途】落雷目标位置评分里的"低智莽夫豁免"（s11_sys_anim_unit_strategy.cpp:1615）：
        /// 莽撞型若位置分只有 1 则放弃；AI 的冒险倾向判定也读它。
        /// </summary>
        public bool isReckless;

        /// <summary>
        /// 是否"猪突"（原版 Seikaku_Chototsu）。
        ///
        /// 【用途】部队 AI 的必撤豁免：原版 s11_game_ai_unit.cpp:133-138 里
        /// "兵力 ≤ 500 必撤"只对**非猪突**生效，猪突部队不会因为兵少就撤。
        /// </summary>
        public bool isChototsu;

        /// <summary>部队行动择优池修正（负值 = 候选更少更果断，正值 = 更摇摆）</summary>
        public int troopBestNAdd;

        /// <summary>计略释放倾向倍率（%，影响部队选择计略技能的意愿）</summary>
        public int troopSkillScale = 100;

        /// <summary>部队角色覆盖阈值修正（加到全局阈值上，负值表示更易被性格覆盖）</summary>
        public int troopRoleOverrideAdd;

        // ==================== 4.5 舌战（Debate） ====================
        //
        // 原版舌战按 Seikaku 的四个枚举值写死了十几处行为分支。本项目全部改成字段：
        // 新增性格时按需填写，不需要改 Debate.cs / DebateAI.cs。

        /// <summary>
        /// 舌战风格档位（0 胆小 / 1 冷静 / 2 刚胆 / 3 莽撞），**仅供展示与兼容**。
        ///
        /// 业务分支请读下方的具名字段（debateAngerTimer / debateCountsAsAngered / ...），
        /// 本字段只用于 UI 显示性格名、以及旧代码的兼容回退。
        /// </summary>
        public int debateStyle = -1;

        /// <summary>兴奋（愤怒）持续回合数。原版：胆小/莽撞 1，冷静/刚胆 4。</summary>
        public int debateAngerTimer = 4;

        /// <summary>
        /// 兴奋期间是否进入"真愤怒"状态（出牌受限）。
        /// 原版只有冷静/刚胆为真；胆小/莽撞虽然 timer=1，但只用于触发瞬间效果。
        /// </summary>
        public bool debateCountsAsAngered;

        /// <summary>
        /// 真愤怒时是否**不受**出牌限制。
        /// 原版仅冷静为真（愤怒时仍可出所有牌）。
        /// </summary>
        public bool debateCanActWhileAngered;

        /// <summary>
        /// 愤怒瞬间对对手造成的体力伤害的**基础值**（另加己方武力）。
        /// 原版：莽撞 = 200；其它 = 0（不触发）。
        /// </summary>
        public int debateAngerHpDamageBase;

        /// <summary>愤怒瞬间是否触发连续攻击。原版：胆小 = true。</summary>
        public bool debateAngerCombo;

        /// <summary>愤怒时是否额外获得一张 3 阶话题卡（几率 %）。原版：冷静 = 40。</summary>
        public int debateAngerFreeTopicCardChance;

        /// <summary>AI：兴奋时是否优先出最低阶话题卡。原版：刚胆。</summary>
        public bool debateAiPrefersLowestTopic;

        /// <summary>AI：兴奋时是否倾向"再考"。原版：冷静。</summary>
        public bool debateAiPrefersRethink;

        /// <summary>AI：兴奋时是否倾向"喊话"。原版：冷静。</summary>
        public bool debateAiAngerPrefersShout;

        /// <summary>AI：对手愤怒且为冷静性格时，是否倾向"无视"。原版：胆小。</summary>
        public bool debateAiIgnoreWhenOpposingCalm;

        /// <summary>AI：对手愤怒且为刚胆性格时，是否转为激进出牌。原版：刚胆对手。</summary>
        public bool debateAiAggressiveVsAngered;

        /// <summary>愤怒时是否允许"再考"。原版：冷静 = true。</summary>
        public bool debateAngerAllowsRethink;

        /// <summary>愤怒时的体力伤害"话题系数"基础值。原版：胆小/刚胆 = 10，其它 = 6。</summary>
        public int debateAngerTopicCoef = 6;

        /// <summary>愤怒时的体力伤害"愤怒系数"基础值。原版：冷静 = 15，其它 = 10。</summary>
        public int debateAngerDamageCoef = 10;

        /// <summary>愤怒时出牌威力直接覆盖为该值（0 = 不覆盖）。原版：刚胆 = 100。</summary>
        public int debateAngerPowerOverride;

        /// <summary>愤怒时是否把手中话题卡降为最低阶。原版：刚胆。</summary>
        public bool debateAngerLowestTopicCards;

        // ==================== 4.6 单挑（Duel） ====================

        /// <summary>
        /// 单挑"最低参战体力"：体力低于此值的武将不再上场（原版 GetDuelMinHp）。
        /// 原版按性格 胆小 80 / 冷静 70 / 刚胆 60 / 莽撞 50，现由数据直接给。
        /// </summary>
        public int duelMinHp = 50;

        /// <summary>
        /// 单挑 AI 决策表类型（<c>DuelAIType</c> 的数值：0 胆小 / 1 冷静 / 2 刚胆 / 3 莽撞）。
        ///
        /// -1 = 未指定 → 回落到"性格 kind - 1"的旧映射（仅为兼容旧存档/旧数据）。
        /// 新性格建议显式填写，这样加性格时不必保证 kind 的连续顺序。
        /// </summary>
        public int duelAiType = -1;

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

        // ==================== 9. 战略执念（城市 / 宝物） ====================

        /// <summary>
        /// 执念城市 Id 列表：只要这些城被别的势力占着，就构成开战理由。
        ///
        /// 对齐出处：Open311 `s11_game_ai_force.cpp:614-698` 的 get_city_priority
        /// —— 原版是一张写死的"君主 → 城"表（袁绍执念邺城 / 曹操执念许昌 / 孙权执念建业 …）。
        /// 这里改成**性格内嵌属性**：同一套性格数据可以被多个武将复用，
        /// 新增"袁绍"这种执念只需在 Json 里给对应性格填上城 Id，无需改代码。
        ///
        /// 取值语义（与原版 get_city_priority 的返回值一致）：
        ///   · 城在此列 → 该城对该势力构成"必争之地"，AI 会因此把对方列为主要敌人。
        /// </summary>
        [JsonProperty]
        public List<int> obsessionCityIds = new List<int>();

        /// <summary>
        /// 喜爱宝物 Id 列表：君主/武将持有时优先自己留用，不轻易赏赐或送出；
        /// 势力级授宝（原版 force_reward）会优先把列在这里的宝物授给"该性格的持有者"。
        ///
        /// 对齐出处：Open311 `s11_game_ai_force_cmd.cpp:414-461` 的 force_reward / need_reward
        /// —— 原版靠 `ItemOwnerPreset` 这张写死的"武将-宝物"表；这里同样改成性格属性，
        /// 于是"关羽喜欢青龙偃月刀"只需给关羽的性格填一个宝物 Id。
        /// </summary>
        [JsonProperty]
        public List<int> favoriteItemIds = new List<int>();

        /// <summary>
        /// 该城的执念优先级（0 = 无执念）。
        ///
        /// 对齐出处：s11_game_ai_force.cpp:614-698 get_city_priority 的返回值语义
        /// （原版 0 / 1 / 2 / 4 四档，≥2 会直接触发开战）。本实现只区分"是否执念"，
        /// 命中即返回 2（与原版"≥2 就开战"的阈值一致）。
        /// </summary>
        /// <param name="cityId">城市 Id</param>
        /// <returns>0 或 2</returns>
        public int GetCityPriority(int cityId)
        {
            if (obsessionCityIds == null || obsessionCityIds.Count == 0)
                return 0;
            for (int i = 0; i < obsessionCityIds.Count; i++)
            {
                if (obsessionCityIds[i] == cityId)
                    return 2;
            }
            return 0;
        }

        /// <summary>
        /// 是否为该性格"喜爱的宝物"。
        /// </summary>
        /// <param name="itemId">宝物 Id</param>
        /// <returns>是否喜爱</returns>
        public bool IsFavoriteItem(int itemId)
        {
            if (favoriteItemIds == null || favoriteItemIds.Count == 0)
                return false;
            for (int i = 0; i < favoriteItemIds.Count; i++)
            {
                if (favoriteItemIds[i] == itemId)
                    return true;
            }
            return false;
        }

        // ==================== 10. 预留（3 项） ====================

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
