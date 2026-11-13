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
        protected override int RelationshipMenuOrder => 4300;
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

        /// <summary>
        /// sex: 0男 1女
        /// </summary>
        static Person GetWoman(List<Person> selected)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                if (selected[i].sex == 1)
                    return selected[i];
            }
            return selected[0];
        }

        /// <summary>
        /// 结婚事件大图: 竹林庭院
        /// </summary>
        const string MarriageImage = "Assets/UI/Texture/66-1.png";

        protected override string TalkImage(List<Person> selected)
        {
            return MarriageImage;
        }

        /// <summary>
        /// 在大图和所有武将应答之前, 先由陛下宣告这门婚事
        /// </summary>
        protected override string GetNarration(List<Person> selected)
        {
            Person man = GetMan(selected);
            Person woman = GetWoman(selected);
            return $"{DateLine}{man.Name}与{woman.Name}喜结连理。";
        }

        protected override void Execute(List<Person> selected)
        {
            Person man = selected[0].sex == 0 ? selected[0] : selected[1];
            Person woman = selected[0].sex == 0 ? selected[1] : selected[0];

            man.AddSpouse(woman);
            woman.AddSpouse(man);

            // 【特技 100 内助】持有该特技的武将结婚时，双方五维永久 +2（原版 SkillId_Naijo）。
            // 写进 baseValue 而不是 PersonAttributeValue.extra_value：后者不在序列化字段里（
            // PersonAttributeValue 是 OptIn 序列化，ToArray 只存 5 个字段），读档会丢。
            if (man.HasFeature(FeatureIdNeijo) || woman.HasFeature(FeatureIdNeijo))
            {
                AddAllAttributes(man, 2);
                AddAllAttributes(woman, 2);
                Sango.Log.Info($"@内助@{man.Name}与{woman.Name}的能力各提升 2 点");
            }

            Sango.Log.Info($"@仲介@{man.Name}与{woman.Name}结为夫妻");
        }

        /// <summary>特技「内助」的 Id（Features.json #100）</summary>
        const int FeatureIdNeijo = 100;

        /// <summary>
        /// 五维基础值各 +value。
        /// 口径与 PersonSortFunction / UIPersonEdit / PersonAddAttributeEffect 一致：
        /// 改 baseValue 后调用 UpdateNoAge 重算派生值；上限按 100 封顶（原版五维上限）。
        /// </summary>
        static void AddAllAttributes(Person person, int value)
        {
            if (person == null || value == 0) return;
            AddOne(person.command, value);
            AddOne(person.strength, value);
            AddOne(person.intelligence, value);
            AddOne(person.politics, value);
            AddOne(person.glamour, value);
        }

        static void AddOne(PersonAttributeValue attr, int value)
        {
            if (attr == null) return;
            attr.baseValue = System.Math.Min(100, attr.baseValue + value);
            if (attr.baseValue < 0) attr.baseValue = 0;
            attr.UpdateNoAge();
        }
    }
}
