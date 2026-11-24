using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 条件：当前武将是否是所在势力的君主。
    ///
    /// 用途举例——特技「桀骜不羁」（身为君主时骑兵暴击伤害提升 20%，否则降低 20%）：
    /// 同一个暴击系数 Action 配两条，靠本条件的 result 互斥生效。
    ///
    /// 参数：
    ///   result 1 = 要求是君主；0 = 要求不是君主（默认 1）
    ///
    /// 【为什么不用 Person.IsLeader】<c>PersonStateType.Leader</c> 是"首领 / 一般武将"档，
    /// 与"君主"无关；君主判定的唯一口径是 <c>Force.mGovernor</c>（存档字段为 Governor）。
    /// </summary>
    public class PersonIsGovernor : Condition
    {
        /// <summary>期望结果：1 要求是君主 / 0 要求不是君主</summary>
        int result = 1;

        /// <summary>
        /// 初始化条件。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 未配置时默认"要求是君主"；显式写 0 才是"要求不是君主"
            result = 1;
            if (p["result"] != null)
                result = p.Value<int>("result") != 0 ? 1 : 0;
        }

        /// <summary>
        /// 判定当前武将是否满足"是 / 不是君主"。
        /// </summary>
        /// <param name="database">条件数据库（取 ActionPerson）</param>
        /// <returns>满足返回 true</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = database?.ActionPerson;
            bool isGovernor = person != null
                && person.BelongForce != null
                && person.BelongForce.mGovernor == person;

            return isGovernor == (result == 1);
        }
    }
}
