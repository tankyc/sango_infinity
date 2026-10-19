using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIStatusItem : MonoBehaviour
    {
        public float maxValue = 100f;
        public DrawStatusComponent status;
        public Text[] statusTitle1;
        public UITextField[] statusValue;

        public void SetStatus(int index, string title, int value)
        {
            if (index >= statusTitle1.Length || index < 0)
            {
                return;
            }
            statusTitle1[index].text = title;
            statusValue[index].titleLabel.text = title;
            statusValue[index].text = ObjectSortTitle.GetAttributeColorText(value);
            status.scaleLenth[index] = Mathf.Min(1, value / maxValue);
            status.UpdateContent();
        }

        public void SetPerson(Person person)
        {
            if (person == null)
            {
                SetStatus(0, PersonSortFunction.SortByCommand.name, 0);
                SetStatus(1, PersonSortFunction.SortByStrength.name, 0);
                SetStatus(2, PersonSortFunction.SortByIntelligence.name, 0);
                SetStatus(3, PersonSortFunction.SortByPolitics.name, 0);
                SetStatus(4, PersonSortFunction.SortByGlamour.name, 0);
                return;
            }

            SetStatus(0, PersonSortFunction.SortByCommand.name, person.Command);
            SetStatus(1, PersonSortFunction.SortByStrength.name, person.Strength);
            SetStatus(2, PersonSortFunction.SortByIntelligence.name, person.Intelligence);
            SetStatus(3, PersonSortFunction.SortByPolitics.name, person.Politics);
            SetStatus(4, PersonSortFunction.SortByGlamour.name, person.Glamour);
        }

        public void SetTroop(Troop troop)
        {
            if (troop == null)
            {
                SetStatus(0, TroopSortFunction.SortByAttack.name, 0);
                SetStatus(1, TroopSortFunction.SortByDefence.name, 0);
                SetStatus(2, TroopSortFunction.SortByIntelligence.name, 0);
                SetStatus(3, TroopSortFunction.SortByBuild.name, 0);
                SetStatus(4, TroopSortFunction.SortByMoveability.name, 0);
                return;
            }

            SetStatus(0, TroopSortFunction.SortByAttack.name, troop.Attack);
            SetStatus(1, TroopSortFunction.SortByDefence.name, troop.Defence);
            SetStatus(2, TroopSortFunction.SortByIntelligence.name, troop.Intelligence);
            SetStatus(3, TroopSortFunction.SortByBuild.name, troop.BuildPower);
            SetStatus(4, TroopSortFunction.SortByMoveability.name, troop.MoveAbility);
        }

        public void SetTroopStatus(int v1, int v2, int v3, int v4, int v5)
        {

            SetStatus(0, TroopSortFunction.SortByAttack.name, v1);
            SetStatus(1, TroopSortFunction.SortByDefence.name, v2);
            SetStatus(2, TroopSortFunction.SortByIntelligence.name, v3);
            SetStatus(3, TroopSortFunction.SortByBuild.name, v4);
            SetStatus(4, TroopSortFunction.SortByMoveability.name, v5);
        }
    }
}