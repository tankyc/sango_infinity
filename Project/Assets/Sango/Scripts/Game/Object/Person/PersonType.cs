namespace Sango.Core
{
    /// <summary>
    /// 武将种类（对应 Open311 的 person_type_t / PersonTypeEnum）。
    ///
    /// 【与 PersonStateType 的区别】
    ///   · <see cref="PersonStateType"/> 是"身份 + 生存状态"（君主/都督/太守/在野/俘虏/死亡），
    ///     会随游戏进程变化，一个武将从一般升到太守、从在籍变俘虏都会改它；
    ///   · 本枚举是"武将数据从哪来"的分类，**运行期不变**，
    ///     用来决定"是否参与历史事件""是否会被自然死亡淘汰""是否算作战力"等。
    ///
    /// 【为什么需要】原版大量逻辑按种类过滤，而不是按身份过滤：
    ///   · 寿命结算只处理非事件、非临时武将（否则贼军会挤占历史武将的死亡名额）；
    ///   · 古代/自建武将只有一次"自动配属"机会，配不上就退场；
    ///   · 部队编成允许历史/古代/自建/临时，排除事件与 NPC。
    /// 本项目原先没有这个字段，这些过滤只能靠 PersonLib 是否为空之类的间接条件替代，
    /// 一旦自建武将也带 PersonLib 就会判断错。
    ///
    /// 【取值】与 Open311 逐值一致，默认 -1 表示尚未分类（老存档读入即为此值）。
    /// </summary>
    public enum PersonType : int
    {
        /// <summary>未分类（原版 type_ 的初始值就是 -1，不是 0）</summary>
        None = -1,

        /// <summary>历史武将（剧本自带的正式武将）</summary>
        Historical = 0,

        /// <summary>古代武将</summary>
        Ancient = 1,

        /// <summary>事件武将（剧情临时登场，不参与寿命/婚姻等常规结算）</summary>
        Event = 2,

        /// <summary>自建武将（玩家在武将编辑器里创建）</summary>
        Custom = 3,

        /// <summary>临时武将（贼军、战场即时生成的敌将，可被复用槽位回收）</summary>
        Temporary = 4,

        /// <summary>未知</summary>
        Unknown = 5,

        /// <summary>NPC（事件用的非战斗角色，等同事件武将）</summary>
        Npc = 6,

        /// <summary>自动生成</summary>
        Auto = 7,

        /// <summary>种类数量上限</summary>
        Max = 8,
    }
}
