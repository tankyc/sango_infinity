using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIPersonItem : MonoBehaviour
    {
        public RawImage headIcon;
        public Text name;
        public Text feature;

        /// <summary>
        /// 设置整格的显示颜色（用于区分状态，如"被占用 = 红 / 不在本城 = 灰"；传 white 恢复默认）
        /// </summary>
        /// <param name="color">颜色</param>
        public void SetColor(Color color)
        {
            if (headIcon != null) headIcon.color = color;
            if (name != null) name.color = color;
            if (feature != null) feature.color = color;
        }

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