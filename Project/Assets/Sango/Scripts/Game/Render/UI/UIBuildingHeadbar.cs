using Sango.Loader;
using UnityEngine.UI;
namespace Sango.Game.Render.UI

{
    public class UIBuildingHeadbar : UGUIWindow
    {
        public Image headIcon;
        public Text name;
        public Image state;
        public Image food;
        public Image energy;
        public Image angry;
        public Text number;

        public void Init(Troop troop)
        {
            name.text = troop.Name;
            headIcon.sprite = GameRenderHelper.LoadHeadIcon(troop.Leader.headIconID);
            UpdateState(troop);
        }

        public void UpdateState(Troop troop)
        {
            state.enabled = false;
            energy.fillAmount = (float)troop.morale / 100.0f;
            angry.fillAmount = 0;
            number.text = troop.troops.ToString();
        }
    }
}
