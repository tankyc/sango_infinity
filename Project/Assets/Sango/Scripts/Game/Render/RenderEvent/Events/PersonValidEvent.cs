using Sango.Core;

namespace Sango.Render
{
    /// <summary>
    /// 武将登场事件 - 当未登场武将满足条件变为在野状态时触发
    /// 根据武将属性类型展示不同的传闻文本
    /// </summary>
    public class PersonValidEvent : RenderEventBase
    {
        /// <summary>武将所在州</summary>
        public Province province;

        /// <summary>目标武将</summary>
        public Person person;

        /// <summary>对话实例</summary>
        private GameDialog.IDialog dialog;

        /// <summary>
        /// 武将属性类型枚举 - 根据五维属性判断武将的定位
        /// </summary>
        private enum PersonType
        {
            /// <summary>武力最高 - 武力突出且为五项中最高</summary>
            武力最高,
            /// <summary>麒麟儿 - 五项全能,文武兼备的奇才</summary>
            麒麟儿,
            /// <summary>智力最高 - 智力拔群且为五项中最高</summary>
            智力最高,
            /// <summary>统率最高 - 统率卓越且为五项中最高</summary>
            统率最高,
            /// <summary>政治型 - 政治能力突出且为五项中最高</summary>
            政治型,
            /// <summary>智力型 - 智力较高且为五项中最高,但未达顶尖水平</summary>
            智力型,
            /// <summary>一般 - 各项属性较为平均,无突出特长</summary>
            一般
        }

        public override void Enter(Scenario scenario)
        {
            IsDone = false;

            // 根据属性获取武将类型,匹配不同传闻
            PersonType type = ClassifyPerson(person);
            string rumorText = GetRumorText(type);

            dialog = GameDialog.Open(GameDialog.DialogStyle.ClickSay, rumorText,
            () =>
            {
                GameDialog.Close();
                IsDone = true;
            });
        }

        /// <summary>
        /// 根据武将五维属性判断其类型
        /// 判断优先级: 麒麟儿 > 武力最高 > 统率最高 > 智力最高 > 政治型 > 智力型 > 一般
        /// </summary>
        /// <param name="p">待分类的武将</param>
        /// <returns>武将属性类型</returns>
        private PersonType ClassifyPerson(Person p)
        {
            int cmd = p.Command;
            int str = p.Strength;
            int intel = p.Intelligence;
            int pol = p.Politics;
            int gla = p.Glamour;

            // 计算五项属性中的最高值
            int maxAttr = System.Math.Max(cmd,
                System.Math.Max(str,
                System.Math.Max(intel,
                System.Math.Max(pol, gla))));

            // 麒麟儿: 五项属性均在80以上,文武双全的稀世奇才
            if (cmd >= 80 && str >= 80 && intel >= 80 && pol >= 80 && gla >= 80)
                return PersonType.麒麟儿;

            // 武力最高: 武力 >= 90 且为五项中最高,万夫不当的猛将
            if (str >= 90 && str == maxAttr)
                return PersonType.武力最高;

            // 统率最高: 统率 >= 90 且为五项中最高,精通兵法的统帅
            if (cmd >= 90 && cmd == maxAttr)
                return PersonType.统率最高;

            // 智力最高: 智力 >= 90 且为五项中最高,神机妙算的奇才
            if (intel >= 90 && intel == maxAttr)
                return PersonType.智力最高;

            // 政治型: 政治 >= 80 且为五项中最高,精通政务的能臣
            if (pol >= 80 && pol == maxAttr)
                return PersonType.政治型;

            // 智力型: 智力 >= 80 且为五项中最高,足智多谋的策士
            if (intel >= 80 && intel == maxAttr)
                return PersonType.智力型;

            // 一般: 各项属性平均,无突出特长
            return PersonType.一般;
        }

        /// <summary>
        /// 根据武将类型获取对应的传闻文本
        /// </summary>
        /// <param name="type">武将属性类型</param>
        /// <returns>传闻描述文本</returns>
        private string GetRumorText(PersonType type)
        {
            string provinceName = province != null ? province.ColorName : "某地";

            switch (type)
            {
                case PersonType.武力最高:
                    return $"据传闻，在{provinceName}出现了一名豪杰。"
                         + "此人拥有万夫不当之勇，力量无人能及，堪称当代无双的猛将。";

                case PersonType.麒麟儿:
                    return $"据传闻，在{provinceName}出现了一名稀世奇才。"
                         + "精通文武两道，无论是统兵作战还是治国安邦，皆有过人之能，"
                         + "世人称之为麒麟儿。";

                case PersonType.智力最高:
                    return $"据传闻，在{provinceName}出现了一名奇才。"
                         + "此人神机妙算，运筹帷幄之中，决胜千里之外，"
                         + "其智谋堪比古之名军师。";

                case PersonType.统率最高:
                    return $"据传闻，在{provinceName}出现了一名帅才。"
                         + "精通兵法韬略，治军严明，善于调兵遣将，"
                         + "是一位不可多得的统军大将。";

                case PersonType.政治型:
                    return $"据传闻，在{provinceName}出现了一名能臣。"
                         + "精通政务，长于治国安民，善于经营之道，"
                         + "是一位可堪大任的政治人才。";

                case PersonType.智力型:
                    return $"据传闻，在{provinceName}出现了一名策士。"
                         + "此人足智多谋，善于出谋划策，"
                         + "常能于危急之际想出破敌之策。";

                case PersonType.一般:
                default:
                    return $"据传闻，在{provinceName}出现了一名有才能的人。"
                         + "据说其具有不俗的能力，值得关注。";
            }
        }
    }
}
