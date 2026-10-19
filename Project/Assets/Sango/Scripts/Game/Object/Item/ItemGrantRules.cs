using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 授予 / 没收珍品（宝物与装备）的**统一规则层**。
    ///
    /// 【为什么单独成类】
    ///   授予与没收改的是同一批状态（国库 / 武将背包 / 装备栏 / 随身宝 / 忠诚 / 特效动作表），
    ///   只是方向相反。玩家侧入口（CityGrantItem）只负责"能不能点、给谁点"，
    ///   数据侧（City.JobGrantItem / City.JobConfiscateItem）只负责扣行动力与记账，
    ///   两边的规则口径**全部**收口在本类，避免出现"窗口允许、执行失败"这种分叉
    ///   （与 CityReward 的 IsRewardTarget 同思路）。
    ///
    /// 【状态口径】
    ///   一件珍品（<see cref="ItemType.IsRareItem"/>）在同一时刻只可能存在三个位置之一：
    ///     ① 势力国库（<see cref="Force.Treasury"/>）—— 未分配
    ///     ② 武将背包（<see cref="Person.itemStore"/>）—— 已分配
    ///     ③ 武将装备栏 / 随身宝（<see cref="Person.EquippedWeapon"/> 等）—— 已分配且已生效
    ///   因为珍品的 storeKind 就是自身 Id（每件唯一），所以"背包里有"与"已装备"是同时成立的：
    ///   装备栏是**视图**，背包是**账本**。授予 / 没收必须同时改这两处，否则会出现
    ///   "卸下来了但背包里还在"这类幽灵道具。
    ///
    /// 【栏位规则】（与 <see cref="ItemType.GetEquipSlot"/> 同一口径）
    ///   · 武器 / 铠甲 / 马匹（含宝物里的宝剑、长柄、暗器、宝弓、名马）→ 装入对应装备栏才生效
    ///   · 其余宝物（书籍 / 玉器 / 珍玩 / 兵符）→ 随身携带，每人限 1 件，授予即生效
    ///   替换时**被换下的旧道具退回国库**（不留在武将背包里），见 <see cref="Grant"/>。
    ///
    /// 【忠诚曲线】一律走 <see cref="GainValueConfig"/> 的 itemGrantGain 表（规则 32），
    ///   授予 +(基础 + 品级 × 每级)、没收 -(基础 + 品级 × 每级)，默认值见 GainValueConfig.DefaultOf。
    ///   忠诚钳制 0~100 由 <see cref="Person.AddLoyalty"/> 统一处理，本类只算增量。
    ///   没收后忠诚过低的"出走判定"复用现有掉忠 → 出走链路，本类不新增规则。
    /// </summary>
    public static class ItemGrantRules
    {
        #region 拒绝原因（直接给 UI 显示，避免各处自己拼文案）

        /// <summary>校验通过</summary>
        public const string ReasonNone = "";
        /// <summary>道具不是珍品（宝物 / 装备）</summary>
        public const string ReasonNotRare = "只有宝物与装备可以授予 / 没收";
        /// <summary>国库里没有这件珍品</summary>
        public const string ReasonNoTreasuryItem = "国库里没有这件珍品";
        /// <summary>国库已满</summary>
        public const string ReasonTreasuryFull = "国库已满，请先腾出空位";
        /// <summary>对象不是本势力在职武将</summary>
        public const string ReasonNotBelong = "只能授予 / 没收本势力武将的物品";
        /// <summary>对象不在本城（出征在外或已调离）</summary>
        public const string ReasonNotInCity = "只能对本城武将操作";
        /// <summary>该武将身上没有这件珍品</summary>
        public const string ReasonNoPersonItem = "该武将身上没有这件珍品";

        #endregion

        /// <summary>
        /// 是否为可授予 / 没收的对象。
        ///
        /// 口径（对应决策点 D4：限"在本城"）：
        ///   · 存活、非在野、非俘虏（被俘 / 在野武将不领本势力赏赐）
        ///   · 属于目标城市所属势力
        ///   · 归属城市就是执行操作的城市
        ///   · **未随军出征**（在外的武将 grant 会让忠诚掉/涨发生在战场上，本期不做）
        /// 注意：不要求武将"本回合空闲"——授予 / 没收不占用被操作武将的行动
        /// （该武将当回合仍可内政 / 出征），只消耗势力的行动力。
        /// </summary>
        /// <param name="person">待判定武将</param>
        /// <param name="city">执行操作的城市（即发起势力在本城）</param>
        /// <returns>可以作为授予 / 没收对象返回 true</returns>
        public static bool IsGrantTarget(Person person, City city)
        {
            if (person == null || city == null)
                return false;

            if (!person.IsAlive || person.IsDead || person.IsWild || person.IsPrisoner)
                return false;

            Force force = city.BelongForce;
            if (force == null || person.BelongForce != force)
                return false;

            if (person.BelongCity != city)
                return false;

            // 随军出征 = 不在本城
            if (person.mBelongTroop != null)
                return false;

            return true;
        }

        /// <summary>
        /// 授予该珍品时武将获得的忠诚增量（正数）。
        /// 公式：基础 + 品级 × 每级增量；品级缺失（0）时只给基础值。
        /// </summary>
        /// <param name="item">待授予的珍品</param>
        /// <returns>忠诚增量（&gt;= 0）</returns>
        public static int GetGrantLoyalty(ItemType item)
        {
            if (item == null)
                return 0;

            int grade = item.grade > 0 ? item.grade : 0;
            int loyalty = GainValueConfig.ItemGrant(GainPlace.ItemGrantLoyaltyBase)
                        + grade * GainValueConfig.ItemGrant(GainPlace.ItemGrantLoyaltyPerGrade);
            return loyalty < 0 ? 0 : loyalty;
        }

        /// <summary>
        /// 没收该珍品时武将损失的忠诚值（**正数**，调用方按负向扣减）。
        /// 公式：基础 + 品级 × 每级增量；比授予略高，使"授予后再没收"成为净亏，堵住刷忠诚的漏洞。
        /// </summary>
        /// <param name="item">待没收的珍品</param>
        /// <returns>忠诚损失量（&gt;= 0）</returns>
        public static int GetConfiscateLoyalty(ItemType item)
        {
            if (item == null)
                return 0;

            int grade = item.grade > 0 ? item.grade : 0;
            int loyalty = GainValueConfig.ItemGrant(GainPlace.ItemConfiscateLoyaltyBase)
                        + grade * GainValueConfig.ItemGrant(GainPlace.ItemConfiscateLoyaltyPerGrade);
            return loyalty < 0 ? 0 : loyalty;
        }

        /// <summary>
        /// 列出国库里当前可授予的珍品（未分配状态）。
        /// 只做归类，不排序；排序由 UI 侧按品级 / 名称处理。
        /// </summary>
        /// <param name="force">势力</param>
        /// <param name="result">结果列表（会先清空）</param>
        public static void GetGrantItemList(Force force, List<ItemType> result)
        {
            if (result == null)
                return;

            result.Clear();
            if (force == null)
                return;

            force.GetTreasuryItems(result, true);
        }

        /// <summary>
        /// 列出该武将身上（装备栏 + 随身宝 + 背包账本）所有可被没收的珍品。
        /// 以背包账本为准遍历：装备栏里的道具必然也记账在背包里（见类注释）。
        /// </summary>
        /// <param name="person">武将</param>
        /// <param name="result">结果列表（会先清空）</param>
        public static void GetConfiscatableItems(Person person, List<ItemType> result)
        {
            if (result == null)
                return;

            result.Clear();
            if (person == null || person.itemStore == null)
                return;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null)
                return;

            foreach (KeyValuePair<int, int> kv in person.itemStore.Items)
            {
                if (kv.Value <= 0)
                    continue;

                ItemType item = scenario.CommonData.ItemTypes.Get(kv.Key);
                if (item != null && item.IsRareItem())
                    result.Add(item);
            }
        }

        /// <summary>
        /// 校验"能否把国库里的这件珍品授予该武将"。
        /// UI 的确认按钮与执行侧共用本方法，避免口径分叉。
        /// </summary>
        /// <param name="city">执行操作的城市</param>
        /// <param name="person">目标武将</param>
        /// <param name="item">待授予珍品</param>
        /// <param name="reason">不通过时的中文原因（通过时为空串）</param>
        /// <returns>可授予返回 true</returns>
        public static bool CanGrant(City city, Person person, ItemType item, out string reason)
        {
            if (item == null || !item.IsRareItem())
            {
                reason = ReasonNotRare;
                return false;
            }

            if (!IsGrantTarget(person, city))
            {
                reason = person != null && city != null && person.BelongCity != city
                    ? ReasonNotInCity : ReasonNotBelong;
                return false;
            }

            Force force = city.BelongForce;
            // 国库里必须真有这件（一件珍品只可能在一个位置，见类注释）
            if (!force.HasTreasury(item))
            {
                reason = ReasonNoTreasuryItem;
                return false;
            }

            reason = ReasonNone;
            return true;
        }

        /// <summary>
        /// 校验"能否从该武将身上没收这件珍品"。
        /// 注意国库容量：没收是**往国库放**，所以国库满时直接拒绝（授予则不受此限，
        /// 因为它是先取出再放回被替换的旧件，净占位不增加）。
        /// 国库容量 v1 暂不设上限（策划案 4.2，<see cref="Force.TreasuryLimit"/> 为 0 即不限），
        /// 此时 <see cref="Force.CanAddTreasury"/> 恒为 true，<see cref="ReasonTreasuryFull"/> 不会触发。
        /// </summary>
        /// <param name="city">执行操作的城市</param>
        /// <param name="person">目标武将</param>
        /// <param name="item">待没收珍品</param>
        /// <param name="reason">不通过时的中文原因（通过时为空串）</param>
        /// <returns>可没收返回 true</returns>
        public static bool CanConfiscate(City city, Person person, ItemType item, out string reason)
        {
            if (item == null || !item.IsRareItem())
            {
                reason = ReasonNotRare;
                return false;
            }

            if (!IsGrantTarget(person, city))
            {
                reason = person != null && city != null && person.BelongCity != city
                    ? ReasonNotInCity : ReasonNotBelong;
                return false;
            }

            if (!person.HasItem(item.Id))
            {
                reason = ReasonNoPersonItem;
                return false;
            }

            Force force = city.BelongForce;
            if (force == null || !force.CanAddTreasury(1))
            {
                reason = ReasonTreasuryFull;
                return false;
            }

            reason = ReasonNone;
            return true;
        }

        /// <summary>
        /// 执行授予：国库取出 → 武将背包记账 → 装入装备栏或随身宝（旧件退回国库）→ 加忠诚 → 重建特效。
        ///
        /// 顺序说明（不能随意调换）：
        ///   ① 先从国库取走待授予道具，再处理被替换的旧件 ——
        ///      这样"国库刚好满"时也能完成替换（取出 1 件刚好腾出旧件的位置）。
        ///   ② 旧件必须先从武将背包移除，再放回国库，避免出现"背包与国库同时有"的重复记账。
        /// </summary>
        /// <param name="city">执行操作的城市</param>
        /// <param name="person">目标武将</param>
        /// <param name="item">待授予珍品</param>
        /// <param name="reason">不通过时的中文原因（通过时为空串）</param>
        /// <returns>被替换下来并已退回国库的旧道具；没有替换则返回 null</returns>
        public static ItemType Grant(City city, Person person, ItemType item, out string reason)
        {
            if (!CanGrant(city, person, item, out reason))
                return null;

            Force force = city.BelongForce;
            force.RemoveTreasury(item, 1);
            person.itemStore.Add(item, 1);

            // 装入对应栏位；被换下的旧件退回国库（决策点 D6）
            ItemType replaced = EquipToSlot(person, item);
            if (replaced != null && replaced != item)
            {
                person.itemStore.Remove(replaced, 1);
                if (force.AddTreasury(replaced, 1) < 0)
                {
                    // 理论上不会发生（刚取出过一件），留日志便于排查异常数据
                    Sango.Log.Warning($"[授予]{person.Name}被替换的{replaced.Name}无法退回国库（{force.Name}国库已满）。");
                }
            }

            int gain = GetGrantLoyalty(item);
            int old = person.loyalty;
            person.AddLoyalty(gain);
            person.BuildEquipActions();

            Sango.Log.Info($"@内政@[{force.Name}]<{city.Name}>授予{person.Name}{item.Name}，" +
                           $"忠诚 {old}→{person.loyalty}（+{gain}）" +
                           (replaced != null ? $"，替换下{replaced.Name}已退回国库" : ""));

            reason = ReasonNone;
            return replaced;
        }

        /// <summary>
        /// 执行没收：先从装备栏卸下（若是已装备的道具）→ 从背包除名 → 放回国库 → 扣忠诚 → 重建特效。
        /// </summary>
        /// <param name="city">执行操作的城市</param>
        /// <param name="person">目标武将</param>
        /// <param name="item">待没收珍品</param>
        /// <param name="reason">不通过时的中文原因（通过时为空串）</param>
        /// <returns>被没收的珍品；未执行返回 null</returns>
        public static ItemType Confiscate(City city, Person person, ItemType item, out string reason)
        {
            if (!CanConfiscate(city, person, item, out reason))
                return null;

            Force force = city.BelongForce;

            // ① 若正装备在栏位上，先卸下（UnequipXxx 内部会重建特效）
            UnequipFromSlot(person, item);
            // ② 背包除名（装备栏与背包是"视图 / 账本"的关系，必须同时清）
            person.itemStore.Remove(item, 1);
            // ③ 进国库（CanConfiscate 已确认有位）
            force.AddTreasury(item, 1);

            int loss = GetConfiscateLoyalty(item);
            int old = person.loyalty;
            person.AddLoyalty(-loss);
            person.BuildEquipActions();

            Sango.Log.Info($"@内政@[{force.Name}]<{city.Name}>没收{person.Name}的{item.Name}，" +
                           $"忠诚 {old}→{person.loyalty}（-{loss}）");

            reason = ReasonNone;
            return item;
        }

        /// <summary>
        /// 把珍品装到它该待的位置：装备类与宝物里的武器 / 名马进装备栏，其余宝物作随身宝。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="item">待装珍品</param>
        /// <returns>该栏位上原本的道具（被替换下来）；原本为空返回 null</returns>
        static ItemType EquipToSlot(Person person, ItemType item)
        {
            int slot = item.GetEquipSlot();
            switch ((EquipmentKind)slot)
            {
                case EquipmentKind.Weapon:
                    {
                        ItemType old = person.EquippedWeapon;
                        person.EquipWeapon(item);
                        return old;
                    }
                case EquipmentKind.Armor:
                    {
                        ItemType old = person.EquippedArmor;
                        person.EquipArmor(item);
                        return old;
                    }
                case EquipmentKind.Horse:
                    {
                        ItemType old = person.EquippedHorse;
                        person.EquipHorse(item);
                        return old;
                    }
                default:
                    {
                        // 其余宝物：随身 1 件，替换时旧宝退回国库
                        ItemType old = person.EquippedTreasure;
                        person.EquipTreasure(item);
                        return old;
                    }
            }
        }

        /// <summary>
        /// 若该珍品正装在某个栏位上则卸下；不在栏位上时不做任何事（只从背包除名即可）。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="item">待卸下的珍品</param>
        static void UnequipFromSlot(Person person, ItemType item)
        {
            if (person.EquippedWeapon == item)
            {
                person.UnequipWeapon();
            }
            else if (person.EquippedArmor == item)
            {
                person.UnequipArmor();
            }
            else if (person.EquippedHorse == item)
            {
                person.UnequipHorse();
            }
            else if (person.EquippedTreasure == item)
            {
                person.UnequipTreasure();
            }
        }
    }
}
