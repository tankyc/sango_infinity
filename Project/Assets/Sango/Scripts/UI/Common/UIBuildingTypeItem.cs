using Sango.Loader;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIBuildingTypeItem : MonoBehaviour
    {
        public Image icon;
        public Image icon1;
        public GameObject titleObj;
        public Text nameLabel;
        public Text costLabel;
        public Text numLabel;
        public Image select;
        public int index;
        public delegate void OnSelect(UIBuildingTypeItem item);
        public OnSelect onSelected;
        public SangoObject obj;
        public bool IsSelected()
        {
            return select.gameObject.activeSelf;
        }

        public UIBuildingTypeItem SetValid(bool b)
        {
            Color color = b ? new Color(0.85f, 0.85f, 0.85f) : Color.gray;
            icon.color = color;
            GetComponent<Button>().interactable = b;
            nameLabel.color = color;
            if (costLabel != null)
                costLabel.color = color;
            if (numLabel != null)
                numLabel.color = color;
            if (select != null) select.color = color;
            return this;
        }

        public UIBuildingTypeItem SetIndex(int i)
        {
            index = i;
            return this;
        }

        public UIBuildingTypeItem SetSelected(bool b)
        {
            Color color = b ? Color.white : new Color(0.85f, 0.85f, 0.85f);
            icon.color = color;
            if (select != null) select.gameObject.SetActive(b);
            return this;
        }

        public UIBuildingTypeItem SetNum(int c)
        {
            return SetNum(c, false);
        }

        /// <summary>
        /// 设置数量文本
        /// </summary>
        /// <param name="c">数量，小于0时表示不显示数量(清空文本)</param>
        /// <param name="changed">数量相对原始值是否发生了变化，变化时使用黄色，未变化时使用白色</param>
        public UIBuildingTypeItem SetNum(int c, bool changed)
        {
            if (numLabel == null) return this;

            if (c < 0)
                numLabel.text = "";
            else
                numLabel.text = c.ToString();

            // 根据数量是否变化切换颜色，变化用黄色提示，未变化恢复白色
            numLabel.color = changed ? Color.yellow : Color.white;
            return this;
        }

        public UIBuildingTypeItem SetBuildingType(BuildingType buildingType)
        {
            obj = buildingType;
            if (buildingType == null)
            {
                icon.enabled = false;
                nameLabel.text = "";
                if (costLabel != null)
                    costLabel.text = "";
                if (numLabel != null)
                    numLabel.text = "";
            }
            else
            {
                icon.enabled = true;
                nameLabel.text = buildingType.Name;
                if (costLabel != null)
                    costLabel.text = buildingType.cost.ToString();
                icon.sprite = GameRenderHelper.LoadBuildingTypeIcon(buildingType.icon);
                if (icon1 != null)
                    icon1.sprite = icon.sprite;
                if (numLabel != null)
                    numLabel.text = "";
            }
            return this;
        }

        public UIBuildingTypeItem SetItemType(ItemType itemType)
        {
            obj = itemType;
            if (itemType == null)
            {
                icon.enabled = false;
                nameLabel.text = "";
                if (costLabel != null)
                    costLabel.text = "";
                if (numLabel != null)
                    numLabel.text = "";
            }
            else
            {
                icon.enabled = true;
                nameLabel.text = itemType.Name;
                if (costLabel != null)
                    costLabel.text = itemType.cost.ToString();
                icon.sprite = GameRenderHelper.LoadBuildingTypeIcon(itemType.icon);
                if (icon1 != null)
                    icon1.sprite = icon.sprite;
                if (numLabel != null)
                    numLabel.text = "";
                icon.color = new Color(0.85f, 0.85f, 0.85f);
            }
            return this;
        }

        public UIBuildingTypeItem SetTroopType(TroopType troopType)
        {
            obj = troopType;
            if (troopType == null)
            {
                icon.enabled = false;
                nameLabel.text = "";
                if (costLabel != null)
                    costLabel.text = "";
                if (numLabel != null)
                    numLabel.text = "";
            }
            else
            {
                icon.enabled = true;
                nameLabel.text = troopType.Name;
                if (costLabel != null)
                    costLabel.text = "";
                icon.sprite = GameRenderHelper.LoadBuildingTypeIcon(troopType.icon);
                if (icon1 != null)
                    icon1.sprite = icon.sprite;
                if (numLabel != null)
                    numLabel.text = "";
                icon.color = new Color(0.85f, 0.85f, 0.85f);
            }
            return this;
        }
        public void OnClick()
        {
            onSelected?.Invoke(this);
        }

    }
}