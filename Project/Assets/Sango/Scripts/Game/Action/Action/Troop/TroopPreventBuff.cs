using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 状态免疫：否决施加到本部队身上的 Buff（八阵"不受异常状态"、虎喝的抗性等）。
    ///
    /// <para>
    /// 【挂载点】<see cref="GameEvent.OnTroopCheckBuff"/>，由 <c>BuffManager.AddBuff</c> 在
    /// 真正创建 BuffInstance 之前询问。否决后连实例都不会创建，也就不会有特效、
    /// 状态位和事件订阅，不存在"先加上再移除"的抖动。
    /// </para>
    /// <para>
    /// 【过滤优先级】<c>buffIds</c> 精确匹配 → <c>buffKinds</c> 按类别匹配 →
    /// 两者都不配则免疫全部。ID 匹配在前，是为了避免"只想免疫混乱却把同类全部免疫"。
    /// </para>
    /// <para>
    /// 【性能】buffKinds 需要反查 Buff 对象，因此只在配置了 kinds 且 ID 未命中时才反查；
    /// 既不配 ID 也不配 kind 的"全免疫"路径完全不做反查。
    /// </para>
    /// </summary>
    public class TroopPreventBuff : TroopTroopActionBase
    {
        /// <summary>要免疫的 Buff Id 列表；null 表示不按 Id 限定</summary>
        int[] buffIds;

        /// <summary>要免疫的 Buff 类别列表；null 表示不按类别限定</summary>
        int[] buffKinds;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：buffIds（int[]）、buffKinds（int[]）、condition。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            JArray idArray = p.Value<JArray>("buffIds");
            if (idArray != null && idArray.Count > 0)
            {
                buffIds = new int[idArray.Count];
                for (int i = 0; i < idArray.Count; i++)
                    buffIds[i] = idArray[i].ToObject<int>();
            }

            JArray kindArray = p.Value<JArray>("buffKinds");
            if (kindArray != null && kindArray.Count > 0)
            {
                buffKinds = new int[kindArray.Count];
                for (int i = 0; i < kindArray.Count; i++)
                    buffKinds[i] = kindArray[i].ToObject<int>();
            }

            GameEvent.OnTroopCheckBuff += OnTroopCheckBuff;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnTroopCheckBuff -= OnTroopCheckBuff;
        }

        /// <summary>Id 是否命中配置的白名单</summary>
        /// <param name="buffId">Buff Id</param>
        /// <returns>命中返回 true</returns>
        bool MatchId(int buffId)
        {
            if (buffIds == null)
                return false;
            for (int i = 0; i < buffIds.Length; i++)
            {
                if (buffIds[i] == buffId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 被施加状态时决定是否否决。
        /// </summary>
        /// <param name="troop">被施加方部队</param>
        /// <param name="buffId">Buff Id</param>
        /// <param name="allowData">是否允许（默认 true，置 false 即免疫）</param>
        void OnTroopCheckBuff(Troop troop, int buffId, OverrideData<bool> allowData)
        {
            if (troop == null || allowData == null)
                return;

            // 归属过滤：只对自己 / 自己势力生效
            if (Troop != null && Troop != troop) return;
            if (Force != null && troop.BelongForce != Force) return;

            if (condition != null && !condition.Check(new TroopConditionDatabase(troop)))
                return;

            if (!MatchId(buffId))
            {
                // ID 未命中时：若没配 kinds，说明"未配白名单"→ 全免疫
                if (buffKinds != null)
                {
                    Buff buff = Scenario.Cur.GetObject<Buff>(buffId);
                    if (buff == null)
                        return;
                    bool matched = false;
                    for (int i = 0; i < buffKinds.Length; i++)
                    {
                        if (buffKinds[i] == buff.kind)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                        return;
                }
            }

            allowData.Value = false;
        }
    }
}
