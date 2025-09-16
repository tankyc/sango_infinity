using Sango.Render;
using System.Globalization;
using UnityEngine;

namespace Sango.Game.Render
{
    public class BuildingRender : ObjectRender
    {
        UnityEngine.UI.Text textInfo { get; set; }
        Building Building { get; set; }
        public BuildingRender(Building building)
        {
            Owener = building;
            Building = building;
            MapObject = MapObject.Create($"{Building.BelongCity.Name}-{Building.Name}");
            MapObject.objType = Building.BuildingType.kind;
            MapObject.modelId = Building.BuildingType.model;
            MapObject.transform.position = Building.CenterCell.Position;
            MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, Building.rot, 0));
            MapObject.transform.localScale = Vector3.one;
            MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
            MapObject.modelLoadedCallback = OnModelLoaded;
            MapRender.Instance.AddStatic(MapObject);

            GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("TroopName")) as GameObject;
            obj.transform.SetParent(MapObject.transform, false);
            obj.transform.localPosition = new Vector3(0, 20, 0);
            UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
            textInfo = text;
            UpdateInfo();
        }

        public void OnModelLoaded(GameObject obj)
        {

            Renderer[] renderers = MapObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (Building.BelongForce != null)
                {
                    renderers[i].material.SetColor("_BaseColor", Building.BelongForce.Flag.color);
                }
                else
                {
                    renderers[i].material.SetColor("_BaseColor", UnityEngine.Color.white);
                }
            }
        }

        public void UpdateInfo()
        {
            textInfo.color = Building.BelongForce.Flag.color;
            textInfo.text = $"<{Building.BelongForce.Name}>\n[{Building.Name}]\n [{Building.durability}]";
        }

        public override void UpdateRender()
        {
            if (MapObject.visible)
            {
                OnModelLoaded(MapObject.loadedModel);
            }
            UpdateInfo();
        }
    }
}
