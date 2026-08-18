using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIScenarioCityMap : MonoBehaviour
    {
        public UICityMapItem cityMapItem;
        public UICityMapItem portMapItem;
        public UICityMapItem troopMapItem;
        public RectTransform mapBounds;
        CreatePool<UICityMapItem> cityPool;
        CreatePool<UICityMapItem> portPool;
        CreatePool<UICityMapItem> troopPool;

        private void Awake()
        {
            cityPool = new CreatePool<UICityMapItem>(cityMapItem);
            portPool = new CreatePool<UICityMapItem>(portMapItem);
            troopPool = new CreatePool<UICityMapItem>(troopMapItem);
        }

        public void Show(Scenario scenario)
        {
            Show(scenario, (City)null);
        }

        public void Show(ShortScenario scenario)
        {
            Show(scenario, null);
        }

        public void Show(Scenario scenario, Force current)
        {
            cityPool.Reset();
            troopPool.Reset();
            portPool.Reset();
            scenario.citySet.ForEach(c =>
            {
                if (c.BuildingType.Id == 1 && c.Id > 0)
                {
                    City city = c;
                    UICityMapItem item = cityPool.Create();
                    item.name = city.Id.ToString();
                    RectTransform rectTransform = item.root;
                    float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                    item.icon.color = city.mBelongForce != null ? city.mBelongForce.Color : Color.white;
                    item.effect?.SetActive(city.mBelongForce == current);
                }
            });
        }
        public void Show(Scenario scenario, City current)
        {
            cityPool.Reset();
            troopPool.Reset();
            portPool.Reset();
            scenario.citySet.ForEach(c =>
            {
                if (c.BuildingType.Id == 1 && c.Id > 0)
                {
                    City city = c;
                    UICityMapItem item = cityPool.Create();
                    item.name = city.Id.ToString();
                    RectTransform rectTransform = item.root;
                    float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                    item.icon.color = city.mBelongForce != null ? city.mBelongForce.Color : Color.white;
                    item.effect?.SetActive(current.IsCity() && city.Id == current.Id);
                }

                if(!current.IsCity())
                {
                    City city = current;
                    UICityMapItem item = portPool.Create();
                    item.name = city.Id.ToString();
                    RectTransform rectTransform = item.root;
                    float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                    item.icon.color = city.mBelongForce != null ? city.mBelongForce.Color : Color.white;
                    item.effect?.SetActive(true);
                }
            });
        }
        public void Show(Scenario scenario, Troop current)
        {
            cityPool.Reset();
            troopPool.Reset();
            portPool.Reset();
            scenario.citySet.ForEach(c =>
            {
                if (c.BuildingType.Id == 1 && c.Id > 0)
                {
                    City city = c;
                    UICityMapItem item = cityPool.Create();
                    item.name = city.Id.ToString();
                    RectTransform rectTransform = item.root;
                    float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                    item.icon.color = city.mBelongForce != null ? city.mBelongForce.Color : Color.white;
                    item.effect?.SetActive(false);
                }

                if (current != null)
                {
                    Troop troop = current;
                    UICityMapItem item = troopPool.Create();
                    item.name = troop.Id.ToString();
                    RectTransform rectTransform = item.root;
                    float x = troop.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                    float y = mapBounds.sizeDelta.y / 2 - troop.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                    rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                    item.icon.color = troop.mBelongForce != null ? troop.mBelongForce.Color : Color.white;
                    item.effect?.SetActive(true);
                }
            });
        }

        public void Show(ShortScenario scenario, Force current)
        {
            cityPool.Reset();
            portPool.Reset();
            foreach (ShortCity city in scenario.citySet)
            {

                if (city.BuildingType > 1) continue;
                if (city.Id == 0) continue;

                UICityMapItem item = cityPool.Create();
                item.name = city.Id.ToString();
                RectTransform rectTransform = item.root;
                float x = city.x * mapBounds.sizeDelta.x / scenario.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / scenario.Map.Height;
                rectTransform.anchoredPosition = new Vector2((int)(x + 0.5f), (int)(y + 0.5f));
                if (city.BelongForce > 0)
                {
                    ShortForce shortForce = scenario.forceSet[city.BelongForce];
                    Flag flag = scenario.CommonData.Flags[shortForce.Flag];
                    item.icon.color = flag.color;
                }
                else
                {
                    item.icon.color = Color.white;
                }

                item.effect?.SetActive(city.BelongForce == current.Id);
            }
        }

    }
}