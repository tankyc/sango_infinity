using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class TroopTrigger : Trigger
    {
        public Troop Troop;
        public Troop DestTroop;
        public Force Force;
        public OverrideData<int> valueOverride;
        public override OverrideData<int> ValueOverride => valueOverride;
        public override Troop ActionTroop => Troop;
        public override Troop TargetTroop => DestTroop;
        public override Cell ActionCell => Troop.cell;
        public override Cell TargetCell => DestTroop.cell;
        public override City ActionCity => Troop.mBelongCity;
        public override City TargetCity => DestTroop.mBelongCity;
        public override Corps ActionCorps => Troop.mBelongCorps;
        public override Corps TargetCorps => DestTroop.mBelongCorps;
        public override Force ActionForce => Force;
        public override Force TargetForce => DestTroop.mBelongForce;
        public override Fire ActiveFire => Troop.cell.fire;
        public override Fire TargetFire => Troop.cell.fire;
        public override object ActionObject => Troop;
        public override object TargetObject => DestTroop;

        public override void Init(TriggerCall call, params SangoObject[] sangoObjects)
        {
            base.Init(call, sangoObjects);
            Troop = sangoObjects[0] as Troop;
            if(Troop == null)
            {
                Force = sangoObjects[0] as Force;
            }
        }

        public virtual bool CheckForceTroop(Troop troop)
        {
            if (Troop != null && Troop != troop) return false;
            if (Force != null && Force != troop.mBelongForce) return false;
            return true;
        }
    }
}
