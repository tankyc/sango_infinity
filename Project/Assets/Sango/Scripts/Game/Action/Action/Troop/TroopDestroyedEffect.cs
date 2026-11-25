using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队溃灭触发：持技部队**被击破的那一刻**，在溃灭位置结算一组"死后余波"效果。
    ///
    /// 【时机】订阅 <c>GameEvent.OnTroopDestroyed</c>，且只处理"被击破的就是持技部队"的那一次。
    /// 该事件由 <c>Troop.OnDestroy</c> 在 <c>Clear()</c> 之前派发，所以 <c>troop.cell</c> 仍然有效——
    /// 这是"玉石俱焚（原地起火）""独力拒敌（相邻敌军止行）"能够实现的前提。
    ///
    /// 【参数】
    /// fireRadius        起火半径（格，0 = 不点火；1 = 自身格 + 相邻格）
    /// fireTurns         火焰持续回合数
    /// fireProbability   每格起火概率（万分比，0 视为必定）
    /// buffId            给范围内敌军附加的状态 Id（"止行"用 Buffs.json Id 5 足止）
    /// buffTurns         状态持续回合数
    /// buffBound         状态作用半径（格）
    /// buffProbability   状态附加概率（万分比，0 视为必定）
    /// </summary>
    public class TroopDestroyedEffect : TroopActionBase
    {
        /// <summary>起火半径</summary>
        int fireRadius;
        /// <summary>火焰持续回合数</summary>
        int fireTurns;
        /// <summary>每格起火概率（万分比）</summary>
        int fireProbability;
        /// <summary>敌军状态 Id</summary>
        int buffId;
        /// <summary>状态持续回合数</summary>
        int buffTurns;
        /// <summary>状态作用半径</summary>
        int buffBound;
        /// <summary>状态附加概率（万分比）</summary>
        int buffProbability;

        /// <summary>
        /// 性别过滤：0 不限（默认）/ 1 只对男性武将部队 / 2 只对女性武将部队。
        /// 作用于 ② 状态段与 ③ 士气段，对应"洛神：周围所有**异性**部队士气 -15"。
        /// </summary>
        int sexFilter;

        /// <summary>士气修正半径（格），0 表示不做士气修正</summary>
        int moraleBound;

        /// <summary>士气修正值（负数 = 降低）</summary>
        int moraleValue;

        /// <summary>士气修正对象：0 敌军（默认）/ 1 友军 / 2 全部</summary>
        int moraleTargetType;

        /// <summary>
        /// 初始化参数并订阅部队击破事件。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 部队，[1] 触发装配的武将）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            fireRadius = p.Value<int>("fireRadius");
            fireTurns = p.Value<int>("fireTurns");
            fireProbability = p.Value<int>("fireProbability");
            buffId = p.Value<int>("buffId");
            buffTurns = p.Value<int>("buffTurns");
            buffBound = p.Value<int>("buffBound");
            buffProbability = p.Value<int>("buffProbability");
            sexFilter = p.Value<int>("sexFilter");
            moraleBound = p.Value<int>("moraleBound");
            moraleValue = p.Value<int>("moraleValue");
            moraleTargetType = p.Value<int>("moraleTargetType");

            // 装配宿主既不是部队也不是势力时不订阅事件（避免"任何部队溃灭都在全场起火"）
            if (Troop == null && Force == null)
            {
                Sango.Log.Error($"溃灭触发效果的装配宿主既不是部队也不是势力，特技不会生效：{p}");
                return;
            }

            GameEvent.OnTroopDestroyed += OnTroopDestroyed;
        }

        /// <summary>
        /// 退订事件。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopDestroyed -= OnTroopDestroyed;
        }

        /// <summary>
        /// 事件驱动效果，不需要外部主动触发。
        /// </summary>
        /// <param name="trigger">触发参数（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 部队被击破回调：只处理持技部队自己，然后按配置结算余波。
        /// </summary>
        /// <param name="destroyed">被击破的部队</param>
        /// <param name="atk">击破来源（未使用）</param>
        /// <param name="atkBack">反击强度因子（本类未使用，0 = 非反击）</param>
        /// <param name="scenario">当前剧本</param>
        void OnTroopDestroyed(Troop destroyed, SangoObject atk, AttackBackInfo atkBack, Scenario scenario)
        {
            if (destroyed == null)
                return;

            // 只处理"持技部队自己溃灭"
            if (Troop != null && destroyed != Troop)
                return;
            if (Troop == null && Force != null && destroyed.BelongForce != Force)
                return;

            Cell cell = destroyed.cell;
            if (cell == null)
                return;

            // ① 原地起火（玉石俱焚）
            if (fireRadius > 0 && fireTurns > 0)
            {
                cell.Spiral(fireRadius, c =>
                {
                    if (c == null)
                        return;
                    if (fireProbability > 0 && !GameRandom.Chance(fireProbability, 10000))
                        return;
                    TroopTurnRangeEffect.IgniteCell(c, destroyed, fireTurns);
                });
            }

            // ② 范围内敌军附加状态（独力拒敌 → 止行）
            if (buffId > 0 && buffBound > 0)
            {
                cell.Spiral(buffBound, c =>
                {
                    if (c == null)
                        return;
                    Troop target = c.troop;
                    if (target == null || !target.IsAlive || target == destroyed)
                        return;
                    if (!destroyed.IsEnemy(target))
                        return;
                    if (sexFilter != 0 && !MatchSex(target, sexFilter))
                        return;
                    if (buffProbability > 0 && !GameRandom.Chance(buffProbability, 10000))
                        return;
                    target.AddBuff(buffId, buffTurns <= 0 ? 1 : buffTurns, null);
                });
            }

            // ③ 范围内部队士气修正（洛神：自部队溃灭后周围异性部队士气 -15）
            // 用 ChangeMorale(value, false)：溃灭余波不该逐支部队弹提示，避免战场上刷屏。
            if (moraleBound > 0 && moraleValue != 0)
            {
                cell.Spiral(moraleBound, c =>
                {
                    if (c == null)
                        return;
                    Troop target = c.troop;
                    if (target == null || !target.IsAlive || target == destroyed)
                        return;

                    bool sameForce = destroyed.IsSameForce(target);
                    if (moraleTargetType == 0 && sameForce) return;
                    if (moraleTargetType == 1 && !sameForce) return;

                    if (sexFilter != 0 && !MatchSex(target, sexFilter))
                        return;

                    target.ChangeMorale(moraleValue, false);
                });
            }
        }

        /// <summary>
        /// 按主将性别匹配过滤条件。
        /// </summary>
        /// <param name="troop">待判定的部队</param>
        /// <param name="filter">1 只男 / 2 只女</param>
        /// <returns>命中返回 true；主将缺失时返回 false</returns>
        static bool MatchSex(Troop troop, int filter)
        {
            Person leader = troop != null ? troop.Leader : null;
            if (leader == null)
                return false;
            // Person.sex：0 男 / 1 女（Person.cs:166）
            return filter == 1 ? leader.sex == 0 : leader.sex == 1;
        }
    }
}
