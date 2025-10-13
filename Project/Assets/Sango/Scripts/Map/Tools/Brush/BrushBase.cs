using UnityEngine;
using System.IO;
using System;
using UnityEngine.EventSystems;

namespace Sango.Tools
{

    public class BrushBase
    {
        protected MapEditor editor;

        public BrushBase(MapEditor e)
        {
            editor = e;
        }

        public virtual void Modify(Vector3 center, MapEditor map)
        {

        }
        public virtual void OnSeasonChanged(int curSeason)
        {

        }
        public virtual void DrawGizmos(Vector3 center)
        {
        }
        public virtual void OnEnter()
        {

        }
        

        protected Vector3 lastCenter = Vector3.zero;

  
        public virtual bool IsPointerOverUI()
        {
            return EditorWindow.IsPointOverUI() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject());
        }

        UnityEngine.Rect labRect = new UnityEngine.Rect();

        public virtual void Update()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, editor.map.showLimitLength + 2000, editor.rayCastLayer))
            {

                if (hit.point != lastCenter)
                {
                    //if (Event.current != null)
                    {
                        if (!IsPointerOverUI() &&
                        ((Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButton(0) ) || Input.GetMouseButtonDown(0)))
                        {
                            Modify(hit.point, editor);
                            lastCenter = hit.point;
                        }
                        DrawGizmos(hit.point);
                    }
                }
            }
        }

        public virtual void OnBrushTypeChange() { }
        public virtual void OnBrushSizeChange() { }
        public virtual void OnBrushOpacityChange() { }
        public virtual void Clear()
        {

        }
        public virtual void OnGUI()
        {

        }

    }
}