using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public partial class ModelConfig : SangoObject
    {
        [JsonProperty]
        public byte modelType;
        [JsonProperty]
        public string model;
        [JsonProperty]
        public string texture;
        [JsonProperty]
        public string package;
        [JsonProperty]
        public bool isShardMat;
        [JsonProperty]
        public Sango.Tools.Rect bounds = new Sango.Tools.Rect(0, 0, 32, 32);

        public string ShaderName
        {
            get
            {
                if (modelType == 7)
                {
                    return "";
                }
                else if (modelType == 6)
                {
                    return "Sango/tree_urp.shader";
                }
                else
                    return "Sango/building_urp.shader";
            }
        }

        //public override void Load(BinaryReader reader)
        //{
        //    base.Load(reader);
        //    modelType = reader.ReadByte();
        //    model = reader.ReadString();
        //    texture = reader.ReadString();
        //    package = reader.ReadString();
        //}

        //public override void Save(BinaryWriter writer)
        //{
        //    base.Save(writer);
        //    writer.Write(modelType);
        //    writer.Write(model);
        //    writer.Write(texture);
        //    writer.Write(package);
        //}



    }
}