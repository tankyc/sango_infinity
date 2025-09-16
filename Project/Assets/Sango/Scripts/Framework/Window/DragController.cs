using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class DragController : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private RectTransform tran;
    private Vector2 pointerOffset;

    private void Awake()
    {
        tran = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 计算触摸点与拖动对象的偏移量
        pointerOffset = eventData.position - (Vector2)tran.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 更新拖动对象的位置
        tran.position = eventData.position - pointerOffset;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 重置偏移量
        pointerOffset = Vector2.zero;
    }
}

