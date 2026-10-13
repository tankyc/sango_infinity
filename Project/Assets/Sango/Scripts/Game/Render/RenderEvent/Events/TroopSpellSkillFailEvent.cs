using Sango.Core;
using UnityEngine;


namespace Sango.Render
{
    public class TroopSpellSkillFailEvent : RenderEventBase
    {
        public Troop troop;
        public SkillInstance skill;
        public Troop targetTroop;
        public BuildingBase targetBuilding;
        public Cell spellCell;
        private bool isAction = false;
        private SkillInstance replaceSkill;
        private float time = 0;

        public void Init(Troop troop, SkillInstance skill, Cell spellCell)
        {
            this.troop = troop;
            this.skill = skill;
            this.spellCell = spellCell;
            this.targetTroop = spellCell.troop;
            this.targetBuilding = spellCell.building;
            this.isAction = false;
            this.replaceSkill = null;
            this.time = 0;
            IsDone = false;
        }
        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            if (IsVisible())
            {
                GameMedia.Instance.PlaySfx(74);
                GameMedia.Instance.PlaySfx(82);
                troop.Render.SetSmokeShow(true);
            }

            if (!skill.IsStrategy())
            {
                replaceSkill = skill.IsRange() ? troop.NormalRangeSkill : troop.NormalSkill;
            }

            if (!skill.IsNormal())
                troop.Render.ShowSkill(skill, true, false);
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(false);
            }
            if (replaceSkill == null)
                GameEvent.OnSkillActionEnd?.Invoke(skill, spellCell, targetTroop, targetBuilding);
        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        /// <summary>
        /// 扣除本次施法失败的气力消耗。
        /// 与成功路径(SkillInstance.Action 末尾统一扣气力)保持一致:
        /// 只要施法动作已经发生, 无论玩家是否跳过动画、部队是否在屏幕内,
        /// 只要施法部队还存活, 就要扣掉该技能自身的 costEnergy。
        /// 注意: 只扣失败技能本身的消耗, 不扣 replaceSkill(普攻)的消耗。
        /// </summary>
        void DeductCostEnergy()
        {
            if (skill != null && troop != null && troop.IsAlive)
                troop.ChangeMorale(-skill.costEnergy, false);
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsVisible() || Input.GetMouseButtonDown(0))
            {
                Action();
                troop?.Render?.SetAniShow(0);
                // 修复: 跳过动画(部队不在屏幕内/玩家点击快进)时此前不扣气力,
                // 导致技能失败可以"免费"白嫖一次出手, 这里补上统一扣气力
                DeductCostEnergy();
                IsDone = true;
                return IsDone;
            }

            if (replaceSkill == null)
            {
                Action();
                IsDone = true;
                // 修复: 此前只对计略类技能扣气力, 其它有消耗的技能失败时不扣;
                // 现统一扣除失败技能自身的气力消耗
                DeductCostEnergy();

                troop?.Render?.SetAniShow(0);
                return IsDone;
            }

            IsDone = replaceSkill.UpdateRender(spellCell, scenario, time, Action);
            time += deltaTime;
            if (IsDone)
            {
                // 修复: 非计略技能失败时, 此前只通过 replaceSkill.Action 扣了普攻的气力(通常为0),
                // 失败技能本身的气力从未扣除, 这里在替代演出结束时补扣
                DeductCostEnergy();
            }
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;
            if (replaceSkill != null)
                replaceSkill.Action(spellCell, 100);
            isAction = true;
        }

    }
}