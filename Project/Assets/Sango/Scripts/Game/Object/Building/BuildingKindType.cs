using System.IO;
using TKNewtonsoft.Json;
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
        /// 墙
        /// </summary>
        Wall = 9,


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
