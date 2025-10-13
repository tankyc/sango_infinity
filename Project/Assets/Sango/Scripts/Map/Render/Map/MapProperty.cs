using System.IO;
using UnityEngine;

namespace Sango.Render
{
    /// <summary>
    /// 地图属性基类
    /// </summary>
    public class MapProperty
    {
        internal MapRender map;
        public int curSeason = 0;
        public MapProperty(MapRender map)
        {
            this.map = map;
        }

        public virtual void OnSeasonChange(int season)
        {
            this.curSeason = season;
            UpdateRender();
        }

        public virtual void UpdateRender() { }
        internal virtual void OnSave(BinaryWriter writer) { }
        internal virtual void OnSaveScale(BinaryWriter writer, int scale) {
            OnSave(writer);
        }
        internal virtual void OnLoad(int versionCode, BinaryReader reader) { }

        public virtual void Init() {
            map.onSeasonChange += OnSeasonChange;
        }
        public virtual void Update() { }
        public virtual void UpdateImmediate() { }
        public virtual void Clear() { 
            map.onSeasonChange -= OnSeasonChange;
        }
    }
}
