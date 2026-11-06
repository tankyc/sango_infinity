/*
 * 文件名：Skill.cs
 * 描述：技能类，管理游戏中的技能配置
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 计略类型 —— 对应原版 StrategyIdEnum 里参与 AI 评分的九个分支
    /// （add_strategy_action，D:\open311\s11\s11_sys_unit_action.cpp:883-1225）。
    ///
    /// 由 SkillTypes.json 的 <c>strategyType</c> 配置；None 表示该技能不按计略分支评分。
    /// </summary>
    public enum StrategyType
    {
        None = 0,
        /// <summary>消火（原版 StrategyId_Shouka）</summary>
        Shouka = 1,
        /// <summary>火计（原版 StrategyId_Fire）</summary>
        Fire = 2,
        /// <summary>伪报（原版 StrategyId_Gihou）</summary>
        Gihou = 3,
        /// <summary>混乱（原版 StrategyId_Kakuran）</summary>
        Kakuran = 4,
        /// <summary>镇静（原版 StrategyId_Chinsei）</summary>
        Chinsei = 5,
        /// <summary>伏兵（原版 StrategyId_Fukuhei）</summary>
        Fukuhei = 6,
        /// <summary>同讨（原版 StrategyId_Doushiuchi）</summary>
        Doushiuchi = 7,
        /// <summary>妖术（原版 StrategyId_Yojutsu）</summary>
        Yojutsu = 8,
        /// <summary>落雷（原版 StrategyId_Rakurai）</summary>
        Rakurai = 9,
    }

    /// <summary>
    /// 技能类，管理游戏中的技能配置
    /// 包含技能类型、攻击力、施法范围、效果等属性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Skill : SangoObjectExtensionData
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
        /// 计略类型（仅 <see cref="StrategyType.None"/> 以外时有意义）。
        /// 用于 AI 评分按原版 add_strategy_action 的九个分支分别给分。
        /// </summary>
        [JsonProperty] public int strategyType;

        /// <summary>
        /// 性格口径的计略分类（<see cref="PersonalitySkillType"/>）。
        ///
        /// -1 = 不参与性格的抗性 / 专精表（火计、灭火等）。
        /// 0 伪报 / 1 扰乱 / 2 镇静 / 3 伏兵 / 4 妖术 / 5 内讧。
        ///
        /// 【为什么放在数据里】旧实现是 PersonalitySkillMap 里写死 24/25/26/27/28 五个技能 Id，
        /// 新增一个计略就必须改代码。现在由技能表自己声明归属，
        /// 判断链路（SkillSuccessMethod / SkillCriticalMethod / TroopAIUtility）无需再改。
        /// </summary>
        [JsonProperty] public int personalitySkillType = -1;

        /// <summary>
        /// 【AI 评分】本战法的动作常量（原版 add_tactic_action 各分支的 949 / 1049 / 899 …）。
        ///
        /// 原版是按 TacticId 硬编码在 s11_sys_unit_action.cpp 的 add_tactic_action 里：
        ///   撞飞/熊手 949、乱射 1049、二段突 899 …… 每个战法不同。
        /// 本项目改为数据驱动：该字段为 0 时回落到 AIConfig.o311TacticActionBonus（949）。
        /// 填了则以本字段为准，从而真正做到"每个战法一个常量"。
        /// </summary>
        [JsonProperty] public int aiActionBonus;

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
        /// 释放完成后是否允许挑起单挑。
        /// 只在**战法**(kind=2)上生效：普攻 / 计略本来就不参与单挑触发（见 DuelSkillTrigger）；
        /// 置否 = 该战法无论命中与否都不会发起单挑；置是也只是"允许"，最终还要过概率与发起资格。
        /// 默认 true（与加入本开关之前的行为一致），需要禁掉的战法在数据里勾掉即可。
        /// 本值在创建战法实例时被复制到 SkillInstance.canTriggerDuel（运行期可由 Action 再改写）。
        /// </summary>
        [JsonProperty] public bool canTriggerDuel = true;

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
        /// 可释放筛选逻辑
        /// </summary>
        [JsonProperty] public string spellConditionMethod;

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
                            // 【越界防护】Customize 的格式必须是 [type, x1, y1, x2, y2, ...]
                            // ——长度 1 + 2n（奇数）。运行时若被改写成偶数长度（例如存档里残留的
                            // [0, range]，或配置错误的 TroopChangeSkillAttackRange 生成的
                            // new int[]{ atkType, atkRange }），原写法 `atkOffsetPoint[i + 1]`
                            // 会在最后一次循环抛 IndexOutOfRangeException，打断整回合 AI
                            // （栈：TroopAIUtility.PriorityAction → SkillInstance.GetAttackCells）。
                            for (int i = 1; i + 1 < atkOffsetPoint.Length; i += 2)
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
                            // 邻居可能为空（地图边界），先判空
                            if (nCell != null && nCell.moveAble)
                                cells.Add(nCell);
                            nCell = atker.cell.GetNeighbor(dir - 1);
                            if (nCell != null && nCell.moveAble)
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

        /// <summary>
        /// 技能是否带有附加效果（混乱 / 火伤 / 位移等）。
        ///
        /// 【修复】原先实现写成 `skillEffects == null || Count == 0`，语义完全反了：
        /// 越是没有特效的技能反而返回 true，导致 scoreEffectBonus 加在了错误的对象上，
        /// 真正带特效的技能拿不到加成。现改为"存在且非空"。
        /// </summary>
        /// <returns>带有附加效果时返回 true</returns>
        public bool HasEffect()
        {
            return skillEffects != null && skillEffects.Count > 0;
        }

        public bool CanAddToTroop(Troop troop, bool isWater)
        {
            if(isWater)
            {
                return troop.WaterTroopTypeLv >= needAblilityLevel;
            }
            else
            {
                return troop.LandTroopTypeLv >= needAblilityLevel;
            }
        }

        public bool IsRange()
        {
            return isRange;
        }

        public bool IsStrategy()
        {
            return kind == (int)SkillKindType.Stratagem;
        }

        /// <summary>
        /// 本次施法对应的原版 Exp 表事件行。
        ///
        /// 【映射依据】项目 Skills.json 里的技能 Id 与 Open311 的 Tactic / Strategy 一一对应
        /// （1/2 普攻、3~21 战法、22~30 计略），名称也逐条对得上；
        /// 战法的行布局是 [暴击, 命中, 未命中] 三连（原版索引
        /// <c>get_exp(tactic-&gt;get_exp() + 1 - tactic_hit - critical)</c>，s11_sys_anim_unit.cpp:1090），
        /// 计略同理（<c>get_exp(Exp_KakeiMiss + 3 * strategy_id - hit - critical)</c>）。
        /// 因此登记每组的"未命中行"，再用 <see cref="CombatExpTable.ResolveRow"/> 反推命中 / 暴击。
        ///
        /// 新增战法 / 计略时必须在这里补一行，否则会落到 <see cref="CombatExpEvent.Attack"/> 兜底。
        /// </summary>
        /// <param name="hit">本次是否命中（普攻恒为 true）</param>
        /// <param name="critical">本次是否暴击</param>
        public CombatExpEvent GetCombatExpEvent(bool hit, bool critical)
        {
            // 计略：优先按 strategyType 判定（Skills.json 已为九条计略全部配置），不依赖 Id。
            if (IsStrategy())
            {
                switch ((StrategyType)strategyType)
                {
                    case StrategyType.Fire: return CombatExpTable.ResolveRow(CombatExpEvent.KakeiMiss, hit, critical);
                    case StrategyType.Shouka: return CombatExpTable.ResolveRow(CombatExpEvent.ShoukaMiss, hit, critical);
                    case StrategyType.Gihou: return CombatExpTable.ResolveRow(CombatExpEvent.GihouMiss, hit, critical);
                    case StrategyType.Kakuran: return CombatExpTable.ResolveRow(CombatExpEvent.KakuranMiss, hit, critical);
                    case StrategyType.Chinsei: return CombatExpTable.ResolveRow(CombatExpEvent.ChinseiMiss, hit, critical);
                    case StrategyType.Fukuhei: return CombatExpTable.ResolveRow(CombatExpEvent.FukuheiMiss, hit, critical);
                    case StrategyType.Doushiuchi: return CombatExpTable.ResolveRow(CombatExpEvent.DoushiuchiMiss, hit, critical);
                    case StrategyType.Yojutsu: return CombatExpTable.ResolveRow(CombatExpEvent.YojutsuMiss, hit, critical);
                    case StrategyType.Rakurai: return CombatExpTable.ResolveRow(CombatExpEvent.RakuraiMiss, hit, critical);
                }
            }

            // 普攻：原版 s11_sys_anim_unit.cpp:1136 `get_exp(critical ? Exp_AttackCritical : Exp_Attack)`
            if (IsNormalSkill)
                return critical ? CombatExpEvent.AttackCritical : CombatExpEvent.Attack;

            // 战法：按技能 Id 找所属的"未命中行"
            CombatExpEvent missEvent;
            switch (Id)
            {
                case 3: missEvent = CombatExpEvent.TsukidashiMiss; break;        // 突刺
                case 4: missEvent = CombatExpEvent.RasenMiss; break;             // 螺旋突刺
                case 5: missEvent = CombatExpEvent.NidandukiMiss; break;         // 二段突刺
                case 6: missEvent = CombatExpEvent.KumadeMiss; break;            // 熊手
                case 7: missEvent = CombatExpEvent.YokonagiMiss; break;          // 横扫
                case 8: missEvent = CombatExpEvent.SenpuuMiss; break;            // 旋风
                case 9:                                                          // 火矢（弩）
                case 15:                                                         // 火矢（兵器）
                case 19: missEvent = CombatExpEvent.HiyaMiss; break;             // 火矢（水军）
                case 10: missEvent = CombatExpEvent.TsuranukiyaMiss; break;      // 贯箭
                case 11: missEvent = CombatExpEvent.RanshaMiss; break;           // 乱射
                case 12: missEvent = CombatExpEvent.TotsugekiMiss; break;        // 突击
                case 13: missEvent = CombatExpEvent.ToppaMiss; break;            // 突破
                case 14: missEvent = CombatExpEvent.TosshinMiss; break;          // 突进
                case 16: missEvent = CombatExpEvent.HasaiMiss; break;            // 破碎
                case 17: missEvent = CombatExpEvent.HoushaMiss; break;           // 放射
                case 18:                                                         // 投石（兵器）
                case 21: missEvent = CombatExpEvent.TousekiMiss; break;          // 投石（水军）
                case 20: missEvent = CombatExpEvent.GekitotsuMiss; break;        // 猛撞
                default: missEvent = CombatExpEvent.TsukidashiMiss; break;       // 未登记战法按突刺档兜底
            }

            return CombatExpTable.ResolveRow(missEvent, hit, critical);
        }

    }
}
