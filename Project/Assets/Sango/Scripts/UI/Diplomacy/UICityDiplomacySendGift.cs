using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UICityDiplomacySendGift : UGUIWindow
    {
        public Text windowTitle;

        public UITextField target;
        public UITextField relationship;
        public UITextField days;
        public UITextField gold;

        public UIPersonItem personItems;
        public UIStatusItem statusItem;
        public UITextField action_value;

        City TargetCity;
        CityDiplomacySendGift currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityDiplomacySendGift>();
            windowTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            int count = currentSystem.personList.Count;
            // 与本城口径一致：外交府可让亲善 AP 减半
            action_value.text = $"{TargetCity.GetJobCostAP(CityJobType.SendGift)}/{TargetCity.BelongCorps.ActionPoint}";
            sureButton.interactable = currentSystem.personList.Count > 0 && currentSystem.targetForces.Count > 0;
            Person actionPerson = count > 0 ? currentSystem.personList[0] : null;
            personItems.SetPerson(actionPerson);
            statusItem.SetPerson(actionPerson);
            Force targetForce = currentSystem.targetForces.Count > 0 ? currentSystem.targetForces[0] : null;
            if (targetForce != null)
            {
                target.text = targetForce.Name;
                // 统一为 0~100 友好度 + 原版五档（险恶/不和/中立/好意/亲密），
                // 不再输出 raw 关系值。
                relationship.text = DiplomacyRules.FormatLike(TargetCity.BelongForce.GetLike(targetForce));
                days.text = $"{TargetCity.Distance(targetForce.CapitalCity)}0日";
            }
            else
            {
                target.text = "";
                relationship.text = "";
                days.text = "";
            }
            // 与本城口径一致：外交府可让亲善资金减半
            gold.text = $"{TargetCity.GetJobCost(CityJobType.SendGift)}/{TargetCity.gold}";
        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(TargetCity.freePersons,
               currentSystem.personList, 1, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            UpdateContent();
        }
        public void OnSelectForce()
        {
            List<Force> forces = new List<Force>();
            Scenario.Cur.forceSet.ForEach(x =>
            {
                if (x.IsAlive && x.mGovernor != null && x != TargetCity.BelongForce && !ForceAI.IsInDiplomacyImmunity(TargetCity.BelongForce, x.Id))
                { forces.Add(x); }
            });

            GameSystem.GetSystem<ForceSelectSystem>().Start(forces,
               currentSystem.targetForces, 1, OnForceChange, currentSystem.customBelongForceTitleList, currentSystem.customTitleName);
        }

        public virtual void OnForceChange(List<Force> forceList)
        {
            currentSystem.targetForces = forceList;
            UpdateContent();
        }
    }
}