using TKNewtonsoft.Json.Linq;
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
            Register("BuildingBaseAttackBack", CraeteHandle<BuildingBaseAttackBack>);
            Register("CityDurabilityLimit", CraeteHandle<CityDurabilityLimit>);
            Register("CityAddDurability", CraeteHandle<CityAddDurability>);
            Register("CityFoodLimit", CraeteHandle<CityFoodLimit>);
            Register("CityGoldLimit", CraeteHandle<CityGoldLimit>);
            Register("CitySecurityChange", CraeteHandle<CitySecurityChange>);
            Register("CityStoreLimit", CraeteHandle<CityStoreLimit>);
            Register("CityTroopsLimit", CraeteHandle<CityTroopsLimit>);
            Register("ForceCityMaxMorale", CraeteHandle<ForceCityMaxMorale>);
            Register("ForcePersonLoyaltyChange", CraeteHandle<ForcePersonLoyaltyChange>);
            Register("ForceTroopMaxTroop", CraeteHandle<ForceTroopMaxTroop>);
            Register("TroopAddAttack", CraeteHandle<TroopAddAttack>);
            Register("TroopAddDamageBuildingExtraFactor", CraeteHandle<TroopAddDamageBuildingExtraFactor>);
            Register("TroopAddDamageTroopExtraFactor", CraeteHandle<TroopAddDamageTroopExtraFactor>);
            Register("TroopAddDefence", CraeteHandle<TroopAddDefence>);
            Register("TroopAddMoveAbility", CraeteHandle<TroopAddMoveAbility>);
            Register("TroopAddSkill", CraeteHandle<TroopAddSkill>);
            Register("TroopReplaceSkill", CraeteHandle<TroopReplaceSkill>);
            Register("TroopSkillCalculateCritical", CraeteHandle<TroopSkillCalculateCritical>);
            Register("TroopSkillCalculateSuccess", CraeteHandle<TroopSkillCalculateSuccess>);
            Register("TroopSkillCalculateAttackBack", CraeteHandle<TroopSkillCalculateAttackBack>);
            Register("BuildingAddBuff", CraeteHandle<BuildingAddBuff>);
            Register("BuildingImproveFoodGain", CraeteHandle<BuildingImproveFoodGain>);
            Register("BuildingImproveGoldGain", CraeteHandle<BuildingImproveGoldGain>);
            Register("BuildingImproveFoodGainByCityTroops", CraeteHandle<BuildingImproveFoodGainByCityTroops>);
            Register("BuildingImproveTroopAttack", CraeteHandle<BuildingImproveTroopAttack>);
            Register("BuildingImproveTroopDefence", CraeteHandle<BuildingImproveTroopDefence>);
            Register("BuildingImproveTroopDefence", CraeteHandle<BuildingImproveTroopDefence>);
            Register("BuildingAddTroopMorale", CraeteHandle<BuildingAddTroopMorale>);
            Register("CityImproveJobResult", CraeteHandle<CityImproveJobResult>);
            Register("CityImproveJobCounterResult", CraeteHandle<CityImproveJobCounterResult>);
            Register("TroopIgnoreZOC", CraeteHandle<TroopIgnoreZOC>);
            Register("TroopTriggerAction", CraeteHandle<TroopTriggerAction>);
            Register("TroopChangeMorale", CraeteHandle<TroopChangeMorale>);
            Register("TroopComboAttack", CraeteHandle<TroopComboAttack>);
            Register("TroopRecure", CraeteHandle<TroopRecure>);
            Register("TroopSetFireDamage", CraeteHandle<TroopSetFireDamage>);
            Register("TroopIgnoreFire", CraeteHandle<TroopIgnoreFire>);
            Register("TroopChangeSkillCost", CraeteHandle<TroopChangeSkillCost>);
            Register("TroopChangeSkillSpellRange", CraeteHandle<TroopChangeSkillSpellRange>);
            Register("TroopSetSkillCost", CraeteHandle<TroopSetSkillCost>);
            Register("TroopChangeDamage", CraeteHandle<TroopChangeDamage>);
            Register("TroopChangeSkillAttackRange", CraeteHandle<TroopChangeSkillAttackRange>);
            Register("TroopChangeCaptiveFactor", CraeteHandle<TroopChangeCaptiveFactor>);
            Register("TroopChangeEscapeFactor", CraeteHandle<TroopChangeEscapeFactor>);
            Register("TroopSetDamage", CraeteHandle<TroopSetDamage>);
            Register("TroopChangePersonEscapeFactor", CraeteHandle<TroopChangePersonEscapeFactor>);
            Register("TroopAddBuff", CraeteHandle<TroopAddBuff>);
            Register("TroopAttackBack", CraeteHandle<TroopAttackBack>);
            Register("TroopStealFood", CraeteHandle<TroopStealFood>);
            Register("TroopAddSkillAttackRange", CraeteHandle<TroopAddSkillAttackRange>);
            Register("TroopAddSkillSpellRange", CraeteHandle<TroopAddSkillSpellRange>);
            Register("TroopModifyMorale", CraeteHandle<TroopModifyMorale>);
            Register("TroopModifyFireDamage", CraeteHandle<TroopModifyFireDamage>);
            
        }

    }
}
