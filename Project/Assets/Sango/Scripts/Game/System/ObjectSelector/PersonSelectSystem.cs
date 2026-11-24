using System;
using System.Collections.Generic;
using static Sango.Core.PersonSortFunction;

namespace Sango.Core.Player
{
    [GameSystem]
    public class PersonSelectSystem : ObjectSelectSystem, IObjectSelectSystem<Person>
    {
        /// <summary>
        /// 通用对象选择接口实现 - 供UIDataEdit按数据集类型统一调用
        /// </summary>
        void IObjectSelectSystem<Person>.Start(List<Person> candidates, List<Person> resultList, int limit, Action<List<Person>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName)
        {
            Start(candidates, resultList, limit, action, customSortTitles, cutomSortTitleName);
        }

        Action<List<Person>> finishAction;
        public List<ButtonData> selectButtons;


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

        public void SelectAll()
        {
            if(selected.Count < selectLimit)
            {
                for(int i = 0; i < Objects.Count; i++)
                {
                    SangoObject dest = Objects[i];
                    if(selected.Contains(dest))
                        continue;
                    // 勾选联动过滤: 一并选人也只能选彼此兼容的项
                    // (selected为空时过滤条件全部成立, 第一个人为基准, 后面的人按已有勾选判定)
                    if (displayFilter != null && !displayFilter(dest))
                        continue;

                    selected.Add(dest);
                    if (selected.Count >= selectLimit)
                        break;
                }
                MarkFilterDirty();
                WindowInterface?.Refresh();
            }
        }

        public void UnSelectAll()
        {
            selected.Clear();
            MarkFilterDirty();
            WindowInterface?.Refresh();
        }

        public void Start(List<Person> persons, List<Person> resultList, int limit, Action<List<Person>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(limit, persons.Count);
            Objects = new List<SangoObject>(persons);
            displayFilter = null;
            filterDirty = false;
            allObjects = new List<SangoObject>(Objects);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
            if (customSortTitles == null)
            {
                customSortTitles = new List<ObjectSortTitle>();
                PersonSortFunction.Instance.GetSortTitleGroup(PersonSortGroupType.Belong, customSortTitles);
            }
            // 【动态特技组】把 SortByFeatureList 标记展开成 N 列（N 随剧本参数变化），
            // 保证下面按 sortIndex 排序用的列表与界面显示的列是同一份（否则索引错位）。
            customSortItems = PersonSortFunction.ExpandFeatureGroups(customSortTitles);
            this.customSortTitleName = cutomSortTitleName;

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

        public void OnBaseSure(List<SangoObject> objects)
        {
            List<Person> people = new List<Person>();
            foreach (SangoObject obj in objects)
            {
                people.Add((Person)obj);
            }
            finishAction?.Invoke(people);
        }

        /// <summary>
        /// 搜索匹配：默认按武将名（基类口径）；关键词带 <c>t:</c> 前缀时改为"按特技名搜索"。
        ///
        /// 【为什么要单独一个前缀】特技搜索要遍历每名武将身上的特技表，与"按名字搜索"
        /// 是两套完全不同的判定。若混在同一个关键词里做全字段模糊匹配，每次输入都要把
        /// 候选集里所有人 × 所有字段扫一遍（几千人 × 数条特技）；用 <c>t:</c> 显式切换后，
        /// 普通搜索只比一个字符串，特技搜索只查该武将自己的特技列表（人均几条）。
        ///
        /// 【匹配范围】
        ///   · <see cref="Person.FeatureList"/> 全量（含未解锁 / 名额外的特技 —— 列表里
        ///     这些特技本来就以灰色显示着，搜不到反而不符合直觉）；
        ///   · <see cref="Person.extra_feature_list"/>（装备 / 官职附加，不占名额）；
        ///   · 混合组按子特技名展开匹配，见 <see cref="Feature.ContainsName(SangoObjectList{Feature}, string)"/>。
        ///
        /// 【只输入 "t:" 没有关键词】按"不过滤"处理：就地清空列表会让玩家以为搜索坏了。
        ///
        /// 【原版无对应实现】Open311 的人物选择是固定对话框列表
        ///（按 person->has_skill 等条件在 C++ 侧预先筛好，s11_sys_scen_ability.cpp:60-64），
        /// 没有"关键词 + 前缀语法"这一层；本条属自设计，仅为让玩家在几千人里定位持有
        /// 某特技的武将，不改变任何业务判定口径。
        /// </summary>
        /// <param name="obj">候选对象</param>
        /// <returns>命中返回 true</returns>
        public override bool MatchSearchKeyword(SangoObject obj)
        {
            if (!IsFeatureSearch)
                return base.MatchSearchKeyword(obj);

            if (string.IsNullOrEmpty(featureSearchText))
                return true;

            Person person = obj as Person;
            if (person == null)
                return false;

            return Feature.ContainsName(person.FeatureList, featureSearchText)
                || Feature.ContainsName(person.extra_feature_list, featureSearchText);
        }

        public override List<ObjectSortTitle> GetSortTitleGroup(int index)
        {
            if (index == 0) return customSortItems;

            List<ObjectSortTitle> sortTitles = new List<ObjectSortTitle>();
            PersonSortFunction.Instance.GetSortTitleGroup((PersonSortGroupType)index, sortTitles);
            return sortTitles;
        }

        public override string GetSortTitleGroupName(int index)
        {
            return PersonSortFunction.Instance.GetSortTitleGroupName((PersonSortGroupType)index);
        }
    }
}
