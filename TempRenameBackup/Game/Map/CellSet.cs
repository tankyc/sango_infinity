using Sango.Render;
using System.IO;
using TKNewtonsoft.Json;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 单元格集合类，管理游戏地图中的所有单元格
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class CellSet : IDataString
    {
        /// <summary>
        /// 宽度
        /// </summary>
        int width, height;
        /// <summary>
        /// 单元格数组
        /// </summary>
        protected Cell[][] Cells;
        /// <summary>
        /// 初始化单元格集合
        /// </summary>
        /// <param name="w">宽度</param>
        /// <param name="h">高度</param>
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
        /// <summary>
        /// 初始化单元格集合
        /// </summary>
        /// <param name="w">宽度</param>
        /// <param name="h">高度</param>
        /// <param name="valus">值数组</param>
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
                    int terrainTypeId = int.Parse(vs[0]);
                    c.TerrainType = Scenario.Cur.CommonData.TerrainTypes.Get(terrainTypeId);
                    if (c.TerrainType == null)
                        c.TerrainType = Scenario.Cur.CommonData.TerrainTypes[0];
                    if (vs.Length > 1)
                    {
                        c.terrainState = int.Parse(vs[2]);
                    }
                    if (vs.Length > 2)
                    {
                        int areaId = int.Parse(vs[1]);
                        c.BelongCity = Scenario.Cur.citySet.Get(areaId);
                        c.BelongCity?.AddAreaCell(c);
                    }
                    //if (vs.Length > 2)
                    //    c.Prosperity = float.Parse(vs[2]);
                    c.moveAble = c.TerrainType.moveable;
                    cells[y] = c;
                }
                Cells[x] = cells;
            }
        }

        /// <summary>
        /// 清空单元格集合
        /// </summary>
        public void Clear()
        {
            Cells = null;
        }
        /// <summary>
        /// 设置单元格的地形类型和状态
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="terrainType">地形类型</param>
        /// <param name="terrainState">地形状态</param>
        /// <param name="belongCity">所属城市</param>
        public void SetTerrainTypeAndState(int x, int y, TerrainType terrainType, int terrainState, City belongCity)
        {
            Cell c = Cells[x][y];
            c.TerrainType = terrainType;
            c.BelongCity = belongCity;
            c.moveAble = c.TerrainType.moveable;
            c.terrainState = terrainState;
            belongCity?.AddAreaCell(c);
        }

        /// <summary>
        /// 初始化单元格集合
        /// </summary>
        /// <param name="map">地图对象</param>
        public void Init(Map map)
        {
            for (ushort x = 0; x < width; x++)
            {
                Cell[] cells = Cells[x];
                for (ushort y = 0; y < height; y++)
                {
                    cells[y].Init(map);
                }
            }

        }

        /// <summary>
        /// 获取指定坐标的单元格
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>单元格对象</returns>
        public Cell GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return null;

            return Cells[x][y];
        }
        /// <summary>
        /// 获取指定坐标的地形类型
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <returns>地形类型</returns>
        public TerrainType GetTerrainType(int x, int y) { return Cells[x][y].TerrainType; }
        /// <summary>
        /// 从字符串加载数据
        /// </summary>
        /// <param name="data">数据字符串</param>
        public void FromString(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ';' });
            int w = int.Parse(values[values.Length - 2]);
            int h = int.Parse(values[values.Length - 1]);
            Init(w, h, values);
        }
        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns>字符串表示</returns>
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
                        stringBuilder.Append(cell.TerrainType?.Id ?? 0);
                        stringBuilder.Append(',');
                        stringBuilder.Append(cell.terrainState);
                        stringBuilder.Append(',');
                        stringBuilder.Append(cell.BelongCity?.Id ?? 0);
                        stringBuilder.Append(';');
                    }
                }
                stringBuilder.Append(Cells.Length);
                stringBuilder.Append(';');
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
