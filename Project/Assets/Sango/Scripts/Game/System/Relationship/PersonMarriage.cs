using System.Collections.Generic;

namespace Sango.Core.Player
{
    /// <summary>
    /// 仲介-结婚
    /// 在本势力武将中挑选两名(一男一女)结为夫妻, 与外交的"和亲"完全独立
    /// 已有配偶的武将(含剧本原配, 和亲, 已仲介结婚)不会出现在候选列表中
    /// </summary>
    [GameSystem]
    public class PersonMarriage : PersonRelationship
    {
        protected override string RelationshipTitle => "结婚";
        protected override string RelationshipMenu => "君主/仲介/结婚";
        protected override int RelationshipMenuOrder => 910;
        protected override int SelectLimit => 2;

        protected override bool FilterCandidate(Person person)
        {
            // 已有配偶的一律排除, 配偶信息本身就是唯一判定依据
            return !person.HasSpouse;
        }

        /// <summary>
        /// 结婚要求一男一女且非血亲: 勾了一个人之后,
        /// 列表只剩下其异性又不是父母子女的人
        /// </summary>
        protected override bool FilterBySelected(SangoObject target, List<SangoObject> selected)
        {
            // 先过基类的血亲过滤(父母/子女一律隐藏)
            if (!base.FilterBySelected(target, selected)) return false;

            if (selected == null || selected.Count <= 0) return true;

            Person first = selected[0] as Person;
            Person person = target as Person;
            if (first == null || person == null) return true;

            return person.sex != first.sex;
        }

        protected override bool Validate(List<Person> selected, out string errorContent)
        {
            errorContent = null;

            if (selected.Count != 2)
            {
                errorContent = "结婚需要选择两名武将!";
                return false;
            }

            Person man = null;
            Person woman = null;
            for (int i = 0; i < selected.Count; i++)
            {
                Person person = selected[i];
                // sex: 0男 1女
                if (person.sex == 0)
                {
                    if (man != null)
                    {
                        errorContent = "结婚必须是一男一女!";
                        return false;
                    }
                    man = person;
                }
                else
                {
                    if (woman != null)
                    {
                        errorContent = "结婚必须是一男一女!";
                        return false;
                    }
                    woman = person;
                }
            }

            if (man == null || woman == null)
            {
                errorContent = "结婚必须是一男一女!";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 男方先开口, 女方随后应允
        /// </summary>
        protected override string GetTalkContent(Person speaker, List<Person> selected)
        {
            Person man = GetMan(selected);
            if (speaker == man)
                return "那我们就遵照陛下的命令，结为夫妻吧。";

            return $"妾身谨遵陛下旨意，愿与{man.Name}结为连理，琴瑟和鸣。";
        }

        /// <summary>
        /// 男方在前, 女方在后逐个弹窗
        /// </summary>
        protected override List<Person> GetTalkOrder(List<Person> selected)
        {
            List<Person> order = new List<Person>();
            Person man = GetMan(selected);
            order.Add(man);
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i] != man)
                    order.Add(selected[i]);
            }
            return order;
        }

        /// <summary>
        /// sex: 0男 1女
        /// </summary>
        static Person GetMan(List<Person> selected)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].sex == 0)
                    return selected[i];
            }
            return selected[0];
        }

        protected override void Execute(List<Person> selected)
        {
            Person man = selected[0].sex == 0 ? selected[0] : selected[1];
            Person woman = selected[0].sex == 0 ? selected[1] : selected[0];

            man.AddSpouse(woman);
            woman.AddSpouse(man);

            Sango.Log.Info($"@仲介@{man.Name}与{woman.Name}结为夫妻");
        }
    }
}
