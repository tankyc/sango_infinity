using System.IO;

namespace Sango.Game
{
    public interface ILinkObject { }

    public class LinkObjectSet<T, T1> where T : ScenarioObject<T1>, new() where T1 : SangoObject, new()
    {
        private T[] values;

        public LinkObjectSet()
        {
            Init(256);
        }

        public LinkObjectSet(int length)
        {
            Init(length);
        }

        void Init(int length)
        {
            if (length < 0)
                length = 0;
            values = new T[length];
            for (int i = 0; i < length; i++)
                values[i] = new T();
        }

        public bool Has(int id)
        {
            return values[id].Id >= 0;
        }

        public T1 Get(int id)
        {
            return values[id].Object;
        }

        //public override void LoadFromStream(BinaryReader reader)
        //{
        //    int count = reader.ReadInt32();
        //    for (int i = 0; i < count; i++)
        //    {
        //        int id = reader.ReadInt32();
        //        values[id].ID = id;
        //    }
        //}
        //public override void SaveToStream(BinaryWriter writer)
        //{
        //    int count = 0;
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //        if (values[i].ID >= 0)
        //            count++;
        //    }
        //    writer.Write(count);
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //        T t = values[i];
        //        if (t.ID >= 0)
        //        {
        //            t.SaveToStream(writer);
        //        }
        //    }
        //}

    }
}
