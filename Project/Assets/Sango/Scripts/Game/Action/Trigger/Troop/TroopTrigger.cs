using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class TroopTrigger : Trigger
    {
        public Troop Troop;
        public Force targetForce;
        public OverrideData<int> damageOverride;
        public override OverrideData<int> DamageOverride => damageOverride;
        public override Troop ActionTroop => Troop;
        public override Troop TargetTroop => Troop;
        public override Cell ActionCell => Troop.cell;
        public override Cell TargetCell => Troop.cell;
        public override City ActionCity => Troop.mBelongCity;
        public override City TargetCity => Troop.mBelongCity;
        public override Corps ActionCorps => Troop.mBelongCorps;
        public override Corps TargetCorps => Troop.mBelongCorps;
        public override Force ActionForce => Troop.mBelongForce;
        public override Force TargetForce => targetForce;
        public override Fire ActiveFire => Troop.cell.fire;
        public override Fire TargetFire => Troop.cell.fire;
        public override object ActionObject => Troop;
        public override object TargetObject => Troop;

        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            Troop = sangoObjects[0] as Troop;
        }
    }
}
