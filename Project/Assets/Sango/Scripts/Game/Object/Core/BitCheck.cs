using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck
    {
        [JsonProperty]
        public uint[] state;
        public int Max { get { return state.Length * 32; } }
        public BitCheck(int size)
        {
            int n = size / 32;
            int len = size % 32 == 0 ? n : n + 1;
            state = new uint[len];
        }
        public bool Has(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            uint dest = (uint)1 << index;
            return (state[where] & dest) == dest;
        }

        public void Set(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            state[where] |= ((uint)1 << index);
        }

        public void Remove(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            state[where] = state[where] & (~((uint)1 << index));
        }

        public void Reset()
        {
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = 0;
            }
        }
        public void SetAll()
        {
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = uint.MaxValue;
            }
        }
     
        //public override void Load(BinaryReader reader)
        //{
        //    for (int i = 0; i < state.Length; i++)
        //        state[i] = reader.ReadUInt32();
        //}
        //public override void Save(BinaryWriter writer)
        //{
        //    for (int i = 0; i < state.Length; i++)
        //        writer.Write(state[i]);
        //}
    }
}
