using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIItemType : MonoBehaviour
    {
        public UITextField name;
        public UITextField numberLabel;

        /// <summary>道具图标（RawImage + Texture；prefab 未挂图标组件时为 null，此时只显示文字）</summary>
        public RawImage icon;

        public ItemType itemType;
        public int maxNumber;
        public int useNumber;

        public UIItemType SetItemType(ItemType type)
        {
            itemType = type;
            name.text = type.Name;
            SetIcon(type);
            return this;
        }

        /// <summary>
        /// 设置道具图标。
        ///
        /// 用 RawImage + Texture 而不是 Image + Sprite：与头像 <c>LoadHeadIcon</c> 同一套口径，
        /// 走 <see cref="GameRenderHelper.LoadItemIcon"/>（资源目录 <c>Assets/ItemIcon</c>）。
        ///
        /// 全部道具（兵装 / 器械 / 船 / 宝物 / 装备）统一口径：icon 字段写 Id，
        /// 资源文件按 Id 命名放在同一个目录，不再指向 UI/AtlasTexture 图集。
        /// </summary>
        void SetIcon(ItemType type)
        {
            if (icon == null)
                return;

            // icon 缺省时隐藏，避免把上一个道具的图留在格子上
            if (type == null || string.IsNullOrEmpty(type.icon))
            {
                icon.enabled = false;
                return;
            }

            icon.enabled = true;
            icon.texture = GameRenderHelper.LoadItemIcon(type.icon);
        }

        public UIItemType SetNumber(int num)
        {
            maxNumber = num;
            if (num > 0)
                numberLabel.text = num.ToString();
            else
                numberLabel.text = $"<color=#ff0000>{num}</color>";
            return this;
        }

        public UIItemType SetUsed(int num)
        {
            if (maxNumber <= 0)
                return this;

            int left = maxNumber - num;
            if (left > 0)
            {
                if (num == 0)
                    numberLabel.text = maxNumber.ToString();
                else
                    numberLabel.text = $"{maxNumber}→{left}";
            }
            else
            {
                if (maxNumber == 0)
                    numberLabel.text = $"<color=#ff0000>{maxNumber}</color>";
                else
                    numberLabel.text = $"{maxNumber}→<color=#ff0000>{left}</color>";
            }
            return this;
        }

    }
}