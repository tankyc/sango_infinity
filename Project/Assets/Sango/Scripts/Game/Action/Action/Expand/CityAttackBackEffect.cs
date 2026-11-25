using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 据点反击的追加效果：反击命中后，按概率给攻击方追加异常状态 / 负伤。
    ///
    /// <para>
    /// 【对应设计条目】拒攻（反击使敌军混乱）、蛮弩（反击使敌将负伤）、
    /// 弩炬（反击附带起火 —— 起火部分待补，见类末说明）。
    /// </para>
    /// <para>
    /// 【时机】订阅 <c>OnBuildingAttackBackSettled</c>：反击伤害**已经扣完**之后才追加，
    /// 因此不会出现"伤害还没落地就先上状态"的时序错位，也不会因为目标被打灭而漏结算。
    /// </para>
    /// <para>
    /// 【归属判定】特技挂在**城内武将**身上（City 装配），但事件参数里的建筑可能是城内设施，
    /// 所以用"建筑 → 所属城 == 本城"判定，城池自己反击与城内设施反击都能命中。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>probability</term><description>每次反击的触发概率（百分点），默认 100</description></item>
    /// <item><term>buffIds</term><description>追加的状态 Id 列表（随机取一个）</description></item>
    /// <item><term>buffTurns</term><description>状态持续回合，默认 1</description></item>
    /// <item><term>buffProbability</term><description>状态部分的独立概率（百分点），默认 100</description></item>
    /// <item><term>injure</term><description>使攻击方主将负伤的概率（百分点），0 = 不启用</description></item>
    /// <item><term>injuryLevel</term><description>伤病等级，默认 1</description></item>
    /// <item><term>troopsPercent</term><description>额外兵力削减（按当前兵力百分比）</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class CityAttackBackEffect : CityActionBase
    {
        /// <summary>整体触发概率（百分点）</summary>
        int probability = 100;
        /// <summary>追加的状态 Id 列表</summary>
        int[] buffIds;
        /// <summary>状态持续回合</summary>
        int buffTurns = 1;
        /// <summary>状态部分的独立概率（百分点）</summary>
        int buffProbability = 100;
        /// <summary>使攻击方主将负伤的概率（百分点）</summary>
        int injure;
        /// <summary>伤病等级</summary>
        int injuryLevel = 1;
        /// <summary>额外兵力削减百分比</summary>
        int troopsPercent;
        /// <summary>使攻击方气力增减（拒攻 = -12）</summary>
        int morale;
        /// <summary>点燃攻击方所在格的概率（百分点，0 = 不点火）—— 弩炬</summary>
        int fire;
        /// <summary>火焰持续回合</summary>
        int fireTurns = 1;
        /// <summary>同一次反击只结算一次（避免同城多人持有同一特技时效果叠加）</summary>
        bool oncePerCounter = true;

        /// <summary>
        /// 上一次已处理的反击标识（静态，跨实例去重）。
        ///
        /// 【为什么需要】事件的订阅者是"每城每个持有者各一个 Action 实例"，
        /// 同城三人持有同一个特技时，一次反击会被三个实例各处理一遍 ——
        /// 表现为"配 100% 实际是 300% 概率"。这里用
        /// 「攻击部队 + 城 + 伤害」三元组做同批去重。
        /// 【已知近似】同一回合内同一部队被同一城打出**完全相同的伤害**两次时，
        /// 第二次会被吞掉。相比"人数叠加"这个偏差更小，且可用
        /// <c>oncePerCounter = false</c> 关掉。
        /// </summary>
        static Troop lastCounterTroop;
        static City lastCounterCity;
        static int lastCounterDamage;

        /// <summary>初始化并订阅据点反击结算事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int prob = p.Value<int>("probability");
            probability = prob > 0 ? prob : 100;

            int turns = p.Value<int>("buffTurns");
            buffTurns = turns > 0 ? turns : 1;

            int bprob = p.Value<int>("buffProbability");
            buffProbability = bprob > 0 ? bprob : 100;

            injure = p.Value<int>("injure");
            int level = p.Value<int>("injuryLevel");
            injuryLevel = level > 0 ? level : 1;
            troopsPercent = p.Value<int>("troopsPercent");
            morale = p.Value<int>("morale");
            fire = p.Value<int>("fire");
            int ft = p.Value<int>("fireTurns");
            fireTurns = ft > 0 ? ft : 1;
            oncePerCounter = p.Value<bool?>("oncePerCounter") ?? true;

            JArray ids = p.Value<JArray>("buffIds");
            if (ids != null && ids.Count > 0)
            {
                buffIds = new int[ids.Count];
                for (int i = 0; i < ids.Count; i++)
                    buffIds[i] = ids[i].Value<int>();
            }

            GameEvent.OnBuildingAttackBackSettled -= OnSettled;
            GameEvent.OnBuildingAttackBackSettled += OnSettled;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnBuildingAttackBackSettled -= OnSettled;
            // 静态去重字段持有的是"上一场战斗"的部队 / 城池引用，
            // 不清会延长它们的生命周期（换剧本后这些对象已经该回收了）
            lastCounterTroop = null;
            lastCounterCity = null;
        }

        /// <summary>
        /// 反击结算后的追加处理。
        /// </summary>
        /// <param name="atkTroop">被反击的攻击部队</param>
        /// <param name="building">发起反击的建筑 / 城池</param>
        /// <param name="damage">本次反击伤害</param>
        void OnSettled(Troop atkTroop, BuildingBase building, int damage)
        {
            if (City == null || atkTroop == null || building == null || damage <= 0)
                return;
            if (!IsOwnBuilding(building, City))
                return;

            // 同一次反击只让**第一个**订阅者结算，避免同城多人持有导致概率叠加
            if (oncePerCounter)
            {
                if (ReferenceEquals(lastCounterTroop, atkTroop)
                    && ReferenceEquals(lastCounterCity, City)
                    && lastCounterDamage == damage)
                    return;
                lastCounterTroop = atkTroop;
                lastCounterCity = City;
                lastCounterDamage = damage;
            }

            if (probability < 100 && !GameRandom.Chance(probability))
                return;

            // ① 追加异常状态（拒攻：混乱）
            if (buffIds != null && buffIds.Length > 0
                && (buffProbability >= 100 || GameRandom.Chance(buffProbability)))
            {
                int buffId = buffIds[GameRandom.Range(0, buffIds.Length)];
                atkTroop.AddBuff(buffId, buffTurns, City);
                Sango.Log.Info($"@特技@[{City.Name}]的反击使[{atkTroop.Name}]获得状态({buffId})");
            }

            // ② 气力削减（拒攻：反击使攻击方气力额外 -12）
            if (morale != 0)
                atkTroop.ChangeMorale(morale);

            // ②.5 点燃攻击方所在格（弩炬）：复用 TroopTurnRangeEffect.IgniteCell，
            //      terrain.fireRate <= 0（水面 / 不可燃地形）时它自己会拒绝，这里不用再判
            if (fire > 0 && atkTroop.cell != null && GameRandom.Chance(fire))
            {
                TroopTurnRangeEffect.IgniteCell(atkTroop.cell, null, fireTurns);
                Sango.Log.Info($"@特技@[{City.Name}]的反击点燃了[{atkTroop.Name}]所在格");
            }

            // ③ 额外兵力削减（按当前兵力百分比）
            if (troopsPercent > 0)
            {
                int cut = atkTroop.GetTroopsNum() * troopsPercent / 100;
                if (cut > 0)
                    atkTroop.ChangeTroops(-cut, City, 0);
            }

            // ③ 使攻击方主将负伤（蛮弩）
            if (injure > 0 && atkTroop.Leader != null
                && atkTroop.Leader.injury < Person.InjuryMaxLevel
                && GameRandom.Chance(injure))
            {
                Person hurt = atkTroop.Leader;
                if (PersonInjury.Injure(hurt, injuryLevel, City))
                {
                    // 【不派发 OnPersonInjury】该事件的第二个参数是 Troop（负伤他的部队），
                    // 而这里的施加者是据点，没有对应部队可传；传攻击部队自己会把
                    // "谁伤的"记反。负伤本身已由 PersonInjury.Injure 落地，
                    // UI / AI 读取的是 person.injury，不依赖这条通知。
                    Sango.Log.Info($"@特技@[{City.Name}]的反击使[{atkTroop.Name}]主将[{hurt.Name}]负伤，等级{hurt.injury}");
                }
            }
        }

        /// <summary>
        /// 判断发起反击的建筑是否属于本城。
        ///
        /// 【为什么要判定】城内设施（箭塔 / 陷阱）的反击同样要走本效果，
        /// 但不能让 A 城的特技去影响 B 城的设施反击。
        /// 城池自己反击时 <paramref name="building"/> 就是 City 本身，直接相等即可。
        /// </summary>
        /// <param name="building">发起反击的建筑</param>
        /// <param name="city">本城</param>
        static bool IsOwnBuilding(BuildingBase building, City city)
        {
            if (ReferenceEquals(building, city))
                return true;
            if (building is Building b)
                return ReferenceEquals(b.BelongCity, city);
            return false;
        }
    }
}
