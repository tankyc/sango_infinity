using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 道具类型选择系统，用于在通用对象选择窗口中选择道具类型
    /// </summary>
    [GameSystem]
    public class ItemTypeSelectSystem : ObjectSelectSystem, IObjectSelectSystem<ItemType>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<ItemType>.Start(List<ItemType> candidates, List<ItemType> resultList, int limit, Action<List<ItemType>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        /// <summary>
        /// 选择完成后的回调
        /// </summary>
        Action<List<ItemType>> finishAction;

        /// <summary>
        /// 选择器按钮列表（一并选择/一并解除）
        /// </summary>
        public List<ButtonData> selectButtons;

        /// <summary>
        /// 默认排序标题组名称
        /// </summary>
        public string defualtTitleName = "道具";

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public List<ObjectSortTitle> defualtTitleList = ItemTypeSortFunction.DefaultSortList;

        public override void Init()
        {
            base.Init();
            selectButtons = new List<ButtonData>()
            {
                new ButtonData()
                {
                    title = "一并",
                    action = SelectAll
                }
                ,
                new ButtonData()
                {
                    title = "一并解除",
                    action = UnSelectAll
                }
            };
        }

        /// <summary>
        /// 一并选择（达到选择上限为止）
        /// </summary>
        public void SelectAll()
        {
            if (selected.Count < selectLimit)
            {
                for (int i = 0; i < Objects.Count; i++)
                {
                    SangoObject dest = Objects[i];
                    if (!selected.Contains(dest))
                    {
                        selected.Add(dest);
                        if (selected.Count >= selectLimit)
                            break;
                    }
                }
                WindowInterface?.Refresh();
            }
        }

        /// <summary>
        /// 一并解除选择
        /// </summary>
        public void UnSelectAll()
        {
            selected.Clear();
            WindowInterface?.Refresh();
        }

        /// <summary>
        /// 启动道具类型选择系统
        /// </summary>
        /// <param name="itemTypes">候选道具类型列表</param>
        /// <param name="resultList">已选中的道具类型列表</param>
        /// <param name="limit">选择数量上限</param>
        /// <param name="action">选择完成回调</param>
        /// <param name="customSortTitles">自定义排序标题列表（为空时使用默认列表）</param>
        /// <param name="cutomSortTitleName">自定义排序标题组名称</param>
        /// <param name="sortIndex">初始排序字段索引；传 -1 表示不排序（保持候选集原顺序）</param>
        /// <param name="browseOnly">true = 纯浏览（不勾选、无确定按钮，只能返回），见 <see cref="ObjectsDisplaySystem.BrowseMode"/></param>
        /// <param name="hideSortGroups">true = 只保留第 1 张排序组表卡（列固定、不切组），见 <see cref="ObjectsDisplaySystem.HideSortGroups"/></param>
        public void Start(List<ItemType> itemTypes, List<ItemType> resultList, int limit, Action<List<ItemType>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1, bool browseOnly = false, bool hideSortGroups = false)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(limit, itemTypes.Count);
            Objects = new List<SangoObject>(itemTypes);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
            // 浏览态 / 单表卡必须在 Push（→ OnEnter → 窗口 Init）之前定下来，
            // 界面据此隐藏勾选列与确定按钮、并把多余的排序组表卡关掉
            BrowseMode = browseOnly;
            HideSortGroups = hideSortGroups;
            if (customSortTitles == null)
            {
                customSortTitles = defualtTitleList;
            }
            customSortItems = customSortTitles;
            this.customSortTitleName = cutomSortTitleName != null ? cutomSortTitleName : defualtTitleName;

            ClickMode = limit == 1;
            if (ClickMode)
            {
                buttonDatas = null;
            }
            else
            {
                buttonDatas = selectButtons;
            }
            if (customSortTitles.Count > sortIndex && sortIndex >= 0)
                Objects.Sort(customSortItems[sortIndex].Sort);
            GameSystemManager.Instance.Push(this);
        }

        /// <summary>
        /// 确认选择回调，将通用对象列表转换为道具类型列表
        /// </summary>
        /// <param name="objects">已选中的通用对象列表</param>
        public void OnBaseSure(List<SangoObject> objects)
        {
            List<ItemType> result = new List<ItemType>();
            foreach (SangoObject obj in objects)
            {
                result.Add((ItemType)obj);
            }
            finishAction?.Invoke(result);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;
            return defualtTitleList;
        }

        /// <summary>
        /// 排序组表卡的名字：只有第 2 张卡（index = 1，「道具默认列」）有名字，其余卡位返回 null 让界面隐藏。
        ///
        /// 【为什么必须这样】原实现对**任何** index 都返回同一个 <see cref="defualtTitleName"/>，
        /// 而窗口的表卡数量来自 prefab 上的 toggleGroup 数组长度（8 个）——
        /// 于是同一排出现了 8 个一模一样的「道具」标签。
        /// 表卡数量由"有几个真实分组"决定，不是由 prefab 槽位数决定。
        /// </summary>
        /// <param name="index">表卡下标</param>
        /// <returns>表卡名；null 表示该卡位不使用</returns>
        public override string GetSortTitleGroupName(int index)
        {
            if (index == 1) return defualtTitleName;
            return null;
        }
    }
}
