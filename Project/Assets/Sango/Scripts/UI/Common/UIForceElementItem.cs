using Sango.Loader;
using Sango.Render;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UIForceElementItem : MonoBehaviour
    {
        public Text name;
        public Image icon;
        public Button button;
        SangoObject targetObject;

        public UIForceElementItem SetName(string n)
        {
            name.text = n;
            return this;
        }
        public UIForceElementItem BindCall(UnityAction call)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(call);
            return this;
        }
        public UIForceElementItem SetIcon(string asset)
        {
            icon.sprite = ObjectLoader.LoadObject<UnityEngine.Sprite>(asset);
            return this;
        }

        public UIForceElementItem SetWidth(int width)
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);
            LayoutElement layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.preferredWidth = width;
            return this;
        }

        public UIForceElementItem SetSangoObject(SangoObject obj)
        {
            targetObject = obj;
            name.fontSize = 20;
            if (obj == null)
            {
                name.text = "";
                icon.enabled = false;
            }
            else
            {
                if (obj is City)
                {
                    City c = (City)obj;
                    name.text = obj.Name;
                    name.color = c.FreePersonCount == 0 ? Color.gray : GameDefine.whiteText;
                    icon.enabled = false;
                    if (c.IsPort() || c.IsGate())
                        name.fontSize = 18;
                }
                else if (obj is Person)
                {
                    name.text = obj.Name;

                    Person p = (Person)obj;
                    if (p.mBelongTroop != null)
                    {
                        name.color = p.mBelongTroop.ActionOver ? Color.gray : GameDefine.whiteText;
                    }
                    else
                    {
                        name.color = obj.ActionOver ? Color.gray : GameDefine.whiteText;
                    }
                    icon.enabled = false;
                }
                else if (obj is Troop)
                {
                    Troop troop = (Troop)obj;
                    name.text = obj.Name + "¶Ó";
                    name.color = obj.ActionOver ? Color.gray : GameDefine.whiteText;
                    string spName;
                    if (troop.missionType > 0)
                    {
                        spName = troop.ActionOver ? "4846-9/4846-9_21" : "4846-9/4846-9_19";
                    }
                    else
                    {
                        spName = troop.ActionOver ? "4846-9/4846-9_17" : "4846-9/4846-9_18";
                    }
                    if (!icon.sprite.name.Equals(spName))
                        icon.sprite = GameRenderHelper.LoadTroopStateIcon(spName);
                    icon.enabled = true;
                }
            }
            return this;
        }

        public void OnClick()
        {
            if (targetObject == null) return;
            if (targetObject is City)
            {
                City city = (City)targetObject;
                Vector3 position = city.CenterCell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
            else if (targetObject is Person)
            {
                Person person = (Person)targetObject;
                if (person.mBelongTroop != null)
                {
                    Vector3 position = person.mBelongTroop.cell.Position;
                    MapRender.Instance.MoveCameraTo(position);
                }
                else
                {
                    Vector3 position = person.BelongCity.CenterCell.Position;
                    MapRender.Instance.MoveCameraTo(position);
                }
            }
            else if (targetObject is Troop)
            {
                Troop troop = (Troop)targetObject;
                Vector3 position = troop.cell.Position;
                MapRender.Instance.MoveCameraTo(position);
            }
        }

    }
}