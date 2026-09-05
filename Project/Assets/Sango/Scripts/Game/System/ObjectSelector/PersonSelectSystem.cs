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
                    if(!selected.Contains(dest))
                    {
                        selected.Add(dest);
                        if (selected.Count >= selectLimit)
                            break;
                    }
                }
                WindowInterface?.Refresh();
            }
        }

        public void UnSelectAll()
        {
            selected.Clear();
            WindowInterface?.Refresh();
        }

        public void Start(List<Person> persons, List<Person> resultList, int limit, Action<List<Person>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(limit, persons.Count);
            Objects = new List<SangoObject>(persons);
            finishAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
            if (customSortTitles == null)
            {
                customSortTitles = new List<ObjectSortTitle>();
                PersonSortFunction.Instance.GetSortTitleGroup(PersonSortGroupType.Belong, customSortTitles);
            }
            customSortItems = customSortTitles;
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
