using Sango.Core;
using Sango.Core.Object.Arrow;
using UnityEngine;

namespace Sango.Render
{
    public abstract class ObjectRender : IRender
    {
        public delegate void OnModelVisibleChangeFunction(MapObject obj);

        public OnModelVisibleChangeFunction onModelVisibleChangeFunction;

        public bool IsVisible()
        {
            return MapObject != null && MapObject.visible;
        }

        public virtual SangoObject Owener { get; set; }
        public virtual MapObject MapObject { get; set; }

        public Transform GetTransform()
        {
            if (MapObject == null) return null;
            return MapObject.transform;
        }

        public virtual Vector3 GetPosition()
        {
            if (MapObject == null) return Vector3.zero;
            return MapObject.position;
        }
        public virtual void SetPosition(Vector3 pos)
        {
            if (MapObject == null) return;
            MapObject.position = pos;
        }
        public virtual Vector3 GetForward()
        {
            if (MapObject == null) return Vector3.forward; 
            return MapObject.forward;
        }
        public virtual void SetForward(Vector3 forward)
        {
            if (MapObject == null) return;
            MapObject.forward = forward;
        }

        public virtual void CastArrow(Vector3 target)
        {
            if (MapObject == null) return;
            BowmanUnitArrowManager arrowManager = MapObject.GetComponentInChildren<BowmanUnitArrowManager>(true);
            arrowManager?.FireArrows(target);
        }

        public virtual void Clear()
        {
            if (MapObject != null)
            {
                MapObject.Destroy();
                MapObject = null;
            }
        }

        public virtual GameObject PlayEffect(string assets)
        {
            if (MapObject == null || MapObject.visible == false) return null;

            if (!string.IsNullOrEmpty(assets))
            {
                GameObject obj = PoolManager.Get(assets);
                if (obj == null)
                {
                    obj = Sango.Loader.ObjectLoader.LoadObject<GameObject>(assets);
                    if (obj != null)
                    {
                        PoolManager.Add(assets, obj);
                        obj = PoolManager.Get(assets);
                    }
                }

                if (obj != null)
                {
                    obj.transform.SetParent(MapObject.transform, false);
                    obj.SetActive(true);

                    return obj;
                }
            }

            return null;
        }

        public virtual void SetFlash(bool b)
        {

        }

        public virtual void UpdateRender() { }
        public virtual void ShowInfo(int damage, int damageType) {; }
        public virtual void ShowInfo(int damage, int damageType, bool isCrit) {; }
        public virtual void ShowSkill(SkillInstance skill, bool isFail, bool isCritical) {; }

        public virtual void OnModelVisibleChange(MapObject obj)
        {

            onModelVisibleChangeFunction?.Invoke(obj);
        }
    }
}
