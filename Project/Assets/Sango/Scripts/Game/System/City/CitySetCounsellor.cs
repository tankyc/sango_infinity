using Sango.UI;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CitySetCounsellor : CityBaseSystem
    {
        public Force TargetForce;
        public List<Person> targetList = new List<Person>();
        public Person counsellor;

        public CitySetCounsellor()
        {
            customTitleName = "军师";
            customMenuName = "君主/军师";
            customMenuOrder = 4100;
            windowName = "window_city_set_counsellor";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByIsCounsellor,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByOfficial,
                PersonSortFunction.SortByMerit,
            };

        }
        protected override bool MenuCanShow()
        {
            return TargetCity.IsCityBase();
        }
        public override bool IsValid
        {
            get
            {
                // 【修复】不能用 Force.PersonCount：它是"势力回合开始时重算"的运行期缓存
                // （`Force.PersonCount` 是自动属性，OptIn 序列化不存档），读档后为 0，
                // 会让军师菜单被置灰；要等该势力下一次 OnForceTurnStart 重算才恢复 ——
                // 正是玩家反馈的"读档后一开始不让任命军师，过几回合又正常"。
                // 这里改为实时统计本势力存活武将，与 OnEnter 里 targetList 的遍历口径一致。
                Force force = TargetCity != null ? TargetCity.BelongForce : null;
                Scenario scenario = Scenario.Cur;
                if (force == null || scenario == null || scenario.personSet == null)
                    return false;

                int count = 0;
                for (int i = 0; i < scenario.personSet.Count; i++)
                {
                    Person p = scenario.personSet[i];
                    if (p != null && p.IsAlive && p.BelongForce == force)
                        count++;
                }
                return count > 1;
            }
        }

        public override void OnEnter()
        {
            personList.Clear();
            targetList.Clear();
            TargetForce = TargetCity.BelongForce;
            counsellor = TargetForce.mCounsellor;
            Scenario.Cur.personSet.ForEach(x =>
            {
                if (x.BelongForce == TargetForce && x != TargetForce.mGovernor && x != TargetForce.mCounsellor && !x.IsPrisoner)
                {
                    targetList.Add(x);
                }
            });
            targetList.Sort((a, b) => -PersonSortFunction.SortByIntelligence.valueSortFunc.Invoke(a, b));
            Window.Instance.Open(windowName);
        }
        public override void OnDestroy()
        {
            GameEvent.DialogClose?.Invoke();
            Window.Instance.Close(windowName);
        }

        public void ClearCounsellor()
        {
            counsellor = null;
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
            {
                TargetForce.ChangeCounsellor(null);
                Done();
                return;
            }

            GameMedia.Instance.PlayDoAcitonSfx();
            TargetForce.ChangeCounsellor(personList[0]);
            GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"交给我吧", () =>
            {
                // TODO:展示武将
                // 暂时直接招募
                Done();

            }, personList[0]);
        }
    }
}
