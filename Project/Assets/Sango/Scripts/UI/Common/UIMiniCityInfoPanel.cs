using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMiniCityInfoPanel : UIMiniInfoPanel
    {
        public Image icon;
        private int delayOneFrame = 1;
        List<ObjectSortTitle> objectSortTitles = new List<ObjectSortTitle>()
        {
            CitySortFunction.SortByLeader.Copy().SetAlignment((int)TextAnchor.MiddleCenter),
            CitySortFunction.SortByGold.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortByFood.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortBySecurity_SecurityLimit.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortByDurability_DurabilityLimit.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortByTroops.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortByMorale.Copy().SetAlignment((int)TextAnchor.MiddleRight),
            CitySortFunction.SortByAllPersonCountInfo.Copy().SetAlignment((int)TextAnchor.MiddleRight),
        };

        public UIMiniCityInfoPanel Show(City c)
        {
            nameLabel.text = c.Name;
            SetCorps(c.mCorps);
            ResetPool();
            List<ObjectSortTitle> SortTitles = new List<ObjectSortTitle>(objectSortTitles);
            GameEvent.OnInitCityMiniPanel?.Invoke(c, SortTitles);
            for (int i = 0; i < SortTitles.Count; i++)
            {
                ObjectSortTitle title = SortTitles[i];
                AddInfo(title.name, title.GetValueStr(c), title.alignment);
            }
            delayOneFrame = 1;
            return this;
        }

        void Update()
        {
            if(delayOneFrame > 0)
            {
                delayOneFrame--;
            }
            else if(delayOneFrame == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
            }
        }
    }
}