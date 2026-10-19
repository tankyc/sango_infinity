using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityBuildingSlot : MonoBehaviour
    {
        public UIBuildingTypeItem uIBuildingTypeItem;
        public UIPersonItem[] uIPersonItems;
        public Text leftLabel;
        public int index;
        public delegate void OnClickAction(UICityBuildingSlot item);
        public Transform personNode;
        public Button upgradeBtn;
        public Transform emptyNode;
        /// <summary>建筑图标（RawImage + Texture，与建筑选择列表同口径）</summary>
        public RawImage buildImage;
        public Text nameLabel;
        public OnClickAction onClickCall;
        public Transform progress;
        public Image progressImg;

        public UICityBuildingSlot SetIndex(int i)
        {
            index = i;
            return this;
        }

        public UICityBuildingSlot SetBuilding(Building building)
        {
            if (building == null)
            {
                leftLabel.text = "";
                nameLabel.enabled = false;
                emptyNode.gameObject.SetActive(true);
                buildImage.enabled = false;
                upgradeBtn.gameObject.SetActive(false);
                personNode.gameObject.SetActive(false);
            }
            else
            {
                emptyNode.gameObject.SetActive(false);
                buildImage.enabled = true;
                nameLabel.enabled = true;

                nameLabel.text = building.BuildingType.Name;
                buildImage.texture = GameRenderHelper.LoadBuildingIcon(building.BuildingType.icon);
                if (!building.isComplate)
                {
                    personNode.gameObject.SetActive(true);
                    leftLabel.text = $"剩:{building.LeftCounter}回";
                    for (int i = 0; i < uIPersonItems.Length; i++)
                    {
                        UIPersonItem uIPersonItem = uIPersonItems[i];
                        if (i < building.Builder.Count)
                            uIPersonItem.SetPerson(building.Builder[i]);
                        else
                            uIPersonItem.SetPerson(null);
                    }

                    progressImg.fillAmount = building.durability / building.DurabilityLimit;
                    progressImg.color = new Color(0.6023496f, 0.8811504f, 0.9056604f);
                }
                else if (building.isUpgrading)
                {
                    personNode.gameObject.SetActive(true);
                    leftLabel.text = $"剩:{building.LeftCounter}回";
                    for (int i = 0; i < uIPersonItems.Length; i++)
                    {
                        UIPersonItem uIPersonItem = uIPersonItems[i];
                        if (i < building.Builder.Count)
                            uIPersonItem.SetPerson(building.Builder[i]);
                        else
                            uIPersonItem.SetPerson(null);
                    }

                    progressImg.fillAmount = building.durability / building.DurabilityLimit;
                    progressImg.color = new Color(0.9176471f, 0.7882354f, 0.1686275f);
                }
                else
                {

                    upgradeBtn.gameObject.SetActive(building.BuildingType.nextId > 0);
                    leftLabel.text = "";
                    personNode.gameObject.SetActive(false);
                }
            }
            return this;
        }

        public void OnClick()
        {
            onClickCall?.Invoke(this);
        }

    }
}