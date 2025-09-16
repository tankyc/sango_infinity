using System.IO;
using Newtonsoft.Json;


namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class ScenarioInfo
    {
        [JsonProperty]
        public int id;
        [JsonProperty]
        public string name;
        [JsonProperty]
        public string tag;
        [JsonProperty]
        public string description;
        [JsonProperty]
        public int year;
        [JsonProperty]
        public int month;
        [JsonProperty]
        public int day;
        [JsonProperty]
        public int curForceId;
        [JsonProperty]
        public string mapType;
        [JsonProperty]
        public int turnCount;
        [JsonProperty]
        public int priority;
        //public override void Load(BinaryReader reader)
        //{
        //    id = reader.ReadInt32();
        //    name = reader.ReadString();
        //    tag = reader.ReadString();
        //    description = reader.ReadString();
        //    year = reader.ReadUInt16();
        //    month = reader.ReadByte();
        //    day = reader.ReadByte();
        //    curForceId = reader.ReadUInt16();
        //    mapType = reader.ReadString();
        //    turnCount = reader.ReadUInt16();
        //}
        //public override void Save(BinaryWriter writer)
        //{
        //    writer.Write(id);
        //    writer.Write(name);
        //    writer.Write(tag);
        //    writer.Write(description);
        //    writer.Write(year);
        //    writer.Write(month);
        //    writer.Write(day);
        //    writer.Write(curForceId);
        //    writer.Write(mapType);
        //    writer.Write(turnCount);
        //}
    }
}
