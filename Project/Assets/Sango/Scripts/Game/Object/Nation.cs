using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 国号（势力可选用的国家名）。
    ///
    /// 对齐 Open311 的 `Nation` 表（`s11_scen.h` 的 `nation_t`，共 84 条）：
    /// 前 42 条为史实国号 / 异民族 / 贼，后 42 条为「儐１～儐４」占位国号，
    /// 供玩家自建势力时使用。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Nation : SangoObject
    {
        /// <summary>
        /// 国号说明文本（原版 lp 前的说明列）。
        ///
        /// 例：魏 —— 「以兖、冀两州为中心的国家。战国七雄之一，也是三晋之一。」
        /// </summary>
        [JsonProperty]
        public string desc;

        /// <summary>
        /// 原版字段 `lp58`（偏移 0x58，`int16`）。
        ///
        /// 观测特征：仅前 37 条史实国号有值（2~38），38 条之后为空（0），
        /// 且值可重复 —— 判定为**纹章 / 旗帜图案 Id**，暂不参与逻辑。
        /// </summary>
        [JsonProperty]
        public int iconId;

        /// <summary>
        /// 原版字段 `lp60`（偏移 0x60，`int8`）。
        ///
        /// 观测特征：前 37 条为 1，其后的异民族 / 贼 / 儐系列为 0 ——
        /// 判定为**是否可被正常授予的史实国号**标记。
        /// </summary>
        [JsonProperty]
        public int isHistorical;

        /// <summary>
        /// 原版字段 `lp64`（偏移 0x64，`int8`）。
        ///
        /// 观测特征：0~83 的一个完整置换，且**越大越显赫**
        ///（魏 82、燕 78 …… 山越 42、儐４ 0）——
        /// 判定为**国号显赫度排序值**，用于同分时的择优与列表排序。
        /// </summary>
        [JsonProperty]
        public int fame;
    }
}
