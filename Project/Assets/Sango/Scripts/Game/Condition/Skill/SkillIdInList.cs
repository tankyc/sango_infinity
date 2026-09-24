using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    public class SkillIdInList : Condition
    {
        List<int> ids;
        // 比较结果: 0不包含 1包含
        int result;
        public override bool Check(IConditionDatabase database)
        {
            SkillInstance skill = database.ActionSkill;
            if (skill != null)
            {
                return ids.Contains(skill.Id) == (result == 1);
            }
            return false;
        }

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        { 
            JArray kindsArray = p.Value<JArray>("ids");
            if (kindsArray != null)
            {
                ids = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    ids.Add(kindsArray[i].ToObject<int>());
            }
            result = p.Value<int>("result");
        }
    }
}
