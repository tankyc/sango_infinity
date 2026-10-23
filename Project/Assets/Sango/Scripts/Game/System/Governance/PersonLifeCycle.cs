using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 寿命模式（对应 Open311 的 LifeModeEnum）。
    /// </summary>
    public enum LifeMode : int
    {
        /// <summary>史实：按武将死亡年推算，会正常老死</summary>
        Historical = 0,

        /// <summary>长寿：整体延后，势力不会因老将凋零断层</summary>
        Longevity = 1,

        /// <summary>虚构：不结算寿终（只有战死/处决/事件会死人）</summary>
        Fictional = 2,

        /// <summary>虚构无死亡：同虚构，保留给"英雄集结"类剧本</summary>
        Virtual = 3,
    }

    /// <summary>
    /// 死亡类型（对应 Open311 的 DeathTypeEnum）。
    /// </summary>
    public enum PersonDeathType : int
    {
        /// <summary>自然死亡（寿终、病逝）</summary>
        Natural = 0,

        /// <summary>非自然死亡（战死、处决），寿命会按能力值再扣减</summary>
        NonNatural = 1,
    }

    /// <summary>
    /// 武将生命周期结算（对应 Open311 的 Game::shoubyou_death + Game::update_shoubyou）。
    ///
    /// 【两段式寿终】原版不是"够岁数就死"，而是：
    ///   第 N 月：按月隔人抽样，命中者打上 EstimatedDeath 标记；
    ///   第 N+1 月：处决所有带标记的人。
    /// 这样死亡会分批发生，也给玩家留一回合缓冲（换太守、调整军队）。
    ///
    /// 【为什么隔月抽样】`id % 2 == month % 2` 让一半人在单月结算、一半人在双月，
    /// 把每月开销与死亡冲击都摊薄，同时保证每个人每两个月都会被检查一次。
    ///
    /// 【概率公式】原版：
    ///   chance = (life_mode_bonus + (year - estimated_death))
    ///            * shoubyou_coef[伤病等级] * mul
    ///   · mul = 4（自然死亡）/ 1（非自然死亡，且寿命再减 maxStat/20+5）
    ///   · 命中即 rand_bool(chance)，即 chance 个百分点
    /// </summary>
    public static class PersonLifeCycle
    {
        /// <summary>
        /// 长寿模式相对史实模式的延寿年数（对应 Open311 的 longevity_life_，默认 20）。
        /// </summary>
        public const int LongevityLife = 20;

        /// <summary>
        /// 各伤病等级对应的寿终概率系数（对应 Open311 的 shoubyou_coef = {1,2,3,4,10}）。
        /// 本项目伤病只有 4 档（健康/轻伤/中伤/重伤），取前 4 个值。
        /// </summary>
        private static readonly int[] InjuryCoef = { 1, 2, 3, 4 };

        /// <summary>
        /// 月度寿命结算：先处决上轮标记者，再抽样标记本轮。
        /// 对应 Open311 的 Game::shoubyou_death。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public static void MonthlySettlement(Scenario scenario)
        {
            if (scenario == null || scenario.Info == null || scenario.personSet == null)
                return;

            int year = scenario.Info.year;

            // 开局年份：老存档没有 startYear，按当前年兜底（等同"当年开局"）。
            // 兜底的同时**写回**——否则每次读档都会重新兜底成"当前年"，
            // 表现为"反复存读档就永远等不到寿终"。
            if (scenario.Info.startYear <= 0)
                scenario.Info.startYear = year;
            int startYear = scenario.Info.startYear;

            // 开局第一年不结算：避免剧本里本该已死的武将第一个月集体去世
            if (year - startYear < 1)
                return;

            // ---------- 第一步：处决上一轮被标记的人 ----------
            List<Person> dying = new List<Person>();
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (!person.HasLifeCycle)
                    continue;
                if (!person.IsEstimatedDeath)
                    continue;
                dying.Add(person);
            }

            // 身份低的先死（君主最后走）：state 数值越大地位越低
            dying.Sort((a, b) =>
            {
                if (a.state != b.state)
                    return b.state.CompareTo(a.state);
                return a.Id.CompareTo(b.Id);
            });

            for (int i = 0; i < dying.Count; i++)
            {
                Person person = dying[i];
                if (person == null || !person.IsAlive)
                    continue;
                Sango.Log.Info($"*[寿终]{person.Name}（{person.Age}岁）寿终正寝");
                person.estimatedDeath = false;
                person.Dead();
            }

            // ---------- 第二步：本轮抽样 ----------
            LifeMode mode = (LifeMode)scenario.Variables.lifeMode;
            int lifeBonus;
            switch (mode)
            {
                case LifeMode.Historical:
                    lifeBonus = 1;
                    break;
                case LifeMode.Longevity:
                    lifeBonus = -LongevityLife + 1;
                    break;
                default:
                    // 虚构 / 虚构无死亡：不结算寿终
                    return;
            }

            int monthKey = scenario.Info.month % 2;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (!person.HasLifeCycle)
                    continue;

                // 隔月抽样：一半人在单月、一半人在双月
                if (person.Id % 2 != monthKey)
                    continue;

                int estimatedDeath = person.GetEstimatedDeathYear();
                if (estimatedDeath == int.MaxValue)
                    continue;

                int life = year - estimatedDeath;
                int mul = 4;

                if (person.deathType == (int)PersonDeathType.NonNatural)
                {
                    // 非自然死亡者：寿命按最突出的能力值再扣，抽样系数降为 1
                    life -= MaxStat(person) / 20 + 5;
                    mul = 1;
                }

                if (life < 0)
                    continue;

                int injury = person.injury;
                if (injury < 0) injury = 0;
                if (injury >= InjuryCoef.Length) injury = InjuryCoef.Length - 1;

                int chance = (lifeBonus + life) * InjuryCoef[injury] * mul;
                if (chance > 0 && GameRandom.Range(0, 100) < chance)
                    person.estimatedDeath = true;
            }
        }

        /// <summary>
        /// 月度伤病更新（对应 Open311 的 Game::update_shoubyou）。
        ///
        /// 两段式，顺序不能换：
        ///   ① 有伤病 → 恢复一级（原版 rand_bool(100) 恒真）→ **本月不再患病**
        ///   ② 没伤病且已超过寿命 → 按概率患病
        ///
        /// 之所以"有病就不判患病"，是因为原版在恢复分支末尾直接 `continue`：
        /// 一个人不可能同一个月既在康复又被新病击倒。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public static void MonthlyInjuryUpdate(Scenario scenario)
        {
            if (scenario == null || scenario.Info == null || scenario.personSet == null)
                return;

            int year = scenario.Info.year;
            int monthKey = scenario.Info.month % 2;
            LifeMode mode = (LifeMode)scenario.Variables.lifeMode;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;

                // 原版 is_event 排除；临时武将与事件同类，同样跳过
                if (!person.HasLifeCycle)
                    continue;

                // 原版 test_mibun({Kunshu, Totoku, Taishu, Ippan, Zaiya, Horyo})：
                // 只有在籍 / 在野 / 俘虏会自然患病，未登场、未发现、死亡不参与
                PersonStateType st = (PersonStateType)person.state;
                if (st != PersonStateType.Governor
                    && st != PersonStateType.Commander
                    && st != PersonStateType.Leader
                    && st != PersonStateType.Normal
                    && st != PersonStateType.Unemployed
                    && st != PersonStateType.Prisoner)
                    continue;

                if (person.Id % 2 != monthKey)
                    continue;

                // ① 有伤病：恢复一级，本月不再患病
                if (person.injury > 0)
                {
                    person.injury--;
                    continue;
                }

                // ② 虚构模式不患病
                if (mode == LifeMode.Fictional || mode == LifeMode.Virtual)
                    continue;

                // 寿命基准：非史实模式统一按长寿年（原版直接把 estimated_death 赋成长寿年）
                int estimatedDeath = mode == LifeMode.Longevity
                    ? LongevityLife
                    : person.GetEstimatedDeathYear();

                if (estimatedDeath == int.MaxValue)
                    continue;

                // 非自然死亡者寿命后移（与寿终抽样里的扣减同一套折算）
                if (person.deathType == (int)PersonDeathType.NonNatural)
                    estimatedDeath += MaxStat(person) / 20 + 5;

                if (year <= estimatedDeath)
                    continue;

                // 超龄越久越容易病倒：每年 +3 个百分点，至少 3%
                int n = (year - estimatedDeath) * 3;
                if (n < 3) n = 3;
                if (GameRandom.Range(0, 100) >= n)
                    continue;

                // 患病等级：默认轻伤，年龄越大越可能直接重伤
                int level = 1;
                int age = person.Age;
                if (age > 0 && GameRandom.Range(0, 100) < age / 10)
                    level = Person.InjuryMaxLevel;

                person.injury = level;

                string symptom = level >= Person.InjuryMaxLevel ? "重伤" : "患病";
                Sango.Log.Info($"*[伤病]{person.Name}（{person.Age}岁）因年迈{symptom}");
            }
        }

        /// <summary>
        /// 年度关系清理（对应 Open311 Game::init_person 里的关系维护段）。
        ///
        /// 只处理**历史武将**：把他们的配偶 / 义兄弟 / 亲爱 / 厌恶列表里
        /// 指向"已死亡武将"的项摘掉。
        ///
        /// 为什么要清：这些列表是对象引用 + Id 数组双份存储，死人不摘掉会导致
        ///   · UI 上显示一个已死的配偶；
        ///   · 忠诚/单挑等判定仍把他算作关系人（`IsLike(deadPerson)` 返回 true）；
        ///   · 读档时把死人对象重新解析进列表，形成"指向已死亡对象"的悬空引用。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public static void AnnualRelationshipCleanup(Scenario scenario)
        {
            if (scenario == null || scenario.personSet == null)
                return;

            int cleaned = 0;

            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (!person.IsHistorical)
                    continue;

                cleaned += CleanList(person.SpouseList, ref person.SpouseListId);
                cleaned += CleanList(person.LikePersonList, ref person.LikePersonListId);
                cleaned += CleanList(person.HatePersonList, ref person.HatePersonListId);

                // 义兄弟是单引用
                if (person.Brother != null && person.Brother.IsDead)
                {
                    person.Brother = null;
                    person.BrotherId = 0;
                    cleaned++;
                }
            }

            if (cleaned > 0)
                Sango.Log.Info($"*[生命周期]年度关系清理：摘除 {cleaned} 条指向已故武将的关系");
        }

        /// <summary>
        /// 开局配属：把"没有剧本指定归属"的自建 / 古代武将随机安置到一座城（在野状态）。
        ///
        /// 对应 Open311 Game::init_person 里的 person_dispatch 段：
        /// 遍历 is_ancient() / is_custom() 的武将尝试配属，配不上的 `set_mibun(-1)` 退场。
        /// 本项目没有"配属失败"的概念（城池总是存在的），所以规则简化为：
        /// 只要归属为空且状态为 Invalid/Error，就随机落到一座现存都市当在野武将。
        ///
        /// 只处理自建 / 古代：历史武将的归属由剧本决定，缺了属于剧本数据错误，
        /// 应交给归属巡检报出来，不该在这里悄悄补上。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        public static void EnsureSpawned(Scenario scenario)
        {
            if (scenario == null || scenario.personSet == null || scenario.citySet == null)
                return;

            List<City> cities = new List<City>();
            for (int i = 0; i < scenario.citySet.Count; i++)
            {
                City city = scenario.citySet[i];
                if (city != null && city.IsAlive && city.IsCity())
                    cities.Add(city);
            }

            if (cities.Count == 0)
                return;

            int spawned = 0;
            for (int i = 0; i < scenario.personSet.Count; i++)
            {
                Person person = scenario.personSet[i];
                if (person == null || !person.IsAlive || person.IsDead)
                    continue;
                if (!person.IsCustom && !person.IsAncient)
                    continue;

                // 已有归属或在野落地的跳过
                if (person.BelongCity != null || person.CurrentCity != null)
                    continue;

                // 只处理"尚未安排"的状态
                if (person.state != (int)PersonStateType.Invalid && person.state != (int)PersonStateType.Error)
                    continue;

                City target = cities[GameRandom.Range(0, cities.Count)];
                PersonStateSystem.LeaveToWild(person, target, PersonChangeReason.Appeared);
                spawned++;
            }

            if (spawned > 0)
                Sango.Log.Info($"*[配属]{spawned} 名自建/古代武将随机落地（在野）");
        }

        /// <summary>
        /// 摘除列表里指向"已死或已失效"武将的项，并同步 Id 数组。
        /// </summary>
        /// <param name="list">对象列表</param>
        /// <param name="ids">对应的 Id 数组（会被重写）</param>
        /// <returns>摘除的条数</returns>
        private static int CleanList(SangoObjectList<Person> list, ref int[] ids)
        {
            if (list == null || list.Count == 0)
                return 0;

            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Person other = list[i];
                if (other != null && other.IsAlive && !other.IsDead)
                    continue;

                list.Remove(other);
                removed++;
            }

            if (removed > 0)
            {
                int[] newIds = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                    newIds[i] = list[i] != null ? list[i].Id : 0;
                ids = newIds;
            }

            return removed;
        }

        /// <summary>
        /// 五维里的最高值（原版用 max_stat 折算非自然死亡的寿命扣减）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <returns>最高能力值</returns>
        private static int MaxStat(Person person)
        {
            int max = person.Command;
            if (person.Strength > max) max = person.Strength;
            if (person.Intelligence > max) max = person.Intelligence;
            if (person.Politics > max) max = person.Politics;
            if (person.Glamour > max) max = person.Glamour;
            return max;
        }
    }
}
