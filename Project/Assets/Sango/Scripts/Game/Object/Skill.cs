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

        public List<Vector2Int> atkOffsetPoint;
        //TODO:技能效果配置

        public void GetSpellRange(Cell where, List<Cell> cells)
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

        public void GetAttackCells(Cell spell, List<Cell> cells)
        {
            if (atkOffsetPoint == null || atkOffsetPoint.Count == 0)
            {
                cells.Add(spell);
            }
            else
            {
                for (int i = 0; i < atkOffsetPoint.Count; i++)
                {
                    Vector2Int offset = atkOffsetPoint[i];
                    Cell dest = spell.OffsetCell(offset.x, offset.y);
                    if (dest != null) cells.Add(dest);
                }
            }
        }

        public bool CanBeSpell(Troop troop)
        {
            //TODO: 完善技能释放规则
            if (costEnergy > troop.energy)
                return false;

            return true;
        }

        public bool IsRange()
        {
            return isRange;
        }

    }
}
