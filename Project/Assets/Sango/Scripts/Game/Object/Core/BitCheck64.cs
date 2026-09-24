/*
 * 文件名：BitCheck64.cs
 * 描述：64位位检查类，用于管理64位状态
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Core
{
    /// <summary>
    /// 64位位检查类，用于管理64位状态
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BitCheck64
    {
        /// <summary>
        /// 状态
        /// </summary>
        [JsonProperty]
        public ulong state;

        /// <summary>
        /// 最大位数
        /// </summary>
        public int Max { get { return 64; } }

        /// <summary>
        /// 检查指定位是否设置
        /// </summary>
        /// <param name="bitPos">位位置</param>
        /// <returns>是否设置</returns>
        public bool Has(int bitPos)
        {
            ulong dest = (ulong)1 << bitPos;
            return (state & dest) == dest;
        }

        /// <summary>
        /// 设置指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Set(int bitPos)
        {
            state |= ((ulong)1 << bitPos);
        }

        /// <summary>
        /// 清除指定位
        /// </summary>
        /// <param name="bitPos">位位置</param>
        public void Remove(int bitPos)
        {
            state = state & (~((ulong)1 << bitPos));
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
            state = ulong.MaxValue;
        }
    }
}
