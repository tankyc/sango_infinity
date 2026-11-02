/*
 * 文件名：CombatExpTable.cs
 * 描述：战斗经验表 —— 逐格照抄 Open311 的 Exp 表。
 *
 * 【原版出处】
 *   结构：s11_scen.h:3557-3564
 *     struct Exp { array<int,5> stat_exp; array<int,6> heishu_exp; int kouseki; int tech_point; };
 *   表体：s11_scen.cpp:46-144（Scenario::Scenario() 里的 exp_ 初始化，共 0x00~0x60 共 97 项）
 *   索引：s11_enum_preset.h:2626-2726 的 ExpEnum
 *
 * 【为什么单独建表而不是塞进 GainValueConfig】
 *   GainValueConfig 是"一个地点 -> 一个数"的扁平键值表，适合"击破城池给多少功绩"这类离散点。
 *   原版这张表是 **二维** 的：同一行里既有 5 项属性经验、6 项兵种适性经验，又有功绩和技巧点，
 *   而且行数 97、彼此还有联动（暴击行通常是命中行的 2 倍功绩、战法 miss 行技巧点为 0）。
 *   拆成 97×4 个扁平键会彻底看不出结构，改一个战法要跨四个分区改四行。
 *   因此这里保留二维结构，默认值 = 原版数据（本文件即"原版数据"的唯一落点），
 *   剧本若要调节奏，走 <see cref="ScenarioVariables"/> 的覆盖机制（后续接入），
 *   而 GainValueConfig 继续负责离散获取点（劝降功绩、单挑、舌战、事件奖励等）。
 */
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Sango.Mod;

namespace Sango.Core
{
    /// <summary>
    /// 战斗经验事件。数值与取值范围全部照抄原版 <c>ExpEnum</c>
    /// （s11_enum_preset.h:2626-2726），**不要重新排序**：表体按这个值直接索引。
    /// </summary>
    public enum CombatExpEvent
    {
        /// <summary>普攻暴击（Exp_AttackCritical）</summary>
        AttackCritical = 0x00,
        /// <summary>普攻命中（Exp_Attack）</summary>
        Attack = 0x01,
        /// <summary>Exp_2（原版预留，未使用）</summary>
        Reserved2 = 0x02,
        /// <summary>Exp_3（原版预留，未使用）</summary>
        Reserved3 = 0x03,
        /// <summary>一齐攻击暴击（Exp_SurroundCritical）</summary>
        SurroundCritical = 0x04,
        /// <summary>一齐攻击（Exp_Surround）</summary>
        Surround = 0x05,
        /// <summary>突刺暴击（Exp_TsukidashiCritical）</summary>
        TsukidashiCritical = 0x06,
        /// <summary>突刺命中（Exp_Tsukidashi）</summary>
        Tsukidashi = 0x07,
        /// <summary>突刺未命中（Exp_TsukidashiMiss）</summary>
        TsukidashiMiss = 0x08,
        /// <summary>螺旋突刺暴击（Exp_RasenCritical）</summary>
        RasenCritical = 0x09,
        /// <summary>螺旋突刺命中（Exp_Rasen）</summary>
        Rasen = 0x0a,
        /// <summary>螺旋突刺未命中（Exp_RasenMiss）</summary>
        RasenMiss = 0x0b,
        /// <summary>二段突刺暴击（Exp_NidandukiCritical）</summary>
        NidandukiCritical = 0x0c,
        /// <summary>二段突刺命中（Exp_Nidanduki）</summary>
        Nidanduki = 0x0d,
        /// <summary>二段突刺未命中（Exp_NidandukiMiss）</summary>
        NidandukiMiss = 0x0e,
        /// <summary>熊手暴击（Exp_KumadeCritical）</summary>
        KumadeCritical = 0x0f,
        /// <summary>熊手命中（Exp_Kumade）</summary>
        Kumade = 0x10,
        /// <summary>熊手未命中（Exp_KumadeMiss）</summary>
        KumadeMiss = 0x11,
        /// <summary>横扫暴击（Exp_YokonagiCritical）</summary>
        YokonagiCritical = 0x12,
        /// <summary>横扫命中（Exp_Yokonagi）</summary>
        Yokonagi = 0x13,
        /// <summary>横扫未命中（Exp_YokonagiMiss）</summary>
        YokonagiMiss = 0x14,
        /// <summary>旋风暴击（Exp_SenpuuCritical）</summary>
        SenpuuCritical = 0x15,
        /// <summary>旋风命中（Exp_Senpuu）</summary>
        Senpuu = 0x16,
        /// <summary>旋风未命中（Exp_SenpuuMiss）</summary>
        SenpuuMiss = 0x17,
        /// <summary>火矢暴击（Exp_HiyaCritical）</summary>
        HiyaCritical = 0x18,
        /// <summary>火矢命中（Exp_Hiya）</summary>
        Hiya = 0x19,
        /// <summary>火矢未命中（Exp_HiyaMiss）</summary>
        HiyaMiss = 0x1a,
        /// <summary>贯箭暴击（Exp_TsuranukiyaCritical）</summary>
        TsuranukiyaCritical = 0x1b,
        /// <summary>贯箭命中（Exp_Tsuranukiya）</summary>
        Tsuranukiya = 0x1c,
        /// <summary>贯箭未命中（Exp_TsuranukiyaMiss）</summary>
        TsuranukiyaMiss = 0x1d,
        /// <summary>乱射暴击（Exp_RanshaCritical）</summary>
        RanshaCritical = 0x1e,
        /// <summary>乱射命中（Exp_Ransha）</summary>
        Ransha = 0x1f,
        /// <summary>乱射未命中（Exp_RanshaMiss）</summary>
        RanshaMiss = 0x20,
        /// <summary>突击暴击（Exp_TotsugekiCritical）</summary>
        TotsugekiCritical = 0x21,
        /// <summary>突击命中（Exp_Totsugeki）</summary>
        Totsugeki = 0x22,
        /// <summary>突击未命中（Exp_TotsugekiMiss）</summary>
        TotsugekiMiss = 0x23,
        /// <summary>突破暴击（Exp_ToppaCritical）</summary>
        ToppaCritical = 0x24,
        /// <summary>突破命中（Exp_Toppa）</summary>
        Toppa = 0x25,
        /// <summary>突破未命中（Exp_ToppaMiss）</summary>
        ToppaMiss = 0x26,
        /// <summary>突进暴击（Exp_TosshinCritical）</summary>
        TosshinCritical = 0x27,
        /// <summary>突进命中（Exp_Tosshin）</summary>
        Tosshin = 0x28,
        /// <summary>突进未命中（Exp_TosshinMiss）</summary>
        TosshinMiss = 0x29,
        /// <summary>破碎暴击（Exp_HasaiCritical）</summary>
        HasaiCritical = 0x2a,
        /// <summary>破碎命中（Exp_Hasai）</summary>
        Hasai = 0x2b,
        /// <summary>破碎未命中（Exp_HasaiMiss）</summary>
        HasaiMiss = 0x2c,
        /// <summary>放射暴击（Exp_HoushaCritical）</summary>
        HoushaCritical = 0x2d,
        /// <summary>放射命中（Exp_Housha）</summary>
        Housha = 0x2e,
        /// <summary>放射未命中（Exp_HoushaMiss）</summary>
        HoushaMiss = 0x2f,
        /// <summary>投石暴击（Exp_TousekiCritical）</summary>
        TousekiCritical = 0x30,
        /// <summary>投石命中（Exp_Touseki）</summary>
        Touseki = 0x31,
        /// <summary>投石未命中（Exp_TousekiMiss）</summary>
        TousekiMiss = 0x32,
        /// <summary>猛撞暴击（Exp_GekitotsuCritical）</summary>
        GekitotsuCritical = 0x33,
        /// <summary>猛撞命中（Exp_Gekitotsu）</summary>
        Gekitotsu = 0x34,
        /// <summary>猛撞未命中（Exp_GekitotsuMiss）</summary>
        GekitotsuMiss = 0x35,
        /// <summary>火计暴击（Exp_KakeiCritical）</summary>
        KakeiCritical = 0x36,
        /// <summary>火计命中（Exp_Kakei）</summary>
        Kakei = 0x37,
        /// <summary>火计未命中（Exp_KakeiMiss）</summary>
        KakeiMiss = 0x38,
        /// <summary>灭火暴击（Exp_ShoukaCritical）</summary>
        ShoukaCritical = 0x39,
        /// <summary>灭火命中（Exp_Shouka）</summary>
        Shouka = 0x3a,
        /// <summary>灭火未命中（Exp_ShoukaMiss）</summary>
        ShoukaMiss = 0x3b,
        /// <summary>伪报暴击（Exp_GihouCritical）</summary>
        GihouCritical = 0x3c,
        /// <summary>伪报命中（Exp_Gihou）</summary>
        Gihou = 0x3d,
        /// <summary>伪报未命中（Exp_GihouMiss）</summary>
        GihouMiss = 0x3e,
        /// <summary>扰乱暴击（Exp_KakuranCritical）</summary>
        KakuranCritical = 0x3f,
        /// <summary>扰乱命中（Exp_Kakuran）</summary>
        Kakuran = 0x40,
        /// <summary>扰乱未命中（Exp_KakuranMiss）</summary>
        KakuranMiss = 0x41,
        /// <summary>镇静暴击（Exp_ChinseiCritical）</summary>
        ChinseiCritical = 0x42,
        /// <summary>镇静命中（Exp_Chinsei）</summary>
        Chinsei = 0x43,
        /// <summary>镇静未命中（Exp_ChinseiMiss）</summary>
        ChinseiMiss = 0x44,
        /// <summary>伏兵暴击（Exp_FukuheiCritical）</summary>
        FukuheiCritical = 0x45,
        /// <summary>伏兵命中（Exp_Fukuhei）</summary>
        Fukuhei = 0x46,
        /// <summary>伏兵未命中（Exp_FukuheiMiss）</summary>
        FukuheiMiss = 0x47,
        /// <summary>内讧暴击（Exp_DoushiuchiCritical）</summary>
        DoushiuchiCritical = 0x48,
        /// <summary>内讧命中（Exp_Doushiuchi）</summary>
        Doushiuchi = 0x49,
        /// <summary>内讧未命中（Exp_DoushiuchiMiss）</summary>
        DoushiuchiMiss = 0x4a,
        /// <summary>妖术暴击（Exp_YojutsuCritical）</summary>
        YojutsuCritical = 0x4b,
        /// <summary>妖术命中（Exp_Yojutsu）</summary>
        Yojutsu = 0x4c,
        /// <summary>妖术未命中（Exp_YojutsuMiss）</summary>
        YojutsuMiss = 0x4d,
        /// <summary>落雷暴击（Exp_RakuraiCritical）</summary>
        RakuraiCritical = 0x4e,
        /// <summary>落雷命中（Exp_Rakurai）</summary>
        Rakurai = 0x4f,
        /// <summary>落雷未命中（Exp_RakuraiMiss）</summary>
        RakuraiMiss = 0x50,
        /// <summary>设置陷阱（Exp_BuildTrap）</summary>
        BuildTrap = 0x51,
        /// <summary>修补陷阱（Exp_RepairTrap）</summary>
        RepairTrap = 0x52,
        /// <summary>陷阱完工（Exp_CompleteTrap）</summary>
        CompleteTrap = 0x53,
        /// <summary>设置设施（Exp_Build）</summary>
        Build = 0x54,
        /// <summary>修补设施（Exp_Repair）</summary>
        Repair = 0x55,
        /// <summary>设施完工（Exp_Complete）</summary>
        Complete = 0x56,
        /// <summary>陷阱·被攻击引爆（Exp_TrapAttack）</summary>
        TrapAttack = 0x57,
        /// <summary>陷阱·被计略引爆（Exp_TrapStrategy）</summary>
        TrapStrategy = 0x58,
        /// <summary>陷阱·被推挤引爆（Exp_TrapCollision）</summary>
        TrapCollision = 0x59,
        /// <summary>Exp_90（原版预留，未使用）</summary>
        Reserved90 = 0x5a,
        /// <summary>援助攻击（Exp_Assist）</summary>
        Assist = 0x5b,
        /// <summary>Exp_92（原版预留，未使用）</summary>
        Reserved92 = 0x5c,
        /// <summary>被普攻命中（Exp_Attacked）</summary>
        Attacked = 0x5d,
        /// <summary>被一齐攻击命中（Exp_Surrounded）</summary>
        Surrounded = 0x5e,
        /// <summary>被战法命中（Exp_TacticAttacked）</summary>
        TacticAttacked = 0x5f,
        /// <summary>补给（Exp_Supply）</summary>
        Supply = 0x60,

        /// <summary>表长（Exp_Max）</summary>
        Max = 0x61,
    }

    /// <summary>
    /// 一条战斗经验记录，字段顺序与原版 <c>Exp</c> 一致（s11_scen.h:3557-3564）。
    /// </summary>
    public struct CombatExpEntry
    {
        // stat_exp[5]：PersonStatType 顺序（s11_enum_scen.h:532-540）
        /// <summary>统率经验</summary>
        public int leadership;
        /// <summary>武力经验</summary>
        public int strength;
        /// <summary>智力经验</summary>
        public int intelligence;
        /// <summary>政治经验</summary>
        public int politics;
        /// <summary>魅力经验</summary>
        public int charisma;

        // heishu_exp[6]：Heishu 顺序（s11_enum_scen.h:116-125）
        /// <summary>枪兵适性经验</summary>
        public int spear;
        /// <summary>戟兵适性经验</summary>
        public int halberd;
        /// <summary>弩兵适性经验</summary>
        public int bow;
        /// <summary>骑兵适性经验</summary>
        public int cavalry;
        /// <summary>兵器适性经验</summary>
        public int siege;
        /// <summary>水军适性经验</summary>
        public int navy;

        /// <summary>功绩（kouseki）</summary>
        public int merit;
        /// <summary>势力技巧点（tech_point，未减半值）</summary>
        public int techniquePoint;
    }

    /// <summary>
    /// 战斗经验表（原版 Exp 表的逐行搬移）。
    /// 取用入口：<see cref="Get"/>；战斗结算见 <see cref="Troop.GainEP(CombatExpEvent, bool)"/>。
    /// </summary>
    public static class CombatExpTable
    {
        /// <summary>数据文件（相对内容根目录）。与 AIConfig.json 同一套 Mod 覆盖机制。</summary>
        public const string ConfigPath = "Data/Common/CombatExpTable.json";

        /// <summary>实际生效的表（首次访问时由 <see cref="EnsureLoaded"/> 填充）</summary>
        static CombatExpEntry[] table;

        /// <summary>是否已尝试加载过（避免每帧重复读盘）</summary>
        static bool loaded;

        /// <summary>
        /// 原版表体（s11_scen.cpp:46-144）。
        /// 只写非零字段，未写字段为 0 —— 与原版的 <c>{ 0, 0, 0, 0, 0 }</c> 等价。
        /// </summary>
        static CombatExpEntry[] Table
        {
            get
            {
                EnsureLoaded();
                return table;
            }
        }

        /// <summary>
        /// 首次访问时装载：先铺内置原版表，再用 JSON 覆盖。
        ///
        /// 【为什么先铺内置表】JSON 可能不存在（老存档 / 缺文件的发行包）、
        /// 也可能只写了一部分行（策划只想改突刺）。此时未覆盖的行必须仍是原版值，
        /// 所以不能"JSON 为空表就清空"，只能逐行覆盖。
        /// </summary>
        static void EnsureLoaded()
        {
            if (loaded)
                return;

            // Path 还没 Init（编辑器里直接调、或游戏启动早期）时先给内置表，
            // 但**不置 loaded** —— 否则 Path 就绪后永远读不到 JSON。
            if (string.IsNullOrEmpty(Sango.Path.ContentRootPath))
            {
                if (table == null)
                    table = BuildTable();
                return;
            }

            loaded = true;
            table = BuildTable();

            try
            {
                bool hit = false;
                ModManager.Instance.LoadFile(ConfigPath, file =>
                {
                    hit = true;
                    JObject root = JObject.Parse(File.ReadAllText(file));
                    JArray entries = root["entries"] as JArray;
                    if (entries == null)
                    {
                        Sango.Log.Warning($"{ConfigPath} 缺少 entries 数组，沿用内置表");
                        return;
                    }

                    for (int i = 0; i < entries.Count; i++)
                    {
                        JObject item = entries[i] as JObject;
                        if (item == null || item["event"] == null)
                            continue;

                        int index = (int)item["event"];
                        if (index < 0 || index >= table.Length)
                        {
                            Sango.Log.Warning($"{ConfigPath} 出现越界的 event={index}，已忽略");
                            continue;
                        }

                        // 只覆盖本行的字段，未写的按 0（与原版 C++ 聚合初始化一致）
                        table[index] = item.ToObject<CombatExpEntry>();
                    }
                });

                Sango.Log.Info(hit
                    ? $"CombatExpTable 已从 {ConfigPath} 载入"
                    : $"未找到 {ConfigPath}，使用内置原版 Exp 表");
            }
            catch (Exception e)
            {
                Sango.Log.Warning($"CombatExpTable 加载失败，沿用内置原版表：{e.Message}");
            }
        }

        /// <summary>取某个事件的经验记录；越界返回零值（原版 <c>get_exp</c> 不做边界检查，这里做一次防御）。</summary>
        public static CombatExpEntry Get(CombatExpEvent expEvent)
        {
            CombatExpEntry[] current = Table;
            int index = (int)expEvent;
            if (index < 0 || index >= current.Length)
                return default;
            return current[index];
        }

        /// <summary>按原版行序构造整张表。</summary>
        static CombatExpEntry[] BuildTable()
        {
            CombatExpEntry[] t = new CombatExpEntry[(int)CombatExpEvent.Max];

            // ---- 0x00 普攻暴击 / 0x01 普攻命中 ----
            t[0x00] = new CombatExpEntry { strength = 1, spear = 1, halberd = 1, bow = 1, navy = 1, merit = 50, techniquePoint = 10 };
            t[0x01] = new CombatExpEntry { strength = 1, spear = 1, halberd = 1, bow = 1, navy = 1, merit = 20 };

            // ---- 0x02 / 0x03 原版预留（弩/水 兵种经验）----
            t[0x02] = new CombatExpEntry { strength = 1, bow = 1, navy = 1, merit = 50, techniquePoint = 10 };
            t[0x03] = new CombatExpEntry { strength = 1, bow = 1, navy = 1, merit = 20 };

            // ---- 0x04 / 0x05 一齐攻击 ----
            t[0x04] = new CombatExpEntry { leadership = 1, strength = 1, spear = 1, halberd = 1, bow = 1, navy = 1, merit = 50, techniquePoint = 1 };
            t[0x05] = new CombatExpEntry { leadership = 1, strength = 1, spear = 1, halberd = 1, bow = 1, navy = 1, merit = 20, techniquePoint = 1 };

            // ---- 0x06~0x08 突刺（枪）----
            t[0x06] = new CombatExpEntry { strength = 4, spear = 4, merit = 100, techniquePoint = 30 };
            t[0x07] = new CombatExpEntry { strength = 3, spear = 3, merit = 50, techniquePoint = 30 };
            t[0x08] = new CombatExpEntry { strength = 1, spear = 1, merit = 10 };

            // ---- 0x09~0x0b 螺旋突刺（枪）----
            t[0x09] = new CombatExpEntry { strength = 4, spear = 4, merit = 100, techniquePoint = 30 };
            t[0x0a] = new CombatExpEntry { strength = 3, spear = 3, merit = 50, techniquePoint = 30 };
            t[0x0b] = new CombatExpEntry { strength = 1, spear = 1, merit = 10 };

            // ---- 0x0c~0x0e 二段突刺（枪）----
            t[0x0c] = new CombatExpEntry { strength = 4, spear = 4, merit = 100, techniquePoint = 30 };
            t[0x0d] = new CombatExpEntry { strength = 3, spear = 3, merit = 50, techniquePoint = 30 };
            t[0x0e] = new CombatExpEntry { strength = 1, spear = 1, merit = 10 };

            // ---- 0x0f~0x11 熊手（戟）----
            t[0x0f] = new CombatExpEntry { strength = 4, halberd = 4, merit = 100, techniquePoint = 30 };
            t[0x10] = new CombatExpEntry { strength = 3, halberd = 3, merit = 50, techniquePoint = 30 };
            t[0x11] = new CombatExpEntry { strength = 1, halberd = 1, merit = 10 };

            // ---- 0x12~0x14 横扫（戟）----
            t[0x12] = new CombatExpEntry { strength = 4, halberd = 4, merit = 100, techniquePoint = 20 };
            t[0x13] = new CombatExpEntry { strength = 3, halberd = 3, merit = 50, techniquePoint = 20 };
            t[0x14] = new CombatExpEntry { strength = 1, halberd = 1, merit = 10 };

            // ---- 0x15~0x17 旋风（戟）----
            t[0x15] = new CombatExpEntry { strength = 4, halberd = 4, merit = 100, techniquePoint = 20 };
            t[0x16] = new CombatExpEntry { strength = 3, halberd = 3, merit = 50, techniquePoint = 20 };
            t[0x17] = new CombatExpEntry { strength = 1, halberd = 1, merit = 10 };

            // ---- 0x18~0x1a 火矢（弩 / 兵器 / 水军共用）----
            t[0x18] = new CombatExpEntry { strength = 4, bow = 4, siege = 4, navy = 4, merit = 100, techniquePoint = 30 };
            t[0x19] = new CombatExpEntry { strength = 3, bow = 3, siege = 3, navy = 3, merit = 50, techniquePoint = 30 };
            t[0x1a] = new CombatExpEntry { strength = 1, bow = 1, siege = 1, navy = 1, merit = 10 };

            // ---- 0x1b~0x1d 贯箭（弩）----
            t[0x1b] = new CombatExpEntry { strength = 4, bow = 4, merit = 100, techniquePoint = 20 };
            t[0x1c] = new CombatExpEntry { strength = 3, bow = 3, merit = 50, techniquePoint = 20 };
            t[0x1d] = new CombatExpEntry { strength = 1, bow = 1, merit = 10 };

            // ---- 0x1e~0x20 乱射（弩）----
            t[0x1e] = new CombatExpEntry { strength = 4, bow = 4, merit = 100, techniquePoint = 20 };
            t[0x1f] = new CombatExpEntry { strength = 3, bow = 3, merit = 50, techniquePoint = 20 };
            t[0x20] = new CombatExpEntry { strength = 1, bow = 1, merit = 10 };

            // ---- 0x21~0x23 突击（骑）----
            t[0x21] = new CombatExpEntry { strength = 4, cavalry = 4, merit = 100, techniquePoint = 30 };
            t[0x22] = new CombatExpEntry { strength = 3, cavalry = 3, merit = 50, techniquePoint = 30 };
            t[0x23] = new CombatExpEntry { strength = 1, cavalry = 1, merit = 10 };

            // ---- 0x24~0x26 突破（骑）----
            t[0x24] = new CombatExpEntry { strength = 4, cavalry = 4, merit = 100, techniquePoint = 30 };
            t[0x25] = new CombatExpEntry { strength = 3, cavalry = 3, merit = 50, techniquePoint = 30 };
            t[0x26] = new CombatExpEntry { strength = 1, cavalry = 1, merit = 10 };

            // ---- 0x27~0x29 突进（骑）----
            t[0x27] = new CombatExpEntry { strength = 4, cavalry = 4, merit = 100, techniquePoint = 30 };
            t[0x28] = new CombatExpEntry { strength = 3, cavalry = 3, merit = 50, techniquePoint = 30 };
            t[0x29] = new CombatExpEntry { strength = 1, cavalry = 1, merit = 10 };

            // ---- 0x2a~0x2c 破碎（兵器；miss 全 0）----
            t[0x2a] = new CombatExpEntry { strength = 4, siege = 4, merit = 100, techniquePoint = 30 };
            t[0x2b] = new CombatExpEntry { strength = 3, siege = 3, merit = 50, techniquePoint = 30 };
            t[0x2c] = new CombatExpEntry();

            // ---- 0x2d~0x2f 放射（兵器；miss 全 0）----
            t[0x2d] = new CombatExpEntry { strength = 4, siege = 4, merit = 100, techniquePoint = 30 };
            t[0x2e] = new CombatExpEntry { strength = 3, siege = 3, merit = 50, techniquePoint = 30 };
            t[0x2f] = new CombatExpEntry();

            // ---- 0x30~0x32 投石（兵器 / 水军；miss 全 0）----
            t[0x30] = new CombatExpEntry { strength = 4, siege = 4, navy = 4, merit = 100, techniquePoint = 20 };
            t[0x31] = new CombatExpEntry { strength = 3, siege = 3, navy = 3, merit = 50, techniquePoint = 20 };
            t[0x32] = new CombatExpEntry();

            // ---- 0x33~0x35 猛撞（水军）----
            t[0x33] = new CombatExpEntry { strength = 4, navy = 4, merit = 100, techniquePoint = 30 };
            t[0x34] = new CombatExpEntry { strength = 3, navy = 3, merit = 50, techniquePoint = 30 };
            t[0x35] = new CombatExpEntry { strength = 1, navy = 1, merit = 10 };

            // ---- 0x36~0x38 火计 ----
            t[0x36] = new CombatExpEntry { intelligence = 2, merit = 100, techniquePoint = 20 };
            t[0x37] = new CombatExpEntry { intelligence = 2, merit = 50, techniquePoint = 20 };
            t[0x38] = new CombatExpEntry { intelligence = 1, merit = 10 };

            // ---- 0x39~0x3b 灭火（暴击全 0）----
            t[0x39] = new CombatExpEntry();
            t[0x3a] = new CombatExpEntry { intelligence = 2, merit = 50 };
            t[0x3b] = new CombatExpEntry { intelligence = 1, merit = 10 };

            // ---- 0x3c~0x3e 伪报 ----
            t[0x3c] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 20 };
            t[0x3d] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 20 };
            t[0x3e] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x3f~0x41 扰乱 ----
            t[0x3f] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 20 };
            t[0x40] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 20 };
            t[0x41] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x42~0x44 镇静 ----
            t[0x42] = new CombatExpEntry { intelligence = 2, merit = 100 };
            t[0x43] = new CombatExpEntry { intelligence = 2, merit = 50 };
            t[0x44] = new CombatExpEntry { intelligence = 1, merit = 10 };

            // ---- 0x45~0x47 伏兵 ----
            t[0x45] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 20 };
            t[0x46] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 20 };
            t[0x47] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x48~0x4a 内讧 ----
            t[0x48] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 30 };
            t[0x49] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 30 };
            t[0x4a] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x4b~0x4d 妖术 ----
            t[0x4b] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 20 };
            t[0x4c] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 20 };
            t[0x4d] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x4e~0x50 落雷 ----
            t[0x4e] = new CombatExpEntry { intelligence = 3, merit = 200, techniquePoint = 20 };
            t[0x4f] = new CombatExpEntry { intelligence = 3, merit = 100, techniquePoint = 20 };
            t[0x50] = new CombatExpEntry { intelligence = 1, merit = 20 };

            // ---- 0x51~0x56 建设（陷阱 / 设施：设置 / 修补 / 完工）----
            t[0x51] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };
            t[0x52] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };
            t[0x53] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };
            t[0x54] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };
            t[0x55] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };
            t[0x56] = new CombatExpEntry { politics = 1, merit = 30, techniquePoint = 5 };

            // ---- 0x57~0x59 陷阱（被攻击 / 被计略 / 被推挤引爆）----
            t[0x57] = new CombatExpEntry { intelligence = 2, bow = 1, siege = 1, navy = 1, merit = 30, techniquePoint = 10 };
            t[0x58] = new CombatExpEntry { intelligence = 2, merit = 30, techniquePoint = 10 };
            t[0x59] = new CombatExpEntry { intelligence = 2, spear = 1, navy = 1, merit = 30, techniquePoint = 10 };

            // ---- 0x5a 原版预留 ----
            t[0x5a] = new CombatExpEntry { strength = 2, merit = 30, techniquePoint = 10 };

            // ---- 0x5b 援助攻击 ----
            t[0x5b] = new CombatExpEntry { leadership = 1, spear = 1, halberd = 1, bow = 1, navy = 1, merit = 30, techniquePoint = 10 };

            // ---- 0x5c 原版预留 ----
            t[0x5c] = new CombatExpEntry { leadership = 1, spear = 1, halberd = 1, bow = 1, cavalry = 1, siege = 1, navy = 1, merit = 30, techniquePoint = 10 };

            // ---- 0x5d~0x5f 受击（无功绩 / 无技巧点）----
            t[0x5d] = new CombatExpEntry { leadership = 1, spear = 1, halberd = 1, bow = 1, cavalry = 1, siege = 1, navy = 1 };
            t[0x5e] = new CombatExpEntry { leadership = 1, spear = 1, halberd = 1, bow = 1, cavalry = 1, siege = 1, navy = 1 };
            t[0x5f] = new CombatExpEntry { spear = 1, halberd = 1, bow = 1, cavalry = 1, siege = 1, navy = 1 };

            // ---- 0x60 补给 ----
            t[0x60] = new CombatExpEntry { leadership = 2, merit = 100 };

            return t;
        }

        /// <summary>
        /// 把 <see cref="CombatExpEvent"/> 的 12 项数值按 5 + 6 + 1 拆成数组。
        /// 给需要"逐项遍历"的调用方用（例如批量加成属性经验）。
        /// </summary>
        public static void Split(CombatExpEntry entry, int[] statExp, int[] heishuExp)
        {
            if (statExp != null && statExp.Length >= 5)
            {
                statExp[0] = entry.leadership;
                statExp[1] = entry.strength;
                statExp[2] = entry.intelligence;
                statExp[3] = entry.politics;
                statExp[4] = entry.charisma;
            }

            if (heishuExp != null && heishuExp.Length >= 6)
            {
                heishuExp[0] = entry.spear;
                heishuExp[1] = entry.halberd;
                heishuExp[2] = entry.bow;
                heishuExp[3] = entry.cavalry;
                heishuExp[4] = entry.siege;
                heishuExp[5] = entry.navy;
            }
        }

        /// <summary>
        /// 战法 / 计略的"命中行 / 暴击行"取值规则（对齐原版索引算法）。
        ///
        /// 原版：
        ///   战法  <c>get_exp(tactic->get_exp() + 1 - tactic_hit - critical)</c>（s11_sys_anim_unit.cpp:1090）
        ///   计略  <c>get_exp(Exp_KakeiMiss + 3 * strategy_id - hit - critical)</c>
        /// 即每组固定按 [暴击, 命中, 未命中] 排布，命中行 = 组首 + 1，暴击行 = 组首 + 0，未命中行 = 组首 + 2。
        /// </summary>
        /// <param name="missEvent">该组的"未命中"行（组首 + 2）</param>
        /// <param name="hit">是否命中</param>
        /// <param name="critical">是否暴击</param>
        public static CombatExpEvent ResolveRow(CombatExpEvent missEvent, bool hit, bool critical)
        {
            if (!hit)
                return missEvent;
            return critical ? (CombatExpEvent)((int)missEvent - 2) : (CombatExpEvent)((int)missEvent - 1);
        }
    }
}
