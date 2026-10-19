using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;
using Sango.Core;
namespace Sango.UI

{
    public class UIBuildingHeadbar : UIBuildingBaseHeadbar
    {
        public UIWorker[] workers;
        public RectTransform workerNode;
        public RectTransform productNode;
        /// <summary>产出道具图标（RawImage + Texture，与道具格 UIBuildingTypeItem 同口径）</summary>
        public RawImage productImg;

        public void ShowWorker(Building building)
        {
            int workerCount = building.BuildingType.workerLimit;
            for (int i = 0; i < workers.Length; i++)
            {
                UIWorker uIWorker = workers[i];
                if (i < workerCount || (building.Workers != null && !building.isComplate && i < building.Workers.Count))
                {
                    if (building.Workers != null && i < building.Workers.Count)
                    {
                        uIWorker.SetEnabled(true);
                        uIWorker.SetPerson(building.Workers.Get(i));
                    }
                    else
                    {
                        uIWorker.SetEnabled(false);
                        uIWorker.SetPerson(null);
                    }
                }
                else
                {
                    uIWorker.SetEnabled(false);
                }
            }
        }

        public void ShowProduct(Building building)
        {
            if (building.ProductItemId <= 0)
            {
                productNode.gameObject.SetActive(false);
                return;
            }

            productNode.gameObject.SetActive(true);
            ItemType itemType = Scenario.Cur.GetObject<ItemType>(building.ProductItemId);
            if (itemType != null)
            {
                productImg.texture = GameRenderHelper.LoadItemIcon(itemType.icon);
            }
        }

        public override void UpdateState(BuildingBase building)
        {
            base.UpdateState(building);
            ShowWorker(building as Building);
            ShowProduct(building as Building);
        }
    }
}
