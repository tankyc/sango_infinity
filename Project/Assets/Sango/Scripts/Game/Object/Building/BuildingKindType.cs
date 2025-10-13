using System.IO;
using Newtonsoft.Json;
using System.Xml;

namespace Sango.Game
{
    public enum BuildingKindType : int
    {
        /// <summary>
        /// 自定义
        /// </summary>
        CustomKind = 0,
        /// <summary>
        /// 城市
        /// </summary>
        City,
        /// <summary>
        /// 关卡
        /// </summary>
        Gate,
        /// <summary>
        /// 港口
        /// </summary>
        Port,
        /// <summary>
        /// 农田
        /// </summary>
        Farm,
        /// <summary>
        /// 市场
        /// </summary>
        Market,
        /// <summary>
        /// 村庄
        /// </summary>
        Village,
        /// <summary>
        /// 兵营
        /// </summary>
        Barracks,
        /// <summary>
        /// 铁匠铺
        /// </summary>
        BlacksmithShop,
        /// <summary>
        /// 工坊
        /// </summary>
        BlacksmithShop1,
        /// <summary>
        /// 造船厂
        /// </summary>
        BlacksmithShop2,
        /// <summary>
        /// 马厩
        /// </summary>
        Stable,
        /// <summary>
        /// 巡查局
        /// </summary>
        PatrolBureau,
        /// <summary>
        /// 箭塔
        /// </summary>
        ArrowTower,
    }
}
