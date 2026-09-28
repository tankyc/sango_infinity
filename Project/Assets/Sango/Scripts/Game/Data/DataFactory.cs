using SimpleJSON;
using System;
using System.Collections;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Core
{
    /// <summary>
    /// 数据工厂类，用于创建和加载数据对象
    /// </summary>
    public class DataFactory : IDataFactory
    {
        /// <summary>
        /// 从二进制读取器创建数据对象
        /// </summary>
        /// <param name="node">二进制读取器</param>
        /// <returns>数据对象</returns>
        public virtual IDataObject Create(BinaryReader node)
        {
            return null;
        }
        /// <summary>
        /// 从XML节点创建数据对象
        /// </summary>
        /// <param name="node">XML节点</param>
        /// <returns>数据对象</returns>
        public virtual IDataObject Create(System.Xml.XmlNode node)
        {
            return null;
        }
        /// <summary>
        /// 从JSON节点创建数据对象
        /// </summary>
        /// <param name="node">JSON节点</param>
        /// <returns>数据对象</returns>
        public virtual IDataObject Create(SimpleJSON.JSONNode node)
        {
            return null;
        }

        /// <summary>
        /// 从二进制读取器加载数据
        /// </summary>
        /// <param name="node">二进制读取器</param>
        public virtual void Load(BinaryReader node) {; }
        /// <summary>
        /// 保存数据到二进制写入器
        /// </summary>
        /// <param name="node">二进制写入器</param>
        public virtual void Save(BinaryWriter node) {; }
        /// <summary>
        /// 保存数据到XML节点
        /// </summary>
        /// <param name="node">XML节点</param>
        public virtual void Save(System.Xml.XmlNode node) { }
        /// <summary>
        /// 从XML节点加载数据
        /// </summary>
        /// <param name="node">XML节点</param>
        public virtual void Load(System.Xml.XmlNode node) { }
        /// <summary>
        /// 保存数据到JSON节点
        /// </summary>
        /// <param name="node">JSON节点</param>
        public virtual void Save(SimpleJSON.JSONNode node) { }
        /// <summary>
        /// 从JSON节点加载数据
        /// </summary>
        /// <param name="node">JSON节点</param>
        public virtual void Load(SimpleJSON.JSONNode node) { }
    }
}
