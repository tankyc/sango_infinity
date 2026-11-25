using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 武将年龄检查条件。
    ///
    /// <para>
    /// 【用途】支撑"老当益壮""少年英雄"一类按年龄生效的设计：
    /// 黄忠式的老将加成（age gte 60）、少年武将的成长或减益（age lte 20）。
    /// 原版没有年龄条件，属于本项目新增的设计需求。
    /// </para>
    /// <para>
    /// 【年龄从哪来】<see cref="Person.Age"/> 由 <c>Person.OnPersonAgeUpdate</c> 按
    /// <c>scenario.Info.year - yearBorn</c> 统一计算，不在本类里另算，
    /// 以免与剧本年份推进（每年一次）的节奏脱节。
    /// </para>
    /// </summary>
    public class PersonAgeCheck : Condition
    {
        /// <summary>true = 检查施法方（ActionPerson），false = 检查目标（TargetPerson）</summary>
        bool compareTarget;

        /// <summary>比较运算符（eq / gt / lt / gte / lte）</summary>
        string @operator;

        /// <summary>参与比较的年龄阈值</summary>
        int age;

        /// <summary>
        /// 初始化条件。
        /// <para>JSON 参数：compareTarget（"self"/"target"，缺省 target）、operator（缺省 gte）、age。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 【默认 target】年龄条件主要用于描述"对方是不是老将/少年"，
            // 与 PersonSexCheck 保持同一默认值。
            compareTarget = (p.Value<string>("compareTarget") ?? "target") == "self";
            @operator = p.Value<string>("operator") ?? "gte";
            age = p.Value<int>("age");
        }

        /// <summary>
        /// 检查年龄是否满足条件。
        /// </summary>
        /// <param name="database">条件上下文</param>
        /// <returns>满足返回 true；取不到武将或年龄数据缺失时返回 false</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person person = compareTarget ? database.ActionPerson : database.TargetPerson;

            // 部队场景回落：取对应部队的主将
            if (person == null)
            {
                Troop troop = compareTarget ? database.ActionTroop : database.TargetTroop;
                person = troop != null ? troop.Leader : null;
            }

            if (person == null)
                return false;

            // 【数据缺失保护】yearBorn 未填时 Age = 当前年份（可能是 200+），
            // 直接比较会让"老将"条件恒成立、"少年"条件恒不成立，属于静默错判。
            // 这里按"年龄未知"处理，一律返回 false，让配置者从数据侧发现问题。
            if (person.yearBorn <= 0 || person.Age < 0)
                return false;

            return GameUtility.CheckCondition(age, @operator, person.Age);
        }
    }
}
