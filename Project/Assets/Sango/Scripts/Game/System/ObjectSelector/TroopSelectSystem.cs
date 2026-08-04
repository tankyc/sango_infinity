using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopSelectSystem : ObjectSelectSystem
    {
        protected Action<List<Troop>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "部队";
        public List<ObjectSortTitle> defualtTitleList = new List<ObjectSortTitle>()
            {
                TroopSortFunction.SortByName,
                TroopSortFunction.SortByBelongForce,
                TroopSortFunction.SortByBelongCorps,
                TroopSortFunction.SortByBelongCity,
                TroopSortFunction.SortByTroops,
                TroopSortFunction.SortByTroopType,
                TroopSortFunction.SortByAbility,
                TroopSortFunction.SortByWaterAbility,
                TroopSortFunction.SortByGold,
                TroopSortFunction.SortByFood,
                TroopSortFunction.SortByAttack,
                TroopSortFunction.SortByDefence,
            };
        public override void Init()
        {
            base.Init();
        }

        public void Start(List<Troop> troopList, List<Troop> resultList, int limit, Action<List<Troop>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            donotFinishThisSystem = false;
            selectLimit = limit;
            Objects = new List<SangoObject>(troopList);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            customSortItems = customSortTitles != null ? customSortTitles : defualtTitleList;
            this.customSortTitleName = cutomSortTitleName != null ? cutomSortTitleName : defualtTitleName; ;
            ClickMode = limit == 1;
            buttonDatas = selectButtons;
            Push();
        }

        public void OnBaseSure(List<SangoObject> objects)
        {
            List<Troop> people = new List<Troop>();
            foreach (SangoObject obj in objects)
            {
                people.Add((Troop)obj);
            }
            finishAction?.Invoke(people);
        }

        //public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        //{
        //    if (index == 0) return customSortItems;

        //    List<ObjectSortTitle> sortTitles = new List<ObjectSortTitle>();
        //    CitySortFunction.Instance.GetSortTitleGroup((CitySortGroupType)index, sortTitles);
        //    return sortTitles;
        //}

        //public override string GetSortTitleGroupName(int index)
        //{
        //    return CitySortFunction.Instance.GetSortTitleGroupName((CitySortGroupType)index);
        //}
    }
}
