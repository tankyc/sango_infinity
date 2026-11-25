using Sango.Render;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class BuffManager
    {
        [JsonProperty]
        public List<BuffInstance> _buffs = new List<BuffInstance>();

        public class BuffEffectInfo
        {
            public string name;
            public int refCount;
            public Vector3 offset;
            public GameObject instanceObj;

            public void CreateAsset(SangoObject target)
            {
                if (instanceObj != null)
                    return;

                // 【防御】target 为空（BuffManager 的 Master 未绑定时的调用）或渲染体缺失时直接返回。
                // 前者是 NRE 的直接来源：异常落在下一行的 target.GetRender() 上，
                // 说明是"target == null"，而不是 GetRender() 返回 null（那会落在 IsVisible 那行）。
                if (target == null)
                {
                    Sango.Log.Error("[Buff] CreateAsset: target 为空，跳过特效创建（BuffManager 的 Master 未绑定？）");
                    return;
                }

                ObjectRender objectRender = target.GetRender();
                if (objectRender == null)
                {
                    Sango.Log.Error("[Buff] CreateAsset: GetRender() 为空，跳过特效创建");
                    return;
                }
                if (!objectRender.IsVisible()) return;

                instanceObj = PoolManager.Create(name);
                if (instanceObj != null)
                {
                    instanceObj.transform.SetParent(objectRender.GetTransform(), false);
                    instanceObj.transform.localPosition = offset;
                }
            }

            public void ClearAsset()
            {
                if (instanceObj != null)
                {
                    PoolManager.Recycle(instanceObj);
                    instanceObj = null;
                }
            }

        }

        Dictionary<string, BuffEffectInfo> assetRef = new Dictionary<string, BuffEffectInfo>();

        public Troop Master { get; private set; }

        public void Init(Troop master)
        {
            Master = master;
            foreach (BuffInstance ins in _buffs)
                ins.Init(this, ins.Buff, ins.Master);

            // 读档 / 绑定后立刻按已加载的 Buff 重建状态位：
            // stateFlags 不序列化（派生状态），不在这里补算就会出现"存档里有混乱、标记却是 0"。
            RefreshMasterStateFlags();
        }

        public void OnModelLoaded(GameObject model)
        {
            foreach (BuffEffectInfo info in assetRef.Values)
                info.CreateAsset(Master);
        }

        public void OnModelClear()
        {
            foreach (BuffEffectInfo info in assetRef.Values)
                info.ClearAsset();
        }


        /// <summary>
        /// 清空所有状态实例（拆剧本 / 部队收尾时调用）。
        ///
        /// 为什么需要：BuffInstance 的 effects（Stun、Escape 等 BuffEffect）会在自己的
        /// Init 里订阅全局事件（例如 <c>OnTroopTurnStart</c>），且只在各自的 Clear() 里退订
        /// （见 BuffInstance.Clear → effects[i].Clear）。常规移除路径（RemoveBuff /
        /// RemoveBuffByKind / TurnUpdate 到期）都没问题，但剧本收尾时如果不清这里，
        /// 旧剧本仍挂着状态的部队就会把订阅带到下一次开局，订阅数逐轮累积。
        /// </summary>
        public void Clear()
        {
            if (_buffs != null)
            {
                for (int i = 0; i < _buffs.Count; i++)
                {
                    BuffInstance buff = _buffs[i];
                    if (buff != null)
                        buff.Clear();       // 内部逐个 effects[i].Clear()，完成事件退订
                }
                _buffs.Clear();
            }

            // 表现对象回收（ClearAsset 内部有 null 判断，重复调用安全）
            foreach (BuffEffectInfo info in assetRef.Values)
            {
                if (info != null)
                    info.ClearAsset();
            }
            assetRef.Clear();

            // Buff 已全清 → 状态位归零（必须在 Master 置空之前，否则算不到宿主身上）
            RefreshMasterStateFlags();

            Master = null;
        }

        public void AddBuff(int id, int turnCount, SangoObject srcTroop)
        {
            // 【状态免疫询问】八阵"不受异常状态"、虎喝等设计在这里否决。
            //
            // 【性能】必须先判 null：没有特技订阅时整段跳过，
            // 不创建 OverrideData、不做委托调用，热路径上零额外开销。
            // 放在建实例之前，是因为否决时不该产生一个已订阅事件的 BuffInstance 再回收。
            if (GameEvent.OnTroopCheckBuff != null)
            {
                Tools.OverrideData<bool> allowData = Tools.OverrideData<bool>.Create(true);
                GameEvent.OnTroopCheckBuff.Invoke(Master, id, allowData);
                bool allow = allowData.ValueAndRecycle;
                if (!allow)
                    return;
            }

            Buff buff = Scenario.Cur.GetObject<Buff>(id);
            BuffInstance buffInstance = new BuffInstance()
            {
                leftCounter = turnCount,
            };
            buffInstance.Init(this, buff, srcTroop);
            _buffs.Add(buffInstance);
            RefreshMasterStateFlags();
        }

        public void RemoveBuff(int id)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = _buffs[i];
                // Buff 数据被删时解析为 null：不能在这里空引用（AddBuff 允许实例骨架存在）
                if (buff.Buff != null && buff.Buff.Id == id)
                {
                    buff.Clear();
                    _buffs.RemoveAt(i);
                }
            }
            RefreshMasterStateFlags();
        }

        public void RemoveBuffByKind(int kind)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = _buffs[i];
                if (buff.Buff != null && buff.Buff.kind == kind)
                {
                    buff.Clear();
                    _buffs.RemoveAt(i);
                }
            }
            RefreshMasterStateFlags();
        }

        /// <summary>
        /// 把所有 Buff 声明的状态折叠成 <see cref="TroopStateFlag"/> 位域。
        ///
        /// 【为什么折叠】ZOC 判定（<see cref="Map.IsZOC"/>）在移动范围展开时对每格 6 邻格
        /// 各判一次，是整条寻路里最热的循环；若每次都遍历 Buff 列表 + 解析 JSON 类名，
        /// 代价不可接受。这里只在 Buff 增删时算一次，热路径读一个 bit。
        ///
        /// 【写入 / 移除成对】本方法是状态位的**唯一写入点**（经
        /// <see cref="RefreshMasterStateFlags"/> 落到 <see cref="Troop.stateFlags"/>），
        /// 且每次都是从 Buff 列表**整体重算**而不是增量 set / clear。
        /// 因此所有移除路径（单个移除 / 按类移除 / 到期 RemoveAll / 清空 / 读档重建）
        /// 都会自然清位，不存在"加的时候置位、移除时忘了清"的残留问题。
        ///
        /// 【复杂度与调用频率（性能核算）】
        ///   · 本方法只在 Buff 增 / 删 / 到期 / 清空 / 读档时调用，**不在每帧、每次攻击、每次寻路里**；
        ///   · 循环体是"读 <see cref="BuffInstance.StateFlag"/> 缓存 + 一次按位或"，
        ///     即 O(N) 次整数运算（N = 该部队身上的 Buff 数，实战通常 0~3）；
        ///   · 单次成本约几十纳秒，且与 buffEffects 里配了几条效果**无关** ——
        ///     JSON 解析已提前到 <see cref="BuffInstance.Init"/> 做了一次。
        ///   · 最坏量级估算：全场 200 支部队 × 每回合各 1 次（<see cref="OnForceTurnStart"/>）
        ///     × 3 个 Buff ≈ 600 次按位或/回合，远低于 1 微秒，可忽略。
        /// 热路径（ZOC 展开、AI 评分、反击判定）读的是 <see cref="Troop.StateFlags"/> 缓存位，O(1)。
        /// </summary>
        /// <returns>状态位域（0 = 无状态）</returns>
        public int GetStateFlags()
        {
            int flags = 0;
            if (_buffs == null) return flags;

            for (int i = 0; i < _buffs.Count; i++)
            {
                BuffInstance ins = _buffs[i];
                if (ins == null) continue;

                // 贡献值在 BuffInstance.Init 里用 Buff.GetStateFlag() 算好并缓存
                flags |= ins.StateFlag;
            }
            return flags;
        }

        /// <summary>把状态位重算推给宿主部队（Master 未绑定或部队已收尾时静默跳过）</summary>
        void RefreshMasterStateFlags()
        {
            if (Master != null)
                Master.RefreshStateFlags();
        }

        /// <summary>
        /// 是否处于**被控**状态（不能行动、不能反击）。
        ///
        /// 【与状态位的关系】直接读 <see cref="Troop.stateFlags"/> 的缓存位，
        /// 不再每次遍历 Buff 列表解析效果类名 —— 既快，也保证与
        /// <see cref="Troop.HasStateFlag"/> 等其他判定得到同一个结论。
        /// </summary>
        public bool HasControlBuff()
        {
            return (GetCachedStateFlags() & TroopStateFlags.ControlMask) != 0;
        }

        /// <summary>
        /// 是否处于**伪报**状态（原版 UnitState_Gihou）。
        /// 与 <see cref="HasControlBuff"/> 的区别：混乱（Stun）不算伪报。
        /// AI 用它做"打了也没意义"的降权，见 TroopAIUtility.CalcSkillBasePriority。
        /// </summary>
        public bool HasEscapeBuff()
        {
            return (GetCachedStateFlags() & (int)TroopStateFlag.Escape) != 0;
        }

        /// <summary>
        /// 取宿主部队已缓存的状态位；Master 未绑定（尚未 Init / 已收尾）时回落到实时重算。
        ///
        /// 【为什么要回落】BuffManager 的 Master 只在 <see cref="Init"/> 里绑定，
        /// 反序列化完成到 Init 之间的窗口里可能有查询进来，这时不能直接空引用。
        /// </summary>
        int GetCachedStateFlags()
        {
            return Master != null ? Master.StateFlags : GetStateFlags();
        }

        /// <summary>
        /// 所有 Buff 对部队攻击力的**累乘**系数（百分比，100 = 不变）。
        ///
        /// 【原版对应】unit_calc_stat 的 <c>state_coef</c>（混乱 ×0.8）。
        /// 本项目改成"每个 Buff 自己声明系数、这里累乘"，新增状态只需配数据。
        /// </summary>
        public int GetTroopAttackCoef()
        {
            int coef = 100;
            for (int i = 0; i < _buffs.Count; i++)
            {
                int c = _buffs[i].Buff != null ? _buffs[i].Buff.troopAttackCoef : 100;
                if (c > 0) coef = coef * c / 100;
            }
            return coef;
        }

        /// <summary>所有 Buff 对部队防御力的累乘系数（100 = 不变）</summary>
        public int GetTroopDefenceCoef()
        {
            int coef = 100;
            for (int i = 0; i < _buffs.Count; i++)
            {
                int c = _buffs[i].Buff != null ? _buffs[i].Buff.troopDefenceCoef : 100;
                if (c > 0) coef = coef * c / 100;
            }
            return coef;
        }

        /// <summary>所有 Buff 对部队建设力的累乘系数（100 = 不变）</summary>
        public int GetTroopBuildCoef()
        {
            int coef = 100;
            for (int i = 0; i < _buffs.Count; i++)
            {
                int c = _buffs[i].Buff != null ? _buffs[i].Buff.troopBuildCoef : 100;
                if (c > 0) coef = coef * c / 100;
            }
            return coef;
        }

        /// <summary>
        /// 所有 Buff 对部队移动力的**累乘**系数（百分比，100 = 不变，0 = 无法移动）。
        ///
        /// 【用途】足止状态。与攻击/防御系数不同，这里 `0` 是**有效值**（不是"未配置"），
        /// 所以用 `&gt;= 0` 判定并允许乘出 0。
        /// </summary>
        public int GetTroopMoveCoef()
        {
            int coef = 100;
            for (int i = 0; i < _buffs.Count; i++)
            {
                int c = _buffs[i].Buff != null ? _buffs[i].Buff.troopMoveCoef : 100;
                if (c < 0) continue;
                coef = coef * c / 100;
            }
            return coef;
        }

        public void OnForceTurnStart(Scenario scenario)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                BuffInstance buff = _buffs[i];
                buff.TurnUpdate();
            }

            _buffs.RemoveAll(x => x.leftCounter < 0);

            // 到期移除走的是 RemoveAll，不经过 RemoveBuff —— 必须在这里补一次状态位重算，
            // 否则"混乱到期消失、ZOC 抑制却还在"。
            RefreshMasterStateFlags();
        }

        /// <summary>
        /// 是否处于无法行动的状态（<see cref="HasControlBuff"/> 的旧命名别名）。
        /// 原先恒返回 false，调用方会误以为"永远没被控"；现统一到状态位判定。
        /// </summary>
        public bool HasControlState()
        {
            return HasControlBuff();
        }

        public void CreateAsset(string asset, Vector3 offset)
        {
            // 【防御】加攻 / 加防 / 沉默这类 Buff 本来就没有特效资源（asset 缺省即 null）。
            // Dictionary 不接受 null 键，不在这里挡住会直接抛 ArgumentNullException。
            if (string.IsNullOrEmpty(asset))
                return;

            if (assetRef.TryGetValue(asset, out BuffEffectInfo refInfo))
            {
                refInfo.refCount++;
                return;
            }
            BuffEffectInfo buffEffectInfo = new BuffEffectInfo()
            {
                name = asset,
                offset = offset,
                refCount = 1
            };
            assetRef[asset] = buffEffectInfo;

            // 【防御】Master 未绑定时不建特效（引用已登记，后续 Master 就绪后仍会走缓存命中路径）。
            // 线上出现的 NRE 就是从这里带着 Master == null 进去的。
            if (Master != null)
                buffEffectInfo.CreateAsset(Master);
        }

        public void ReleaseAsset(string asset)
        {
            // 与 CreateAsset 对称：没有特效资源的 Buff 不需要回收
            if (string.IsNullOrEmpty(asset))
                return;

            if (assetRef.TryGetValue(asset, out BuffEffectInfo refInfo))
            {
                refInfo.refCount--;
                if(refInfo.refCount == 0)
                {
                    refInfo.ClearAsset();
                    assetRef.Remove(asset);
                }
            }
        }
    }
}
