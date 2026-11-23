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
                // 只显示**生效**的第一个特技（超出剧本上限的特技不展示）。
                // 第 1 槽尚未解锁时由 GetFeatureDisplayName 套灰色富文本，与详情界面口径一致。
                if (person.ActiveFeatureCount > 0)
                    feature.text = Person.GetFeatureDisplayName(person, 0);
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