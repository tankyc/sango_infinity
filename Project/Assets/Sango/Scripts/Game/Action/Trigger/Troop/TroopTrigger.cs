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
        public override City ActionCity => Troop.BelongCity;
        public override City TargetCity => DestTroop.BelongCity;
        public override Corps ActionCorps => Troop.BelongCorps;
        public override Corps TargetCorps => DestTroop.BelongCorps;
        public override Force ActionForce => Force;
        public override Force TargetForce => DestTroop.BelongForce;
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
            if (Force != null && Force != troop.BelongForce) return false;
            return true;
        }
    }
}
