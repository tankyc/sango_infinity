namespace Sango.Core
{
    public class TroopBanishTroop : TroopDestroyTroop
    {
        public override MissionType MissionType { get { return MissionType.TroopBanishTroop; } }
        public override bool IsMissionComplete
        {
            get
            {
                return (TargetTroop == null ||
                    !TargetTroop.IsAlive ||
                    !TargetTroop.IsEnemy(Troop) ||

                    // 出了边境即可
                    !TargetTroop.cell.BelongCity.IsSameForce(Troop)

                    );
            }
        }

    }
}
