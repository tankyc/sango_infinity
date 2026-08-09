using Sango.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{
    public class UIPersonCreateMenu : UGUIWindow
    {
        public Text personCount;
        public UIObjectDisplayPlane objectDisplayPlane;
        List<SangoObject> editPersonList = new List<SangoObject>();
        public static List<ObjectSortTitle> SortList;
        public GameObject menuRoot;

        protected override void Awake()
        {
            base.Awake();
            menuRoot.SetActive(false);
            SortList = new List<ObjectSortTitle>()
            {
                PersonLibSortFunction.SortByName,
                PersonLibSortFunction.SortByYearBorn,
                PersonLibSortFunction.SortByYearDead,
                PersonLibSortFunction.SortBySex ,
                PersonLibSortFunction.SortByCommand,
                PersonLibSortFunction.SortByStrength,
                PersonLibSortFunction.SortByIntelligence,
                PersonLibSortFunction.SortByPolitics,
                PersonLibSortFunction.SortByGlamour,
             };
        }

        public override void OnOpen()
        {
            base.OnOpen();
            editPersonList.Clear();
            Sango.Core.GameCustomEdit.Instance.ScenarioAddon.PersonAddonMap.ForEach(x => editPersonList.Add(x));
            objectDisplayPlane.Init(editPersonList, SortList, true);
            personCount.text = $"{editPersonList.Count}/9999";
        }

        public void OnCreateNewPerson()
        {
            menuRoot.SetActive(true);
        }
        public void OnAutoCreateNewPerson()
        {
            Window.Instance.Open("window_create_person_auto");
        }

        public void OnManualCreateNewPerson()
        {
            Window.Instance.Open("window_create_person");
        }

        public void OnCreateBack()
        {
            menuRoot.SetActive(false);
        }

        public void OnEditPerson()
        {

        }

        public void OnDeletePerson()
        {

        }


    }
}
