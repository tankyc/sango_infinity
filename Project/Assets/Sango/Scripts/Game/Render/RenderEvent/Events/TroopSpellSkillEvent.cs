using Sango.Render;
using UnityEngine;


namespace Sango.Game.Render
{
    public class TroopSpellSkillEvent : RenderEventBase
    {
        public Troop troop;
        public Skill skill;
        public Cell spellCell;
        private bool isAction = false;
        private float time = 0;
        public override void Enter(Scenario scenario)
        {
            isAction = false;
            time = 0;
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(true);
            }
        }

        public override void Exit(Scenario scenario)
        {
            if (IsVisible())
            {
                troop.Render.SetSmokeShow(false);
            }
        }

        public override bool IsVisible()
        {
            return troop.Render.IsVisible();
        }

        public override bool Update(Scenario scenario, float deltaTime)
        {
            if (!IsVisible())
            {
                Action();
                IsDone = true;
                return IsDone;
            }
            IsDone = skill.UpdateRender(troop, spellCell, scenario, time, Action);
            time += deltaTime;
            return IsDone;
        }


        public void Action()
        {
            if (isAction) return;

            Troop targetTroop = spellCell.troop;
            BuildingBase targetBuilding = spellCell.building;
            //TODO: 释放技能
            Troop.tempCellList.Clear();
            skill.GetAttackCells(troop, spellCell, Troop.tempCellList);
            for (int i = 0; i < Troop.tempCellList.Count; i++)
            {
                Cell atkCell = Troop.tempCellList[i];
                Troop beAtkTroop = atkCell.troop;
                if (beAtkTroop != null && skill.canDamageTroop)
                {
                    int damage = Troop.CalculateSkillDamage(troop, beAtkTroop, skill);
                    if (damage < 0)
                    {
                        damage = 0;
                    }
                    beAtkTroop.ChangeTroops(-damage, troop);
                     
#if SANGO_DEBUG
                    Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 使用<{skill.Name}> 攻击 {beAtkTroop.BelongForce.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType.Name}], 造成伤害:{damage}, 目标剩余兵力: {beAtkTroop.GetTroopsNum()}");
#endif
                    // 反击
                    if (beAtkTroop.IsAlive && targetTroop == beAtkTroop)
                    {
                        float hitBack = beAtkTroop.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(troop.cell, atkCell));
                        if (hitBack > 0)
                        {
                            int hitBackDmg = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(beAtkTroop, troop, null));
                            troop.ChangeTroops(-hitBackDmg, beAtkTroop);
#if SANGO_DEBUG
                            Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 受到 {beAtkTroop.BelongForce.Name}的[{beAtkTroop.Name} - {beAtkTroop.TroopType.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif

                        }
                    }
                }

                BuildingBase beAtkBuildingBase = atkCell.building;
                if (beAtkBuildingBase != null && skill.canDamageBuilding)
                {
                    int damage = Troop.CalculateSkillDamage(troop, beAtkBuildingBase, skill);
#if SANGO_DEBUG
                    Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 使用<{skill.Name}> 攻击 {beAtkBuildingBase.BelongForce?.Name}的 [{beAtkBuildingBase.Name}], 造成伤害:{damage}, 目标剩余耐久: {beAtkBuildingBase.durability}");
#endif

                    if(beAtkBuildingBase is City)
                    {
                        City city = (City)beAtkBuildingBase;
                        if (city.BelongForce == null || city.troops <= 0)
                        {
                            Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 攻破城池: <{beAtkBuildingBase.Name}>");
                            city.OnFall(troop);
                            return;
                        }
                    }

                    if (beAtkBuildingBase.ChangeDurability(-damage, troop))
                    {
#if SANGO_DEBUG

                        Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 攻破城池: <{beAtkBuildingBase.Name}>");
#endif
                    }
                    else
                    {
                        // 城池反击
                        if (targetBuilding == beAtkBuildingBase)
                        {
                            float hitBack = beAtkBuildingBase.GetAttackBackFactor(skill, Scenario.Cur.Map.Distance(troop.cell, atkCell));
                            if (hitBack > 0)
                            {
                                int dam = beAtkBuildingBase.GetAttack();
                                if (dam > 0)
                                {
                                    int hitBackDmg = (int)System.Math.Ceiling(hitBack * Troop.CalculateSkillDamage(beAtkBuildingBase, troop, null));
                                    troop.ChangeTroops(-hitBackDmg, beAtkBuildingBase);
#if SANGO_DEBUG
                                    Sango.Log.Print($"{troop.BelongForce.Name}的[{troop.Name} - {troop.TroopType.Name}] 受到 {beAtkBuildingBase.BelongForce?.Name}的[{beAtkBuildingBase.Name}]反击伤害:{hitBackDmg}, 目标剩余兵力: {troop.GetTroopsNum()}");
#endif
                                }
                            }
                        }
                    }
                }

            }
            troop.morale -= skill.costEnergy;
            if(troop.morale < 0)
            {
                troop.morale = 0;
            }
            troop.Render.UpdateRender();
            isAction = true;
        }

    }
}
