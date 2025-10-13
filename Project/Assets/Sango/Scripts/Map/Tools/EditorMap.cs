using RTEditor;
using Sango.Render;
using System.Collections.Generic;
using UnityEngine;
using Sango;
using Sango.Game;
using System;
using UnityEditor;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using Sango.Mod;

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