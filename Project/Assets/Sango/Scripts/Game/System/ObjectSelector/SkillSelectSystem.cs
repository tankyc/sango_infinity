using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 技能选择系统，用于在通用对象选择窗口中选择技能
    /// </summary>
    [GameSystem]
    public class SkillSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Skill>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Skill>.Start(List<Skill> candidates, List<Skill> resultList, int limit, Action<List<Skill>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        /// <summary>
        /// 选择完成后的回调
        /// </summary>
        Action<List<Skill>> finishAction;

        /// <summary>
        /// 选择器按钮列表（一并选择/一并解除）
        /// </summary>
        public List<ButtonData> selectButtons;

        /// <summary>
        /// 默认排序标题组名称
        /// </summary>
        public string defualtTitleName = "技能";

        /// <summary>
        /// 默认排序标题列表
        /// </summary>
        public List<ObjectSortTitle> defualtTitleList = SkillSortFunction.DefaultSortList;

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
        /// 启动技能选择系统
        /// </summary>
        /// <param name="skills">候选技能列表</param>
        /// <param name="resultList">已选中的技能列表</param>
        /// <param name="limit">选择数量上限</param>
        /// <param name="action">选择完成回调</param>
        /// <param name="customSortTitles">自定义排序标题列表（为空时使用默认列表）</param>
        /// <param name="cutomSortTitleName">自定义排序标题组名称</param>
        /// <param name="sortIndex">初始排序字段索引</param>
        public void Start(List<Skill> skills, List<Skill> resultList, int limit, Action<List<Skill>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(limit, skills.Count);
            Objects = new List<SangoObject>(skills);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
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
        /// 确认选择回调，将通用对象列表转换为技能列表
        /// </summary>
        /// <param name="objects">已选中的通用对象列表</param>
        public void OnBaseSure(List<SangoObject> objects)
        {
            List<Skill> result = new List<Skill>();
            foreach (SangoObject obj in objects)
            {
                result.Add((Skill)obj);
            }
            finishAction?.Invoke(result);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;
            return defualtTitleList;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return defualtTitleName;
        }
    }
}
