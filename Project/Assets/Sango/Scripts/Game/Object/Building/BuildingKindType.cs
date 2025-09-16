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
        /// 农田
        /// </summary>
        Farm,
        /// <summary>
        /// 矿场
        /// </summary>
        Mine,
        /// <summary>
        /// 市场
        /// </summary>
        Market,
        /// <summary>
        /// 村庄
        /// </summary>
        Village,
        /// <summary>
        /// 粮仓
        /// </summary>
        FoodStore,
        /// <summary>
        /// 箭塔
        /// </summary>
        ArrowTower,
        /// <summary>
        /// 堡垒
        /// </summary>
        Castle,
    }
}
