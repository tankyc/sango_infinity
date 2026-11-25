using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 蓄力：本回合未移动，则下回合的**首次攻击**伤害提升（蓄力：每回合未移动，下回合首击 +10%）。
    ///
    /// <para>
    /// 【状态机】用 <see cref="Troop.chargeState"/> 三态推进，避免在属性重算里判断"有没有移动过"
    /// （属性重算与移动无关，放那里会反复覆盖）：
    /// · 行动结束（<c>OnTroopActionOver</c>）时 <c>moveDistanceThisTurn == 0</c> → 置 1（待激活）；
    /// · 下个回合开始（<c>OnTroopTurnStart</c>）时 1 → 2（已激活）；
    /// · 打出一次伤害后 2 → 0（只吃"首次"这一发）。
    /// </para>
    /// <para>
    /// 【为什么用 OnTroopActionOver 而不是 OnForceTurnStart】
    /// <c>OnForceTurnStart</c> 里会先把 <c>moveDistanceThisTurn</c> 清零，
    /// 在它之后无法判断"上回合到底有没有移动"，所以必须挂在"行动结束、计数尚未清零"的时刻。
    /// </para>
    /// </summary>
    public class TroopChargeAttack : TroopTroopActionBase
    {
        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：value（首击增伤百分点）、kinds、checkLand、condition。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            if (Troop == null)
            {
                Sango.Log.Error("TroopChargeAttack 只能配在部队（特技）上，配在势力上不会生效");
                return;
            }

            GameEvent.OnTroopActionOver += OnTroopActionOver;
            GameEvent.OnTroopTurnStart += OnTroopTurnStart;
            GameEvent.OnTroopChangeTroops += OnTroopChangeTroops;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopActionOver -= OnTroopActionOver;
            GameEvent.OnTroopTurnStart -= OnTroopTurnStart;
            GameEvent.OnTroopChangeTroops -= OnTroopChangeTroops;
        }

        /// <summary>本部队是否符合兵种 / 地形限制</summary>
        bool MatchTroop(Troop troop)
        {
            if (troop == null)
                return false;
            if (checkLand == 1 && troop.IsInWater) return false;
            if (checkLand == 2 && !troop.IsInWater) return false;
            if (kinds == null)
                return true;
            bool landOk = troop.LandTroopType != null && kinds.Contains(troop.LandTroopType.kind);
            bool waterOk = troop.WaterTroopType != null && kinds.Contains(troop.WaterTroopType.kind);
            return landOk || waterOk;
        }

        /// <summary>
        /// 行动结束时判定"本回合是否移动过"。
        /// </summary>
        /// <param name="troop">结束行动的部队</param>
        void OnTroopActionOver(Troop troop)
        {
            if (Troop == null || troop != Troop)
                return;
            if (!MatchTroop(troop))
                return;
            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            troop.chargeState = troop.moveDistanceThisTurn == 0 ? 1 : 0;
        }

        /// <summary>
        /// 回合开始时把"待激活"转为"已激活"。
        /// </summary>
        /// <param name="troop">回合开始的部队</param>
        /// <param name="scenario">场景对象</param>
        void OnTroopTurnStart(Troop troop, Scenario scenario)
        {
            if (Troop == null || troop != Troop)
                return;

            // 上回合标记为"待激活"才转正；其余情况（含中途换特技）一律清空，避免残留
            troop.chargeState = troop.chargeState == 1 ? 2 : 0;
        }

        /// <summary>
        /// 首次攻击结算时增伤，并消耗掉激活状态。
        /// </summary>
        /// <param name="defencer">受伤部队</param>
        /// <param name="atk">攻击来源</param>
        /// <param name="atkBack">反击强度因子，0 = 非反击（见 TroopActionBase.IsAttackBack）</param>
        /// <param name="overrideData">兵力变化量</param>
        void OnTroopChangeTroops(Troop defencer, SangoObject atk, AttackBackInfo atkBack, OverrideData<int> overrideData)
        {
            if (Troop == null || overrideData == null)
                return;
            if (IsChargeNotReady()) return;

            // 只修正自己打出去的掉兵；反击不消耗蓄力（蓄力描述的是"主动首击"）
            if (overrideData.Value <= 0)
                return;

            // 反击判定用"是否为 0"：atkBack 是强度因子（部队 28~90 等），不是 0/1 布尔，
            // 历史写法 `atkBack == 1` 永不成立。下一行 `atkTroop != Troop` 在反击场景本来也会拦截，
            // 这里保留显式判断，避免以后调整判断顺序时把"反击不消耗蓄力"的约束弄丢。
            if (IsAttackBack(atkBack))
                return;

            Troop atkTroop = atk?.AsTroop();
            if (atkTroop != Troop)
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(Troop)))
                return;

            overrideData.Value = overrideData.Value * (100 + value) / 100;
            Troop.chargeState = 0;
        }

        /// <summary>
        /// 蓄力是否处于"已激活但未消耗"状态。
        /// 单独抽成方法是为了让 <c>OnTroopChangeTroops</c> 的第一道判断保持一行、可读。
        /// </summary>
        /// <returns>未激活返回 true</returns>
        bool IsChargeNotReady()
        {
            return Troop == null || Troop.chargeState != 2;
        }
    }
}
