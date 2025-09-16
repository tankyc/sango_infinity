using SimpleJSON;
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    public class DataFactory : IDataFactory
    {
        public virtual IDataObject Create(BinaryReader node)
        {
            return null;
        }
        public virtual IDataObject Create(System.Xml.XmlNode node)
        {
            return null;
        }
        public virtual IDataObject Create(SimpleJSON.JSONNode node)
        {
            return null;
        }

        public virtual void Load(BinaryReader node) {; }
        public virtual void Save(BinaryWriter node) {; }
        public virtual void Save(System.Xml.XmlNode node) { }
        public virtual void Load(System.Xml.XmlNode node) { }
        public virtual void Save(SimpleJSON.JSONNode node) { }
        public virtual void Load(SimpleJSON.JSONNode node) { }
    }
}
