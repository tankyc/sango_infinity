using Sango.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Sango.UI
{
    public class UIObjectListItem : MonoBehaviour
    {
        public UITextItem idItem;
        public UITextItem textItem;
        public UISelectItem selectItem;
        List<UITextItem> pool = new List<UITextItem>();
        List<UITextItem> usedItems = new List<UITextItem>();
        public int index;
        public delegate void OnSelect(UIObjectListItem item);
        public delegate void OnShow(UIObjectListItem item);
        public OnSelect onSelected;
        public OnSelect onDoubleSelected;
        public OnShow onShow;
        public RectTransform contentRect;
        public Image selectImg;
        public Image overImg;
        public Image pressImg;
        public bool hasId = false;

        /// <summary>双击间隔判定阈值（秒）</summary>
        const float DoubleClickInterval = 0.3f;

        float lastClickTime = -1f;

        /// <summary>当前是否被置灰（灰显 = 不满足条件，不可操作）</summary>
        public bool IsDisabled { get; private set; }

        void ScrollCellIndex(int idx)
        {
            index = idx;
            onShow?.Invoke(this);
        }

        /// <summary>
        /// 单击 / 双击分发。这里只做分发，具体动作由宿主（如官职界面）决定。
        /// 双击时仍会先触发一次单击回调，符合"先选中、再执行"的直觉。
        /// </summary>
        public void OnClick()
        {
            onSelected?.Invoke(this);

            if (onDoubleSelected == null)
                return;

            float now = Time.unscaledTime;
            if (lastClickTime >= 0f && now - lastClickTime <= DoubleClickInterval)
            {
                lastClickTime = -1f;
                onDoubleSelected(this);
            }
            else
            {
                lastClickTime = now;
            }
        }

        /// <summary>
        /// 置灰：不满足条件的行用灰字显示，但仍可点击（点击后由宿主决定是否拒绝）。
        /// 注意每列文本在 SetObjectSortTitle 里会被重置成白色，所以宿主必须在渲染之后再调用一次。
        /// </summary>
        public void SetDisable(bool disabled)
        {
            IsDisabled = disabled;
            Color c = disabled ? new Color(0.42f, 0.42f, 0.42f, 1f) : Color.white;
            if (idItem != null) idItem.SetColor(c);
            if (textItem != null) textItem.SetColor(c);
            for (int i = 0; i < usedItems.Count; i++)
                usedItems[i].SetColor(c);
        }

        /// <summary>从池/新建的列文本里取回颜色（Clear 之后需要重新应用置灰）</summary>
        public void RefreshDisable()
        {
            if (IsDisabled)
                SetDisable(true);
        }

        public void Clear()
        {
            selectItem.SetVisible(false);
            textItem.SetText("");
            for (int i = 0; i < usedItems.Count; i++)
            {
                usedItems[i].gameObject.SetActive(false);
                pool.Add(usedItems[i]);
            }
            usedItems.Clear();
        }

        public void Set(string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            selectItem.SetVisible(!string.IsNullOrEmpty(content));
            textItem.SetText(content);
            textItem.SetObjectSortTitle(objectSortTitle);
            textItem.SetObject(sangoObject);
        }
        public void SetId(string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            if (idItem == null) return;
            idItem.gameObject.SetActive(hasId);
            idItem.SetText(content);
            idItem.SetObjectSortTitle(objectSortTitle);
            idItem.SetObject(sangoObject);
        }



        public void Add(string content, float width, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            UITextItem item;
            if (pool.Count == 0)
            {
                GameObject obj = GameObject.Instantiate(textItem.gameObject, contentRect);
                item = obj.GetComponent<UITextItem>();
            }
            else
            {
                item = pool[0];
                pool.RemoveAt(0);
            }
            usedItems.Add(item);
            item.gameObject.SetActive(true);
            item.SetWidth(width).SetText(content);
            item.transform.SetAsLastSibling();
            item.SetObjectSortTitle(objectSortTitle);
            item.SetObject(sangoObject);
        }

        public void Add(string content, float width, int alignment, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            UITextItem item;
            if (pool.Count == 0)
            {
                GameObject obj = GameObject.Instantiate(textItem.gameObject, contentRect);
                item = obj.GetComponent<UITextItem>();
            }
            else
            {
                item = pool[0];
                pool.RemoveAt(0);
            }
            usedItems.Add(item);
            item.gameObject.SetActive(true);
            item.SetWidth(width).SetText(content).SetAlignment((TextAnchor)alignment);
            item.transform.SetAsLastSibling();
            item.SetObjectSortTitle(objectSortTitle);
            item.SetObject(sangoObject);
        }

        public void Set(int index, string content, ObjectSortTitle objectSortTitle = null, SangoObject sangoObject = null)
        {
            if(hasId)
            {
                if (index == 0)
                {
                    SetId(content, objectSortTitle, sangoObject);
                }
                else if (index == 1)
                {
                    Set(content, objectSortTitle, sangoObject);
                }
                else
                {
                    UITextItem item = usedItems[index - 2];
                    item.SetText(content);
                    item.SetObjectSortTitle(objectSortTitle);
                    item.SetObject(sangoObject);
                }
            }
            else
            {
                if (index == 0)
                    Set(content, objectSortTitle, sangoObject);
                else
                {
                    UITextItem item = usedItems[index - 1];
                    item.SetText(content);
                    item.SetObjectSortTitle(objectSortTitle);
                    item.SetObject(sangoObject);
                }
            }
           
        }

        public void Set(SangoObject sangoObject, List<ObjectSortTitle> objectSorts)
        {
            for (int i = 0; i < objectSorts.Count; i++)
            {
                ObjectSortTitle sortTitle = objectSorts[i];
                string content = sangoObject == null ? "" : sortTitle.GetValueStr(sangoObject);
                if(hasId )
                {
                    if (i == 0)
                    {
                        SetId(content, sortTitle, sangoObject);
                        continue;
                    }
                    else if (i == 1)
                    {
                        Set(content, sortTitle, sangoObject);
                        continue;
                    }

                    UITextItem item = usedItems[i - 2];
                    item.SetText(content);
                    item.SetObjectSortTitle(sortTitle);
                    item.SetObject(sangoObject);
                }
                else
                {
                    if (i == 0)
                    {
                        Set(content, sortTitle, sangoObject);
                        continue;
                    }

                    UITextItem item = usedItems[i - 1];
                    item.SetText(content);
                    item.SetObjectSortTitle(sortTitle);
                    item.SetObject(sangoObject);
                }
               
            }
        }

        public void SetSelected(bool b)
        {
            selectItem?.SetSelected(b);
            selectImg.enabled = b;
        }

        public bool IsSelected()
        {
            return selectImg.enabled;
        }

        /// <summary>“已派去登用”标记（运行时创建：选中圈右侧的黄色小圈，仅供一键登庸的多选界面使用）</summary>
        Image markImg;

        /// <summary>
        /// 显示/隐藏“已派去登用”黄色圈标记。
        /// 首次显示时按需运行时创建（复用选中圈的圆形样式，染成黄色，不依赖预制体改动）。
        /// </summary>
        public void SetMark(bool show)
        {
            if (show)
            {
                if (markImg == null)
                    markImg = CreateMark();
                if (markImg != null)
                    markImg.enabled = true;
            }
            else if (markImg != null)
            {
                markImg.enabled = false;
            }
        }

        Image CreateMark()
        {
            if (selectImg == null || selectImg.sprite == null)
                return null;

            GameObject go = new GameObject("mark_dispatched", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(selectImg.transform, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            // 锚定在选中圈右侧 3px，竖直居中，尺寸与选中圈相当
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(3f, 0f);
            rt.sizeDelta = new Vector2(16f, 16f);

            Image img = go.GetComponent<Image>();
            img.sprite = selectImg.sprite;
            img.color = new Color(1f, 0.8f, 0.25f, 1f); // 黄色，与“登庸中”提示呼应
            img.raycastTarget = false;
            img.enabled = true;
            return img;
        }

        public void SetOver(bool b)
        {
            overImg.enabled = b;
        }

        public void SetPressd(bool b)
        {
            pressImg.enabled = b;
        }

    }

    /// <summary>
    /// 列表行的拖动事件绑定器（目前只有官职界面用）。
    ///
    /// 【为什么不用 AddComponent&lt;IDragHandler&gt;】列表行的根节点上已经有 <see cref="EventTrigger"/>
    /// （预制体自带，用来做悬停/按下高亮），而 <c>EventTrigger</c> 自身实现了
    /// <c>IBeginDragHandler / IDragHandler / IEndDragHandler</c>。Unity 派发拖动时取的是
    /// "从命中物体向上找到的**第一个**拖动处理器"，同级组件的先后顺序不可控 ——
    /// 另挂一个 IDragHandler 有概率被 EventTrigger 抢先吞掉事件。
    /// 所以这里往 EventTrigger 的 triggers 里追加条目，保证一定能收到（只在运行时改，不写回预制体）。
    /// </summary>
    public class UIDragBinder : MonoBehaviour
    {
        /// <summary>进入拖拽模式（水平位移超过阈值）时回调</summary>
        public System.Action<UIObjectListItem> onBeginDrag;

        /// <summary>
        /// 拖拽过程中指针移动回调，参数为当前屏幕坐标。
        /// 仅在已进入拖拽模式后触发，供宿主做"浮动文本跟随指针 / 悬停行高亮"。
        /// </summary>
        public System.Action<UIObjectListItem, Vector2> onDragging;

        /// <summary>拖拽结束时回调，参数为松手时的屏幕坐标</summary>
        public System.Action<UIObjectListItem, Vector2> onEndDrag;

        /// <summary>判定为"拖动"所需的水平位移（像素）</summary>
        public const float DragThreshold = 16f;

        UIObjectListItem item;
        bool dragging;
        Vector2 startPosition;

        /// <summary>
        /// 绑定拖动事件。重复调用不会重复添加条目。
        /// </summary>
        public void Bind(UIObjectListItem target)
        {
            if (target == null || item == target)
                return;

            item = target;
            EventTrigger trigger = GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry begin = new EventTrigger.Entry();
            begin.eventID = EventTriggerType.BeginDrag;
            begin.callback.AddListener(OnBegin);
            trigger.triggers.Add(begin);

            EventTrigger.Entry drag = new EventTrigger.Entry();
            drag.eventID = EventTriggerType.Drag;
            drag.callback.AddListener(OnDrag);
            trigger.triggers.Add(drag);

            EventTrigger.Entry end = new EventTrigger.Entry();
            end.eventID = EventTriggerType.EndDrag;
            end.callback.AddListener(OnEnd);
            trigger.triggers.Add(end);
        }

        void OnBegin(BaseEventData data)
        {
            dragging = false;
            PointerEventData e = data as PointerEventData;
            if (e != null)
                startPosition = e.position;
        }

        void OnDrag(BaseEventData data)
        {
            if (item == null)
                return;

            PointerEventData e = data as PointerEventData;
            if (e == null)
                return;

            if (!dragging)
            {
                if (Mathf.Abs(e.position.x - startPosition.x) < DragThreshold)
                    return;

                dragging = true;
                onBeginDrag?.Invoke(item);
            }

            // 进入拖拽后仍要逐帧收到指针位置，否则浮动文本只能显示在起手点
            onDragging?.Invoke(item, e.position);
        }

        void OnEnd(BaseEventData data)
        {
            if (!dragging)
                return;

            dragging = false;
            PointerEventData e = data as PointerEventData;
            onEndDrag?.Invoke(item, e != null ? e.position : Vector2.zero);
        }
    }
}