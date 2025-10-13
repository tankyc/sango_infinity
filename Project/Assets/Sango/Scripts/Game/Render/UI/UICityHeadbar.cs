using System.Text;
using UnityEngine.UI;

namespace Sango.Game.Render.UI
{
    public class UICityHeadbar : UGUIWindow
    {
        public Text name;
        public Image state;
        public Image food;
        public Image durability;
        public Text number;
        public Text info;
        public AnimationText aniText;

        public void Init(City city)
        {
            name.text = city.Name;
            UpdateState(city);
        }

        public void UpdateState(City city)
        {
            state.enabled = false;
            food.enabled = false;
            durability.fillAmount = (float)city.durability / city.CityLevelType.maxDurability;
            number.text = city.troops.ToString();
            string cityInfo = $"(人:{city.allPersons.Count} 闲:{city.freePersons.Count}) [商:{city.commerce},农:{city.agriculture},治:{city.security},建:{city.allBuildings.Count}/{city.CityLevelType.insideSlot}]\n<金:{city.gold}+{city.totalGainGold}>\n<粮:{city.food}+{city.totalGainFood}>";
            if (city.IsBorderCity)
                cityInfo = $"*{cityInfo}";
            info.text = cityInfo;
        }

           public void ShowDamage(int damage, int damageType = 13)
        {
            UITools.ShowDamage(aniText, damage, damageType);
        }

    }
}
