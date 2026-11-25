using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core.Action
{
    /// <summary>
    /// 光环类效果基类：以一个格子为中心，给半径内的部队提供常驻加成。
    ///
    /// 子类只需要提供"光环中心"和"归属势力"，范围判定、目标进出处理、叠加与回退都在本类完成。
    /// 目前有两个宿主：
    ///   TroopAura    部队光环：中心 = 该部队所在格子（部队移动时自动重算）
    ///   BuildingAura 建筑光环：中心 = 建筑中心格
    ///
    /// 参数：
    /// value      光环数值（可为负，负数即减益光环）
    /// bound      光环半径（格）
    /// auraType   作用属性：0 攻击力 / 1 防御力
    /// targetType 作用对象：0 己方 / 1 敌方 / 2 所有
    /// stackMode  叠加规则：0 多个光环取最大值 / 1 累加
    ///
    /// 接入事件：OnTroopEnterCell（目标部队进 / 出光环范围，以及部队光环的源部队自身移动）。
    /// 属性落在 troop.extraAttack / troop.extraDefence 上，不会被属性重算重置。
    /// </summary>
    public abstract class AuraActionBase : ActionBase
    {
        /// <summary>作用属性：攻击力（写 Troop.extraAttack）</summary>
        public const int AURA_ATTACK = 0;
        /// <summary>作用属性：防御力（写 Troop.extraDefence）</summary>
        public const int AURA_DEFENCE = 1;
        /// <summary>作用属性：物理战法会心率（百分点，判定时查询）</summary>
        public const int AURA_CRITICAL_RATE = 2;
        /// <summary>作用属性：暴击倍率（百分点，判定时查询）</summary>
        public const int AURA_CRITICAL_DAMAGE = 3;
        /// <summary>作用属性：计策成功率（百分点，判定时查询）</summary>
        public const int AURA_SKILL_SUCCESS = 4;
        /// <summary>作用属性：计策识破率（百分点，降低对方成功率）</summary>
        public const int AURA_SKILL_RESIST = 5;
        /// <summary>作用属性：辅助攻击概率（百分点，写 Troop.auraAssistChance）</summary>
        public const int AURA_ASSIST = 6;
        /// <summary>作用属性：每回合气力回复（点，回合开始时结算）</summary>
        public const int AURA_MORALE = 7;
        /// <summary>作用属性：陆地移动力（+格，读 Troop.MoveAbility）</summary>
        public const int AURA_MOVE_LAND = 8;
        /// <summary>作用属性：水上移动力（+格，读 Troop.MoveAbility）</summary>
        public const int AURA_MOVE_WATER = 9;
        /// <summary>作用属性：建设力（+点，读 Troop.BuildPower）</summary>
        public const int AURA_BUILD_POWER = 10;
        /// <summary>作用属性：对部队伤害（百分比，伤害结算时乘）</summary>
        public const int AURA_DAMAGE_TROOP = 11;
        /// <summary>作用属性：对建筑耐久伤害（百分比）</summary>
        public const int AURA_DAMAGE_BUILDING = 12;
        /// <summary>作用属性：对据点守军伤害（百分比）</summary>
        public const int AURA_DAMAGE_BUILDING_TROOP = 13;
        /// <summary>作用属性：受到伤害减免（百分比，受击时乘）</summary>
        public const int AURA_DAMAGE_REDUCE = 14;
        /// <summary>作用属性：单挑触发概率（+百分点）</summary>
        public const int AURA_DUEL_CHANCE = 15;
        /// <summary>作用属性：俘虏敌将概率（+百分点）</summary>
        public const int AURA_CAPTIVE_CHANCE = 16;
        /// <summary>作用属性：粮食消耗（百分比，负值减耗）</summary>
        public const int AURA_FOOD_COST = 17;
        /// <summary>作用属性：击破获取金币（+百分点）</summary>
        public const int AURA_GOLD_GAIN = 18;
        /// <summary>作用属性：击破获取粮食（+百分点）</summary>
        public const int AURA_FOOD_GAIN = 19;
        /// <summary>作用属性：每回合伤兵回复（伤兵总数的百分比）</summary>
        public const int AURA_WOUNDED_RECOVER = 20;
        /// <summary>作用属性：战法气力消耗降低（百分比，负值加耗）</summary>
        public const int AURA_SKILL_COST_REDUCE = 21;
        /// <summary>作用属性：计略气力消耗降低（百分比，负值加耗）</summary>
        public const int AURA_STRATEGY_COST_REDUCE = 22;
        /// <summary>作用属性：计略释放范围增加（+格）</summary>
        public const int AURA_STRATEGY_RANGE = 23;
        /// <summary>作用属性：每回合自动灭火（value = 扑灭个数）</summary>
        public const int AURA_FIRE_EXTINGUISH = 24;
        /// <summary>作用属性：每回合随机放火（value = 放火个数，turns = 持续回合）</summary>
        public const int AURA_FIRE_IGNITE = 25;
        /// <summary>作用属性：每回合对范围内敌军造成伤害（value = 伤害点数）</summary>
        public const int AURA_AREA_DAMAGE = 26;
        /// <summary>作用属性：每回合对范围内敌军随机添加 BUFF（buffIds 池，probability 万分比）</summary>
        public const int AURA_AREA_BUFF = 27;
        /// <summary>作用属性：每回合兵力回复（点，区别于伤兵回复）</summary>
        public const int AURA_TROOP_RECOVER = 28;
        /// <summary>作用属性：气力上限（+点）</summary>
        public const int AURA_MAX_MORALE = 29;
        /// <summary>作用属性：战法释放范围增加（+格，不含计略）</summary>
        public const int AURA_SKILL_RANGE = 30;
        /// <summary>作用属性：火焰伤害（百分比）</summary>
        public const int AURA_FIRE_DAMAGE = 31;
        /// <summary>作用属性：技巧点获取（百分比）</summary>
        public const int AURA_TECHNIQUE_GAIN = 32;
        /// <summary>作用属性：防止武将受伤（value &gt; 0 生效）</summary>
        public const int AURA_PREVENT_INJURY = 33;
        /// <summary>作用属性：防止战法位移（value &gt; 0 生效）</summary>
        public const int AURA_PREVENT_SKILL_OFFSET = 34;
        /// <summary>作用属性：防止 BUFF（value &gt; 0 生效）</summary>
        public const int AURA_PREVENT_BUFF = 35;
        /// <summary>作用属性：无视火焰移动消耗（value &gt; 0 生效）</summary>
        public const int AURA_IGNORE_FIRE = 36;
        /// <summary>作用属性：无视 ZOC（value &gt; 0 生效）</summary>
        public const int AURA_IGNORE_ZOC = 37;
        /// <summary>
        /// 作用属性：伤兵产生率（+百分点，基础 14）。
        ///
        /// 例：value=10 → 14+10=24%；value=-4 → 10%。
        /// 注意这是**加百分点**，不是 14×(100+value)/100 的百分比缩放。
        /// </summary>
        public const int AURA_WOUNDED_RATE = 38;
        /// <summary>作用属性：兵种适性经验获取（百分比）</summary>
        public const int AURA_ABILITY_EXP_GAIN = 39;
        /// <summary>作用属性：功绩获取（百分比）</summary>
        public const int AURA_MERIT_GAIN = 40;
        /// <summary>作用属性：武将等级经验获取（百分比）</summary>
        public const int AURA_EXP_GAIN = 41;
        /// <summary>作用属性：能力经验获取（百分比）</summary>
        public const int AURA_ATTRIBUTE_EXP_GAIN = 42;
        /// <summary>作用属性：受到伤害反弹（百分比，反弹给攻击方）</summary>
        public const int AURA_REFLECT_DAMAGE = 43;
        /// <summary>作用属性：降低被部队反击概率（百分比）</summary>
        public const int AURA_REDUCE_COUNTER_CHANCE = 44;
        /// <summary>作用属性：战法命中波及范围（+格）</summary>
        public const int AURA_SKILL_ATTACK_RANGE = 45;
        /// <summary>作用属性：战法连击概率（万分比，追加同战法且不扣气力）</summary>
        public const int AURA_SKILL_COMBO = 46;
        /// <summary>作用属性：普攻连击概率（万分比，追加一次普攻且不扣气力）</summary>
        public const int AURA_NORMAL_COMBO = 47;
        /// <summary>作用属性：免疫火焰伤害（value &gt; 0 生效，区别于 36 只免移动消耗）</summary>
        public const int AURA_FIRE_IMMUNE = 48;
        /// <summary>作用属性：降低被建筑反击概率（百分比）</summary>
        public const int AURA_REDUCE_BUILDING_COUNTER = 49;

        /// <summary>叠加规则：多个光环取最大值</summary>
        public const int STACK_MAX = 0;
        /// <summary>叠加规则：多个光环数值累加</summary>
        public const int STACK_SUM = 1;

        /// <summary>光环数值</summary>
        protected int value;
        /// <summary>光环半径（格）</summary>
        protected int bound;
        /// <summary>
        /// 作用属性：
        /// 0 攻击 / 1 防御 / 2 战法会心率 / 3 暴击倍率 / 4 计策成功率 /
        /// 5 计策识破率 / 6 辅助攻击概率 / 7 气力回复 /
        /// 8 陆地移动力 / 9 水上移动力 / 10 建设力 /
        /// 11 对部队伤害 / 12 对建筑耐久伤害 / 13 对据点守军伤害 / 14 受到伤害减免 /
        /// 15 单挑概率 / 16 俘虏概率 / 17 粮食消耗 / 18 金币获取 / 19 粮食获取 /
        /// 20 伤兵回复 / 21 战法气力消耗降低 / 22 计略气力消耗降低 / 23 计略释放范围 /
        /// 24 自动灭火 / 25 自动放火 / 26 范围伤害 / 27 范围 BUFF /
        /// 28 兵力回复 / 29 气力上限 / 30 战法释放范围 / 31 火焰伤害 / 32 技巧点获取 /
        /// 33 防止受伤 / 34 防止位移 / 35 防止 BUFF / 36 无视火焰移动 / 37 无视 ZOC /
        /// 38 伤兵率 / 39 适性经验 / 40 功绩 / 41 等级经验 / 42 能力经验 / 43 反伤 /
        /// 44 被部队反击 / 45 战法波及范围 / 46 战法连击 / 47 普攻连击 /
        /// 48 免疫火焰伤害 / 49 被建筑反击。
        /// </summary>
        protected int auraType;
        /// <summary>作用对象（0 己方 / 1 敌方 / 2 所有）</summary>
        protected int targetType;
        /// <summary>叠加规则（0 取最大 / 1 累加）</summary>
        protected int stackMode;

        /// <summary>持续回合数（放火 / BUFF 用，默认 3）</summary>
        protected int turns = 3;

        /// <summary>触发概率（万分比，BUFF 用，默认 10000 = 必中）</summary>
        protected int probability = 10000;

        /// <summary>BUFF 候选池（auraType = 27 时随机取一个；auraType = 35 时作为免疫 Id 列表）</summary>
        protected int[] buffIds;

        /// <summary>BUFF 类别池（auraType = 35 时按 Buff.kind 过滤免疫）</summary>
        protected int[] buffKinds;

        /// <summary>回合型范围效果的格子缓冲（灭火 / 放火共用）</summary>
        static readonly List<Cell> auraCellBuffer = new List<Cell>(64);

        /// <summary>势力对象（技法装配时有值）</summary>
        protected Force Force { get; set; }

        /// <summary>光环当前覆盖的部队</summary>
        readonly List<Troop> targets = new List<Troop>();

        /// <summary>光环数值（供叠加计算读取）</summary>
        public int Value => value;
        /// <summary>光环半径（格，供范围显示读取）</summary>
        public int Range => bound;
        /// <summary>作用属性（供叠加计算读取）</summary>
        public int AuraType => auraType;
        /// <summary>叠加规则（供叠加计算读取）</summary>
        public int StackMode => stackMode;

        /// <summary>
        /// 光环中心格；为空表示光环当前不可用
        /// </summary>
        protected abstract Cell CenterCell { get; }

        /// <summary>
        /// 光环归属势力；用于敌我判定
        /// </summary>
        protected abstract Force OwnerForce { get; }

        /// <summary>
        /// 光环源部队（部队光环有值；建筑光环为 null）。
        /// 供回合型效果（放火 / 伤害 / BUFF）作为来源与伤害归属。
        /// </summary>
        protected virtual Troop OwnerTroop => null;

        /// <summary>
        /// 光环源建筑（建筑光环有值；部队光环为 null）。
        /// 回合型效果（24~27）在建筑所属势力的回合开始时结算。
        /// </summary>
        protected virtual BuildingBase OwnerBuilding => null;

        /// <summary>
        /// 光环是否有效（源部队存活 / 建筑未摧毁 且 光环半径配置合法）
        /// </summary>
        public bool IsActive => bound > 0 && CenterCell != null && OwnerForce != null;

        /// <summary>
        /// 初始化光环参数并订阅移动事件，然后把范围内已有部队补挂一次
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            auraType = p.Value<int>("auraType");
            targetType = p.Value<int>("targetType");
            stackMode = p.Value<int>("stackMode");

            // 回合型效果参数（放火 / BUFF）
            turns = p.Value<int>("turns");
            if (turns <= 0) turns = 3;
            probability = p.Value<int>("probability");
            if (probability <= 0) probability = 10000;
            JArray buffIdsArray = p.Value<JArray>("buffIds");
            if (buffIdsArray != null)
                buffIds = buffIdsArray.ToObject<int[]>();
            JArray buffKindsArray = p.Value<JArray>("buffKinds");
            if (buffKindsArray != null)
                buffKinds = buffKindsArray.ToObject<int[]>();

            // 子类先解析宿主，再调用本方法
            if (!IsActive)
                return;

            GameEvent.OnTroopEnterCell += OnTroopEnterCell;

            // 回合型范围效果（24~27）：
            //   · 部队光环 → 源部队自己的回合开始时结算；
            //   · 建筑光环 → 建筑所属势力的回合开始时结算（建筑没有“部队回合”）。
            if (auraType >= AURA_FIRE_EXTINGUISH && auraType <= AURA_AREA_BUFF)
            {
                if (OwnerBuilding != null)
                    GameEvent.OnForceTurnStart += OnForceTurnStart;
                else
                    GameEvent.OnTroopTurnStart += OnTroopTurnStart;
            }

            // 事件否决型效果（33~35）：被保护部队在对应判定点直接否决
            if (auraType == AURA_PREVENT_INJURY)
                GameEvent.OnPersonCheckInjury += OnPersonCheckInjury;
            else if (auraType == AURA_PREVENT_SKILL_OFFSET)
                GameEvent.OnTroopCheckSkillOffset += OnTroopCheckSkillOffset;
            else if (auraType == AURA_PREVENT_BUFF)
                GameEvent.OnTroopCheckBuff += OnTroopCheckBuff;

            // 效果生成时范围内可能已经有部队驻留，先补挂一次
            CenterCell.Spiral(bound, (cell) =>
            {
                if (cell != null && cell.troop != null)
                    AddTarget(cell.troop);
            });
        }

        /// <summary>
        /// 光环是事件驱动的常驻效果（随位置变化自动增减），不需要外部再主动触发
        /// </summary>
        /// <param name="trigger">触发事件（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 清理光环：退订事件，并把已经加上去的加成全部回退
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            GameEvent.OnForceTurnStart -= OnForceTurnStart;
            GameEvent.OnPersonCheckInjury -= OnPersonCheckInjury;
            GameEvent.OnTroopCheckSkillOffset -= OnTroopCheckSkillOffset;
            GameEvent.OnTroopCheckBuff -= OnTroopCheckBuff;

            for (int i = 0; i < targets.Count; i++)
            {
                Troop troop = targets[i];
                if (troop != null)
                    AuraManager.Remove(troop, this);
            }
            targets.Clear();
        }

        /// <summary>
        /// 部队移动回调：目标进入 / 离开光环范围；源部队自身移动时整体重算
        /// </summary>
        /// <param name="troop">移动的部队</param>
        /// <param name="dest">移动后的格子</param>
        /// <param name="last">移动前的格子</param>
        void OnTroopEnterCell(Troop troop, Cell dest, Cell last)
        {
            if (troop == null)
                return;

            Cell center = CenterCell;
            if (center == null)
                return;

            // 源部队自己移动：整个光环圈跟着平移，全部重新判定
            if (IsAuraOwner(troop))
            {
                RebuildAll(center);
                return;
            }

            bool inNow = dest != null && dest.Distance(center) <= bound;
            bool inBefore = last != null && last.Distance(center) <= bound;

            if (!inBefore && inNow)
                AddTarget(troop);
            else if (inBefore && !inNow)
                RemoveTarget(troop);
        }

        /// <summary>
        /// 该部队是否是光环的源（部队光环才有意义）
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>是否是光环源</returns>
        protected virtual bool IsAuraOwner(Troop troop)
        {
            return false;
        }

        /// <summary>当前正在补挂目标的光环实例（静态回调用；事件顺序执行，不可重入）</summary>
        static AuraActionBase spiralAddOwner;

        /// <summary>补挂目标的格子回调（静态复用，避免部队每移动一格都分配闭包）</summary>
        static readonly System.Action<Cell> spiralAddTargetAction = SpiralAddTarget;

        static void SpiralAddTarget(Cell cell)
        {
            if (cell != null && cell.troop != null)
                spiralAddOwner?.AddTarget(cell.troop);
        }

        /// <summary>
        /// 源部队移动后重算：先剔除已经不在范围内的，再扫描新位置覆盖到的部队
        /// </summary>
        /// <param name="center">光环中心格</param>
        void RebuildAll(Cell center)
        {
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                Troop troop = targets[i];
                if (troop == null || !troop.IsAlive || troop.cell == null || troop.cell.Distance(center) > bound)
                    RemoveTarget(troop);
            }

            // 静态回调：源部队每走一格都会走这里，原来每次 new 一个闭包
            spiralAddOwner = this;
            center.Spiral(bound, spiralAddTargetAction);
            spiralAddOwner = null;
        }

        /// <summary>
        /// 目标进入范围
        /// </summary>
        /// <param name="troop">部队对象</param>
        void AddTarget(Troop troop)
        {
            if (troop == null || targets.Contains(troop))
                return;
            if (!InScope(troop))
                return;

            targets.Add(troop);
            AuraManager.Add(troop, this);
        }

        /// <summary>
        /// 目标离开范围
        /// </summary>
        /// <param name="troop">部队对象</param>
        void RemoveTarget(Troop troop)
        {
            if (troop == null)
                return;
            if (!targets.Remove(troop))
                return;

            AuraManager.Remove(troop, this);
        }

        /// <summary>
        /// 源部队回合开始：结算回合型范围效果（灭火 / 放火 / 伤害 / BUFF）。
        ///
        /// 只在源部队自己的回合触发一次，避免每支受影响部队各触发一次造成重复结算。
        /// </summary>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (!IsAuraOwner(troop) || !IsActive)
                return;

            ExecuteTurnAura();
        }

        /// <summary>
        /// 建筑光环：在建筑所属势力的回合开始时结算（每势力每回合一次）。
        /// 建筑没有“部队回合”，所以不能复用 OnTroopTurnStart。
        /// </summary>
        void OnForceTurnStart(Force force, Scenario scenario)
        {
            BuildingBase owner = OwnerBuilding;
            if (!IsActive || owner == null || owner.BelongForce != force)
                return;

            ExecuteTurnAura();
        }

        /// <summary>回合型效果统一入口（部队光环 / 建筑光环共用）</summary>
        void ExecuteTurnAura()
        {
            switch (auraType)
            {
                case AURA_FIRE_EXTINGUISH: ExtinguishFires(); break;
                case AURA_FIRE_IGNITE: IgniteFires(); break;
                case AURA_AREA_DAMAGE: DamageEnemiesInRange(); break;
                case AURA_AREA_BUFF: AddBuffToEnemiesInRange(); break;
            }
        }

        /// <summary>灭火：范围内随机扑灭 value 处火焰</summary>
        void ExtinguishFires()
        {
            Cell center = CenterCell;
            if (center == null || value <= 0)
                return;

            auraCellBuffer.Clear();
            center.Spiral(bound, (cell) =>
            {
                if (cell != null && cell.fire != null)
                    auraCellBuffer.Add(cell);
            });

            int count = System.Math.Min(value, auraCellBuffer.Count);
            for (int i = 0; i < count; i++)
            {
                int index = GameRandom.Range(0, auraCellBuffer.Count);
                Cell cell = auraCellBuffer[index];
                auraCellBuffer.RemoveAt(index);
                if (cell.fire != null)
                    cell.fire.Clear();
            }
            auraCellBuffer.Clear();
        }

        /// <summary>放火：范围内随机点燃 value 个可点火空地（排除水 / 内政地 / 已有部队或建筑）</summary>
        void IgniteFires()
        {
            Cell center = CenterCell;
            if (center == null || value <= 0)
                return;

            auraCellBuffer.Clear();
            center.Spiral(bound, (cell) =>
            {
                if (cell == null || cell.fire != null || cell.troop != null || cell.building != null)
                    return;
                if (cell.IsInterior)
                    return;
                if (cell.TerrainType != null && cell.TerrainType.isWater)
                    return;
                auraCellBuffer.Add(cell);
            });

            int count = System.Math.Min(value, auraCellBuffer.Count);
            Troop owner = OwnerTroop;
            for (int i = 0; i < count; i++)
            {
                int index = GameRandom.Range(0, auraCellBuffer.Count);
                Cell cell = auraCellBuffer[index];
                auraCellBuffer.RemoveAt(index);
                TroopTurnRangeEffect.IgniteCell(cell, owner, turns);
            }
            auraCellBuffer.Clear();
        }

        /// <summary>对范围内敌军造成固定伤害（value 点）</summary>
        void DamageEnemiesInRange()
        {
            if (value <= 0)
                return;

            // 部队光环归属到源部队；建筑光环归属到建筑（ChangeTroops 接受 SangoObject）
            SangoObject source = OwnerTroop != null ? (SangoObject)OwnerTroop : OwnerBuilding;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                Troop target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    RemoveTarget(target);
                    continue;
                }
                target.ChangeTroops(-value, source, 0);
            }
        }

        /// <summary>对范围内敌军按概率随机施加一个 BUFF</summary>
        void AddBuffToEnemiesInRange()
        {
            if (buffIds == null || buffIds.Length == 0)
                return;

            // 部队光环来源是部队；建筑光环来源是建筑（BuffInstance.Master 已是 SangoObject）
            SangoObject source = OwnerTroop != null ? (SangoObject)OwnerTroop : OwnerBuilding;
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                Troop target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    RemoveTarget(target);
                    continue;
                }
                if (!GameRandom.Chance(probability, 10000))
                    continue;

                int buffId = buffIds[GameRandom.Range(0, buffIds.Length)];
                target.AddBuff(buffId, turns, source);
            }
        }

        /// <summary>防止武将受伤：被光环保护的武将所在部队直接否决本次负伤</summary>
        void OnPersonCheckInjury(Person person, SangoObject source, OverrideData<bool> canInjure)
        {
            if (person == null || canInjure == null || !canInjure.Value)
                return;

            Troop troop = person.mBelongTroop;
            if (troop == null)
                return;

            if (AuraManager.GetValue(troop, AURA_PREVENT_INJURY) > 0)
                canInjure.Value = false;
        }

        /// <summary>防止战法位移：被光环保护的部队直接否决本次位移</summary>
        void OnTroopCheckSkillOffset(Troop troop, Troop caster, OverrideData<bool> canOffset)
        {
            if (troop == null || canOffset == null || !canOffset.Value)
                return;

            if (AuraManager.GetValue(troop, AURA_PREVENT_SKILL_OFFSET) > 0)
                canOffset.Value = false;
        }

        /// <summary>
        /// 防止 BUFF：被光环保护的部队直接否决本次状态施加。
        ///
        /// 过滤优先级：buffIds 精确匹配 → buffKinds 按类别匹配 → 两者都不配则免疫全部。
        /// ID 匹配在前，避免"只想免疫混乱却把同类全部免疫"。
        /// </summary>
        void OnTroopCheckBuff(Troop troop, int buffId, OverrideData<bool> canBuff)
        {
            if (troop == null || canBuff == null || !canBuff.Value)
                return;

            if (AuraManager.GetValue(troop, AURA_PREVENT_BUFF) <= 0)
                return;

            if (buffIds != null && buffIds.Length > 0)
            {
                bool match = false;
                for (int i = 0; i < buffIds.Length; i++)
                {
                    if (buffIds[i] == buffId)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                    return;
            }

            if (buffKinds != null && buffKinds.Length > 0)
            {
                Scenario scenario = Scenario.Cur;
                Buff buff = scenario != null ? scenario.GetObject<Buff>(buffId) : null;
                if (buff == null)
                    return;

                bool match = false;
                for (int i = 0; i < buffKinds.Length; i++)
                {
                    if (buffKinds[i] == buff.kind)
                    {
                        match = true;
                        break;
                    }
                }
                if (!match)
                    return;
            }

            canBuff.Value = false;
        }

        /// <summary>
        /// 敌我判定：该部队是否属于本光环的作用对象
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <returns>是否需要作用在该部队上</returns>
        protected bool InScope(Troop troop)
        {
            Force owner = OwnerForce;
            if (troop == null || owner == null || !troop.IsAlive)
                return false;

            if (targetType == 0)
                return owner == troop.BelongForce;
            if (targetType == 1)
                return owner.IsEnemy(troop.BelongForce);
            return true;
        }
    }
}
