using Newtonsoft.Json;

namespace Sango.Core
{
    /*
     * 
     *  剑	1
        枪	1
        刀	1
        弓	1
        军马	2
        冲车	3
        井阑	3
        投石	3
        木兽	3
        走舸	4
        楼船	4
        斗舰	4

     * */
    public enum ItemKindType : int
    {
        None = 0,

        /// <summary>
        /// 兵器
        /// </summary>
        Weapon,

        /// <summary>
        /// 战马
        /// </summary>
        Horse,

        /// <summary>
        /// 器械
        /// </summary>
        Machine,

        /// <summary>
        /// 船
        /// </summary>
        Boat,

        /// <summary>
        /// 装备
        /// </summary>
        Equipment_Weapon,

        Equipment_Horse,

        Equipment_Armor
    }

    /*
     * 
     *  剑	1
        枪	2
        戟	3
        弓	4
        军马	5
        冲车	6
        木兽	6
        井阑	7
        投石	7
        走舸	8
        楼船	8
        斗舰	8
     * */
    public enum ItemStoreKindType : int
    {
        None = 0,

        Sword = 1,
        Spear = 2,
        Halberd = 3,
        Crossbow = 4,
        Horse = 5,

        /// <summary>
        /// 冲车
        /// </summary>
        Helepolis = 6,

        /// <summary>
        /// 投石
        /// </summary>
        Catapult = 7,

        /// <summary>
        /// 船
        /// </summary>
        Boat = 8
    }
}
