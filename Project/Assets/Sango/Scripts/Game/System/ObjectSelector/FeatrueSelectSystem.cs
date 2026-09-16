using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class FeatrueSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Feature>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Feature>.Start(List<Feature> candidates, List<Feature> resultList, int limit, Action<List<Feature>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        protected Action<List<Feature>> finishAction;
        public List<ButtonData> selectButtons;
        public string defualtTitleName = "特技";
        public List<ObjectSortTitle> defualtTitleList = FeatureSortFunction.DefaultSortList;
        public override void Init()
        {
            base.Init();
        }

        public void Start(List<Feature> troopList, List<Feature> resultList, int limit, Action<List<Feature>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
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
            List<Feature> people = new List<Feature>();
            foreach (SangoObject obj in objects)
            {
                people.Add((Feature)obj);
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
