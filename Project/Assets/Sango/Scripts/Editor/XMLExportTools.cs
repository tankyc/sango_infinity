using Sango;
using Sango.Game;
using Sango.Game.Condition;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;

public static class XMLExportTools
{
    static Type DataInterfaceType = typeof(IDataObject);
    static Type DataFactoryInterfaceType = typeof(IDataFactory);
    
    const string classStr =
@"namespace {1}
{{
    public class {0}
    {{
        
        public override void Save(System.Xml.XmlNode node) 
        {{ 
{2}
        }}

        public override void Load(System.Xml.XmlNode node) 
        {{ 
{3}
        }}

        public override void Save(SimpleJSON.JSONNode node) 
        {{ 
{4}
        }}

        public override void Load(SimpleJSON.JSONNode node) 
        {{ 
{5}
        }}
        
        public override void Save(System.IO.BinaryWriter node) 
        {{
{6}
        }}
        
        public override void Load(System.IO.BinaryReader node) 
        {{
{7}
        }}
    }}
}}
";


    private static List<Type> customGenericList = new List<Type>()
    {
        typeof(Database<>),
        typeof(ScenarioObject<>),

    };

    private static List<Type> valueTypeList = new List<Type>()
    {
        typeof(bool),
        typeof(sbyte),
        typeof(byte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(string)
    };

    private static List<Type> customTypeList = new List<Type>()
    {
        typeof(AttributeChangeType),
        typeof(CellSet),
        typeof(Vector2Int),
        typeof(Bounds),
        typeof(Sango.Tools.Rect),
        typeof(Color32),
        typeof(Condition)
    };

    static string loaderDir = UnityEngine.Application.dataPath + "/Sango/Loader";
    static string filePath = UnityEngine.Application.dataPath + "/Sango/Loader/{0}_Loader.cs";

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Sango/Data/清理")]
#endif
    static void ProcessXMLClean()
    {
        Directory.Delete(loaderDir);
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    private static List<Type> alreadyGenericList = new List<Type>();
    private static Queue<Type> needGenericList = new Queue<Type>();

    public static bool IsValueType(Type type)
    {
        return valueTypeList.Contains(type);
    }
    public static Type GetFirstGenericType(Type type)
    {
        if (type.IsGenericType) { return type; }
        Type baseType = type.BaseType;
        while (baseType != null)
        {
            if (baseType.IsGenericType)
            {
                return baseType;
            }
            baseType = baseType.BaseType;
        }
        return null;
    }
    public static bool CheckIsAlreadyGeneric(Type type)
    {
        if (customTypeList.Contains(type))
            return true;

        Type baseGenericType = GetFirstGenericType(type);
        if (baseGenericType != null)
        {
            Type genericType = baseGenericType.GetGenericTypeDefinition();
            for (int i = 0; i < customGenericList.Count; i++)
            {
                if (genericType == customGenericList[i])
                {
                    alreadyGenericList.Add(type);
                    return true;
                }
            }
        }
        return false;
    }

    static string GetGenericTypeFullName(Type piType)
    {
        if (piType.IsGenericType)
        {
            Type[] typeParameters = piType.GenericTypeArguments;
            string targetR = "`" + typeParameters.Length;
            if (typeParameters.Length > 0)
            {
                StringBuilder replaceType = new StringBuilder();
                replaceType.Append("<");
                for (int i = 0; i < typeParameters.Length; i++)
                {
                    Type itemType = typeParameters[i];


                    replaceType.Append(GetGenericTypeFullName(itemType));
                    if (i < typeParameters.Length - 1)
                        replaceType.Append(",");

                }
                replaceType.Append(">");

                return piType.FullName.Substring(0, piType.FullName.IndexOf('[')).Replace(targetR, replaceType.ToString()).Replace("+", ".");
            }
            else
            {
                return "";
            }
        }
        else
            return piType.FullName.Replace("+", ".");
    }

    static string GetGenericTypeName(Type piType)
    {
        if (piType.IsGenericType)
        {
            Type[] typeParameters = piType.GenericTypeArguments;
            string targetR = "`" + typeParameters.Length;
            if (typeParameters.Length > 0)
            {
                StringBuilder replaceType = new StringBuilder();
                replaceType.Append("<");
                for (int i = 0; i < typeParameters.Length; i++)
                {
                    Type itemType = typeParameters[i];


                    replaceType.Append(GetGenericTypeName(itemType));
                    if (i < typeParameters.Length - 1)
                        replaceType.Append(",");

                }
                replaceType.Append(">");

                return piType.Name.Substring(0, piType.Name.IndexOf('[')).Replace(targetR, replaceType.ToString()).Replace("+", ".");
            }
            else
            {
                return "";
            }
        }
        else
            return piType.Name.Replace("+", ".");
    }

    static string GenericArrayType(Type type, int depth = 0)
    {
        if (alreadyGenericList.Contains(type))
            return "";

        string depthStr = "   ";
        for (int i = 0; i < depth; i++)
            depthStr += "   ";
        if (type.IsArray)
        {
            string TypeFullName = type.FullName;
            Type elementType = type.GetElementType();
            needGenericList.Enqueue(elementType);
            if (elementType.IsValueType)
            {
                string elementTypeFullName = elementType.FullName;
                alreadyGenericList.Add(type);
                Log.Print($"CenericType : {TypeFullName}");
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"{depthStr}public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}    string [] vs = node.InnerText.Split(',');");
                stringBuilder.AppendLine($"{depthStr}    o = new {elementTypeFullName}[vs.Length];");
                stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < vs.Length; i++)");
                stringBuilder.AppendLine($"{depthStr}       {elementTypeFullName}.TryParse(vs[i], out o[i]);");
                stringBuilder.AppendLine($"{depthStr}}}");
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                stringBuilder.AppendLine($"{depthStr}     System.Text.StringBuilder sb = new System.Text.StringBuilder();");
                stringBuilder.AppendLine($"{depthStr}     for(int i = 0, len = o.Length; i < len; i++)");
                stringBuilder.AppendLine($"{depthStr}     {{");
                stringBuilder.AppendLine($"{depthStr}           sb.Append(o[i].ToString());");
                stringBuilder.AppendLine($"{depthStr}           if(i < len - 1)");
                stringBuilder.AppendLine($"{depthStr}               sb.Append(',');");
                stringBuilder.AppendLine($"{depthStr}     }}");
                stringBuilder.AppendLine($"{depthStr}     node.InnerText = sb.ToString();");
                stringBuilder.AppendLine($"{depthStr}}}");
                return stringBuilder.ToString();
            }
            else if (elementType == typeof(string))
            {
                string elementTypeFullName = elementType.FullName;
                alreadyGenericList.Add(type);
                Log.Print($"CenericType : {TypeFullName}");
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"{depthStr}public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}    int count = node.ChildNodes.Count;");
                stringBuilder.AppendLine($"{depthStr}    o = new {elementTypeFullName}[count];");
                stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < count; i++)");
                stringBuilder.AppendLine($"{depthStr}       o[i] = node.ChildNodes[i].InnerText;");
                stringBuilder.AppendLine($"{depthStr}}}");
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                stringBuilder.AppendLine($"{depthStr}     for (int i = 0; i < o.Length; ++i)");
                stringBuilder.AppendLine($"{depthStr}     {{");
                stringBuilder.AppendLine($"{depthStr}           AddNode(node, \"Item\", o[i]);");
                stringBuilder.AppendLine($"{depthStr}     }}");
                stringBuilder.AppendLine($"{depthStr}}}");
                return stringBuilder.ToString();
            }
            else
            {
                string elementTypeFullName = GetGenericTypeFullName(elementType);
                alreadyGenericList.Add(type);
                Log.Print($"CenericType : {TypeFullName}");
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine($"{depthStr}public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}    int count = node.ChildNodes.Count;");
                stringBuilder.AppendLine($"{depthStr}    o = new {elementTypeFullName}[count];");
                stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < count; i++)");
                stringBuilder.AppendLine($"{depthStr}    {{");
                stringBuilder.AppendLine($"{depthStr}        {elementTypeFullName} c = new {elementTypeFullName}();");
                stringBuilder.AppendLine($"{depthStr}         Load(ref c, node.ChildNodes[i]);");
                stringBuilder.AppendLine($"{depthStr}         o[i] = c;");
                stringBuilder.AppendLine($"{depthStr}    }}");
                stringBuilder.AppendLine($"{depthStr}}}");
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                stringBuilder.AppendLine($"{depthStr}     for (int i = 0; i < o.Length; ++i)");
                stringBuilder.AppendLine($"{depthStr}     {{");
                stringBuilder.AppendLine($"{depthStr}          {TypeFullName} c = o[i];");
                stringBuilder.AppendLine($"{depthStr}          if(c == null) continue;");
                stringBuilder.AppendLine($"{depthStr}          Save(c, node, \"Item\");");
                stringBuilder.AppendLine($"{depthStr}     }}");
                stringBuilder.AppendLine($"{depthStr}}}");
                return stringBuilder.ToString();
            }
        }
        else
        {
            if (type.IsGenericType)
            {
                Type genericType = type.GetGenericTypeDefinition();
                if (genericType == typeof(List<>))
                {
                    string TypeFullName = GetGenericTypeFullName(type);
                    Type[] typeParameters = type.GenericTypeArguments;
                    Type elementType = typeParameters[0];
                    needGenericList.Enqueue(elementType);
                    if (elementType.IsValueType || elementType == typeof(string))
                    {
                        string elementTypeFullName = elementType.FullName;
                        alreadyGenericList.Add(type);
                        Log.Print($"CenericType : {TypeFullName}");
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine($"{depthStr}public static void Load(this {TypeFullName} o, System.Xml.XmlNode node)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}    if(o == null) o = new {TypeFullName}();");
                        stringBuilder.AppendLine($"{depthStr}    string [] vs = node.InnerText.Split(',');");
                        stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < vs.Length; i++)");
                        stringBuilder.AppendLine($"{depthStr}    {{");
                        stringBuilder.AppendLine($"{depthStr}        {elementTypeFullName} e;");
                        stringBuilder.AppendLine($"{depthStr}        {elementTypeFullName}.TryParse(vs[i], out e);");
                        stringBuilder.AppendLine($"{depthStr}        o.Add(e);");
                        stringBuilder.AppendLine($"{depthStr}    }}");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        stringBuilder.AppendLine($"{depthStr}public static void Save(this {TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                        stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                        stringBuilder.AppendLine($"{depthStr}     System.Text.StringBuilder sb = new System.Text.StringBuilder();");
                        stringBuilder.AppendLine($"{depthStr}     for(int i = 0, len = o.Count; i < len; i++)");
                        stringBuilder.AppendLine($"{depthStr}     {{");
                        stringBuilder.AppendLine($"{depthStr}           sb.Append(o[i].ToString());");
                        stringBuilder.AppendLine($"{depthStr}           if(i < len - 1)");
                        stringBuilder.AppendLine($"{depthStr}               sb.Append(',');");
                        stringBuilder.AppendLine($"{depthStr}     }}");
                        stringBuilder.AppendLine($"{depthStr}     node.InnerText = sb.ToString();");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        return stringBuilder.ToString();
                    }
                    else if (elementType.BaseType != null && elementType.BaseType.GetGenericTypeDefinition() == typeof(ScenarioObject<>))
                    {
                        string elementTypeFullName = GetGenericTypeFullName(elementType);
                        alreadyGenericList.Add(type);
                        Log.Print($"CenericType : {TypeFullName}");
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine($"{depthStr}public static void Load(this {TypeFullName} o, System.Xml.XmlNode node)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}    if(o == null) o = new {TypeFullName}();");
                        stringBuilder.AppendLine($"{depthStr}    string [] vs = node.InnerText.Split(',');");
                        stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < vs.Length; i++)");
                        stringBuilder.AppendLine($"{depthStr}    {{");
                        stringBuilder.AppendLine($"{depthStr}        {elementTypeFullName} v = new {elementTypeFullName}();");
                        stringBuilder.AppendLine($"{depthStr}        int e;");
                        stringBuilder.AppendLine($"{depthStr}        int.TryParse(vs[i], out e);");
                        stringBuilder.AppendLine($"{depthStr}        v.ID = e;");
                        stringBuilder.AppendLine($"{depthStr}        o.Add(v);");
                        stringBuilder.AppendLine($"{depthStr}    }}");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        stringBuilder.AppendLine($"{depthStr}public static void Save(this {TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}     if(o == null) return;");
                        stringBuilder.AppendLine($"{depthStr}     if(o.Count == 0) return;");
                        stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                        stringBuilder.AppendLine($"{depthStr}     System.Text.StringBuilder sb = new System.Text.StringBuilder();");
                        stringBuilder.AppendLine($"{depthStr}     for(int i = 0, len = o.Count; i < len; i++)");
                        stringBuilder.AppendLine($"{depthStr}     {{");
                        stringBuilder.AppendLine($"{depthStr}           sb.Append(o[i].ToString());");
                        stringBuilder.AppendLine($"{depthStr}           if(i < len - 1)");
                        stringBuilder.AppendLine($"{depthStr}               sb.Append(',');");
                        stringBuilder.AppendLine($"{depthStr}     }}");
                        stringBuilder.AppendLine($"{depthStr}     node.InnerText = sb.ToString();");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        return stringBuilder.ToString();
                    }
                    else
                    {
                        string elementTypeFullName = GetGenericTypeFullName(elementType);
                        alreadyGenericList.Add(type);
                        Log.Print($"CenericType : {TypeFullName}");
                        StringBuilder stringBuilder = new StringBuilder();
                        stringBuilder.AppendLine($"{depthStr}public static void Load(this {TypeFullName} o, System.Xml.XmlNode node)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}    int count = node.ChildNodes.Count;");
                        stringBuilder.AppendLine($"{depthStr}    for(int i = 0; i < count; i++)");
                        stringBuilder.AppendLine($"{depthStr}    {{");
                        stringBuilder.AppendLine($"{depthStr}        {elementTypeFullName} c = new {elementTypeFullName}();");
                        stringBuilder.AppendLine($"{depthStr}        Load(c, node.ChildNodes[i]);");
                        stringBuilder.AppendLine($"{depthStr}        o.Add(c);");
                        stringBuilder.AppendLine($"{depthStr}    }}");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        stringBuilder.AppendLine($"{depthStr}public static void Save(this {TypeFullName} o, System.Xml.XmlNode parent, string value_name)");
                        stringBuilder.AppendLine($"{depthStr}{{");
                        stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                        stringBuilder.AppendLine($"{depthStr}     System.Xml.XmlNode node = AddNode(parent, value_name);");
                        stringBuilder.AppendLine($"{depthStr}     for(int i = 0, len = o.Count; i < len; i++)");
                        stringBuilder.AppendLine($"{depthStr}     {{");
                        stringBuilder.AppendLine($"{depthStr}           {elementTypeFullName} c = o[i];");
                        stringBuilder.AppendLine($"{depthStr}           if(c == null) continue;");
                        stringBuilder.AppendLine($"{depthStr}           Save(c, node, \"Item\");");
                        stringBuilder.AppendLine($"{depthStr}     }}");
                        stringBuilder.AppendLine($"{depthStr}}}");
                        return stringBuilder.ToString();
                    }
                }
            }
        }
        return "";
    }

    static bool IsExtenalClass(Type type)
    {
        return type.IsSubclassOf(typeof(DataObject)) || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>));
    }

    static void GenericType(Type type)
    {
        if (CheckIsAlreadyGeneric(type))
            return;


        StringBuilder xml_load = new StringBuilder();
        xml_load.AppendLine(@"            int count = node.ChildNodes.Count;
            for (int i = 0; i < count; ++i)
            {
                System.Xml.XmlNode attr = node.ChildNodes[i];
                switch (attr.Name)
                {");
        StringBuilder xml_save_node = new StringBuilder();
        StringBuilder xml_save_parent = new StringBuilder();
        xml_save_parent.AppendLine(@"            System.Xml.XmlNode node = DataLoader.AddNode(parentNode, valueName);
            Save(node);");

        StringBuilder json_load = new StringBuilder();
        StringBuilder json_save_node = new StringBuilder();
        StringBuilder json_save_parent = new StringBuilder();
        json_save_parent.AppendLine(@"            SimpleJSON.JSONNode node = new SimpleJSON.JSONArray();
            parentNode.Add(valueName, node);
            Save(node);");

        StringBuilder binary_load = new StringBuilder();
        StringBuilder binary_save = new StringBuilder();


        MemberInfo[] members = type.GetMembers();
        if (members.Length == 0)
            return;

        Log.Print($"CenericType : {type.FullName}");

        for (int i = 0; i < members.Length; i++)
        {
            MemberInfo memberInfo = members[i];
            if (!memberInfo.HasAttribute<DataMemberAttribute>(true))
                continue;

            bool isProperty = false;
            Type memberType;
            if (memberInfo.MemberType == MemberTypes.Field)
            {
                FieldInfo fi = memberInfo as FieldInfo;
                memberType = fi.FieldType;
            }
            else if (memberInfo.MemberType == MemberTypes.Property)
            {
                isProperty = true;
                PropertyInfo ppi = memberInfo as PropertyInfo;
                memberType = ppi.PropertyType;
            }
            else
                continue;

            string memberTypeFullName = GetGenericTypeFullName(memberType);
            if (IsValueType(memberType))
            {
                xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                xml_load.AppendLine($"                        {memberInfo.Name} = Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                xml_load.AppendLine("                        break;");

                xml_save_node.AppendLine($"            Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                json_load.AppendLine($"            {{");
                json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                json_load.AppendLine($"                {memberInfo.Name} = Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                json_load.AppendLine($"            }}");

                json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\", {memberInfo.Name});");
            }
            else if (DataFactoryInterfaceType.IsAssignableFrom(memberType))
            {
                if (memberType.IsClass)
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        {memberInfo.Name} = new {memberTypeFullName}().Create(attr) as {memberTypeFullName};");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            if ({memberInfo.Name} != null)");
                    xml_save_node.AppendLine($"                {memberInfo.Name}.Save(Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                {memberInfo.Name} = new {memberTypeFullName}().Create(attr) as {memberTypeFullName};");
                    json_load.AppendLine($"                {memberInfo.Name}.Load(attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\", {memberInfo.Name});");
                }
            }
            else if (DataInterfaceType.IsAssignableFrom(memberType))
            {
                if (memberType.IsClass)
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
                    xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
                    xml_load.AppendLine($"                        {memberInfo.Name}.Load(attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            if ({memberInfo.Name} != null)");
                    xml_save_node.AppendLine($"                {memberInfo.Name}.Save(Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                if ({memberInfo.Name} == null)");
                    json_load.AppendLine($"                    {memberInfo.Name} = new {memberTypeFullName}();");
                    json_load.AppendLine($"                {memberInfo.Name}.Load(attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\", {memberInfo.Name});");
                }
                else
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        {memberInfo.Name}.Load(attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            {memberInfo.Name}.Save(Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                {memberInfo.Name}.Load(attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\", {memberInfo.Name});");
                }
            }
            //else if (ComponentInterfaceType.IsAssignableFrom(memberType))
            //{
            //    if (memberType.IsClass)
            //    {
            //        xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
            //        xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
            //        xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
            //        xml_load.AppendLine($"                        {memberInfo.Name}.Container = this;");
            //        xml_load.AppendLine($"                        {memberInfo.Name}.Load(attr);");
            //        xml_load.AppendLine("                        break;");

            //        xml_save_node.AppendLine($"            if ({memberInfo.Name} != null)");
            //        xml_save_node.AppendLine($"                {memberInfo.Name}.Save(Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

            //        json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
            //        json_load.AppendLine($"            {{");
            //        json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
            //        json_load.AppendLine($"                if ({memberInfo.Name} == null)");
            //        json_load.AppendLine($"                    {memberInfo.Name} = new {memberTypeFullName}();");
            //        json_load.AppendLine($"                {memberInfo.Name}.Container = this;");
            //        json_load.AppendLine($"                {memberInfo.Name}.Load(attr);");
            //        json_load.AppendLine($"            }}");

            //        json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\", {memberInfo.Name});");
            //    }
            //}
            else if (memberType.IsGenericType && memberType.GetGenericTypeDefinition() == typeof(List<>))
            {
                Type[] typeParameters = memberType.GenericTypeArguments;
                Type elementType = typeParameters[0];
                string elementTypeFullName = GetGenericTypeFullName(elementType);
                if (!DataInterfaceType.IsAssignableFrom(elementType))
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
                    xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
                    xml_load.AppendLine($"                        {memberInfo.Name}.Clear();");
                    xml_load.AppendLine($"                        Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            if ({memberInfo.Name} != null && {memberInfo.Name}.Count > 0)");
                    xml_save_node.AppendLine($"                Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                if ({memberInfo.Name} == null)");
                    json_load.AppendLine($"                    {memberInfo.Name} = new {memberTypeFullName}();");
                    json_load.AppendLine($"                {memberInfo.Name}.Clear();");
                    json_load.AppendLine($"                Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.Save( {memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");
                }
                else
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
                    xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
                    xml_load.AppendLine($"                        {memberInfo.Name}.Clear();");
                    xml_load.AppendLine($"                        Sango.Game.DataLoader.Load<{elementTypeFullName}>({memberInfo.Name}, attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            if ({memberInfo.Name} != null && {memberInfo.Name}.Count > 0)");
                    xml_save_node.AppendLine($"                Sango.Game.DataLoader.Save<{elementTypeFullName}>({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                if ({memberInfo.Name} == null)");
                    json_load.AppendLine($"                    {memberInfo.Name} = new {memberTypeFullName}();");
                    json_load.AppendLine($"                {memberInfo.Name}.Clear();");
                    json_load.AppendLine($"                Sango.Game.DataLoader.Load<{elementTypeFullName}>({memberInfo.Name}, attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode<{elementTypeFullName}>(node, \"{memberInfo.Name}\", {memberInfo.Name});");

                }
            }
            else if (memberType.IsArray)
            {
                Type elementType = memberType.GetElementType();
                string elementTypeFullName = GetGenericTypeFullName(elementType);
                if (elementType.IsValueType)
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        {memberInfo.Name} = Sango.Game.DataLoader.LoadArray<{elementTypeFullName}>(attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            if ({memberInfo.Name} != null && {memberInfo.Name}.Length > 0)");
                    xml_save_node.AppendLine($"                Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                {memberInfo.Name} = Sango.Game.DataLoader.LoadArray<{elementTypeFullName}>(attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.Save( {memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");
                }
                else
                {
                    //xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    //xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
                    //xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
                    //xml_load.AppendLine($"                        {memberInfo.Name}.Clear();");
                    //xml_load.AppendLine($"                        Sango.Game.DataLoader.Load<{elementTypeFullName}>({memberInfo.Name}, Sango.Game.DataLoader.AddNode(attr, \"{memberInfo.Name}\"));");
                    //xml_load.AppendLine("                        break;");

                    //xml_save_node.AppendLine($"            if ({memberInfo.Name} != null && {memberInfo.Name}.Length > 0)");
                    //xml_save_node.AppendLine($"                Sango.Game.DataLoader.Save<{elementTypeFullName}>({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    //json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    //json_load.AppendLine($"            {{");
                    //json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    //json_load.AppendLine($"                if ({memberInfo.Name} == null)");
                    //json_load.AppendLine($"                    {memberInfo.Name} = new {memberTypeFullName}();");
                    //json_load.AppendLine($"                {memberInfo.Name}.Clear();");
                    //json_load.AppendLine($"                Sango.Game.DataLoader.Load<{elementTypeFullName}>({memberInfo.Name}, attr);");
                    //json_load.AppendLine($"            }}");

                    //json_save_node.AppendLine($"            Sango.Game.DataLoader.AddNode<{elementTypeFullName}>(node, \"{memberInfo.Name}\", {memberInfo.Name});");

                }
            }
            else
            {
                if (memberType.IsClass)
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        if ({memberInfo.Name} == null)");
                    xml_load.AppendLine($"                            {memberInfo.Name} = new {memberTypeFullName}();");
                    xml_load.AppendLine($"                        Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                Sango.Game.DataLoader.Load({memberInfo.Name}, attr);");
                    json_load.AppendLine($"            }}");


                    json_save_node.AppendLine($"            Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                }
                else
                {
                    xml_load.AppendLine($"                    case \"{memberInfo.Name}\":");
                    xml_load.AppendLine($"                        Sango.Game.DataLoader.Load(ref {memberInfo.Name}, attr);");
                    xml_load.AppendLine("                        break;");

                    xml_save_node.AppendLine($"            Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                    json_load.AppendLine($"            if (node.HasKey(\"{memberInfo.Name}\"))");
                    json_load.AppendLine($"            {{");
                    json_load.AppendLine($"                SimpleJSON.JSONNode attr = node[\"{memberInfo.Name}\"];");
                    json_load.AppendLine($"                Sango.Game.DataLoader.Load(ref {memberInfo.Name}, attr);");
                    json_load.AppendLine($"            }}");

                    json_save_node.AppendLine($"            Sango.Game.DataLoader.Save({memberInfo.Name}, Sango.Game.DataLoader.AddNode(node, \"{memberInfo.Name}\"));");

                }


            }
        }

        xml_load.AppendLine(@"                }");
        xml_load.AppendLine(@"            }");

        string clearFullTypeName = GetGenericTypeFullName(type);
        string clearTypeName = GetGenericTypeName(type);
        if (string.IsNullOrEmpty(clearTypeName))
        {
            return;
        }

        string fileName = string.Format(filePath, clearFullTypeName.Replace(".", "_"));
        string finale = string.Format(classStr, clearTypeName, type.Namespace,
        xml_save_node.ToString(), xml_load.ToString(), json_save_node.ToString(),
            json_load.ToString(), binary_save.ToString(), binary_load.ToString()
            );
        Sango.File.WriteAllText(fileName, finale);
    }


#if UNITY_EDITOR
    [UnityEditor.MenuItem("Sango /Data/XML 序列化代码生成")]
#endif
    static void ProcessXMLScript()
    {
        alreadyGenericList.Clear();
        needGenericList.Clear();
        alreadyGenericList.AddRange(customGenericList);

        //Type type = typeof(int[]);
        //Type type1 = typeof(Person[]);
        //Type type2 = typeof(List<int>);
        //Type type3 = typeof(List<Person>);
        //Type type4 = typeof(int);
        //Log.PrintError(type4.IsValueType);
        //Type type5 = typeof(string);
        //Log.PrintError(type5.IsValueType);

        Sango.Directory.Create(filePath, false);
        System.Reflection.Assembly[] AS = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var A in AS)
        {
            try
            {
                System.Type[] types = A.GetTypes();
                foreach (var T in types)
                {
                    if (DataInterfaceType.IsAssignableFrom(T) && T.HasAttribute<DataContractAttribute>(true))
                        GenericType(T);
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException) {; }
        }

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}