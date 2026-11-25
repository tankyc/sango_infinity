using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 武将性别检查条件。
    ///
    /// <para>
    /// 【用途】支撑"只对异性生效"的特技设计 —— 美人计（对异性魅力低的武将）、
    /// 洛神（自部队溃灭后周围异性部队士气下降）、国色天香（大乔小乔）、红颜（对非女性武将部队计略上升）。
    /// 原版没有这一类判定，属于本项目新增的设计需求。
    /// </para>
    /// <para>
    /// 【为什么回落到部队主将】这些特技多数挂在部队上生效，此时
    /// <see cref="IConditionDatabase.TargetPerson"/> 不一定会被填充（部分调用方只塞了部队）。
    /// 因此 Person 为空时按"对应部队的主将"再取一次，避免条件恒为 false。
    /// </para>
    /// </summary>
    public class PersonSexCheck : Condition
    {
        /// <summary>true = 检查施法方（ActionPerson），false = 检查目标（TargetPerson）</summary>
        bool compareTarget;

        /// <summary>期望性别：0 男 / 1 女（对应 <see cref="Person.sex"/>）</summary>
        int sex;

        /// <summary>
        /// 初始化条件。
        /// <para>JSON 参数：compareTarget（"self"/"target"，缺省 target）、sex（0/1）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            // 【默认 target】这类条件主要用来描述"对方是异性"，
            // 与 TroopAroundTeammateCount 的默认 self 语义不同，故显式取 target。
            compareTarget = (p.Value<string>("compareTarget") ?? "target") == "self";
            sex = p.Value<int>("sex");
        }

        /// <summary>
        /// 检查性别是否匹配。
        /// </summary>
        /// <param name="database">条件上下文</param>
        /// <returns>性别匹配返回 true；取不到武将时返回 false</returns>
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

            return person.sex == sex;
        }
    }
}
