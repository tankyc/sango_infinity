
using Sango;
using Sango.Game;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using Unity.VisualScripting;

public static class BinaryExportTools
{
#if UNITY_EDITOR
    //[UnityEditor.MenuItem("Sango/Data/Binary 清理")]
#endif
    static void ProcessXMLClean()
    {

    }

    private static List<Type> alreadyGenericList = new List<Type>();
    private static Queue<Type> needGenericList = new Queue<Type>();

    private static List<Type> customGenericList = new List<Type>();

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


    static string GenericCustomType(Type type)
    {
        if (alreadyGenericList.Contains(type))
            return "";

        if (type.IsSubclassOf(typeof(DataObject)))
        {
            if (type.BaseType != null && type.BaseType.IsGenericType)
            {
                Type[] typeParameters = type.BaseType.GenericTypeArguments;
                if (typeParameters.Length > 0)
                {
                    // 处理LinkObj : ScenarioObject<T>
                    Type _ObjectType = typeof(ScenarioObject<>);
                    Type dest = _ObjectType.MakeGenericType(typeParameters);
                    if (dest != null && type.IsSubclassOf(dest))
                    {

                        string TypeFullName = GetGenericTypeName(type);
                        if (string.IsNullOrEmpty(TypeFullName))
                            return "";

                        Log.Print($"CenericCustomType : {TypeFullName}");
                        StringBuilder sb_load = new StringBuilder();
                        sb_load.AppendLine($"       public static void Load(this {TypeFullName} o, System.Xml.XmlNode node)");
                        sb_load.AppendLine($"       {{");
                        sb_load.AppendLine($"            o.ID = int.Parse(node.InnerText);");
                        sb_load.AppendLine($"       }}");
                        sb_load.AppendLine($"       public static void Save(this {TypeFullName} o, System.Xml.XmlNode node)");
                        sb_load.AppendLine($"       {{");
                        sb_load.AppendLine($"            node.InnerText = o.ID.ToString();");
                        sb_load.AppendLine($"       }}");
                        alreadyGenericList.Add(type);
                        Log.Print($"CenericType : {TypeFullName}");
                        return sb_load.ToString();
                    }

                    // 处理LinkObj : Database<T>
                    _ObjectType = typeof(Database<>);
                    dest = _ObjectType.MakeGenericType(typeParameters);
                    if (dest != null && type.IsSubclassOf(dest))
                    {
                        string TypeFullName = GetGenericTypeName(type);
                        if (string.IsNullOrEmpty(TypeFullName))
                            return "";
                        Type p1 = typeParameters[0];
                        string p1_fullName = GetGenericTypeName(p1);
                        Log.Print($"CenericCustomType : {TypeFullName}");
                        StringBuilder sb_load = new StringBuilder();
                        sb_load.AppendLine($"       public static void Load(this {TypeFullName} o, System.Xml.XmlNode node)");
                        sb_load.AppendLine($"       {{");
                        sb_load.AppendLine("            int count = node.ChildNodes.Count;");
                        sb_load.AppendLine("            for (int i = 0; i < count; ++i)");
                        sb_load.AppendLine("            {");
                        sb_load.AppendLine("                System.Xml.XmlNode attr = node.ChildNodes[i];");
                        sb_load.AppendLine($"                {p1_fullName} v = new {p1_fullName}();");
                        sb_load.AppendLine("                Load(v, attr);");
                        sb_load.AppendLine("                o.Add(v);");
                        sb_load.AppendLine("             }");
                        sb_load.AppendLine($"       }}");
                        sb_load.AppendLine($"       public static void Save(this {TypeFullName} o, System.Xml.XmlNode node)");
                        sb_load.AppendLine($"       {{");
                        sb_load.AppendLine($"            o.ForEach(x=>{{ Save(x, AddNode(node, \"{p1_fullName}\"));}});");
                        sb_load.AppendLine($"       }}");
                        alreadyGenericList.Add(type);
                        Log.Print($"CenericType : {TypeFullName}");
                        return sb_load.ToString();
                    }
                }
            }
        }

        return "";

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
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
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
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                stringBuilder.AppendLine($"{depthStr}     for (int i = 0; i < o.Length; ++i)");
                stringBuilder.AppendLine($"{depthStr}     {{");
                stringBuilder.AppendLine($"{depthStr}           AddNode(node, \"Item\", o[i]);");
                stringBuilder.AppendLine($"{depthStr}     }}");
                stringBuilder.AppendLine($"{depthStr}}}");
                return stringBuilder.ToString();
            }
            else
            {
                string elementTypeFullName = GetGenericTypeName(type);
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
                stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
                stringBuilder.AppendLine($"{depthStr}{{");
                stringBuilder.AppendLine($"{depthStr}     if( o == null) return;");
                stringBuilder.AppendLine($"{depthStr}     for (int i = 0; i < o.Length; ++i)");
                stringBuilder.AppendLine($"{depthStr}     {{");
                stringBuilder.AppendLine($"{depthStr}          {TypeFullName} c = o[i];");
                stringBuilder.AppendLine($"{depthStr}          if(c == null) continue;");
                stringBuilder.AppendLine($"{depthStr}          Save(c, AddNode(node, \"Item\"));");
                stringBuilder.AppendLine($"{depthStr}     }}");
                stringBuilder.AppendLine($"{depthStr}}}");
                return stringBuilder.ToString();
            }
        }
        return "";
    }

    static string GenericType2(Type type, int depth = 0)
    {
        if (alreadyGenericList.Contains(type))
            return "";

        string depthStr = "   ";
        for (int i = 0; i < depth; i++)
            depthStr += "   ";

        if (type.IsValueType)
        {
            string TypeFullName = type.FullName;
            alreadyGenericList.Add(type);
            Log.Print($"CenericType : {TypeFullName}");
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{depthStr}public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
            stringBuilder.AppendLine($"{depthStr}{{");
            stringBuilder.AppendLine($"{depthStr}    {TypeFullName}.TryParse(node.InnerText, out o);");
            stringBuilder.AppendLine($"{depthStr}}}");
            stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
            stringBuilder.AppendLine($"{depthStr}{{");
            stringBuilder.AppendLine($"{depthStr}    node.InnerText = o.ToString();");
            stringBuilder.AppendLine($"{depthStr}}}");
            return stringBuilder.ToString();
        }
        else if (type == typeof(string))
        {
            string TypeFullName = type.FullName;
            alreadyGenericList.Add(type);
            Log.Print($"CenericType : {TypeFullName}");
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{depthStr}public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
            stringBuilder.AppendLine($"{depthStr}{{");
            stringBuilder.AppendLine($"{depthStr}    o = node.InnerText;");
            stringBuilder.AppendLine($"{depthStr}}}");
            stringBuilder.AppendLine($"{depthStr}public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
            stringBuilder.AppendLine($"{depthStr}{{");
            stringBuilder.AppendLine($"{depthStr}    node.InnerText = o;");
            stringBuilder.AppendLine($"{depthStr}}}");
            return stringBuilder.ToString();
        }
        return "";
    }

    static string GenericType(Type type)
    {
        if (alreadyGenericList.Contains(type))
            return "";
        
        string TypeFullName = GenericCustomType(type);
        if (!string.IsNullOrEmpty(TypeFullName))
            return TypeFullName;

        TypeFullName = GenericArrayType(type);
        if (!string.IsNullOrEmpty(TypeFullName))
            return TypeFullName;

        TypeFullName = GenericType2(type);
        if (!string.IsNullOrEmpty(TypeFullName))
            return TypeFullName;

        TypeFullName = GetGenericTypeName(type);
        if (string.IsNullOrEmpty(TypeFullName))
            return "";

        alreadyGenericList.Add(type);
        Log.Print($"CenericType : {TypeFullName}");

        StringBuilder sb_load = new StringBuilder();
        StringBuilder sb_save = new StringBuilder();

        sb_load.AppendLine($"       public static void Load(ref {TypeFullName} o, System.Xml.XmlNode node)");
        sb_load.AppendLine($"       {{");
        sb_save.AppendLine($"       public static void Save({TypeFullName} o, System.Xml.XmlNode node)");
        sb_save.AppendLine($"       {{");

        List<MemberInfo> members = new List<MemberInfo>();
        members.AddRange(type.GetProperties());
        members.AddRange(type.GetFields());
        if (members.Count > 0)
        {

            sb_load.AppendLine("            int count = node.ChildNodes.Count;");
            sb_load.AppendLine("            for (int i = 0; i < count; ++i)");
            sb_load.AppendLine("            {");
            sb_load.AppendLine("                System.Xml.XmlNode attr = node.ChildNodes[i];");
            sb_load.AppendLine("                switch (attr.Name)");
            sb_load.AppendLine("                {");

            foreach (System.Reflection.MemberInfo pi in members)
            {
                if (pi.HasAttribute<DataMemberAttribute>(true))
                {
                    sb_load.AppendLine($"                case \"{pi.Name}\":");
                    Type piType = null;
                    FieldInfo fi = pi as FieldInfo;
                    bool isProperty = false;
                    if (fi != null)
                    {
                        piType = fi.FieldType;
                    }
                    else
                    {
                        PropertyInfo ppi = pi as PropertyInfo;
                        if (ppi != null)
                        {
                            piType = ppi.PropertyType;
                            isProperty = true;
                        }
                    }

                    if (!needGenericList.Contains(piType))
                        needGenericList.Enqueue(piType);

                    if (isProperty)
                    {
                        sb_load.AppendLine($"                   var v = o.{pi.Name};");
                        sb_load.AppendLine($"                   Load(ref v, attr);");
                        sb_load.AppendLine($"                   o.{pi.Name} = v;");
                        sb_save.AppendLine($"           Save(o.{pi.Name}, AddNode(node, \"{pi.Name}\"));");
                    }
                    else
                    {
                        sb_load.AppendLine($"                   Load(ref o.{pi.Name}, attr);");
                        sb_save.AppendLine($"           Save(o.{pi.Name}, AddNode(node, \"{pi.Name}\"));");

                    }
                    sb_load.AppendLine($"                   break;");
                }
            }
            sb_load.AppendLine("                }");
            sb_load.AppendLine("            }");
        }

        sb_load.AppendLine("       }");
        sb_save.AppendLine("       }");

        StringBuilder stringBuilder = new StringBuilder();

        stringBuilder.AppendLine(sb_load.ToString());
        stringBuilder.AppendLine(sb_save.ToString());
        return stringBuilder.ToString();
    }


#if UNITY_EDITOR
    //[UnityEditor.MenuItem("Sango/Data/Binary 序列化代码生成")]
#endif
    static void ProcessXMLScript()
    {
        alreadyGenericList.Clear();
        needGenericList.Clear();

        //Type type = typeof(int[]);
        //Type type1 = typeof(Person[]);
        //Type type2 = typeof(List<int>);
        //Type type3 = typeof(List<Person>);
        //Type type4 = typeof(int);
        //Log.PrintError(type4.IsValueType);
        //Type type5 = typeof(string);
        //Log.PrintError(type5.IsValueType);

        string filePath = UnityEngine.Application.dataPath + "/Sango/XmlGenerate/XmlLoader.cs";
        Sango.Directory.Create(filePath, false);
        var result = new System.Collections.Generic.List<System.Type>();
        System.Reflection.Assembly[] AS = System.AppDomain.CurrentDomain.GetAssemblies();
        foreach (var A in AS)
        {
            try
            {
                System.Type[] types = A.GetTypes();
                foreach (var T in types)
                {
                    if (T.IsSubclassOf(typeof(DataObject)) && T.HasAttribute<DataContractAttribute>(true))
                        result.Add(T);
                }
            }
            catch (System.Reflection.ReflectionTypeLoadException) {; }
        }
        string className = $"XmlLoader.cs";
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("public static class XmlLoader");
        sb.AppendLine("{");

        sb.Append(@"        public static System.Xml.XmlNode AddNode(System.Xml.XmlNode node, string name)
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
        }");
        sb.AppendLine("");

        foreach (Type type in result)
        {
            string classStr = GenericType(type);
            if (!string.IsNullOrEmpty(classStr))
                sb.AppendLine(classStr);
        }

        while (needGenericList.Count > 0)
        {
            Type type = needGenericList.Dequeue();
            string classStr = GenericType(type);
            if (!string.IsNullOrEmpty(classStr))
                sb.AppendLine(classStr);
        }

        sb.AppendLine("}");
        Sango.File.WriteAllText(filePath, sb.ToString());
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }
}