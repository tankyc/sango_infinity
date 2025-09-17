using System.IO;
using Newtonsoft.Json;

namespace Sango.Game
{
    public enum AttributeType : int
    {
        ///// <summary>
        ///// 统御
        ///// </summary>
        Command = 0,
        ///// <summary>
        ///// 武力
        ///// </summary>
        Strength = 1,
        ///// <summary>
        ///// 智力
        ///// </summary>
        Intelligence = 2,
        ///// <summary>
        ///// 政治
        ///// </summary>
        Politics = 3,
        ///// <summary>
        ///// 魅力
        ///// </summary>
        Glamour = 4,
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class PersonAttributeType : SangoObject
    {

        public int nameID;
        //public override void Load(BinaryReader reader)
        //{
        //    nameID = reader.ReadInt32();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    writer.Write(nameID);
        //}
    }
}
