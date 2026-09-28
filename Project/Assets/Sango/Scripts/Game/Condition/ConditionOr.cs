using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 或条件类，用于组合多个条件，只要有一个条件满足就返回true
    /// </summary>
    public class ConditionOr : Condition
    {
        /// <summary>
        /// 左侧条件
        /// </summary>
        Condition L;

        /// <summary>
        /// 右侧条件
        /// </summary>
        Condition R;

        /// <summary>
        /// 检查所有条件是否都满足
        /// </summary>
        /// <param name="objects">检查条件所需的对象</param>
        /// <returns>所有条件是否都满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (L != null && L.Check(database)) return true;
            if (R != null && R.Check(database)) return true;
            return false;
        }

        /// <summary>
        /// 初始化或条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            JObject Lobj = p.Value<JObject>("L");
            if (Lobj != null)
            {
                L = Condition.Create(Lobj.Value<string>("class"));
                if (L != null)
                    L.Init(Lobj, sangoObjects);
            }

            JObject Robj = p.Value<JObject>("R");
            if (Robj != null)
            {
                R = Condition.Create(Robj.Value<string>("class"));
                if (R != null)
                    R.Init(Robj, sangoObjects);
            }
        }
    }
}
