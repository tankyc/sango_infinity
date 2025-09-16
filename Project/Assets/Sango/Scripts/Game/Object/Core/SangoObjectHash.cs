//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.IO;
//using Newtonsoft.Json;
//using System.Xml;

//namespace Sango.Game
//{
//    [JsonObject(MemberSerialization.OptIn)]
//    public class SangoObjectHash<T> : Database<T> where T : SangoObject, new()
//    {
//        public List<T> objects = new List<T>();
//        public Hashtable hash = new Hashtable();
//        public override int Length { get { return objects.Count; } }
//        public SangoObjectHash()
//        {

//        }
//        public override void Clear()
//        {
//            objects.Clear();
//            hash.Clear();
//        }
//        public override void Reset(int length)
//        {
//            objects.Clear();
//            hash.Clear();
//        }
//        public override void Add(T obj)
//        {
//            objects.Add(obj);
//            hash[obj.Id] = obj;
//        }
//        public override void Remove(T obj)
//        {
//            objects.Remove(obj);
//            hash.Remove(obj.Id);
//        }
//        public override T Get(int id)
//        {
//            return hash[id] as T; 
//        }

//        public override void ForEach(Serialization.Action<T> action)
//        {
//            for (int i = 0; i < objects.Count; i++)
//            {
//                T obj = objects[i];
//                if (obj != null)
//                    action(obj);
//            }
//        }

//        public override T Find(Predicate<T> match)
//        {
//            return objects.Find(match);
//        }

//        public override List<T> FindAll(Predicate<T> match)
//        {
//            return objects.FindAll(match);
//        }
       
//        public override void Load(BinaryReader reader)
//        {
//            int length = reader.ReadInt32();
//            Reset(0);
//            for (int i = 0; i < length; i++)
//            {
//                T t = new T();
//                t.Load(reader);
//                Add(t);
//            }
//        }
//        public override void Save(BinaryWriter writer)
//        {
//            writer.Write(Length);
//            for (int i = 0; i < Length; i++)
//            {
//                T t = objects[i];
//                if (t != null)
//                    t.Save(writer);
//            }
//        }
//    }
//}
