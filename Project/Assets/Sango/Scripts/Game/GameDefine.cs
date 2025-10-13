namespace Sango.Game
{
    public enum SeasonType : int
    {
        Autumn = 0,
        Spring = 1,
        Summer = 2,
        Winter = 3,
    }

    public class GameDefine
    {
        //--- 每个月对应的季节
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

    }

    public enum CityJobType : int
    {
        /// <summary>
        /// 农业
        /// </summary>
        Farming = 0,

        /// <summary>
        /// 商业
        /// </summary>
        Develop,

        /// <summary>
        /// 巡查
        /// </summary>
        Inspection,

        /// <summary>
        /// 训练
        /// </summary>
        TrainTroop,

        /// <summary>
        /// 搜索
        /// </summary>
        Searching,

        /// <summary>
        /// 招募士兵
        /// </summary>
        RecuritTroop,

        /// <summary>
        /// 招募武将
        /// </summary>
        RecuritPerson,

        /// <summary>
        /// 生产兵装
        /// </summary>
        CreateItems,

        /// <summary>
        /// 建造
        /// </summary>
        Build,

        /// <summary>
        /// 生产器具
        /// </summary>
        CreateMachine,

        /// <summary>
        /// 生产船
        /// </summary>
        CreateBoat,

        /// <summary>
        /// 生产马
        /// </summary>
        CreateHourse,

        MaxJobCount
    }
}
