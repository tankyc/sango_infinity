/*
 * 文件名：TroopTelemetry.cs
 * 描述：玩家部队出城时的组队信息采集（主将 + 副将 + 特技 + 兵种 + 城池）。
 *
 * 【为什么需要它】
 *   玩家反馈"这队打不过""这队怎么带错了"，光看截图分不清是谁带的队、
 *   带的什么兵种、有没有搬运/连击之类的特技。这里在部队真正离开城池的那一帧
 *   抓一份快照送到后台，排障时可以直接按玩家名 / 城池 / 主将检索。
 *
 * 【挂在哪一步】
 *   原版"出城"没有单独函数，本项目对应的是"部队离开出兵城的中心格"：
 *   City.EnsureTroop 会先把部队临时放到 CenterCell（City.cs:5710-5713），
 *   之后部队第一次移动触发 Troop.UpdateCell → GameEvent.OnTroopLeaveCell
 *   （Troop.cs:2509-2513）。此时 Init 已经跑完（Name、TroopType、士气都已就绪），
 *   比订阅 OnTroopCreated 更准确 —— 后者在 scenario.Add 里触发，早于 troop.Init，
 *   troop.Name 还是空串。
 *
 * 【只报一次】
 *   部队在城格与邻格之间来回移动会反复触发 LeaveCell，所以按 troopId 做
 *   DedupeWindow 秒的节流；窗口外的再次出城（回城补给后又出发）仍然会上报。
 *
 * 【不报的情况】
 *   1. 非玩家势力（AI 部队量大，不收）；
 *   2. 不是在出兵城中心格出发（中途路过别的城不重复记）；
 *   3. 未配置上报地址 / 口令，或 EnableTeamReport 关闭。
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core.Telemetry
{
    /// <summary>武将快照（服务端 mapPerson 一一对应，字段名不可改）</summary>
    [Serializable]
    public class TeamPerson
    {
        /// <summary>武将 Id</summary>
        public int id;

        /// <summary>武将姓名（已去掉富文本标签）</summary>
        public string name;

        /// <summary>特技列表</summary>
        public List<TeamFeature> features = new List<TeamFeature>();
    }

    /// <summary>特技快照</summary>
    [Serializable]
    public class TeamFeature
    {
        /// <summary>特技 Id</summary>
        public int id;

        /// <summary>特技名（已去掉 &lt;color&gt; 标签）</summary>
        public string name;
    }

    /// <summary>
    /// 一条组队记录。字段名与 PlayerInsight/server/store.js 的 TeamStore.normalize 一一对应，
    /// 改字段时两边必须同步，否则后台会落到默认值。
    /// </summary>
    [Serializable]
    public class TeamRecord
    {
        /// <summary>发生时间（Unix 毫秒）</summary>
        public long occurredAt;

        public string appVersion;
        public string platform;
        public string deviceId;
        public string deviceModel;
        public string osVersion;
        public string playerId;
        public string playerName;
        public string scenarioName;

        /// <summary>势力 Id</summary>
        public int forceId;

        /// <summary>势力名</summary>
        public string forceName;

        /// <summary>军团名（原名带富文本，已剥离）</summary>
        public string corpsName;

        /// <summary>出兵城池 Id</summary>
        public int cityId;

        /// <summary>出兵城池名</summary>
        public string cityName;

        /// <summary>部队 Id</summary>
        public int troopId;

        /// <summary>部队名（通常等于主将名）</summary>
        public string troopName;

        /// <summary>当前兵种 Id（在水上时是水军，Troop.cs:739-743）</summary>
        public int troopTypeId;

        /// <summary>当前兵种名</summary>
        public string troopTypeName;

        /// <summary>地面部队类型 Id（陆上兵种；出城时即使在水上也照记，Troop.cs:773-788）</summary>
        public int landTroopTypeId;

        /// <summary>地面部队类型名</summary>
        public string landTroopTypeName;

        /// <summary>主将</summary>
        public TeamPerson leader;

        /// <summary>副将（最多两人）</summary>
        public List<TeamPerson> members = new List<TeamPerson>();
    }

    /// <summary>
    /// 组队信息采集器。对外不需要调用任何方法，Bootstrap 会自动订阅事件。
    /// </summary>
    public static class TroopTelemetry
    {
        /// <summary>同一支部队的上报节流表：troopId → 上次上报时间（Time.realtimeSinceStartup）</summary>
        private static readonly Dictionary<int, float> LastReportAt = new Dictionary<int, float>();

        /// <summary>
        /// 自动初始化。AfterSceneLoad 与 BugReporter 保持一致，
        /// 保证 Scenario.Cur 之类依赖已就绪，且不会在主菜单阶段瞎跑。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            GameEvent.OnTroopLeaveCell -= OnTroopLeaveCell;
            GameEvent.OnTroopLeaveCell += OnTroopLeaveCell;
        }

        /// <summary>
        /// 部队离开一格。只有"离开出兵城中心格"才算一次出城。
        /// </summary>
        /// <param name="troop">移动中的部队</param>
        /// <param name="lastCell">离开的格子</param>
        /// <param name="destCell">目标格子</param>
        private static void OnTroopLeaveCell(Troop troop, Cell lastCell, Cell destCell)
        {
            if (!TelemetryConfig.EnableTeamReport) return;
            if (!TelemetryConfig.IsConfigured) return;
            if (troop == null || lastCell == null) return;

            try
            {
                Scenario scenario = Scenario.Cur;
                if (scenario == null) return;

                // 必须是"从出兵城的中心格离开"，途中路过别的城不算。
                // 这一条放在最前面：格子级事件的调用量极大（每支部队每走一格都来一次），
                // 绝大多数情况在这里就被挡掉，后面的取势力/取上下文都不执行
                City city = troop.BelongCity;
                if (city == null || city.CenterCell != lastCell) return;

                // 只收玩家势力的部队：AI 部队每回合都在出城，收了会把后台埋掉。
                // 判定用 Force.IsPlayer（剧本 playerForceList 设定，Scenario.cs:893-906），
                // 不能用 CurRunForce —— 那是"当前行动势力"，玩家回合里若有 AI 盟军行动就会误判
                Force force = troop.BelongForce;
                if (force == null || !force.IsPlayer) return;

                if (!PassThrottle(troop.Id)) return;

                TeamRecord record = BuildRecord(troop, force, city, scenario);
                if (record == null) return;

                TelemetryRunner runner = TelemetryRunner.Ensure();
                if (runner == null) return;

                runner.Run(Send(record));
            }
            catch (Exception e)
            {
                // 采集失败绝不影响游戏主流程，只留一条警告
                Sango.Log.Warning($"[Telemetry] 组队信息采集失败：{e.Message}");
            }
        }

        /// <summary>
        /// 同一支部队的节流：窗口内不重复上报。
        /// 用 DedupeWindow 而不是另开配置，是因为两者语义相同（同一现象短时间内重复）。
        /// </summary>
        /// <param name="troopId">部队 Id</param>
        /// <returns>true 表示允许上报</returns>
        private static bool PassThrottle(int troopId)
        {
            float now = Time.realtimeSinceStartup;

            float last;
            if (LastReportAt.TryGetValue(troopId, out last) && now - last < TelemetryConfig.DedupeWindow)
            {
                return false;
            }

            LastReportAt[troopId] = now;

            // 部队是会反复创建的（打完就解散），表不能无限长；超过队列量级直接清空重来
            if (LastReportAt.Count > TelemetryConfig.MaxQueue)
            {
                LastReportAt.Clear();
                LastReportAt[troopId] = now;
            }

            return true;
        }

        /// <summary>组装一条组队记录</summary>
        private static TeamRecord BuildRecord(Troop troop, Force force, City city, Scenario scenario)
        {
            Person leader = troop.Leader;

            TeamRecord record = new TeamRecord
            {
                occurredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                appVersion = Application.version,
                platform = Application.platform.ToString(),
                deviceId = BugReporter.SafeDeviceId(),
                deviceModel = SystemInfo.deviceModel,
                osVersion = SystemInfo.operatingSystem,
                playerName = BugReporter.PlayerNameOf(scenario),
                scenarioName = scenario.Name ?? string.Empty,
                forceId = force.Id,
                forceName = Clean(force.Name),
                corpsName = Clean(troop.BelongCorps != null ? troop.BelongCorps.Name : string.Empty),
                cityId = city.Id,
                cityName = Clean(city.Name),
                troopId = troop.Id,
                // troop.Name 在 Init 里才赋值，个别创建路径可能还没跑，兜底用主将名
                troopName = Clean(string.IsNullOrEmpty(troop.Name) && leader != null ? leader.Name : troop.Name),
                leader = MapPerson(leader),
                members = new List<TeamPerson>(),
            };

            record.playerId = record.deviceId;

            // 兵种：IsInWater 依赖当前格的地形，理论上出城时一定已就绪，仍做判空
            TroopType troopType = troop.TroopType;
            if (troopType != null)
            {
                record.troopTypeId = troopType.Id;
                record.troopTypeName = Clean(troopType.Name);
            }

            // 地面部队类型：这是"这名武将陆上带什么兵"，与上面当前兵种分开记。
            // 出城瞬间若正好在水上（港口出兵），当前兵种会是水军，只看它就分不清陆上编制
            record.landTroopTypeId = troop.LandTroopTypeId;
            TroopType landTroopType = troop.LandTroopType;
            record.landTroopTypeName = landTroopType != null ? Clean(landTroopType.Name) : string.Empty;

            if (troop.Member1 != null) record.members.Add(MapPerson(troop.Member1));
            if (troop.Member2 != null) record.members.Add(MapPerson(troop.Member2));

            return record;
        }

        /// <summary>武将 → 快照（含特技）</summary>
        private static TeamPerson MapPerson(Person person)
        {
            if (person == null) return null;

            TeamPerson snapshot = new TeamPerson
            {
                id = person.Id,
                name = Clean(person.Name),
            };

            // Feature.Name 在特技未激活时会带 <color=#6D6D6D> 标签，必须剥掉
            if (person.FeatureList != null)
            {
                for (int i = 0; i < person.FeatureList.Count; i++)
                {
                    Feature feature = person.FeatureList[i];
                    if (feature == null) continue;

                    snapshot.features.Add(new TeamFeature
                    {
                        id = feature.Id,
                        name = Clean(feature.Name),
                    });
                }
            }

            return snapshot;
        }

        /// <summary>去掉富文本标签（军团名、特技名都带颜色）</summary>
        private static string Clean(string text)
        {
            return BugReporter.StripRichText(text);
        }

        /// <summary>
        /// 发送一条记录。失败重试一次：组队信息是稀疏事件（每回合几支），
        /// 不值得为它建队列，但也不该因为一次抖动就丢掉。
        /// </summary>
        private static IEnumerator Send(TeamRecord record)
        {
            string json = JsonUtility.ToJson(record);

            for (int attempt = 0; attempt < 2; attempt++)
            {
                bool ok = false;
                yield return TelemetrySender.Post("/teams", json, result => ok = result);

                if (ok) yield break;
                if (attempt == 0) yield return new WaitForSecondsRealtime(3f);
            }
        }
    }
}
