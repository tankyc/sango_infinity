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
    /// 本项目暂时既没有 Force.ForceType，也没有根城（mainCamp）刷怪流程，
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
            Qiang = 1,
            /// <summary>山越（原版 ForceId_Sanetsu = 0x2b）</summary>
            Shanyue = 2,
            /// <summary>乌丸（原版 ForceId_Ugan = 0x2c）</summary>
            Wuhuan = 3,
            /// <summary>南蛮（原版 ForceId_Nanban = 0x2d）</summary>
            Nanman = 4,
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
        /// <summary>一条"异族范围城"配置：城 Id 与城名必须**同时**匹配</summary>
        public struct CityEntry
        {
            /// <summary>城 Id（Scenario.json 的 citySet.Id）</summary>
            public int Id;

            /// <summary>城名</summary>
            public string Name;

            /// <summary>该城所属的异族范围</summary>
            public Tribe Tribe;
        }

        /// <summary>
        /// 异族范围城 —— 对应原版 `SuppressedSkillCity`（`s11_db.cpp:243-248`）：
        ///   · 乌丸：襄平、北平、蓟
        ///   · 羌：安定、天水、武威
        ///   · 山越：吴、会稽、柴桑
        ///   · 南蛮：江州、建宁、云南
        ///
        /// 这些城的领内会孕育对应异族的根城，并需要「亲×」特技的守将压制
        /// （原版 `GameAI::set_guard` 的第一轮）。
        /// </summary>
        public static readonly CityEntry[] CityEntries =
        {
            // 乌丸
            new CityEntry { Id = 1,  Name = "襄平", Tribe = Tribe.Wuhuan },
            new CityEntry { Id = 2,  Name = "北平", Tribe = Tribe.Wuhuan },
            new CityEntry { Id = 3,  Name = "蓟",   Tribe = Tribe.Wuhuan },

            // 羌
            new CityEntry { Id = 20, Name = "安定", Tribe = Tribe.Qiang },
            new CityEntry { Id = 21, Name = "天水", Tribe = Tribe.Qiang },
            new CityEntry { Id = 22, Name = "武威", Tribe = Tribe.Qiang },

            // 山越
            new CityEntry { Id = 24, Name = "吴",   Tribe = Tribe.Shanyue },
            new CityEntry { Id = 25, Name = "会稽", Tribe = Tribe.Shanyue },
            new CityEntry { Id = 27, Name = "柴桑", Tribe = Tribe.Shanyue },

            // 南蛮
            new CityEntry { Id = 39, Name = "江州", Tribe = Tribe.Nanman },
            new CityEntry { Id = 41, Name = "建宁", Tribe = Tribe.Nanman },
            new CityEntry { Id = 42, Name = "云南", Tribe = Tribe.Nanman },
        };

        /// <summary>按「城 Id + 城名」双重校验查异族范围（None = 非异族范围）</summary>
        public static Tribe GetCityTribe(int cityId, string cityName)
        {
            if (cityId <= 0) return Tribe.None;

            for (int i = 0; i < CityEntries.Length; i++)
            {
                if (CityEntries[i].Id == cityId && CityEntries[i].Name == cityName)
                    return CityEntries[i].Tribe;
            }

            return Tribe.None;
        }

        /// <summary>
        /// 把异族范围固化到 <see cref="City.barbarianTribe"/>（**只在为 0 时写入**）。
        /// 调用时机：<c>City.OnScenarioPrepare</c>。
        /// </summary>
        public static void FixupCityTribe(City city)
        {
            if (city == null || city.barbarianTribe != 0) return;
            city.barbarianTribe = (int)GetCityTribe(city.Id, city.Name);
        }

        /// <summary>
        /// 异族范围 → 「亲×」特技 Id（`Features.json`）：92 亲乌 / 93 亲羌 / 94 亲越 / 95 亲蛮。
        /// 返回 0 表示该异族没有对应特技（不该出现，仅作兜底）。
        /// </summary>
        public static int GetGuardFeatureId(int tribe)
        {
            switch (tribe)
            {
                case (int)Tribe.Wuhuan: return 92;      // 亲乌
                case (int)Tribe.Qiang: return 93;      // 亲羌
                case (int)Tribe.Shanyue: return 94;   // 亲越
                case (int)Tribe.Nanman: return 95;    // 亲蛮
                default: return 0;
            }
        }

        public static readonly Entry[] Entries =
        {
            new Entry { Id = 722, Name = "乌丸头目", Tribe = Tribe.Wuhuan },
            new Entry { Id = 723, Name = "乌丸武将", Tribe = Tribe.Wuhuan },
            new Entry { Id = 724, Name = "羌族头目", Tribe = Tribe.Qiang },
            new Entry { Id = 725, Name = "羌族武将", Tribe = Tribe.Qiang },
            new Entry { Id = 726, Name = "山越头目", Tribe = Tribe.Shanyue },
            new Entry { Id = 727, Name = "山越武将", Tribe = Tribe.Shanyue },
            new Entry { Id = 728, Name = "南蛮头目", Tribe = Tribe.Nanman },
            new Entry { Id = 729, Name = "南蛮武将", Tribe = Tribe.Nanman },
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
