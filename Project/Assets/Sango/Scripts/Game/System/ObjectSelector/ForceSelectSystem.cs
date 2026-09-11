using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class ForceSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Force>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Force>.Start(List<Force> candidates, List<Force> resultList, int limit, Action<List<Force>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        protected Action<List<Force>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "势力";
        public List<ObjectSortTitle> defualtTitleList = new List<ObjectSortTitle>()
            {
                ForceSortFunction.SortByName,
                ForceSortFunction.SortByLeader,
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

        public void Start(List<Force> cities, List<Force> resultList, int limit, Action<List<Force>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            donotFinishThisSystem = false;
            selectLimit = limit;
            Objects = new List<SangoObject>(cities);
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
            List<Force> people = new List<Force>();
            foreach (SangoObject obj in objects)
            {
                people.Add((Force)obj);
            }
            finishAction?.Invoke(people);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;

            List<ObjectSortTitle> sortTitles = new List<ObjectSortTitle>();
            ForceSortFunction.Instance.GetSortTitleGroup((ForceSortGroupType)index, sortTitles);
            return sortTitles;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return ForceSortFunction.Instance.GetSortTitleGroupName((ForceSortGroupType)index);
        }
    }
}
