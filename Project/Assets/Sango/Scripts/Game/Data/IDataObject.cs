using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public interface IDataObject
    {
        public void Save(System.Xml.XmlNode node);
        public void Load(System.Xml.XmlNode node);
        public void Save(SimpleJSON.JSONNode node);
        public void Load(SimpleJSON.JSONNode node);
        public void Save(BinaryWriter node);
        public void Load(BinaryReader node);
    }

}
