using Sango.Core;
using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectFieldEdit : MonoBehaviour
    {
        public UIObjectDisplayPlane uIObjectDisplayPlane;
        float clickTime = 0;
        List<SangoObject> multObjets = new List<SangoObject>();
        public void OnSelectObjectField(UITextItem textItem)
        {
            if (clickTime <= 0)
            {
                clickTime = Time.realtimeSinceStartup;
                return;
            }

            if(Time.realtimeSinceStartup - clickTime < 0.3f)
            {
                clickTime = 0;
                if (textItem.obj != null)
                {
                    multObjets.Clear();
                    uIObjectDisplayPlane.GetSelectObjects(multObjets);
                    multObjets.Remove(textItem.obj);
                    multObjets.Add(textItem.obj);
                    //Sango.Log.Error(objectSortTitle.GetValueStr(obj));
                    UIDataEdit.Show(multObjets, textItem.objectSortTitle, Scenario.Cur, () =>
                    {
                        uIObjectDisplayPlane.OnRefresh();
                    });
                }
                else
                {
                    Sango.Log.Error(textItem.objectSortTitle.name);
                }
            }
            else
            {
                clickTime = Time.realtimeSinceStartup;
            }
        }
    }
}