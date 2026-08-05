using System.Text;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityHeadbar : UIBuildingBaseHeadbar
    {
        public static bool showIndo = false;
        public Image state;
        public Image food;
        public Image bg;
        public Text number;
        public Text info;
        public UnityEngine.GameObject corpsNode;
        public Text corpsText;
        public Image corpsImg;
        public override void UpdateState(BuildingBase building)
        {
            base.UpdateState(building);
            City city = (City)building;
            state.enabled = false;
            food.enabled = false;
            number.text = city.troops.ToString();
            if (info != null)
                info.enabled = showIndo;
            string cityInfo = $"(人:{city.allPersons.Count}闲:{city.freePersons.Count})俘虏:{city.captiveList.Count},建:{city.GetInteriorCellUsedCount()}/{city.InteriorCellCount}\n[商:{city.commerce},农:{city.agriculture}治:{city.security},训:{city.morale}]\n<金:{city.gold}+{city.totalGainGold}><粮:{city.food}+{city.totalGainFood}>\n枪:{city.itemStore.GetNumber(2)},刀:{city.itemStore.GetNumber(3)}驽:{city.itemStore.GetNumber(4)},马:{city.itemStore.GetNumber(5)}\n船:{city.itemStore.GetNumber(9)},冲车:{city.itemStore.GetNumber(7)}井阑:{city.itemStore.GetNumber(8)}";
            if (city.IsBorderCity)
                cityInfo = $"*{cityInfo}";
            info.text = cityInfo;

            bg.enabled = building.BelongForce != null;

            if (building.BelongForce != null)
            {
                bg.color = building.BelongForce.Color;
            }

            Corps corps = building.BelongCorps;
            if (corps != null && corps.IsPlayer && corps.Index > 1)
            {
                corpsNode.SetActive(true);
                corpsImg.color = building.BelongCorps.Color;
                corpsText.text = building.BelongCorps.Index.ToString();
            }
            else
            {
                corpsNode.SetActive(false);
            }

        }

        public void OnEnable()
        {
            GameEvent.OnCityHeadbarShowInfoChange += OnCityHeadbarShowInfoChange;
        }
        public void OnDisable()
        {
            GameEvent.OnCityHeadbarShowInfoChange -= OnCityHeadbarShowInfoChange;
        }

        void OnCityHeadbarShowInfoChange()
        {
            if (info != null)
                info.enabled = showIndo;
        }
    }
}
