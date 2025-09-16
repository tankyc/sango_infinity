using System;
using System.Collections;
using System.IO;

namespace Sango.Game
{
    public struct MapCoords
    {
        public ushort x;
        public ushort y;

        public void LoadFromStream(BinaryReader reader) 
        { 
            x = reader.ReadUInt16(); 
            y = reader.ReadUInt16();
        }
        public void SaveToStream(BinaryWriter writer) 
        { 
            writer.Write(x); 
            writer.Write(y);
        }

    }
}
