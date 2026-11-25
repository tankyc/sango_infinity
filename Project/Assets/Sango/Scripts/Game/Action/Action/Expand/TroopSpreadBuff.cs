using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 状态扩散：本队命中 / 施加某种异常状态后，把它扩散给周围的敌军。
    ///
    /// <para>
    /// 【对应设计条目】煽惑（计略命中后混乱扩散）、疑机（伪报向邻军扩散）、
    /// 佯装（状态转嫁）一类"一个人的异常变成一群人的异常"。
    /// </para>
    /// <para>
    /// 【时机】订阅 <c>OnTroopBuffApplied</c>：状态**已经**挂到目标身上之后才扩散，
    /// 因此不会影响本次施加本身的成功率 / 免疫判定。
    /// </para>
    /// <para>
    /// 【防递归】扩散出去的 AddBuff 会再次触发同一事件。这里用
    /// <c>spreadDepth</c> 深度标记截断：只有深度 0（原始施加）才扩散，
    /// 否则相邻两支敌军会来回弹射到全图。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>bound</term><description>扩散半径（格），默认 1</description></item>
    /// <item><term>probability</term><description>每个目标命中的概率（百分点），默认 100</description></item>
    /// <item><term>turns</term><description>扩散后的持续回合（0 = 沿用原状态的剩余回合）</description></item>
    /// <item><term>buffIds</term><description>仅对这些状态 Id 生效（缺省 = 任意状态都扩散）</description></item>
    /// <item><term>maxTargets</term><description>最多扩散给几个目标（0 = 不限）</description></item>
    /// <item><term>onlyEnemy</term><description>只扩散给敌军（默认 true）</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class TroopSpreadBuff : TroopActionBase
    {
        /// <summary>
        /// 扩散深度。0 = 原始施加，>0 = 由扩散产生。
        ///
        /// 【为什么是静态】递归是跨 Action 实例的（A 队扩散给 B 队，B 队身上的实例又会响应
        /// B 队的事件），只能用全局深度标记；战斗结算是单线程的，静态安全。
        /// </summary>
        static int spreadDepth;

        /// <summary>扩散半径</summary>
        int bound = 1;
        /// <summary>每个目标的命中概率（百分点）</summary>
        int probability = 100;
        /// <summary>扩散后的持续回合（0 = 沿用）</summary>
        int turns;
        /// <summary>只对这些状态 Id 生效（null / 空 = 全部）</summary>
        int[] buffIds;
        /// <summary>最多扩散目标数（0 = 不限）</summary>
        int maxTargets;
        /// <summary>只扩散给敌军</summary>
        bool onlyEnemy = true;

        /// <summary>收集到的扩散目标（复用，避免每次命中都分配）</summary>
        readonly List<Troop> targetBuffer = new List<Troop>(8);

        /// <summary>初始化并订阅状态生效事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int b = p.Value<int>("bound");
            bound = b > 0 ? b : 1;

            int prob = p.Value<int>("probability");
            probability = prob > 0 ? prob : 100;

            turns = p.Value<int>("turns");
            maxTargets = p.Value<int>("maxTargets");
            onlyEnemy = p.Value<bool?>("onlyEnemy") ?? true;

            JArray ids = p.Value<JArray>("buffIds");
            if (ids != null && ids.Count > 0)
            {
                buffIds = new int[ids.Count];
                for (int i = 0; i < ids.Count; i++)
                    buffIds[i] = ids[i].Value<int>();
            }

            GameEvent.OnTroopBuffApplied -= OnTroopBuffApplied;
            GameEvent.OnTroopBuffApplied += OnTroopBuffApplied;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnTroopBuffApplied -= OnTroopBuffApplied;
        }

        /// <summary>
        /// 状态生效后的扩散结算。
        /// </summary>
        /// <param name="target">刚获得状态的部队</param>
        /// <param name="buffId">状态 Id</param>
        /// <param name="src">施加者</param>
        /// <param name="turnCount">施加时的回合数</param>
        void OnTroopBuffApplied(Troop target, int buffId, SangoObject src, int turnCount)
        {
            // ① 只处理"本方施加的"状态
            if (Troop == null || src == null || !ReferenceEquals(src, Troop))
                return;
            // ② 扩散产生的状态不再扩散（防递归）
            if (spreadDepth > 0)
                return;
            // ③ 状态白名单
            if (buffIds != null && buffIds.Length > 0 && System.Array.IndexOf(buffIds, buffId) < 0)
                return;
            // ④ onlyEnemy：只扩散"施加到敌军身上"的异常状态。
            //    自己给自己上的增益（target == 自己）与给友军上的增益都不扩散 ——
            //    否则一个群体增益会被扩散成两次、甚至反向污染。
            if (onlyEnemy && (target == null || !target.IsEnemy(Troop)))
                return;

            CollectTargets(target);
            if (targetBuffer.Count == 0)
                return;

            int spreadTurns = turns > 0 ? turns : turnCount;
            int count = 0;

            spreadDepth++;
            try
            {
                for (int i = 0; i < targetBuffer.Count; i++)
                {
                    if (maxTargets > 0 && count >= maxTargets)
                        break;

                    Troop other = targetBuffer[i];
                    if (other == null || other == Troop || !other.IsAlive)
                        continue;
                    if (probability < 100 && !GameRandom.Chance(probability))
                        continue;

                    other.AddBuff(buffId, spreadTurns, Troop);
                    count++;
                }
            }
            finally
            {
                spreadDepth--;
            }

            if (count > 0)
                Sango.Log.Info($"@特技@[{Troop.Name}]的状态({buffId})扩散到 {count} 支敌军");
        }

        /// <summary>收集圆心周围的候选部队</summary>
        /// <param name="center">圆心部队</param>
        void CollectTargets(Troop center)
        {
            targetBuffer.Clear();
            if (center == null || center.cell == null)
                return;

            collectTroop = center;
            collectSelf = this;
            // 以**被施加状态的那支部队**为圆心，而不是持技部队 ——
            // "混乱扩散"是围着已经混乱的人传，不是围着施法者传
            center.cell.Spiral(bound, collectAction);
            collectTroop = null;
            collectSelf = null;
        }

        /// <summary>Spiral 回调用的中转字段（避免每次扩散都分配闭包）</summary>
        static Troop collectTroop;
        /// <summary>Spiral 回调用的中转字段</summary>
        static TroopSpreadBuff collectSelf;
        /// <summary>Spiral 回调（静态缓存，零分配）</summary>
        static readonly System.Action<Cell> collectAction = CollectCell;

        /// <summary>
        /// Spiral 回调：把范围内的敌方部队收进缓冲。
        /// </summary>
        /// <param name="cell">当前格</param>
        static void CollectCell(Cell cell)
        {
            if (cell == null || cell.troop == null || collectSelf == null)
                return;

            Troop other = cell.troop;
            // 圆心那支部队已经有这个状态了，跳过
            if (other == collectTroop || !other.IsAlive)
                return;

            // 【敌我判定基准是"持技部队"】不能用圆心判断：
            // 圆心是已经中状态的敌军，而"另一支敌军"和它并不是敌人（同属敌方阵营），
            // 拿圆心做基准会把所有扩散目标全部过滤掉。
            Troop self = collectSelf.Troop;
            if (collectSelf.onlyEnemy && (self == null || !other.IsEnemy(self)))
                return;

            collectSelf.targetBuffer.Add(other);
        }
    }
}
