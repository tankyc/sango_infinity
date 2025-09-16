using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Sango.Game
{
    public static class DataLoader
    {
        //internal static readonly System.Type[] ConvertTypes = new System.Type[12]
        //{
        //    typeof(bool),
        //    typeof(sbyte),
        //    typeof(byte),
        //    typeof(short),
        //    typeof(ushort),
        //    typeof(int),
        //    typeof(uint),
        //    typeof(long),
        //    typeof(ulong),
        //    typeof(float),
        //    typeof(double),
        //    typeof(string)
        //};

        //public static T Read<T>(BinaryReader reader) where T : IConvertible
        //{
        //    Type tType = typeof(T);
        //    if (tType == ConvertTypes[1])
        //    {
        //        return (T)reader.ReadBoolean();
        //    }
        //    else if (tType == ConvertTypes[2])
        //    {
        //        return reader.ReadBoolean();
        //    }
        //    else if (tType == ConvertTypes[3])
        //    {
        //        byte result;
        //        if (byte.TryParse(value, out result))
        //            return result;
        //        return default(byte);
        //    }
        //    else if (tType == ConvertTypes[4])
        //    {
        //        short result;
        //        if (short.TryParse(value, out result))
        //            return result;
        //        return default(short);
        //    }
        //    else if (tType == ConvertTypes[5])
        //    {
        //        ushort result;
        //        if (ushort.TryParse(value, out result))
        //            return result;
        //        return default(ushort);
        //    }
        //    else if (tType == ConvertTypes[6])
        //    {
        //        int result;
        //        if (int.TryParse(value, out result))
        //            return result;
        //        return default(int);
        //    }
        //    else if (tType == ConvertTypes[7])
        //    {
        //        uint result;
        //        if (uint.TryParse(value, out result))
        //            return result;
        //        return default(uint);
        //    }
        //    else if (tType == ConvertTypes[8])
        //    {
        //        long result;
        //        if (long.TryParse(value, out result))
        //            return result;
        //        return default(long);
        //    }
        //    else if (tType == ConvertTypes[9])
        //    {
        //        ulong result;
        //        if (ulong.TryParse(value, out result))
        //            return result;
        //        return default(ulong);
        //    }
        //    else if (tType == ConvertTypes[10])
        //    {
        //        float result;
        //        if (float.TryParse(value, out result))
        //            return result;
        //        return default(float);
        //    }
        //    else if (tType == ConvertTypes[11])
        //    {
        //        double result;
        //        if (double.TryParse(value, out result))
        //            return result;
        //        return default(double);
        //    }
        //    else if (tType == ConvertTypes[12])
        //    {
        //        return value;
        //    }
        //    return default(T);
        //}

        #region 基本数据处理
        /// <summary>
        /// this is to slow
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="node"></param>
        /// <returns></returns>
        public static T TryParse<T>(string value) where T : IConvertible
        {
            Type tType = typeof(T);
            // 使用Convert.ChangeType尝试转换
            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Sango.Log.Error(e);
                return default;
            }
        }

        #region Xml

        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name)
        {
            System.Xml.XmlDocument doc = node.OwnerDocument;
            if (doc == null)
                doc = node as System.Xml.XmlDocument;
            System.Xml.XmlElement cotentNode = doc.CreateElement(name);
            node.AppendChild(cotentNode);
            return cotentNode;
        }

        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name, string value)
        {
            System.Xml.XmlElement cotentNode = node.OwnerDocument.CreateElement(name);
            //if (string.IsNullOrEmpty(value))
            cotentNode.InnerText = value;
            //}
            node.AppendChild(cotentNode);
            return cotentNode;
        }

        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name, IDataObject value)
        {
            if (value == null) return null;
            System.Xml.XmlElement cotentNode = node.OwnerDocument.CreateElement(name);
            value.Load(cotentNode);
            node.AppendChild(cotentNode);
            return cotentNode;
        }

        #region single
        public static void Save(bool o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(sbyte o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(byte o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(short o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ushort o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(int o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(uint o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(long o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ulong o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(float o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(double o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(string o, XmlNode node)
        {
            node.InnerText = o;
        }
        public static void Save(IDataObject o, XmlNode node)
        {
            o.Save(node);
        }
        public static T TryParse<T>(XmlNode node) where T : IConvertible
        {
            Type tType = typeof(T);
            // 使用Convert.ChangeType尝试转换
            try
            {
                return (T)Convert.ChangeType(node.InnerText, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Sango.Log.Error(e);
                return default;
            }
        }
        public static bool Load(bool defaultValue, XmlNode node)
        {
            bool o;
            if (!bool.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static sbyte Load(sbyte defaultValue, XmlNode node)
        {
            sbyte o;
            if (!sbyte.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static byte Load(byte defaultValue, XmlNode node)
        {
            byte o;
            if (!byte.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("byte 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static short Load(short defaultValue, XmlNode node)
        {
            short o;
            if (!short.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("short 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static ushort Load(ushort defaultValue, XmlNode node)
        {
            ushort o;
            if (!ushort.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("ushort 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static int Load(int defaultValue, XmlNode node)
        {
            int o;
            if (!int.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("int 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static uint Load(uint defaultValue, XmlNode node)
        {
            uint o;
            if (!uint.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("uint 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static long Load(long defaultValue, XmlNode node)
        {
            long o;
            if (!long.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("long 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static float Load(float defaultValue, XmlNode node)
        {
            float o;
            if (!float.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("float 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static ulong Load(ulong defaultValue, XmlNode node)
        {
            ulong o;
            if (!ulong.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("ulong 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static double Load(double defaultValue, XmlNode node)
        {
            double o;
            if (!double.TryParse(node.InnerText, out o))
            {
                Sango.Log.Error("double 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static string Load(string defaultValue, XmlNode node)
        {
            return node.InnerText;
        }
        #endregion single

        #region List
        public static void Save<T>(IList<T> o, XmlNode node) where T : IDataObject, new()
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                XmlNode itemNode = AddNode(node, "Item");
                Save(o[i], itemNode);
            }
        }

        public static void Save(IList<string> o, XmlNode node)
        {
            SaveArray<string>(o, node);

        }
        public static void Save(IList<byte> o, XmlNode node)
        {
            SaveArray<byte>(o, node);

        }
        public static void Save(IList<sbyte> o, XmlNode node)
        {
            SaveArray<sbyte>(o, node);

        }
        public static void Save(IList<short> o, XmlNode node)
        {
            SaveArray<short>(o, node);

        }
        public static void Save(IList<ushort> o, XmlNode node)
        {
            SaveArray<ushort>(o, node);

        }
        public static void Save(IList<int> o, XmlNode node)
        {
            SaveArray<int>(o, node);

        }
        public static void Save(IList<uint> o, XmlNode node)
        {
            SaveArray<uint>(o, node);

        }
        public static void Save(IList<long> o, XmlNode node)
        {
            SaveArray<long>(o, node);

        }
        public static void Save(IList<ulong> o, XmlNode node)
        {
            SaveArray<ulong>(o, node);

        }
        public static void Save(IList<bool> o, XmlNode node)
        {
            SaveArray<bool>(o, node);

        }
        public static void Save(IList<float> o, XmlNode node)
        {
            SaveArray<float>(o, node);

        }
        public static void Save(IList<double> o, XmlNode node)
        {
            SaveArray<double>(o, node);
        }

        public static void Load<T>(IList<T> o, XmlNode node) where T : IDataObject, new()
        {
            int count = node.ChildNodes.Count;
            for (int i = 0; i < count; ++i)
            {
                XmlNode itemNode = node.ChildNodes[i];
                T t = new T();
                t.Load(itemNode);
                o.Add(t);
            }
        }

        public static void Load(IList<string> o, XmlNode node)
        {
            int count = node.ChildNodes.Count;
            for (int i = 0; i < count; ++i)
            {
                XmlNode itemNode = node.ChildNodes[i];
                o.Add(Load("", itemNode));
            }
        }
        public static void Load(IList<byte> o, XmlNode node)
        {
            LoadArray<byte>(o, node);

        }
        public static void Load(IList<sbyte> o, XmlNode node)
        {
            LoadArray<sbyte>(o, node);

        }
        public static void Load(IList<short> o, XmlNode node)
        {
            LoadArray<short>(o, node);

        }
        public static void Load(IList<ushort> o, XmlNode node)
        {
            LoadArray<ushort>(o, node);

        }
        public static void Load(IList<int> o, XmlNode node)
        {
            LoadArray<int>(o, node);

        }
        public static void Load(IList<uint> o, XmlNode node)
        {
            LoadArray<uint>(o, node);
        }
        public static void Load(IList<long> o, XmlNode node)
        {
            LoadArray<long>(o, node);
        }
        public static void Load(IList<ulong> o, XmlNode node)
        {
            LoadArray<ulong>(o, node);
        }
        public static void Load(IList<bool> o, XmlNode node)
        {
            LoadArray<bool>(o, node);
        }
        public static void Load(IList<float> o, XmlNode node)
        {
            LoadArray<float>(o, node);
        }
        public static void Load(IList<double> o, XmlNode node)
        {
            LoadArray<double>(o, node);
        }

        #endregion List


        public static void LoadArray<T>(IList<T> list, XmlNode node) where T : IConvertible
        {
            string[] strings = node.InnerText.Split(',');
            for (int i = 0; i < strings.Length; ++i)
            {
                list.Add(TryParse<T>(strings[i]));
            }
        }

        public static T[] LoadArray<T>(XmlNode node) where T : IConvertible
        {
            string[] strings = node.InnerText.Split(',');
            T[] values = new T[strings.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] = TryParse<T>(strings[i]);
            }
            return values;
        }

        public static void SaveArray<T>(IList<T> list, XmlNode node) where T : IConvertible
        {
            if (list == null) return;
            int count = list.Count;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0, len = count; i < len; i++)
            {
                sb.Append(list[i].ToString());
                if (i < len - 1)
                    sb.Append(',');
            }
            node.InnerText = sb.ToString();
        }

        public static void SaveArray<T>(T[] o, XmlNode node) where T : IConvertible
        {
            if (o == null) return;
            int count = o.Length;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0, len = count; i < len; i++)
            {
                sb.Append(o[i].ToString());
                if (i < len - 1)
                    sb.Append(',');
            }
            node.InnerText = sb.ToString();
        }

        public static T[] LoadArray<T>(SimpleJSON.JSONNode node) where T : IConvertible
        {
            string[] strings = node.Value.Split(',');
            T[] values = new T[strings.Length];
            for (int i = 0; i < values.Length; ++i)
            {
                values[i] = TryParse<T>(strings[i]);
            }
            return values;
        }

        public static void SaveArray<T>(T[] o, SimpleJSON.JSONNode node) where T : IConvertible
        {
            if (o == null) return;
            int count = o.Length;

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0, len = count; i < len; i++)
            {
                sb.Append(o[i].ToString());
                if (i < len - 1)
                    sb.Append(',');
            }
            node.Value = sb.ToString();
        }

        #endregion Xml
        #region Json
        public static T Load<T>(SimpleJSON.JSONNode node) where T : struct
        {
            // 使用Convert.ChangeType尝试转换
            try
            {
                return (T)Convert.ChangeType(node.Value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                Sango.Log.Error(e);
                return default;
            }
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node)
        {
            SimpleJSON.JSONClass classNode = new SimpleJSON.JSONClass();
            node.Add(classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddArrayNode(SimpleJSON.JSONNode node, string name)
        {
            SimpleJSON.JSONArray classNode = new SimpleJSON.JSONArray();
            node.Add(name, classNode);
            return classNode;
        }

        #region single

        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name)
        {
            SimpleJSON.JSONClass classNode = new SimpleJSON.JSONClass();
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, string value)
        {
            if (value == null) return null;
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, bool value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, byte value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, sbyte value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, short value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, ushort value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, int value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, uint value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, float value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, double value)
        {
            SimpleJSON.JSONData classNode = new SimpleJSON.JSONData(value);
            node.Add(name, classNode);
            return classNode;
        }
        public static SimpleJSON.JSONNode AddNode(SimpleJSON.JSONNode node, string name, IDataObject value)
        {
            if (value == null) return null;
            SimpleJSON.JSONClass classNode = new SimpleJSON.JSONClass();
            node.Add(name, classNode);
            value.Save(classNode);
            return classNode;
        }

        public static bool Load(bool defaultValue, SimpleJSON.JSONNode node)
        {
            bool o;
            if (!bool.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static sbyte Load(sbyte defaultValue, SimpleJSON.JSONNode node)
        {
            sbyte o;
            if (!sbyte.TryParse(node.Value, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static byte Load(byte defaultValue, SimpleJSON.JSONNode node)
        {
            byte o;
            if (!byte.TryParse(node.Value, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static short Load(short defaultValue, SimpleJSON.JSONNode node)
        {
            short o;
            if (!short.TryParse(node.Value, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static ushort Load(ushort defaultValue, SimpleJSON.JSONNode node)
        {
            ushort o;
            if (!ushort.TryParse(node.Value, out o))
            {
                Sango.Log.Error("bool 序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static int Load(int defaultValue, SimpleJSON.JSONNode node)
        {
            int o;
            if (!int.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static uint Load(uint defaultValue, SimpleJSON.JSONNode node)
        {
            uint o;
            if (!uint.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static long Load(long defaultValue, SimpleJSON.JSONNode node)
        {
            long o;
            if (!long.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static ulong Load(ulong defaultValue, SimpleJSON.JSONNode node)
        {
            ulong o;
            if (!ulong.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static float Load(float defaultValue, SimpleJSON.JSONNode node)
        {
            float o;
            if (!float.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static double Load(double defaultValue, SimpleJSON.JSONNode node)
        {
            double o;
            if (!double.TryParse(node.Value, out o))
            {
                Sango.Log.Error("序列化失败");
                return defaultValue;
            }
            return o;
        }
        public static string Load(string defaultValue, SimpleJSON.JSONNode node)
        {
            return node.Value;
        }

        public static void Save(bool o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(sbyte o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(byte o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(short o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(ushort o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(int o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(uint o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(long o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(ulong o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(float o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(double o, SimpleJSON.JSONNode node)
        {
            node.Value = o.ToString();
        }
        public static void Save(string o, SimpleJSON.JSONNode node)
        {
            node.Value = o;
        }
        public static void Save(IDataObject o, SimpleJSON.JSONNode node)
        {
            o.Save(node);
        }
        #endregion sigle
        #region List
        public static SimpleJSON.JSONNode AddNode<T>(SimpleJSON.JSONNode node, string name, IList<T> value) where T : IDataObject, new()
        {
            if (value == null) return null;
            int count = value.Count;
            SimpleJSON.JSONNode classNode = AddArrayNode(node, name);
            for (int i = 0; i < count; ++i)
            {
                value[i].Save(Sango.Game.DataLoader.AddNode(classNode));
            }
            return classNode;
        }

        public static void Save<T>(IList<T> o, SimpleJSON.JSONNode node) where T : IDataObject, new()
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                o[i].Save(Sango.Game.DataLoader.AddNode(node));
            }
        }

        public static void Save(IList<string> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<byte> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<sbyte> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<short> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<ushort> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<int> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<uint> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<long> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<ulong> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<bool> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<float> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }
        public static void Save(IList<double> o, SimpleJSON.JSONNode node)
        {
            if (o == null) return;
            int count = o.Count;
            for (int i = 0; i < count; ++i)
            {
                Save(o[i], Sango.Game.DataLoader.AddNode(node));
            }
        }

        public static void Load<T>(IList<T> o, SimpleJSON.JSONNode node) where T : IDataObject, new()
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                T t = new T();
                t.Load(itemNode);
                o.Add(t);
            }
        }

        public static void Load(IList<string> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load("", itemNode));
            }
        }
        public static void Load(IList<byte> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((byte)0, itemNode));
            }
        }
        public static void Load(IList<sbyte> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((sbyte)0, itemNode));
            }
        }
        public static void Load(IList<short> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((short)0, itemNode));
            }
        }
        public static void Load(IList<ushort> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((ushort)0, itemNode));
            }
        }
        public static void Load(IList<int> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((int)0, itemNode));
            }
        }
        public static void Load(IList<uint> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((uint)0, itemNode));
            }
        }
        public static void Load(IList<long> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((long)0, itemNode));
            }
        }
        public static void Load(IList<ulong> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((ulong)0, itemNode));
            }
        }
        public static void Load(IList<bool> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load(false, itemNode));
            }
        }
        public static void Load(IList<float> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((float)0, itemNode));
            }
        }
        public static void Load(IList<double> o, SimpleJSON.JSONNode node)
        {
            int count = node.Count;
            for (int i = 0; i < count; ++i)
            {
                SimpleJSON.JSONNode itemNode = node[i];
                o.Add(Load((double)0, itemNode));
            }
        }
        #endregion List

        #endregion Json
        #region Binary
        public static bool Load(bool defaultValue, BinaryReader node)
        {
            return node.ReadBoolean();
        }
        public static sbyte Load(sbyte defaultValue, BinaryReader node)
        {
            return node.ReadSByte();
        }
        public static byte Load(byte defaultValue, BinaryReader node)
        {
            return node.ReadByte();
        }
        public static short Load(short defaultValue, BinaryReader node)
        {
            return node.ReadInt16();
        }
        public static ushort Load(ushort defaultValue, BinaryReader node)
        {
            return node.ReadUInt16();
        }
        public static int Load(int defaultValue, BinaryReader node)
        {
            return node.ReadInt32();
        }
        public static uint Load(uint defaultValue, BinaryReader node)
        {
            return node.ReadUInt32();

        }
        public static long Load(long defaultValue, BinaryReader node)
        {
            return node.ReadInt64();

        }
        public static ulong Load(ulong defaultValue, BinaryReader node)
        {
            return node.ReadUInt64();

        }
        public static float Load(float defaultValue, BinaryReader node)
        {
            return node.ReadSingle();
        }
        public static double Load(double defaultValue, BinaryReader node)
        {
            return node.ReadDouble();
        }
        public static string Load(string defaultValue, BinaryReader node)
        {
            return node.ReadString();
        }
        public static void Load(IDataObject o, BinaryReader node)
        {
            o.Load(node);
        }
        public static void Save(bool o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(sbyte o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(byte o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(short o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(ushort o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(int o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(uint o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(long o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(ulong o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(float o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(double o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(string o, BinaryWriter node)
        {
            node.Write(o);
        }
        public static void Save(IDataObject o, BinaryWriter node)
        {
            o.Save(node);
        }
        #endregion Binary

        #endregion 基本数据处理


        #region 额外没有继承DataObject数据处理
        #region UnityEngine.Vector2Int
        public static void Save(UnityEngine.Vector2Int o, System.Xml.XmlNode node)
        {
            node.InnerText = $"{o.x},{o.y}";
        }
        public static void Load(ref UnityEngine.Vector2Int o, System.Xml.XmlNode node)
        {
            string data = node.InnerText;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            int x = int.Parse(values[0]);
            int y = int.Parse(values[1]);
            o.x = x; o.y = y;
        }
        public static void Save(UnityEngine.Vector2Int o, SimpleJSON.JSONNode node)
        {
            node.Value = $"{o.x},{o.y}";
        }
        public static void Load(ref UnityEngine.Vector2Int o, SimpleJSON.JSONNode node)
        {
            string data = node.Value;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            int x = int.Parse(values[0]);
            int y = int.Parse(values[1]);
            o.x = x; o.y = y;
        }
        public static void Save(UnityEngine.Vector2Int o, BinaryWriter node)
        {
            node.Write(o.x);
            node.Write(o.y);
        }
        public static void Load(ref UnityEngine.Vector2Int o, BinaryReader node)
        {
            o.x = node.ReadInt32();
            o.y = node.ReadInt32();
        }
        #endregion UnityEngine.Vector2Int
        #region UnityEngine.Bounds
        public static void Save(UnityEngine.Bounds o, System.Xml.XmlNode node)
        {
            node.InnerText = $"{o.center.x},{o.center.y},{o.center.z},{o.size.x},{o.size.y},{o.size.z}";
        }
        public static void Load(ref UnityEngine.Bounds o, System.Xml.XmlNode node)
        {
            string data = node.InnerText;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            float x = float.Parse(values[0]);
            float y = float.Parse(values[1]);
            float z = float.Parse(values[2]);
            o.center = new Vector3(x, y, z);
            x = float.Parse(values[3]);
            y = float.Parse(values[4]);
            z = float.Parse(values[5]);
            o.size = new Vector3(x, y, z);
        }
        public static void Save(UnityEngine.Bounds o, SimpleJSON.JSONNode node)
        {
            node.Value = $"{o.center.x},{o.center.y},{o.center.z},{o.size.x},{o.size.y},{o.size.z}";
        }
        public static void Load(ref UnityEngine.Bounds o, SimpleJSON.JSONNode node)
        {
            string data = node.Value;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            float x = float.Parse(values[0]);
            float y = float.Parse(values[1]);
            float z = float.Parse(values[2]);
            o.center = new Vector3(x, y, z);
            x = float.Parse(values[3]);
            y = float.Parse(values[4]);
            z = float.Parse(values[5]);
            o.size = new Vector3(x, y, z);
        }
        public static void Save(UnityEngine.Bounds o, BinaryWriter node)
        {
            node.Write(o.center.x);
            node.Write(o.center.y);
            node.Write(o.center.z);
            node.Write(o.size.x);
            node.Write(o.size.y);
            node.Write(o.size.z);
        }
        public static void Load(ref UnityEngine.Bounds o, BinaryReader node)
        {
            float x = node.ReadSingle();
            float y = node.ReadSingle();
            float z = node.ReadSingle();
            o.center = new Vector3(x, y, z);
            x = node.ReadSingle();
            y = node.ReadSingle();
            z = node.ReadSingle();
            o.size = new Vector3(x, y, z);
        }
        #endregion UnityEngine.Bounds

        #region Sango.Tools.Rect
        public static void Save(Sango.Tools.Rect o, System.Xml.XmlNode node)
        {
            node.InnerText = $"{o.center.x},{o.center.y},{o.size.x},{o.size.y}";
        }
        public static void Load(ref Sango.Tools.Rect o, System.Xml.XmlNode node)
        {
            string data = node.InnerText;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            float x = float.Parse(values[0]);
            float y = float.Parse(values[1]);
            o.center = new Vector2(x, y);
            x = float.Parse(values[2]);
            y = float.Parse(values[3]);
            o.size = new Vector2(x, y);
        }
        public static void Save(Sango.Tools.Rect o, SimpleJSON.JSONNode node)
        {
            node.Value = $"{o.center.x},{o.center.y},{o.size.x},{o.size.y}";
        }
        public static void Load(ref Sango.Tools.Rect o, SimpleJSON.JSONNode node)
        {
            string data = node.Value;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            float x = float.Parse(values[0]);
            float y = float.Parse(values[1]);
            o.center = new Vector2(x, y);
            x = float.Parse(values[2]);
            y = float.Parse(values[3]);
            o.size = new Vector2(x, y);
        }
        public static void Save(Sango.Tools.Rect o, BinaryWriter node)
        {
            node.Write(o.center.x);
            node.Write(o.center.y);
            node.Write(o.size.x);
            node.Write(o.size.y);
        }
        public static void Load(ref Sango.Tools.Rect o, BinaryReader node)
        {
            float x = node.ReadSingle();
            float y = node.ReadSingle();
            o.center = new Vector2(x, y);
            x = node.ReadSingle();
            y = node.ReadSingle();
            o.size = new Vector2(x, y);
        }
        #endregion Sango.Tools.Rect
        #region UnityEngine.Color32
        public static void Save(UnityEngine.Color32 o, SimpleJSON.JSONNode node)
        {
            node.Value = $"{o.r},{o.g},{o.b},{o.a}";
        }
        public static void Load(ref UnityEngine.Color32 o, SimpleJSON.JSONNode node)
        {
            string data = node.Value;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });
            byte r = byte.Parse(values[0]);
            byte g = byte.Parse(values[1]);
            byte b = byte.Parse(values[2]);
            byte a = 255;
            if (values.Length > 3)
                a = byte.Parse(values[3]);
            o.r = r; o.g = g; o.b = b; o.a = a;
        }
        public static void Save(UnityEngine.Color32 o, BinaryWriter node)
        {
            node.Write(o.r);
            node.Write(o.g);
            node.Write(o.b);
            node.Write(o.a);
        }
        public static void Load(ref UnityEngine.Color32 o, BinaryReader node)
        {
            o.r = node.ReadByte();
            o.g = node.ReadByte();
            o.b = node.ReadByte();
            o.a = node.ReadByte();
        }


        public static void Save(UnityEngine.Color32 o, System.Xml.XmlNode node)
        {
            node.Value = $"{o.r},{o.g},{o.b},{o.a}";
        }

        public static void Load(ref UnityEngine.Color32 o, System.Xml.XmlNode node)
        {
            string data = node.InnerText;
            if (string.IsNullOrEmpty(data))
                return;

            string[] values = data.Split(new char[] { ',' });

            byte r;
            if (!byte.TryParse(values[0], out r))
            {
                Debug.LogError(values[0]);
            }
            byte g = byte.Parse(values[1]);
            byte b = byte.Parse(values[2]);
            byte a = 255;
            if (values.Length > 3)
                a = byte.Parse(values[3]);
            o.r = r; o.g = g; o.b = b; o.a = a;
        }
        #endregion UnityEngine.Color32


        #endregion 额外没有继承DataObject数据处理
    }
}
