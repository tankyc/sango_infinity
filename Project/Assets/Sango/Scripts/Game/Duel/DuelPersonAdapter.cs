/*
 * 文件名：DuelPersonAdapter.cs
 * 描述：单挑系统与游戏真实对象之间的适配层
 *
 * 设计说明：
 *   · 单挑系统内部一律使用游戏真实类型：Person（武将）/ Troop（部队）/ Force（势力）/ Equipment（宝物）。
 *   · 真实类型没有提供单挑所需的全部访问器，这里用【扩展方法】补齐，
 *     单挑本体（Duel.cs / DuelAI.cs / DuelPhase.cs）无需大改即可直接使用真实对象。
 *   · 已有实例成员（Person.IsSpouse / Person.IsBrother / Person.IsParentchild /
 *     Person.CompatibilityDistance / Person.HasFeatrue 等）优先级高于扩展方法，
 *     单挑里调用它们时自动走游戏自带实现，此处不再重复定义。
 *   · 需要按项目数据调整的项集中在下面几个静态类，业务代码无需改动：
 *       DuelFeatureId  —— 强运 / 捕缚 特技在本项目中的 ID（已按 Features.json 填写）
 *       DuelItemMap    —— 宝物 → 单挑宝物大类的映射（按品名/子类型匹配，可覆盖）
 *       DuelSettings   —— 难度、寿命模式、功能开关、% 挂钩
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core.Duel
{
    #region 随机源

    /// <summary>
    /// 单挑随机源。默认转发到项目的 GameRandom；
    /// 调用 SetSeed 后切换为独立的可复现随机（用于测试 / 录像回放）。
    /// </summary>
    public static class DuelRandom
    {
        private static System.Random s_random = null;
        private static int s_seed = 0;

        /// <summary>设置种子，切换为可复现随机</summary>
        public static void SetSeed(int seed)
        {
            s_seed = seed;
            s_random = new System.Random(seed);
        }

        /// <summary>恢复使用项目的 GameRandom</summary>
        public static void UseProjectRandom()
        {
            s_random = null;
            s_seed = 0;
        }

        /// <summary>当前种子（未设置时为 0）</summary>
        public static int GetSeed() { return s_seed; }

        /// <summary>返回 [0, max) 的随机整数</summary>
        public static int Range(int max)
        {
            if (max <= 0) return 0;
            return s_random != null ? s_random.Next(max) : GameRandom.Range(max);
        }

        /// <summary>以 percent% 的概率返回 true</summary>
        public static bool Chance(int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return s_random != null ? s_random.Next(100) < percent : GameRandom.Chance(percent);
        }
    }

    #endregion

    #region 全局设置 / 钩子

    /// <summary>
    /// 单挑系统所需的全局设置与外部依赖钩子。
    /// 接入真实游戏时，按需给这些字段/委托赋值即可，无需改动单挑本体。
    /// </summary>
    public static class DuelSettings
    {
        private static readonly List<Equipment> s_emptyItems = new List<Equipment>();

        /// <summary>难度（影响 AI 的必杀/退却倾向）</summary>
        public static Difficulty Difficulty = Difficulty.Normal;

        /// <summary>寿命模式（影响老将的武力衰减判定）</summary>
        public static LifeMode LifeMode = LifeMode.Normal;

        /// <summary>战死频率（影响单挑致死的判定）</summary>
        public static BattleDeathMode BattleDeathMode = BattleDeathMode.Normal;

        /// <summary>功能是否被禁用（一击必杀 / 捕缚 / AI 退却 等）</summary>
        public static Func<Feature, bool> IsFeatDisabled = feature => false;

        /// <summary>
        /// 获取武将持有的宝物列表（影响单挑中的名马 / 剑 / 长武器 / 暗器 / 弓）。
        /// 默认取武将的武器、马匹、护甲三件装备。
        /// </summary>
        public static Func<Person, List<Equipment>> GetPersonItemList = DefaultGetPersonItemList;

        /// <summary>获取武将宝物提供的单挑战力加成，默认 0</summary>
        public static Func<Person, int> GetDuelItemPower = person => 0;

        /// <summary>获取势力颜色（用于历史日志着色），默认取势力旗色</summary>
        public static Func<Person, int> GetForceColor = DefaultGetForceColor;

        /// <summary>百分概率判定，默认走 DuelRandom</summary>
        public static Func<int, bool> RandBool = DuelRandom.Chance;

        /// <summary>[0, max) 随机整数，默认走 DuelRandom</summary>
        public static Func<int, int> RandInt = DuelRandom.Range;

        /// <summary>
        /// 根据伤病计算有效武力。
        ///
        /// 默认实现从**未折减的武力**（Person.RawStrength）按单挑内部的伤病等级折算：
        /// 单挑里的伤病可能高过进场时的 person.injury（打到一半挨了必杀会 +1 级），
        /// 而 Person.Strength 只反映进场时的伤病，所以必须从 Raw 重算，不能乘两次。
        /// 留下这个钩子是为了让集成层可以换一套自己的算法。
        /// </summary>
        public static Func<Person, int, int> CalcStrength = DefaultCalcStrength;

        /// <summary>默认实现：从武将的三件装备中取宝物列表</summary>
        private static List<Equipment> DefaultGetPersonItemList(Person person)
        {
            if (person == null) return s_emptyItems;

            List<Equipment> list = new List<Equipment>(3);
            if (person.EquippedWeapon != null) list.Add(person.EquippedWeapon);
            if (person.EquippedHorse != null) list.Add(person.EquippedHorse);
            if (person.EquippedArmor != null) list.Add(person.EquippedArmor);
            return list;
        }

        /// <summary>默认实现：取武将所属势力的旗色，转成 0xRRGGBB</summary>
        private static int DefaultGetForceColor(Person person)
        {
            Flag flag = person?.mBelongForce?.mFlag;
            if (flag == null) return 0;
            Color32 c = flag.color;
            return (c.r << 16) | (c.g << 8) | c.b;
        }

        /// <summary>默认实现：以未折减的武力为底，按单挑内部伤病等级折算（与 Person 同一套系数：三国志11 原版 100/80/50/30）</summary>
        private static int DefaultCalcStrength(Person person, int shoubyou)
        {
            if (person == null) return 0;
            return Person.ApplyInjuryDecay(person.RawStrength, shoubyou);
        }

        /// <summary>恢复为默认实现</summary>
        public static void Reset()
        {
            Difficulty = Difficulty.Normal;
            LifeMode = LifeMode.Normal;
            BattleDeathMode = BattleDeathMode.Normal;
            IsFeatDisabled = feature => false;
            GetPersonItemList = DefaultGetPersonItemList;
            GetDuelItemPower = person => 0;
            GetForceColor = DefaultGetForceColor;
            RandBool = DuelRandom.Chance;
            RandInt = DuelRandom.Range;
            CalcStrength = DefaultCalcStrength;
        }
    }

    #endregion

    #region 武将 ID 解析

    /// <summary>
    /// 把真实武将解析为单挑内部的 PersonId（吕布 / 关羽 / 张飞 ...）。
    /// 优先查 IdMap（按 Id 精确匹配，性能最好）；未命中则按姓名匹配。
    /// 结果按 Id 缓存，避免每次伤害计算都做字符串匹配。
    /// </summary>
    public static class DuelPersonId
    {
        private static readonly Dictionary<PersonId, string[]> s_names = new Dictionary<PersonId, string[]>
        {
            { PersonId.Ryofu,            new[] { "吕布", "呂布" } },
            { PersonId.Chouhi,           new[] { "张飞", "張飛" } },
            { PersonId.Kanu,             new[] { "关羽", "關羽" } },
            { PersonId.Kyocho,           new[] { "许褚", "許褚" } },
            { PersonId.Chouun,           new[] { "赵云", "趙雲" } },
            { PersonId.Bachou,           new[] { "马超", "馬超" } },
            { PersonId.Kouchuu_Kanshou,  new[] { "黄忠", "黃忠" } },
            { PersonId.Kakouen,          new[] { "夏侯渊", "夏侯淵" } },
            { PersonId.Ousou,            new[] { "黄盖", "黃蓋" } },
            { PersonId.Shukuyuu,         new[] { "周瑜" } },
        };

        /// <summary>按武将 Id 精确映射（优先级最高）。例：IdMap[851] = PersonId.Ryofu;</summary>
        public static readonly Dictionary<int, PersonId> IdMap = new Dictionary<int, PersonId>();

        private static readonly Dictionary<int, PersonId> s_cache = new Dictionary<int, PersonId>();

        /// <summary>清除姓名匹配缓存（IdMap 或武将数据变动后调用）</summary>
        public static void ClearCache() { s_cache.Clear(); }

        /// <summary>解析武将，未识别时返回 PersonId.Invalid</summary>
        public static PersonId Resolve(Person person)
        {
            if (person == null) return PersonId.Invalid;

            if (IdMap.TryGetValue(person.Id, out PersonId byId))
                return byId;

            if (s_cache.TryGetValue(person.Id, out PersonId cached))
                return cached;

            PersonId result = PersonId.Invalid;
            string name = person.Name;
            if (!string.IsNullOrEmpty(name))
            {
                foreach (KeyValuePair<PersonId, string[]> kv in s_names)
                {
                    for (int i = 0; i < kv.Value.Length; i++)
                    {
                        if (name.IndexOf(kv.Value[i], StringComparison.Ordinal) >= 0)
                        {
                            result = kv.Key;
                            break;
                        }
                    }
                    if (result != PersonId.Invalid) break;
                }
            }

            s_cache[person.Id] = result;
            return result;
        }
    }

    #endregion

    #region 性格 / 特技 / 宝物映射

    /// <summary>
    /// 把真实武将的 personality(性格) 转换为单挑 AI 性格。
    ///
    /// 项目内 Personalities 数据与单挑 AI 性格一一对应：
    ///   kind = 1 胆小 → Seikaku.Shoushin
    ///   kind = 2 冷静 → Seikaku.Reisei
    ///   kind = 3 刚胆 → Seikaku.Goutan
    ///   kind = 4 莽撞 → Seikaku.Chototsu
    /// 因此 kind 减 1 即为 Seikaku 枚举值，无需额外配置。
    /// </summary>
    public static class DuelSeikaku
    {
        /// <summary>性格数量（对应 Personalities 的 1~4）</summary>
        public const int Count = 4;

        /// <summary>自定义覆盖：personality 值 → 单挑 AI 性格。仅当项目性格数据与默认值不一致时才需要填写</summary>
        public static readonly Dictionary<int, Seikaku> PersonalityMap = new Dictionary<int, Seikaku>();

        /// <summary>兜底性格（数据缺失时使用）</summary>
        public static Seikaku Fallback = Seikaku.Reisei;

        /// <summary>自定义解析委托，置空则使用内置转换</summary>
        public static Func<Person, Seikaku> Resolver = null;

        public static Seikaku Resolve(Person person)
        {
            if (person == null) return Fallback;

            if (Resolver != null)
                return Resolver(person);

            int kind = person.mPersonality != null ? person.mPersonality.kind : 0;
            if (kind <= 0)
                kind = person.personality;

            if (PersonalityMap.TryGetValue(kind, out Seikaku mapped))
                return mapped;

            if (kind >= 1 && kind <= Count)
                return (Seikaku)(kind - 1);

            return Fallback;
        }
    }

    /// <summary>
    /// 单挑用到的特技在本项目中的 ID。
    /// 已按 Build/Content/Data/Common/Features.json 填写：20 = 捕缚，33 = 强运。
    /// </summary>
    public static class DuelFeatureId
    {
        /// <summary>强运：不会战死、不会被俘、必定退却成功</summary>
        public static int Kyouun = 33;

        /// <summary>捕缚：单挑获胜时更容易俘虏</summary>
        public static int Hobaku = 20;
    }

    /// <summary>
    /// 宝物 → 单挑宝物大类 / 特殊宝物 ID 的映射。
    ///
    /// 项目里宝物是 Equipment（继承 ItemType），其分类靠 kind(ItemKindType) + storeKind(ItemStoreKindType)。
    /// 单挑只关心"名马 / 剑 / 长武器 / 弓 / 暗器"这几类，以及三件有名宝物，
    /// 因此这里给出默认映射，并允许用 CategoryMap / SpecialMap 按宝物 Id 覆盖。
    /// </summary>
    public static class DuelItemMap
    {
        /// <summary>按宝物 Id 覆盖大类：Equipment.Id → ItemType</summary>
        public static readonly Dictionary<int, ItemType> CategoryMap = new Dictionary<int, ItemType>();

        /// <summary>按宝物 Id 覆盖特殊宝物：Equipment.Id → ItemId</summary>
        public static readonly Dictionary<int, ItemId> SpecialMap = new Dictionary<int, ItemId>();

        /// <summary>按名字片段匹配的特殊宝物（名宝）</summary>
        private static readonly (string key, ItemId id)[] s_specialNames =
        {
            ("青龙偃月刀", ItemId.BlueDragon),
            ("蛇矛",       ItemId.SerpentBlade),
            ("方天画戟",   ItemId.CrescentHalberd),
        };

        /// <summary>暗器的名称片段</summary>
        private static readonly string[] s_throwingKnifeNames = { "暗器", "手戟", "飞刀", "短戟" };

        /// <summary>解析宝物大类</summary>
        public static ItemType GetCategory(Equipment item)
        {
            if (item == null) return ItemType.None;

            if (CategoryMap.TryGetValue(item.Id, out ItemType mapped))
                return mapped;

            switch (item.kind)
            {
                case (int)ItemKindType.Equipment_Horse:
                    return ItemType.EliteHorse;
            }

            switch (item.storeKind)
            {
                case (int)ItemStoreKindType.Sword:
                    return ItemType.Sword;
                case (int)ItemStoreKindType.Spear:
                case (int)ItemStoreKindType.Halberd:
                    return ItemType.LongSpear;
                case (int)ItemStoreKindType.Crossbow:
                    return IsThrowingKnife(item) ? ItemType.ThrowingKnife : ItemType.Bow;
                case (int)ItemStoreKindType.Horse:
                    return ItemType.EliteHorse;
            }

            return ItemType.None;
        }

        /// <summary>解析特殊宝物 ID（非名宝返回 ItemId.Invalid）</summary>
        public static ItemId GetSpecialId(Equipment item)
        {
            if (item == null) return ItemId.Invalid;

            if (SpecialMap.TryGetValue(item.Id, out ItemId mapped))
                return mapped;

            string name = item.Name;
            if (string.IsNullOrEmpty(name))
                return ItemId.Invalid;

            for (int i = 0; i < s_specialNames.Length; i++)
            {
                if (name.IndexOf(s_specialNames[i].key, StringComparison.Ordinal) >= 0)
                    return s_specialNames[i].id;
            }

            return ItemId.Invalid;
        }

        /// <summary>是否为暗器</summary>
        private static bool IsThrowingKnife(Equipment item)
        {
            string name = item.Name;
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < s_throwingKnifeNames.Length; i++)
            {
                if (name.IndexOf(s_throwingKnifeNames[i], StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }

    #endregion

    #region 真实类型的单挑访问器

    /// <summary>
    /// 为 Sango.Core.Person 补齐单挑所需的访问器。
    /// 命名与真实类型已有成员冲突的（IsPlayer / IsSpouse / IsBrother / IsParentchild 等）一律加后缀区分。
    /// </summary>
    public static class PersonDuelExtensions
    {
        /// <summary>武将标识（解析为单挑内部的 PersonId）</summary>
        public static PersonId GetId(this Person self)
        {
            return DuelPersonId.Resolve(self);
        }

        /// <summary>姓名</summary>
        public static string GetName(this Person self)
        {
            return self == null ? string.Empty : self.Name;
        }

        /// <summary>所属势力 ID</summary>
        public static int GetForceId(this Person self)
        {
            if (self == null) return -1;
            return self.mBelongForce != null ? self.mBelongForce.Id : self.BelongForce;
        }

        /// <summary>所在地区（城市）ID</summary>
        public static int GetDistrictId(this Person self)
        {
            if (self == null) return -1;
            City city = self.mCurrentCity ?? self.mBelongCity;
            return city != null ? city.Id : self.BelongCity;
        }

        /// <summary>伤病程度（0=健康，越大越重）</summary>
        public static int GetShoubyou(this Person self)
        {
            return self == null ? -1 : self.injury;
        }

        /// <summary>体力</summary>
        public static int GetHp(this Person self)
        {
            return self == null ? 0 : self.stamina;
        }

        /// <summary>年龄</summary>
        public static int GetAge(this Person self)
        {
            return self == null ? 0 : self.Age;
        }

        /// <summary>单挑 AI 性格</summary>
        public static Seikaku GetSeikaku(this Person self)
        {
            return DuelSeikaku.Resolve(self);
        }

        /// <summary>忠诚度</summary>
        public static int GetLoyalty(this Person self)
        {
            return self == null ? 0 : self.loyalty;
        }

        /// <summary>出生地 ID</summary>
        public static int GetBirthplaceId(this Person self)
        {
            return self == null ? -1 : self.birthplace;
        }

        /// <summary>势力颜色</summary>
        public static int GetColor(this Person self)
        {
            return self == null ? 0 : DuelSettings.GetForceColor(self);
        }

        /// <summary>获取指定能力值</summary>
        public static int GetStat(this Person self, PersonStatType type)
        {
            if (self == null) return 0;
            switch (type)
            {
                case PersonStatType.Strength: return self.Strength;
                case PersonStatType.Command: return self.Command;
                case PersonStatType.Intelligence: return self.Intelligence;
                case PersonStatType.Politics: return self.Politics;
            }
            return 0;
        }

        /// <summary>按伤病计算后的能力值</summary>
        public static int CalcStat(this Person self, PersonStatType type, int shoubyou)
        {
            if (self == null) return 0;
            if (type == PersonStatType.Strength && DuelSettings.CalcStrength != null)
                return DuelSettings.CalcStrength(self, shoubyou);
            return self.GetStat(type);
        }

        /// <summary>是否拥有指定特技（对应单挑内部 SkillId）</summary>
        public static bool HasSkill(this Person self, SkillId skill)
        {
            if (self == null) return false;
            switch (skill)
            {
                case SkillId.Kyouun:
                    return DuelFeatureId.Kyouun >= 0 && self.HasFeatrue(DuelFeatureId.Kyouun);
            }
            return false;
        }

        /// <summary>是否拥有指定特技 Id</summary>
        public static bool HasFeatureId(this Person self, int featureId)
        {
            return self != null && featureId >= 0 && self.HasFeatrue(featureId);
        }

        /// <summary>
        /// 是否玩家武将。真实 Person 已有 IsPlayer 属性，故改名以避免成员冲突。
        /// 君主/都督由玩家控制时同样算玩家武将。
        /// </summary>
        public static bool IsPlayerPerson(this Person self)
        {
            return self != null && (self.IsPlayer || self.IsPlayerControl);
        }

        /// <summary>是否君主</summary>
        public static bool IsKunshu(this Person self)
        {
            return self != null && self.state == (int)PersonStateType.Governor;
        }

        /// <summary>是否为血亲 / 配偶 / 义兄弟（用于一击必杀与俘虏判定的豁免）</summary>
        public static bool IsFamily(this Person self, Person other)
        {
            if (self == null || other == null || self == other) return false;
            if (self.IsParentchild(other)) return true;
            if (self.IsSpouse(other)) return true;
            if (self.IsBrother(other)) return true;
            return false;
        }

        /// <summary>对方是否为义兄弟</summary>
        public static bool IsGikyoudai(this Person self, Person other)
        {
            if (self == null || other == null) return false;
            return self.IsBrother(other);
        }

        /// <summary>对方是否为血亲（血缘相同或父子）</summary>
        public static bool IsKetsuen(this Person self, Person other)
        {
            if (self == null || other == null) return false;
            if (self.consanguinity > 0 && self.consanguinity == other.consanguinity) return true;
            return self.IsParentchild(other);
        }

        /// <summary>与对方的相性距离（0~150，越小越亲近）</summary>
        public static int GetAishouDistance(this Person self, Person other)
        {
            if (self == null || other == null) return 75;
            return self.CompatibilityDistance(other);
        }
    }

    /// <summary>为 Sango.Core.Equipment（宝物）补齐单挑所需的访问器</summary>
    public static class EquipmentDuelExtensions
    {
        /// <summary>宝物大类。对应 C++ Item::get_type</summary>
        public static ItemType GetTypeValue(this Equipment self)
        {
            return DuelItemMap.GetCategory(self);
        }

        /// <summary>特殊宝物 ID。对应 C++ Item::get_id</summary>
        public static ItemId GetItemId(this Equipment self)
        {
            return DuelItemMap.GetSpecialId(self);
        }
    }

    /// <summary>
    /// 为 Sango.Core.Troop（部队）补齐单挑所需的访问器。
    /// 命名与真实类型已有成员冲突的一律加后缀区分。
    /// </summary>
    public static class TroopDuelExtensions
    {
        /// <summary>是否为玩家操作。真实 Troop 已有 IsPlayer 属性，故改名以避免成员冲突</summary>
        public static bool IsPlayerControlled(this Troop self)
        {
            return self != null && self.IsPlayer;
        }

        /// <summary>兵力</summary>
        public static int GetTroops(this Troop self)
        {
            return self == null ? 0 : self.troops;
        }

        /// <summary>
        /// 增减士气，返回实际变化量。
        /// 真实 Troop 走 ChangeMorale(增量)，故这里计算 clamp 后的实际差值。
        /// </summary>
        public static int AddEnergy(this Troop self, int value)
        {
            if (self == null) return 0;
            int before = self.morale;
            int target = Math.Max(0, Math.Min(self.MaxMorale, before + value));
            int delta = target - before;
            if (delta != 0)
                self.ChangeMorale(delta);
            return delta;
        }

        /// <summary>
        /// 同步兵装数量。对应 C++ Unit::SyncEquipmentQuantity。
        /// 项目暂无"单挑导致兵装损耗"的设定，这里保留接口空实现，便于后续扩展。
        /// </summary>
        public static void SyncEquipmentQuantity(this Troop self)
        {
            // TODO: 若后续加入单挑兵装损耗，在此同步 troop 的兵装数量
        }

        /// <summary>获取坐标（格子）</summary>
        public static object GetPos(this Troop self)
        {
            return self == null ? null : (object)self.cell;
        }

        /// <summary>势力颜色（0xRRGGBB）</summary>
        public static int GetColor(this Troop self)
        {
            Flag flag = self?.mBelongForce?.mFlag;
            if (flag == null) return 0;
            Color32 c = flag.color;
            return (c.r << 16) | (c.g << 8) | c.b;
        }

        /// <summary>是否包含指定武将</summary>
        public static bool HasMember(this Troop self, PersonId id)
        {
            if (self == null || id == PersonId.Invalid) return false;
            if (DuelPersonId.Resolve(self.Leader) == id) return true;
            if (DuelPersonId.Resolve(self.Member1) == id) return true;
            if (DuelPersonId.Resolve(self.Member2) == id) return true;
            return false;
        }
    }

    /// <summary>
    /// 为 Sango.Core.Force（势力）补齐单挑所需的访问器。
    /// 命名与真实类型已有成员冲突的一律加后缀区分。
    /// </summary>
    public static class ForceDuelExtensions
    {
        /// <summary>势力 ID</summary>
        public static int GetId(this Force self)
        {
            return self == null ? -1 : self.Id;
        }

        /// <summary>是否为一般势力（非异族等在野势力，不参与俘虏/死亡结算）</summary>
        public static bool IsNormal(this Force self)
        {
            if (self == null) return false;
            return !IsBarbarian(self);
        }

        /// <summary>
        /// 判断是否为异族势力。
        /// 项目当前没有显式的异族标记，预留此钩子：接入异族数据后在此返回 true。
        /// </summary>
        private static bool IsBarbarian(Force self)
        {
            return false;
        }

        /// <summary>是否为玩家势力。真实 Force 已有 IsPlayer 属性，故改名以避免成员冲突</summary>
        public static bool IsPlayerForce(this Force self)
        {
            return self != null && self.IsPlayer;
        }

        /// <summary>获取对某势力的友好度。对应 C++ Force::get_like</summary>
        public static int GetLike(this Force self, int forceId)
        {
            if (self == null || Scenario.Cur == null) return 0;
            Force other = Scenario.Cur.forceSet.Get(forceId);
            if (other == null) return 0;
            return Scenario.Cur.GetRelation(self, other);
        }
    }

    #endregion
}
