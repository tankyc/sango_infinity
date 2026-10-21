/*
 * 文件名：EventEffectsTroopCreate.cs
 * 描述：部队编成类事件效果——创建、解散、瞬移、换兵种
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【本文件依据的引擎入口（全部为已有公共方法，未新增引擎逻辑）】
 *   · City.EnsureTroop(troop, scenario) —— 设 cell → cell.troop → Scenario.Add → troop.Init
 *     （Init 内部负责：武将 mBelongTroop 回填、InitActionList、CalculateAttribute、
 *       加入 allTroops / allAttackTroops、new TroopRender、buffManager.Init、抛 OnTroopEnterCell）
 *   · Troop.UpdateCell(dest, last, isEndMove) —— 移动结束收尾：换格、清旧格、抛进出格事件、刷渲染
 *   · Troop.Clear() —— 完整解散：退名单、移场景、释放俘虏、清 actionList / 战法 / buff、清格子、抛 OnTroopClear（幂等）
 *   · Troop.ResetActionAndStatus() —— 换兵种后重算战法与属性
 *
 * 【为什么创建必须"先入城再出城"】
 *   项目硬规则：部队不允许停留在城市格。而 EnsureTroop 只能把部队暂时放在城市中心格
 *   （Init 里要用 BelongCity 建立名单与属性）。因此创建流程固定为：
 *   EnsureTroop（借用城池完成初始化）→ 立刻 UpdateCell 移到指定的城外格；
 *   若目标格不可停留，则 Clear() 回滚，绝不留一支部队杵在城中心。
 */

namespace Sango.Core.Event
{
    /// <summary>
    /// 创建一支部队。
    ///
    /// 参数：CitySlot（出兵城，必需）、PersonSlot（主将，必需）、Member1Slot / Member2Slot（可选）、
    ///       X / Y（目标格坐标，必需）、Troops（可选）、LandTroopTypeId / WaterTroopTypeId（可选）。
    ///
    /// 【不扣城池兵力】这是"剧情部队"——凭空出现的那种。要模拟真实出兵请另配 CityAddTroops 负数。
    /// 【兵种缺省】不填时取 Id = 1 的兵种（引擎里的默认兵种，CityExpedition 的兜底分支同样用它）。
    /// </summary>
    [EventEffectMeta("创建部队", "在指定坐标创建一支部队（先入城初始化再移出，绝不留在城市格）", "CitySlot", "PersonSlot", "X", "Y", "Troops")]
    public class TroopCreateEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return false;

            City city = GetSlot<City>("CitySlot", context);
            Person leader = GetSlot<Person>("PersonSlot", context);
            if (city == null)
            {
                WarnSlotMissing("CitySlot", GetString("CitySlot"));
                return false;
            }
            if (leader == null)
            {
                WarnSlotMissing("PersonSlot", GetString("PersonSlot"));
                return false;
            }

            int x = EvalInt("X", context);
            int y = EvalInt("Y", context);
            Cell target = scenario.Map.GetCell(x, y);
            if (target == null)
            {
                Log.Warning($"事件：创建部队失败 —— 坐标 ({x},{y}) 不在地图内");
                return false;
            }

            // 兵种：优先用参数指定，缺省取引擎默认兵种
            int landId = EvalInt("LandTroopTypeId", context, 0);
            if (landId <= 0) landId = 1;
            TroopType land = scenario.GetObject<TroopType>(landId);
            if (land == null)
            {
                Log.Warning($"事件：创建部队失败 —— 找不到兵种 Id={landId}");
                return false;
            }

            int waterId = EvalInt("WaterTroopTypeId", context, 0);
            TroopType water = waterId > 0 ? scenario.GetObject<TroopType>(waterId) : land;
            if (water == null) water = land;

            int troops = EvalInt("Troops", context, 0);
            if (troops <= 0)
            {
                // 缺省取"城里现有兵力"，但要有下限，避免创建出 0 兵部队（引擎多处会按 0 兵判负）
                troops = city.troops > 0 ? city.troops : 1;
            }

            Troop troop = new Troop();
            troop.Leader = leader;
            troop.Member1 = GetSlot<Person>("Member1Slot", context);
            troop.Member2 = GetSlot<Person>("Member2Slot", context);
            troop.LandTroopType = land;
            troop.WaterTroopType = water;
            troop.troops = troops;
            troop.morale = city.morale;
            troop.energy = city.energy;
            troop.IsAlive = true;
            troop.ActionOver = false;

            // 入城完成初始化（Render / 战法 / 名单 / 属性都在 Init 里做）
            city.EnsureTroop(troop, scenario);

            // 编制上限要等 Init 里的 CalculateAttribute 之后才有效
            if (troop.MaxTroops > 0 && troop.troops > troop.MaxTroops)
                troop.troops = troop.MaxTroops;

            // 立刻移出城市格：目标格必须可停留（CanStay 会校验 空格 + 无建筑 + 地形可走）
            if (!target.CanStay(troop))
            {
                Log.Warning($"事件：创建部队失败 —— 坐标 ({x},{y}) 不可停留（有部队/建筑，或该兵种无法通行），已回滚");
                troop.Clear();
                return false;
            }

            troop.UpdateCell(target, city.CenterCell, true);

            Log.Info($"事件：{city.Name} 派出部队「{troop.Name}」（{troop.troops} 兵）至 ({x},{y})");
            return true;
        }
    }

    /// <summary>
    /// 解散一支部队。
    /// 走 Troop.Clear()：释放俘虏、清战法与 buff、摘出城池名单、移出场景、清格子、抛 OnTroopClear。
    /// 该方法本身幂等（isCleared 保护），重复调用不会出问题。
    /// </summary>
    [EventEffectMeta("解散部队", "立即解散指定部队（走引擎完整收尾流程，含释放俘虏）", "TroopSlot")]
    public class TroopDisbandEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            string name = troop.Name;
            troop.Clear();
            Log.Info($"事件：部队「{name}」已解散");
            return true;
        }
    }

    /// <summary>
    /// 把部队瞬移到指定坐标。
    /// 用 UpdateCell 做"移动结束"的收尾：换格、清旧格、抛进出格事件、刷渲染，
    /// 不检查路径与移动力（剧情调度、奇袭、伏兵出场都用这个）。
    /// </summary>
    [EventEffectMeta("部队瞬移", "把部队直接移动到指定坐标（不检查路径与移动力）", "TroopSlot", "X", "Y")]
    public class TroopTeleportEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Map == null) return false;

            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int x = EvalInt("X", context);
            int y = EvalInt("Y", context);
            Cell target = scenario.Map.GetCell(x, y);
            if (target == null)
            {
                Log.Warning($"事件：部队瞬移失败 —— 坐标 ({x},{y}) 不在地图内");
                return false;
            }

            Cell last = troop.cell;
            if (last == target)
            {
                Log.Info($"事件：部队「{troop.Name}」已在 ({x},{y})，瞬移跳过");
                return true;
            }

            if (!target.CanStay(troop))
            {
                Log.Warning($"事件：部队瞬移失败 —— 目标 ({x},{y}) 不可停留（有部队/建筑，或该兵种无法通行）");
                return false;
            }

            troop.UpdateCell(target, last, true);
            Log.Info($"事件：部队「{troop.Name}」瞬移至 ({x},{y})");
            return true;
        }
    }

    /// <summary>
    /// 更换部队兵种并重算。
    ///
    /// 【为什么必须调 ResetActionAndStatus】兵种决定战法表与大量地形/攻防加成，
    /// 只改 LandTroopType / WaterTroopType 会让部队"长得像新兵种、打起来还是旧兵种"。
    /// ResetActionAndStatus 会重新 InitActionList 并 CalculateAttribute，是引擎既有的重算入口。
    ///
    /// 【已知限制】进城/出城的占位名单（allAttackTroops）以"是否战斗兵种"为条件，
    /// 从战斗兵种换成非战斗兵种时该名单不会自动摘除；本效果会在换完后做一次校正。
    /// </summary>
    [EventEffectMeta("更换兵种", "更换部队的陆上/水上兵种并重算战法与属性", "TroopSlot", "LandTroopTypeId", "WaterTroopTypeId")]
    public class TroopChangeTypeEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null) return false;

            Troop troop = GetSlot<Troop>("TroopSlot", context);
            if (troop == null)
            {
                WarnSlotMissing("TroopSlot", GetString("TroopSlot"));
                return false;
            }

            int landId = EvalInt("LandTroopTypeId", context, 0);
            int waterId = EvalInt("WaterTroopTypeId", context, 0);
            if (landId <= 0 && waterId <= 0)
            {
                Log.Warning("事件：更换兵种失败 —— LandTroopTypeId 与 WaterTroopTypeId 均未指定");
                return false;
            }

            TroopType land = landId > 0 ? scenario.GetObject<TroopType>(landId) : null;
            TroopType water = waterId > 0 ? scenario.GetObject<TroopType>(waterId) : null;
            if ((landId > 0 && land == null) || (waterId > 0 && water == null))
            {
                Log.Warning($"事件：更换兵种失败 —— 兵种 Id 不存在（land={landId}, water={waterId}）");
                return false;
            }

            string oldName = troop.TroopType != null ? troop.TroopType.Name : "无";
            if (land != null) troop.LandTroopType = land;
            if (water != null) troop.WaterTroopType = water;

            // 重算战法与属性（引擎既有入口）
            troop.ResetActionAndStatus();

            // 占位名单校正：非战斗兵种不应留在 allAttackTroops 里
            City city = troop.BelongCity;
            if (city != null && city.allAttackTroops != null)
            {
                bool isFight = troop.LandTroopType != null && troop.LandTroopType.isFight && troop.LandTroopType.Id != 1;
                bool inList = city.allAttackTroops.Contains(troop);
                if (isFight && !inList) city.allAttackTroops.Add(troop);
                else if (!isFight && inList) city.allAttackTroops.Remove(troop);
            }

            Log.Info($"事件：部队「{troop.Name}」兵种 {oldName} → {(troop.TroopType != null ? troop.TroopType.Name : "无")}");
            return true;
        }
    }

    /// <summary>部队编成类效果的批量注册入口</summary>
    public static class EventEffectsTroopCreate
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("TroopCreate", EventEffectBase.CreateHandle<TroopCreateEffect>);
            EventEffectBase.Register("TroopDisband", EventEffectBase.CreateHandle<TroopDisbandEffect>);
            EventEffectBase.Register("TroopTeleport", EventEffectBase.CreateHandle<TroopTeleportEffect>);
            EventEffectBase.Register("TroopChangeType", EventEffectBase.CreateHandle<TroopChangeTypeEffect>);
        }
    }
}
