using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public class SkillIsStrategySkill : Condition
    {
        // 比较结果: 0不是 1是
        int result;
        public override bool Check(IConditionDatabase database)
        {
            SkillInstance skill = database.ActionSkill;
            if (skill != null)
            {
                return (skill.IsStrategy()) == (result == 1);
            }
            return false;
        }

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            result = p.Value<int>("result");
        }
    }
}
