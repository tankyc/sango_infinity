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
            customMenuOrder = 1000;
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
                // 复用武力列的显示内容，仅改写排序规则以优先展示高武力武将。
                valueStrGetCall = sortTitle.valueStrGetCall,
                valueObjGet = sortTitle.valueObjGet,
                valueObjSet = sortTitle.valueObjSet,
                // 训练收益以武力为关联属性，数值高的武将优先显示和选择。
                valueSortFunc = (a, b) => b.Strength.CompareTo(a.Strength)
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
                // 没有兵就无从训练 —— 对齐原版 can_drill_command 的 `troops != 0`。
                // 这条此前只加在 JobTrainTroops 内部（执行时返回 0），
                // 而 IsValid / GetInvalidReason 都没有对应项，导致"无兵时按钮仍亮"。
                return TargetCity.troops > 0 &&
                    TargetCity.freePersons.Count > 0 &&
                    TargetCity.CheckJobCost(CityJobType.TrainTroops) &&
                    TargetCity.morale < TargetCity.MaxMorale &&
                    TargetCity.GetJobCounter((int)CityJobType.TrainTroops) == 0 &&
                    TargetCity.BelongCorps.ActionPoint >= TargetCity.GetJobCostAP(CityJobType.TrainTroops);
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.troops <= 0)
                return "城中无兵";
            if (TargetCity.freePersons.Count <= 0)
                return "无空闲武将";
            if (!TargetCity.CheckJobCost(CityJobType.TrainTroops))
                return "资金不足";
            if (TargetCity.morale >= TargetCity.MaxMorale)
                return "士气已满";
            if (TargetCity.GetJobCounter((int)CityJobType.TrainTroops) != 0)
                return "本回合已训练过";
            // 与 IsValid 同源：走本城口径（军事府可减半），否则会显示"行动力不足"却其实够
            if (TargetCity.BelongCorps.ActionPoint < TargetCity.GetJobCostAP(CityJobType.TrainTroops))
                return "行动力不足";
            return null;
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
