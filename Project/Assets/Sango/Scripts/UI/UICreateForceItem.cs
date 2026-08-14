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
        public Action<int> onClickNew;
        public Action<int> onClickDelete;
        public int index;

        public void OnClickNew()
        {
            onClickNew?.Invoke(index);
        }

        public void OnClickDelete()
        {
            onClickDelete?.Invoke(index);
        }
    }
}
