using Sango.Core.Player;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UIPersonRecruitInfo : UGUIWindow
    {
        public Text titleText;
        public UIPersonItem personItem;
        public UIStatusItem statusItem;
        public UITextField ageText;
        public UITextField sexText;
        public UITextField featureText;
        public UITextField featureDescriptionText;
        public UITextField[] abilityLevel;
        public Text descriptionText;

        public GameObject btnGroup_1;
        public GameObject btnGroup_2;

        public Button recruitBtn1;
        public Button recruitBtn2;
    
        PersonRecruit personRecruit;
        public override void OnOpen()
        {
            base.OnOpen();
            btnGroup_1.gameObject.SetActive(false);
            btnGroup_2.gameObject.SetActive(false);
            personRecruit = GameSystem.GetSystem<PersonRecruit>();
            if (personRecruit.recruitType == 0)
            {
                titleText.text = "发现武将";
                btnGroup_1.gameObject.SetActive(true);
                recruitBtn1.interactable = true;
            }
            else
            {
                titleText.text = "登庸武将";
                btnGroup_2.gameObject.SetActive(true);
                recruitBtn2.interactable = true;
            }
            SetPerson(personRecruit.target);
        }

        public void OnCancel()
        {
            personRecruit.Cancel();
        }

        public void OnRecruit()
        {
            personRecruit.RecruitTarget();
            if (personRecruit.tryLimit <= 0)
            {
                recruitBtn1.interactable = false;
                recruitBtn2.interactable = false;
            }
        }

        public void OnRecruit2()
        {
            personRecruit.RecruitTarget2();
            if (personRecruit.tryLimit <= 0)
            {
                recruitBtn1.interactable = false;
                recruitBtn2.interactable = false;
            }
        }

        public void OnRelease()
        {
            personRecruit.ReleaseTarget();
        }

        public void OnKill()
        {
            personRecruit.KillTarget();
        }

        public void OnDetain()
        {
            personRecruit.DetainTarget();
        }

        public void SetPerson(Person person)
        {
            personItem.SetPerson(person);
            statusItem.SetPerson(person);
            ageText.text = person.Age.ToString();
            sexText.text = PersonSortFunction.SortBySex.GetValueStr(person);
            if (person.mFeatureList != null && person.mFeatureList.Count > 0)
            {
                featureText.text = person.mFeatureList[0].Name;
                featureDescriptionText.text = person.mFeatureList[0].desc;
            }
            else
            {
                featureText.text = "";
                featureDescriptionText.text = "";
            }

            List<PersonSortFunction.SortTitle> sortTitles = new List<PersonSortFunction.SortTitle>()
            {
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByWaterLv,
            };

            for (int i = 0; i < sortTitles.Count; i++)
            {
                abilityLevel[i].SetTitle(sortTitles[i].name).SetText(sortTitles[i].GetValueStr(person));
            }

            descriptionText.text = PersonSortFunction.SortByDescription.GetValueStr(person);
        }
    }
}