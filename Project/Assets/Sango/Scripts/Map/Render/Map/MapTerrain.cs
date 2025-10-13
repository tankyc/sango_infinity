using System.IO;

namespace Sango.Render
{
    public class MapTerrain : MapProperty
    {
        public int cellSize = 64;
        public MapCell[] terrainCells;

        public MapTerrain(MapRender map) : base(map)
        {

        }

        public override void Init()
        {
            base.Init();
            int wCount = map.mapData.vertex_width / cellSize;
            int hCount = map.mapData.vertex_height / cellSize;
            terrainCells = new MapCell[wCount * hCount];

            for (int y = 0; y < hCount; y++)
            {
                for (int x = 0; x < wCount; x++)
                {
                    MapCell cell = new MapCell(map, x * cellSize, y * cellSize, cellSize, cellSize);
                    terrainCells[y * wCount + x] = cell;
                    map.AddStatic(cell);
                }
            }
        }

        internal override void OnSave(BinaryWriter writer)
        {
            writer.Write(cellSize);
        }
        internal override void OnLoad(int versionCode, BinaryReader reader)
        {
            cellSize = reader.ReadInt32();
            Rebuild();
        }

        public override void Clear()
        {
            base.Clear();
            if (terrainCells == null) return;
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                map.RemoveStatic(cell);
                cell.Clear();
            }
            terrainCells = null;
        }

        public override void UpdateRender()
        {

        }

        public void Rebuild()
        {
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                if (cell.IsValid())
                {
                    cell.PrepareDatas();
                }
            }
        }

        public override void Update()
        {
            if (terrainCells == null) return;

            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                cell.Update();
            }
        }

        public override void UpdateImmediate()
        {
            for (int i = 0; i < terrainCells.Length; i++)
            {
                MapCell cell = terrainCells[i];
                if (cell == null) continue;
                cell.visible = true;
                cell.PrepareDatas();
            }
        }
    }
}
