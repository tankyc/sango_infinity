using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class TroopSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Troop>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Troop>.Start(List<Troop> candidates, List<Troop> resultList, int limit, Action<List<Troop>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        protected Action<List<Troop>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "部队";
        public List<ObjectSortTitle> defualtTitleList = new List<ObjectSortTitle>()
            {
                TroopSortFunction.SortByName,
                TroopSortFunction.SortByBelongForce,
                TroopSortFunction.SortByBelongCorps,
                TroopSortFunction.SortByBelongCity,
                // 【任务列】选择器此前只罗列属性，看不到"这支部队正在干什么"，
                // 玩家从城市 / 势力的部队按钮进来时无法区分待命部队与出征部队。
                // 详情面板（UITroopInformation）早已用同一列，这里补齐，两处口径同源。
                TroopSortFunction.SortByMissionType,
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
