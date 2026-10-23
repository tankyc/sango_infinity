using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Core
{
    /// <summary>
    /// 建筑大类型
    /// </summary>
    public enum BuildingMajorType : int
    {
        /// <summary>
        /// 固定设施
        /// </summary>
        Fixture = 0,

        /// <summary>
        /// 军事设施
        /// </summary>
        Military,
        
        /// <summary>
        /// 障碍物
        /// </summary>
        Obstacle,

        /// <summary>
        /// 爆炸物
        /// </summary>
        Explosive,

        /// <summary>
        /// 内政设施
        /// </summary>
        Interior,

        /// <summary>
        /// 其他
        /// </summary>
        Other,
    }
        
    /// <summary>
    /// 建筑物小类
    /// </summary>
    public enum BuildingKindType : int
    {
        /// <summary>
        /// 自定义
        /// </summary>
        CustomKind = 0,

        /// <summary>
        /// 城市
        /// </summary>
        City = 1,

        /// <summary>
        /// 关卡
        /// </summary>
        Gate = 2,

        /// <summary>
        /// 港口
        /// </summary>
        Port = 3,

        /// <summary>
        /// 阵
        /// </summary>
        Camp = 4,

        /// <summary>
        /// 小城
        /// </summary>
        LittleCity = 5,


        ///// <summary>
        ///// 砦
        ///// </summary>
        //Camp_2 = 5,

        ///// <summary>
        ///// 城塞
        ///// </summary>
        //Camp_3 = 6,

        /// <summary>
        /// 箭楼
        /// </summary>
        ArrowTower = 7,

        /// <summary>
        /// 墙（土垒 / 石墙共用此 kind，靠 BuildingType.Id 区分）
        /// </summary>
        Wall = 9,

        // ==================== 障碍 / 陷阱（对齐原版 FacilityId） ====================
        //
        // 原版这一段是 FacilityId_Fire(0x10) ~ FacilityId_PheasantTower(0x1e)，
        // 本项目按"原版 facility_id + 1"的既有规律落到 17~30。
        // 数据来源：Data/Export/Common/BuildingType_建筑类型.xlsx
        //   障碍 = majorType 2（Obstacle）
        //   陷阱 = majorType 3（Explosive，原版叫 Trap）
        //
        // 【归属规则】这两类建成后立刻中立（清 BelongForce / BelongCorps），
        // 见 Building.OnCompleted 与 GovernanceValidator。
        // 唯一例外是堤防（Levee）：原版 is_trap_type() 显式排除它，不参与中立化。

        /// <summary>火种</summary>
        Fire = 17,

        /// <summary>火焰种</summary>
        HellFire = 18,

        /// <summary>火球</summary>
        FireBall = 19,

        /// <summary>火焰球</summary>
        HellFireBall = 20,

        /// <summary>火船</summary>
        FireShip = 21,

        /// <summary>业火球</summary>
        InfernoBall = 22,

        /// <summary>业火种</summary>
        Inferno = 23,

        /// <summary>落石</summary>
        Rockfall = 24,

        /// <summary>堤防（陷阱类型，但**不参与**建成即中立）</summary>
        Levee = 25,

        /// <summary>浅滩</summary>
        Sandbar = 26,

        /// <summary>山岳</summary>
        Mountains = 27,

        /// <summary>长城</summary>
        GreatWall = 28,

        /// <summary>遗迹</summary>
        Relic = 29,

        /// <summary>庙</summary>
        Shrine = 30,


        /// <summary>
        /// 市场
        /// </summary>
        Market = 32,

        /// <summary>
        /// 农田
        /// </summary>
        Farm = 33,
       
        /// <summary>
        /// 兵营
        /// </summary>
        Barracks = 34,

        /// <summary>
        /// 铁匠铺
        /// </summary>
        BlacksmithShop = 35,

        /// <summary>
        /// 马厩
        /// </summary>
        Stable = 36,

        /// <summary>
        /// 工坊
        /// </summary>
        MechineFactory = 37,

        /// <summary>
        /// 造船厂
        /// </summary>
        BoatFactory = 38,

        /// <summary>
        /// 造币厂
        /// </summary>
        Mint = 39,

        /// <summary>
        /// 谷仓
        /// </summary>
        Barn = 40,

        /// <summary>
        /// 符节台
        /// </summary>
        SpBuilding1 = 41,

        /// <summary>
        /// 军事府
        /// </summary>
        MilitaryOffice = 42,

        /// <summary>
        /// 人才府
        /// </summary>
        RecruitBuilding = 43,

        /// <summary>
        /// 外交府
        /// </summary>
        SpBuilding2 = 44,

        /// <summary>
        /// 计略府
        /// </summary>
        SpBuilding3 = 45,

        /// <summary>
        /// 炼兵所
        /// </summary>
        TrainTroopBuilding = 46,

        /// <summary>
        /// 大市场
        /// </summary>
        BigMarket = 47,

        /// <summary>
        /// 鱼市
        /// </summary>
        FishMarket = 48,

        /// <summary>
        /// 黑市
        /// </summary>
        BlackMarket = 49,

        /// <summary>
        /// 军屯田
        /// </summary>
        MilitaryGarrison = 50,

        /// <summary>
        /// 村庄
        /// </summary>
        Village,

    }
}
