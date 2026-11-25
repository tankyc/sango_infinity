using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class BuffInstance : SangoObject
    {
        public override SangoObjectType ObjectType => SangoObjectType.BuffInstance;

        public BuffManager Manager { get; private set; }

        /// <summary>
        /// 来源对象的 id（存档数值），读取 Master 时按需解析
        /// </summary>
        [JsonProperty("Master")]
        public int MasterId;

        /// <summary>
        /// 来源对象类型（存档数值，SangoObjectType）：0 未知 / Troops / City / Building。
        /// 旧存档没有该字段，读档时按"先按部队解析"兼容。
        /// </summary>
        [JsonProperty("MasterType")]
        public int MasterType;

        SangoObject mMaster;
        public SangoObject Master
        {
            get
            {
                if (mMaster == null && MasterId > 0)
                    mMaster = ResolveMaster();
                return mMaster;
            }
            private set
            {
                mMaster = value;
                MasterId = value != null ? value.Id : 0;
                MasterType = value != null ? (int)value.ObjectType : 0;
            }
        }

        /// <summary>
        /// 按存档类型解析来源对象。
        /// 建筑光环的 27 号效果来源是建筑，不能再按部队 Id 解析（两张表的 Id 会撞）。
        /// 旧存档 MasterType = 0，按部队解析以保持兼容。
        /// </summary>
        SangoObject ResolveMaster()
        {
            switch ((SangoObjectType)MasterType)
            {
                case SangoObjectType.City:
                    return IdRef.Resolve<City>(MasterId);
                case SangoObjectType.Building:
                    return IdRef.Resolve<Building>(MasterId);
                case SangoObjectType.Troops:
                    return IdRef.Resolve<Troop>(MasterId);
                default:
                    return IdRef.Resolve<Troop>(MasterId);
            }
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

        /// <summary>
        /// 本实例对部队状态位域的贡献值（<see cref="Buff.GetStateFlag"/> 的**缓存**）。
        ///
        /// 【为什么缓存】Buff 是静态数据，位域结果在运行期不会变；而
        /// <see cref="BuffManager.GetStateFlags"/> 每次 Buff 增删 / 到期 / 读档都要全量重算。
        /// 不缓存时每次重算都要遍历 buffEffects 的 JObject 并做字符串比较；
        /// 缓存后重算退化为"N 次整数或运算"（N = 该部队身上的 Buff 数，通常 0~3），
        /// 单次开销稳定在纳秒级，且与效果类数量无关。
        ///
        /// 【失效时机】只在 <see cref="Init"/> 里重算；Buff 数据在开局加载后不再热更新，
        /// 因此不需要额外失效逻辑。读档走 <see cref="BuffManager.Init"/> → 逐实例 Init，天然重算。
        /// </summary>
        int cachedStateFlag;

        /// <summary>本实例贡献的状态位（缓存值，见 <see cref="cachedStateFlag"/>）</summary>
        public int StateFlag => cachedStateFlag;

        /// <summary>
        /// 是否已向 <see cref="BuffManager"/> 登记过特效资源。
        ///
        /// 【为什么需要】<see cref="Init"/> 会被重复调用（读档重建 / BuffManager 重新绑定），
        /// 而 <c>CreateAsset</c> 是引用计数语义：重复登记会让 refCount 多于实际实例数，
        /// 之后 <see cref="Clear"/> 只减一次，特效对象永远回收不掉。
        /// </summary>
        bool assetCreated;

        public void Init(BuffManager manager, Buff buff, SangoObject master)
        {
            Manager = manager;
            Master = master;
            Buff = buff;

            // 数据缺失时贡献值归 0（Buff.GetStateFlag 也需要非空 Buff 才能算）
            cachedStateFlag = Buff != null ? Buff.GetStateFlag() : 0;

            // 【防御】Buff 数据被删（老存档里存了已移除的 Buff Id）时解析结果为 null，
            // 后续 Buff.asset / Buff.buffEffects 都会空引用。这里只记日志并保留实例骨架，
            // 由 BuffManager 的状态重算把它当作"无状态"处理，等下次存读档自然清掉。
            if (Buff == null)
            {
                Sango.Log.Error($"[Buff] BuffInstance.Init: Buff(Id={BuffId}) 在剧本数据中不存在，已跳过效果与特效初始化");
                return;
            }

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

            // 只登记一次：重复 Init 不能把引用计数加多次
            if (!assetCreated)
            {
                Manager.CreateAsset(Buff.asset, Buff.offset);
                assetCreated = true;
            }

            InitBuffEffects();
        }

        public void InitBuffEffects()
        {
            // 【防重复订阅】Init 会被多次调用（BuffManager.Init 绑定 / 读档重建 / 重新初始化）。
            // 直接 new List 覆盖旧列表会让旧 effects 的全局事件订阅永远留在事件上
            // （Stun 的 OnTroopTurnStart、Freeze 的 OnTroopTurnStart、Shield/Invincible 的
            // OnTroopChangeTroops），表现为"已经移除的 Buff 还在生效"，且订阅数逐次累积。
            ClearBuffEffects();

            if (Buff.buffEffects == null) return;
            if (Buff.buffEffects.Count == 0) return;
            effects = new List<BuffEffect>();
            for (int i = 0; i < Buff.buffEffects.Count; i++)
            {
                JObject valus = Buff.buffEffects[i] as JObject;
                if (valus == null) continue;

                BuffEffect eft = BuffEffect.Create(valus.Value<string>("class"));
                if (eft != null)
                {
                    eft.Init(valus, this);
                    effects.Add(eft);
                }
            }
        }

        /// <summary>只清理效果实例（退订事件），不动特效资源与 Buff 引用</summary>
        void ClearBuffEffects()
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
                effects[i].Clear();

            effects.Clear();
            effects = null;
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
            // 【防御】读档后若 Buff 已从剧本数据里删除，Buff 解析为 null；
            // 或实例尚未 Init（Manager 未绑定）就被 Clear，都可能在这里空引用。
            // 只释放自己登记过的那一份，避免重复 Init / Clear 把别人的引用计数减穿。
            if (assetCreated && Manager != null && Buff != null)
            {
                Manager.ReleaseAsset(Buff.asset);
                assetCreated = false;
            }

            ClearBuffEffects();
        }

        public bool IsControlBuff()
        {
            return Buff != null && Buff.IsControlBuff();
        }

        /// <summary>是否为伪报（原版 UnitState_Gihou），见 <see cref="Buff.IsEscapeBuff"/></summary>
        public bool IsEscapeBuff()
        {
            return Buff != null && Buff.IsEscapeBuff();
        }
    }
}
