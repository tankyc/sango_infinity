using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 双方属性对比条件（施法方属性 与 目标方属性 直接比较）。
    ///
    /// <para>
    /// 【为什么不能复用 TroopAttributeCheck】那个类只把"某一方的属性"与一个常量比较，
    /// 表达不了"敌方武力低于我方武力"这类相对判定。而特技清单里这类判定出现频率极高：
    /// 骁将 / 步将（对武力低的敌部队战法会心）、骋袭、无双（对武力低的敌部队暴击）、
    /// 龙胆、名将（对统率低于自己的武将增伤）、游侠（对兵力高于自己的部队会心）。
    /// </para>
    /// <para>
    /// 【口径】<c>operator</c> 描述的是"施法方属性 op 目标方属性"：
    /// 配 <c>"operator":"gt"</c> 即"我的属性 &gt; 对方属性"（对弱者生效），
    /// 配 <c>"lt"</c> 即"我的属性 &lt; 对方属性"（对强者生效）。
    /// </para>
    /// <para>
    /// 【为什么默认 lt】最常见的语义是"对武力比自己低的敌人"，但那是 gt。
    /// 这里默认值取 lt 只是为了与"我方属性低于目标"的字面读法一致，
    /// 配置时请显式写 operator，避免记错方向。
    /// </para>
    /// </summary>
    public class TroopAttributeCompareBoth : Condition
    {
        /// <summary>
        /// 参与比较的属性名：command / strength / intelligence / politics / glamour /
        /// attack / defence / troops / energy / morale。
        /// </summary>
        string attributeType;

        /// <summary>比较运算符（eq / gt / lt / gte / lte），比较方向为"施法方 op 目标方"</summary>
        string @operator;

        /// <summary>
        /// 初始化条件。
        /// <para>JSON 参数：attributeType、operator（缺省 lt）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            attributeType = p.Value<string>("attributeType");
            @operator = p.Value<string>("operator") ?? "lt";
        }

        /// <summary>
        /// 取部队的一项属性值。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>属性值；未知属性名返回 0</returns>
        int GetAttributeValue(Troop troop)
        {
            if (troop == null)
                return 0;

            switch (attributeType)
            {
                case "command":
                    return troop.Command;
                case "strength":
                    return troop.Strength;
                case "intelligence":
                    return troop.Intelligence;
                case "politics":
                    return troop.Politics;
                case "glamour":
                    return troop.Glamour;
                case "attack":
                    return troop.Attack;
                case "defence":
                    return troop.Defence;
                case "troops":
                    return troop.troops;
                case "energy":
                    return troop.energy;
                case "morale":
                    return troop.morale;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 比较双方属性。
        /// </summary>
        /// <param name="database">条件上下文（需要同时具备施法方与目标方部队）</param>
        /// <returns>满足返回 true；任一方部队为空时返回 false</returns>
        public override bool Check(IConditionDatabase database)
        {
            Troop action = database.ActionTroop;
            Troop target = database.TargetTroop;
            if (action == null || target == null)
                return false;

            int actionValue = GetAttributeValue(action);
            int targetValue = GetAttributeValue(target);

            // CheckCondition(阈值, 运算符, 实际值) 的语义是"实际值 op 阈值"，
            // 这里想让"施法方 op 目标方"成立，故把目标方当阈值传入。
            return GameUtility.CheckCondition(targetValue, @operator, actionValue);
        }
    }
}
