using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
    public class PersonLibSortFunction : Singleton<PersonLibSortFunction>
    {
        public delegate string PersonValueStrGet(PersonLib person);
        public delegate int PersonValueGet(PersonLib person);
        public delegate int PersonSortFunc(PersonLib person1, PersonLib person2);

        /// <summary>
        /// 获取Person对象属性值的object类型代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonValueObjGet(PersonLib person);

        /// <summary>
        /// 设置Person对象属性值的代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonValueObjSet(PersonLib person, object value);

        public class SortTitle : ObjectSortTitle
        {
            public PersonValueStrGet valueGetCall;
            public PersonSortFunc personSortFunc;
            public PersonValueObjGet valueObjGet;
            public PersonValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((PersonLib)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((PersonLib)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueGetCall.Invoke((PersonLib)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return personSortFunc.Invoke((PersonLib)a, (PersonLib)b);
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueGetCall = valueGetCall,
                    personSortFunc = personSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                };
            }
        }

        public static SortTitle SortByName = new SortTitle()
        {
            name = "武将",
            width = 4.20f,
            valueGetCall = x => x.Name,
            personSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
        };

        public static SortTitle SortByCommand = new SortTitle()
        {
            name = "统率",
            width = 2.00f,
            valueGetCall = x => x.command.ToString(),
            personSortFunc = (a, b) => a.command.CompareTo(b.command),
            valueObjGet = x => x.command,
            valueObjSet = null,
        };

        public static SortTitle SortByStrength = new SortTitle()
        {
            name = "武力",
            width = 2.00f,
            valueGetCall = x => x.strength.ToString(),
            personSortFunc = (a, b) => a.strength.CompareTo(b.strength),
            valueObjGet = x => x.strength,
            valueObjSet = null,
        };

        public static SortTitle SortByIntelligence = new SortTitle()
        {
            name = "智力",
            width = 2.00f,
            valueGetCall = x => x.intelligence.ToString(),
            personSortFunc = (a, b) => -a.intelligence.CompareTo(b.intelligence),
            valueObjGet = x => x.intelligence,
            valueObjSet = null,
        };

        public static SortTitle SortByPolitics = new SortTitle()
        {
            name = "政治",
            width = 2.00f,
            valueGetCall = x => x.politics.ToString(),
            personSortFunc = (a, b) => b.politics.CompareTo(a.politics),
            valueObjGet = x => x.politics,
            valueObjSet = null,
        };

        public static SortTitle SortByGlamour = new SortTitle()
        {
            name = "魅力",
            width = 2.00f,
            valueGetCall = x => x.glamour.ToString(),
            personSortFunc = (a, b) => a.glamour.CompareTo(b.glamour),
            valueObjGet = x => x.glamour,
            valueObjSet = null,
        };

        public static SortTitle SortBySpearLv = new SortTitle()
        {
            name = "枪兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.spearLv),
            personSortFunc = (a, b) => a.spearLv.CompareTo(b.spearLv),
            valueObjGet = x => x.spearLv,
            valueObjSet = null,
        };

        public static SortTitle SortByHalberdLv = new SortTitle()
        {
            name = "戟兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.halberdLv),
            personSortFunc = (a, b) => a.halberdLv.CompareTo(b.halberdLv),
            valueObjGet = x => x.halberdLv,
            valueObjSet = null,
        };

        public static SortTitle SortByCrossbowLv = new SortTitle()
        {
            name = "弓兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.crossbowLv),
            personSortFunc = (a, b) => a.crossbowLv.CompareTo(b.crossbowLv),
            valueObjGet = x => x.crossbowLv,
            valueObjSet = null,
        };

        public static SortTitle SortByRideLv = new SortTitle()
        {
            name = "骑兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.rideLv),
            personSortFunc = (a, b) => a.rideLv.CompareTo(b.rideLv),
            valueObjGet = x => x.rideLv,
            valueObjSet = null,
        };

        public static SortTitle SortByWaterLv = new SortTitle()
        {
            name = "水军",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.waterLv),
            personSortFunc = (a, b) => a.waterLv.CompareTo(b.waterLv),
            valueObjGet = x => x.waterLv,
            valueObjSet = null,
        };

        public static SortTitle SortByMachineLv = new SortTitle()
        {
            name = "兵器",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.machineLv),
            personSortFunc = (a, b) => a.machineLv.CompareTo(b.machineLv),
            valueObjGet = x => x.machineLv,
            valueObjSet = null,
        };

        //public static SortTitle SortByFeatureList = new SortTitle()
        //{
        //    name = "特技",
        //    width = 6.00f,
        //    alignment = (int)TextAnchor.MiddleLeft,
        //    valueGetCall = x =>
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        if (x.FeatureList != null)
        //        {
        //            for (int i = 0; i < x.FeatureList.Length; i++)
        //            {
        //                sb.Append(x.FeatureList[i].Name);
        //                if (i < x.FeatureList.Count - 1)
        //                    sb.Append(", ");
        //            }
        //        }
        //        return sb.ToString();
        //    },
        //    personSortFunc = (a, b) =>
        //    {
        //        if (a.FeatureList == null && b.FeatureList == null)
        //            return 0;
        //        if (a.FeatureList != null && b.FeatureList == null)
        //            return -1;
        //        if (a.FeatureList == null && b.FeatureList != null)
        //            return 1;
        //        return a.FeatureList.Count.CompareTo(b.FeatureList.Count);
        //    },
        //    valueObjGet = null,
        //    valueObjSet = null,
        //};

        //public static SortTitle SortByFeatureDesc = new SortTitle()
        //{
        //    name = "说明",
        //    width = 30.00f,
        //    valueGetCall = x =>
        //    {
        //        if (x.FeatureList == null || x.FeatureList.Count == 0)
        //            return string.Empty;

        //        StringBuilder sb = new StringBuilder();
        //        for (int i = 0; i < x.FeatureList.Count; i++)
        //        {
        //            var feat = x.FeatureList[i];
        //            sb.Append(feat.desc ?? string.Empty);
        //            if (i < x.FeatureList.Count - 1)
        //                sb.Append("\n");
        //        }
        //        return sb.ToString();
        //    },
        //    personSortFunc = (a, b) => 0,
        //    valueObjGet = null,
        //    valueObjSet = null,
        //};

        public static SortTitle SortBySex = new SortTitle()
        {
            name = "性别",
            width = 1.30f,
            valueGetCall = x => x.sex == 0 ? "男" : "女",
            personSortFunc = (a, b) => a.sex.CompareTo(b.sex),
            valueObjGet = x => x.sex,
            valueObjSet = (x, v) => x.sex = (int)v,
        };

        public static SortTitle SortByMerit = new SortTitle()
        {
            name = "功绩",
            width = 4.00f,
            valueGetCall = x => x.merit.ToString(),
            personSortFunc = (a, b) => a.merit.CompareTo(b.merit),
            valueObjGet = x => x.merit,
            valueObjSet = (x, v) => x.merit = (int)v,
        };

        public static SortTitle SortByExp = new SortTitle()
        {
            name = "经验",
            width = 2.00f,
            valueGetCall = x => x.Exp.ToString(),
            personSortFunc = (a, b) => a.Exp.CompareTo(b.Exp),
            valueObjGet = x => x.Exp,
            valueObjSet = null,
        };

        public static SortTitle SortByDescription = new SortTitle()
        {
            name = "身平",
            width = 2.00f,
            valueGetCall = x => GameLanguage.GetString(x.Id),
            personSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByFamilyName = new SortTitle()
        {
            name = "姓",
            width = 2.00f,
            valueGetCall = x => x.familyName,
            personSortFunc = (a, b) => a.familyName.CompareTo(b.familyName),
            valueObjGet = x => x.familyName,
            valueObjSet = (x, v) => x.familyName = (string)v,
        };

        public static SortTitle SortByGiveName = new SortTitle()
        {
            name = "名",
            width = 2.00f,
            valueGetCall = x => x.giveName,
            personSortFunc = (a, b) => a.giveName.CompareTo(b.giveName),
            valueObjGet = x => x.giveName,
            valueObjSet = (x, v) => x.giveName = (string)v,
        };

        public static SortTitle SortByNickName = new SortTitle()
        {
            name = "字",
            width = 2.00f,
            valueGetCall = x => x.nickName,
            personSortFunc = (a, b) => a.nickName.CompareTo(b.nickName),
            valueObjGet = x => x.nickName,
            valueObjSet = (x, v) => x.nickName = (string)v,
        };

        public static SortTitle SortByYearAvailable = new SortTitle()
        {
            name = "登场年",
            width = 2.00f,
            valueGetCall = x => x.yearAvailable.ToString(),
            personSortFunc = (a, b) => a.yearAvailable.CompareTo(b.yearAvailable),
            valueObjGet = x => x.yearAvailable,
            valueObjSet = (x, v) => x.yearAvailable = (int)v,
        };

        public static SortTitle SortByYearBorn = new SortTitle()
        {
            name = "出生年",
            width = 2.50f,
            valueGetCall = x => x.yearBorn.ToString(),
            personSortFunc = (a, b) => a.yearBorn.CompareTo(b.yearBorn),
            valueObjGet = x => x.yearBorn,
            valueObjSet = (x, v) => x.yearBorn = (int)v,
        };

        public static SortTitle SortByYearDead = new SortTitle()
        {
            name = "死亡年",
            width = 2.50f,
            valueGetCall = x => x.yearDead.ToString(),
            personSortFunc = (a, b) => a.yearDead.CompareTo(b.yearDead),
            valueObjGet = x => x.yearDead,
            valueObjSet = (x, v) => x.yearDead = (int)v,
        };

        public static SortTitle SortByCompatibility = new SortTitle()
        {
            name = "相性",
            width = 2.00f,
            valueGetCall = x => x.compatibility.ToString(),
            personSortFunc = (a, b) => a.compatibility.CompareTo(b.compatibility),
            valueObjGet = x => x.compatibility,
            valueObjSet = (x, v) => x.compatibility = (int)v,
        };

        public static SortTitle SortByState = new SortTitle()
        {
            name = "身份",
            width = 2.80f,
            valueGetCall = x =>
            {
                if (x == null) return "未知";
                switch (x.state)
                {
                    case 1: return "君主";
                    case 2: return "都督";
                    case 3: return "太守";
                    case 4: return "一般";
                    case 5: return "在野";
                    case 6: return "俘虏";
                    case 7: return "未登场";
                    case 8: return "未发现";
                    case 9: return "死亡";
                    default: return x.state.ToString();
                }
            },
            personSortFunc = (a, b) => a.state.CompareTo(b.state),
            valueObjGet = x => x.state,
            valueObjSet = (x, v) => x.state = (int)v,
        };

        public static SortTitle SortByStamina = new SortTitle()
        {
            name = "体力",
            width = 2.00f,
            valueGetCall = x => x.stamina.ToString(),
            personSortFunc = (a, b) => a.stamina.CompareTo(b.stamina),
            valueObjGet = x => x.stamina,
            valueObjSet = (x, v) => x.stamina = (int)v,
        };

        // 性格（小写！！！）
        //public static SortTitle SortByPersonality = new SortTitle()
        //{
        //    name = "性格",
        //    width = 2.00f,
        //    valueGetCall = x => x == null || x.personality == null ? "—" : x.personality.Name,
        //    personSortFunc = (a, b) =>
        //    {
        //        string aName = a?.personality?.Name ?? "";
        //        string bName = b?.personality?.Name ?? "";
        //        return aName.CompareTo(bName);
        //    },
        //    valueObjGet = x => x.personality,
        //    valueObjSet = (x, v) => x.personality = (Personality)v,
        //};


        //public static SortTitle SortByFather = new SortTitle()
        //{
        //    name = "父亲",
        //    width = 2.40f,
        //    valueGetCall = x => x == null || x.Father == null ? " " : x.Father.Name,
        //    personSortFunc = (a, b) => SangoObject.Compare(a?.Father, b?.Father),
        //    valueObjGet = x => x.Father,
        //    valueObjSet = (x, v) => x.Father = (Person)v,
        //};

        //public static SortTitle SortByMother = new SortTitle()
        //{
        //    name = "母亲",
        //    width = 2.40f,
        //    valueGetCall = x => x == null || x.Mother == null ? " " : x.Mother.Name,
        //    personSortFunc = (a, b) => SangoObject.Compare(a?.Mother, b?.Mother),
        //    valueObjGet = x => x.Mother,
        //    valueObjSet = (x, v) => x.Mother = (Person)v,
        //};

        //public static SortTitle SortByBrother = new SortTitle()
        //{
        //    name = "兄弟",
        //    width = 7.20f,
        //    valueGetCall = x =>
        //    {
        //        if (x == null) return " ";
        //        if (x.BrotherList == null || x.BrotherList.Count == 0) return " ";

        //        var names = new System.Collections.Generic.List<string>();
        //        foreach (Person brother in x.BrotherList)
        //        {
        //            if (brother != null) names.Add(brother.Name);
        //        }
        //        return names.Count == 0 ? " " : string.Join("，", names);
        //    },
        //    personSortFunc = (a, b) =>
        //    {
        //        if (a.BrotherList != null && b.BrotherList != null)
        //        {
        //            return a.BrotherList.Count.CompareTo(b.BrotherList.Count);
        //        }

        //        if (a.BrotherList != null)
        //            return 1;

        //        if (b.BrotherList != null)
        //            return -1;

        //        return 0;
        //    },
        //    valueObjGet = null,
        //    valueObjSet = null,
        //};

        //public static SortTitle SortBySpouse = new SortTitle()
        //{
        //    name = "配偶",
        //    width = 7.20f,
        //    valueGetCall = x =>
        //    {
        //        if (x == null) return " ";
        //        if (x.SpouseList == null || x.SpouseList.Count == 0) return " ";

        //        var names = new System.Collections.Generic.List<string>();
        //        foreach (Person spouse in x.SpouseList)
        //        {
        //            if (spouse != null) names.Add(spouse.Name);
        //        }
        //        return names.Count == 0 ? " " : string.Join("，", names);
        //    },
        //    personSortFunc = (a, b) =>
        //    {
        //        if (a.SpouseList != null && b.SpouseList != null)
        //        {
        //            return a.SpouseList.Count.CompareTo(b.SpouseList.Count);
        //        }

        //        if (a.SpouseList != null)
        //            return 1;

        //        if (b.SpouseList != null)
        //            return -1;

        //        return 0;
        //    },
        //    valueObjGet = null,
        //    valueObjSet = null,
        //};
    }

}
