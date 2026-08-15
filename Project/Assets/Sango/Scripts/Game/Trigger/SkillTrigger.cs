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
        public override City ActionCity => skill.master.BelongCity;
        public override City TargetCity => targetTroop?.BelongCity ?? targetBuilding?.mCity;
        public override Corps ActionCorps => skill.master.BelongCorps;
        public override Corps TargetCorps => targetTroop?.BelongCorps ?? targetBuilding?.mCorps;
        public override Force ActionForce => skill.master.BelongForce;
        public override Force TargetForce => targetTroop?.BelongForce ?? targetBuilding?.mForce;

        public override Fire ActiveFire => skill.master.cell.fire;
        public override Fire TargetFire => atk_cell.fire;
        public override object ActionObject => skill;
        public override object TargetObject => atk_cell;
        public override OverrideData<int> DamageOverride => damageOverride;
    }
}
