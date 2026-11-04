using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
using System;

namespace Sango.UI
{
    public class UICitySearching : UGUIWindow
    {
        public Text windiwTitle;
        public UITextField personCountLabel;

        public UIPersonItem personItems;
        public UITextField action_value;
        public UIStatusItem statusItem;

        City TargetCity;
        CitySearching currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {   
            currentSystem = GameSystem.GetSystem<CitySearching>();
            windiwTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            personItems.SetPerson(null);
            if (currentSystem.personList.Count > 0 )
            {
                string content = $"最适合担任此任务的人，\n除{currentSystem.personList[0].ColorName}之外别无其他人选。";
                if (currentSystem.personList[0] == TargetCity.BelongForce.mCounsellor)
                {
                    content = $"我对此任务很有信心，\n请务必交给我吧。";
                }

                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, content, () => { UpdateContent(); }, TargetCity.BelongForce.mCounsellor);
            }
            else
            {
                GameDialog.Instance.Open(GameDialog.DialogStyle.ClickPersonSay, $"如今并无适合担任此任务的人选。", () => { UpdateContent(); }, TargetCity.BelongForce.mCounsellor);
            }
        }

        public void UpdateContent()
        {
            int count = currentSystem.personList.Count;
            // 走本城口径：人才府会让探索 AP 减半，显示必须与门禁(CitySearching)、结算(City.JobSearching)同源，
            // 否则会出现"面板写 20、点下去只扣 10"
            action_value.text = $"{count * TargetCity.GetJobCostAP(CityJobType.Searching)}/{TargetCity.BelongCorps.ActionPoint}";
            sureButton.interactable = count > 0;
            Person actionPerson = count > 0 ? currentSystem.personList[0] : null;
            personItems.SetPerson(actionPerson);
            statusItem.SetPerson(actionPerson);
            personCountLabel.text = $"{currentSystem.personList.Count}人";
        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        int GetMaxCount()
        {
            int pCount = TargetCity.freePersons.Count;
            // 可选人数上限也要按减半后的实际扣费算，否则一次选多人会超出军团行动力
            int apCost = TargetCity.GetJobCostAP(CityJobType.Searching);
            if (apCost > 0)
            {
                return Math.Min(pCount, TargetCity.BelongCorps.ActionPoint / apCost);
            }
            else
                return pCount;
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(TargetCity.freePersons,
               currentSystem.personList, GetMaxCount(), OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            UpdateContent();
        }

    }
}
