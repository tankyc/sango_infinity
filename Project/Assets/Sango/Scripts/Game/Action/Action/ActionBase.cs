using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 动作基类
    /// </summary>
    public abstract class ActionBase
    {
        /// <summary>
        /// 初始化动作
        /// </summary>
        /// <param name="p">参数</param>
        /// <param name="sangoObjects">游戏对象</param>
        public abstract void Init(JObject p, params SangoObject[] sangoObjects);
        /// <summary>
        /// 清除动作
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// 执行动作
        /// </summary>
        public abstract void Execute(Trigger trigger);


        public virtual void OnForceTurnStart(Force force, Scenario scenario)
        {

        }

        /// <summary>
        /// 动作创建委托
        /// </summary>
        public delegate ActionBase ActionCreator();

        /// <summary>
        /// 动作创建映射
        /// </summary>
        public static Dictionary<string, ActionCreator> CreateMap = new Dictionary<string, ActionCreator>();
        /// <summary>
        /// 注册动作
        /// </summary>
        /// <param name="name">动作名称</param>
        /// <param name="action">动作创建器</param>
        public static void Register(string name, ActionCreator action)
        {
            CreateMap[name] = action;
        }

        /// <summary>
        /// 创建动作处理器
        /// </summary>
        /// <typeparam name="T">动作类型</typeparam>
        /// <returns>动作实例</returns>
        public static ActionBase CraeteHandle<T>() where T : ActionBase, new()
        {
            return new T();
        }
        /// <summary>
        /// 创建动作
        /// </summary>
        /// <param name="name">动作名称</param>
        /// <returns>动作实例</returns>
        public static ActionBase Create(string name)
        {
            ActionCreator actionBaseCreator;
            if (CreateMap.TryGetValue(name, out actionBaseCreator))
                return actionBaseCreator();
            return null;
        }

        /// <summary>
        /// 初始化所有动作
        /// </summary>
        public static void Init()
        {
            // 强化防卫：拥有该研究的势力，据点被攻击时自动反击伤害提高至 200%。
            Register("BuildingBaseAttackBack", CraeteHandle<BuildingBaseAttackBack>);
            // 强化城墙：拥有该研究的势力，城池、关卡和港口的耐久上限各增加 3000。
            Register("CityDurabilityLimit", CraeteHandle<CityDurabilityLimit>);
            // 培训工兵：拥有该研究的势力，城池/关/港每回合的耐久自然回复量按配置百分比提高
            // （原版 s11_game_week.cpp:943-953，回复量 ×2.5）。
            Register("CityDurabilityRecover", CraeteHandle<CityDurabilityRecover>);
            // 据点即时修复：研究或效果刚生效时，立即为所属城池、关卡和港口补充配置数值的耐久。
            Register("CityAddDurability", CraeteHandle<CityAddDurability>);
            // 粮仓扩建：计算粮食上限时，为符合类型的己方据点额外增加配置数值的储粮空间。
            Register("CityFoodLimit", CraeteHandle<CityFoodLimit>);
            // 金库扩建：计算资金上限时，为符合类型的己方据点额外增加配置数值的存钱空间。
            Register("CityGoldLimit", CraeteHandle<CityGoldLimit>);
            // 治安维护：每季结算治安下降时，按配置百分比减少符合类型的己方据点掉治安数值。
            Register("CitySecurityChange", CraeteHandle<CitySecurityChange>);
            // 仓库扩建：计算道具仓库上限时，为符合类型的己方据点额外增加配置数值的存放格数。
            Register("CityStoreLimit", CraeteHandle<CityStoreLimit>);
            // 兵营扩建：计算可驻扎兵力上限时，为符合类型的己方据点额外增加配置数值的兵力容量。
            Register("CityTroopsLimit", CraeteHandle<CityTroopsLimit>);
            // 熟练兵：拥有该研究的势力，所属据点的部队气力上限增加 20。
            Register("ForceCityMaxMorale", CraeteHandle<ForceCityMaxMorale>);
            // 势力忠诚保护：换季准备扣减忠诚前，按配置百分比调整该势力触发全体掉忠的概率。
            Register("ForcePersonLoyaltyChange", CraeteHandle<ForcePersonLoyaltyChange>);
            // 军制改革：拥有该研究的势力，所有部队的兵力上限增加 3000。
            Register("ForceTroopMaxTroop", CraeteHandle<ForceTroopMaxTroop>);
            // 势力能力标记：给掌握该技巧的势力打上 ForceAbilityFlags 的位
            // （如神火计 = DivineFire，业务侧据此判断"会不会这招"，而不是反查技巧列表）。
            Register("ForceAddAbilityFlag", CraeteHandle<ForceAddAbilityFlag>);
            // 精锐枪兵：拥有该研究的势力，枪兵部队攻击力增加 10。
            // 精锐戟兵：拥有该研究的势力，戟兵部队攻击力增加 10。
            // 精锐弩兵：拥有该研究的势力，弩兵部队攻击力增加 10。
            // 精锐骑兵：拥有该研究的势力，骑兵部队攻击力增加 10。
            Register("TroopAddAttack", CraeteHandle<TroopAddAttack>);
            // 攻城强化：计算部队战法对据点、设施和建筑的**耐久**伤害时，按配置百分比提高符合兵种的破坏力。
            Register("TroopAddDamageBuildingExtraFactor", CraeteHandle<TroopAddDamageBuildingExtraFactor>);
            // 攻城强化（守军）：计算部队对据点（城/关/港）**守军兵力**的伤害时，按配置百分比提高。
            // 云梯 = 耐久与守军两段同时 ×1.4（原版 calc_attack_damage 的 target_building 分支）。
            Register("TroopAddDamageBuildingTroopExtraFactor", CraeteHandle<TroopAddDamageBuildingTroopExtraFactor>);
            // 对军强化：计算部队战法对敌军的伤害时，按配置百分比提高符合兵种的战法威力。
            Register("TroopAddDamageTroopExtraFactor", CraeteHandle<TroopAddDamageTroopExtraFactor>);
            // 精锐枪兵：拥有该研究的势力，枪兵部队防御力增加 10。
            // 精锐戟兵：拥有该研究的势力，戟兵部队防御力增加 10。
            // 精锐弩兵：拥有该研究的势力，弩兵部队防御力增加 10。
            // 精锐骑兵：拥有该研究的势力，骑兵部队防御力增加 10。
            Register("TroopAddDefence", CraeteHandle<TroopAddDefence>);
            // 特技 #3 强行：提高非兵器、非输送陆上部队移动力。
            // 特技 #4 长驱：提高骑兵部队移动力。
            // 特技 #6 操舵：提高水上部队移动力。
            // 特技 #8 搬运：提高输送部队移动力。
            // 技巧 #4 精锐枪兵：枪兵部队移动力增加 10%。
            // 技巧 #8 精锐戟兵：戟兵部队移动力增加 10%。
            // 技巧 #12 精锐弩兵：弩兵部队移动力增加 10%。
            // 技巧 #14 出产良马：骑兵部队移动力增加 10%。
            // 技巧 #16 精锐骑兵：骑兵部队移动力增加 10%。
            // 技巧 #21 强化车轴：兵器部队移动力增加 10%。
            // 技巧 #33 木牛流马：输送队移动力增加 10%。
            Register("TroopAddMoveAbility", CraeteHandle<TroopAddMoveAbility>);
            // 按武将属性折算的移动力加成（兵贵神速：移动力 += 统率 × 5%，向下取整）。
            // 与 TroopAddMoveAbility 的区别：那个是固定值，这个每次属性重算时现算。
            Register("TroopAddMoveAbilityByAttribute", CraeteHandle<TroopAddMoveAbilityByAttribute>);
            // 按武将属性折算的攻击力 / 防御力加成（例：霸王 攻 += 武力×10%，铁壁 防 += 统率×10%）。
            // 与 TroopAddAttack / TroopAddDefence 的区别：那两个是固定值，这两个每次属性重算时现算。
            Register("TroopAddAttackByAttribute", CraeteHandle<TroopAddAttackByAttribute>);
            Register("TroopAddDefenceByAttribute", CraeteHandle<TroopAddDefenceByAttribute>);
            // 条件化属性修正：condition 满足时按加算 / 乘算改攻击、防御、移动力、兵力上限。
            // 覆盖威震华夏（兵力>5000 防御+10%）、咆哮（兵力<3000 攻击翻倍）、
            // 执锐（攻击+20%）、披坚（防御+20%）、龙胆（兵力越少攻防越高）等。
            Register("TroopModifyAttributeByCondition", CraeteHandle<TroopModifyAttributeByCondition>);
            // 特技五维可逆修正：stat 0~4，mode 0 加 / 1 乘，targetType 0 主将 / 1 本队所有。
            // 覆盖佐治、识兵、统兵、威势、威震这类"统率 +5 / 武力 +N%"的设计。
            Register("TroopAddPersonAttribute", CraeteHandle<TroopAddPersonAttribute>);
            // 单挑结算统一入口：resultType 0 胜 / 1 负 / 2 平 / 3 任意，targetType 0 败方 / 1 胜方 / 2 双方。
            // 覆盖"赢单挑后只混乱败军"这类设计，失败 / 平局不会误触发。
            Register("FeatureCombatDuelEffect", CraeteHandle<FeatureCombatDuelEffect>);
            // 伤害已结算统一入口：skillType 0 普攻 / 1 战法 / 2 计策 / 3 任意，
            // targetType 0 目标 / 1 施术者 / 2 双方；selfHealPercent 按伤害回兵。
            Register("FeatureCombatDamageEffect", CraeteHandle<FeatureCombatDamageEffect>);
            // 命中（伤害结算前）统一入口：skillType 0 普攻 / 1 战法 / 2 计策 / 3 任意。
            Register("FeatureCombatHitEffect", CraeteHandle<FeatureCombatHitEffect>);
            // 内政工作速度加成（循吏等）：jobTypes 限定设施类别，speed 为百分比，minTurns 为工期下限。
            Register("FeatureCityWorkBoost", CraeteHandle<FeatureCityWorkBoost>);
            // 成对 / 组合特技指令：pairId 引用 FeaturePairs 配置，执行时先校验后一次性扣资源。
            Register("FeaturePairCommand", CraeteHandle<FeaturePairCommand>);
            // 人物常驻被动：五维加减 / 经验倍率。数值由 PersonPassiveRule 在特技集合变化时统一求值，
            // 这里注册只是为了让解析不报"类未注册"（Instance 本身不订阅任何事件）。
            Register("PersonAddAttribute", CraeteHandle<PersonAddAttribute>);
            Register("PersonAddExpFactor", CraeteHandle<PersonAddExpFactor>);
            // 常驻修正包（交易收益 / 设施返还 / 灾害免疫 / 状态回合），同样由 PersonPassiveRule 求值。
            Register("PersonAddModifier", CraeteHandle<PersonAddModifier>);
            // 月初效果（忠诚 / 功绩 / 伤病 / 传艺 / 势力技巧点）由 FeatureMonthlyEffectSystem 统一结算。
            Register("PersonMonthlyEffect", CraeteHandle<PersonMonthlyEffect>);
            // 状态扩散（煽惑 / 疑机 / 佯装）：挂在 OnTroopBuffApplied，带深度标记防递归。
            Register("TroopSpreadBuff", CraeteHandle<TroopSpreadBuff>);
            // 据点反击的追加效果（拒攻 / 蛮弩）：挂在 OnBuildingAttackBackSettled。
            Register("CityAttackBackEffect", CraeteHandle<CityAttackBackEffect>);
            // 斩杀（剑侠 / 伏锋 / 斩将）：走统一战死入口 Person.DeadInBattle。
            Register("TroopAssassinate", CraeteHandle<TroopAssassinate>);
            // 回合结束的破坏工作（破绽 / 潜入）：未行动部队 → 相邻敌据点削减耐久 / 治安。
            Register("TroopTurnEndSabotage", CraeteHandle<TroopTurnEndSabotage>);
            // 据点每月兵装产出（良工 / 马政 / 亲羌 等）与搜索额外奖励（得财 / 察弊 / 抚众 等）。
            Register("CityProductGainMonthly", CraeteHandle<CityProductGainMonthly>);
            Register("CitySearchingExtraEffect", CraeteHandle<CitySearchingExtraEffect>);
            // 特技范围属性：以拥有者部队为圆心，按格数给友军 / 敌军的属性加成（含粮耗倍率）。
            // 覆盖军神（范围内移动力+5）、兵仙、列阵、八阵、营屯省用（粮耗-30%）。
            Register("TroopAuraAttribute", CraeteHandle<TroopAuraAttribute>);
            // 特技范围减伤：半径内符合条件的部队受到的伤害按百分比降低（干城 / 义勇军 / 列阵）。
            Register("TroopAuraDamageReduce", CraeteHandle<TroopAuraDamageReduce>);
            // 自身粮耗倍率：value 为百分比，0 = 不消耗粮食（辟谷奇兵 / 军屯），100 = 正常。
            Register("TroopSetFoodCost", CraeteHandle<TroopSetFoodCost>);
            // 回合兵力转移：mode 0 抽取敌军（攻心）/ 1 恢复友军（轮回），数量与主将魅力相关。
            Register("TroopTurnDrainTroops", CraeteHandle<TroopTurnDrainTroops>);
            // 回合以兵换粮：脯粮（每回合按配置转换少量兵力为随军粮食）。
            Register("TroopConvertTroopsToFood", CraeteHandle<TroopConvertTroopsToFood>);
            // 状态免疫：否决施加到本部队的 Buff（八阵「不受异常状态」）。
            // buffIds / buffKinds 都不配 = 免疫全部，只配其一则按 ID / 类别精确免疫。
            Register("TroopPreventBuff", CraeteHandle<TroopPreventBuff>);
            // 战法位移免疫：不被敌方战法推走（铁壁）。
            Register("TroopPreventSkillOffset", CraeteHandle<TroopPreventSkillOffset>);
            // 按受击方位修正伤害：position 0 正面 / 1 侧面 / 2 背面。
            // 奇袭（攻击侧）用 isDefender 0 + position 2（背面 +30）；
            // 正合（防守侧）用 isDefender 1 + position 0（正面 -30）/ 1（侧面 -15）。
            Register("TroopChangeDamageByDirection", CraeteHandle<TroopChangeDamageByDirection>);
            // 按本回合行军距离增伤（冲锋）：跑得越远打得越狠，maxDistance 可封顶。
            Register("TroopAddDamageByMoveDistance", CraeteHandle<TroopAddDamageByMoveDistance>);
            // 条件型伤害修正：把固定的 value 换成按局势算出的百分点。
            // kind 0 属性差（乘险）/ 1 目标已负伤（久御）/ 2 相邻敌军数（叠锋）；
            // statType 0 统率 / 1 武力，perUnit 每单位加成，maxBonus 封顶（0 = 不封顶）。
            Register("TroopDamageByCondition", CraeteHandle<TroopDamageByCondition>);
            // 领地内物理攻击附加固定伤害（乘险 55459）：附加量 = 本队攻击力 × attackFactor。
            Register("TroopDamageAddByTerritory", CraeteHandle<TroopDamageAddByTerritory>);
            // 连击同一目标递增伤害（叠锋 55559）：首击 0%，每次 +15%，封顶 45%，换目标重置。
            Register("TroopDamageByComboSameTarget", CraeteHandle<TroopDamageByComboSameTarget>);
            // 受物理攻击累计减伤（久御 55555）：每次 +2%，封顶 20%，层数持久化。
            Register("TroopDamageReduceByHurtStack", CraeteHandle<TroopDamageReduceByHurtStack>);
            // 回合开始三格内敌军计数减伤（无畏 55416）：每支 +5%，封顶 30%，回合开始重算。
            Register("TroopDamageReduceByNearEnemy", CraeteHandle<TroopDamageReduceByNearEnemy>);
            // 周围敌军数附加伤害与回气（长驱策 30137）：radius 内每支 +50 伤害、+5 气力。
            Register("TroopAddDamageByAroundEnemy", CraeteHandle<TroopAddDamageByAroundEnemy>);
            // 护卫（承担型）：半径内友军武将的负伤 / 战死改由本部队主将承担（主将伤病 +level）。
            // 与 TroopPreventPersonInjury / TroopPreventPersonBattleDeath（无条件免疫）不同，本类有代价。
            Register("TroopGuardPerson", CraeteHandle<TroopGuardPerson>);
            // 城池耐久伤害减免：值 30 = 城池受到的耐久伤害 -30%（坚城 / 固守 / 郝昭固守）。
            // 可配在城池（只保护该城）或势力（保护该势力所有城）；设施不参与。
            Register("CityReduceDurabilityDamage", CraeteHandle<CityReduceDurabilityDamage>);
            // 城陷保留建筑（安民）：在"攻方主将魅力 / 20"的保留名额上追加 value 座，
            // keepAll=1 表示全部保留。只对攻方生效。
            Register("CityKeepBuildingsOnFall", CraeteHandle<CityKeepBuildingsOnFall>);
            // 提高本部队带兵上限（附众：+3000）。
            Register("TroopAddMaxTroops", CraeteHandle<TroopAddMaxTroops>);
            // 所在城市在册武将每回合获得经验（劝学）。
            Register("CityAddPersonExp", CraeteHandle<CityAddPersonExp>);
            // 提高登用成功率（伯乐 +30 点 / 蜀相 翻倍）：宿主是武将本人（PersonRelationship 类特技）。
            Register("PersonImproveRecruitProbability", CraeteHandle<PersonImproveRecruitProbability>);
            // 攻城不受到据点反击（先登）：只对持技部队自己生效，不改据点拥有方的反击强度。
            Register("TroopIgnoreBuildingAttackBack", CraeteHandle<TroopIgnoreBuildingAttackBack>);
            // 蓄力：本回合未移动 → 下回合首次攻击增伤（状态机走 Troop.chargeState）
            Register("TroopChargeAttack", CraeteHandle<TroopChargeAttack>);
            // 仁德：每月给本势力全部在籍武将加忠诚
            Register("ForceAddPersonLoyaltyMonthly", CraeteHandle<ForceAddPersonLoyaltyMonthly>);
            // 兵种替换（特殊兵 / 兵圣）：landFrom/landTo、waterFrom/waterTo 成对配置
            Register("TroopChangeTroopType", CraeteHandle<TroopChangeTroopType>);
            // 一次性消耗品：万寿果（授予后寿命 +N 年并从背包移除）
            Register("PersonAddLifeYear", CraeteHandle<PersonAddLifeYear>);
            // 特技 #26 白马：未研究骑射也可施展骑射，且骑射可会心一击。
            // 技巧 #15 骑射：骑兵部队获得弓箭攻击战法。
            Register("TroopAddSkill", CraeteHandle<TroopAddSkill>);
            // 战法升级：部队属性重算时，把符合兵种的原战法替换为配置指定的新战法。
            Register("TroopReplaceSkill", CraeteHandle<TroopReplaceSkill>);
            // 特技 #1 飞将：陆上对低武力敌军施展战法成功时会心。
            // 特技 #16 乱战：森林中攻击时会心。
            // 特技 #17 待伏：伏兵成功时会心。
            // 特技 #18 攻城：攻击据点、设施、建筑物时会心。
            // 特技 #24 驱逐：施展一般攻击时会心。
            // 特技 #35 枪将：对低武力敌军施展枪兵战法成功时会心。
            // 特技 #36 戟将：对低武力敌军施展戟兵战法成功时会心。
            // 特技 #37 弓将：对低武力敌军施展弩兵战法成功时会心。
            // 特技 #38 骑将：对低武力敌军施展骑兵战法成功时会心。
            // 特技 #39 水将：对低武力敌军施展水军战法成功时会心。
            // 特技 #40 勇将：对低武力敌军施展全战法成功时会心。
            // 特技 #41 神将：对低武力敌军施展一般攻击及战法成功时会心。
            // 特技 #42 斗神：枪兵战法与戟兵战法成功时会心。
            // 特技 #43 枪神：枪兵战法成功时会心。
            // 特技 #44 戟神：戟兵战法成功时会心。
            // 特技 #45 弓神：弩兵战法成功时会心。
            // 特技 #46 骑神：骑兵战法成功时会心。
            // 特技 #47 工神：兵器战法成功时会心。
            // 特技 #48 水神：水军战法成功时会心。
            // 特技 #49 霸王：全战法成功时会心。
            // 特技 #59 妙计：对低智力敌军施展部队计略成功时会心。
            // 特技 #60 秘计：对高智力敌军施展部队计略成功时会心。
            // 特技 #63 火神：对低智力敌军施展火计成功时会心。
            // 特技 #64 神算：对低智力敌军施展计略成功时会心。
            // 特技 #68 深谋：部队计略成功时会心。
            Register("TroopSkillCalculateCritical", CraeteHandle<TroopSkillCalculateCritical>);
            // 特技 #54 火攻：对低智力敌军施展火计时必定成功。
            // 特技 #55 言毒：对低智力敌军施展伪报时必定成功。
            // 特技 #56 机智：对低智力敌军施展扰乱时必定成功。
            // 特技 #57 诡计：对低智力敌军施展内讧时必定成功。
            // 特技 #58 虚实：对低智力敌军施展全部队计略时必定成功。
            // 特技 #61 看破：必定识破低智力敌军施展的部队计略。
            // 特技 #62 洞察：必定识破敌方部队施展的部队计略。
            // 特技 #63 火神：对低智力敌军施展火计时必定成功。
            // 特技 #64 神算：对低智力敌军施展计略时必定成功。
            // 特技 #73 规律：防止遭受部队计略伪报。
            // 特技 #74 沉着：防止遭受部队计略扰乱。
            // 特技 #75 明镜：防止遭受部队计略伪报、扰乱。
            Register("TroopSkillCalculateSuccess", CraeteHandle<TroopSkillCalculateSuccess>);
            // 特技 #14 突袭：陆上攻击时不受反击损伤。
            // 特技 #15 强袭：水上攻击时不受反击损伤。
            // 特技 #62 洞察：识破敌方计略并将反击伤害提高至 200%。
            Register("TroopSkillCalculateAttackBack", CraeteHandle<TroopSkillCalculateAttackBack>);
            // 范围状态建筑：配置触发时，为建筑范围内指定阵营的部队随机附加指定状态，并持续配置回合数。
            Register("BuildingAddBuff", CraeteHandle<BuildingAddBuff>);
            // 谷仓：建成后，农场不必紧邻也能使农场粮食收入提高至 150%。
            Register("BuildingImproveFoodGain", CraeteHandle<BuildingImproveFoodGain>);
            // 造币厂：建成后，市场不必紧邻也能使市场资金收入提高至 150%。
            Register("BuildingImproveGoldGain", CraeteHandle<BuildingImproveGoldGain>);
            // 军屯农：城市士兵越多，农场粮食收入越高；士兵达到 3 万时为普通农场的 200%。
            Register("BuildingImproveFoodGainByCityTroops", CraeteHandle<BuildingImproveFoodGainByCityTroops>);
            // 太鼓台：每回合重算时，周围 3 格内己方部队攻击力增加 10，敌方部队攻击力减少 10。
            Register("BuildingImproveTroopAttack", CraeteHandle<BuildingImproveTroopAttack>);
            // 阵：周围 2 格内己方部队防御力增加 10。
            // 砦：周围 3 格内己方部队防御力增加 15。
            // 城塞：周围 4 格内己方部队防御力增加 20。
            Register("BuildingImproveTroopDefence", CraeteHandle<BuildingImproveTroopDefence>);

            // 军乐台：在结算时为周围 2 格内己方部队恢复 10 点气力。
            Register("BuildingAddTroopMorale", CraeteHandle<BuildingAddTroopMorale>);

            // 通用建筑光环：以建筑中心格为圆心，给半径内部队提供攻击 / 防御常驻加成。
            // 属性、数值、半径、敌我、叠加规则全部由 JSON 配置，新增建筑不需要再写代码。
            Register("BuildingAura", CraeteHandle<BuildingAura>);
            // 建筑触发器容器：triggerList 决定时机（回合开始/结束、建造完成），actionList 依次执行。
            Register("BuildingTriggerAction", CraeteHandle<BuildingTriggerAction>);
            // 城市触发器容器：triggerList 决定时机（回合开始/结束、月初、换季、陷落），actionList 依次执行。
            Register("CityTriggerAction", CraeteHandle<CityTriggerAction>);
            // 势力触发器容器：triggerList 决定时机（势力回合开始/结束），actionList 依次执行。
            Register("ForceTriggerAction", CraeteHandle<ForceTriggerAction>);
            // 通用部队光环：以部队所在格为圆心，给半径内部队提供攻击 / 防御常驻加成，跟随部队移动。
            Register("TroopAura", CraeteHandle<TroopAura>);
            // 特技 #80 名声：增加征兵士兵数。
            // 特技 #81 能吏：增加枪、戟、弩兵装产量。
            // 特技 #82 繁殖：增加军马兵装产量。
            Register("CityImproveJobResult", CraeteHandle<CityImproveJobResult>);
            // 特技 #89 米道：所属城市每季粮食收入提高至 150%。
            Register("CityImproveFoodHarvest", CraeteHandle<CityImproveFoodHarvest>);
            // 特技 #88 富豪：所属城市每月资金收入提高至 150%。
            Register("CityImproveGoldHarvest", CraeteHandle<CityImproveGoldHarvest>);
            // 特技 #90 征税：所属城市每回合额外获得资金，单次收入减半。
            Register("CityGoldHarvestEveryTurn", CraeteHandle<CityGoldHarvestEveryTurn>);
            // 特技 #91 征收：所属城市每月额外获得粮食，单次收入减半。
            Register("CityFoodHarvestEveryMonth", CraeteHandle<CityFoodHarvestEveryMonth>);
            // 特技 #83 发明：兵器兵装生产期减半。
            // 特技 #84 造船：舰船兵装生产期减半。
            Register("CityImproveJobCounterResult", CraeteHandle<CityImproveJobCounterResult>);
            // 特技 #1 飞将：陆上忽略敌军控制区域。
            // 特技 #2 遁走：陆上忽略敌军控制区域。
            // 特技 #5 推进：水上忽略敌军控制区域。
            Register("TroopIgnoreZOC", CraeteHandle<TroopIgnoreZOC>);
            // 特技 #10 扫荡：攻击后降低敌军气力（小）。
            // 特技 #11 威风：攻击后降低敌军气力（大）。
            // 特技 #12 昂扬：击破敌军后恢复气力。
            // 特技 #13 连击：一般攻击可连续攻击两次。
            // 特技 #19 掎角：满足包围条件时概率使目标混乱。
            // 特技 #23 攻心：攻击时吸收敌军部分士兵。
            // 特技 #31 怒发：受战法攻击时恢复气力。
            // 特技 #50 疾驰：骑兵战法成功时使低攻击目标混乱。
            // 特技 #69 反计：识破计略后施展同样计略。
            // 特技 #76 奏乐：每回合恢复友军气力。
            // 特技 #77 诗想：所有气力恢复效果提高至 200%。
            // 技巧 #2 袭击兵粮：枪兵攻击命中敌军后，触发夺取敌方兵粮的后续效果。
            // 技巧 #10 还射：弩兵遭受弓箭攻击命中后，触发自动反击的后续效果。
            Register("TroopTriggerAction", CraeteHandle<TroopTriggerAction>);
            // 部队气力调整：在配置的事件发生时，为符合条件的部队增加或减少配置数值的气力。
            Register("TroopChangeMorale", CraeteHandle<TroopChangeMorale>);
            // 部队连击：在配置的攻击事件发生时，让符合条件的部队额外再攻击一次。
            Register("TroopComboAttack", CraeteHandle<TroopComboAttack>);
            // 辅佐能力：部队任一武将持有"辅佐"特性时装配本 Action，在属性计算阶段把配置概率(取Max不叠加)写入 Troop.assistChance，
            // CalcAssistChance 在关系档位均未命中时按该值兜底判定可越过人际关系获得支援攻击（按类型识别，不依赖特性 Id）。
            Register("TroopAssistAttack", CraeteHandle<TroopAssistAttack>);
            // 部队恢复：在配置的事件发生时，为符合条件的部队恢复配置数值的兵力。
            Register("TroopRecure", CraeteHandle<TroopRecure>);
            // 特技 #63 火神：对低智力目标的火计必定成功，并强化火攻相关效果。
            Register("TroopSetFireDamage", CraeteHandle<TroopSetFireDamage>);
            // 特技 #63 火神：免疫火焰伤害。
            Register("TroopIgnoreFire", CraeteHandle<TroopIgnoreFire>);
            // 特技 #70 奇谋：对敌方部队施展部队计略的成功率提高至 200%。
            Register("TroopImproveSkillSuccess", CraeteHandle<TroopImproveSkillSuccess>);
            // 战法耗气调整：部队属性重算时，为符合兵种与条件的战法增减配置数值的气力消耗。
            Register("TroopChangeSkillCost", CraeteHandle<TroopChangeSkillCost>);
            // 技巧 #11 强弩：弩兵战法施放距离增加 1 格。
            // 技巧 #30 神火计：火计施放距离增加至 3 格。
            // 特技 #25 射程：井阑、投石射程增加 1 格。
            // 特技 #66 鬼谋：部队计略施放范围增加 2 格。
            Register("TroopChangeSkillSpellRange", CraeteHandle<TroopChangeSkillSpellRange>);
            // 特技 #65 百出：减少部队计略消耗的气力。
            Register("TroopSetSkillCost", CraeteHandle<TroopSetSkillCost>);
            // 特技 #30 铁壁：攻击损伤降低 20%。
            // 特技 #32 藤甲：非火攻击损伤减半。
            // 特技 #51 射手：森林中弩兵战法伤害提高。
            // 技巧 #1 锻炼枪兵：枪兵战法威力增加 10%。
            // 技巧 #4 精锐枪兵：枪兵战法威力增加 10%。
            // 技巧 #5 锻炼戟兵：戟兵战法威力增加 10%。
            // 技巧 #8 精锐戟兵：戟兵战法威力增加 10%。
            // 技巧 #9 锻炼弩兵：弩兵战法威力增加 10%。
            // 技巧 #12 精锐弩兵：弩兵战法威力增加 10%。
            // 技巧 #13 锻炼骑兵：骑兵战法威力增加 10%。
            // 技巧 #16 精锐骑兵：骑兵战法威力增加 10%。
            Register("TroopChangeDamage", CraeteHandle<TroopChangeDamage>);
            // 技巧 #24 霹雳：投石攻击时，同时波及目标周围 1 格内的敌军。
            // 特技 #67 连环：部队计略成功时，使目标相邻部队也遭受计略波及。
            Register("TroopChangeSkillAttackRange", CraeteHandle<TroopChangeSkillAttackRange>);
            // 特技 #20 捕缚：击破敌军时必定捕获未持有强运且无名马的敌将。
            Register("TroopChangeCaptiveFactor", CraeteHandle<TroopChangeCaptiveFactor>);
            // 部队单挑概率变化：战法命中后触发单挑的概率（百分比），在部队属性重算时生效。
            Register("TroopChangeDuelChance", CraeteHandle<TroopChangeDuelChance>);
            // 特技 #34 血路：部队溃灭时，同部队武将不会被俘虏。
            Register("TroopChangeEscapeFactor", CraeteHandle<TroopChangeEscapeFactor>);
            // 技巧 #7 大盾：受到一般攻击时有 30% 概率不受伤害。
            // 特技 #28 不屈：兵力较少时不受攻击损伤。
            // 特技 #29 金刚：敌方攻击力较弱时不受损伤。
            Register("TroopSetDamage", CraeteHandle<TroopSetDamage>);
            // 特技 #33 强运：武将不会战死、被俘或负伤。
            Register("TroopChangePersonEscapeFactor", CraeteHandle<TroopChangePersonEscapeFactor>);
            // 部队状态：在配置的事件发生时，为符合条件的部队添加指定状态，并持续配置回合数。
            Register("TroopAddBuff", CraeteHandle<TroopAddBuff>);
            // 技巧 #10 还射：弩兵遭受弓箭攻击时，自动对攻击者施展配置指定的反击战法。
            Register("TroopAttackBack", CraeteHandle<TroopAttackBack>);
            // 技巧 #2 袭击兵粮：枪兵攻击时，夺取敌方部队兵粮；夺取量受己方兵力和攻击力影响。
            Register("TroopStealFood", CraeteHandle<TroopStealFood>);
            // 特技 #63 火神：强化火攻相关战法的攻击范围。
            Register("TroopAddSkillAttackRange", CraeteHandle<TroopAddSkillAttackRange>);
            // 战法射程强化：部队属性重算后，为符合兵种与条件的战法增加配置格数的施放距离。
            Register("TroopAddSkillSpellRange", CraeteHandle<TroopAddSkillSpellRange>);
            // 部队气力修正：在部队属性重算后，直接为符合条件的部队增加或减少配置数值的气力。
            Register("TroopModifyMorale", CraeteHandle<TroopModifyMorale>);
            // 特技 #32 藤甲：火攻损伤提高至 200%，与非火伤害减半配套结算。
            Register("TroopModifyFireDamage", CraeteHandle<TroopModifyFireDamage>);
            // 战法反制：符合配置的战法事件发生时，让目标部队自动对施法部队施展指定反击战法。
            Register("TroopSkillBack", CraeteHandle<TroopSkillBack>);

            // 特技 #86 眼力：存在未发现的在野武将时执行人才探索必定发现。
            Register("CityChangeSearchingWild", CraeteHandle<CityChangeSearchingWild>);
            // 特技 #85 指导：研究技巧的费用减半。
            Register("CityImproveResearchCost", CraeteHandle<CityImproveResearchCost>);
            // 特技 #86 眼力：提高人才探索成功率，确保发现未发现的在野武将。
            Register("CityImproveSearchingWild", CraeteHandle<CityImproveSearchingWild>);
            // 特技 #79 屯田：所属关所、港口的士兵不消耗兵粮。
            Register("CityChangeFoodCost", CraeteHandle<CityChangeFoodCost>);
            // 特技（作用于城市）道具制造权限：武将携带后，其所在城市可在铁匠铺 / 工坊 / 造船厂
            // 生产配置指定的道具，即使势力尚未研究该道具要求的科技。
            // 判定走 FeaturePermission.CityCanCreateItem，本类只做参数解析与命中判定。
            Register("CityAllowCreateItem", CraeteHandle<CityAllowCreateItem>);
            // 特技（作用于城市）兵种组建权限：武将携带后，出征界面可组建配置指定的兵种，
            // 支持按 isLand 分别配置陆上 / 水上兵种（landTroopTypeIds / waterTroopTypeIds）。
            // 判定走 FeaturePermission.AnyPersonAllowsTroopType。
            Register("CityAllowTroopType", CraeteHandle<CityAllowTroopType>);
            // 特技 #78 筑城：军事设施建设时的耐久上升量提高至 200%。
            Register("TroopImproveBuildPower", CraeteHandle<TroopImproveBuildPower>);
            // 通用：给部队设置寻路/地形免疫标记位（PathFlag）。
            // 解毒 flag=1、踏破/难所行军 flag=2，均可在 Features.json / Techniques.json 里配置。
            Register("TroopSetPathFlag", CraeteHandle<TroopSetPathFlag>);
            // 通用：给武将设置标记位（PersonMarkType），特技 / 装备的 actionEntities 用。
            // 例：启用"免疫俘虏" → { "class": "PersonSetMark", "mark": "IgnoreCapture", "value": 1 }
            Register("PersonSetMark", CraeteHandle<PersonSetMark>);
            // 特技 #52 猛者：以战法让敌方部队移动区格时，有几率让敌方一名武将负伤。
            Register("TroopOffsetInjurePerson", CraeteHandle<TroopOffsetInjurePerson>);
            // 特技 #53 护卫：本部队的武将不会负伤（免伤判定挂在 PersonInjury 的询问事件上，
            // 因此天然免疫"猛者"这类伤害来源，与订阅先后无关）。
            Register("TroopPreventPersonInjury", CraeteHandle<TroopPreventPersonInjury>);
            // 特技 #53 护卫：本部队的武将不会战死（免死判定挂在 Person.DeadInBattle 的询问事件上；
            // 处决 / 病逝 / 剧情杀走 Person.Dead，不受本特技影响）。
            Register("TroopPreventPersonBattleDeath", CraeteHandle<TroopPreventPersonBattleDeath>);
            // 特技 #22 掠夺：击破敌军时缴获全部物资，此 Action 负责粮食部分。
            Register("TroopAddFoodGain", CraeteHandle<TroopAddFoodGain>);
            // 特技 #22 掠夺：击破敌军时缴获全部物资，此 Action 负责资金部分。
            Register("TroopAddGoldGain", CraeteHandle<TroopAddGoldGain>);
            // 特技 #21 精妙：击破敌方部队时将本次技巧点提高至 200%。
            Register("TroopImproveDefeatTechniquePoint", CraeteHandle<TroopImproveDefeatTechniquePoint>);
            // 特技 #97 仁政：阻止所在城市武将在换季结算时降低忠诚。
            Register("CityPreventPersonLoyaltyLoss", CraeteHandle<CityPreventPersonLoyaltyLoss>);
            // 特技 #99 祈愿：春秋季初概率赋予城市丰收状态，并提高状态期间粮食产量至 150%。
            Register("CityBumperHarvest", CraeteHandle<CityBumperHarvest>);
            // 让某兵种类型的二动
            Register("TroopResetActionOver", CraeteHandle<TroopResetActionOver>);

            // ================================================================
            // 扩展批次（2026-10-19）：补齐三个"引擎有接口、玩法没接上"的战斗结算点，
            // 以及内政 / 经济 / 建筑产出的收益修正。
            //
            // 装配说明：
            //   · TroopSkill* 系列用 TroopTroopActionBase，特技（装配部队）与科技（装配势力）都能用；
            //   · City* / Building* 系列用 ForceActionBase，只用于科技（特技装配时取不到 Force）。
            // ================================================================

            // 暴击伤害系数（%）：value 100 = 不变，120 = ×1.2
            Register("TroopSkillCriticalFactor", CraeteHandle<TroopSkillCriticalFactor>);
            // 战法对建筑/城池的耐久伤害（%）
            Register("TroopSkillDamageBuildingDurability", CraeteHandle<TroopSkillDamageBuildingDurability>);
            // 战法对城内/建筑内兵力的伤害（%）
            Register("TroopSkillDamageBuildingTroops", CraeteHandle<TroopSkillDamageBuildingTroops>);

            // 内政工作的金钱消耗（%）：value -20 = 降 20%
            Register("CityJobCostModify", CraeteHandle<CityJobCostModify>);
            // 内政工作的功绩收益（%）
            Register("CityJobMeritGain", CraeteHandle<CityJobMeritGain>);
            // 内政工作的技巧点收益（%）
            Register("CityJobTechniquePointGain", CraeteHandle<CityJobTechniquePointGain>);

            // 城市金收获的最终修正（%）
            Register("CityGoldHarvestAfter", CraeteHandle<CityGoldHarvestAfter>);
            // 城市粮收获的最终修正（%）
            Register("CityFoodHarvestAfter", CraeteHandle<CityFoodHarvestAfter>);
            // 月度治安变化（加值，非百分比）
            Register("CitySecurityChangeMonth", CraeteHandle<CitySecurityChangeMonth>);

            // 建筑人口产出修正（%）
            Register("BuildingPopulationGain", CraeteHandle<BuildingPopulationGain>);
            // 建筑自身产出修正（%）
            Register("BuildingProductGain", CraeteHandle<BuildingProductGain>);

            // ================================================================
            // 扩展批次（2026-11-24）：新被动特技的通用效果执行器。
            // 这批被动大量同构（相邻敌军每回合扣兵 / 扣粮 / 扣气力、击破敌军回兵回气、
            // 溃灭后起火 / 敌方止行、造成位移后附加状态或掠夺气力），
            // 因此收敛成 4 个数据驱动 Action，避免为每条被动写一个近重复类。
            // ================================================================

            // 周期范围效果：持技部队回合开始时，对半径内目标扣兵 / 扣粮 / 扣气力 /
            // 回兵 / 回气力 / 附加状态 / 点火 / 给所属据点加兵。
            Register("TroopTurnRangeEffect", CraeteHandle<TroopTurnRangeEffect>);
            // 击破敌军触发：自身回兵、范围内友军回气/恢复行动、范围内敌军扣兵扣气、
            // 概率学得敌将特技。
            Register("TroopDefeatEnemyEffect", CraeteHandle<TroopDefeatEnemyEffect>);
            // 部队溃灭触发：原地起火、范围内敌军附加状态（止行）。
            Register("TroopDestroyedEffect", CraeteHandle<TroopDestroyedEffect>);
            // 造成位移触发：概率附加状态（混乱）、掠夺目标气力。
            Register("TroopOffsetEffect", CraeteHandle<TroopOffsetEffect>);

        }

    }
}
