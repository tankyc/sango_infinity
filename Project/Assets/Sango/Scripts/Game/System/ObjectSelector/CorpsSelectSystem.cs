using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CorpsSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Corps>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Corps>.Start(List<Corps> candidates, List<Corps> resultList, int limit, Action<List<Corps>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

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

        /// <summary>
        /// 启动军团选择系统
        /// </summary>
        /// <param name="cities">候选军团列表</param>
        /// <param name="resultList">已选中的军团列表</param>
        /// <param name="limit">选择数量上限</param>
        /// <param name="action">选择完成回调</param>
        /// <param name="customSortTitles">自定义排序标题列表（为空时使用默认列表）</param>
        /// <param name="cutomSortTitleName">自定义排序标题组名称</param>
        /// <param name="browseOnly">true = 纯浏览（不勾选、无确定按钮，只能返回），见 <see cref="ObjectsDisplaySystem.BrowseMode"/></param>
        /// <param name="hideSortGroups">true = 只保留第 1 张排序组表卡（列固定、不切组），见 <see cref="ObjectsDisplaySystem.HideSortGroups"/></param>
        public void Start(List<Corps> cities, List<Corps> resultList, int limit, Action<List<Corps>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, bool browseOnly = false, bool hideSortGroups = false)
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
            // 浏览态 / 单表卡必须在 Push（→ OnEnter → 窗口 Init）之前定下来
            BrowseMode = browseOnly;
            HideSortGroups = hideSortGroups;
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
