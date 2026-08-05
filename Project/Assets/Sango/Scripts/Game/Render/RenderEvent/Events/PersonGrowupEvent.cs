using Sango.Core;

namespace Sango.Render
{
    /// <summary>
    /// 武将子嗣长大成人事件 - 当子嗣年满16岁加入势力时触发
    /// 根据子嗣的能力类型和性别展示不同的对话
    /// </summary>
    public class PersonGrowupEvent : RenderEventBase
    {
        /// <summary>父武将</summary>
        public Person father;

        /// <summary>子嗣武将</summary>
        public Person person;

        /// <summary>对话实例</summary>
        private GameDialog.IDialog dialog;

        /// <summary>
        /// 子嗣才能类型枚举
        /// </summary>
        private enum ChildType
        {
            /// <summary>猛将之资 - 武力突出</summary>
            猛将之资,
            /// <summary>麒麟之资 - 五项全能</summary>
            麒麟之资,
            /// <summary>智谋之资 - 智力拔群</summary>
            智谋之资,
            /// <summary>统帅之资 - 统率卓越</summary>
            统帅之资,
            /// <summary>政务之资 - 政治突出</summary>
            政务之资,
            /// <summary>才智之资 - 智力较高</summary>
            才智之资,
            /// <summary>普通之资 - 各项平均</summary>
            普通之资
        }

        public override void Enter(Scenario scenario)
        {
            IsDone = false;

            bool isFemale = person != null && person.sex == 1;
            ChildType type = ClassifyChild(person);

            // 父亲禀报
            string fatherReport = GetFatherReport(isFemale, type);
            // 子嗣宣誓
            string childPledge = GetChildPledge(isFemale, type);

            dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, fatherReport,
            () =>
            {
                dialog = GameDialog.Open(GameDialog.DialogStyle.ClickPersonSay, childPledge,
                () =>
                {
                    GameMedia.Instance.PlayDoAcitonSfx();
                    GameDialog.Close();
                    IsDone = true;
                });
                if (person != null)
                    dialog.SetPerson(person);
            });
            if (father != null)
                dialog.SetPerson(father);
        }

        /// <summary>
        /// 根据五维属性判断子嗣的才能类型
        /// 判断优先级: 麒麟之资 > 猛将之资 > 统帅之资 > 智谋之资 > 政务之资 > 才智之资 > 普通之资
        /// </summary>
        /// <param name="p">子嗣武将</param>
        /// <returns>子嗣才能类型</returns>
        private ChildType ClassifyChild(Person p)
        {
            if (p == null) return ChildType.普通之资;

            int cmd = p.Command;
            int str = p.Strength;
            int intel = p.Intelligence;
            int pol = p.Politics;
            int gla = p.Glamour;

            int maxAttr = System.Math.Max(cmd,
                System.Math.Max(str,
                System.Math.Max(intel,
                System.Math.Max(pol, gla))));

            // 麒麟之资: 五项均 >= 70, 文武双全
            if (cmd >= 70 && str >= 70 && intel >= 70 && pol >= 70 && gla >= 70)
                return ChildType.麒麟之资;

            // 猛将之资: 武力 >= 85 且为最高
            if (str >= 85 && str == maxAttr)
                return ChildType.猛将之资;

            // 统帅之资: 统率 >= 85 且为最高
            if (cmd >= 85 && cmd == maxAttr)
                return ChildType.统帅之资;

            // 智谋之资: 智力 >= 85 且为最高
            if (intel >= 85 && intel == maxAttr)
                return ChildType.智谋之资;

            // 政务之资: 政治 >= 75 且为最高
            if (pol >= 75 && pol == maxAttr)
                return ChildType.政务之资;

            // 才智之资: 智力 >= 75 且为最高
            if (intel >= 75 && intel == maxAttr)
                return ChildType.才智之资;

            return ChildType.普通之资;
        }

        /// <summary>
        /// 获取父亲禀报的对话文本
        /// </summary>
        /// <param name="isFemale">子嗣性别是否为女性</param>
        /// <param name="type">子嗣才能类型</param>
        /// <returns>父亲禀报文本</returns>
        private string GetFatherReport(bool isFemale, ChildType type)
        {
            string fatherName = father != null ? father.ColorName : "臣";
            string childName = person != null ? person.ColorName : "此子";
            string childCall = isFemale ? "小女" : "犬子";

            switch (type)
            {
                case ChildType.麒麟之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子天资聪颖，文武兼备，无论是上阵杀敌还是运筹帷幄皆有不凡之资，"
                         + $"愿能为{childName}觅得建功立业之机。";

                case ChildType.猛将之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子天生神力，勇武过人，颇有{fatherName}当年之风范，"
                         + $"可为主公阵前杀敌，必不负主公所望。";

                case ChildType.智谋之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子自幼聪慧过人，才智非凡，善谋略、通韬略，"
                         + $"若假以时日，必成主公帐下得力智囊。";

                case ChildType.统帅之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子熟读兵书，深谙用兵之道，治军有方，"
                         + $"日后可为主公统领三军，镇守一方。";

                case ChildType.政务之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子勤勉好学，精通政务之道，善理民政赋税，"
                         + $"可为主公治理州郡，安定民生。";

                case ChildType.才智之资:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子机敏聪颖，心思缜密，颇有计谋之才，"
                         + $"可为主公出谋划策，共图大事。";

                case ChildType.普通之资:
                default:
                    return $"启禀主公，{childCall}{childName}现已长大成人。"
                         + $"此子虽非天纵之才，却也勤勉忠厚，"
                         + $"可效力于军中，助主公一臂之力。";
            }
        }

        /// <summary>
        /// 获取子嗣宣誓效忠的对话文本
        /// </summary>
        /// <param name="isFemale">子嗣性别是否为女性</param>
        /// <param name="type">子嗣才能类型</param>
        /// <returns>子嗣宣誓文本</returns>
        private string GetChildPledge(bool isFemale, ChildType type)
        {
            string childName = person != null ? person.ColorName : "在下";

            if (isFemale)
            {
                // 女性子嗣
                switch (type)
                {
                    case ChildType.猛将之资:
                        return $"{childName}虽为女子之身，却自幼习武不辍。"
                             + "愿为主公执剑策马，驰骋沙场，以报知遇之恩！";

                    case ChildType.麒麟之资:
                        return $"{childName}愿为主公尽展所学。"
                             + "文能提笔安天下，武能上马定乾坤，"
                             + "定不辜负主公厚望！";

                    case ChildType.智谋之资:
                        return $"{childName}虽无扛鼎之力，却有运筹之智。"
                             + "愿为明主出谋划策，助主公成就大业！";

                    case ChildType.统帅之资:
                        return $"{childName}承继家学，熟读兵书战策。"
                             + "愿为主公统领军阵，指挥若定，护我山河！";

                    case ChildType.政务之资:
                        return $"{childName}愿为主公分忧解劳，治理州郡。"
                             + "以民生为本，富国强兵，助主公安定一方。";

                    case ChildType.才智之资:
                        return $"{childName}愿效犬马之劳，"
                             + "以所学之智回报主公养育栽培之恩。";

                    case ChildType.普通之资:
                    default:
                        return $"{childName}虽才疏学浅，却有一片赤胆忠心。"
                             + "愿为主公赴汤蹈火，在所不辞！";
                }
            }
            else
            {
                // 男性子嗣
                switch (type)
                {
                    case ChildType.猛将之资:
                        return $"{childName}自幼习武，练就一身本领。"
                             + "愿为主公开疆拓土，斩将夺旗，万死不辞！";

                    case ChildType.麒麟之资:
                        return $"{childName}定当竭尽全力，不负所学。"
                             + "文韬武略皆为主公所用，共图天下霸业！";

                    case ChildType.智谋之资:
                        return $"{childName}虽不敢比肩古人，然胸有谋略。"
                             + "愿为主公运筹帷幄，决胜千里之外！";

                    case ChildType.统帅之资:
                        return $"{childName}承父辈之志，精研兵家之道。"
                             + "愿为主公领兵挂帅，荡平四海，一统寰宇！";

                    case ChildType.政务之资:
                        return $"{childName}愿以绵薄之力，为主公内修政理。"
                             + "使百姓安居乐业，后方固若金汤。";

                    case ChildType.才智之资:
                        return $"{childName}虽年少，愿勤学不倦。"
                             + "为主公帐下尽一己之智，共谋天下！";

                    case ChildType.普通之资:
                    default:
                        return $"{childName}愿为主公效命，"
                             + "鞍前马后，赴汤蹈火，在所不辞！";
                }
            }
        }
    }
}
