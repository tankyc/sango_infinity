using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEngine;

namespace Sango.Data
{
    public interface StringDataBase
    {
        bool TryParse(string s);
    }

    public class TxtLoad
    {
        public static T[] LoadTxt<T>(string path, bool firstIsDesc = true) where T : StringDataBase, new()
        {
            Stream stream = System.IO.File.OpenRead(path);  // 打开位图文件
            StreamReader reader = new StreamReader(stream);
            List<T> list = new List<T>();
            if (firstIsDesc)
                reader.ReadLine();
            while (!reader.EndOfStream)
            {
                string s = reader.ReadLine();
                T t = new T();
                if (t.TryParse(s))
                {
                    list.Add(t);
                }
                else
                    break;
            }
            reader.Close();
            stream.Close();

            return list.ToArray();
        }

    }

    public class Model : StringDataBase
    {
        public int id;
        public string name;
        public int type;
        public Vector2Int coords;
        public Vector2Int cell;
        public int height;
        public float dir;
        public bool valid;

        public bool TryParse(string s)
        {
            string[] str = s.Split('\t');
            int.TryParse(str[0], out id);

            valid = (str[1] == "○");
            //if (valid)
            //    Debug.Log(id.ToString());

            int.TryParse(str[2], out type);
            name = str[3];
            int coords_x;
            int coords_y;
            int.TryParse(str[4], out coords_x);
            int.TryParse(str[5], out coords_y);
            coords = new Vector2Int(coords_x, coords_y);

            string[] cellStr = str[6].Split(',');
            int cell_x;
            int cell_y;
            int.TryParse(cellStr[0], out cell_x);
            int.TryParse(cellStr[1], out cell_y);
            cell = new Vector2Int(cell_x, cell_y);

            height = int.Parse(str[7]);
            dir = float.Parse(str[8]);
            return valid;
        }
    }

    public interface XmlDataBase
    {
        bool TryParse(XmlElement node);
    }

    public class XmlLoad
    {
        public static T[] LoadXml<T>(string path, string elementName) where T : XmlDataBase, new()
        {
            Stream stream = System.IO.File.OpenRead(path);  // 打开位图文件
            XmlDocument xml = new XmlDocument();
            xml.Load(stream);
            XmlElement node = xml.DocumentElement[elementName];
            List<T> list = new List<T>();
            while (node != null)
            {
                T t = new T();
                if (t.TryParse(node))
                {
                    list.Add(t);
                }
                else
                    break;

                node = node.NextSibling as XmlElement;
            }
            stream.Close();
            return list.ToArray();
        }

    }

    public class MapTerrainType : XmlDataBase
    {
        public string typeName;
        public int typeIndex;
        public bool fireable;
        public bool movable;
        public bool ambush;

        public bool TryParse(XmlElement node)
        {
            //int.TryParse(node.GetAttribute("id"), out typeIndex);
            //typeName = node["name"].GetAttribute("value");
            //int.TryParse(node.GetAttribute("id"), out typeIndex);
            return true;
        }
    }

    public class MapBlock : StringDataBase
    {
        public int terrainType;         // 地形类型
        public Vector2Int coords;       // 地形坐标
        public int areaId;              // 地形区域ID
        public bool canBuild;           // 是否可以内政建设 

        public bool TryParse(string s)
        {
            //string[] str = s.Split('\t');
            //int.TryParse(str[0], out id);

            //valid = (str[1] == "○");
            //if (valid)
            //    Debug.Log(id.ToString());

            //int.TryParse(str[2], out type);
            //name = str[3];
            //int coords_x;
            //int coords_y;
            //int.TryParse(str[4], out coords_x);
            //int.TryParse(str[5], out coords_y);
            //coords = new Vector2Int(coords_x, coords_y);

            //string[] cellStr = str[6].Split(',');
            //int cell_x;
            //int cell_y;
            //int.TryParse(cellStr[0], out cell_x);
            //int.TryParse(cellStr[1], out cell_y);
            //cell = new Vector2Int(cell_x, cell_y);

            //height = int.Parse(str[7]);
            //dir = float.Parse(str[8]);
            return true;
        }
    }
}

