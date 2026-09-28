using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 条件基类，所有游戏中的条件判断都继承自此类
    /// </summary>
    public interface IConditionDatabase
    {
        SkillInstance ActionSkill { get; }
        SkillInstance TargetSkill { get; }
        Person ActionPerson { get; }
        Person TargetPerson { get; }
        Troop ActionTroop { get; }
        Troop TargetTroop { get; }
        Cell ActionCell { get; }
        Cell TargetCell { get; }
        City ActionCity { get; }
        City TargetCity { get; }
        Corps ActionCorps { get; }
        Corps TargetCorps { get; }
        Force ActionForce { get; }
        Force TargetForce { get; }
        object ActionObject { get; }
        object TargetObject { get; }
    }

    public struct DefaultConditionDatabase : IConditionDatabase
    {
        public SkillInstance ActionSkill { get; set; }
        public SkillInstance TargetSkill { get; set; }
        public Person ActionPerson { get; set; }
        public Person TargetPerson { get; set; }
        public Troop ActionTroop { get; set; }
        public Troop TargetTroop { get; set; }
        public Cell ActionCell { get; set; }
        public Cell TargetCell { get; set; }
        public City ActionCity { get; set; }
        public City TargetCity { get; set; }
        public Corps ActionCorps { get; set; }
        public Corps TargetCorps { get; set; }
        public Force ActionForce { get; set; }
        public Force TargetForce { get; set; }
        public object ActionObject { get; set; }
        public object TargetObject { get; set; }
    }
}
