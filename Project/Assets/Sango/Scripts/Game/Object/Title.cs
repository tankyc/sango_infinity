using Newtonsoft.Json;
namespace Sango.Core
{
    /// <summary>
    /// 州
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Title : SangoObject
    {
        /// <summary>
        /// 指挥
        /// </summary>
        [JsonProperty]
        public int troopsLimit;

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty]
        public int level;

        /// <summary>
        /// 晋升到下一级爵位所需的城池数（-1 表示已达最高爵位、不再晋升）。
        ///
        /// 对齐 Open311 的 `Title::max_city_count_`（s11_scen.h:2145，注释「다음 관직에 필요한 도시 수」）
        /// 与默认值表 `TitleMaxCityCount`（s11_db.cpp:238-240）：
        ///   无=2  州刺史=4  州牧=6  羽林=8  五官=12  大将军=14  大司马=18  公=20  王=24  皇帝=-1
        /// 即：势力当前爵位为该爵位、且城池数达到本阈值时，月度结算可晋升一级。
        /// </summary>
        [JsonProperty]
        public int maxCityCount;
    }
}
