using System;
using System.Collections.Generic;
using Sango.Tools;

namespace Sango.Core
{
    /// <summary>
    /// 单挑系统类，负责处理三国志11风格的单挑逻辑
    /// </summary>
    public class DuelSystem
    {
        /// <summary>
        /// 攻击者部队
        /// </summary>
        public Troop AttackerTroop { get; private set; }
        
        /// <summary>
        /// 防御者部队
        /// </summary>
        public Troop DefenderTroop { get; private set; }
        
        /// <summary>
        /// 攻击者将领
        /// </summary>
        public Person Attacker { get; private set; }
        
        /// <summary>
        /// 防御者将领
        /// </summary>
        public Person Defender { get; private set; }
        
        /// <summary>
        /// 攻击者生命值
        /// </summary>
        public int AttackerHealth { get; private set; }
        
        /// <summary>
        /// 防御者生命值
        /// </summary>
        public int DefenderHealth { get; private set; }
        
        /// <summary>
        /// 最大生命值
        /// </summary>
        public int MaxHealth { get; private set; }
        
        /// <summary>
        /// 是否正在单挑中
        /// </summary>
        public bool IsDueling { get; private set; }
        
        /// <summary>
        /// 当前单挑状态
        /// </summary>
        public DuelState CurrentState { get; private set; }
        
        /// <summary>
        /// 回合数
        /// </summary>
        public int TurnCount { get; private set; }
        
        /// <summary>
        /// 攻击者决策
        /// </summary>
        public DuelDecision AttackerDecision { get; set; }
        
        /// <summary>
        /// 防御者决策
        /// </summary>
        public DuelDecision DefenderDecision { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="attackerTroop">攻击者部队</param>
        /// <param name="defenderTroop">防御者部队</param>
        public DuelSystem(Troop attackerTroop, Troop defenderTroop)
        {
            AttackerTroop = attackerTroop;
            DefenderTroop = defenderTroop;
            Attacker = attackerTroop.Leader;
            Defender = defenderTroop.Leader;
            MaxHealth = 100;
            AttackerHealth = MaxHealth;
            DefenderHealth = MaxHealth;
            IsDueling = false;
            CurrentState = DuelState.None;
            TurnCount = 0;
            AttackerDecision = DuelDecision.None;
            DefenderDecision = DuelDecision.None;
        }

        /// <summary>
        /// 开始单挑
        /// </summary>
        /// <returns>是否成功开始单挑</returns>
        public bool StartDuel()
        {
            if (IsDueling) return false;

            // 检查是否满足单挑条件
            if (!CanStartDuel()) return false;

            IsDueling = true;
            CurrentState = DuelState.Started;
            TurnCount = 0;
            AttackerDecision = DuelDecision.None;
            DefenderDecision = DuelDecision.None;

            // 触发单挑开始事件
            GameEvent.OnDuelStart?.Invoke(this);

            return true;
        }

        /// <summary>
        /// 检查是否可以开始单挑
        /// </summary>
        /// <returns>是否满足单挑条件</returns>
        public bool CanStartDuel()
        {
            // 检查双方部队是否为敌方
            if (!AttackerTroop.IsEnemy(DefenderTroop)) return false;

            // 检查双方距离是否为1
            if (Scenario.Cur.Map.Distance(AttackerTroop.cell, DefenderTroop.cell) > 1) return false;

            // 检查双方将领是否存在
            if (Attacker == null || Defender == null) return false;

            // 检查双方部队是否为战斗部队
            if (!AttackerTroop.IsFight || !DefenderTroop.IsFight) return false;

            return true;
        }

        /// <summary>
        /// 处理单挑回合
        /// </summary>
        /// <returns>单挑结果</returns>
        public DuelResult ProcessDuelTurn()
        {
            if (!IsDueling) return DuelResult.None;

            TurnCount++;

            // 每3回合要求选择决策
            if (TurnCount % 3 == 1)
            {
                // 触发决策选择事件
                GameEvent.OnDuelDecisionRequired?.Invoke(this);
                
                // 等待玩家选择决策
                // 这里需要UI系统来处理决策选择
                // 暂时使用默认决策
                if (AttackerDecision == DuelDecision.None)
                {
                    AttackerDecision = GetDefaultDecision(Attacker);
                }
                if (DefenderDecision == DuelDecision.None)
                {
                    DefenderDecision = GetDefaultDecision(Defender);
                }
            }

            // 双方攻击顺序
            bool attackerFirst = GameRandom.Chance(50, 100);

            if (attackerFirst)
            {
                // 攻击者先攻击
                AttackResult attackerResult = Attack(Attacker, Defender, AttackerDecision, DefenderDecision);
                if (DefenderHealth <= 0)
                {
                    return EndDuel(DuelResult.AttackerWin);
                }

                // 防御者反击
                AttackResult defenderResult = Attack(Defender, Attacker, DefenderDecision, AttackerDecision);
                if (AttackerHealth <= 0)
                {
                    return EndDuel(DuelResult.DefenderWin);
                }
            }
            else
            {
                // 防御者先攻击
                AttackResult defenderResult = Attack(Defender, Attacker, DefenderDecision, AttackerDecision);
                if (AttackerHealth <= 0)
                {
                    return EndDuel(DuelResult.DefenderWin);
                }

                // 攻击者反击
                AttackResult attackerResult = Attack(Attacker, Defender, AttackerDecision, DefenderDecision);
                if (DefenderHealth <= 0)
                {
                    return EndDuel(DuelResult.AttackerWin);
                }
            }

            // 检查回合数是否达到上限
            if (TurnCount >= 50)
            {
                return EndDuel(DuelResult.Draw);
            }

            return DuelResult.Continue;
        }

        /// <summary>
        /// 获取默认决策
        /// </summary>
        /// <param name="person">将领</param>
        /// <returns>默认决策</returns>
        private DuelDecision GetDefaultDecision(Person person)
        {
            // 根据将领属性选择默认决策
            // 武力高的倾向于攻击，智力高的倾向于技能，统率高的倾向于防御
            int strength = person.Strength;
            int intelligence = person.Intelligence;
            int command = person.Command;

            if (strength >= intelligence && strength >= command)
            {
                return DuelDecision.Attack;
            }
            else if (intelligence >= strength && intelligence >= command)
            {
                return DuelDecision.Skill;
            }
            else
            {
                return DuelDecision.Defend;
            }
        }

        /// <summary>
        /// 攻击逻辑
        /// </summary>
        /// <param name="attacker">攻击者</param>
        /// <param name="defender">防御者</param>
        /// <param name="attackerDecision">攻击者决策</param>
        /// <param name="defenderDecision">防御者决策</param>
        /// <returns>攻击结果</returns>
        private AttackResult Attack(Person attacker, Person defender, DuelDecision attackerDecision, DuelDecision defenderDecision)
        {
            // 计算攻击成功率
            int attackSuccessRate = CalculateAttackSuccessRate(attacker, defender);
            bool isHit = GameRandom.Chance(attackSuccessRate, 100);

            if (!isHit)
            {
                return AttackResult.Miss;
            }

            // 计算伤害
            int damage = CalculateDamage(attacker, defender);

            // 应用决策克制关系
            float counterFactor = DuelDecisionCounter.GetCounterFactor(attackerDecision, defenderDecision);
            damage = (int)(damage * counterFactor);

            // 应用伤害
            if (attacker == Attacker)
            {
                DefenderHealth -= damage;
                if (DefenderHealth < 0) DefenderHealth = 0;
            }
            else
            {
                AttackerHealth -= damage;
                if (AttackerHealth < 0) AttackerHealth = 0;
            }

            // 检查是否暴击
            int criticalRate = CalculateCriticalRate(attacker, defender);
            bool isCritical = GameRandom.Chance(criticalRate, 100);

            if (isCritical)
            {
                // 暴击伤害翻倍
                int criticalDamage = damage;
                if (attacker == Attacker)
                {
                    DefenderHealth -= criticalDamage;
                    if (DefenderHealth < 0) DefenderHealth = 0;
                }
                else
                {
                    AttackerHealth -= criticalDamage;
                    if (AttackerHealth < 0) AttackerHealth = 0;
                }
                return AttackResult.CriticalHit;
            }

            return AttackResult.Hit;
        }

        /// <summary>
        /// 计算攻击成功率
        /// </summary>
        /// <param name="attacker">攻击者</param>
        /// <param name="defender">防御者</param>
        /// <returns>攻击成功率（百分比）</returns>
        private int CalculateAttackSuccessRate(Person attacker, Person defender)
        {
            // 基础命中率
            int baseRate = 80;

            // 武力差影响
            int strengthDiff = attacker.Strength - defender.Strength;
            int strengthBonus = strengthDiff / 5;

            // 最终命中率
            int finalRate = baseRate + strengthBonus;
            return Math.Max(30, Math.Min(95, finalRate));
        }

        /// <summary>
        /// 计算暴击率
        /// </summary>
        /// <param name="attacker">攻击者</param>
        /// <param name="defender">防御者</param>
        /// <returns>暴击率（百分比）</returns>
        private int CalculateCriticalRate(Person attacker, Person defender)
        {
            // 基础暴击率
            int baseRate = 10;

            // 武力差影响
            int strengthDiff = attacker.Strength - defender.Strength;
            int strengthBonus = strengthDiff / 10;

            // 最终暴击率
            int finalRate = baseRate + strengthBonus;
            return Math.Max(0, Math.Min(30, finalRate));
        }

        /// <summary>
        /// 计算伤害
        /// </summary>
        /// <param name="attacker">攻击者</param>
        /// <param name="defender">防御者</param>
        /// <returns>伤害值</returns>
        private int CalculateDamage(Person attacker, Person defender)
        {
            // 基础伤害
            int baseDamage = 10;

            // 武力差影响
            int strengthDiff = attacker.Strength - defender.Strength;
            int strengthBonus = strengthDiff / 10;

            // 最终伤害
            int finalDamage = baseDamage + strengthBonus;
            return Math.Max(1, finalDamage);
        }

        /// <summary>
        /// 结束单挑
        /// </summary>
        /// <param name="result">单挑结果</param>
        /// <returns>单挑结果</returns>
        private DuelResult EndDuel(DuelResult result)
        {
            IsDueling = false;
            CurrentState = DuelState.Ended;

            // 处理单挑结果
            HandleDuelResult(result);

            // 触发单挑结束事件
            GameEvent.OnDuelEnd?.Invoke(this, result);

            return result;
        }

        /// <summary>
        /// 处理单挑结果
        /// </summary>
        /// <param name="result">单挑结果</param>
        private void HandleDuelResult(DuelResult result)
        {
            switch (result)
            {
                case DuelResult.AttackerWin:
                    // 攻击者获胜
                    HandleAttackerWin();
                    break;
                case DuelResult.DefenderWin:
                    // 防御者获胜
                    HandleDefenderWin();
                    break;
                case DuelResult.Draw:
                    // 平局
                    HandleDraw();
                    break;
            }
        }

        /// <summary>
        /// 处理攻击者获胜的情况
        /// </summary>
        private void HandleAttackerWin()
        {
            // 攻击者部队士气提升
            AttackerTroop.morale = Math.Min(100, AttackerTroop.morale + 20);

            // 防御者部队士气下降
            DefenderTroop.morale = Math.Max(0, DefenderTroop.morale - 20);

            // 有概率俘虏敌方将领
            if (GameRandom.Chance(30, 100))
            {
                CaptureGeneral(Defender, AttackerTroop);
            }

            // 有概率直接消灭敌方部队
            if (GameRandom.Chance(20, 100))
            {
                DefenderTroop.Clear();
            }
        }

        /// <summary>
        /// 处理防御者获胜的情况
        /// </summary>
        private void HandleDefenderWin()
        {
            // 防御者部队士气提升
            DefenderTroop.morale = Math.Min(100, DefenderTroop.morale + 20);

            // 攻击者部队士气下降
            AttackerTroop.morale = Math.Max(0, AttackerTroop.morale - 20);

            // 有概率俘虏敌方将领
            if (GameRandom.Chance(30, 100))
            {
                CaptureGeneral(Attacker, DefenderTroop);
            }

            // 有概率直接消灭敌方部队
            if (GameRandom.Chance(20, 100))
            {
                AttackerTroop.Clear();
            }
        }

        /// <summary>
        /// 处理平局的情况
        /// </summary>
        private void HandleDraw()
        {
            // 双方士气小幅提升
            AttackerTroop.morale = Math.Min(100, AttackerTroop.morale + 5);
            DefenderTroop.morale = Math.Min(100, DefenderTroop.morale + 5);
        }

        /// <summary>
        /// 俘虏将领
        /// </summary>
        /// <param name="general">被俘虏的将领</param>
        /// <param name="capturingTroop">俘虏方部队</param>
        private void CaptureGeneral(Person general, Troop capturingTroop)
        {
            // 从原部队中移除将领
            if (general == capturingTroop.Leader)
            {
                // 不能俘虏自己的将领
                return;
            }

            // 添加到俘虏列表
            capturingTroop.captiveList.Add(general);

            // 从原势力中移除
            if (general.mForce != null)
            {
                general.mForce.BeCaptiveList.Add(general);
            }

            // 触发俘虏事件
            GameEvent.OnPersonCaptured?.Invoke(general, capturingTroop);
        }
    }

    /// <summary>
    /// 单挑结果枚举
    /// </summary>
    public enum DuelResult
    {
        /// <summary>
        /// 无结果
        /// </summary>
        None,
        /// <summary>
        /// 攻击者获胜
        /// </summary>
        AttackerWin,
        /// <summary>
        /// 防御者获胜
        /// </summary>
        DefenderWin,
        /// <summary>
        /// 平局
        /// </summary>
        Draw,
        /// <summary>
        /// 继续
        /// </summary>
        Continue
    }

    /// <summary>
    /// 攻击结果枚举
    /// </summary>
    public enum AttackResult
    {
        /// <summary>
        /// 未命中
        /// </summary>
        Miss,
        /// <summary>
        /// 命中
        /// </summary>
        Hit,
        /// <summary>
        /// 暴击
        /// </summary>
        CriticalHit
    }

    /// <summary>
    /// 决策选项枚举
    /// </summary>
    public enum DuelDecision
    {
        /// <summary>
        /// 无决策
        /// </summary>
        None,
        /// <summary>
        /// 攻击
        /// </summary>
        Attack,
        /// <summary>
        /// 防御
        /// </summary>
        Defend,
        /// <summary>
        /// 技能
        /// </summary>
        Skill
    }

    /// <summary>
    /// 决策克制关系类
    /// </summary>
    public static class DuelDecisionCounter
    {
        /// <summary>
        /// 获取决策克制关系
        /// </summary>
        /// <param name="attackerDecision">攻击者决策</param>
        /// <param name="defenderDecision">防御者决策</param>
        /// <returns>克制系数，1为正常，大于1为克制，小于1为被克制</returns>
        public static float GetCounterFactor(DuelDecision attackerDecision, DuelDecision defenderDecision)
        {
            // 攻击克制技能，技能克制防御，防御克制攻击
            if (attackerDecision == DuelDecision.Attack && defenderDecision == DuelDecision.Skill)
            {
                return 1.5f; // 攻击克制技能
            }
            else if (attackerDecision == DuelDecision.Skill && defenderDecision == DuelDecision.Defend)
            {
                return 1.5f; // 技能克制防御
            }
            else if (attackerDecision == DuelDecision.Defend && defenderDecision == DuelDecision.Attack)
            {
                return 1.5f; // 防御克制攻击
            }
            else if (attackerDecision == defenderDecision)
            {
                return 1.0f; // 相同决策，正常效果
            }
            else
            {
                return 0.8f; // 被克制
            }
        }
    }
}
