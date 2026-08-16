/*
 * 文件名：GameDefine.cs
 * 描述：游戏定义类，包含游戏中使用的枚举和常量
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

namespace Sango.Core
{
    /// <summary>
    /// 季节类型枚举
    /// </summary>
    public enum SeasonType : int
    {
        /// <summary>
        /// 秋季
        /// </summary>
        Autumn = 0,
        /// <summary>
        /// 春季
        /// </summary>
        Spring = 1,
        /// <summary>
        /// 夏季
        /// </summary>
        Summer = 2,
        /// <summary>
        /// 冬季
        /// </summary>
        Winter = 3,
    }

    /// <summary>
    /// 游戏定义类，包含游戏中使用的常量和静态数据
    /// </summary>
    public class GameDefine
    {
        /// <summary>
        /// 每个月对应的季节
        /// </summary>
        public static SeasonType[] SeasonInMonth = {
            SeasonType.Spring,
            SeasonType.Spring,
            SeasonType.Spring,
            SeasonType.Summer,
            SeasonType.Summer,
            SeasonType.Summer,
            SeasonType.Autumn,
            SeasonType.Autumn,
            SeasonType.Autumn,
            SeasonType.Winter,
            SeasonType.Winter,
            SeasonType.Winter,
            };

        /// <summary>
        /// 季节名称
        /// </summary>
        public static string[] seasonName = { "秋", "春", "夏", "冬" };

        /// <summary>
        /// 白色文本颜色
        /// </summary>
        public static UnityEngine.Color whiteText = new UnityEngine.Color(0.862745f, 0.862745f, 0.862745f);

        /// <summary>
        /// 圆圈符号
        /// </summary>
        public static string o = "○";
        /// <summary>
        /// 叉号符号
        /// </summary>
        public static string x = "×";
    }

    /// <summary>
    /// 性别枚举
    /// </summary>
    public enum Sex : int
    {
        /// <summary>
        /// 男性
        /// </summary>
        Male = 0,
        /// <summary>
        /// 女性
        /// </summary>
        Female = 1
    }

    /// <summary>
    /// 适性级别枚举
    /// </summary>
    public enum LevelString : int
    {
        /// <summary>
        /// C级
        /// </summary>
        Ｃ = 0,
        /// <summary>
        /// B级
        /// </summary>
        Ｂ,
        /// <summary>
        /// A级
        /// </summary>
        Ａ,
        /// <summary>
        /// S级
        /// </summary>
        Ｓ,
        /// <summary>
        /// SS级
        /// </summary>
        ＳＳ,
        /// <summary>
        /// SSS级
        /// </summary>
        ＳＳＳ,
        /// <summary>
        /// SSR级
        /// </summary>
        ＳＳR,
    }

    /// <summary>
    /// 宝物类型枚举
    /// </summary>
    public enum TreasureType : int
    {
        /// <summary>
        /// 马
        /// </summary>
        horse = 0,
        /// <summary>
        /// 武器
        /// </summary>
        Weapon = 1,
        /// <summary>
        /// 书
        /// </summary>
        Book = 2,
    }

    /// <summary>
    /// 城市工作类型枚举
    /// </summary>
    public enum CityJobType : int
    {
        /// <summary>
        /// 默认
        /// </summary>
        Default = 0,

        /// <summary>
        /// 农业
        /// </summary>
        Farming = 1,

        /// <summary>
        /// 商业
        /// </summary>
        Develop = 2,

        /// <summary>
        /// 巡查
        /// </summary>
        Inspection = 3,

        /// <summary>
        /// 训练
        /// </summary>
        TrainTroops = 4,

        /// <summary>
        /// 搜索
        /// </summary>
        Searching = 5,

        /// <summary>
        /// 招募士兵
        /// </summary>
        RecruitTroops = 6,

        /// <summary>
        /// 招募武将
        /// </summary>
        RecruitPerson = 7,

        /// <summary>
        /// 生产兵装
        /// </summary>
        CreateItems = 8,

        /// <summary>
        /// 建造
        /// </summary>
        Build = 9,

        /// <summary>
        /// 生产兵器
        /// </summary>
        CreateMachine = 10,

        /// <summary>
        /// 生产船
        /// </summary>
        CreateBoat = 11,

        /// <summary>
        /// 生产马
        /// </summary>
        CreateHorse = 12,

        /// <summary>
        /// 交易粮食
        /// </summary>
        TradeFood = 13,

        /// <summary>
        /// 派遣武将
        /// </summary>
        TransformPerson = 14,

        /// <summary>
        /// 召唤武将
        /// </summary>
        CallPerson = 15,

        /// <summary>
        /// 升级建筑
        /// </summary>
        UpgradeBuilding = 16,

        /// <summary>
        /// 组建部队
        /// </summary>
        MakeTroop = 17,

        /// <summary>
        /// 组建运输部队
        /// </summary>
        MakeTansport = 18,

        /// <summary>
        /// 研究
        /// </summary>
        Research = 19,

        /// <summary>
        /// 褒赏
        /// </summary>
        Reward = 20,

        xxxx = 21,

        /// <summary>
        /// 送礼
        /// </summary>
        SendGift = 22,

        /// <summary>
        /// 同盟
        /// </summary>
        Alliance = 23,
        /// <summary>
        /// 同盟
        /// </summary>
        DiscardAlliance = 24,
        /// <summary>
        /// 最大工作数量
        /// </summary>
        MaxJobCount
    }

    /// <summary>
    /// 信息类型枚举
    /// </summary>
    public enum InfoType : int
    {
        /// <summary>
        /// 资金
        /// </summary>
        Gold = 0,
        /// <summary>
        /// 枪
        /// </summary>
        Spear,
        /// <summary>
        /// 士兵
        /// </summary>
        Troop,
        /// <summary>
        /// 剑
        /// </summary>
        Sword,
        /// <summary>
        /// 弩
        /// </summary>
        CrossBow,
        /// <summary>
        /// 粮食
        /// </summary>
        Food,
        /// <summary>
        /// 马
        /// </summary>
        Horse,
        /// <summary>
        /// 冲车
        /// </summary>
        Helepolis,

        /// <summary>
        /// 木兽
        /// </summary>
        WoodenBeast,

        /// <summary>
        /// 井阑
        /// </summary>
        SiegeTower,

        /// <summary>
        /// 投石车
        /// </summary>
        Catapult,
        /// <summary>
        /// 船
        /// </summary>
        Boat,
        /// <summary>
        /// 大船
        /// </summary>
        BigBoat,
        /// <summary>
        /// 士气
        /// </summary>
        Morale,
        /// <summary>
        /// 耐久
        /// </summary>
        Durability,
        /// <summary>
        /// 治安
        /// </summary>
        Security
    }

    public enum PersonRecruitType
    {
        /// <summary>
        /// 普通登庸
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 有势力的俘虏
        /// </summary>
        OnCityFall = 1, //

        /// <summary>
        /// 没有势力的俘虏
        /// </summary>
        OnForceFall = 2,
        
        /// <summary>
        /// 搜索人才
        /// </summary>
        OnSearching = 3, // 搜索人才
    }
}
