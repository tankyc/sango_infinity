using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIMiniBuldingInfoPanel : UIMiniInfoPanel
    {
        private int delayOneFrame = 1;
        List<ObjectSortTitle> objectSortTitles = new List<ObjectSortTitle>()
        {
            BuildingSortFunction.SortByDurability_DurabilityLimit.Copy().SetAlignment((int)TextAnchor.MiddleCenter),
        };

        public UIMiniBuldingInfoPanel Show(Building c)
        {
            nameLabel.text = c.Name;
            SetCorps(c.mCorps);
            ResetPool();
            List<ObjectSortTitle> SortTitles = new List<ObjectSortTitle>(objectSortTitles);
            GameEvent.OnInitBuildingMiniPanel?.Invoke(c, SortTitles);
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