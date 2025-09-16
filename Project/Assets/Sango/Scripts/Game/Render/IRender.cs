using Sango.Render;
using UnityEngine;

namespace Sango.Game.Render
{
    public interface IRender
    {
        public bool IsVisible();
        public  SangoObject Owener { get; set; }
        public  MapObject MapObject { get; set; }
        public Vector3 GetPosition();
        public void SetPosition(Vector3 pos);
        public Vector3 GetForward();
        public void SetForward(Vector3 forward);
        public void Clear();
    }
}
