using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Core
{
    /// <summary>
    /// 数据工厂接口，用于创建数据对象
    /// </summary>
    public interface IDataFactory : IDataObject
    {
        /// <summary>
        /// 从二进制读取器创建数据对象
        /// </summary>
        /// <param name="node">二进制读取器</param>
        /// <returns>数据对象</returns>
        public IDataObject Create(BinaryReader node);
        /// <summary>
        /// 从XML节点创建数据对象
        /// </summary>
        /// <param name="node">XML节点</param>
        /// <returns>数据对象</returns>
        public IDataObject Create(System.Xml.XmlNode node);
        /// <summary>
        /// 从JSON节点创建数据对象
        /// </summary>
        /// <param name="node">JSON节点</param>
        /// <returns>数据对象</returns>
        public IDataObject Create(SimpleJSON.JSONNode node);

    }
}
