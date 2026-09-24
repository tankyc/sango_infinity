using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public class ProbabilityCheck : Condition
    {
        int probability;
        public override bool Check(IConditionDatabase database)
        {
            return GameRandom.Range(0, 10000) <= probability;
        }

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            probability = p.Value<int>("probability");
        }
    }
}
