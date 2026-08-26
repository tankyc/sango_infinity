using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 城池治安系统逻辑
    /// </summary>
    [GameSystem]
    public class PersonInformation : GameSystem
    {
        public Person Target;
        public List<SangoObject> default_objects = new List<SangoObject>();
        public List<SangoObject> all_objects = new List<SangoObject>();
        protected string windowName = "window_information_person";

        public void Start(Person target)
        {
            Target = target;
            all_objects = default_objects;
            Push();
        }

        public void Start(Person target, List<SangoObject> city_list)
        {
            Target = target;
            all_objects = city_list;
            Push();
        }

        public override void Init()
        {
            Name = "武将情报";
            GameEvent.OnGameInformationContextMenuShow += OnGameSettingContextMenuShow;
            GameEvent.OnCityRightMouseButtonContextMenuShow += OnCityRightMouseButtonContextMenuShow;
        }
        protected virtual bool CityMenuCanShow()
        {
            return true;
        }

        public override void Clear()
        {
            GameEvent.OnGameInformationContextMenuShow -= OnGameSettingContextMenuShow;
            GameEvent.OnCityRightMouseButtonContextMenuShow -= OnCityRightMouseButtonContextMenuShow;
        }

        protected virtual void OnGameSettingContextMenuShow(IContextMenuData menuData)
        {
            default_objects.Clear();
            Scenario.Cur.personSet.ForEach(person =>
            {
                if(person.IsValid) 
                    default_objects.Add(person);
            });
            Target = default_objects[0] as Person;
            menuData.Add("全武将", 300, null, OnClickMenuItem);
        }

        protected virtual void OnCityRightMouseButtonContextMenuShow(IContextMenuData menuData, City city)
        {
            //Target = city;
            default_objects.Clear();
            city.allPersons.ForEach(person =>
            {
                default_objects.Add(person);
            });
            default_objects.AddRange(city.wildPersons);
            if(default_objects.Count > 0) 
                Target = default_objects[0] as Person;
            if (CityMenuCanShow())
                menuData.Add("所属武将", 20, null, OnClickMenuItem, default_objects.Count > 0);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            ContextMenu.CloseAll();
            all_objects = default_objects;
            Push();
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
                        Back();
                        break;
                    }
            }
        }
    }
}
