using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;
using Sango.Core;
namespace Sango.UI
{
    public class UICityDiplomacyAlliance : UGUIWindow
    {
        public Text windowTitle;

        public UITextField target;
        public UITextField gold;
        public Slider goldSlider;
        public UITextField relationship;
        public UITextField days;

        public UIPersonItem personItems;
        public UITextField action_value;
        public UIStatusItem statusItem;

        City TargetCity;

        /// <summary>
        /// 当前活跃的外交命令。
        ///
        /// 【为什么不是 GetSystem&lt;CityDiplomacyAlliance&gt;】这个窗口被四个命令共用：
        /// 结盟 / 劝降 / 俘虏交换 / 请求援军 —— 界面元素（目标势力、使者、关系、距离、确认）
        /// 完全通用，只有标题与金额条不同。具体是哪一个由各命令在 OnEnter 里登记到
        /// <see cref="CityDiplomacyBase.Current"/>。
        /// </summary>
        CityDiplomacyBase currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = CityDiplomacyBase.Current;
            if (currentSystem == null)
            {
                Sango.Log.Error("window_city_diplomacy_alliance 打开时没有活跃的外交命令");
                return;
            }

            windowTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            int count = currentSystem.personList.Count;
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.SendGift)}/{TargetCity.BelongCorps.ActionPoint}";
            sureButton.interactable = currentSystem.personList.Count > 0 && currentSystem.targetForces.Count > 0;
            Person actionPerson = count > 0 ? currentSystem.personList[0] : null;
            personItems.SetPerson(actionPerson);
            statusItem.SetPerson(actionPerson);
            Force targetForce = currentSystem.targetForces.Count > 0 ? currentSystem.targetForces[0] : null;
            if (targetForce != null)
            {
                target.text = targetForce.Name;
                relationship.text = Scenario.Cur.GetRelation(TargetCity.BelongForce, targetForce).ToString();
                days.text = $"{TargetCity.Distance(targetForce.CapitalCity)}0日";
            }
            else
            {
                target.text = "";
                relationship.text = "";
                days.text = "";
            }
            // 金额条按命令需要显示：劝降不花钱（NeedGold = false），直接隐藏滑块与数值
            bool needGold = currentSystem.NeedGold;
            if (goldSlider != null)
                goldSlider.gameObject.SetActive(needGold);

            if (needGold && TargetCity.gold > 0)
            {
                goldSlider.SetValueWithoutNotify((float)currentSystem.gold / TargetCity.gold);
                gold.text = $"{currentSystem.gold}/{TargetCity.gold}";
            }
            else
            {
                gold.text = "";
            }
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

        public void OnGoldCalculator()
        {

        }

        public void OnGoldSliderValueChanged(float p)
        {
            currentSystem.gold = (int)System.Math.Ceiling(TargetCity.gold * p);
            gold.text = $"{currentSystem.gold}/{TargetCity.gold}";
        }
    }
}