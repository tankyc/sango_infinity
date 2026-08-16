using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core;
namespace Sango.UI
{
    public class UICityDiplomacyDiscardAlliance : UGUIWindow
    {
        public Text windowTitle;

        public UITextField target;
        public UITextField relationship;

        public UITextField action_value;

        City TargetCity;
        CityDiplomacyDiscardAlliance currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityDiplomacyDiscardAlliance>();
            windowTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            UpdateContent();
        }

        public void UpdateContent()
        {
            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.DiscardAlliance)}/{TargetCity.mCorps.ActionPoint}";
            sureButton.interactable = currentSystem.targetForces.Count > 0;
            Force targetForce = currentSystem.targetForces.Count > 0 ? currentSystem.targetForces[0] : null;
            if (targetForce != null)
            {
                target.text = targetForce.Name;
                relationship.text = Scenario.Cur.GetRelation(TargetCity.mForce, targetForce).ToString();
            }
            else
            {
                target.text = "";
                relationship.text = "";
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

        public void OnSelectForce()
        {
            List<Force> forces = new List<Force>();
            Scenario.Cur.forceSet.ForEach((x =>
            {
                if (x.IsAlive && x.mGovernor != null && x != TargetCity.mForce && x.IsAlliance(TargetCity.mForce))
                { 
                    forces.Add(x);
                }
            }));

            GameSystem.GetSystem<ForceSelectSystem>().Start(forces,
               currentSystem.targetForces, 1, OnForceChange, currentSystem.customForceTitleList, currentSystem.customTitleName);
        }

        public virtual void OnForceChange(List<Force> forceList)
        {
            currentSystem.targetForces = forceList;
            UpdateContent();
        }
    }
}