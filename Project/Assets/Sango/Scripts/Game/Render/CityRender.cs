using Sango.Render;
using System.Globalization;
using UnityEngine;

namespace Sango.Game.Render
{
    public class CityRender : ObjectRender
    {
        City City { get; set; }
        MapObject wall;
        MapObject center;
        UnityEngine.UI.Text textInfo { get; set; }

        public CityRender(City city)
        {
            Owener = city;
            City = city;
            if (city.models != null)
            {
                for (int i = 0; i < city.models.Count; i++)
                {
                    MapObject = MapObject.Create(city.Name);
                    MapObject.objType = city.BuildingType.kind;
                    MapObject.modelId = city.models[i];
                    MapObject.transform.position = city.CenterCell.Position;
                    MapObject.transform.rotation = Quaternion.Euler(new Vector3(0, city.rot, 0));
                    MapObject.transform.localScale = Vector3.one;
                    MapObject.bounds = new Sango.Tools.Rect(0, 0, 32, 32);
                    MapRender.Instance.AddStatic(MapObject);

                    MapObject.modelLoadedCallback = OnModelLoaded;

                    GameObject obj = GameObject.Instantiate(Resources.Load<GameObject>("CityName")) as GameObject;
                    obj.transform.SetParent(MapObject.transform, false);
                    obj.transform.localPosition = new Vector3(0, 20, 0);
                    UnityEngine.UI.Text text = obj.GetComponent<UnityEngine.UI.Text>();
                    textInfo = text;
                    UpdateInfo();

                }
            }
        }

        public void OnModelLoaded(GameObject obj)
        {

            Renderer[] renderers = MapObject.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (City.BelongForce != null)
                {
                    renderers[i].material.SetColor("_BaseColor", City.BelongForce.Flag.color);
                }
                else
                {
                    renderers[i].material.SetColor("_BaseColor", UnityEngine.Color.white);
                }
            }
        }

        public void UpdateInfo()
        {
            if (City.BelongForce != null)
            {
                textInfo.color = City.BelongForce.Flag.color;
            }
            else
            {
                textInfo.color = Color.white;
            }
            string cityInfo = $"{City.BelongForce?.Name}.{City.Name}[耐:{City.durability}](人:{City.allPersons.Count} 闲:{City.freePersons.Count})\n[商:{City.commerce},农:{City.agriculture},治:{City.security},建:{City.allBuildings.Count}/{City.CityLevelType.insideSlot+ City.CityLevelType.outsideSlot}]\n[兵:{City.troops}]\n<金:{City.gold}+{City.totalGainGold}>\n<粮:{City.food}+{City.totalGainFood}>";
            if (City.IsBorderCity)
                cityInfo = $"*{cityInfo}";
            textInfo.text = cityInfo;
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
