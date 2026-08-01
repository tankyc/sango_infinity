using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CorpsSelectSystem : ObjectSelectSystem
    {
        protected Action<List<Corps>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "势力";
        public List<ObjectSortTitle> defualtTitleList = new List<ObjectSortTitle>()
            {
                CorpsSortFunction.SortByName,
                CorpsSortFunction.SortByLeader,
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

        public void Start(List<Corps> cities, List<Corps> resultList, int limit, Action<List<Corps>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
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
            List<Corps> people = new List<Corps>();
            foreach (SangoObject obj in objects)
            {
                people.Add((Corps)obj);
            }
            finishAction?.Invoke(people);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;

            List<ObjectSortTitle> sortTitles = new List<ObjectSortTitle>();
            CorpsSortFunction.Instance.GetSortTitleGroup((CorpsSortGroupType)index, sortTitles);
            return sortTitles;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return CorpsSortFunction.Instance.GetSortTitleGroupName((CorpsSortGroupType)index);
        }
    }
}
