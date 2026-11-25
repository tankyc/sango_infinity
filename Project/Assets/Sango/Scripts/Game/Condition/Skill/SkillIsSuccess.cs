using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 本次施法是否成功（条件）。
    ///
    /// <para>
    /// 【历史坑】原实现读的是 <c>tempCriticalFactor &gt; 100</c>（会心倍率），
    /// 与会心条件 <see cref="SkillIsCritical"/> 完全重复，导致"计略失败"永远判不出来：
    /// 55421 后着（计略失败后再行动）、55495 沮气（计略失败额外扣气）、
    /// 30144 百奇谋（计略成功暴击）三条设计全部失效。
    /// </para>
    /// <para>
    /// 【正确字段】<c>SkillInstance.tempSuccessFactor</c>：1 = 施放成功、0 = 失败。
    /// 由 <c>CheckSuccess</c> 在每次施法流程开头重置为 1、失败分支置 0
    /// （<c>SkillInstance.cs:897/920/927/950</c>），调用点在 <c>Troop.cs:3135</c>，
    /// 因此每次施法都拿得到"本次"的真实结果，不会残留上一次的值。
    /// </para>
    /// <para>
    /// 【口径】普攻必中（<c>IsNormal</c> 直接返回 true，tempSuccessFactor 保持 1），
    /// 所以本条件对普攻恒为"成功"；它真正的作用对象是计略与战法。
    /// </para>
    /// JSON 参数：<c>result</c>（0 = 要求失败，1 = 要求成功，缺省 0）。
    /// </summary>
    public class SkillIsSuccess : Condition
    {
        /// <summary>比较结果：0 要求失败，1 要求成功</summary>
        int result;

        /// <summary>
        /// 检查本次施法结果是否与配置一致。
        /// </summary>
        /// <param name="database">条件上下文</param>
        /// <returns>一致返回 true；没有施法方时返回 false</returns>
        public override bool Check(IConditionDatabase database)
        {
            SkillInstance skill = database.ActionSkill;
            if (skill != null)
            {
                return (skill.tempSuccessFactor > 0) == (result == 1);
            }
            return false;
        }

        /// <summary>初始化条件</summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            result = p.Value<int>("result");
        }
    }
}
