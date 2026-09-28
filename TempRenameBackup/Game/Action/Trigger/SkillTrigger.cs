using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class SkillTrigger : Trigger
    {
        public Cell atk_cell;
        public Troop targetTroop;
        public BuildingBase targetBuilding;
        public SkillInstance skill;
        public OverrideData<int> damageOverride;

        public override SkillInstance ActionSkill => skill;
        public override SkillInstance TargetSkill => null;
        public override Person ActionPerson => skill.master.Leader;
        public override Person TargetPerson => targetTroop?.Leader;
        public override Troop ActionTroop => skill.master;
        public override Troop TargetTroop => targetTroop;
        public override Cell ActionCell => skill.master.cell;
        public override Cell TargetCell => atk_cell;
        public override City ActionCity => skill.master.mBelongCity;
        public override City TargetCity => targetTroop?.mBelongCity ?? targetBuilding?.mBelongCity;
        public override Corps ActionCorps => skill.master.mBelongCorps;
        public override Corps TargetCorps => targetTroop?.mBelongCorps ?? targetBuilding?.mBelongCorps;
        public override Force ActionForce => skill.master.mBelongForce;
        public override Force TargetForce => targetTroop?.mBelongForce ?? targetBuilding?.mBelongForce;

        public override Fire ActiveFire => skill.master.cell.fire;
        public override Fire TargetFire => atk_cell.fire;
        public override object ActionObject => skill;
        public override object TargetObject => atk_cell;
        public override OverrideData<int> ValueOverride => damageOverride;
    }
}
