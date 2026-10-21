using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 战法会心率修正（value 为百分比点数）。
    ///
    /// 接入事件：|value| >= 100 时订阅 OnTroopBeforeCalculateSkillCritical（计算前覆盖），
    /// 否则订阅 OnTroopAfterCalculateSkillCritical（在计算结果上加减）。
    /// </summary>
    public class TroopSkillCalculateCritical : TroopTroopActionBase
    {
        /// <summary>
        /// 自己是否是目标
        /// </summary>
        bool selfIsTarget;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            selfIsTarget = p.Value<bool>("selfIsTarget");
            if (value >= 100 || value <= -100)
                GameEvent.OnTroopBeforeCalculateSkillCritical += OnTroopCalculateSkillCritical;
            else
                GameEvent.OnTroopAfterCalculateSkillCritical += OnTroopCalculateSkillCritical;

        }

        public override void Clear()
        {
            if (value >= 100 || value <= -100)
                GameEvent.OnTroopBeforeCalculateSkillCritical -= OnTroopCalculateSkillCritical;
            else
                GameEvent.OnTroopAfterCalculateSkillCritical -= OnTroopCalculateSkillCritical;
        }

        void OnTroopCalculateSkillCritical(Troop troop, SkillInstance skill, Cell spellCell, OverrideData<int> overrideData)
        {
            if (Force != null && troop.BelongForce != Force) return;

            if(!selfIsTarget)
            {
                if (Troop != null && Troop != troop) return;
            }
            else
            {
                // 【空引用修复】施放格子可能为空（例如无格子的全局判定），此时无法判定归属
                if (spellCell == null) return;
                if (Troop != null && Troop != spellCell.troop) return;
            }

            if (!CheckIsNormalSkill(skill, isNormal))
                return;

            if (checkLand == 1 && troop.IsInWater)
                return;
            if (checkLand == 2 && !troop.IsInWater)
                return;

            if (checkLand == 0 && kinds != null && !kinds.Contains(troop.LandTroopType.kind) && !kinds.Contains(troop.WaterTroopType.kind))
                return;

            if (checkLand == 1 && kinds != null && !kinds.Contains(troop.LandTroopType.kind))
                return;

            if (checkLand == 2 && kinds != null && !kinds.Contains(troop.WaterTroopType.kind))
                return;

            if (condition != null && !condition.Check(new TroopActionConditionDatabase(skill, spellCell)))
                return;

            overrideData.Value += value;
        }
    }
}
