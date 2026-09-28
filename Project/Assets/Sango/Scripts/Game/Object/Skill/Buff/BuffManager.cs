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

            Master = null;
        }

        public void AddBuff(int id, int turnCount, Troop srcTroop)
        {
            Buff buff = Scenario.Cur.GetObject<Buff>(id);
            BuffInstance buffInstance = new BuffInstance()
            {
                leftCounter = turnCount,
            };
            buffInstance.Init(this, buff, srcTroop);
            _buffs.Add(buffInstance);
        }

        public void RemoveBuff(int id)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = _buffs[i];
                if (buff.Buff.Id == id)
                {
                    buff.Clear();
                    _buffs.RemoveAt(i);
                }
            }
        }

        public void RemoveBuffByKind(int kind)
        {
            for (int i = _buffs.Count - 1; i >= 0; i--)
            {
                BuffInstance buff = _buffs[i];
                if(buff.Buff.kind == kind)
                {
                    buff.Clear();
                    _buffs.RemoveAt(i);
                }
            }
        }

        public bool HasControlBuff()
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                BuffInstance buff = _buffs[i];
                if (buff.IsControlBuff())
                    return true;
            }
            return false;
        }

        public void OnForceTurnStart(Scenario scenario)
        {
            for (int i = 0; i < _buffs.Count; i++)
            {
                BuffInstance buff = _buffs[i];
                buff.TurnUpdate();
            }

            _buffs.RemoveAll(x => x.leftCounter < 0);
        }

        public bool HasControlState()
        {
            return false;
        }

        public void CreateAsset(string asset, Vector3 offset)
        {
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
