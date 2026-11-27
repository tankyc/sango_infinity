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
        public Image[] statusExp;

        public void SetStatus(int index, string title, int value, float expProgress = -1f)
        {
            if (index >= statusTitle1.Length || index < 0)
            {
                return;
            }
            statusTitle1[index].text = title;
            statusValue[index].titleLabel.text = title;
            statusValue[index].text = ObjectSortTitle.GetAttributeColorText(value);
            if (status != null && status.scaleLenth != null && index < status.scaleLenth.Length)
            {
                // maxValue 在预制体漏填时是 0；直接 value / maxValue 会抛 DivideByZeroException。
                float divisor = maxValue > 0f ? maxValue : 1f;
                status.scaleLenth[index] = Mathf.Clamp01(value / divisor);
                status.UpdateContent();
            }

            // expProgress < 0 表示调用方没有经验数据（如部队五维），保持预制体原状；
            // 武将能力则传入 0~1 的"到下一次增长"的进度。
            if (expProgress >= 0f && statusExp != null && index < statusExp.Length && statusExp[index] != null)
            {
                statusExp[index].fillAmount = Mathf.Clamp01(expProgress);
            }
        }

        public void SetPerson(Person person)
        {
            if (person == null)
            {
                SetStatus(0, PersonSortFunction.SortByCommand.name, 0, 0f);
                SetStatus(1, PersonSortFunction.SortByStrength.name, 0, 0f);
                SetStatus(2, PersonSortFunction.SortByIntelligence.name, 0, 0f);
                SetStatus(3, PersonSortFunction.SortByPolitics.name, 0, 0f);
                SetStatus(4, PersonSortFunction.SortByGlamour.name, 0, 0f);
                return;
            }

            SetStatus(0, PersonSortFunction.SortByCommand.name, person.Command, person.command.ExpProgress);
            SetStatus(1, PersonSortFunction.SortByStrength.name, person.Strength, person.strength.ExpProgress);
            SetStatus(2, PersonSortFunction.SortByIntelligence.name, person.Intelligence, person.intelligence.ExpProgress);
            SetStatus(3, PersonSortFunction.SortByPolitics.name, person.Politics, person.politics.ExpProgress);
            SetStatus(4, PersonSortFunction.SortByGlamour.name, person.Glamour, person.glamour.ExpProgress);
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