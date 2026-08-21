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

        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Troop>))]
        public Troop Master { get; private set; }

        [JsonProperty]
        [JsonConverter(typeof(Id2ObjConverter<Buff>))]
        public Buff Buff { get; private set; }

        [JsonProperty]
        public int leftCounter;

        List<BuffEffect> effects;
        public Troop Target => Manager.Master;

        public void Init(BuffManager manager, Buff buff, Troop master)
        {
            Manager = manager;
            Master = master;
            Buff = buff;
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

        public void Clear()
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
