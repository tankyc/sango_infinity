using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 地域（大区）。当前承载"建筑规划偏好"这一项地域特性。
    ///
    /// 【为什么用显式字段而不是 JsonExtensionData】
    /// 地域特性是要给策划直接读改 / 校验的配置项，显式字段有类型、有默认值、能在编辑器里提示；
    /// 扩展数据字典是"来什么都收"，不适合承担有语义的配置。
    ///
    /// 【默认没有】字段默认 null = 该地域没有特性。规划器读到 null 就整层跳过修正，
    /// 因此 Regions.json 与所有既有剧本数据都**不需要任何改动**。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Region : SangoObject
    {
        /// <summary>
        /// 建筑规划偏好：建筑小类（<c>BuildingKindType</c> 的整数值）→ 份额权重乘数
        /// （**百分比，100 = 不修正**）。
        ///
        /// 例：河北多粮 → <c>"buildBias": { "33": 150 }</c>，即该地域的农田权重 ×1.5。
        /// 未列出的建筑小类按 100（不修正）计。
        ///
        /// 【优先级】城市所属**州（<see cref="Province"/>.buildBias）优先**；
        /// 州没配才落到本地域的配置 —— 更细粒度的覆盖更粗的，两者**不相乘**，
        /// 免得同一效果被叠加两次。
        /// </summary>
        [JsonProperty] public Dictionary<int, int> buildBias;
    }
}
