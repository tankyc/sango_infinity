using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIEventPass : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    public GameObject passToObject;
    public bool nextOnly = true;
    public bool PointerClick = true;
    public bool PointerDown = true;
    public bool PointerUp = true;
    public bool Drag = true;
    public void PassEvent<T>(PointerEventData data, ExecuteEvents.EventFunction<T> func) where T : IEventSystemHandler
    {
        if(passToObject != null)
        {
            ExecuteEvents.Execute(passToObject, data, func);
            return;
        }

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);

        GameObject currObj = gameObject;
        int currIndex = -1;
        for (int i = 0; i < results.Count; i++)
        {
            if (currIndex >= 0)
            {
                ExecuteEvents.Execute(results[i].gameObject, data, func);
                if (nextOnly)
                    break;
            }
            if (currObj == results[i].gameObject)
                currIndex = i;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (PointerClick)
            PassEvent(eventData, ExecuteEvents.pointerClickHandler);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (PointerDown)
            PassEvent(eventData, ExecuteEvents.pointerDownHandler);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (PointerUp)
            PassEvent(eventData, ExecuteEvents.pointerUpHandler);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (Drag)
            PassEvent(eventData, ExecuteEvents.dragHandler);
    }
}
