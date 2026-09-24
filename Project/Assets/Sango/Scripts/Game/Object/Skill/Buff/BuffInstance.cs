using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class BuffInstance : SangoObject
    {
        public override SangoObjectType ObjectType => SangoObjectType.BuffInstance;

        public BuffManager Manager { get; private set; }

        /// <summary>
        /// 所属部队的 id（存档数值），读取 Master 时按需解析
        /// </summary>
        [JsonProperty("Master")]
        public int MasterId;

        Troop mMaster;
        public Troop Master
        {
            get
            {
                if (mMaster == null && MasterId > 0) mMaster = IdRef.Resolve<Troop>(MasterId);
                return mMaster;
            }
            private set { mMaster = value; MasterId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 状态(Buff)的 id（存档数值），读取 Buff 时按需解析
        /// </summary>
        [JsonProperty("Buff")]
        public int BuffId;

        Buff mBuff;
        public Buff Buff
        {
            get
            {
                if (mBuff == null && BuffId > 0) mBuff = IdRef.Resolve<Buff>(BuffId);
                return mBuff;
            }
            private set { mBuff = value; BuffId = value != null ? value.Id : 0; }
        }

        [JsonProperty]
        public int leftCounter;

        List<BuffEffect> effects;
        public Troop Target => Manager.Master;

        public void Init(BuffManager manager, Buff buff, Troop master)
        {
            Manager = manager;
            Master = master;
            Buff = buff;

            // 【诊断】传入的 BuffManager 尚未绑定部队（未 Init）时，特效层拿不到渲染体。
            // 说明：BuffManager.Master 的 set 访问器不可访问（由 BuffManager.Init 内部赋值），
            // 这里**不能补绑**，只记录现场以便定位"谁在未 Init 的 manager 上加 Buff"；
            // 空引用本身已由 BuffEffectInfo.CreateAsset / BuffManager.CreateAsset 的空判防护挡住。
            if (Manager.Master == null)
            {
                Sango.Log.Error(string.Format(
                    "[Buff] 施加 Buff(id={0}, asset={1}) 时 BuffManager 未绑定部队（Master 为空）→ 特效已跳过，请查该 Buff 的施加来源",
                    buff != null ? buff.Id : 0,
                    buff != null ? buff.asset : "?"));
            }

            Manager.CreateAsset(Buff.asset, Buff.offset);
            InitBuffEffects();
        }

        public void InitBuffEffects()
        {
            if (Buff.buffEffects == null) return;
            if (Buff.buffEffects.Count == 0) return;
            effects = new List<BuffEffect>();
            for (int i = 0; i < Buff.buffEffects.Count; i++)
            {
                JObject valus = Buff.buffEffects[i] as JObject;
                BuffEffect eft = BuffEffect.Create(valus.Value<string>("class"));
                if (eft != null)
                {
                    eft.Init(valus, this);
                    effects.Add(eft);
                }
            }
        }

        public bool TurnUpdate()
        {
            leftCounter--;
            if (leftCounter < 0)
            {
                Clear();
                return true;
            }
            
            // 执行BUFF效果
            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    effect.Action(this, Target, null, null);
                }
            }
            
            return false;
        }

        public override void Clear()
        {
            Manager.ReleaseAsset(Buff.asset);
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                    effects[i].Clear();

                effects.Clear();
                effects = null;
            }
        }

        public bool IsControlBuff()
        {
            return Buff.IsControlBuff();
        }
    }
}
