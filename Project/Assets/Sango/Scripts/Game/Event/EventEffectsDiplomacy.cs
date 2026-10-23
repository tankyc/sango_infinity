/*
 * 文件名：EventEffectsDiplomacy.cs
 * 描述：外交与技巧类事件效果——结盟、停战、通商、破盟、授予技巧、完成研究
 * 创建日期：2026-10-19
 * 最后修改：2026-10-19
 *
 * 【为什么这一批需要先补引擎封装】
 *   同盟不是"给势力加个标记"，它是一张有生命周期的对象：
 *   Alliance 自身持有 ForceList / leftCount / allianceType，
 *   并且必须同时登记到 Scenario.allianceSet 与双方的 Force.AllianceList 上，
 *   到期时由 Alliance.OnTurnStart 逐方摘除。任何一处漏登记，
 *   表现都是"看起来结盟了，但 AI 不认、到期也不清理"。
 *   本文件按 DiplomacyActionAlliance.Perform 的既有顺序逐步复制，不做简化。
 *
 * 【技巧授予同理】
 *   走新增的 Force.GainTechnique，由它负责同步 Techniques_list、
 *   注册被动 action、重建科技树与可建建筑表（详见该方法的注释）。
 *
 * 【明确不支持】
 *   · ForceRemoveTechnique：技巧的被动 action 一旦注册进 actionList 就无法精确撤回
 *     （InitActions 可能注册多条），强行移除会留下"数据没了但效果还在"的假象，
 *     因此不提供该效果，需要时请调整数据而不是在剧本里删科技。
 *   · 势力灭亡：涉及城池移交、武将下野、军团回收，必须走引擎既有灭亡流程。
 */

namespace Sango.Core.Event
{
    /// <summary>
    /// 建立两势力之间的盟约（同盟 / 停战 / 通商）。
    /// 严格复制 DiplomacyActionAlliance 的登记顺序：建对象 → 进场景 → 挂双方列表 → 调关系值。
    /// </summary>
    [EventEffectMeta("建立盟约", "让两个势力结盟/停战/通商；Type 取 Alliance/Truce/Trade", "ForceSlot", "OtherSlot", "Type", "Duration")]
    public class ForceSetDiplomacyEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force a = GetSlot<Force>("ForceSlot", context);
            Force b = GetSlot<Force>("OtherSlot", context);

            if (a == null || b == null || a == b)
            {
                Log.Warning("事件：建立盟约失败 —— ForceSlot / OtherSlot 未绑定或指向同一势力");
                return false;
            }

            AllianceType type;
            string typeText = GetString("Type", "Alliance");
            switch ((typeText ?? "Alliance").ToLowerInvariant())
            {
                case "alliance": type = AllianceType.Alliance; break;
                case "truce": type = AllianceType.Truce; break;
                // "trade" 已删除：原版没有通商命令，盟约只有同盟与停战
                default:
                    Log.Warning($"事件：建立盟约失败 —— 未知的 Type={typeText}（可用 Alliance/Truce）");
                    return false;
            }

            // 默认时长取剧本配置（同盟 12 个月），避免在事件里写死
            int duration = EvalInt("Duration", context, 0);
            if (duration <= 0 && Scenario.Cur != null && Scenario.Cur.Variables != null)
                duration = Scenario.Cur.Variables.diplomacyAllianceDuration;
            if (duration <= 0) duration = 12;

            // 同类型盟约已存在时不重复创建，否则双方列表里会留下两张含义相同的盟约
            if (FindAlliance(a, b, type) != null)
            {
                Log.Info($"事件：{a.Name} 与 {b.Name} 已存在同类盟约，本次跳过");
                return true;
            }

            Alliance alliance = new Alliance()
            {
                ForceList = new SangoObjectList<Force>(),
                leftCount = duration,
                allianceType = type,
                IsAlive = true
            };
            alliance.ForceList.Add(a);
            alliance.ForceList.Add(b);

            // ① 进场景（分配 Id，随存档序列化）
            Scenario.Cur.Add(alliance);

            // ② 挂到双方列表（AI 判定与到期摘除都读这里）
            a.AllianceList.Add(alliance);
            b.AllianceList.Add(alliance);

            // ③ 关系值：与原外交行为保持同一口径
            DiplomacyManager diplomacy = GameSystem.GetSystem<DiplomacyManager>();
            if (diplomacy != null && Scenario.Cur != null && Scenario.Cur.Variables != null)
            {
                int increase = type == AllianceType.Alliance
                    ? Scenario.Cur.Variables.diplomacyAllianceRelationIncrease
                    : 0;
                if (increase != 0) diplomacy.AddRelation(a, b, increase);
            }

            Log.Info($"事件：{a.Name} 与 {b.Name} 建立{TypeName(type)}（{duration} 个月）");
            return true;
        }

        /// <summary>盟约类型的中文名</summary>
        /// <param name="type">类型</param>
        /// <returns>中文名</returns>
        internal static string TypeName(AllianceType type)
        {
            switch (type)
            {
                case AllianceType.Alliance: return "同盟";
                case AllianceType.Truce: return "停战";
                default: return "盟约";
            }
        }

        /// <summary>查找两势力之间指定类型的盟约</summary>
        /// <param name="a">势力甲</param>
        /// <param name="b">势力乙</param>
        /// <param name="type">类型</param>
        /// <returns>盟约；不存在返回 null</returns>
        internal static Alliance FindAlliance(Force a, Force b, AllianceType type)
        {
            if (a == null || b == null || a.AllianceList == null) return null;

            for (int i = 0; i < a.AllianceList.Count; i++)
            {
                Alliance alliance = a.AllianceList.Get(i);
                if (alliance == null || !alliance.IsAlive) continue;
                if (alliance.allianceType != type) continue;
                if (alliance.ForceList == null || !alliance.ForceList.Contains(b)) continue;
                return alliance;
            }
            return null;
        }
    }

    /// <summary>
    /// 破除两势力之间的盟约。
    /// 清理顺序与 Alliance.OnTurnStart 的自然到期路径一致：
    /// 标记失效 → 从各势力列表摘除 → 从场景集合移除。
    /// </summary>
    [EventEffectMeta("破除盟约", "解除两个势力之间的盟约；Type 取 Alliance/Truce/Trade", "ForceSlot", "OtherSlot", "Type")]
    public class ForceBreakAllianceEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force a = GetSlot<Force>("ForceSlot", context);
            Force b = GetSlot<Force>("OtherSlot", context);

            if (a == null || b == null || a == b)
            {
                Log.Warning("事件：破除盟约失败 —— ForceSlot / OtherSlot 未绑定或指向同一势力");
                return false;
            }

            AllianceType type;
            string typeText = GetString("Type", "Alliance");
            switch ((typeText ?? "Alliance").ToLowerInvariant())
            {
                case "alliance": type = AllianceType.Alliance; break;
                case "truce": type = AllianceType.Truce; break;
                default:
                    Log.Warning($"事件：破除盟约失败 —— 未知的 Type={typeText}（可用 Alliance/Truce）");
                    return false;
            }

            Alliance alliance = ForceSetDiplomacyEffect.FindAlliance(a, b, type);
            if (alliance == null)
            {
                Log.Info($"事件：{a.Name} 与 {b.Name} 之间没有可破除的{ForceSetDiplomacyEffect.TypeName(type)}，本次跳过");
                return true;
            }

            // ① 先失效，避免 AI 在本帧剩余流程里继续把它当有效盟约
            alliance.IsAlive = false;

            // ② 从各势力列表摘除（改的是 force.AllianceList，不是正在遍历的 ForceList，安全）
            if (alliance.ForceList != null)
            {
                for (int i = 0; i < alliance.ForceList.Count; i++)
                {
                    Force force = alliance.ForceList.Get(i);
                    if (force != null && force.AllianceList != null)
                        force.AllianceList.Remove(alliance);
                }
            }

            // ③ 从场景集合移除（否则会作为孤儿对象一直随存档序列化）
            if (Scenario.Cur != null) Scenario.Cur.Remove(alliance);

            Log.Info($"事件：{a.Name} 与 {b.Name} 的{ForceSetDiplomacyEffect.TypeName(type)}已解除");
            return true;
        }
    }

    /// <summary>给势力授予一项技巧（同步科技树与被动效果）</summary>
    [EventEffectMeta("授予技巧", "给势力点亮一项技巧（自动重建科技树与可建建筑表）", "ForceSlot", "TechniqueId")]
    public class ForceAddTechniqueEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }

            int techniqueId = EvalInt("TechniqueId", context);
            if (techniqueId <= 0)
            {
                Log.Warning($"事件：授予技巧失败 —— TechniqueId 非法（{techniqueId}）");
                return false;
            }

            Technique technique = GetTechnique(techniqueId);
            if (technique == null)
            {
                Log.Warning($"事件：授予技巧失败 —— CommonData.Techniques 里找不到 Id={techniqueId}");
                return false;
            }

            if (!force.GainTechnique(technique))
            {
                Log.Info($"事件：{force.Name} 已掌握技巧 {technique.Name}，本次授予跳过");
                return true;
            }

            Log.Info($"事件：{force.Name} 掌握了技巧 {technique.Name}");
            return true;
        }

        /// <summary>按 Id 取技巧定义</summary>
        /// <param name="techniqueId">技巧 Id</param>
        /// <returns>技巧对象；取不到返回 null</returns>
        internal static Technique GetTechnique(int techniqueId)
        {
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Techniques == null)
                return null;
            return scenario.CommonData.Techniques.Get(techniqueId);
        }
    }

    /// <summary>
    /// 立即完成势力当前在研的技巧。
    /// 用于"三顾茅庐后立刻掌握某某"这类剧情加速，不改变研究规则本身。
    /// </summary>
    [EventEffectMeta("完成当前研究", "立即完成该势力正在研究的技巧（没有在研则跳过）", "ForceSlot")]
    public class ForceCompleteResearchEffect : EventEffectBase
    {
        /// <summary>执行</summary>
        /// <param name="context">上下文</param>
        /// <returns>是否成功</returns>
        public override bool Execute(EventContext context)
        {
            Force force = GetSlot<Force>("ForceSlot", context);
            if (force == null)
            {
                WarnSlotMissing("ForceSlot", GetString("ForceSlot"));
                return false;
            }

            int techniqueId = force.ResearchTechnique;
            if (techniqueId <= 0)
            {
                Log.Info($"事件：{force.Name} 当前没有在研技巧，本次跳过");
                return true;
            }

            Technique technique = ForceAddTechniqueEffect.GetTechnique(techniqueId);
            if (technique == null)
            {
                Log.Warning($"事件：完成研究失败 —— 在研 Id={techniqueId} 在技巧表里不存在");
                return false;
            }

            force.GainTechnique(technique);
            // 清空研究状态：否则下一回合研究会从"已完成的旧项目"继续，研究进度与科技树对不上
            force.ResearchTechnique = 0;
            force.ResearchLeftCounter = 0;

            Log.Info($"事件：{force.Name} 完成了技巧研究 {technique.Name}");
            return true;
        }
    }

    /// <summary>外交 / 技巧类效果的批量注册入口</summary>
    public static class EventEffectsDiplomacy
    {
        /// <summary>注册本文件内的全部效果</summary>
        public static void RegisterAll()
        {
            EventEffectBase.Register("ForceSetDiplomacy", EventEffectBase.CreateHandle<ForceSetDiplomacyEffect>);
            EventEffectBase.Register("ForceBreakAlliance", EventEffectBase.CreateHandle<ForceBreakAllianceEffect>);
            EventEffectBase.Register("ForceAddTechnique", EventEffectBase.CreateHandle<ForceAddTechniqueEffect>);
            EventEffectBase.Register("ForceCompleteResearch", EventEffectBase.CreateHandle<ForceCompleteResearchEffect>);
        }
    }
}
