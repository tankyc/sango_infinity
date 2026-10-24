using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 能力选择系统：在通用对象选择窗口里选择一条"能力"（研究项目 / 培育项目）。
    ///
    /// 与其它 <c>XxxSelectSystem</c> 同构，只是候选集来自
    /// <c>Scenario.CommonData.Abilities</c>。能力条数少（当前 27 条）且没有天然排序字段，
    /// 因此默认不注册排序标题（<c>sortIndex = -1</c> 跳过排序）。
    /// </summary>
    [GameSystem]
    public class AbilitySelectSystem : ObjectSelectSystem, IObjectSelectSystem<Ability>
    {
        /// <summary>通用对象选择接口实现，供 UIDataEdit 按数据集类型统一调用</summary>
        void IObjectSelectSystem<Ability>.Start(List<Ability> candidates, List<Ability> resultList, int limit,
            Action<List<Ability>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        /// <summary>选择完成后的回调</summary>
        Action<List<Ability>> finishAction;

        /// <summary>默认排序标题组名称</summary>
        public string defualtTitleName = "能力";

        /// <summary>
        /// 启动能力选择系统。
        /// </summary>
        /// <param name="abilities">候选能力列表</param>
        /// <param name="resultList">已选中的能力列表（可为 null）</param>
        /// <param name="limit">选择数量上限</param>
        /// <param name="action">选择完成回调</param>
        /// <param name="customSortTitles">自定义排序标题（传 null 会退化为空列表 = 不排序）</param>
        /// <param name="cutomSortTitleName">排序标题组名称</param>
        /// <param name="sortIndex">初始排序字段索引；-1 表示不排序</param>
        public void Start(List<Ability> abilities, List<Ability> resultList, int limit,
            Action<List<Ability>> action, List<ObjectSortTitle> customSortTitles,
            string cutomSortTitleName, int sortIndex = -1)
        {
            if (abilities == null)
                abilities = new List<Ability>();

            donotFinishThisSystem = false;
            selectLimit = Math.Min(limit, abilities.Count);
            Objects = new List<SangoObject>(abilities);
            // 本系统是单例复用：二次打开必须清掉上一轮的搜索过滤与候选快照，
            // 否则输入过关键词后过滤仍基于旧候选集（对齐 PersonSelectSystem.Start 的口径）
            displayFilter = null;
            filterDirty = false;
            allObjects = new List<SangoObject>(Objects);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = resultList != null ? new List<SangoObject>(resultList) : new List<SangoObject>();
            selected.RemoveAll(x => x == null);

            customSortItems = customSortTitles ?? new List<ObjectSortTitle>();
            this.customSortTitleName = cutomSortTitleName != null ? cutomSortTitleName : defualtTitleName;

            ClickMode = limit == 1;
            buttonDatas = null;

            if (customSortItems.Count > sortIndex && sortIndex >= 0)
                Objects.Sort(customSortItems[sortIndex].Sort);

            GameSystemManager.Instance.Push(this);
        }

        /// <summary>确认选择回调，把通用对象列表转回能力列表</summary>
        /// <param name="objects">已选中的通用对象列表</param>
        public void OnBaseSure(List<SangoObject> objects)
        {
            List<Ability> result = new List<Ability>();
            if (objects != null)
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    if (objects[i] is Ability ability)
                        result.Add(ability);
                }
            }
            finishAction?.Invoke(result);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            return customSortItems;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return defualtTitleName;
        }
    }
}
