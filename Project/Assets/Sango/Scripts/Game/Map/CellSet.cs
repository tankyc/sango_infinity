using Sango.Render;
using System.IO;
using Newtonsoft.Json;
using System.Text;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CellSet : IDataString
    {
        int width, height;
        protected Cell[][] Cells;
        public void Init(int w, int h)
        {
            width = w;
            height = h;
            Cells = new Cell[w][];
            for (ushort x = 0; x < w; x++)
            {
                Cell[] cells = new Cell[h];
                for (ushort y = 0; y < h; y++)
                    cells[y] = new Cell(x, y);
                Cells[x] = cells;
            }
        }
        public void Init(int w, int h, string[] valus)
        {
            width = w;
            height = h;
            Cells = new Cell[w][];
            for (ushort x = 0; x < w; x++)
            {
                Cell[] cells = new Cell[h];
                for (ushort y = 0; y < h; y++)
                {
                    Cell c = new Cell(x, y);

                    string[] vs = valus[x * h + y].Split(',');
                    c.terrainType = int.Parse(vs[0]);
                    c.TerrainType = Scenario.Cur.CommonData.TerrainTypes.Get(c.terrainType);
                    if (c.TerrainType == null)
                        c.TerrainType = Scenario.Cur.CommonData.TerrainTypes[0];
                    //if (vs.Length > 1)
                    //    c.Fertility = float.Parse(vs[1]);
                    //if (vs.Length > 2)
                    //    c.Prosperity = float.Parse(vs[2]);
                    c.moveAble = c.TerrainType.moveable;
                    cells[y] = c;
                }
                Cells[x] = cells;
            }
        }
        public void SetTerrainType(int x, int y, int terrainType)
        {
            Cell c = Cells[x][y];
            c.terrainType = terrainType;
            c.TerrainType = Scenario.Cur.CommonData.TerrainTypes.Get(c.terrainType);
            if (c.TerrainType == null)
                c.TerrainType = Scenario.Cur.CommonData.TerrainTypes[0];
            //c.Fertility = c.TerrainType.Fertility;
            //c.Prosperity = c.TerrainType.Prosperity;
            c.moveAble = c.TerrainType.moveable;
        }

        public void Init(Map map)
        {
            for (ushort x = 0; x < width; x++)
            {
                for (ushort y = 0; y < height; y++)
                {
                    Cells[x][y].Init(map);
                }
            }

        }

        public Cell GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return null;

            return Cells[x][y];
        }
        public TerrainType GetTerrainType(int x, int y) { return Cells[x][y].TerrainType; }
        public void FromString(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            int w = int.Parse(values[values.Length - 2]);
            int h = int.Parse(values[values.Length - 1]);
            Init(w, h, values);
        }
        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (Cells != null)
            {
                for (ushort x = 0; x < Cells.Length; x++)
                {
                    Cell[] cells = Cells[x];
                    for (ushort y = 0; y < cells.Length; y++)
                    {
                        Cell cell = cells[y];
                        stringBuilder.Append(cell.terrainType);
                        //stringBuilder.Append(',');
                        //stringBuilder.Append(cell.Fertility);
                        //stringBuilder.Append(',');
                        //stringBuilder.Append(cell.Prosperity);
                        //stringBuilder.Append(';');
                    }
                }
                stringBuilder.Append(Cells.Length);
                stringBuilder.Append(',');
                stringBuilder.Append(Cells[0].Length);
            }
            return stringBuilder.ToString();
        }
        //public override void Load(BinaryReader node)
        //{
        //    int w = node.ReadInt32();
        //    if (w > 0)
        //    {
        //        int h = node.ReadInt32();
        //        Cells = new Cell[w][];
        //        for (ushort x = 0; x < w; x++)
        //        {
        //            Cell[] cells = new Cell[h];
        //            for (ushort y = 0; y < h; y++)
        //            {
        //                Cell c = new Cell(x, y);
        //                c.terrainType = node.ReadInt32();
        //                c.TerrainType = Scenario.Cur.CommonData.TerrainTypes.Get(c.terrainType);
        //                if (c.TerrainType == null)
        //                    c.TerrainType = Scenario.Cur.CommonData.TerrainTypes[0];
        //                cells[y] = c;
        //                c.moveAble = c.TerrainType.moveable;
        //            }
        //            Cells[x] = cells;
        //        }
        //    }
        //}
        //public override void Save(BinaryWriter node)
        //{
        //    if (Cells != null)
        //    {
        //        node.Write(Cells.Length);
        //        node.Write(Cells[0].Length);
        //        for (int x = 0; x < Cells.Length; x++)
        //        {
        //            Cell[] cells = Cells[x];
        //            for (int y = 0; y < cells.Length; y++)
        //            {
        //                Cell cell = cells[y];
        //                node.Write(cell.terrainType);

        //            }
        //        }
        //    }
        //    else
        //    {
        //        node.Write(0);
        //    }
        //}
        //public override void Save(System.Xml.XmlNode node)
        //{
        //    node.InnerText = ToString();
        //}
        //public override void Load(System.Xml.XmlNode node)
        //{
        //    FromString(node.InnerText);
        //}
        //public override void Save(SimpleJSON.JSONNode node)
        //{
        //    node.Value = ToString();
        //}
        //public override void Load(SimpleJSON.JSONNode node)
        //{
        //    FromString(node.Value);
        //}
    }
}
