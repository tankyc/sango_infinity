using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityDiplomacyTruce : UGUIWindow
    {
        public Text windowTitle;
        public UITextField personCountLabel;

        public UIPersonItem personItems;
        public UITextField action_value;
        public UIStatusItem statusItem;

        City TargetCity;
        CityDiplomacyTruce currentSystem;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityDiplomacyTruce>();
            windowTitle.text = currentSystem.customTitleName;
            TargetCity = currentSystem.TargetCity;
            personItems.SetPerson(null);
            if (currentSystem.personList.Count > 0 )
            {
                string content = $"最适合担任此任务的人，\n除{currentSystem.personList[0].ColorName}之外别无其他人选。";
                if (currentSystem.personList[0] == TargetCity.mForce.mCounsellor)
                {
                    content = $"我对此任务很有信心，\n请务必交给我吧。";
                }

                GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, content, () => { GameDialog.Close(); UpdateContent(); });
                Person person = TargetCity.mForce.mCounsellor;
                dialog.SetPerson(person);
            }
            else
            {
                GameDialog.IDialog dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, $"如今并无适合担任此任务的人选。", () => { GameDialog.Close(); });
            }
        }

        public void UpdateContent()
        {
            int count = currentSystem.personList.Count;
            action_value.text = $"{count * 1}/{TargetCity.mCorps.ActionPoint}";
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

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(TargetCity.freePersons,
               currentSystem.personList, TargetCity.mCorps.ActionPoint, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = personList;
            UpdateContent();
        }

    }
}