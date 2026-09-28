using Sango.Core;
using UnityEngine.UI;

namespace Sango.UI
{
    public class UICityDiplomacyPlayerCheckDialog : UGUIWindow
    {
        public Text windowTitle;
        public UITextField who;
        public UITextField target;
        public UITextField gold;
        public UITextField relationship;

        public System.Action sureAction;
        public System.Action cancelAction;
        DiplomacyActionType actionType;
        Person person;
        Force receiverForce;
        int resourceValue;


        public override void OnOpen(params object[] objects)
        {
            actionType = (DiplomacyActionType)objects[0];
            windowTitle.text = GameSystem.GetSystem<DiplomacyManager>().GetActionName(actionType);
            person = objects[1] as Person;
            receiverForce = objects[2] as Force;
            resourceValue = (int)objects[3];
            sureAction = (System.Action)objects[4];
            if (objects.Length > 5)
                cancelAction = (System.Action)objects[5];
            UpdateContent();
        }

        public void UpdateContent()
        {
            who.text = $"{person.BelongForce.Name}";
            target.text = $"{GameSystem.GetSystem<DiplomacyManager>().GetActionName(actionType)}";
            relationship.text = Scenario.Cur.GetRelation(person.BelongForce, receiverForce).ToString();
            gold.text = $"{resourceValue}";
        }
        
        public void OnSure()
        {
            Close();
            sureAction?.Invoke();
        }
        
        public void OnCancel()
        {
            Close();
            cancelAction?.Invoke();
        }
    }
}