using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 护卫（承担型）：半径内友军武将的负伤 / 战死由本部队主将承担。
    ///
    /// <para>
    /// 【与 TroopPreventPersonInjury 的区别】那个是"本部队武将不会负伤"（无条件免疫，没有代价）；
    /// 本类是"友军受伤改由我承担" —— 友军的伤病被否决，同时本部队主将伤病等级加重。
    /// 对应设计稿的「护卫：同队及周围队受伤，战死改为自己承担（自己承担战死改为受伤）」。
    /// </para>
    /// <para>
    /// 【承担不成就不否决】先尝试让主将受伤（<see cref="PersonInjury.Injure"/> 返回 true 才成立），
    /// 成功了才把友军的本次负伤 / 战死拦下。这样当主将已到伤病上限、或主将持「强运」免伤时，
    /// 友军仍按正常流程受伤 / 战死，不会退化成"无条件免伤"。
    /// </para>
    /// <para>
    /// 【不会递归】承担方是本部队主将，而本类明确排除"本部队编制内的武将"
    /// （<see cref="Troop.HasPerson"/>），所以 <see cref="PersonInjury.Injure"/> 内部
    /// 再次派发 OnPersonCheckInjury 时会被自己的第一条判断挡掉，最多一层。
    /// </para>
    /// </summary>
    public class TroopGuardPerson : TroopTroopActionBase
    {
        /// <summary>护卫半径（格）；0 表示只护卫同队（此时等同于无条件免疫，一般不用）</summary>
        int bound;

        /// <summary>承担时主将加重的伤病等级（1 轻伤 / 2 中伤 / 3 重伤）</summary>
        int level;

        /// <summary>1 = 战死也改为承担（默认）/ 0 = 只承担负伤</summary>
        int preventDeath;

        /// <summary>
        /// 初始化。
        /// <para>
        /// JSON 参数：bound（缺省 1）、level（缺省 1）、preventDeath（缺省 1）、condition。
        /// </para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Troop == null)
            {
                Sango.Log.Error("TroopGuardPerson 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }

            bound = p.Value<int?>("bound") ?? 1;
            level = p.Value<int?>("level") ?? 1;
            preventDeath = p.Value<int?>("preventDeath") ?? 1;
            if (level <= 0)
                level = 1;

            GameEvent.OnPersonCheckInjury += OnPersonCheckInjury;
            GameEvent.OnPersonCheckBattleDeath += OnPersonCheckBattleDeath;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnPersonCheckInjury -= OnPersonCheckInjury;
            GameEvent.OnPersonCheckBattleDeath -= OnPersonCheckBattleDeath;
        }

        /// <summary>
        /// 该武将是否属于"可以被护卫的友军部队"（不含本部队，避免自己吞自己的伤）。
        /// </summary>
        /// <param name="person">待判定的武将</param>
        /// <returns>属于友军返回 true</returns>
        bool IsGuardedOther(Person person)
        {
            if (Troop == null || person == null)
                return false;

            // 本部队编制内的武将不承担（否则会无限自吞，且语义上本来就该自己承受）
            if (Troop.HasPerson(person))
                return false;

            if (bound <= 0)
                return false;
            Cell ownerCell = Troop.cell;
            if (ownerCell == null)
                return false;

            bool hit = false;
            ownerCell.Spiral(bound, c =>
            {
                Troop t = c.troop;
                if (t == null || t == Troop)
                    return;
                if (!t.IsSameForce(Troop))
                    return;
                if (t.HasPerson(person))
                    hit = true;
            });
            return hit;
        }

        /// <summary>
        /// 尝试由主将承担一份伤病。
        /// </summary>
        /// <param name="source">伤害来源</param>
        /// <returns>真的承担成功返回 true</returns>
        bool TakeInjury(SangoObject source)
        {
            Person guard = Troop != null ? Troop.Leader : null;
            if (guard == null || !guard.IsAlive)
                return false;
            return PersonInjury.Injure(guard, level, source);
        }

        /// <summary>
        /// 友军武将即将负伤时，改由主将承担。
        /// </summary>
        /// <param name="person">待负伤的武将</param>
        /// <param name="source">伤害来源</param>
        /// <param name="canInjure">是否允许负伤，置 false 即否决</param>
        void OnPersonCheckInjury(Person person, SangoObject source, OverrideData<bool> canInjure)
        {
            if (canInjure == null || !canInjure.Value)
                return;
            if (!IsGuardedOther(person))
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(Troop)))
                return;

            if (TakeInjury(source))
                canInjure.Value = false;
        }

        /// <summary>
        /// 友军武将即将战死时，改由主将承担（战死 → 受伤）。
        /// </summary>
        /// <param name="person">待战死的武将</param>
        /// <param name="source">致死来源</param>
        /// <param name="canDie">是否允许战死，置 false 即免死</param>
        void OnPersonCheckBattleDeath(Person person, SangoObject source, OverrideData<bool> canDie)
        {
            if (preventDeath == 0)
                return;
            if (canDie == null || !canDie.Value)
                return;
            if (!IsGuardedOther(person))
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(Troop)))
                return;

            if (TakeInjury(source))
                canDie.Value = false;
        }
    }
}
