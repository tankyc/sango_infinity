using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem(autoInit = false)]
    public class CityTrainTroops : CityBaseSystem
    {
        public CityTrainTroops()
        {
            customTitleName = "训练";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByStrength,
            };
            customMenuName = "军事/训练";
            customMenuOrder = 103;
            windowName = "window_city_train_troops";
        }

        /// <summary>进入训练时将武力列设置为默认倒序排序列。</summary>
        public override void OnEnter()
        {
            customTitleList[1] = CreateTrainPersonSortTitle();
            base.OnEnter();
        }

        /// <summary>复制武力展示列并改写比较器，避免改变全局共享的武力排序规则。</summary>
        private static PersonSortFunction.SortTitle CreateTrainPersonSortTitle()
        {
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.SortByStrength;
            sortTitle = new PersonSortFunction.SortTitle
            {
                name = sortTitle.name,
                width = sortTitle.width,
                valueGetCall = sortTitle.valueGetCall,
                valueObjGet = sortTitle.valueObjGet,
                valueObjSet = sortTitle.valueObjSet,
                // 训练收益以武力为关联属性，数值高的武将优先显示和选择。
                personSortFunc = (a, b) => b.Strength.CompareTo(a.Strength)
            };
            return sortTitle;
        }

        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                return TargetCity.freePersons.Count > 0 &&
                    TargetCity.CheckJobCost(CityJobType.TrainTroops) &&
                    TargetCity.morale < TargetCity.MaxMorale &&
                    TargetCity.GetJobCounter((int)CityJobType.TrainTroops) == 0 &&
                    TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TrainTroops);
            }
        }

        public override int CalculateWonderNumber()
        {
            return TargetCity.JobTrainTroops(personList.ToArray(), true);
        }

        /// <summary>按武力倒序自动推荐最多三名训练武将。</summary>
        public override void RecommandPersonList()
        {
            personList.Clear();
            List<Person> people = new List<Person>(TargetCity.freePersons);
            // 与选择器的默认武力倒序保持一致。
            people.Sort((a, b) => b.Strength.CompareTo(a.Strength));
            if (people.Count > 3) people.RemoveRange(3, people.Count - 3);
            if (people != null)
            {
                for (int i = 0; i < people.Count; ++i)
                {
                    Person p = people[i];
                    if (p != null)
                        personList.Add(p);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count > 0)
            {
                TargetCity.JobTrainTroops(personList.ToArray());
                GameMedia.Instance.PlayDoAcitonSfx();
                Done();
            }
        }
    }
}
