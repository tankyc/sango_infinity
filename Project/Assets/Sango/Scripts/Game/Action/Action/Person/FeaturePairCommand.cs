using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 成对 / 组合特技的可注册指令（连舟寨、冀雄、车下士这类）。
    ///
    /// <para>
    /// 数据来自 <see cref="ScenarioCommonData.FeaturePairs"/>（pairId 引用），
    /// 本 Action 只负责：UI 菜单注册 → 条件校验 → 原子结算。
    /// </para>
    /// <para>
    /// 【性能】只在部队交互菜单弹出时判定一次；候选列表用静态缓冲复用，
    /// 不做全剧本遍历，也不在战斗/寻路热路径上。
    /// </para>
    /// </summary>
    public class FeaturePairCommand : ActionBase
    {
        /// <summary>引用的成对配置 Id</summary>
        int pairId;

        /// <summary>装配宿主武将（特技持有者）</summary>
        Person ownerPerson;

        /// <summary>配置缓存（剧本公共数据，只读）</summary>
        FeaturePairConfig config;

        /// <summary>配置缺失是否已报过（避免每次弹菜单刷屏）</summary>
        bool configMissingReported;

        /// <summary>当前菜单上下文</summary>
        Troop currentTroop;
        Cell currentCell;
        Person currentA;
        Person currentB;

        /// <summary>候选武将缓冲（复用，避免每次弹菜单分配）</summary>
        static readonly List<Person> candidateBuffer = new List<Person>(16);

        /// <summary>跨队候选部队缓冲（复用）</summary>
        static readonly List<Troop> nearbyTroopBuffer = new List<Troop>(8);

        /// <summary>跨队收集上下文（静态回调用，避免闭包分配）</summary>
        static Troop pairOwnerTroop;
        static FeaturePairConfig pairCollectConfig;

        /// <summary>跨队收集回调（静态复用）</summary>
        static readonly System.Action<Troop> collectNearbyTroopAction = CollectNearbyTroop;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            pairId = p.Value<int>("pairId");
            if (pairId <= 0)
                Sango.Log.Error("[成对特技] FeaturePairCommand 未配置 pairId，指令不会生效");

            for (int i = 0; i < sangoObjects.Length; i++)
            {
                if (sangoObjects[i] is Person person)
                {
                    ownerPerson = person;
                    break;
                }
            }

            GameEvent.OnTroopInteractiveContextDialogShow += OnDialogShow;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopInteractiveContextDialogShow -= OnDialogShow;
        }

        /// <summary>事件驱动效果，不需要外部主动触发。</summary>
        public override void Execute(Trigger trigger)
        {
        }

        FeaturePairConfig GetConfig()
        {
            if (config != null)
                return config;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.FeaturePairs == null)
                return null;

            config = scenario.CommonData.FeaturePairs.Get(pairId);
            if (config == null && !configMissingReported)
            {
                configMissingReported = true;
                Sango.Log.Error($"[成对特技] FeaturePairCommand 找不到 pairId={pairId} 的配置，指令不会生效");
            }
            return config;
        }

        /// <summary>部队交互菜单弹出：注册组合指令</summary>
        void OnDialogShow(ITroopInteractiveDialogData dialogData, Troop troop, Cell actionCell)
        {
            if (dialogData == null || troop == null || troop.BelongForce == null
                || !troop.BelongForce.IsPlayer
                || troop.BelongForce != Scenario.Cur.CurRunForce)
                return;

            FeaturePairConfig cfg = GetConfig();
            if (cfg == null)
                return;

            Person a = ownerPerson != null && troop.HasPerson(ownerPerson)
                ? ownerPerson
                : FindFeatureHolder(troop, cfg.featureId);
            if (a == null)
                return;

            Person b = FindPartner(troop, cfg, a);
            bool valid = b != null && CheckResources(troop, cfg, a, b, actionCell);

            if (valid)
            {
                currentTroop = troop;
                currentCell = actionCell;
                currentA = a;
                currentB = b;

                dialogData.SetContent(cfg.Name);
                dialogData.SetValid(true);
                dialogData.SetSureAction(OnSure);
                return;
            }

            // 条件不满足：显示选项与原因，确认按钮不响应（可见但置灰）
            currentTroop = null;
            currentCell = null;
            currentA = null;
            currentB = null;

            string reason = b == null ? "缺少配合武将" : "条件或资源不足";
            dialogData.SetContent($"{cfg.Name}（{reason}）");
            dialogData.SetValid(false);
            dialogData.SetSureAction(null);
        }

        /// <summary>确认执行：再次校验后原子结算</summary>
        void OnSure()
        {
            if (currentTroop == null || currentA == null || currentB == null)
                return;

            FeaturePairConfig cfg = GetConfig();
            if (cfg == null || !CheckResources(currentTroop, cfg, currentA, currentB, currentCell))
                return;

            ExecutePair(cfg, currentTroop, currentA, currentB);
        }

        Person FindFeatureHolder(Troop troop, int featureId)
        {
            if (featureId <= 0)
                return null;

            if (troop.Leader != null && troop.Leader.HasFeature(featureId))
                return troop.Leader;
            if (troop.Member1 != null && troop.Member1.HasFeature(featureId))
                return troop.Member1;
            if (troop.Member2 != null && troop.Member2.HasFeature(featureId))
                return troop.Member2;
            return null;
        }

        Person FindPartner(Troop troop, FeaturePairConfig cfg, Person a)
        {
            // ① 本队
            candidateBuffer.Clear();
            if (troop.Leader != null) candidateBuffer.Add(troop.Leader);
            if (troop.Member1 != null) candidateBuffer.Add(troop.Member1);
            if (troop.Member2 != null) candidateBuffer.Add(troop.Member2);

            Person b = FeaturePairSystem.FindPartner(cfg, a, candidateBuffer);
            if (b != null)
            {
                candidateBuffer.Clear();
                return b;
            }

            // ② 跨队：仅当配置允许且限定距离；先按势力 + 距离过滤部队，再收集武将
            if (cfg.requireSameTroop == 0 && cfg.maxDistance > 0
                && Scenario.Cur != null && Scenario.Cur.troopsSet != null)
            {
                nearbyTroopBuffer.Clear();
                pairOwnerTroop = troop;
                pairCollectConfig = cfg;
                Scenario.Cur.troopsSet.ForEach(collectNearbyTroopAction);
                pairOwnerTroop = null;
                pairCollectConfig = null;

                candidateBuffer.Clear();
                for (int i = 0; i < nearbyTroopBuffer.Count; i++)
                {
                    Troop other = nearbyTroopBuffer[i];
                    if (other.Leader != null) candidateBuffer.Add(other.Leader);
                    if (other.Member1 != null) candidateBuffer.Add(other.Member1);
                    if (other.Member2 != null) candidateBuffer.Add(other.Member2);
                }

                b = FeaturePairSystem.FindPartner(cfg, a, candidateBuffer);
                nearbyTroopBuffer.Clear();
            }

            candidateBuffer.Clear();
            return b;
        }

        /// <summary>跨队收集：同势力 + 距离内的存活部队</summary>
        static void CollectNearbyTroop(Troop other)
        {
            if (other == null || other == pairOwnerTroop || !other.IsAlive)
                return;
            if (other.BelongForce == null || other.BelongForce != pairOwnerTroop.BelongForce)
                return;
            if (other.cell == null || pairOwnerTroop.cell == null)
                return;
            if (pairOwnerTroop.cell.Distance(other.cell) > pairCollectConfig.maxDistance)
                return;

            nearbyTroopBuffer.Add(other);
        }

        /// <summary>原子校验：任一条件不满足则不扣任何资源</summary>
        bool CheckResources(Troop troop, FeaturePairConfig cfg, Person a, Person b, Cell actionCell)
        {
            JObject p = cfg.commandParams;
            if (p == null)
                return true;

            Troop ta = a.mBelongTroop;
            Troop tb = b.mBelongTroop;
            if (ta == null || tb == null || ta.cell == null || tb.cell == null)
                return false;

            if (p.Value<bool>("requireWater") && !ta.IsInWater && !tb.IsInWater)
                return false;

            if (p.Value<bool>("requireLine") && !SameLine(ta.cell, tb.cell))
                return false;

            City city = a.BelongCity != null ? a.BelongCity : troop.BelongCity;
            if (city == null)
                return false;

            int troopsCost = p.Value<int>("troopsCost");
            int goldCost = p.Value<int>("goldCost");
            if (troopsCost > 0 && city.troops < troopsCost)
                return false;
            if (goldCost > 0 && city.gold < goldCost)
                return false;

            return true;
        }

        /// <summary>执行组合效果（调用前必须已通过 CheckResources）</summary>
        void ExecutePair(FeaturePairConfig cfg, Troop troop, Person a, Person b)
        {
            JObject p = cfg.commandParams;
            if (p == null)
                return;

            City city = a.BelongCity != null ? a.BelongCity : troop.BelongCity;
            if (city == null)
                return;

            int troopsCost = p.Value<int>("troopsCost");
            int goldCost = p.Value<int>("goldCost");
            if (troopsCost > 0)
                city.troops -= troopsCost;
            if (goldCost > 0)
                city.AddGold(-goldCost);

            Troop ta = a.mBelongTroop;
            Troop tb = b.mBelongTroop;

            int morale = p.Value<int>("morale");
            if (morale != 0)
            {
                ta?.ChangeMorale(morale);
                tb?.ChangeMorale(morale);
            }

            int[] buffIds = p.Value<JArray>("buffIds")?.ToObject<int[]>();
            int buffTurns = p.Value<int>("buffTurns");
            if (buffTurns <= 0) buffTurns = 1;
            if (buffIds != null && buffIds.Length > 0)
            {
                int buffId = buffIds[GameRandom.Range(0, buffIds.Length)];
                if (tb != null) tb.AddBuff(buffId, buffTurns, ta);
            }
        }

        static bool SameLine(Cell a, Cell b)
        {
            return a.x == b.x || a.y == b.y;
        }
    }
}
