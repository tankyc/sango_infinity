using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sango.Core
{
    // =====================================================================================
    // 原版（三国志11 PK）剧本解析器
    //
    // 【文件一】ScenXXX.s11（type = 0x16 ScenPK）—— 动态数据
    //   0x00  Header(0x5A) → ScenSummary(16093) → Scenario(11)
    //   → Building[87]×18 → Person[850]×152 → Item[100]×57 → Force[47]×72
    //   → District[47]×8 → City[42]×81 → Gate[10]×64 → Port[35]×64 → Kokugou[84]×87
    //   （Province/Region/Facility/Equipment/Title/Rank/Skill/Tech/Tactic/Terrain/
    //     Kanji/Ability/Unit/Campaign/AI/Map/Profile 在 ScenPK 下长度为 0）
    //
    // 【文件二】Scenario.s11（type = 0x18 ScenarioPK）—— 静态表
    //   0x00  Header(0x5A)
    //   → City[42]×25 → Gate[10]×26 → Port[35]×26 → Province[12]×44 → Region[6]×18
    //   → Facility[64]×191 → Equipment[12]×150 → Title[10]×44 → Rank[81]×46
    //   → Skill[100]×93 → Tech[36]×92 → Tactic[32]×56 → Terrain[32]×23
    //   → Kanji[400]×11 → Ability[98]×74
    //   合计 47838 + 90 = 47928 = 文件长度（已验证）
    //
    // 全部长度均为逐字段累加原版 read 实现得出，非估计。
    // =====================================================================================

    /// <summary>原版一个据点（城 / 关 / 港）的静态名。</summary>
    public class Open311KyotenInfo
    {
        public string Name;
        public string Read;
        /// <summary>所属州（City 专用）</summary>
        public int Province;
        /// <summary>相邻据点 Id（6 个，-1 = 无）</summary>
        public int[] Neighbors = new int[6];
    }

    /// <summary>原版城市（ScenPK 动态段）。</summary>
    public class Open311CityData
    {
        public int Id;
        /// <summary>所属军团 Id（对应 District）</summary>
        public int District;
        public uint BaseMaxTroops;
        public uint Troops;
        public uint Gold;
        public uint Food;
        /// <summary>兵装库存 [12]，顺序：枪/戟/弩/骑/兵器/水军…</summary>
        public uint[] Equipment = new uint[12];
        public int Rate;
        public int BaseRevenue;
        public int BaseHarvest;
        public int BaseMaxHp;
        public int Energy;
        public int PublicOrder;
        /// <summary>特产兵种标记 [6]</summary>
        public bool[] Tokusan = new bool[6];
    }

    /// <summary>原版关所 / 港口（ScenPK 动态段）。</summary>
    public class Open311GatePortData
    {
        public int Id;
        public int District;
        public uint Troops;
        public uint Gold;
        public uint Food;
        public uint[] Equipment = new uint[12];
        public int Energy;
        public int BaseMaxHp;
        public bool IsGate;
    }

    /// <summary>原版武将（ScenPK 动态段，152 字节）。</summary>
    public class Open311PersonData
    {
        public int Id;
        public string FamilyName;   // sei_
        public string GivenName;    // mei_
        public string NickName;     // azana_（字）
        public int Portrait;
        public int Gender;
        public int Appearance;
        public int Birth;
        public int Death;
        public int DeathType;
        public int Ketsuen;
        public int Father;
        public int Mother;
        public int Generation;
        public int Spouse;
        public int Gikyoudai;
        public int Aishou;
        public int[] Like = new int[5];
        public int[] Hate = new int[5];
        public int DistrictId;
        /// <summary>所属据点（**building_id**，需换算成 city_id）</summary>
        public int Service;
        public int Location;
        /// <summary>身分（原版 MibunEnum）</summary>
        public int Mibun;
        public int Rank;
        public int AppearingKunshu;
        public int Loyalty;
        public int Kouseki;
        /// <summary>兵种适性 [6]：枪/戟/弩/骑/兵器/水军</summary>
        public int[] Tekisei = new int[6];
        /// <summary>五维 [5]：统率/武力/智力/政治/魅力</summary>
        public int[] BaseStat = new int[5];
        public int[] StatAging = new int[5];
        public int Birthplace;
        public int Skill;
        public int Wadai;
        public int Giri;
        public int Ambition;
        public int Promotion;
        public int Seikaku;
        public int Voice;
        public int Tone;
        public int Kanshitsu;
        public int StrategicTendency;
        public int LocalAffiliation;
        public int Skeleton;
        public int[] Body = new int[8];
        public int LeftWeapon;
        public int RightWeapon;
        public int Horse;
        public int OldAge;
        public uint Status;
    }

    /// <summary>原版宝物（ScenPK 动态段，57 字节）。</summary>
    public class Open311ItemData
    {
        public int Id;
        public string Name;
        public string Read;
        public int Type;
        public int Value;
        public int Portrait;
        public int OwnerPerson;
        public int LocationCity;
        public int State;
    }

    /// <summary>原版势力（ScenPK 动态段，72 字节）。</summary>
    public class Open311ForceData
    {
        public int Id;
        public int Kunshu;
        public int Gunshi;
        /// <summary>对每个势力的友好度 [47]</summary>
        public byte[] Like = new byte[47];
        public int Title;
        public int Kokugou;
        public int Color;
        public int Policy;
        public int PolicyTarget;
        public uint Ally0;
        public uint Ally1;
        public uint Tech0;
        public uint Tech1;
    }

    /// <summary>原版军团（ScenPK 动态段，8 字节）。</summary>
    public class Open311DistrictData
    {
        public int Id;
        public int Force;
        public int Number;
        public int Totoku;
        public int Policy;
        public int PolicyTarget;
        public int MidObjective;
        public int MidObjectiveTarget;
    }

    /// <summary>原版建筑（ScenPK 动态段，18 字节）。</summary>
    public class Open311BuildingData
    {
        public int Id;
        public int Facility;
        public int OwnerForce;
        public int Hp;
        public uint Completed;
        public int PosX;
        public int PosY;
        public int Dir;
    }

    /// <summary>原版国号（ScenPK 动态段，87 字节）。</summary>
    public class Open311KokugouData
    {
        public int Id;
        public string Name;
        public string Read;
        public string Desc;
        public int[] City = new int[2];
        public uint Available;
    }

    /// <summary>原版设施类型表（Scenario.s11，191 字节/条）。</summary>
    public class Open311FacilityData
    {
        public int Id;
        public string Name;
        public string Read;
        public string Desc;
        public string Cond;
        public int Type;
        public int Yield;
        public int Hp;
        public int GoldCost;
        public int RequiredTech;
        public int Range;
        public int MinRange;
        public int MaxRange;
        public int TechPoint;
    }

    /// <summary>原版地形表（Scenario.s11，23 字节/条）。</summary>
    public class Open311TerrainData
    {
        public int Id;
        public string Name;
        public string Read;
        public int Flag;
    }

    /// <summary>两个原版文件解析后的全部数据。</summary>
    public class Open311ScenarioData
    {
        // ---------- ScenXXX.s11 ----------
        public int Number;
        public int StartYear;
        public int StartMonth;
        public int StartDay;
        public string ScenName;
        public string ScenDesc;
        public int Emperor;
        public uint ScenType;

        public List<Open311BuildingData> Buildings = new List<Open311BuildingData>();
        public List<Open311PersonData> Persons = new List<Open311PersonData>();
        public List<Open311ItemData> Items = new List<Open311ItemData>();
        public List<Open311ForceData> Forces = new List<Open311ForceData>();
        public List<Open311DistrictData> Districts = new List<Open311DistrictData>();
        public List<Open311CityData> Cities = new List<Open311CityData>();
        public List<Open311GatePortData> Gates = new List<Open311GatePortData>();
        public List<Open311GatePortData> Ports = new List<Open311GatePortData>();
        public List<Open311KokugouData> Kokugous = new List<Open311KokugouData>();

        // ---------- Scenario.s11（静态表）----------
        public List<Open311KyotenInfo> CityInfos = new List<Open311KyotenInfo>();
        public List<Open311KyotenInfo> GateInfos = new List<Open311KyotenInfo>();
        public List<Open311KyotenInfo> PortInfos = new List<Open311KyotenInfo>();
        public List<string> ProvinceNames = new List<string>();
        public List<string> RegionNames = new List<string>();
        public List<Open311FacilityData> Facilities = new List<Open311FacilityData>();
        public List<Open311TerrainData> Terrains = new List<Open311TerrainData>();
        public List<string> TitleNames = new List<string>();
        public List<string> RankNames = new List<string>();
        public List<string> SkillNames = new List<string>();
        public List<string> TechNames = new List<string>();
        public List<string> TacticNames = new List<string>();
        public List<string> AbilityNames = new List<string>();
    }

    /// <summary>
    /// 原版剧本 / 静态表的解析器。
    ///
    /// 所有段长度、条数、字段顺序均逐项取自原版 read 实现：
    ///   Scenario::read（s11_scen.cpp:2305-2640）及各 ScenObject 子类的 read。
    /// </summary>
    public static class Open311ScenarioParser
    {
        // ---------- 原版常量（s11_enum_preset.h / s11_enum_scen.h）----------
        public const int CityId_Max = 0x2a;          // 42
        public const int GateId_Max = 0x0a;          // 10
        public const int PortId_Max = 0x23;          // 35
        public const int BuildingId_KyotenMax = CityId_Max + GateId_Max + PortId_Max;  // 87
        public const int PersonId_CustomFirst = 0x352;   // 850
        public const int ItemId_Max = 0x64;              // 100
        public const int ForceId_Max = 0x2f;             // 47
        public const int ForceId_NormalLast = 0x29;      // 41
        public const int DistrictId_Max = 0x2f;          // 47
        public const int ProvinceId_Max = 0x0c;          // 12
        public const int RegionId_Max = 6;
        public const int FacilityId_Max = 0x40;          // 64
        public const int EquipmentId_Max = 0x0c;         // 12
        public const int TitleId_Max = 0x0a;             // 10
        public const int RankId_Max = 0x51;              // 81
        public const int KokugouId_Max = 0x54;           // 84
        public const int SkillId_Max = 0x64;             // 100
        public const int TechId_Max = 0x24;              // 36
        public const int TacticId_Max = 0x20;            // 32
        public const int TerrainId_Max = 0x20;           // 32
        public const int KanjiId_Max = 400;
        public const int AbilityId_Max = 0x62;           // 98

        // ---------- 单条长度（逐字段累加）----------
        const int LenCityScen = 81;      // 动态段
        const int LenGateScen = 64;
        const int LenPerson = 152;
        const int LenItem = 57;
        const int LenForce = 72;
        const int LenDistrict = 8;
        const int LenBuilding = 18;
        const int LenKokugou = 87;

        const int LenCityInfo = 25;      // 静态段（name5 + read13 + province1 + neighbor6）
        const int LenGateInfo = 26;      // name7 + read19
        const int LenPortInfo = 26;
        const int LenProvinceInfo = 44;  // name5 + read25 + desc8 + region1 + neighbor5
        const int LenRegionInfo = 18;    // name5 + read13
        const int LenFacility = 191;     // 繁中 name11
        const int LenEquipment = 150;    // 7+19+55+3+32+32+2
        const int LenTitle = 44;         // 11+31+2
        const int LenRank = 46;          // 9+31+2+1+1+1+1
        const int LenSkill = 93;         // 5+19+67+1+1
        const int LenTech = 92;          // 9+33+41+1+1+2+2+2+1
        const int LenTactic = 56;        // 9+33+1+1+1+1+1+4+4+1
        const int LenTerrain = 23;       // 5+17+1
        const int LenKanji = 11;         // 3+7+1
        const int LenAbility = 74;       // 9+55+1+1+3+1+1+1+1+1

        /// <summary>ScenSummary 段长（繁中）：5 + 17 + 363 + 42 + 168 + 42×369</summary>
        const int LenScenSummary = 16093;

        static Encoding s_Big5;

        /// <summary>繁中编码（原版 PK 繁中版用 Big5）</summary>
        public static Encoding Big5
        {
            get
            {
                if (s_Big5 == null)
                {
                    try { s_Big5 = Encoding.GetEncoding(950); }
                    catch { s_Big5 = Encoding.UTF8; }
                }
                return s_Big5;
            }
        }

        // =================================================================================
        // 文件一：ScenXXX.s11
        // =================================================================================
        public static bool ParseScen(string path, Open311ScenarioData data, out string error)
        {
            error = null;
            if (data == null) { error = "data 为空"; return false; }

            Open311FileReader reader = new Open311FileReader();
            if (!reader.Open(path)) { error = "不是合法的原版文件"; return false; }
            if (reader.Type != Open311FileReader.FileType_ScenPK)
            {
                error = "类型不是 ScenPK(0x16)，实际 " + reader.Type;
                return false;
            }

            // ---------- ScenSummary ----------
            data.Number = reader.ReadInt8();
            data.StartYear = reader.ReadInt16();
            data.StartMonth = reader.ReadInt8();
            data.StartDay = reader.ReadInt8();
            data.ScenName = ReadFixedString(reader, 0x11);
            data.ScenDesc = ReadFixedString(reader, 0x16b);

            reader.Skip(CityId_Max);            // city color[42]
            reader.Skip(CityId_Max * 4);        // city pos[42] (uint16 x2)

            int forceCount = ForceId_NormalLast + 1;   // 42
            reader.Skip(forceCount * (1 + 0x170));     // difficulty(1) + desc(0x170)

            // ---------- Scenario ----------
            reader.ReadInt8();                  // number（与 summary 重复）
            reader.ReadInt16();                 // start_year
            reader.ReadInt8();                  // start_month
            reader.ReadInt8();                  // start_day
            data.Emperor = reader.ReadInt16();
            data.ScenType = reader.ReadUInt32();

            // ---------- Building[87] ----------
            for (int i = 0; i < BuildingId_KyotenMax; i++)
            {
                Open311BuildingData b = new Open311BuildingData();
                b.Id = i;
                b.Facility = reader.ReadInt8();
                b.OwnerForce = reader.ReadInt8();
                b.Hp = reader.ReadUInt16();
                b.Completed = reader.ReadUInt32();
                reader.ReadUInt32();            // _18
                reader.ReadUInt8();             // _1c
                b.PosX = reader.ReadInt16();
                b.PosY = reader.ReadInt16();
                b.Dir = reader.ReadInt8();
                data.Buildings.Add(b);
            }

            // ---------- Person[850] ----------
            for (int i = 0; i < PersonId_CustomFirst; i++)
                data.Persons.Add(ReadPerson(reader, i));

            // ---------- Item[100] ----------
            for (int i = 0; i < ItemId_Max; i++)
                data.Items.Add(ReadItem(reader, i));

            // ---------- Force[47] ----------
            for (int i = 0; i < ForceId_Max; i++)
                data.Forces.Add(ReadForce(reader, i));

            // ---------- District[47] ----------
            for (int i = 0; i < DistrictId_Max; i++)
                data.Districts.Add(ReadDistrict(reader, i));

            // ---------- City[42] ----------
            for (int i = 0; i < CityId_Max; i++)
                data.Cities.Add(ReadCity(reader, i));

            // ---------- Gate[10] ----------
            for (int i = 0; i < GateId_Max; i++)
                data.Gates.Add(ReadGatePort(reader, i, true));

            // ---------- Port[35] ----------
            for (int i = 0; i < PortId_Max; i++)
                data.Ports.Add(ReadGatePort(reader, i, false));

            // ---------- Kokugou[84] ----------
            for (int i = 0; i < KokugouId_Max; i++)
            {
                Open311KokugouData k = new Open311KokugouData();
                k.Id = i;
                k.Name = ReadFixedString(reader, 5);
                k.Read = ReadFixedString(reader, 0x0d);
                k.Desc = ReadFixedString(reader, 0x3f);
                k.City[0] = reader.ReadInt8();
                k.City[1] = reader.ReadInt8();
                k.Available = reader.ReadUInt32();
                data.Kokugous.Add(k);
            }

            // 文件应恰好读完（剩余 0 字节）
            if (reader.Position != reader.Length)
                error = "Scen 文件未对齐：已读 " + reader.Position + "，文件长 " + reader.Length;

            return true;
        }

        // =================================================================================
        // 文件二：Scenario.s11（静态表）
        // =================================================================================
        public static bool ParseScenario(string path, Open311ScenarioData data, out string error)
        {
            error = null;
            if (data == null) { error = "data 为空"; return false; }

            Open311FileReader reader = new Open311FileReader();
            if (!reader.Open(path)) { error = "不是合法的原版文件"; return false; }
            if (reader.Type != Open311FileReader.FileType_ScenarioPK)
            {
                error = "类型不是 ScenarioPK(0x18)，实际 " + reader.Type;
                return false;
            }

            // ---------- City 名[42] ----------
            for (int i = 0; i < CityId_Max; i++)
                data.CityInfos.Add(ReadKyotenInfo(reader, true, 5, 0x0d));

            // ---------- Gate 名[10] ----------
            for (int i = 0; i < GateId_Max; i++)
                data.GateInfos.Add(ReadKyotenInfo(reader, false, 7, 0x13));

            // ---------- Port 名[35] ----------
            for (int i = 0; i < PortId_Max; i++)
                data.PortInfos.Add(ReadKyotenInfo(reader, false, 7, 0x13));

            // ---------- Province[12] ----------
            for (int i = 0; i < ProvinceId_Max; i++)
            {
                string name = ReadFixedString(reader, 5);
                reader.Skip(0x19);              // read
                reader.Skip(8);                 // desc
                reader.ReadInt8();              // region
                reader.Skip(5);                 // neighbor[5]
                data.ProvinceNames.Add(name);
            }

            // ---------- Region[6] ----------
            for (int i = 0; i < RegionId_Max; i++)
            {
                string name = ReadFixedString(reader, 5);
                reader.Skip(0x0d);
                data.RegionNames.Add(name);
            }

            // ---------- Facility[64] ----------
            for (int i = 0; i < FacilityId_Max; i++)
            {
                Open311FacilityData f = new Open311FacilityData();
                f.Id = i;
                f.Name = ReadFixedString(reader, 11);     // 繁中 11
                f.Read = ReadFixedString(reader, 21);
                f.Desc = ReadFixedString(reader, 71);
                f.Cond = ReadFixedString(reader, 71);
                f.Type = reader.ReadInt8();
                f.Yield = reader.ReadUInt16();
                f.Hp = reader.ReadUInt16();
                f.GoldCost = reader.ReadUInt16();
                f.RequiredTech = reader.ReadInt8();
                f.Range = reader.ReadUInt8();
                f.MinRange = reader.ReadUInt8();
                f.MaxRange = reader.ReadUInt8();
                reader.ReadUInt8();         // _c5
                reader.Skip(4);             // buildable_terrain
                f.TechPoint = reader.ReadUInt8();
                data.Facilities.Add(f);
            }

            // ---------- Equipment[12]：7+19+55+stat3+terrain32+tactics32+gold2 ----------
            for (int i = 0; i < EquipmentId_Max; i++)
            {
                reader.Skip(7 + 19 + 55 + 3 + 32 + 32 + 2);
            }

            // ---------- Title[10]：11+31+2 ----------
            for (int i = 0; i < TitleId_Max; i++)
            {
                string name = ReadFixedString(reader, 0x0b);
                reader.Skip(0x1f);
                reader.ReadUInt16();
                data.TitleNames.Add(name);
            }

            // ---------- Rank[81]：9+31+2+1+1+1+1 ----------
            for (int i = 0; i < RankId_Max; i++)
            {
                string name = ReadFixedString(reader, 9);
                reader.Skip(0x1f + 2 + 1 + 1 + 1 + 1);
                data.RankNames.Add(name);
            }

            // ---------- Skill[100]：5+19+67+1+1 ----------
            for (int i = 0; i < SkillId_Max; i++)
            {
                string name = ReadFixedString(reader, 5);
                reader.Skip(0x13 + 0x43);
                reader.ReadInt8();          // level
                reader.ReadInt8();          // type
                data.SkillNames.Add(name);
            }

            // ---------- Tech[36]：9+33+41+1+1+2+2+2+1 ----------
            for (int i = 0; i < TechId_Max; i++)
            {
                string name = ReadFixedString(reader, 9);
                reader.Skip(0x21 + 0x29 + 1 + 1 + 2 + 2 + 2 + 1);
                data.TechNames.Add(name);
            }

            // ---------- Tactic[32]：9+33+1+1+1+1+1+4+4+1 ----------
            for (int i = 0; i < TacticId_Max; i++)
            {
                string name = ReadFixedString(reader, 9);
                reader.Skip(0x21 + 1 + 1 + 1 + 1 + 1 + 4 + 4 + 1);
                data.TacticNames.Add(name);
            }

            // ---------- Terrain[32]：5+17+1 ----------
            for (int i = 0; i < TerrainId_Max; i++)
            {
                Open311TerrainData t = new Open311TerrainData();
                t.Id = i;
                t.Name = ReadFixedString(reader, 5);
                t.Read = ReadFixedString(reader, 0x11);
                t.Flag = reader.ReadUInt8();
                data.Terrains.Add(t);
            }

            // ---------- Kanji[400]：3+7+1 ----------
            reader.Skip(KanjiId_Max * LenKanji);

            // ---------- Ability[98]：9+55+1+1+3+1+1+1+1+1 ----------
            for (int i = 0; i < AbilityId_Max; i++)
            {
                string name = ReadFixedString(reader, 9);
                reader.Skip(55 + 1 + 1 + 3 + 1 + 1 + 1 + 1 + 1);
                data.AbilityNames.Add(name);
            }

            if (reader.Position != reader.Length)
                error = "Scenario 文件未对齐：已读 " + reader.Position + "，文件长 " + reader.Length;

            return true;
        }

        // =================================================================================
        // 各对象读取
        // =================================================================================
        static Open311PersonData ReadPerson(Open311FileReader r, int id)
        {
            Open311PersonData p = new Open311PersonData();
            p.Id = id;
            p.FamilyName = ReadFixedString(r, 5);
            p.GivenName = ReadFixedString(r, 5);
            p.NickName = ReadFixedString(r, 5);
            r.Skip(0x19 + 0x0d);            // name_read / azana_read
            p.Portrait = r.ReadInt16();
            p.Gender = r.ReadInt8();
            p.Appearance = r.ReadInt16();
            p.Birth = r.ReadInt16();
            p.Death = r.ReadInt16();
            p.DeathType = r.ReadInt8();
            p.Ketsuen = r.ReadInt16();
            p.Father = r.ReadInt16();
            p.Mother = r.ReadInt16();
            p.Generation = r.ReadUInt8();
            p.Spouse = r.ReadInt16();
            p.Gikyoudai = r.ReadInt16();
            p.Aishou = r.ReadUInt8();
            for (int i = 0; i < 5; i++) p.Like[i] = r.ReadInt16();
            for (int i = 0; i < 5; i++) p.Hate[i] = r.ReadInt16();
            p.DistrictId = r.ReadInt8();
            p.Service = r.ReadInt16();
            p.Location = r.ReadInt16();
            p.Mibun = r.ReadInt8();
            p.Rank = r.ReadInt8();
            p.AppearingKunshu = r.ReadInt16();
            p.Loyalty = r.ReadUInt8();
            p.Kouseki = r.ReadUInt16();
            for (int i = 0; i < 6; i++) p.Tekisei[i] = r.ReadInt8();
            for (int i = 0; i < 5; i++) p.BaseStat[i] = r.ReadInt8();
            for (int i = 0; i < 5; i++) p.StatAging[i] = r.ReadInt8();
            p.Birthplace = r.ReadInt8();
            p.Skill = r.ReadInt8();
            p.Wadai = r.ReadInt8();
            p.Giri = r.ReadInt8();
            p.Ambition = r.ReadInt8();
            p.Promotion = r.ReadInt8();
            p.Seikaku = r.ReadInt8();
            p.Voice = r.ReadInt8();
            p.Tone = r.ReadInt8();
            p.Kanshitsu = r.ReadInt8();
            p.StrategicTendency = r.ReadInt8();
            p.LocalAffiliation = r.ReadInt8();
            p.Skeleton = r.ReadInt8();
            for (int i = 0; i < 8; i++) p.Body[i] = r.ReadInt8();
            p.LeftWeapon = r.ReadInt8();
            p.RightWeapon = r.ReadInt8();
            p.Horse = r.ReadInt8();
            p.OldAge = r.ReadUInt8();
            p.Status = r.ReadUInt32();
            return p;
        }

        static Open311ItemData ReadItem(Open311FileReader r, int id)
        {
            Open311ItemData it = new Open311ItemData();
            it.Id = id;
            it.Name = ReadFixedString(r, 0x0d);
            it.Read = ReadFixedString(r, 0x25);
            it.Type = r.ReadInt8();
            it.Value = r.ReadUInt8();
            it.Portrait = r.ReadInt8();
            it.OwnerPerson = r.ReadInt16();
            it.LocationCity = r.ReadInt8();
            it.State = r.ReadInt8();
            return it;
        }

        static Open311ForceData ReadForce(Open311FileReader r, int id)
        {
            Open311ForceData f = new Open311ForceData();
            f.Id = id;
            f.Kunshu = r.ReadInt16();
            f.Gunshi = r.ReadInt16();
            for (int i = 0; i < ForceId_Max; i++) f.Like[i] = r.ReadUInt8();
            f.Title = r.ReadInt8();
            f.Kokugou = r.ReadInt8();
            f.Color = r.ReadInt8();
            f.Policy = r.ReadInt8();
            f.PolicyTarget = r.ReadInt8();
            f.Ally0 = r.ReadUInt32();
            f.Ally1 = r.ReadUInt32();
            f.Tech0 = r.ReadUInt32();       // major >= 1
            f.Tech1 = r.ReadUInt32();
            return f;
        }

        static Open311DistrictData ReadDistrict(Open311FileReader r, int id)
        {
            Open311DistrictData d = new Open311DistrictData();
            d.Id = id;
            d.Force = r.ReadInt8();
            d.Number = r.ReadInt8();
            d.Totoku = r.ReadInt16();
            d.Policy = r.ReadInt8();
            d.PolicyTarget = r.ReadInt8();
            d.MidObjective = r.ReadInt8();
            d.MidObjectiveTarget = r.ReadInt8();
            return d;
        }

        static Open311CityData ReadCity(Open311FileReader r, int id)
        {
            Open311CityData c = new Open311CityData();
            c.Id = id;
            c.District = r.ReadInt8();
            c.BaseMaxTroops = r.ReadUInt32();
            c.Troops = r.ReadUInt32();
            c.Gold = r.ReadUInt32();
            c.Food = r.ReadUInt32();
            for (int i = 0; i < EquipmentId_Max; i++) c.Equipment[i] = r.ReadUInt32();
            c.Rate = r.ReadUInt8();
            r.ReadUInt8();                  // _7d
            c.BaseRevenue = r.ReadUInt16();
            c.BaseHarvest = r.ReadUInt16();
            c.BaseMaxHp = r.ReadUInt16();
            c.Energy = r.ReadUInt8();
            c.PublicOrder = r.ReadUInt8();
            for (int i = 0; i < 6; i++) c.Tokusan[i] = r.ReadBool();
            return c;
        }

        static Open311GatePortData ReadGatePort(Open311FileReader r, int id, bool isGate)
        {
            Open311GatePortData g = new Open311GatePortData();
            g.Id = id;
            g.IsGate = isGate;
            g.District = r.ReadInt8();
            g.Troops = r.ReadUInt32();
            g.Gold = r.ReadUInt32();
            g.Food = r.ReadUInt32();
            for (int i = 0; i < EquipmentId_Max; i++) g.Equipment[i] = r.ReadUInt32();
            g.Energy = r.ReadUInt8();
            g.BaseMaxHp = r.ReadUInt16();
            return g;
        }

        static Open311KyotenInfo ReadKyotenInfo(Open311FileReader r, bool isCity, int nameLen, int readLen)
        {
            Open311KyotenInfo info = new Open311KyotenInfo();
            info.Name = ReadFixedString(r, nameLen);
            info.Read = ReadFixedString(r, readLen);
            if (isCity)
            {
                info.Province = r.ReadInt8();
                for (int i = 0; i < 6; i++) info.Neighbors[i] = r.ReadInt8();
            }
            return info;
        }

        /// <summary>
        /// 读定长字符串（Big5），遇到 \0 截断。
        ///
        /// 【必须逐字节 ReadUInt8】原版 read_string 是逐字符调用 read_char（带解密），
        /// 不是整段裸读。ScenPK 虽然 seed=0（未加密）结果一样，
        /// 但走同一条路径才能保证将来支持加密文件时不用改这里。
        /// </summary>
        static string ReadFixedString(Open311FileReader r, int byteLength)
        {
            if (byteLength <= 0) return string.Empty;

            byte[] buf = new byte[byteLength];
            for (int i = 0; i < byteLength; i++)
                buf[i] = r.ReadUInt8();

            int len = 0;
            while (len < buf.Length && buf[len] != 0) len++;
            if (len == 0) return string.Empty;

            return Big5.GetString(buf, 0, len);
        }
    }
}
