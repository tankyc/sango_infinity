using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CitySelectSystem : ObjectSelectSystem
    {
        protected Action<List<City>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "城市";
        public List<ObjectSortTitle> defualtTitleList = new List<ObjectSortTitle>()
            {
                CitySortFunction.SortByName,
                CitySortFunction.SortByPersonCount,
                CitySortFunction.SortByBelongCity,
                CitySortFunction.SortByTroops,
                CitySortFunction.SortByGold,
                CitySortFunction.SortByFood,
                CitySortFunction.SortByLevel,
            };
        public override void Init()
        {
            base.Init();
            selectButtons = new List<ButtonData>()
            {
                new ButtonData()
                {
                    title = "地图",
                    action = SelectOnMap,
                    style = 1
                }
            };
        }

        void SelectOnMap()
        {

        }

        public void Start(List<City> cities, List<City> resultList, int limit, Action<List<City>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            donotFinishThisSystem = false;
            selectLimit = limit;
            Objects = new List<SangoObject>(cities);
            finishAction = action;
            sureAction = OnBaseSure;
            if (resultList == null)
                selected = new List<SangoObject>();
            else
                selected = new List<SangoObject>(resultList);
            customSortItems = customSortTitles != null ? customSortTitles : defualtTitleList;
            this.customSortTitleName = cutomSortTitleName != null ? cutomSortTitleName : defualtTitleName; ;
            ClickMode = limit == 1;
            buttonDatas = selectButtons;
            Push();
        }

        public void OnBaseSure(List<SangoObject> objects)
        {
            List<City> people = new List<City>();
            foreach (SangoObject obj in objects)
            {
                people.Add((City)obj);
            }
            finishAction?.Invoke(people);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;

            List<ObjectSortTitle> sortTitles = new List<ObjectSortTitle>();
            CitySortFunction.Instance.GetSortTitleGroup((CitySortGroupType)index, sortTitles);
            return sortTitles;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return CitySortFunction.Instance.GetSortTitleGroupName((CitySortGroupType)index);
        }
    }
}
