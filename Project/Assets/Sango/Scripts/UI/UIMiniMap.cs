using Sango.Core.Player;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    /// <summary>
    /// 游戏开始界面
    /// </summary>
    public class UIMiniMap : MonoBehaviour
    {
        abstract class MapNodeData
        {
            public GameObject @object;
            public Image image;
            protected RectTransform rectTransform;
            public MapNodeData(Image image, GameObject gameObject)
            {
                @object = gameObject;
                this.image = image;
                rectTransform = image.rectTransform;
            }
        }

        class MapCityNodeData : MapNodeData
        {
            public City city;
            public MapCityNodeData(City city, Image image, GameObject gameObject) : base(image, gameObject)
            {
                this.city = city;
                Color c = city.mBelongForce == null ? Color.white : city.mBelongForce.mFlag.color;
                image.color = c;
            }

            public MapCityNodeData UpdateCell(RectTransform mapBounds)
            {
                float x = city.x * mapBounds.sizeDelta.x / Scenario.Cur.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - city.y * mapBounds.sizeDelta.y / Scenario.Cur.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
                return this;
            }

            public void UpdateImage()
            {
                Color c = city.mBelongForce == null ? Color.white : city.mBelongForce.mFlag.color;
                image.color = c;
            }
        }
        class MapTroopNodeData : MapNodeData
        {
            public Troop troop;
            public MapTroopNodeData(Image image, GameObject gameObject) : base(image, gameObject)
            {
            }

            public void Init(Troop troop)
            {
                this.troop = troop;
                image.color = troop.mBelongForce.mFlag.color;
                image.enabled = true;
            }

            public MapTroopNodeData UpdateCell(Cell dest, RectTransform mapBounds)
            {
                float x = dest.x * mapBounds.sizeDelta.x / Scenario.Cur.Map.Width - mapBounds.sizeDelta.x / 2;
                float y = mapBounds.sizeDelta.y / 2 - dest.y * mapBounds.sizeDelta.y / Scenario.Cur.Map.Height;
                rectTransform.anchoredPosition = new Vector2(x, y);
                return this;
            }

            public void Clear()
            {
                image.enabled = false;
            }
        }


        public DrawLineComponent drawLine;
        public GameObject troopObj;
        public GameObject cityObj;
        public GameObject miniCityObj;
        public RectTransform mapBounds;
        public RectTransform mapValidBounds;

        public RectTransform[] cameraCorners;

        List<MapCityNodeData> mapCityNodes = new List<MapCityNodeData>();
        List<MapTroopNodeData> mapTroopNodes = new List<MapTroopNodeData>();
        Queue<MapTroopNodeData> mapTroopNodesPool = new Queue<MapTroopNodeData>();

        public void Start()
        {
            GameEvent.OnTroopEnterCell += OnTroopEnterCell;
            GameEvent.OnTroopCreated += OnTroopCreated;
            GameEvent.OnTroopClear += OnTroopClear;
            GameEvent.OnCityFall += OnCityFall;
            GameEvent.OnScenarioInit += OnScenarioInit;
            MapRender.Instance.onValueChanged = OnCameraValueChanged;
            InitCities();
        }

        Vector2 WorldPos2MiniMapPos(Vector3 worldPos)
        {
            float x = worldPos.z * mapBounds.sizeDelta.x / (Scenario.Cur.Map.Width * Scenario.Cur.Map.GridSize) - mapBounds.sizeDelta.x / 2;
            float y = mapBounds.sizeDelta.y / 2 - worldPos.x * mapBounds.sizeDelta.y / (Scenario.Cur.Map.Height * Scenario.Cur.Map.GridSize);
            return new Vector2(x, y); ;
        }

        Vector3 MiniMapPos2WorldPos(Vector2 minimapPos)
        {
            float z = (minimapPos.x + mapBounds.sizeDelta.x / 2) * (Scenario.Cur.Map.Width * Scenario.Cur.Map.GridSize) / mapBounds.sizeDelta.x;
            float x = (mapBounds.sizeDelta.y / 2 - minimapPos.y) * (Scenario.Cur.Map.Height * Scenario.Cur.Map.GridSize) / mapBounds.sizeDelta.y;
            return new Vector3(x, 0, z);
        }

        private Vector3[] corners = new Vector3[4];
        public void OnCameraValueChanged(MapCamera camera)
        {
            Vector3 rot = mapBounds.localRotation.eulerAngles;
            rot.z = camera.lookRotate.y + 90;
            mapBounds.localRotation = Quaternion.Euler(rot);
            if (CameraPlaneView.GetPlaneCorners(Vector3.up, Vector3.zero, camera.camera, MapRender.Instance.showLimitLength, out corners))
            {
                drawLine.line.dataPoints[0] = WorldPos2MiniMapPos(corners[0]);
                drawLine.line.dataPoints[1] = WorldPos2MiniMapPos(corners[1]);
                drawLine.line.dataPoints[2] = WorldPos2MiniMapPos(corners[2]);
                drawLine.line.dataPoints[3] = WorldPos2MiniMapPos(corners[3]);
                drawLine.line.dataPoints[4] = drawLine.line.dataPoints[0];
                drawLine.RefreshChart();
            }
        }

        public void OnDestroy()
        {
            ClearCities();
            mapCityNodes.Clear();
            mapTroopNodes.Clear();
            mapTroopNodesPool.Clear();
            GameEvent.OnTroopCreated -= OnTroopCreated;
            GameEvent.OnTroopClear -= OnTroopClear;
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;
            GameEvent.OnCityFall -= OnCityFall;
            GameEvent.OnScenarioInit -= OnScenarioInit;

            MapRender.Instance.onValueChanged = null;

        }

        void ClearCities()
        {
            for (int i = 0; i < mapTroopNodes.Count; i++)
                mapTroopNodesPool.Enqueue(mapTroopNodes[i]);
            mapTroopNodes.Clear();

            for (int i = 0; i < mapCityNodes.Count; i++)
            {
                MapCityNodeData nodeData = mapCityNodes[i];
                GameObject.Destroy(nodeData.@object);
            }
            mapCityNodes.Clear();
        }

        void OnScenarioInit(Scenario scenario)
        {
            ClearCities();
            InitCities();
        }

        void OnCityFall(City city, Force lastForce, Troop troop)
        {
            for (int i = 0; i < mapCityNodes.Count; i++)
            {
                MapCityNodeData data = mapCityNodes[i];
                if (data.city == city)
                {
                    data.UpdateImage();
                    return;
                }
            }
        }
        void OnTroopCreated(Troop troop, Scenario scenario)
        {
            MapTroopNodeData data;
            if (mapTroopNodesPool.Count > 0)
                data = mapTroopNodesPool.Dequeue();
            else
            {
                GameObject go = GameObject.Instantiate(troopObj, troopObj.transform.parent);
                go.SetActive(true);
                Image image = go.GetComponentInChildren<Image>(true);
                data = new MapTroopNodeData(image, go);
            }

            data.Init(troop);
            data.UpdateCell(troop.cell, mapBounds);
            mapTroopNodes.Add(data);
        }

        void OnTroopClear(Troop troop, Scenario scenario)
        {
            for (int i = 0; i < mapTroopNodes.Count; i++)
            {
                MapTroopNodeData data = mapTroopNodes[i];
                if (data.troop == troop)
                {
                    mapTroopNodes.RemoveAt(i);
                    data.Clear();
                    mapTroopNodesPool.Enqueue(data);
                    return;
                }
            }
        }

        void OnTroopEnterCell(Troop troop, Cell destCell, Cell lastCell)
        {
            for (int i = 0; i < mapTroopNodes.Count; i++)
            {
                MapTroopNodeData data = mapTroopNodes[i];
                if (data.troop == troop)
                {
                    data.UpdateCell(destCell, mapBounds);
                    return;
                }
            }
        }

        void InitCities()
        {
            Scenario.Cur.citySet.ForEach(city =>
            {
                if (city.IsCity())
                {
                    GameObject go = GameObject.Instantiate(cityObj, cityObj.transform.parent);
                    go.SetActive(true);
                    Image image = go.GetComponentInChildren<Image>(true);
                    mapCityNodes.Add(new MapCityNodeData(city, image, go).UpdateCell(mapBounds));
                }
                else
                {
                    GameObject go = GameObject.Instantiate(miniCityObj, miniCityObj.transform.parent);
                    go.SetActive(true);
                    Image image = go.GetComponentInChildren<Image>(true);
                    mapCityNodes.Add(new MapCityNodeData(city, image, go).UpdateCell(mapBounds));
                }
            });
        }

        public void OnClickMap()
        {
            if (GameSystemManager.Instance.CurrentCommand != null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(mapValidBounds, Input.mousePosition, Game.Instance.UICamera, out Vector2 localPoint))
            {
                Vector2 halfSize = mapValidBounds.sizeDelta / 2;
                if (localPoint.x > -halfSize.x && localPoint.x < halfSize.x && localPoint.y > -halfSize.y && localPoint.y < halfSize.y)
                {
                    Vector3 worldPos = MiniMapPos2WorldPos(localPoint);
                    MapRender.Instance.MoveCameraTo(worldPos);
                }
            }
        }

        public void OnSwitchPortShow()
        {
            for (int i = 0; i < mapCityNodes.Count; i++)
            {
                MapCityNodeData data = mapCityNodes[i];
                if (!data.city.IsCity())
                {
                    data.image.gameObject.SetActive(!data.image.gameObject.activeSelf);
                }
            }
        }

    }
}
