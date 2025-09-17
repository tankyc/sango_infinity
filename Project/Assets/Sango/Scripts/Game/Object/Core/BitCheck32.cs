using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck32
    {
        [JsonProperty]
        public uint state;
        public int Max { get { return 32; } }

        public bool Has(int bitPos)
        {
            uint dest = (uint)1 << bitPos;
            return (state & dest) == dest;
        }

        public void Set(int bitPos)
        {
            state |= ((uint)1 << bitPos);
        }

        public void Remove(int bitPos)
        {
            state = state & (~((uint)1 << bitPos));
        }

        public void Reset()
        {
            state = 0;
        }
        public void SetAll()
        {
            state = uint.MaxValue;
        }

        //public override void LoadFromStream(BinaryReader reader)
        //{
        //    state = reader.ReadUInt32();
        //}
        //public override void SaveToStream(BinaryWriter writer)
        //{
        //    writer.Write(state);
        //}
    }
}
