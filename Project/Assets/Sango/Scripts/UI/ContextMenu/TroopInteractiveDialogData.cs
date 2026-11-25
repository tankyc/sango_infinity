using System;
using System.Collections.Generic;
using UnityEngine;

using Sango.Core; namespace Sango.UI
{
    public class TroopInteractiveDialogData : ITroopInteractiveDialogData
    {
        public static TroopInteractiveDialogData InteractiveDialogData = new TroopInteractiveDialogData();

        public string content;
        public System.Action sureAction;
        public bool isValid = true;

        public bool IsValid => isValid;

        public void Clear()
        {
            content = null;
            sureAction = null;
            isValid = true;
        }

        public void SetValid(bool valid)
        {
            isValid = valid;
        }

        public bool IsEmpty()
        {
            return string.IsNullOrEmpty(content);
        }

        public void SetContent(string content)
        {
            this.content = content;
        }

        public void SetSureAction(System.Action action)
        {
            this.sureAction = action;
        }
    }
}
