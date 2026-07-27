/*
 * 文件名：Skill.cs
 * 描述：技能类，管理游戏中的技能配置
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能类，管理游戏中的技能配置
    /// 包含技能类型、攻击力、施法范围、效果等属性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Skill : SangoObject
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Skill; } }

        /// <summary>
        /// 技能类型
        /// </summary>
        [JsonProperty] public int kind;

        /// <summary>
        /// 基础攻击力
        /// </summary>
        [JsonProperty] public int atk;

        /// <summary>
        /// 基础攻城值
        /// </summary>
        [JsonProperty] public int atkDurability;

        /// <summary>
        /// 释放所需能量
        /// </summary>
        [JsonProperty] public int costEnergy;

        /// <summary>
        /// 是否可以对部队造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageTroop;

        /// <summary>
        /// 是否可以对器械造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageMachine;

        /// <summary>
        /// 是否可以对船只造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageBoat;

        /// <summary>
        /// 是否可以对建筑造成伤害
        /// </summary>
        [JsonProperty] public bool canDamageBuilding;

        /// <summary>
        /// 是否可误伤
        /// </summary>
        [JsonProperty] public bool canDamageTeam;

        /// <summary>
        /// 是否可空放
        /// </summary>
        [JsonProperty] public bool canSpellToCell;

        /// <summary>
        /// 是否只可对友军释放
        /// </summary>
        [JsonProperty] public bool onlySpellToTeam;

        /// <summary>
        /// 是否为远程技能
        /// </summary>
        [JsonProperty] public bool isRange;

        /// <summary>
        /// 施法范围
        /// </summary>
        [JsonProperty] public int[] spellRanges;

        /// <summary>
        /// 射程筛选逻辑
        /// </summary>
        [JsonProperty] public string rangeFilterMethod;

        /// <summary>
        /// 所需适应等级
        /// </summary>
        [JsonProperty] public int needAblilityLevel;

        /// <summary>
        /// 基础成功率
        /// </summary>
        [JsonProperty] public int successRate;

        /// <summary>
        /// 成功率计算类型
        /// </summary>
        [JsonProperty] public string successMethod;

        /// <summary>
        /// 暴击率计算类型
        /// </summary>
        [JsonProperty] public string criticalMethod;

        /// <summary>
        /// 额外的攻击位置
        /// </summary>
        [JsonProperty] public int[] atkOffsetPoint;

        /// <summary>
        /// 位移配置
        /// </summary>
        [JsonProperty] public int[] offsetAction;

        /// <summary>
        /// 碰撞伤害系数
        /// </summary>
        [JsonProperty] public int blockFactor;

        /// <summary>
        /// 技能效果
        /// </summary>
        [JsonProperty] public JArray skillEffects;

        /// <summary>
        /// 技能视觉效果类型
        /// </summary>
        [JsonProperty] public string visualType;

        /// <summary>
        /// 技能时间轴数据
        /// </summary>
        [JsonProperty] public JObject timelineData;

        /// <summary>
        /// 技能释放时候武将语音
        /// </summary>
        [JsonProperty] public int[] personSound;

        /// <summary>
        /// 激活的音效
        /// </summary>
        [JsonProperty] public int actionSound;


        ////从攻击位置反找一个施法位置
        //public void GetSpellCells(Cell atkCell, List<Cell> cells)
        //{

        //}
        public bool IsNormalSkill => kind == 1;

        public void GetAttackCells(int[] atkOffsetPoint, Troop atker, Cell spell, List<Cell> cells)
        {
            if (atkOffsetPoint == null || atkOffsetPoint.Length == 0)
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
                            for (int i = 1; i < atkOffsetPoint.Length; i += 2)
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
                            if (atkOffsetPoint.Length > 1)
                            {
                                int radius = atkOffsetPoint[1];
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                spell.Ring(radius, (cell) => { if (cell.moveAble) cells.Add(cell); });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 2
                    case SkillAttackOffsetType.DirectionLine:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int length = atkOffsetPoint[1];
                                if (length <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }

                                atker.cell.DirectionLine(spell, length, (cell) =>
                                {
                                    if (cell.moveAble)
                                        cells.Add(cell);
                                });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    //3
                    case SkillAttackOffsetType.SelfRing:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int radius = atkOffsetPoint[1];
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                atker.cell.Ring(radius, (cell) => { if (cell.moveAble) cells.Add(cell); });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    //4
                    case SkillAttackOffsetType.SpellNeighbors:
                        {
                            int dir = atker.cell.Cub.DirectionTo(spell.Cub);
                            cells.Add(spell);
                            Cell nCell = atker.cell.GetNeighbor(dir + 1);
                            if (nCell.moveAble)
                                cells.Add(nCell);
                            nCell = atker.cell.GetNeighbor(dir - 1);
                            if (nCell.moveAble)
                                cells.Add(nCell);
                        }
                        break;
                    // 5
                    case SkillAttackOffsetType.Spiral:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int radius = atkOffsetPoint[1];
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                spell.Spiral(radius, (cell) => { if (cell.moveAble) cells.Add(cell); });
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 6
                    case SkillAttackOffsetType.Fan:
                        {
                            if (atkOffsetPoint.Length > 2)
                            {
                                int radius = atkOffsetPoint[1];
                                int angle = atkOffsetPoint[2]; // 扇形角度
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                // 计算扇形范围
                                int dir = atker.cell.Cub.DirectionTo(spell.Cub);
                                int startDir = (dir - angle / 60 + 6) % 6;
                                int endDir = (dir + angle / 60) % 6;
                                for (int r = 1; r <= radius; r++)
                                {
                                    for (int d = startDir; d != endDir; d = (d + 1) % 6)
                                    {
                                        Cell cell = spell.OffsetCell(d, r);
                                        if (cell != null && cell.moveAble)
                                            cells.Add(cell);
                                    }
                                }
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 7
                    case SkillAttackOffsetType.Rectangle:
                        {
                            if (atkOffsetPoint.Length > 2)
                            {
                                int width = atkOffsetPoint[1];
                                int length = atkOffsetPoint[2];
                                if (width <= 0 || length <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                // 计算矩形范围
                                int dir = atker.cell.Cub.DirectionTo(spell.Cub);
                                for (int l = 0; l < length; l++)
                                {
                                    Cell currentCell = spell.OffsetCell(dir, l);
                                    if (currentCell == null)
                                        break;
                                    for (int w = -width / 2; w <= width / 2; w++)
                                    {
                                        int perpDir = (dir + 1) % 6;
                                        Cell cell = currentCell.OffsetCell(perpDir, w);
                                        if (cell != null && cell.moveAble)
                                            cells.Add(cell);
                                    }
                                }
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 8
                    case SkillAttackOffsetType.Cross:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int length = atkOffsetPoint[1];
                                if (length <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                // 计算十字形范围
                                cells.Add(spell);
                                for (int d = 0; d < 6; d++)
                                {
                                    for (int l = 1; l <= length; l++)
                                    {
                                        Cell cell = spell.OffsetCell(d, l);
                                        if (cell != null && cell.moveAble)
                                            cells.Add(cell);
                                        else
                                            break;
                                    }
                                }
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 9
                    case SkillAttackOffsetType.Square:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int size = atkOffsetPoint[1];
                                if (size <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                // 计算方形范围
                                for (int x = -size; x <= size; x++)
                                {
                                    for (int y = -size; y <= size; y++)
                                    {
                                        Cell cell = spell.OffsetCell(x, y);
                                        if (cell != null && cell.moveAble)
                                            cells.Add(cell);
                                    }
                                }
                            }
                            else
                                Sango.Log.Error("技能命中配置不正确!!");
                        }
                        break;
                    // 10
                    case SkillAttackOffsetType.Diamond:
                        {
                            if (atkOffsetPoint.Length > 1)
                            {
                                int radius = atkOffsetPoint[1];
                                if (radius <= 0)
                                {
                                    cells.Add(spell);
                                    return;
                                }
                                // 计算菱形范围
                                for (int r = 0; r <= radius; r++)
                                {
                                    for (int d = 0; d < 6; d++)
                                    {
                                        Cell cell = spell.OffsetCell(d, r);
                                        if (cell != null && cell.moveAble)
                                            cells.Add(cell);
                                    }
                                }
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

        public bool IsSingleSkill()
        {
            return atkOffsetPoint == null || atkOffsetPoint.Length == 0;
        }

        public bool HasEffect()
        {
            return skillEffects == null || skillEffects.Count == 0;
        }

        public bool CanAddToTroop(Troop troop)
        {
            return troop.TroopTypeLv >= needAblilityLevel;
        }

        public bool IsRange()
        {
            return isRange;
        }

        public bool IsStrategy()
        {
            return kind == 3;
        }

    }
}
