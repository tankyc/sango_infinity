using Sango.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Sango.UI
{

    public class UICreateForceItem : MonoBehaviour
    {
        public Text btnName;
        public Text forceName;
        public Text cityName;
        public Image flagColor;
        public Button cancelBtn;
        public Action<ShortForce> onClickNew;
        public Action<ShortForce> onClickDelete;
        public ShortForce target;

        public void OnClickNew()
        {
            onClickNew?.Invoke(target);
        }

        public void OnClickDelete()
        {
            onClickDelete?.Invoke(target);
        }
    }
}
