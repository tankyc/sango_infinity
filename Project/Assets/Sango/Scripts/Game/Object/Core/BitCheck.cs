/*
 * 文件名：BitCheck.cs
 * 描述：位检查类，用于管理位状态
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Core
{
    /// <summary>
    /// 位检查类，用于管理位状态
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck
    {
        /// <summary>
        /// 状态数组
        /// </summary>
        [JsonProperty]
        public uint[] state;

        /// <summary>
        /// 最大位数
        /// </summary>
        public int Max { get { return state.Length * 32; } }

        /// <summary>
        /// 构造函数
        /// </summary>
        public BitCheck()
        {
            state = new uint[1];
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="size">大小</param>
        public BitCheck(int size)
        {
            int n = size / 32;
            int len = size % 32 == 0 ? n : n + 1;
            state = new uint[len];
        }

        /// <summary>
        /// 检查指定位是否设置
        /// </summary>
        /// <param name="bitPos">位位置</param>
        /// <returns>是否设置</returns>
        public bool Has(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            uint dest = (uint)1 << index;
            return (state[where] & dest) == dest;
        }

        /// <summary>
        /// 设置指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Set(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            state[where] |= ((uint)1 << index);
        }

        /// <summary>
        /// 清除指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Remove(int bitPos)
        {
            int where = bitPos / 32;
            int index = bitPos % 32;
            state[where] = state[where] & (~((uint)1 << index));
        }

        /// <summary>
        /// 重置所有位
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = 0;
            }
        }

        /// <summary>
        /// 设置所有位
        /// </summary>
        public void SetAll()
        {
            for (int i = 0; i < state.Length; i++)
            {
                state[i] = uint.MaxValue;
            }
        }
     
        //public override void Load(BinaryReader reader)
        //{
        //    for (int i = 0; i < state.Length; i++)
        //        state[i] = reader.ReadUInt32();
        //}
        //public override void Save(BinaryWriter writer)
        //{
        //    for (int i = 0; i < state.Length; i++)
        //        writer.Write(state[i]);
        //}
    }
}
