using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 不可行动(扰乱)
    /// </summary>
    public class AddDefence : BuffEffect
    {
        int value;

        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
            value = p.Value<int>("value");
            master.Target.extraDefence += value;
        }

        public override void Clear()
        {
            master.Target.extraDefence -= value;
            base.Clear();
        }
    }
}
