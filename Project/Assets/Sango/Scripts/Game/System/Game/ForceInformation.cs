using Sango.Core.Player;
using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力情报
    /// </summary>
    [GameSystem]
    public class ForceInformation : GameSystem
    {
        public Force Target;
        public List<SangoObject> default_objects = new List<SangoObject>();
        public List<SangoObject> all_objects = new List<SangoObject>();
        protected string windowName = "window_information_force";

        public void Start(Force target)
        {
            Target = target;
            all_objects = default_objects;
            Push();
        }

        public void Start(Force target, List<SangoObject> city_list)
        {
            Target = target;
            all_objects = city_list;
            Push();
        }

        public override void Init()
        {
            Name = "势力情报";
            GameEvent.OnCityRightMouseButtonContextMenuShow += OnCityRightMouseButtonContextMenuShow;
            GameEvent.OnGameInformationContextMenuShow += OnGameSettingContextMenuShow;
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
            menuData.Add("全势力", 400, null, OnClickMenuItem);
        }

        protected virtual void OnCityRightMouseButtonContextMenuShow(IContextMenuData menuData, City city)
        {
            Target = city.mBelongForce;
            if (CityMenuCanShow())
                menuData.Add(Name, 20, city.mBelongForce, OnClickMenuItem, city.mBelongForce != null);
        }

        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            List<SangoObject> obj_list = new List<SangoObject>();
            Scenario.Cur.forceSet.ForEach(city =>
            {
                if (city.IsAlive)
                    obj_list.Add(city);
            });

            all_objects = obj_list;
            Target = contextMenuItem.CustomData as Force;
            if (Target == null)
                Target = all_objects[0] as Force;
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
                        GameSystemManager.Instance.Back();
                        break;
                    }
            }
        }
    }
}
