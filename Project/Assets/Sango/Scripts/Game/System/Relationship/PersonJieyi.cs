using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 仲介-结义
    /// 在本势力武将中挑选两名或三名同性武将结为异姓兄弟
    /// 已属于某个兄弟组的武将(含剧本原设兄弟, 已仲介结义)不会出现在候选列表中
    /// </summary>
    [GameSystem]
    public class PersonJieyi : PersonRelationship
    {
        /// <summary>
        /// 结义最少人数
        /// </summary>
        const int MinCount = 2;

        /// <summary>
        /// 结义最多人数
        /// </summary>
        const int MaxCount = 3;

        protected override string RelationshipTitle => "结义";
        protected override string RelationshipMenu => "君主/仲介/结义";
        protected override int RelationshipMenuOrder => 911;
        protected override int SelectLimit => MaxCount;

        protected override bool FilterCandidate(Person person)
        {
            // 已经在任何兄弟组中的一律排除
            return !person.HasSwornBrother;
        }

        /// <summary>
        /// 结义要求全员同性且非血亲: 勾了第一个武将之后,
        /// 列表只剩下与其同性又不是父母子女的人
        /// </summary>
        protected override bool FilterBySelected(SangoObject target, List<SangoObject> selected)
        {
            // 先过基类的血亲过滤(父母/子女一律隐藏)
            if (!base.FilterBySelected(target, selected)) return false;

            if (selected == null || selected.Count <= 0) return true;

            Person first = selected[0] as Person;
            Person person = target as Person;
            if (first == null || person == null) return true;

            return person.sex == first.sex;
        }

        protected override bool Validate(List<Person> selected, out string errorContent)
        {
            errorContent = null;

            if (selected.Count < MinCount)
            {
                errorContent = "结义至少需要选择两名武将!";
                return false;
            }

            if (selected.Count > MaxCount)
            {
                errorContent = $"结义最多只能选择{MaxCount}名武将!";
                return false;
            }

            int sex = selected[0].sex;
            for (int i = 1; i < selected.Count; i++)
            {
                // 结义不分男女, 但要求全员同性
                if (selected[i].sex != sex)
                {
                    errorContent = "结义必须是同性武将, 男女不能结义!";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 由即将担任组头(Id最小)的武将先开口, 其余武将依次应和
        /// </summary>
        protected override string GetTalkContent(Person speaker, List<Person> selected)
        {
            Person head = GetHead(selected);
            if (speaker == head)
                return "那我们就遵照陛下的命令，结下义兄弟的誓约吧。";

            return $"愿从{head.Name}兄之命，不求同年同月同日生，但求同年同月同日死！";
        }

        /// <summary>
        /// 按长幼顺序(组头在前)逐个弹窗
        /// </summary>
        protected override List<Person> GetTalkOrder(List<Person> selected)
        {
            List<Person> order = new List<Person>(selected);
            order.Sort((a, b) => a.Id.CompareTo(b.Id));
            return order;
        }

        /// <summary>
        /// 组头 = Id最小者, 与Person.SwornBrothers的结义规则一致
        /// </summary>
        static Person GetHead(List<Person> selected)
        {
            Person head = selected[0];
            for (int i = 1; i < selected.Count; i++)
            {
                if (selected[i].Id < head.Id)
                    head = selected[i];
            }
            return head;
        }

        /// <summary>
        /// 男性结义大图: 将领盟誓
        /// </summary>
        const string MaleImage = "Assets/UI/Texture/34-1.png";

        /// <summary>
        /// 女性结义大图: 山水云海
        /// </summary>
        const string FemaleImage = "Assets/UI/Texture/4851-1.png";

        protected override string TalkImage(List<Person> selected)
        {
            return IsFemaleGroup(selected) ? FemaleImage : MaleImage;
        }

        /// <summary>
        /// 在大图和武将应答之前, 先宣告结义之事
        /// </summary>
        protected override string GetNarration(List<Person> selected)
        {
            return $"{DateLine}{JoinNames(selected)}{(IsFemaleGroup(selected) ? "义结金兰。" : "结为异姓兄弟。")}";
        }

        /// <summary>
        /// 结义要求全员同性, 看第一个人就能定男/女
        /// </summary>
        static bool IsFemaleGroup(List<Person> picked)
        {
            return picked.Count > 0 && picked[0] != null && picked[0].sex == 1;
        }

        protected override void Execute(List<Person> selected)
        {
            Person.SwornBrothers(selected);

            string names = "";
            for (int i = 0; i < selected.Count; i++)
            {
                names += i == 0 ? selected[i].Name : "、" + selected[i].Name;
            }
            Sango.Log.Info($"@仲介@{names}结为异姓兄弟");
        }
    }
}
