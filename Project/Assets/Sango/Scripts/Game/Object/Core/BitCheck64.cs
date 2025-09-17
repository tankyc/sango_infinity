using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck64
    {
        [JsonProperty]
        public ulong state;

        public int Max { get { return 64; } }

        public bool Has(int bitPos)
        {
            ulong dest = (ulong)1 << bitPos;
            return (state & dest) == dest;
        }

        public void Set(int bitPos)
        {
            state |= ((ulong)1 << bitPos);
        }

        public void Remove(int bitPos)
        {
            state = state & (~((ulong)1 << bitPos));
        }

        public void Reset()
        {
            state = 0;
        }
        public void SetAll()
        {
            state = ulong.MaxValue;
        }
    }
}
