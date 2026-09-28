using Sango.Core.Player;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 性格排序功能类，提供性格对象的各种排序字段定义。
    ///
    /// 字段按影响面分组：基础标识 / 计略抗性 / 计略专精 / 势力倾向 /
    /// 部队作战 / 单挑 / 内政 / 外交人事 / 成长忠诚。
    /// 全部使用 <see cref="DataEditType.IntCalculator"/>，可在编辑器内直接改值。
    /// </summary>
    public class PersonalitySortFunction : Singleton<PersonalitySortFunction>
    {
        /// <summary>
        /// 获取性格对象显示字符串的代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <returns>显示字符串</returns>
        public delegate string PersonalityValueStrGet(Personality personality);

        /// <summary>
        /// 性格对象排序比较的代理
        /// </summary>
        /// <param name="personality1">性格对象1</param>
        /// <param name="personality2">性格对象2</param>
        /// <returns>比较结果</returns>
        public delegate int PersonalitySortFunc(Personality personality1, Personality personality2);

        /// <summary>
        /// 获取性格对象属性值的object类型代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonalityValueObjGet(Personality personality);

        /// <summary>
        /// 设置性格对象属性值的代理
        /// </summary>
        /// <param name="personality">性格对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonalityValueObjSet(Personality personality, object value);

        /// <summary>
        /// 性格排序标题，封装单个属性的显示、排序与编辑逻辑
        /// </summary>
        public class SortTitle : ObjectSortTitle
        {
            public PersonalityValueStrGet valueStrGetCall;
            public PersonalitySortFunc valueSortFunc;
            public PersonalityValueObjGet valueObjGet;
            public PersonalityValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Personality)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Personality)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Personality)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Personality)a, (Personality)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueStrGetCall = valueStrGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        /// <summary>
        /// 生成一个 int 字段的排序 / 编辑标题（避免为 50+ 个字段重复样板）。
        /// </summary>
        /// <param name="name">显示名</param>
        /// <param name="getter">取值</param>
        /// <param name="setter">写值</param>
        /// <param name="width">列宽</param>
        /// <returns>排序标题</returns>
        static SortTitle IntField(string name, Func<Personality, int> getter, Action<Personality, int> setter, float width = 2.4f)
        {
            return new SortTitle()
            {
                name = name,
                width = width,
                valueStrGetCall = x => getter(x).ToString(),
                valueSortFunc = (a, b) => getter(a).CompareTo(getter(b)),
                valueObjGet = x => getter(x),
                valueObjSet = (x, v) => setter(x, (int)v),
                editType = DataEditType.IntCalculator,
            };
        }

        #region 基础标识

        /// <summary>按ID排序</summary>
        public static SortTitle SortById = new SortTitle()
        {
            name = "ID",
            width = 2.00f,
            valueStrGetCall = x => x.Id.ToString(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = x => x.Id,
            valueObjSet = null,
        };

        /// <summary>按名称排序</summary>
        public static SortTitle SortByName = new SortTitle()
        {
            name = "性格",
            width = 4.00f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>按类型排序（性格编号，新增性格顺延）</summary>
        public static SortTitle SortByKind = new SortTitle()
        {
            name = "类型",
            width = 2.00f,
            valueStrGetCall = x => x.kind.ToString(),
            valueSortFunc = (a, b) => a.kind.CompareTo(b.kind),
            valueObjGet = x => x.kind,
            valueObjSet = (x, v) => x.kind = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        #endregion

        #region 计略抗性（作为目标）

        public static SortTitle SortByFalseReportSuccessAdd = IntField("伪报抗性", x => x.falseReportSuccessAdd, (x, v) => x.falseReportSuccessAdd = v);
        public static SortTitle SortByDisturbSuccessAdd = IntField("扰乱抗性", x => x.disturbSuccessAdd, (x, v) => x.disturbSuccessAdd = v);
        public static SortTitle SortByCalmdownSuccessAdd = IntField("镇静抗性", x => x.calmdownSuccessAdd, (x, v) => x.calmdownSuccessAdd = v);
        public static SortTitle SortByAmbushSuccessAdd = IntField("伏兵抗性", x => x.ambushSuccessAdd, (x, v) => x.ambushSuccessAdd = v);
        public static SortTitle SortBySorcerySuccessAdd = IntField("妖术抗性", x => x.sorcerySuccessAdd, (x, v) => x.sorcerySuccessAdd = v);
        public static SortTitle SortByInfightingSuccessAdd = IntField("内讧抗性", x => x.infightingSuccessAdd, (x, v) => x.infightingSuccessAdd = v);

        #endregion

        #region 计略专精（作为施法者）

        public static SortTitle SortByFalseReportCriticalAdd = IntField("伪报专精", x => x.falseReportCriticalAdd, (x, v) => x.falseReportCriticalAdd = v);
        public static SortTitle SortByDisturbCriticalAdd = IntField("扰乱专精", x => x.disturbCriticalAdd, (x, v) => x.disturbCriticalAdd = v);
        public static SortTitle SortByCalmdownCriticalAdd = IntField("镇静专精", x => x.calmdownCriticalAdd, (x, v) => x.calmdownCriticalAdd = v);
        public static SortTitle SortByAmbushCriticalAdd = IntField("伏兵专精", x => x.ambushCriticalAdd, (x, v) => x.ambushCriticalAdd = v);
        public static SortTitle SortBySorceryCriticalAdd = IntField("妖术专精", x => x.sorceryCriticalAdd, (x, v) => x.sorceryCriticalAdd = v);
        public static SortTitle SortByInfightingCriticalAdd = IntField("内讧专精", x => x.infightingCriticalAdd, (x, v) => x.infightingCriticalAdd = v);

        #endregion

        #region 势力倾向

        public static SortTitle SortByWarTendencyAdd = IntField("战争倾向", x => x.warTendencyAdd, (x, v) => x.warTendencyAdd = v);
        public static SortTitle SortByDefenseTendencyAdd = IntField("防御倾向", x => x.defenseTendencyAdd, (x, v) => x.defenseTendencyAdd = v);
        public static SortTitle SortByDiplomacyTendencyAdd = IntField("外交倾向", x => x.diplomacyTendencyAdd, (x, v) => x.diplomacyTendencyAdd = v);
        public static SortTitle SortByEconomicTendencyAdd = IntField("经济倾向", x => x.economicTendencyAdd, (x, v) => x.economicTendencyAdd = v);
        public static SortTitle SortByTechnologyTendencyAdd = IntField("科技倾向", x => x.technologyTendencyAdd, (x, v) => x.technologyTendencyAdd = v);
        public static SortTitle SortByRecruitCaptiveTendencyAdd = IntField("招降倾向", x => x.recruitCaptiveTendencyAdd, (x, v) => x.recruitCaptiveTendencyAdd = v);
        public static SortTitle SortByReleaseCaptiveTendencyAdd = IntField("释放倾向", x => x.releaseCaptiveTendencyAdd, (x, v) => x.releaseCaptiveTendencyAdd = v);
        public static SortTitle SortByRansomCaptiveTendencyAdd = IntField("赎回倾向", x => x.ransomCaptiveTendencyAdd, (x, v) => x.ransomCaptiveTendencyAdd = v);

        #endregion

        #region 部队作战

        public static SortTitle SortByTroopAttackScale = IntField("部队攻击", x => x.troopAttackScale, (x, v) => x.troopAttackScale = v);
        public static SortTitle SortByTroopCounterScale = IntField("反击规避", x => x.troopCounterScale, (x, v) => x.troopCounterScale = v);
        public static SortTitle SortByTroopRetreatAdd = IntField("撤退倾向", x => x.troopRetreatAdd, (x, v) => x.troopRetreatAdd = v);
        public static SortTitle SortByTroopAggression = IntField("好战度", x => x.troopAggression, (x, v) => x.troopAggression = v);
        public static SortTitle SortByTroopBestNAdd = IntField("择优修正", x => x.troopBestNAdd, (x, v) => x.troopBestNAdd = v);
        public static SortTitle SortByTroopSkillScale = IntField("计略倾向", x => x.troopSkillScale, (x, v) => x.troopSkillScale = v);
        public static SortTitle SortByTroopRoleOverrideAdd = IntField("角色覆盖", x => x.troopRoleOverrideAdd, (x, v) => x.troopRoleOverrideAdd = v);

        #endregion

        #region 单挑

        public static SortTitle SortByDuelAcceptAdd = IntField("单挑应战", x => x.duelAcceptAdd, (x, v) => x.duelAcceptAdd = v);
        public static SortTitle SortByDuelRetreatAdd = IntField("单挑脱战", x => x.duelRetreatAdd, (x, v) => x.duelRetreatAdd = v);

        #endregion

        #region 内政专有

        public static SortTitle SortByDomesticFarmingScale = IntField("农业效率", x => x.domesticFarmingScale, (x, v) => x.domesticFarmingScale = v);
        public static SortTitle SortByDomesticDevelopScale = IntField("商业效率", x => x.domesticDevelopScale, (x, v) => x.domesticDevelopScale = v);
        public static SortTitle SortByDomesticSecurityScale = IntField("治安效率", x => x.domesticSecurityScale, (x, v) => x.domesticSecurityScale = v);
        public static SortTitle SortByDomesticTrainScale = IntField("训练效率", x => x.domesticTrainScale, (x, v) => x.domesticTrainScale = v);
        public static SortTitle SortByDomesticSearchScale = IntField("搜索效率", x => x.domesticSearchScale, (x, v) => x.domesticSearchScale = v);
        public static SortTitle SortByDomesticRecruitTroopScale = IntField("征兵效率", x => x.domesticRecruitTroopScale, (x, v) => x.domesticRecruitTroopScale = v);
        public static SortTitle SortByDomesticRecruitPersonScale = IntField("募将效率", x => x.domesticRecruitPersonScale, (x, v) => x.domesticRecruitPersonScale = v);
        public static SortTitle SortByDomesticCreateItemScale = IntField("兵装效率", x => x.domesticCreateItemScale, (x, v) => x.domesticCreateItemScale = v);
        public static SortTitle SortByDomesticCreateMachineScale = IntField("兵器效率", x => x.domesticCreateMachineScale, (x, v) => x.domesticCreateMachineScale = v);
        public static SortTitle SortByDomesticCreateBoatScale = IntField("造船效率", x => x.domesticCreateBoatScale, (x, v) => x.domesticCreateBoatScale = v);
        public static SortTitle SortByDomesticBuildScale = IntField("建造效率", x => x.domesticBuildScale, (x, v) => x.domesticBuildScale = v);
        public static SortTitle SortByDomesticTradeScale = IntField("交易效率", x => x.domesticTradeScale, (x, v) => x.domesticTradeScale = v);
        public static SortTitle SortByDomesticRepairScale = IntField("修缮效率", x => x.domesticRepairScale, (x, v) => x.domesticRepairScale = v);

        #endregion

        #region 外交与人事

        public static SortTitle SortByGiftEffectAdd = IntField("送礼效果", x => x.giftEffectAdd, (x, v) => x.giftEffectAdd = v);
        public static SortTitle SortByNegotiationAdd = IntField("交涉修正", x => x.negotiationAdd, (x, v) => x.negotiationAdd = v);
        public static SortTitle SortByCaptiveDealAdd = IntField("俘虏处置", x => x.captiveDealAdd, (x, v) => x.captiveDealAdd = v);
        public static SortTitle SortByLoyaltyDriftAdd = IntField("忠诚漂移", x => x.loyaltyDriftAdd, (x, v) => x.loyaltyDriftAdd = v);

        #endregion

        #region 成长与忠诚

        public static SortTitle SortByGrowthScale = IntField("成长速度", x => x.growthScale, (x, v) => x.growthScale = v);
        public static SortTitle SortByLearnSpeedScale = IntField("学习速度", x => x.learnSpeedScale, (x, v) => x.learnSpeedScale = v);
        public static SortTitle SortByLoyaltyKeepAdd = IntField("忠诚维持", x => x.loyaltyKeepAdd, (x, v) => x.loyaltyKeepAdd = v);
        public static SortTitle SortByRevoltRiskAdd = IntField("叛乱风险", x => x.revoltRiskAdd, (x, v) => x.revoltRiskAdd = v);

        #endregion

        /// <summary>
        /// 默认排序标题列表（覆盖全部性格影响面）
        /// </summary>
        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            // 基础标识
            SortById,
            SortByName,
            SortByKind,

            // 计略抗性
            SortByFalseReportSuccessAdd,
            SortByDisturbSuccessAdd,
            SortByCalmdownSuccessAdd,
            SortByAmbushSuccessAdd,
            SortBySorcerySuccessAdd,
            SortByInfightingSuccessAdd,

            // 计略专精
            SortByFalseReportCriticalAdd,
            SortByDisturbCriticalAdd,
            SortByCalmdownCriticalAdd,
            SortByAmbushCriticalAdd,
            SortBySorceryCriticalAdd,
            SortByInfightingCriticalAdd,

            // 势力倾向
            SortByWarTendencyAdd,
            SortByDefenseTendencyAdd,
            SortByDiplomacyTendencyAdd,
            SortByEconomicTendencyAdd,
            SortByTechnologyTendencyAdd,
            SortByRecruitCaptiveTendencyAdd,
            SortByReleaseCaptiveTendencyAdd,
            SortByRansomCaptiveTendencyAdd,

            // 部队作战
            SortByTroopAttackScale,
            SortByTroopCounterScale,
            SortByTroopRetreatAdd,
            SortByTroopAggression,
            SortByTroopBestNAdd,
            SortByTroopSkillScale,
            SortByTroopRoleOverrideAdd,

            // 单挑
            SortByDuelAcceptAdd,
            SortByDuelRetreatAdd,

            // 内政专有
            SortByDomesticFarmingScale,
            SortByDomesticDevelopScale,
            SortByDomesticSecurityScale,
            SortByDomesticTrainScale,
            SortByDomesticSearchScale,
            SortByDomesticRecruitTroopScale,
            SortByDomesticRecruitPersonScale,
            SortByDomesticCreateItemScale,
            SortByDomesticCreateMachineScale,
            SortByDomesticCreateBoatScale,
            SortByDomesticBuildScale,
            SortByDomesticTradeScale,
            SortByDomesticRepairScale,

            // 外交与人事
            SortByGiftEffectAdd,
            SortByNegotiationAdd,
            SortByCaptiveDealAdd,
            SortByLoyaltyDriftAdd,

            // 成长与忠诚
            SortByGrowthScale,
            SortByLearnSpeedScale,
            SortByLoyaltyKeepAdd,
            SortByRevoltRiskAdd,
        };
    }
}
