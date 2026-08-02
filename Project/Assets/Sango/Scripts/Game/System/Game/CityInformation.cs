using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池治安系统逻辑
    /// </summary>
    [GameSystem]
    public class CityInformation : GameSystem
    {
        public City Target;
        public List<SangoObject> default_objects = new List<SangoObject>();
        public List<SangoObject> all_objects = new List<SangoObject>();
        protected string windowName = "window_information_city";

        public void Start(City target)
        {
            Target = target;
            all_objects = default_objects;
            Push();
        }

        public void Start(City target, List<SangoObject> city_list)
        {
            Target = target;
            all_objects = city_list;
            Push();
        }

        public override void Init()
        {
            Name = "都市情报";
            Clear();
            GameEvent.OnCityRightMouseButtonContextMenuShow += OnCityRightMouseButtonContextMenuShow;
            GameEvent.OnGameInformationContextMenuShow += OnGameSettingContextMenuShow;

#if UNITY_ANDROID || UNITY_IPHONE
            GameEvent.OnCityContextMenuShow += OnCityContextMenuShow;
#endif
            GameEvent.OnScenarioInit += OnScenarioInit;
        }
        protected virtual bool CityMenuCanShow()
        {
            return Target.IsCity();
        }

        public override void Clear()
        {
            GameEvent.OnGameInformationContextMenuShow -= OnGameSettingContextMenuShow;
#if UNITY_ANDROID || UNITY_IPHONE
            GameEvent.OnCityContextMenuShow -= OnCityContextMenuShow;
#endif
            GameEvent.OnCityRightMouseButtonContextMenuShow -= OnCityRightMouseButtonContextMenuShow;
            GameEvent.OnScenarioInit -= OnScenarioInit;
        }
#if UNITY_ANDROID || UNITY_IPHONE


        protected virtual void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            Target = city;
            if (CityMenuCanShow())
                menuData.Add("情报", 1000, city, OnClickMenuItem, true);
        }
#endif
        protected virtual void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
            Target = default_objects[0] as City;
            menuData.Add("全都市", 200, null, OnClickMenuItem);
            menuData.Add("全港关", 220, null, OnClickMenuItem_PortGat);
        }

        protected virtual void OnCityRightMouseButtonContextMenuShow(IContextMenuData menuData, City city)
        {
            Target = city;
            if (CityMenuCanShow())
                menuData.Add("详细情报", 0, null, OnClickMenuItem, true);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            all_objects = default_objects;
            Push();
        }

        protected virtual void OnClickMenuItem_PortGat(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();

            List<SangoObject> port_gate_list = new List<SangoObject>();
            Scenario.Cur.citySet.ForEach(city =>
            {
                if (!city.IsCity())
                    port_gate_list.Add(city);
            });

            all_objects = port_gate_list;
            Target  = all_objects[0] as City;
            Push();
        }

        public virtual void OnScenarioInit(Scenario scenario)
        {
            default_objects.Clear();
            scenario.citySet.ForEach(city =>
            {
                if (city.IsCity())
                    default_objects.Add(city);
            });
        }

        public override void OnEnter()
        {
            Window.Instance.Open(windowName, this);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            Window.Instance.SetVisible(windowName, true);
        }

        public override void OnExit()
        {
            Window.Instance.SetVisible(windowName, false);
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {
            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        GameSystemManager.Instance.Back();
                        break;
                    }
            }
        }
    }
}
