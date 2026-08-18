using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    public abstract class ForceTroopActionBase : ForceActionBase
    {
        protected int value;
        protected List<int> kinds;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            value = p.Value<int>("value");
            JArray kindsArray = p.Value<JArray>("kinds");
            if (kindsArray != null)
            {
                kinds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    kinds.Add(kindsArray[i].ToObject<int>());
            }
        }

        public virtual bool CheckForceTroop(Troop troop)
        {
            if (Force != troop.mBelongForce) return false;
            if (kinds != null && !kinds.Contains(troop.TroopType.kind)) return false;
            return true;
        }

    }
}
