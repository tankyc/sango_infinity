using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Skill : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Skill; } }

        [JsonProperty] public int atk;
        [JsonProperty] public int atkDurability;
        [JsonProperty] public int costEnergy;
        [JsonProperty] public bool canDamageTroop;
        [JsonProperty] public bool canDamageMachine;
        [JsonProperty] public bool canDamageBoat;
        [JsonProperty] public bool canDamageBuilding;
        [JsonProperty] public bool canDamageTeam;
        [JsonProperty] public bool isRange;
        [JsonProperty] public int level;
        [JsonProperty] public int[] spellRanges;
        [JsonProperty] public int needAblilityLevel;

        [JsonProperty] public List<int> atkOffsetPoint;
        //TODO:技能效果配置

        public void GetSpellRange(Troop atker, Cell where, List<Cell> cells)
        {
            if (spellRanges == null || spellRanges.Length == 0)
            {
                cells.Add(where);
            }
            else
            {
                for (int i = 0; i < spellRanges.Length; i++)
                {
                    Scenario.Cur.Map.GetRing(where, spellRanges[i], cells);
                }
            }
        }

        ////从攻击位置反找一个施法位置
        //public void GetSpellCells(Cell atkCell, List<Cell> cells)
        //{

        //}

        public void GetAttackCells(Troop atker, Cell spell, List<Cell> cells)
        {
            if (atkOffsetPoint == null || atkOffsetPoint.Count == 0)
            {
                cells.Add(spell);
            }
            else
            {
                SkillAttackOffsetType aopType = (SkillAttackOffsetType)atkOffsetPoint[0];
                switch (aopType)
                {
                    // 0
                    case SkillAttackOffsetType.Customize:
                        {
                            for (int i = 1; i < atkOffsetPoint.Count; i += 2)
                            {
                                int offsetX = atkOffsetPoint[i];
                                int offsetY = atkOffsetPoint[i + 1];
                                Cell dest = spell.OffsetCell(offsetX, offsetY);
                                if (dest != null) cells.Add(dest);
                            }
                        }
                        break;
                    // 1
                    case SkillAttackOffsetType.Ring:
                        {
                            if (atkOffsetPoint.Count > 1)
                            {
                                int radius = atkOffsetPoint[1];
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                spell.Ring(radius, (cell) => { cells.Add(cell); });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 2
                    case SkillAttackOffsetType.DirectionLine:
                        {
                            if (atkOffsetPoint.Count > 1)
                            {
                                int length = atkOffsetPoint[1];
                                if (length <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }

                                atker.cell.DirectionLine(spell, length, (cell) =>
                                {
                                    cells.Add(cell);
                                });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    default:
                        cells.Add(spell);
                        break;
                }
            }
        }

        public bool CanAddToTroop(Troop troop)
        {
            return troop.TroopTypeLv >= needAblilityLevel;
        }

        public bool CanBeSpell(Troop troop)
        {
            //TODO: 完善技能释放规则
            if (costEnergy > troop.morale)
                return false;

            return true;
        }

        public bool IsRange()
        {
            return isRange;
        }

        public bool UpdateRender(Troop troop, Cell spellCell, Scenario scenario, float time, Action action)
        {
            if (time <= 0f)
            {
                troop.Render.FaceTo(spellCell.Position);
                troop.Render.SetAniShow(1);
            }
            if (time > 1f)
                action();
            if (time > 2.5f)
            {
                troop.Render.SetAniShow(0);
                return true;
            }
            return false;
        }
    }
}
