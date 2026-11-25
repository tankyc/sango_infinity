using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 刺杀 / 斩杀 / 濒危：按概率直接让目标主将负危或战死。
    ///
    /// <para>
    /// 【对应设计条目】
    /// · 剑侠（回合开始，相邻敌军中武力最低的主将，成功率随武力差 1%~20%）；
    /// · 伏锋（回合开始，相邻敌军武力低于阈值 → 10% 濒危 / 2% 斩杀）；
    /// · 斩将（回合开始，相邻受控敌军主将 5% 战死）。
    /// </para>
    /// <para>
    /// 【死亡为什么必须走 <see cref="Person.DeadInBattle"/>】
    /// 它会在真正处理死亡之前派发 <c>OnPersonCheckBattleDeath</c>，
    /// 护卫 / 名马 / 剧情保护这些"免死"才能生效；直接改 state 会绕过全部保护，
    /// 还会漏掉随从殉死、装备转移等收尾。
    /// </para>
    /// <para>
    /// 【触发点】<c>trigger</c>：
    /// 0 单挑结束（只认我方胜）、1 命中判定成功、2 部队被击破、3 本部队回合开始（相邻扫描）。
    /// 前三种走 <c>CombatContext</c>，第四种走 <c>OnTroopTurnStart</c>。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>trigger</term><description>0/1/2/3，见上，默认 2</description></item>
    /// <item><term>probability</term><description>基础触发概率（百分点），默认 100</description></item>
    /// <item><term>bound</term><description>trigger = 3 时的扫描半径，默认 1</description></item>
    /// <item><term>statType</term><description>比较的属性：0 统率 / 1 武力（默认 1）</description></item>
    /// <item><term>statBelow</term><description>只对"该属性低于此值"的目标生效（0 = 不限）</description></item>
    /// <item><term>statDiffBonus</term><description>每 1 点属性差追加的概率（百分点），用于剑侠 1%~20% 曲线</description></item>
    /// <item><term>maxProbability</term><description>概率上限（百分点），默认 100</description></item>
    /// <item><term>endanger</term><description>true = 只使其濒危（伤病拉满），false = 直接战死</description></item>
    /// <item><term>requireInjured</term><description>要求目标主将已负伤</description></item>
    /// <item><term>minInjury</term><description>要求目标伤病等级 ≥ 该值</description></item>
    /// <item><term>maxUses</term><description>本场最多触发次数（0 = 不限）</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class TroopAssassinate : TroopActionBase
    {
        /// <summary>触发点：0 单挑胜 / 1 命中 / 2 击破 / 3 部队回合开始</summary>
        int trigger = 2;
        /// <summary>基础触发概率（百分点）</summary>
        int probability = 100;
        /// <summary>回合开始的扫描半径</summary>
        int bound = 1;
        /// <summary>比较属性：0 统率 / 1 武力</summary>
        int statType = 1;
        /// <summary>只对属性低于该值的目标生效（0 = 不限）</summary>
        int statBelow;
        /// <summary>每 1 点属性差追加的概率（百分点）</summary>
        int statDiffBonus;
        /// <summary>概率上限</summary>
        int maxProbability = 100;
        /// <summary>true = 只使其濒危（不致死）</summary>
        bool endanger;
        /// <summary>要求目标主将已负伤</summary>
        bool requireInjured;
        /// <summary>要求的最低伤病等级</summary>
        int minInjury;
        /// <summary>
        /// 要求目标部队带有指定状态位（<see cref="TroopStateFlag"/> 的枚举值，0 = 不限）。
        /// 斩将的"处于控制状态"就配这里：配 1 = 混乱，配 3 = 混乱或眩晕。
        /// </summary>
        int requireStateFlag;
        /// <summary>本场最多触发次数（0 = 不限）</summary>
        int maxUses;

        /// <summary>本场已触发次数</summary>
        int usedCount;
        /// <summary>上一次处理过的目标（避免同一次结算里重复处理同一人）</summary>
        Person lastHandledPerson;
        /// <summary>相邻敌军缓冲（复用）</summary>
        readonly List<Troop> targetBuffer = new List<Troop>(8);
        /// <summary>Spiral 回调（缓存，避免每次回合开始都新建委托）</summary>
        System.Action<Cell> collectAction;

        /// <summary>初始化并按 trigger 订阅对应事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            trigger = p.Value<int>("trigger");
            int prob = p.Value<int>("probability");
            probability = prob > 0 ? prob : 100;

            int b = p.Value<int>("bound");
            bound = b > 0 ? b : 1;

            statType = p.Value<int>("statType");
            statBelow = p.Value<int>("statBelow");
            statDiffBonus = p.Value<int>("statDiffBonus");
            int maxP = p.Value<int>("maxProbability");
            maxProbability = maxP > 0 ? maxP : 100;

            endanger = p.Value<bool>("endanger");
            requireInjured = p.Value<bool>("requireInjured");
            minInjury = p.Value<int>("minInjury");
            requireStateFlag = p.Value<int>("requireStateFlag");
            maxUses = p.Value<int>("maxUses");
            usedCount = 0;
            lastHandledPerson = null;

            // 只订阅配置的那一个触发点，避免"配了击破却在命中时乱杀"
            switch (trigger)
            {
                case 0:
                    GameEvent.OnCombatDuelFinished -= OnDuelFinished;
                    GameEvent.OnCombatDuelFinished += OnDuelFinished;
                    break;
                case 1:
                    GameEvent.OnCombatHit -= OnCombatHit;
                    GameEvent.OnCombatHit += OnCombatHit;
                    break;
                case 3:
                    if (collectAction == null)
                        collectAction = CollectCell;
                    GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
                    GameEvent.OnTroopTurnStart += OnTroopTurnStart;
                    break;
                default:
                    GameEvent.OnCombatTroopDestroyed -= OnTroopDestroyed;
                    GameEvent.OnCombatTroopDestroyed += OnTroopDestroyed;
                    break;
            }
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnCombatDuelFinished -= OnDuelFinished;
            GameEvent.OnCombatHit -= OnCombatHit;
            GameEvent.OnCombatTroopDestroyed -= OnTroopDestroyed;
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            targetBuffer.Clear();
        }

        /// <summary>单挑结束（只认我方获胜）</summary>
        /// <param name="ctx">战斗上下文</param>
        void OnDuelFinished(CombatContext ctx)
        {
            if (!IsCaster(ctx) || ctx.duelResult != 1)
                return;
            // 每次事件都是新的一批：清掉上一次的去重标记，
            // 否则同一个敌将第二次被命中 / 击破时就再也不会被判定
            lastHandledPerson = null;
            TryAssassinate(ctx.targetPerson, 0, ctx.targetTroop);
        }

        /// <summary>命中判定成功</summary>
        /// <param name="ctx">战斗上下文</param>
        void OnCombatHit(CombatContext ctx)
        {
            if (!IsCaster(ctx))
                return;
            lastHandledPerson = null;
            TryAssassinate(ctx.targetPerson, 0, ctx.targetTroop);
        }

        /// <summary>部队被击破</summary>
        /// <param name="ctx">战斗上下文</param>
        void OnTroopDestroyed(CombatContext ctx)
        {
            if (!IsCaster(ctx))
                return;
            lastHandledPerson = null;
            TryAssassinate(ctx.targetPerson, 0, ctx.targetTroop);
        }

        /// <summary>
        /// 本部队回合开始：扫描相邻敌军，对符合条件的主将逐个尝试。
        ///
        /// 【只处理自己那一次】事件会对全场部队逐个派发，不判 <c>troop == Troop</c>
        /// 就会变成"一支持有队伍每回合杀全场"。
        /// </summary>
        /// <param name="troop">当前行动部队</param>
        /// <param name="scenario">剧本</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (Troop == null || troop != Troop || Troop.cell == null)
                return;

            // 每次回合开始都是新的一批目标：清掉上一回合的去重标记，
            // 否则同一个敌将在被扫描过一次后就永远不再是被刺杀对象
            lastHandledPerson = null;

            targetBuffer.Clear();
            Troop.cell.Spiral(bound, collectAction);
            if (targetBuffer.Count == 0)
                return;

            for (int i = 0; i < targetBuffer.Count; i++)
            {
                Troop other = targetBuffer[i];
                if (other == null || !other.IsAlive)
                    continue;

                Person victim = other.Leader;
                if (victim == null || !victim.IsAlive)
                    continue;

                // 属性门槛：剑侠"武力最低"由下面的排序选，伏锋/斩将用 statBelow
                int victimStat = GetStat(victim);
                if (statBelow > 0 && victimStat >= statBelow)
                    continue;

                // 武力差加成：概率 = 基础 + 差值 × 每点加成（夹到上限）
                int chance = probability;
                if (statDiffBonus > 0 && Troop.Leader != null)
                {
                    int diff = GetStat(Troop.Leader) - victimStat;
                    if (diff > 0)
                        chance += diff * statDiffBonus;
                }
                if (chance > maxProbability)
                    chance = maxProbability;
                if (chance <= 0)
                    continue;

                TryAssassinate(victim, chance, other);
            }
        }

        /// <summary>取指定属性的当前值</summary>
        /// <param name="person">武将</param>
        int GetStat(Person person)
        {
            return statType == 0 ? person.Command : person.Strength;
        }

        /// <summary>
        /// 是否由本部队发起（战斗上下文版）。
        ///
        /// 【必须判 caster】事件是全局派发的：不判 caster 就等于"任何一支部队打人，
        /// 持有斩杀特技的部队都会隔空杀人"。
        /// </summary>
        /// <param name="ctx">战斗上下文</param>
        bool IsCaster(CombatContext ctx)
        {
            return ctx != null && Troop != null && ReferenceEquals(ctx.caster, Troop);
        }

        /// <summary>
        /// 刺杀判定并执行。
        /// </summary>
        /// <param name="person">目标主将</param>
        /// <param name="chance">本次概率（百分点，&lt;= 0 表示用基础概率）</param>
        /// <param name="victimTroop">目标所在部队（可为 null，仅用于状态位筛选）</param>
        void TryAssassinate(Person person, int chance, Troop victimTroop)
        {
            if (Troop == null || person == null || !person.IsAlive)
                return;
            if (ReferenceEquals(lastHandledPerson, person))
                return;
            if (maxUses > 0 && usedCount >= maxUses)
                return;
            if (requireInjured && person.injury <= 0)
                return;
            if (minInjury > 0 && person.injury < minInjury)
                return;
            // 控制状态筛选（斩将）：位掩码命中任意一位即通过
            if (requireStateFlag != 0)
            {
                if (victimTroop == null)
                    return;
                // HasStateFlag 只接受单个枚举值，用 "掩码逐位试" 的方式支持多状态合并配置
                bool matched = false;
                int mask = requireStateFlag;
                for (int bit = 0; bit < 32; bit++)
                {
                    int flag = 1 << bit;
                    if ((mask & flag) == 0)
                        continue;
                    if (victimTroop.HasStateFlag((TroopStateFlag)flag))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                    return;
            }

            int finalChance = chance > 0 ? chance : probability;
            if (finalChance < 100 && !GameRandom.Chance(finalChance))
                return;

            usedCount++;
            lastHandledPerson = person;

            if (endanger)
            {
                // 濒危 = 伤病拉满但不死：走统一的负伤接口，保留"免伤"类特技的判定
                if (PersonInjury.Injure(person, Person.InjuryMaxLevel, Troop))
                    Sango.Log.Info($"@特技@[{Troop.Name}]使[{person.Name}]濒危（伤病 {person.injury}）");
                return;
            }

            Sango.Log.Info($"@特技@[{Troop.Name}]发动刺杀/斩杀，目标[{person.Name}]");
            // 走统一战死入口：护卫 / 名马等免死判定仍然生效
            person.DeadInBattle(Troop);
        }

        /// <summary>Spiral 回调：收集范围内的敌方部队</summary>
        /// <param name="cell">当前格</param>
        void CollectCell(Cell cell)
        {
            if (cell == null || cell.troop == null)
                return;

            Troop other = cell.troop;
            if (other == Troop || !other.IsAlive)
                return;
            if (Troop != null && !other.IsEnemy(Troop))
                return;

            targetBuffer.Add(other);
        }
    }
}
