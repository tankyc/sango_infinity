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

}
