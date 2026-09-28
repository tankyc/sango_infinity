using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIPersonItem : MonoBehaviour
    {
        public RawImage headIcon;
        public Text name;
        public Text feature;

        public void SetPerson(Person person, int headIconType = 2)
        {
            if (person != null)
            {
                headIcon.texture = GameRenderHelper.LoadHeadIcon(person.headIconID, headIconType);
                headIcon.enabled = true;
                name.text = person.Name;
                if (person.FeatureList != null && person.FeatureList.Count > 0)
                    feature.text = person.FeatureList[0].Name;
                else
                    feature.text = "";
            }
            else
            {
                headIcon.texture = null;
                headIcon.enabled = false;
                name.text = "";
                feature.text = "";
            }
        }

    }
}