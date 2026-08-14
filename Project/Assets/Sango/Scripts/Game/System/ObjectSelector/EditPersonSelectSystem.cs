
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class EditPersonSelectSystem : ObjectSelectSystem
    {
        Action<List<PersonLib>> finishAction;
        Action<List<Person>> finishPersonAction;
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

        public void UnSelectAll()
        {
            selected.Clear();
            WindowInterface?.Refresh();
        }

        public void Start(List<PersonLib> persons, List<PersonLib> resultList, int limit, Action<List<PersonLib>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(Math.Abs(limit), persons.Count);
            Objects = new List<SangoObject>(persons);
            finishAction = action;
            finishPersonAction = null;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
            if (customSortTitles == null)
            {
                customSortTitles = new List<ObjectSortTitle>();
            }
            customSortItems = customSortTitles;
            this.customSortTitleName = cutomSortTitleName;

            ClickMode = limit == -1;
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

        public void Start(List<Person> persons, List<Person> resultList, int limit, Action<List<Person>> action, List<ObjectSortTitle> customSortTitles, string cutomSortTitleName, int sortIndex = 1)
        {
            donotFinishThisSystem = false;
            selectLimit = Math.Min(Math.Abs(limit), persons.Count);
            Objects = new List<SangoObject>(persons);
            finishAction = null;
            finishPersonAction = action;
            sureAction = OnBaseSure;
            selected = new List<SangoObject>(resultList);
            selected.RemoveAll(x => x == null);
            if (customSortTitles == null)
            {
                customSortTitles = new List<ObjectSortTitle>();
            }
            customSortItems = customSortTitles;
            this.customSortTitleName = cutomSortTitleName;

            ClickMode = limit == -1;
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
            if (finishPersonAction != null)
            {
                List<Person> people = new List<Person>();
                foreach (SangoObject obj in objects)
                {
                    people.Add((Person)obj);
                }
                finishPersonAction?.Invoke(people);
            }
            else
            {
                List<PersonLib> people = new List<PersonLib>();
                foreach (SangoObject obj in objects)
                {
                    people.Add((PersonLib)obj);
                }
                finishAction?.Invoke(people);
            }

       
        }
    }
}
