using Sango.Game.Render.UI;
using Sango.Render;
using System.Runtime.Remoting.Messaging;
using UnityEngine;

namespace Sango.Game.Render
{
    public abstract class ObjectRender : IRender
    {
       public bool IsVisible()
        {
            return MapObject != null && MapObject.visible;
        }

        public virtual SangoObject Owener { get; set; }
        public virtual MapObject MapObject { get; set; }

        public virtual Vector3 GetPosition()
        {
            return MapObject.position;
        }
        public virtual void SetPosition(Vector3 pos)
        {
            MapObject.position = pos;
        }
        public virtual Vector3 GetForward()
        {
            return MapObject.forward;
        }
        public virtual void SetForward(Vector3 forward)
        {
            MapObject.forward = forward;
        }
        public virtual void Clear()
        {
            if (MapObject != null)
            {
                MapObject.Destroy();
                MapObject = null;
            }
        }

        public virtual void UpdateRender() { }

         public virtual void ShowDamage(int damage, int damageType)
        {
            
        }
    }
}
