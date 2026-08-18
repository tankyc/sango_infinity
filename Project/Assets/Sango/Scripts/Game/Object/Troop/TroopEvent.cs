
/*
 * 文件名：GameEvent.cs
 * 描述：游戏事件类，定义游戏中所有的事件委托
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Sango.Core.Tools;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{

    public class TroopEvent : EventBase
    {
        /// <summary>
        /// 部队AI
        /// </summary>
        public EventDelegate<Troop, Scenario> OnAIStart;
        public EventDelegate<Troop, Scenario> OnAIPrepare;
        public EventDelegate<Troop, Scenario> OnAIEnd;

        public EventDelegate<Troop, Cell, Cell> OnLeaveCell;
        public EventDelegate<Troop, Cell, Cell> OnEnterCell;

        public EventDelegate<Troop, Scenario> OnTurnStart;
        public EventDelegate<Troop, Scenario> OnTurnEnd;

        /// <summary>
        /// 部队组建的时候
        /// </summary>
        public EventDelegate<Troop, Scenario> OnCreated;

        /// <summary>
        /// 部队溃灭的时候
        /// </summary>
        public EventDelegate<Troop, Scenario> OnDestroyed;

        /// <summary>
        /// 部队计算属性的时候
        /// </summary>
        public EventDelegate<Troop, Scenario> OnCalculateAttribute;
        public EventDelegate<Troop, Scenario> OnAfterCalculateAttribute;

        /// <summary>
        /// 部队计算反击的时候
        /// </summary>
        public EventDelegate<Troop, Troop, SkillInstance, Scenario, OverrideData<int>> OnCalculateAttackBack;

        /// <summary>
        /// 可监听改计算部队最大兵力
        /// City, Troop, OverrideData
        /// </summary>
        public EventDelegate<City, Troop, OverrideData<int>> OnCalculateMaxTroops;

        /// <summary>
        /// 可监听改计算战法成功率(百分比) 必爆, 设置100则为必中, 设置为0则必不中
        /// City, Skill, spellCell, OverrideFunc
        /// </summary>
        public EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnBeforeCalculateSkillSuccess;

        /// <summary>
        /// 可监听改计算战法成功率(百分比)
        /// City, Skill, spellCell, OverrideFunc
        /// </summary>
        public EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnAfterCalculateSkillSuccess;

        /// <summary>
        /// 可监听改计算战法暴击率(百分比) 必爆, 设置100则为必爆, 设置为0则必不爆
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnBeforeCalculateSkillCritical;

        /// <summary>
        /// 可监听改计算战法暴击率(百分比)
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnAfterCalculateSkillCritical;

        /// <summary>
        /// 可监听改计算战法暴击时的伤害倍率(百分比)
        /// City, Skill, spellCell,  OverrideFunc
        /// </summary>
        public EventDelegate<Troop, SkillInstance, Cell, OverrideData<int>> OnCalculateSkillCriticalFactor;

        /// <summary>
        /// 当部队兵力变化时
        /// </summary>
        public EventDelegate<Troop, SangoObject, SkillInstance, int, OverrideData<int>> OnChangeTroops;

        /// <summary>
        /// 当部队气力变化时
        /// </summary>
        public EventDelegate<Troop, int, OverrideData<int>> OnChangeMorale;

        /// <summary>
        /// 当部队结束行动时
        /// </summary>
        public EventDelegate<Troop> OnActionOver;

        /// <summary>
        /// 技能实例计算属性时
        /// </summary>
        public EventDelegate<Troop, SkillInstance> OnSkillCalculateAttribute;

        /// <summary>
        /// 技能实例命中敌人时候
        /// </summary>
        public EventDelegate<SkillInstance, Troop, OverrideData<int>> OnSkillDamageTroop;

        /// <summary>
        /// 技能实例命中敌人之后
        /// </summary>
        public EventDelegate<SkillInstance, Troop, OverrideData<int>> OnSkillDamageTroopAfter;

        /// <summary>
        /// 技能实例命中建筑士兵
        /// </summary>
        public EventDelegate<SkillInstance, BuildingBase, OverrideData<int>> OnSkillDamageBuildingTroops;

        /// <summary>
        /// 技能实例命中建筑耐久
        /// </summary>
        public EventDelegate<SkillInstance, BuildingBase, OverrideData<int>> OnSkillDamageBuildingDurability;

        /// <summary>
        /// 技能实例效果触发结束
        /// </summary>
        public EventDelegate<SkillInstance> OnSkillActionOver;

        /// <summary>
        /// 技能实例效果触发结束
        /// </summary>
        public EventDelegate<SkillInstance, Cell, Troop, BuildingBase> OnSkillActionEnd;

        /// <summary>
        /// 放火
        /// </summary>
        public EventDelegate<SkillInstance, Fire> OnFireAdd;

        public void Clear()
        {

        }
    }
}