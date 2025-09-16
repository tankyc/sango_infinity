using UnityEngine;

namespace Sango.Render
{
    public class MapLooper : MonoBehaviour
    {
        private void Update()
        {
#if UNITY_EDITOR
            if (MapRender.Instance != null)
                MapRender.Instance.Update();
#else
            MapRender.Instance.Update();
#endif

        }
    }
}
