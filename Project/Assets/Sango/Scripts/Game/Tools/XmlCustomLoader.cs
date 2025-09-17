using UnityEngine;

public static class XmlLoader
{

    //public static void Save(this Sango.Game.CellSet o, System.Xml.XmlNode parent, string value_name)
    //{
    //    if (o == null) return;
    //    AddNode(parent, value_name, o.ToString());
    //}

    //public static void Load(this Sango.Game.CellSet o, System.Xml.XmlNode node)
    //{
    //    o.FromString(node.InnerText);
    //}
    //public static void Save(this Sango.Game.AttributeChangeType o, System.Xml.XmlNode parent, string value_name)
    //{
    //    if (o == null) return;
    //    System.Xml.XmlNode node = AddNode(parent, value_name);
    //    Save(o.Id, node, "Id");
    //    Save(o.Name, node, "Name");
    //    Save(o.ToString(), node, "Value");
    //}

    //public static void Load(this Sango.Game.AttributeChangeType o, System.Xml.XmlNode node)
    //{
    //    int count = node.ChildNodes.Count;
    //    for (int i = 0; i < count; ++i)
    //    {
    //        System.Xml.XmlNode attr = node.ChildNodes[i];
    //        switch (attr.Name)
    //        {
    //            case "Id":
    //                var vId = o.Id;
    //                o.Id = vId;
    //                break;
    //            case "Name":
    //                var vName = o.Name;
    //                o.Name = vName;
    //                break;
    //            case "Value":
    //                //o.FromString(attr.InnerText);
    //                break;
    //        }
    //    }
    //}

}