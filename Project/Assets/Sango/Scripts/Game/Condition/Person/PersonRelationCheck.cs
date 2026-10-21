using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 武将关系检查条件。
    ///
    /// 参数：
    ///   relationType 关系类型（字符串，见下方取值表；非法值一律视为不成立）
    ///   result       期望结果（1 期望"是" / 0 期望"不是"），缺省 1
    ///
    /// 参与判定的两个人固定取 database.ActionPerson（行动方）与 database.TargetPerson（目标方）。
    /// TargetPerson 为 null 时（战法命中建筑、部队属性重算等没有"对方武将"的上下文）
    /// 直接返回 false —— 关系判定必须两个武将都在场，"是夫妻"与"不是夫妻"才有意义。
    ///
    /// 各关系类型的取数来源（与 Person 现有实现保持一致，避免编辑器语义漂移）：
    ///   spouse        Person.IsSpouse           夫妻，双向
    ///   sibling       Father / Mother 相同      同胞兄弟姊妹（含同父异母 / 同母异父）
    ///   swornBrother  Person.IsBrotherGroupmate 结义兄弟（同 BrotherId 组，天生对称）
    ///   parentChild   Person.IsParentchild      父母子女（含过继），双向
    ///   blood         Person.IsBloodRelative    血亲：亲子 / 同胞 / 三代内有共同祖先
    ///   like          Person.IsLike             行动方亲爱目标方（单向）
    ///   likeEither    任一方向 IsLike           互相亲爱（双向）
    ///   hate          Person.IsHate             行动方厌恶目标方（单向）
    ///   hatedEither   Person.IsHatedByEither    任一方厌恶另一方（双向）
    ///
    /// 注意：
    ///   · 同胞与义兄弟在数据里共用 BrotherId，无法用组号区分，
    ///     所以 sibling 只看父母字段；要判"结义"请用 swornBrother。
    ///   · Person.IsBrother 不对称（幼弟的列表里可能没有组头），本类不暴露该方法，
    ///     需要"同组"语义时统一走 IsBrotherGroupmate。
    /// </summary>
    public class PersonRelationCheck : Condition
    {
        /// <summary>关系类型（字符串，非法值视为不成立）</summary>
        string relationType;

        /// <summary>期望结果：1 期望"是"，0 期望"不是"</summary>
        int result;

        /// <summary>
        /// 初始化武将关系检查条件
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            relationType = p.Value<string>("relationType") ?? "spouse";
            // 用可空取值：p.Value<int> 缺失时返回 0，会把"期望是"误判成"期望不是"
            result = p.Value<int?>("result") ?? 1;
        }

        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="database">数据实例</param>
        /// <returns>条件是否满足</returns>
        public override bool Check(IConditionDatabase database)
        {
            Person self = database.ActionPerson;
            Person target = database.TargetPerson;

            // 缺人时一律不成立（与其它 Person*Check 的处理保持一致）
            if (self == null || target == null)
                return false;

            // 自己与自己不构成任何人际关系：matched 保持 false，
            // 这样"期望不是"的写法在同人上下文里仍然成立
            bool matched = false;
            if (self != target)
            {
                switch (relationType)
                {
                    case "spouse":
                        matched = self.IsSpouse(target);
                        break;
                    case "sibling":
                        matched = IsSibling(self, target);
                        break;
                    case "swornBrother":
                        matched = self.IsBrotherGroupmate(target);
                        break;
                    case "parentChild":
                        matched = self.IsParentchild(target);
                        break;
                    case "blood":
                        matched = self.IsBloodRelative(target);
                        break;
                    case "like":
                        matched = self.IsLike(target);
                        break;
                    case "likeEither":
                        matched = self.IsLike(target) || target.IsLike(self);
                        break;
                    case "hate":
                        matched = self.IsHate(target);
                        break;
                    case "hatedEither":
                        matched = Person.IsHatedByEither(self, target);
                        break;
                    default:
                        // 配错类型名时宁可判不成立，也不要静默放行
                        return false;
                }
            }

            return result == 1 ? matched : !matched;
        }

        /// <summary>
        /// 同胞兄弟姊妹：同一位父亲或同一位母亲（含同父异母 / 同母异父）。
        /// 不看 BrotherId —— 该字段被结义复用，无法区分血亲与义兄弟。
        /// </summary>
        /// <param name="a">行动方武将</param>
        /// <param name="b">目标方武将</param>
        /// <returns>是否同胞</returns>
        static bool IsSibling(Person a, Person b)
        {
            if (a == null || b == null || a == b) return false;
            if (a.Father != null && a.Father == b.Father) return true;
            if (a.Mother != null && a.Mother == b.Mother) return true;
            return false;
        }
    }
}
