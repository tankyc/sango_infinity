/*
 * 文件名：BitCheck32.cs
 * 描述：32位位检查类，用于管理32位状态
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 32位位检查类，用于管理32位状态
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck32
    {
        /// <summary>
        /// 状态
        /// </summary>
        [JsonProperty]
        public uint state;

        /// <summary>
        /// 最大位数
        /// </summary>
        public int Max { get { return 32; } }

        /// <summary>
        /// 检查指定位是否设置
        /// </summary>
        /// <param name="bitPos">位位置</param>
        /// <returns>是否设置</returns>
        public bool Has(int bitPos)
        {
            uint dest = (uint)1 << bitPos;
            return (state & dest) == dest;
        }

        /// <summary>
        /// 设置指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Set(int bitPos)
        {
            state |= ((uint)1 << bitPos);
        }

        /// <summary>
        /// 清除指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Remove(int bitPos)
        {
            state = state & (~((uint)1 << bitPos));
        }

        /// <summary>
        /// 检查是否为空
        /// </summary>
        /// <returns>是否为空</returns>
        public bool IsEnpty()
        {
            return state == 0;
        }

        /// <summary>
        /// 重置所有位
        /// </summary>
        public void Reset()
        {
            state = 0;
        }

        /// <summary>
        /// 设置所有位
        /// </summary>
        public void SetAll()
        {
            state = uint.MaxValue;
        }

        //public override void LoadFromStream(BinaryReader reader)
        //{
        //    state = reader.ReadUInt32();
        //}
        //public override void SaveToStream(BinaryWriter writer)
        //{
        //    writer.Write(state);
        //}
    }
}
