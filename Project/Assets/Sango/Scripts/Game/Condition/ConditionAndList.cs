using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 和集,任何一个不满足则返回false
    /// </summary>
    public class ConditionAndList : Condition
    {
        List<Condition> list = new List<Condition>();

        /// <summary>
        /// 检查所有条件是否都满足
        /// </summary>
        /// <param name="objects">检查条件所需的对象</param>
        /// <returns>所有条件是否都满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            for (int i = 0; i < list.Count; ++i)
            {
                Condition c = list[i];
                if (c != null && !c.Check(database))
                    return false;
            }
            return true;
        }

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            JArray array = p.Value<JArray>("list");
            for (int i = 0; i < array.Count; i++)
            {
                JObject obj = array[i].Value<JObject>();
                Condition c = Condition.Create(obj.Value<string>("class"));
                if (c != null)
                {
                    c.Init(obj, sangoObjects);
                    list.Add(c);
                }
            }
        }
    }
}
