namespace Sango.Core
{
    /// <summary>
    /// 异族武将配置（当前阶段的临时方案，等 ForceType_Iminzoku 落地后再并入势力层）。
    ///
    /// 【背景】原版（Open311）把异民族建模成 **4 个 Id 固定的普通 Force**：
    ///   ForceId_Kyou = 0x2a（羌）/ ForceId_Sanetsu = 0x2b（山越）/
    ///   ForceId_Ugan = 0x2c（乌丸）/ ForceId_Nanban = 0x2d（南蛮），见 s11_enum_preset.h。
    /// 异族武将则是从 PersonId_*_Bushou 模板克隆出来的临时武将
    /// （s11_sys_scen_unit.cpp:84-153 的 create_bandit_unit + create_bandit_person）。
    ///
    /// 本项目暂时既没有 Force.ForceType，也没有根城（nejiro）刷怪流程，
    /// 所以先退一步：用「武将 Id + 名字」双重校验，把数据里那 8 个异族模板武将标出来，
    /// 让归属巡检、身份筛选先能正确排除它们，不至于把"故意没有所属城"的人清成在野。
    ///
    /// 【为什么必须双重校验】
    ///   · 只比 Id：剧本 / Mod 重排武将库后 Id 会漂移，要么误判正常武将，要么漏判异族；
    ///   · 只比名字：玩家自建武将取同名就会被误判；
    ///   两个条件同时命中才算异族。任一条件对不上就退回"正常武将"，
    ///   属于失败时偏保守的方向（宁可漏判，绝不把正常武将排除出巡检）。
    ///
    /// 【当前用途】
    ///   · 只负责"**配置 → 运行时字段**"的固化：<see cref="Person.FixupBarbarianTribe"/>
    ///     在 <c>Person.OnScenarioPrepare</c> 里调用一次，把结果写进 <see cref="Person.barbarianTribe"/>
    ///   · 之后所有判定都读字段（<see cref="Person.IsBarbarian"/>），不再走本表的字符串比较
    ///   · <see cref="Person.IsSpecialPerson"/>：事件 / NPC / 异族的统一排除口径
    ///   · <see cref="GovernanceValidator.ValidatePerson"/>：跳过这些人的归属巡检
    ///
    /// 【数据来源】Build/Content/Data/Common/PersonLibrary.json 的 722~729，
    /// 这 8 条在剧本里是 BelongCity = 0 的模板（本身就是"不落在城里"的合法状态）。
    /// 接入 ForceType 之后本表保留为兜底 —— 判"人"和判"势力"本来就是两件事。
    /// </summary>
    public static class BarbarianConfig
    {
        /// <summary>
        /// 异民族阵营。取值刻意与 Open311 的 ForceId 段对应，
        /// 后续接入势力层时可以直接映射成 0x2a~0x2d。
        /// </summary>
        public enum Tribe : int
        {
            /// <summary>不是异族</summary>
            None = 0,
            /// <summary>羌族（原版 ForceId_Kyou = 0x2a）</summary>
            Kyou = 1,
            /// <summary>山越（原版 ForceId_Sanetsu = 0x2b）</summary>
            Sanetsu = 2,
            /// <summary>乌丸（原版 ForceId_Ugan = 0x2c）</summary>
            Ugan = 3,
            /// <summary>南蛮（原版 ForceId_Nanban = 0x2d）</summary>
            Nanban = 4,
        }

        /// <summary>一条异族武将配置：Id 与名字必须**同时**匹配才算命中</summary>
        public struct Entry
        {
            /// <summary>武将 Id（PersonLibrary.json 的 Id 字段）</summary>
            public int Id;

            /// <summary>武将名（PersonLibrary.json 的 Name 字段，纯文本，不含颜色标记）</summary>
            public string Name;

            /// <summary>所属异民族阵营</summary>
            public Tribe Tribe;
        }

        /// <summary>
        /// 异族武将表：乌丸 / 羌族 / 山越 / 南蛮，各一个头目 + 一个武将。
        /// 顺序与 Open311 的 ForceId 段一致（Kyou → Sanetsu → Ugan → Nanban）。
        /// </summary>
        public static readonly Entry[] Entries =
        {
            new Entry { Id = 722, Name = "乌丸头目", Tribe = Tribe.Ugan },
            new Entry { Id = 723, Name = "乌丸武将", Tribe = Tribe.Ugan },
            new Entry { Id = 724, Name = "羌族头目", Tribe = Tribe.Kyou },
            new Entry { Id = 725, Name = "羌族武将", Tribe = Tribe.Kyou },
            new Entry { Id = 726, Name = "山越头目", Tribe = Tribe.Sanetsu },
            new Entry { Id = 727, Name = "山越武将", Tribe = Tribe.Sanetsu },
            new Entry { Id = 728, Name = "南蛮头目", Tribe = Tribe.Nanban },
            new Entry { Id = 729, Name = "南蛮武将", Tribe = Tribe.Nanban },
        };

        /// <summary>
        /// 按 Id + 名字取所属异民族阵营。
        /// </summary>
        /// <param name="id">武将 Id</param>
        /// <param name="name">武将名（纯文本）</param>
        /// <returns>命中的阵营；非异族返回 <see cref="Tribe.None"/></returns>
        public static Tribe GetTribe(int id, string name)
        {
            if (id <= 0 || string.IsNullOrEmpty(name))
                return Tribe.None;

            for (int i = 0; i < Entries.Length; i++)
            {
                // 双重校验：Id 与名字必须同时命中，任一不符都不认
                if (Entries[i].Id == id && Entries[i].Name == name)
                    return Entries[i].Tribe;
            }

            return Tribe.None;
        }

        /// <summary>
        /// 判定指定 Id / 名字是否为异族武将。
        /// </summary>
        /// <param name="id">武将 Id</param>
        /// <param name="name">武将名（纯文本）</param>
        /// <returns>是异族返回 true</returns>
        public static bool IsBarbarian(int id, string name)
        {
            return GetTribe(id, name) != Tribe.None;
        }

        /// <summary>
        /// 判定武将对象是否为异族（空对象返回 false）。
        ///
        /// **仅供固化与工具调用**：运行期判定请用 <see cref="Person.IsBarbarian"/>（读固化字段，O(1)），
        /// 不要在每个回合的流程里反复调本方法做字符串比较。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>是异族返回 true</returns>
        public static bool IsBarbarian(Person person)
        {
            if (person == null)
                return false;

            return IsBarbarian(person.Id, person.Name);
        }
    }
}
