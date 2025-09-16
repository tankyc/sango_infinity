namespace Sango.Game
{
    public abstract class DataObject : IDataObject
    {
        public virtual void Load(System.IO.BinaryReader node) {; }
        public virtual void Save(System.IO.BinaryWriter node) {; }
        public virtual void Save(System.Xml.XmlNode node) {; }
        public virtual void Load(System.Xml.XmlNode node) {; }
        public virtual void Save(SimpleJSON.JSONNode node) {; }
        public virtual void Load(SimpleJSON.JSONNode node) {; }
    }
}
