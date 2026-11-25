using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 回合结束的破坏工作：本部队**本回合未行动**时，对相邻敌方据点削减耐久 / 治安。
    ///
    /// <para>
    /// 【对应设计条目】破绽（未行动 → 相邻耐久未满的敌据点各 33% 掉 200 耐久，
    /// 最低保留 1 点）、潜入（未行动 → 相邻治安未满的敌据点各 33% 掉 10 治安，最低 0）。
    /// </para>
    /// <para>
    /// 【时机】订阅 <c>OnTurnEnd(Scenario)</c>：这是"一个回合全部结算完"的全局点，
    /// 部队的 <c>ActionOver</c> 已经定型，可以安全判定"本回合是否动过"。
    /// </para>
    /// <para>
    /// 【为什么不复用 TroopTurnRangeEffect】那个是"部队回合开始 + 目标是部队 / 空地"，
    /// 本效果要的是"回合结束 + 目标是敌方据点建筑"，时机和目标都不同。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>probability</term><description>每个目标的命中概率（百分点），默认 33</description></item>
    /// <item><term>bound</term><description>扫描半径（格），默认 1</description></item>
    /// <item><term>durabilityReduce</term><description>耐久削减量（正数），0 = 不做耐久</description></item>
    /// <item><term>securityReduce</term><description>治安削减量（正数），0 = 不做治安；只对城池生效</description></item>
    /// <item><term>onlyUnacted</term><description>要求本部队本回合未行动，默认 true</description></item>
    /// <item><term>maxTargets</term><description>每回合最多命中几个目标，0 = 不限</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class TroopTurnEndSabotage : TroopActionBase
    {
        /// <summary>每个目标的命中概率（百分点）</summary>
        int probability = 33;
        /// <summary>扫描半径</summary>
        int bound = 1;
        /// <summary>耐久削减量</summary>
        int durabilityReduce;
        /// <summary>治安削减量</summary>
        int securityReduce;
        /// <summary>要求本回合未行动</summary>
        bool onlyUnacted = true;
        /// <summary>每回合最多命中目标数（0 = 不限）</summary>
        int maxTargets;

        /// <summary>相邻敌方据点缓冲（复用）</summary>
        readonly List<BuildingBase> targetBuffer = new List<BuildingBase>(8);
        /// <summary>Spiral 回调（缓存，避免每回合新建委托）</summary>
        System.Action<Cell> collectAction;

        /// <summary>
        /// 本回合已结算过的部队 Id。
        ///
        /// 【为什么要全局去重】事件的订阅者是"每个持有者一个 Action 实例"：
        /// 同一支部队里两人持有破绽时，会各自掷一次骰子，等效概率翻倍。
        /// 这里按"回合 + 部队"去重，保证每支部队每回合只结算一次（与"取最大值"口径一致）。
        /// </summary>
        static int lastTurnKey = int.MinValue;
        static readonly HashSet<int> doneTroopIds = new HashSet<int>();

        /// <summary>初始化并订阅回合结束事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            int prob = p.Value<int>("probability");
            probability = prob > 0 ? prob : 33;

            int b = p.Value<int>("bound");
            bound = b > 0 ? b : 1;

            durabilityReduce = p.Value<int>("durabilityReduce");
            securityReduce = p.Value<int>("securityReduce");
            onlyUnacted = p.Value<bool?>("onlyUnacted") ?? true;
            maxTargets = p.Value<int>("maxTargets");

            if (collectAction == null)
                collectAction = CollectCell;

            GameEvent.OnTurnEnd -= OnTurnEnd;
            GameEvent.OnTurnEnd += OnTurnEnd;
        }

        /// <summary>退订事件</summary>
        public override void Clear()
        {
            GameEvent.OnTurnEnd -= OnTurnEnd;
            targetBuffer.Clear();
        }

        /// <summary>
        /// 回合结束结算。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnTurnEnd(Scenario scenario)
        {
            if (Troop == null || !Troop.IsAlive || Troop.cell == null)
                return;
            if (durabilityReduce <= 0 && securityReduce <= 0)
                return;
            // "本回合未行动" —— ActionOver 为 false 说明这支部队还没动过
            if (onlyUnacted && Troop.ActionOver)
                return;

            // 同一支部队每回合只结算一次（同队多人持有不叠加）
            int turnKey = scenario != null ? scenario.TurnCount : 0;
            if (turnKey != lastTurnKey)
            {
                lastTurnKey = turnKey;
                doneTroopIds.Clear();
            }
            if (!doneTroopIds.Add(Troop.Id))
                return;

            targetBuffer.Clear();
            Troop.cell.Spiral(bound, collectAction);
            if (targetBuffer.Count == 0)
                return;

            int hit = 0;
            for (int i = 0; i < targetBuffer.Count; i++)
            {
                if (maxTargets > 0 && hit >= maxTargets)
                    break;

                BuildingBase building = targetBuffer[i];
                if (building == null || !building.IsAlive)
                    continue;
                if (probability < 100 && !GameRandom.Chance(probability))
                    continue;

                bool applied = false;

                // ① 耐久削减
                //    · 只打"耐久未满"的据点（设计原文：相邻耐久未满的敌据点）
                //    · 最低保留 1 点 —— 打到 0 是"攻陷"，会触发 OnFall / 城陷，
                //      那不是破坏工作该做的事
                if (durabilityReduce > 0 && building.durability > 1
                    && building.durability < building.DurabilityLimit)
                {
                    int dmg = System.Math.Min(durabilityReduce, building.durability - 1);
                    if (dmg > 0)
                    {
                        building.ChangeDurability(-dmg, Troop);
                        Sango.Log.Info($"@特技@[{Troop.Name}]对[{building.Name}]造成 {dmg} 点耐久破坏");
                        applied = true;
                    }
                }

                // ② 治安削减（只对城池；"治安未满"才生效，最低 0）
                if (securityReduce > 0 && building is City city
                    && city.security > 0 && city.security < 100)
                {
                    int cut = System.Math.Min(securityReduce, city.security);
                    if (cut > 0)
                    {
                        city.AddSecurity(-cut);
                        Sango.Log.Info($"@特技@[{Troop.Name}]使[{city.Name}]治安 -{cut}");
                        applied = true;
                    }
                }

                if (applied)
                    hit++;
            }
        }

        /// <summary>Spiral 回调：收集范围内的敌方据点</summary>
        /// <param name="cell">当前格</param>
        void CollectCell(Cell cell)
        {
            if (cell == null || cell.building == null)
                return;

            BuildingBase building = cell.building;
            if (!building.IsAlive)
                return;
            if (Troop != null && Troop.BelongForce != null
                && ReferenceEquals(building.BelongForce, Troop.BelongForce))
                return;

            targetBuffer.Add(building);
        }
    }
}
