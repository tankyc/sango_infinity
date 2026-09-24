using TKNewtonsoft.Json;
using System;

namespace Sango.Core
{
    /// <summary>
    /// 地图格子的坐标。
    ///
    /// 存档形态仍然是 <c>[x, y]</c> 两个数（与旧格式完全一致），
    /// 但解析不再依赖 <c>GameEvent.OnScenarioPrepare</c> 与反射回填，
    /// 而是由持有者（如 Troop.cell）在访问时用坐标去地图上取格子。
    /// </summary>
    public struct CellXY
    {
        public int x;
        public int y;

        public CellXY(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>无效/未设置（坐标为负数）</summary>
        public static CellXY Invalid { get { return new CellXY(-1, -1); } }

        public bool IsValid { get { return x >= 0 && y >= 0; } }

        public static CellXY From(Cell cell)
        {
            if (cell == null) return Invalid;
            return new CellXY(cell.x, cell.y);
        }

        public Cell ToCell()
        {
            if (!IsValid) return null;
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return null;
            return scenario.Map.GetCell(x, y);
        }
    }

    public class CellXYConverter : JsonConverter<CellXY>
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartArray();
            if (value is CellXY)
            {
                CellXY pos = (CellXY)value;
                writer.WriteValue(pos.x);
                writer.WriteValue(pos.y);
            }
            writer.WriteEndArray();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            int x = -1;
            int y = -1;
            bool xReaded = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                    break;

                if (reader.TokenType == JsonToken.Integer)
                {
                    int v = serializer.Deserialize<int>(reader);
                    if (!xReaded)
                    {
                        x = v;
                        xReaded = true;
                    }
                    else
                    {
                        y = v;
                    }
                }
            }
            return new CellXY(x, y);
        }
    }
}
