using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMiniTroopInfoPanel : UIMiniInfoPanel
    {
        public Image icon;
        public UITextField troopType;
        public UITextField troopTypeLevel;

        private int delayOneFrame = 1;
        List<ObjectSortTitle> objectSortTitles = new List<ObjectSortTitle>()
        {
            TroopSortFunction.SortByMember1.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            TroopSortFunction.SortByMember2.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            TroopSortFunction.SortByGold.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            TroopSortFunction.SortByFood.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            TroopSortFunction.SortByTroops.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            TroopSortFunction.SortByMoraleByMax.Copy().SetAlignment((int)TextAnchor.MiddleRight),
        };


        public UIMiniTroopInfoPanel Show(Troop c)
        {
            nameLabel.text = c.Name;
            SetCorps(c.mBelongCorps);
            ResetPool();
            List<ObjectSortTitle> SortTitles = new List<ObjectSortTitle>(objectSortTitles);
            GameEvent.OnInitTroopMiniPanel?.Invoke(c, SortTitles);
            for (int i = 0; i < SortTitles.Count; i++)
            {
                ObjectSortTitle title = SortTitles[i];
                UITextField textField = AddInfo(title.name, title.GetValueStr(c), title.alignment);
                textField.titleLabel.transform.parent.gameObject.SetActive(title != objectSortTitles[1]);
            }
            delayOneFrame = 1;
            troopType.text = c.TroopType.Name;
            troopTypeLevel.text = Scenario.Cur.Variables.GetAbilityName(c.TroopTypeLv);
            return this;
        }
        void Update()
        {
            if (delayOneFrame > 0)
            {
                delayOneFrame--;
            }
            else if (delayOneFrame == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            }
        }
    }
}