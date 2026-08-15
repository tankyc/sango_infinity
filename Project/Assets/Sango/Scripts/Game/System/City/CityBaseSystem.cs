using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    public class CityBaseSystem : GameSystem
    {
        public City TargetCity { get; set; }
        public List<Person> personList = new List<Person>();

        public string customTitleName;
        public List<ObjectSortTitle> customTitleList;
        public string customMenuName;
        public int customMenuOrder;
        public string windowName;

        public int wonderNumber;

        public override void Init()
        {
            GameEvent.OnCityContextMenuShow += OnCityContextMenuShow;
        }

        public override void Clear()
        {
            GameEvent.OnCityContextMenuShow -= OnCityContextMenuShow;
        }

        protected virtual bool MenuCanShow()
        {
            return TargetCity.IsCity();
        }

        public virtual bool IsValid => true;

        protected virtual void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            TargetCity = city;
            if (MenuCanShow() && city.mForce != null && city.mForce.IsPlayer && city.mForce == Scenario.Cur.CurRunForce)
            {
                menuData.Add(customMenuName, customMenuOrder, city, OnClickMenuItem, IsValid);
            }
        }
        protected virtual void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            TargetCity = contextMenuItem.CustomData as City;
            GameSystemManager.Instance.Push(this);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            personList.Clear();
            RecommandPersonList();
            UpdateJobValue();
            Window.Instance.Open(windowName);
        }
        public virtual void RecommandPersonList()
        {

        }
        public virtual void UpdateJobValue()
        {
            wonderNumber = CalculateWonderNumber();
        }

        public virtual int CalculateWonderNumber()
        {
            return 0;
        }

        public override void OnDestroy()
        {
            Window.Instance.Close(windowName);
        }

        public virtual void DoJob()
        {
            Done();
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
