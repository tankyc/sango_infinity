using Sango.Core;
using Sango.Core.Player;
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
        List<PersonLib> editPersonLibList = new List<PersonLib>();
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
            menuRoot.SetActive(false);
            editPersonList.Clear();
            editPersonLibList.Clear();
            Sango.Core.GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.ForEach(x =>
            {
                editPersonList.Add(x); editPersonLibList.Add(x);
            }
            );
            objectDisplayPlane.Init(editPersonList, SortList, true);
            personCount.text = $"{editPersonList.Count}/9999";
        }

        public void OnCreateNewPerson()
        {
            menuRoot.SetActive(true);
        }
        public void OnAutoCreateNewPerson()
        {
            Close();
            GameCustomEdit.Instance.TargetEditPerson = null;
            Window.Instance.Open("window_create_person_auto");
        }

        public void OnManualCreateNewPerson()
        {
            Close();
            GameCustomEdit.Instance.TargetEditPerson = null;
            Window.Instance.Open("window_create_person");
        }

        public void OnCreateBack()
        {
            menuRoot.SetActive(false);
        }

        public void OnEditPerson()
        {
            GameSystem.GetSystem<EditPersonSelectSystem>().Start(editPersonLibList, new List<PersonLib>(), -1, (x) =>
            {
                if (x.Count > 0)
                {
                    PersonLib personLib = x[0] as PersonLib;
                    GameCustomEdit.Instance.TargetEditPerson = personLib;
                    Close();
                    Window.Instance.Open("window_create_person");
                }
            }, SortList, "编辑武将");
        }

        public void OnDeletePerson()
        {
            GameSystem.GetSystem<EditPersonSelectSystem>().Start(editPersonLibList, new List<PersonLib>(), -1, (x) =>
            {
                if (x.Count > 0)
                {
                    PersonLib personLib = x[0] as PersonLib;
                    GameCustomEdit.Instance.TargetEditPerson = personLib;
                    GameDialog.Instance.Open(GameDialog.DialogStyle.Normal, $"确定要删除{personLib.ColorName}, 删除之后将无法找回...", () =>
                    {
                        GameCustomEdit.Instance.SelfScenarioAddon.PersonLibrary.Remove(personLib);
                        editPersonLibList.Remove(personLib);
                        editPersonList.Remove(personLib);
                        objectDisplayPlane.Init(editPersonList, SortList, true);
                        personCount.text = $"{editPersonList.Count}/9999";
                        GameCustomEdit.Instance.SaveScenarioAddon();
                    });

                }
            }, SortList, "删除武将");
        }


    }
}
