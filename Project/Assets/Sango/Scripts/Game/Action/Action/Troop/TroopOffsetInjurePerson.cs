using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技 #52 猛者：以战法让敌方部队移动区格时，有几率让敌方一名武将负伤。
    ///
    /// 数据参数（Features.json 的 actionEntities，缺省值见字段初始化）：
    ///   value  —— 触发概率（百分点），例如 30 表示 30%
    ///   injury —— 每次触发加重的伤病等级，缺省 1（健康→轻伤）
    ///
    /// 接线方式：监听 <see cref="GameEvent.OnTroopSkillOffset"/> —— 该信号由
    /// SkillInstance.DoOffset 在"目标部队真的被推出区格"之后派发。
    /// 本动作装配在**施法方**部队上（Troop.InitActionList 按武将特技装配），
    /// 因此只处理"本部队施法、把敌军推走"的那一次位移，与三国志11 的猛者口径一致
    /// （特技持有者的部队用战法推动敌部队 → 敌将负伤）。
    /// 接入事件：OnTroopSkillOffset
    /// </summary>
    public class TroopOffsetInjurePerson : TroopActionBase
    {
        /// <summary>触发概率（百分点）</summary>
        int chance;

        /// <summary>每次触发加重的伤病等级（封顶 Person.InjuryMaxLevel）</summary>
        int injuryLevel = 1;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            chance = value;
            int injury = p.Value<int>("injury");
            injuryLevel = injury > 0 ? injury : 1;
            GameEvent.OnTroopSkillOffset += OnTroopSkillOffset;
        }

        public override void Clear()
        {
            GameEvent.OnTroopSkillOffset -= OnTroopSkillOffset;
        }

        /// <summary>
        /// 战法位移结算完成：施法部队把敌军推出区格时，按概率让敌方一名武将负伤
        /// </summary>
        /// <param name="actionTroop">施法部队</param>
        /// <param name="targetTroop">被位移的部队</param>
        /// <param name="offsetCells">实际位移的格数</param>
        void OnTroopSkillOffset(Troop actionTroop, Troop targetTroop, int offsetCells)
        {
            if (actionTroop == null || targetTroop == null) return;

            // 只处理本部队作为施法方的那次位移（动作是按部队装配的）
            if (Troop != null && Troop != actionTroop) return;
            if (Force != null && actionTroop.BelongForce != Force) return;

            // 必须真的推走了区格，且被推的是敌军：
            // 友军 / 同盟被推走不算"让敌方部队移动区格"，没有推动（被挡住、无处可去）也不算。
            if (offsetCells <= 0 || !targetTroop.IsAlive) return;
            if (!actionTroop.IsEnemy(targetTroop)) return;

            if (chance <= 0) return;
            if (!GameRandom.Chance(chance)) return;

            Person hurt = PickInjurablePerson(targetTroop);
            if (hurt == null) return;

            // 加重失败（被免伤 / 已到伤病上限）时不必发提示，避免"看起来受伤了其实没变"。
            // 把施法部队作为伤害来源传进去：免伤类特技（如 #53 护卫）在 PersonInjury 的询问事件里
            // 就能拿到"是谁造成的伤害"，本特技因此不需要知道护卫的存在。
            if (!PersonInjury.Injure(hurt, injuryLevel, actionTroop)) return;

            Sango.Log.Info($"@特技@[{actionTroop.BelongForce?.Name}]的[{actionTroop.Name}]以战法将[{targetTroop.BelongForce?.Name}]的[{targetTroop.Name}]推出{offsetCells}格, 敌将[{hurt.Name}]负伤, 伤病等级:{hurt.injury}");
            GameEvent.OnPersonInjury?.Invoke(hurt, actionTroop);
        }

        /// <summary>
        /// 在被位移的部队里随机挑一名还能继续加重的武将（伤病未到上限）；
        /// 全员都到上限时返回空，本次位移不再造成伤病。
        /// </summary>
        /// <param name="troop">被位移的部队</param>
        static Person PickInjurablePerson(Troop troop)
        {
            if (troop == null) return null;

            List<Person> candidates = new List<Person>(3);
            troop.ForEachPerson(person =>
            {
                if (person != null && person.IsAlive && person.injury < Person.InjuryMaxLevel)
                    candidates.Add(person);
            });

            if (candidates.Count <= 0) return null;
            return candidates[GameRandom.Range(candidates.Count)];
        }
    }
}
