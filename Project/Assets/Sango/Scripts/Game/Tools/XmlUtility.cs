using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;



namespace Sango.Game
{
    public class XmlUtility
    {
        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name)
        {
            System.Xml.XmlElement cotentNode = node.OwnerDocument.CreateElement(name);
            node.AppendChild(cotentNode);
            return cotentNode;
        }

        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name, string value)
        {
            System.Xml.XmlElement cotentNode = node.OwnerDocument.CreateElement(name);
            cotentNode.InnerText = value;
            node.AppendChild(cotentNode);
            return cotentNode;
        }

        #region 基本数据处理
        public static void Load(ref bool o, XmlNode node)
        {
            bool.TryParse(node.InnerText, out o);
        }
        public static void Load(ref sbyte o, XmlNode node)
        {
            sbyte.TryParse(node.InnerText, out o);
        }
        public static void Load(ref byte o, XmlNode node)
        {
            byte.TryParse(node.InnerText, out o);
        }
        public static void Load(ref short o, XmlNode node)
        {
            short.TryParse(node.InnerText, out o);
        }
        public static void Load(ref ushort o, XmlNode node)
        {
            ushort.TryParse(node.InnerText, out o);
        }
        public static void Load(ref int o, XmlNode node)
        {
            int.TryParse(node.InnerText, out o);
        }
        public static void Load(ref uint o, XmlNode node)
        {
            uint.TryParse(node.InnerText, out o);
        }
        public static void Load(ref long o, XmlNode node)
        {
            long.TryParse(node.InnerText, out o);
        }
        public static void Load(ref ulong o, XmlNode node)
        {
            ulong.TryParse(node.InnerText, out o);
        }
        public static void Load(ref float o, XmlNode node)
        {
            float.TryParse(node.InnerText, out o);
        }
        public static void Load(ref double o, XmlNode node)
        {
            double.TryParse(node.InnerText, out o);
        }
        public static void Load(ref string o, XmlNode node)
        {
            o = node.InnerText;
        }
        public static void Load(ref bool[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                bool.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref sbyte[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                sbyte.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref byte[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                byte.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref short[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                short.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref ushort[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                ushort.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref int[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                int.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref uint[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                uint.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref long[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                long.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref ulong[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                ulong.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref float[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                float.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref double[] o, XmlNode node)
        {
            if (o == null) return;
            string[] strings = node.InnerText.Split(',');
            int count = Math.Min(o.Length, strings.Length);
            for (int i = 0; i < count; ++i)
            {
                double.TryParse(strings[i], out o[i]);
            }
        }
        public static void Load(ref string[] o, XmlNode node)
        {
            int count = Math.Min(o.Length, node.ChildNodes.Count);
            for (int i = 0; i < count; ++i)
            {
                o[i] = node.ChildNodes[i].InnerText;
            }
        }
        public static void Save(ref bool o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref sbyte o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref byte o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref short o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref ushort o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref int o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref uint o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref long o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref ulong o, XmlNode node)
        {
            node.InnerText = o.ToString();
        }
        public static void Save(ref string o, XmlNode node)
        {
            node.InnerText = o;
        }
        public static void Save(ref bool[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref sbyte[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref byte[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref short[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref ushort[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref int[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref uint[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref long[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref ulong[] o, XmlNode node)
        {
            if (o == null) return;
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < o.Length; ++i)
            {
                sb.Append(o[i].ToString());
                if (i < o.Length - 1) sb.Append(",");
            }
            node.InnerText = sb.ToString();

        }
        public static void Save(ref string[] o, XmlNode node)
        {
            if (o == null) return;
            for (int i = 0; i < o.Length; ++i)
            {
                AddNode(node, "Item", o[i]);
            }
        }
        #endregion 基本数据处理

    }
}
