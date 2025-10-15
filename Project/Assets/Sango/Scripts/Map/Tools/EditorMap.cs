using Sango.Game;
using Sango.Render;
using UnityEngine;

namespace Sango.Tools
{
    [ExecuteInEditMode]
    /// <summary>
    /// 地图编辑器
    /// </summary>
    public class EditorMap : MonoBehaviour
    {
        public void Awake()
        {
            Path.Init();
            GameData.Instance.Init();
            MapRender.Instance.LoadMap(Path.FindFile($"Map/DefaultMap.bin"));
            MapRender.Instance.UpdateImmediate();
        }

        public void OnDestroy()
        {
            MapRender.Instance.Clear();
        }
    }
}