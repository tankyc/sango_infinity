using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 势力爵位 / 称号晋升系统（月度结算）。
    ///
    /// 对齐 Open311 的 <c>Game::title_rises</c>（s11_game_month.cpp:1772-1790）：每月结算时，
    /// 对每个可受爵的势力按当前爵位、城池数、汉室状态（尚存/衰微）与是否保护皇帝，晋升一级，
    /// 并产生三类演出分支：
    ///   · 爵位授予 / 自称（无~大将军）—— 对应汉帝授予 / 自称爵位；
    ///   · 王公授予 / 自称（大司马 / 公）；
    ///   · 称帝（王 → 皇帝：禅让 / 篡夺 / 自称）。
    ///
    /// 【汉室状态的识别】对齐原版：皇帝是剧本里真实存在的武将「献帝」（见
    /// <see cref="Scenario.emperorPersonId"/>，默认 703、类型为事件武将 type=2），而不是布尔开关。
    ///   · 汉室尚存 = 皇帝武将存在且存活；
    ///   · 保护皇帝势力 = 皇帝武将所在城的归属势力（原版 <c>is_protecting_the_emperor</c> 派生路径）；
    ///   · 禅让 / 篡夺 = 保护皇帝势力的王晋升时，置 <see cref="Scenario.emperorPersonId"/> = -1（皇帝退位）。
    /// 皇帝死亡更替（灵帝→少帝→献帝→无，<c>is_emperor_dead</c>）属 D20 二期，暂未实现。
    /// </summary>
    [GameSystem(order = 110)]
    public class TitlePromotionSystem : GameSystem
    {
        // ====================================================================
        // 爵位 Id 常量（对齐本项目 Titles.json：Id 越大爵位越高，1=无 … 10=皇帝）。
        // 与原版 TitleId 枚举（s11_enum_preset.h，0=皇帝 … 9=无）**序号相反**：
        // 本项目 titleId = 10 - 原版 titleId。
        // ====================================================================
        const int TitleIdEmperor = 10;            // 皇帝（原版 TitleId_Emperor = 0）
        const int TitleIdKing = 9;                // 王（原版 TitleId_King = 1）
        const int TitleIdHighLord = 8;            // 公（原版 TitleId_HighLord = 2）
        const int TitleIdCommanderInChief = 6;    // 大将军（原版 TitleId_CommanderInChief = 4）
        const int TitleIdNone = 1;                // 无（原版 TitleId_None = 9）

        /// <summary>献帝武将 Id（对齐原版 PersonId_Kentei；本项目武将库中献帝 Id = 703）</summary>
        const int PersonIdKentei = 703;

        /// <summary>黄巾国号 Id（对齐原版 KokugouId_YellowTurban，本项目 Nations.json 中黄巾为 6）</summary>
        const int NationIdYellowTurban = 6;

        // 汉室态度（kanshitsu），对齐 Open311 KanshitsuEnum（s11_enum_scen.h:406-412）
        const int KanshitsuIgnore = 0;      // 无视
        const int KanshitsuNormal = 1;      // 普通
        const int KanshitsuImportance = 2;  // 重视

        public override void Init()
        {
            GameEvent.OnMonthUpdate += OnMonthStart;
        }

        public override void Clear()
        {
            GameEvent.OnMonthUpdate -= OnMonthStart;
        }

        /// <summary>月度结算入口（订阅 OnMonthUpdate，与 CitySecurity/CityBusiness 同约定）。</summary>
        /// <param name="scenario">当前剧本</param>
        public void OnMonthStart(Scenario scenario)
        {
            TitleRises(scenario);
        }

        // ====================================================================
        // 对齐 Game::title_rises（s11_game_month.cpp:1772-1790）
        // ====================================================================
        void TitleRises(Scenario scenario)
        {
            if (scenario == null || scenario.forceSet == null)
                return;

            // 保护皇帝势力（若有）。原版通过皇帝人物 → 所在城 → 势力派生。
            Force emperorForce = GetEmperorProtectingForce(scenario);

            // 正常势力列表（异民族/贼势力排除），保护皇帝势力排最前（s11_game_month.cpp:1775-1780）
            List<Force> forceList = new List<Force>();
            scenario.forceSet.ForEach(f =>
            {
                if (f != null && f.IsAlive && f.IsNormal)
                    forceList.Add(f);
            });
            if (emperorForce != null)
            {
                forceList.Remove(emperorForce);
                forceList.Insert(0, emperorForce);
            }

            // 一次性过滤不可受爵的势力（s11_game_month.cpp:1781-1783，先判后晋升）
            forceList.RemoveAll(f => !CanReceiveTitle(f, scenario));

            // 逐个晋升（s11_game_month.cpp:1784-1789）
            for (int i = 0; i < forceList.Count; i++)
            {
                TitleEventCore(forceList[i], scenario);
            }
        }

        /// <summary>取当前皇帝武将（无则返回 null）。</summary>
        Person GetEmperor(Scenario scenario)
        {
            if (scenario == null || scenario.emperorPersonId <= 0 || scenario.personSet == null)
                return null;
            return scenario.personSet.Get(scenario.emperorPersonId);
        }

        /// <summary>
        /// 汉室是否尚存。对齐原版 <c>get_emperor_id() &gt;= 0</c> 及皇帝存活判定：
        /// 皇帝武将存在且存活即「汉室尚存」。
        /// </summary>
        bool IsEmperorAlive(Scenario scenario)
        {
            Person emperor = GetEmperor(scenario);
            return emperor != null && emperor.IsAlive && !emperor.IsDead;
        }

        /// <summary>
        /// 找出保护皇帝势力。对齐 Game::get_emperor_protecting_force（s11_game_month.cpp:1792-1801）：
        /// 原版派生自皇帝人物所在城的归属势力，这里同源派生。
        /// </summary>
        Force GetEmperorProtectingForce(Scenario scenario)
        {
            Person emperor = GetEmperor(scenario);
            if (emperor == null || !emperor.IsAlive || emperor.IsDead)
                return null;
            return emperor.BelongCity != null ? emperor.BelongCity.BelongForce : null;
        }

        /// <summary>
        /// 能否受爵（晋升）。对齐 Game::can_receive_title（s11_game_month.cpp:1803-1820）。
        /// </summary>
        bool CanReceiveTitle(Force force, Scenario scenario)
        {
            // 原版：未存活 或 黄巾国号 → false（s11_game_month.cpp:1805-1806）
            if (force == null || !force.IsAlive || force.IsBandit || force.NationId == NationIdYellowTurban)
                return false;

            // 原版：已是皇帝 → false（s11_game_month.cpp:1808-1809）
            int titleId = GetCurrentRank(force);
            if (titleId >= TitleIdEmperor)
                return false;

            // 原版：君主不可会面 → false（s11_game_month.cpp:1810-1812）
            if (!CanMeeting(force.mGovernor))
                return false;

            // 原版：城池数 < 当前爵位的晋升阈值 → false（s11_game_month.cpp:1813-1815）
            int maxCityCount = GetMaxCityCount(scenario, titleId);
            if (force.CityCount < maxCityCount)
                return false;

            // 原版：王及以上（即将称帝）且皇帝存活 → 仅保护皇帝势力可称帝（s11_game_month.cpp:1816-1818）
            if (titleId >= TitleIdKing && IsEmperorAlive(scenario))
                return GetEmperorProtectingForce(scenario) == force;

            return true;
        }

        /// <summary>
        /// 君主能否会面（主持晋升）。对齐 System::can_meeting（s11_sys_event_utils.cpp:122-131）：
        /// 非空、未死亡、非俘虏、不在部队中。
        /// </summary>
        bool CanMeeting(Person person)
        {
            if (person == null)
                return false;
            if (person.IsDead || person.IsPrisoner)
                return false;
            if (person.mBelongTroop != null)
                return false;
            return true;
        }

        /// <summary>
        /// 取势力当前爵位（归一化）。TitleId 未设置（&lt;=0）视为「无」（最低爵位）。
        /// </summary>
        int GetCurrentRank(Force force)
        {
            int id = force != null ? force.TitleId : 0;
            return id <= 0 ? TitleIdNone : id;
        }

        /// <summary>取某爵位的晋升阈值（对齐 Title::max_city_count_，s11_scen.h:2145 / s11_db.cpp:238）。</summary>
        int GetMaxCityCount(Scenario scenario, int titleId)
        {
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Titles == null)
                return int.MaxValue; // 取不到配置时视为不可晋升，避免误晋升
            Title title = scenario.CommonData.Titles.Get(titleId);
            return title != null ? title.maxCityCount : int.MaxValue;
        }

        // ====================================================================
        // 对齐 System::title_event（s11_sys_event_title.cpp:423-502）
        // 按当前爵位分三档：爵位（1~6）→ 王公（7~8）→ 皇帝（9）。
        // ====================================================================
        void TitleEventCore(Force force, Scenario scenario)
        {
            if (force == null || scenario == null)
                return;

            int titleId = GetCurrentRank(force);
            int newTitleId = titleId + 1;
            bool emperorAlive = IsEmperorAlive(scenario);

            // 一、爵位事件：无~大将军（原版 old_title_id >= TitleId_CommanderInChief，s11_sys_event_title.cpp:430-445）
            if (titleId <= TitleIdCommanderInChief)
            {
                if (emperorAlive)
                {
                    // 爵位授予（汉帝授予）。忠诚变化(无视/普通/重视) = (1,3,6)，城池无额外效果
                    TitleEventChange(force, scenario, newTitleId, 1, 3, 6, 0, 0);
                    Sango.Log.Info($"爵位晋升：{force.Name} 受汉帝授予，由【{TitleName(scenario, titleId)}】晋升为【{TitleName(scenario, newTitleId)}】");
                }
                else
                {
                    // 爵位自称。忠诚变化 = (6,3,1)
                    TitleEventChange(force, scenario, newTitleId, 6, 3, 1, 0, 0);
                    Sango.Log.Info($"爵位晋升：{force.Name} 自称爵位，由【{TitleName(scenario, titleId)}】晋升为【{TitleName(scenario, newTitleId)}】");
                }
                return;
            }

            // 二、王公事件：大司马/公（原版 old_title_id >= TitleId_HighLord，s11_sys_event_title.cpp:448-463）
            if (titleId <= TitleIdHighLord)
            {
                if (emperorAlive)
                {
                    // 王公授予。忠诚(2,6,12)，城池治安+10 气力+10
                    TitleEventChange(force, scenario, newTitleId, 2, 6, 12, 10, 10);
                    Sango.Log.Info($"爵位晋升：{force.Name} 受汉帝册封王公，由【{TitleName(scenario, titleId)}】晋升为【{TitleName(scenario, newTitleId)}】");
                }
                else
                {
                    // 王公自称。忠诚(12,6,2)，城池治安+10 气力+10
                    TitleEventChange(force, scenario, newTitleId, 12, 6, 2, 10, 10);
                    Sango.Log.Info($"爵位晋升：{force.Name} 自称王公，由【{TitleName(scenario, titleId)}】晋升为【{TitleName(scenario, newTitleId)}】");
                }
                return;
            }

            // 三、皇帝事件：王（原版 else 分支，s11_sys_event_title.cpp:465-501）
            if (titleId >= TitleIdKing)
            {
                if (GetEmperorProtectingForce(scenario) == force)
                {
                    // 保护皇帝 → 禅让 或 篡夺（按君主汉室态度），并宣告汉室衰微
                    if (IsZenjou(force, scenario))
                    {
                        // 禅让。忠诚(3,9,18)，城池治安+15 气力+15
                        TitleEventChange(force, scenario, newTitleId, 3, 9, 18, 15, 15);
                        scenario.emperorPersonId = -1;
                        Sango.Log.Info($"称帝：{force.Name} 受汉帝禅让，由【王】即位为【皇帝】，汉室衰微");
                    }
                    else
                    {
                        // 篡夺。忠诚(18,9,0)，城池治安+10 气力+10
                        TitleEventChange(force, scenario, newTitleId, 18, 9, 0, 10, 10);
                        scenario.emperorPersonId = -1;
                        Sango.Log.Info($"称帝：{force.Name} 篡夺汉室，由【王】即位为【皇帝】，汉室衰微");
                    }
                }
                else
                {
                    // 皇帝自称。忠诚(18,9,3)，城池治安+15 气力+15
                    TitleEventChange(force, scenario, newTitleId, 18, 9, 3, 15, 15);
                    Sango.Log.Info($"称帝：{force.Name} 自立为帝，由【王】即位为【皇帝】");
                }
                return;
            }
        }

        /// <summary>
        /// 保护皇帝的势力称帝时，走禅让还是篡夺。对齐 s11_sys_event_title.cpp:466-496：
        /// 君主汉室态度为「普通」且满治安城池 ≥15 → 禅让；「无视」或不足 15 → 篡夺；「重视」→ 禅让。
        /// </summary>
        bool IsZenjou(Force force, Scenario scenario)
        {
            Person kunshu = force.mGovernor;
            if (kunshu == null || !kunshu.IsAlive || kunshu.IsDead)
                return true; // 原版：君主非存活则跳过 switch，落入禅让分支

            switch (kunshu.kanshitsu)
            {
                case KanshitsuNormal:
                {
                    // 满治安正规城池数 ≥ 15 → 禅让，否则篡夺
                    int fullOrderCity = 0;
                    scenario.citySet.ForEach(city =>
                    {
                        if (city != null && city.IsCity() && city.BelongForce == force && city.security >= 100)
                            fullOrderCity++;
                    });
                    return fullOrderCity >= 15;
                }
                case KanshitsuIgnore:
                    return false; // 篡夺
                case KanshitsuImportance:
                default:
                    return true; // 禅让（未分类亦按禅让，对齐原版 switch 无 default 时落入禅让）
            }
        }

        /// <summary>
        /// 变更爵位并结算副作用。对齐 System::title_event_change（s11_sys_event_title.cpp:302-331）：
        /// ① 设置新爵位；② 按汉室态度给势力武将（除君主外）加忠诚；③ 城池治安/气力（有驻军才加气力）。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="scenario">剧本</param>
        /// <param name="newTitleId">新爵位 Id</param>
        /// <param name="ignoreLoyalty">汉室态度=无视 的忠诚变化</param>
        /// <param name="normalLoyalty">汉室态度=普通 的忠诚变化</param>
        /// <param name="importanceLoyalty">汉室态度=重视 的忠诚变化</param>
        /// <param name="cityPublicOrder">城池治安变化</param>
        /// <param name="cityEnergy">城池气力变化</param>
        void TitleEventChange(Force force, Scenario scenario, int newTitleId,
            int ignoreLoyalty, int normalLoyalty, int importanceLoyalty,
            int cityPublicOrder, int cityEnergy)
        {
            if (force == null || !force.IsAlive)
                return;

            // ① 设置新爵位（原版 force_set_title_id，s11_sys_event_title.cpp:306）
            Title newTitle = scenario.CommonData != null && scenario.CommonData.Titles != null
                ? scenario.CommonData.Titles.Get(newTitleId) : null;
            if (newTitle != null)
                force.Title = newTitle;
            else
                force.TitleId = newTitleId;

            // ② 按汉室态度给势力武将（除君主外）加忠诚（s11_sys_event_title.cpp:307-318）
            int[] loyaltyByKanshitsu = { ignoreLoyalty, normalLoyalty, importanceLoyalty };
            scenario.personSet.ForEach(person =>
            {
                if (person == null || person == force.mGovernor)
                    return;
                if (person.BelongForce != force)
                    return;
                if (!person.IsAlive || person.IsDead)
                    return;
                if (person.kanshitsu >= 0 && person.kanshitsu < loyaltyByKanshitsu.Length)
                    person.AddLoyalty(loyaltyByKanshitsu[person.kanshitsu]);
            });

            // ③ 城池治安/气力（s11_sys_event_title.cpp:319-330；原版只对正规城池，气力仅在驻军时加）
            if (cityPublicOrder != 0 || cityEnergy != 0)
            {
                scenario.citySet.ForEach(city =>
                {
                    if (city == null || !city.IsCity() || city.BelongForce != force)
                        return;
                    if (cityPublicOrder != 0)
                        city.AddSecurity(cityPublicOrder);
                    if (cityEnergy != 0 && city.troops > 0)
                    {
                        city.morale += cityEnergy;
                        if (city.morale < 0)
                            city.morale = 0;
                    }
                });
            }
        }

        /// <summary>取爵位显示名（日志用）。</summary>
        string TitleName(Scenario scenario, int titleId)
        {
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Titles == null)
                return titleId.ToString();
            Title title = scenario.CommonData.Titles.Get(titleId);
            return title != null ? title.Name : titleId.ToString();
        }
    }
}
