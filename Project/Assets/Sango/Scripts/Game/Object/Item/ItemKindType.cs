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
        Boat = 4,

        /// <summary>
        /// 宝物
        /// </summary>
        Treasure = 5,

        /// <summary>
        /// 装备
        /// </summary>
        Equipment = 6,
    }

    public enum WeaponKind : int
    {
        Sword,
        Spear,
        Halberd,
        CrossBow,
    }

    /// <summary>
    /// 器械种类（对应道具库中的攻城器具）
    /// </summary>
    public enum MachineKind : int
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,

        /// <summary>
        /// 冲车 —— 对城关港耐久伤害极高
        /// </summary>
        Helepolis,

        /// <summary>
        /// 井阑 —— 对城内部队可远程攻击
        /// </summary>
        SiegeTower,

        /// <summary>
        /// 投石车 —— 范围攻击，可攻城可野战
        /// </summary>
        Catapult,

        /// <summary>
        /// 木兽 —— 火攻器械，施放火计
        /// </summary>
        WoodenBeast,
    }

    /// <summary>
    /// 船只种类（对应道具库中的船只）
    /// </summary>
    public enum BoatKind : int
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,

        /// <summary>
        /// 走舸 —— 基础船只，无战法
        /// </summary>
        LightBoat,

        /// <summary>
        /// 楼船 —— 大型船只，可施展箭雨等战法
        /// </summary>
        TowerShip,

        /// <summary>
        /// 斗舰 —— 顶级船只，可施展近接战法
        /// </summary>
        FightingShip,
    }

    /// <summary>
    /// 装备种类（与数据 ItemTypes.json 中装备的 subKind 一一对应）
    /// 装备统一使用 kind = ItemKindType.Equipment，再以 subKind 细分三类；
    /// 装备必须装入武将对应装备栏才会生效（见 Person.EquippedWeapon / EquippedArmor / EquippedHorse）
    /// </summary>
    public enum EquipmentKind : int
    {
        /// <summary>
        /// 武器 —— 剑 / 枪 / 戟 / 弩 各系，装入武器栏生效
        /// </summary>
        Weapon = 0,

        /// <summary>
        /// 铠甲 —— 装入铠甲栏生效
        /// </summary>
        Armor = 1,

        /// <summary>
        /// 马匹 —— 装入马匹栏生效
        /// </summary>
        Horse = 2,
    }

    /// <summary>
    /// 宝物种类（与数据 ItemTypes.json 中宝物的 subKind 一一对应）
    /// 宝物统一使用 kind = ItemKindType.Treasure，再以 subKind 细分小类
    /// </summary>
    public enum TreasureKind : int
    {
        /// <summary>
        /// 名马 —— 赤兔马、的卢、爪黄飞电、绝影、大宛马等，提升撤退成功率
        /// </summary>
        FamousHorse = 0,

        /// <summary>
        /// 宝剑 —— 七星宝刀、倚天剑、青釭剑、雌雄一对剑等
        /// </summary>
        Sword = 1,

        /// <summary>
        /// 长柄武器 —— 方天画戟、青龙偃月刀、蛇矛等
        /// </summary>
        LongWeapon = 2,

        /// <summary>
        /// 暗器 —— 流星锤、飞刀、短戟、手戟、铁锁等
        /// </summary>
        HiddenWeapon = 3,

        /// <summary>
        /// 宝弓 —— 养由基弓、李广弓、檀弓、东胡飞弓、貊弓等
        /// </summary>
        Bow = 4,

        /// <summary>
        /// 书籍 —— 孙子兵法、六韬、三略、太平要术等，提升能力经验获取
        /// </summary>
        Book = 5,

        /// <summary>
        /// 玉器 —— 玉玺等礼器，登庸时效果显著
        /// </summary>
        Jade = 6,

        /// <summary>
        /// 珍玩 —— 铜雀等奇珍
        /// </summary>
        Curio = 7,

        /// <summary>
        /// 兵符 —— 陷阵营、虎豹骑等精锐部队兵符
        /// </summary>
        TroopTally = 8,

        /// <summary>
        /// 铠甲 —— 预留分类，当前数据中暂无此类宝物
        /// </summary>
        Armor = 9,
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
