using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 击破敌军触发：持技部队**击破敌方部队的那一刻**，结算一组"以击破为条件"的效果。
    ///
    /// 【时机】订阅 <c>GameEvent.OnTroopDestroyed</c>。
    /// 该事件由 <c>Troop.ChangeTroops</c> 在兵力归零时、<c>Clear()</c> **之前**派发，
    /// 因此被击破部队的 <c>cell</c> 仍然有效（可用于"击破位置"相关的范围效果）。
    ///
    /// 【击破者判定】事件的 atk 通常是 <see cref="SkillInstance"/>（普攻与战法都走战法结算），
    /// 少数路径直接传 Troop。这里统一按 <c>AsTroop</c> 的同一口径手工归一：
    /// SkillInstance → master，Troop → 本身。
    ///
    /// 【参数】
    /// selfRecurePercent       自身按**当前兵力**百分比回复（"以德服人"= 5）
    /// selfMorale              自身气力增减（"蛟龙入水"= 10，可配 requireEnemyInWater）
    /// bound                   友军 / 敌军范围效果的半径（格）
    /// allyMorale              范围内友军气力增减（"江东霸王"）
    /// extraActionProbability  范围内友军恢复行动的概率（万分比，"鼓舞三军"= 2000）
    /// enemyTroopsPercent      范围内敌军按当前兵力百分比增减（"锋矢十面" = -50）
    /// enemyMoralePercent      范围内敌军按当前气力百分比增减（"锋矢十面" = -50）
    /// learnFeatureProbability 学得敌将特技的概率（万分比，"即学即用"）
    /// learnMeritThreshold     学习前置：主将功绩下限（"即学即用" = 15000）
    /// requireEnemyInWater     仅当被击破部队是水上部队时生效（"蛟龙入水"）
    /// condition               额外条件（TroopConditionDatabase）
    /// </summary>
    public class TroopDefeatEnemyEffect : TroopActionBase
    {
        /// <summary>自身按当前兵力百分比回复兵力</summary>
        int selfRecurePercent;
        /// <summary>自身气力增减</summary>
        int selfMorale;
        /// <summary>友军 / 敌军范围效果的半径</summary>
        int bound;
        /// <summary>范围内友军气力增减</summary>
        int allyMorale;
        /// <summary>范围内友军恢复行动的概率（万分比）</summary>
        int extraActionProbability;
        /// <summary>范围内敌军兵力百分比增减</summary>
        int enemyTroopsPercent;
        /// <summary>范围内敌军气力百分比增减</summary>
        int enemyMoralePercent;
        /// <summary>学得敌将特技的概率（万分比）</summary>
        int learnFeatureProbability;
        /// <summary>学习前置：主将功绩下限</summary>
        int learnMeritThreshold;
        /// <summary>仅当被击破者是水上部队时生效</summary>
        bool requireEnemyInWater;
        /// <summary>额外条件</summary>
        Condition condition;

        /// <summary>范围内目标复用缓冲</summary>
        readonly List<Troop> targetBuffer = new List<Troop>(8);

        /// <summary>
        /// 初始化参数并订阅部队击破事件。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 部队，[1] 触发装配的武将）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            selfRecurePercent = p.Value<int>("selfRecurePercent");
            selfMorale = p.Value<int>("selfMorale");
            bound = p.Value<int>("bound");
            allyMorale = p.Value<int>("allyMorale");
            extraActionProbability = p.Value<int>("extraActionProbability");
            enemyTroopsPercent = p.Value<int>("enemyTroopsPercent");
            enemyMoralePercent = p.Value<int>("enemyMoralePercent");
            learnFeatureProbability = p.Value<int>("learnFeatureProbability");
            learnMeritThreshold = p.Value<int>("learnMeritThreshold");
            requireEnemyInWater = p.Value<bool>("requireEnemyInWater");

            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"击破触发效果的 condition 类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }

            // 装配宿主既不是部队也不是势力时不订阅事件（避免"任何人击破敌军都触发"）
            if (Troop == null && Force == null)
            {
                Sango.Log.Error($"击破触发效果的装配宿主既不是部队也不是势力，特技不会生效：{p}");
                return;
            }

            GameEvent.OnTroopDestroyed += OnTroopDestroyed;
        }

        /// <summary>
        /// 退订事件。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopDestroyed -= OnTroopDestroyed;
        }

        /// <summary>
        /// 事件驱动效果，不需要外部主动触发。
        /// </summary>
        /// <param name="trigger">触发参数（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }

        /// <summary>
        /// 部队被击破回调：先确认"是不是我打的"，再结算全部效果。
        /// </summary>
        /// <param name="destroyed">被击破的部队</param>
        /// <param name="atk">击破来源（SkillInstance / Troop，可为 null）</param>
        /// <param name="atkBack">反击标记（未使用）</param>
        /// <param name="scenario">当前剧本</param>
        void OnTroopDestroyed(Troop destroyed, SangoObject atk, int atkBack, Scenario scenario)
        {
            if (destroyed == null)
                return;

            Troop owner = ResolveAttacker(atk);
            if (owner == null || owner == destroyed)
                return;

            // 只处理"持技部队击破敌军"
            if (Troop != null)
            {
                if (owner != Troop)
                    return;
            }
            else if (Force != null && owner.BelongForce != Force)
            {
                return;
            }

            if (!owner.IsAlive)
                return;

            // 兵种过滤（如「锋矢十面」只在弩兵击破时生效）
            if (kinds != null && (owner.TroopType == null || !kinds.Contains(owner.TroopType.kind)))
                return;

            if (requireEnemyInWater && !destroyed.IsInWater)
                return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(owner)))
                return;

            // ① 自身回兵（以德服人）
            if (selfRecurePercent > 0)
            {
                int recure = owner.troops * selfRecurePercent / 100;
                if (recure > 0)
                    owner.ChangeTroops(recure, owner, 0);
            }

            // ② 自身气力（蛟龙入水）
            if (selfMorale != 0)
                owner.ChangeMorale(selfMorale);

            if (bound <= 0)
                return;

            // ③ 范围效果：友军加气力 / 概率恢复行动；敌军扣兵扣气
            CollectRangeTroops(owner);
            for (int i = 0; i < targetBuffer.Count; i++)
            {
                Troop target = targetBuffer[i];
                if (target == null || !target.IsAlive)
                    continue;

                if (owner.IsSameForce(target))
                {
                    if (allyMorale != 0)
                        target.ChangeMorale(allyMorale);

                    if (extraActionProbability > 0
                        && GameRandom.Chance(extraActionProbability, 10000))
                    {
                        // 与 TroopResetActionOver 同口径：额外行动不允许再叠二动次数
                        if (!target.isExtraAction)
                            target.ActionOverCount++;
                    }
                }
                else if (owner.IsEnemy(target))
                {
                    if (enemyTroopsPercent != 0)
                    {
                        int num = target.troops * enemyTroopsPercent / 100;
                        if (num != 0)
                            target.ChangeTroops(num, owner, 0);
                    }

                    if (enemyMoralePercent != 0)
                    {
                        int num = target.morale * enemyMoralePercent / 100;
                        if (num != 0)
                            target.ChangeMorale(num);
                    }
                }
            }

            // ④ 学得敌将特技（即学即用）
            if (learnFeatureProbability > 0
                && GameRandom.Chance(learnFeatureProbability, 10000))
            {
                TryLearnEnemyFeature(owner, destroyed);
            }
        }

        /// <summary>
        /// 把击破来源归一成"部队"。
        /// </summary>
        /// <param name="atk">事件里的 atk 参数</param>
        /// <returns>部队对象；无法归一时返回 null</returns>
        static Troop ResolveAttacker(SangoObject atk)
        {
            if (atk == null)
                return null;
            if (atk is SkillInstance skill)
                return skill.master;
            return atk as Troop;
        }

        /// <summary>
        /// 按半径收集周围存活的部队（不含自己）。
        /// </summary>
        /// <param name="owner">持技部队</param>
        void CollectRangeTroops(Troop owner)
        {
            targetBuffer.Clear();
            if (owner.cell == null)
                return;

            owner.cell.Spiral(bound, c =>
            {
                if (c == null)
                    return;
                Troop other = c.troop;
                if (other == null || !other.IsAlive || other == owner)
                    return;
                if (!targetBuffer.Contains(other))
                    targetBuffer.Add(other);
            });
        }

        /// <summary>
        /// 从被击破部队的武将身上随机学走一条自己没有的特技。
        ///
        /// 【为什么照抄 AbilityStudySystem 的三步】
        /// 特技列表是 SangoObjectList + 存档用的 Id 数组双份数据，
        /// 只改列表不同步 Id 数组会"当场生效、读档丢失"；
        /// 只改数组不调 InitPersonActions 会"读档才有、当场没效果"。
        /// </summary>
        /// <param name="owner">击破方部队</param>
        /// <param name="destroyed">被击破部队</param>
        void TryLearnEnemyFeature(Troop owner, Troop destroyed)
        {
            Person learner = owner.Leader;
            if (learner == null)
                return;
            if (learner.merit < learnMeritThreshold)
                return;
            // 剧本特技名额已满则不再学（与培育系统同口径）
            if (learner.IsFeatureFull)
                return;

            Feature learned = PickLearnableFeature(learner, destroyed);
            if (learned == null)
                return;

            learner.EnsureFeatureList();
            learner.FeatureList.Add(learned);
            learner.InitPersonActions();
            learner.FeatureListId = learner.FeatureList?.ToArray();
        }

        /// <summary>
        /// 在被击破部队的成员里挑一条学习者还没有的特技。
        /// </summary>
        /// <param name="learner">学习武将</param>
        /// <param name="destroyed">被击破部队</param>
        /// <returns>可学的特技；没有则 null</returns>
        static Feature PickLearnableFeature(Person learner, Troop destroyed)
        {
            Feature result = null;

            destroyed.ForEachPerson(p =>
            {
                if (result != null || p == null || p.FeatureList == null)
                    return;

                for (int i = 0; i < p.ActiveFeatureCount && i < p.FeatureList.Count; i++)
                {
                    Feature feature = p.FeatureList[i];
                    if (feature == null)
                        continue;
                    // 用"查全表"口径去重：特技已在列表里但暂时没解锁时，再 Add 会变成两条同名特技
                    if (learner.HasFeatureById(feature.Id))
                        continue;

                    result = feature;
                    return;
                }
            });

            return result;
        }
    }
}
