using Sango.Core.Player;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Sango.Core
{
    public enum PersonSortTileType : int
    {
        Name = 0,

    }

    public enum PersonSortGroupType : int
    {
        //自定义,功能独有
        Custom = 0,
        //所属
        Belong,
        //能力
        Attribute,
        //特技
        Feature,
        //适应
        Ability,
        //任务
        Mission,
        //个人
        Personal,
        //血缘
        Consanguinity,

        Max
    }

    public class PersonSortFunction : Singleton<PersonSortFunction>
    {
        public delegate string PersonValueStrGet(Person person);
        public delegate int PersonValueGet(Person person);
        public delegate int PersonSortFunc(Person person1, Person person2);

        /// <summary>
        /// 获取Person对象属性值的object类型代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>属性值</returns>
        public delegate object PersonValueObjGet(Person person);

        /// <summary>
        /// 设置Person对象属性值的代理
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void PersonValueObjSet(Person person, object value);

        public class SortTitle : ObjectSortTitle
        {
            public PersonValueStrGet valueGetCall;
            public PersonSortFunc personSortFunc;
            public PersonValueObjGet valueObjGet;
            public PersonValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueObjGet?.Invoke((Person)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Person)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueGetCall.Invoke((Person)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return personSortFunc.Invoke((Person)a, (Person)b);
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

        public void GetSortTitleGroup(PersonSortGroupType personSortTileGroupType, List<ObjectSortTitle> titleList)
        {
            switch (personSortTileGroupType)
            {
                case PersonSortGroupType.Belong:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByBelongForce);
                        titleList.Add(SortByBelongCorps);
                        titleList.Add(SortByBelongCity);
                        titleList.Add(SortByCurrentCity);
                        titleList.Add(SortByState);
                        titleList.Add(SortByIsCityLeader);
                        titleList.Add(SortByLoyalty);
                        titleList.Add(SortByMerit);
                        break;
                    }
                case PersonSortGroupType.Attribute:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByState);
                        //                        titleList.Add(SortByTroopsLimit);//删
                        titleList.Add(SortByCommand);
                        titleList.Add(SortByStrength);
                        titleList.Add(SortByIntelligence);
                        titleList.Add(SortByPolitics);
                        titleList.Add(SortByGlamour);
                        titleList.Add(SortByStamina);
                        //剧本缺 伤病、道具  保留空位
                        //                        titleList.Add(SortByFeatureList);//删
                        break;
                    }
                case PersonSortGroupType.Feature:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByFeatureList);
                        titleList.Add(SortByFeatureDesc);
                        break;
                    }
                case PersonSortGroupType.Ability:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortBySpearLv);
                        titleList.Add(SortByHalberdLv);
                        titleList.Add(SortByCrossbowLv);
                        titleList.Add(SortByRideLv);
                        titleList.Add(SortByMachineLv);
                        titleList.Add(SortByWaterLv);
                        //                        titleList.Add(SortByFeatureList);//删
                        break;
                    }
                case PersonSortGroupType.Mission:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByMissionType);
                        titleList.Add(SortByMissionTarget);
                        //                        titleList.Add(GetSortByDistanceDay);
                        titleList.Add(SortByAction);
                        break;
                    }
                case PersonSortGroupType.Personal:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByOfficial);
                        titleList.Add(SortByTroopsLimit);
                        //剧本缺 俸禄 保留空位
                        titleList.Add(SortByPersonality);
                        titleList.Add(SortByAge);
                        titleList.Add(SortBySex);
                        break;
                    }
                case PersonSortGroupType.Consanguinity:
                    {
                        titleList.Add(SortByName);
                        titleList.Add(SortByFather);
                        titleList.Add(SortByMother);
                        titleList.Add(SortByBrother);
                        titleList.Add(SortBySpouse);
                        break;
                    }
            }
        }

        public string GetSortTitleGroupName(PersonSortGroupType personSortTileGroupType)
        {
            switch (personSortTileGroupType)
            {
                case PersonSortGroupType.Belong: return "所属";
                case PersonSortGroupType.Attribute: return "能力";
                case PersonSortGroupType.Feature: return "特技";
                case PersonSortGroupType.Ability: return "适应";
                case PersonSortGroupType.Mission: return "任务";
                case PersonSortGroupType.Personal: return "个人";
                case PersonSortGroupType.Consanguinity: return "血缘";
            }

            return "";
        }

        public static SortTitle SortByName = new SortTitle()
        {
            name = "武将",
            width = 3.20f,
            valueGetCall = x => x.Name,
            personSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = (x, v) => x.Name = (string)v,
        };

        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "指挥",
            width = 2.80f,
            valueGetCall = x => x.TroopsLimit.ToString(),
            personSortFunc = (a, b) => a.TroopsLimit.CompareTo(b.TroopsLimit),
            valueObjGet = x => x.TroopsLimit,
            valueObjSet = null,
        };

        public static SortTitle SortByCommand = new SortTitle()
        {
            name = "统率",
            width = 2.00f,
            valueGetCall = x => x.Command.ToString(),
            personSortFunc = (a, b) => a.Command.CompareTo(b.Command),
            valueObjGet = x => x.Command,
            valueObjSet = null,
        };

        public static SortTitle SortByStrength = new SortTitle()
        {
            name = "武力",
            width = 2.00f,
            valueGetCall = x => x.Strength.ToString(),
            personSortFunc = (a, b) => a.Strength.CompareTo(b.Strength),
            valueObjGet = x => x.Strength,
            valueObjSet = null,
        };

        public static SortTitle SortByIntelligence = new SortTitle()
        {
            name = "智力",
            width = 2.00f,
            valueGetCall = x => x.Intelligence.ToString(),
            personSortFunc = (a, b) => -a.Intelligence.CompareTo(b.Intelligence),
            valueObjGet = x => x.Intelligence,
            valueObjSet = null,
        };

        public static SortTitle SortByPolitics = new SortTitle()
        {
            name = "政治",
            width = 2.00f,
            valueGetCall = x => x.Politics.ToString(),
            personSortFunc = (a, b) => b.Politics.CompareTo(a.Politics),
            valueObjGet = x => x.Politics,
            valueObjSet = null,
        };

        public static SortTitle SortByGlamour = new SortTitle()
        {
            name = "魅力",
            width = 2.00f,
            valueGetCall = x => x.Glamour.ToString(),
            personSortFunc = (a, b) => a.Glamour.CompareTo(b.Glamour),
            valueObjGet = x => x.Glamour,
            valueObjSet = null,
        };

        public static SortTitle SortByMilitaryAbility = new SortTitle()
        {
            name = "军事",
            width = 2.00f,
            valueGetCall = x => x.MilitaryAbility.ToString(),
            personSortFunc = (a, b) => a.MilitaryAbility.CompareTo(b.MilitaryAbility),
            valueObjGet = x => x.MilitaryAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseCommerceAbility = new SortTitle()
        {
            name = "商业",
            width = 2.00f,
            valueGetCall = x => x.BaseCommerceAbility.ToString(),
            personSortFunc = (a, b) => a.BaseCommerceAbility.CompareTo(b.BaseCommerceAbility),
            valueObjGet = x => x.BaseCommerceAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseSecurityAbility = new SortTitle()
        {
            name = "治安",
            width = 2.00f,
            valueGetCall = x => x.BaseSecurityAbility.ToString(),
            personSortFunc = (a, b) => a.BaseSecurityAbility.CompareTo(b.BaseSecurityAbility),
            valueObjGet = x => x.BaseSecurityAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseTrainTroopAbility = new SortTitle()
        {
            name = "训练",
            width = 2.00f,
            valueGetCall = x => x.BaseTrainTroopAbility.ToString(),
            personSortFunc = (a, b) => a.BaseTrainTroopAbility.CompareTo(b.BaseTrainTroopAbility),
            valueObjGet = x => x.BaseTrainTroopAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseAgricultureAbility = new SortTitle()
        {
            name = "农业",
            width = 2.00f,
            valueGetCall = x => x.BaseAgricultureAbility.ToString(),
            personSortFunc = (a, b) => a.BaseAgricultureAbility.CompareTo(b.BaseAgricultureAbility),
            valueObjGet = x => x.BaseAgricultureAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseBuildAbility = new SortTitle()
        {
            name = "建设",
            width = 2.00f,
            valueGetCall = x => x.BaseBuildAbility.ToString(),
            personSortFunc = (a, b) => a.BaseBuildAbility.CompareTo(b.BaseBuildAbility),
            valueObjGet = x => x.BaseBuildAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseCreativeAbility = new SortTitle()
        {
            name = "生产",
            width = 2.00f,
            valueGetCall = x => x.BaseCreativeAbility.ToString(),
            personSortFunc = (a, b) => a.BaseCreativeAbility.CompareTo(b.BaseCreativeAbility),
            valueObjGet = x => x.BaseCreativeAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseSearchingAbility = new SortTitle()
        {
            name = "搜寻",
            width = 2.00f,
            valueGetCall = x => x.BaseSearchingAbility.ToString(),
            personSortFunc = (a, b) => a.BaseSearchingAbility.CompareTo(b.BaseSearchingAbility),
            valueObjGet = x => x.BaseSearchingAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseRecruitmentAbility = new SortTitle()
        {
            name = "招募",
            width = 2.00f,
            valueGetCall = x => x.BaseRecruitmentAbility.ToString(),
            personSortFunc = (a, b) => a.BaseRecruitmentAbility.CompareTo(b.BaseRecruitmentAbility),
            valueObjGet = x => x.BaseRecruitmentAbility,
            valueObjSet = null,
        };

        public static SortTitle SortBySpearLv = new SortTitle()
        {
            name = "枪兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.SpearLv),
            personSortFunc = (a, b) => a.SpearLv.CompareTo(b.SpearLv),
            valueObjGet = x => x.SpearLv,
            valueObjSet = null,
        };

        public static SortTitle SortByHalberdLv = new SortTitle()
        {
            name = "戟兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.HalberdLv),
            personSortFunc = (a, b) => a.HalberdLv.CompareTo(b.HalberdLv),
            valueObjGet = x => x.HalberdLv,
            valueObjSet = null,
        };

        public static SortTitle SortByCrossbowLv = new SortTitle()
        {
            name = "弓兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.CrossbowLv),
            personSortFunc = (a, b) => a.CrossbowLv.CompareTo(b.CrossbowLv),
            valueObjGet = x => x.CrossbowLv,
            valueObjSet = null,
        };

        public static SortTitle SortByRideLv = new SortTitle()
        {
            name = "骑兵",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.RideLv),
            personSortFunc = (a, b) => a.RideLv.CompareTo(b.RideLv),
            valueObjGet = x => x.RideLv,
            valueObjSet = null,
        };

        public static SortTitle SortByWaterLv = new SortTitle()
        {
            name = "水军",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.WaterLv),
            personSortFunc = (a, b) => a.WaterLv.CompareTo(b.WaterLv),
            valueObjGet = x => x.WaterLv,
            valueObjSet = null,
        };

        public static SortTitle SortByMachineLv = new SortTitle()
        {
            name = "兵器",
            width = 2.00f,
            valueGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.MachineLv),
            personSortFunc = (a, b) => a.MachineLv.CompareTo(b.MachineLv),
            valueObjGet = x => x.MachineLv,
            valueObjSet = null,
        };

        public static SortTitle SortByFeatureList = new SortTitle()
        {
            name = "特技",
            width = 6.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueGetCall = x =>
            {
                StringBuilder sb = new StringBuilder();
                if (x.FeatureList != null)
                {
                    for (int i = 0; i < x.FeatureList.Count; i++)
                    {
                        sb.Append(x.FeatureList[i].Name);
                        if (i < x.FeatureList.Count - 1)
                            sb.Append(", ");
                    }
                }
                return sb.ToString();
            },
            personSortFunc = (a, b) =>
            {
                if (a.FeatureList == null && b.FeatureList == null)
                    return 0;
                if (a.FeatureList != null && b.FeatureList == null)
                    return -1;
                if (a.FeatureList == null && b.FeatureList != null)
                    return 1;
                return a.FeatureList.Count.CompareTo(b.FeatureList.Count);
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByFeatureDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            valueGetCall = x =>
            {
                if (x.FeatureList == null || x.FeatureList.Count == 0)
                    return string.Empty;

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < x.FeatureList.Count; i++)
                {
                    var feat = x.FeatureList[i];
                    sb.Append(feat.desc ?? string.Empty);
                    if (i < x.FeatureList.Count - 1)
                        sb.Append("\n");
                }
                return sb.ToString();
            },
            personSortFunc = (a, b) => 0,
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortBySex = new SortTitle()
        {
            name = "性别",
            width = 2.00f,
            valueGetCall = x => x.sex == 0 ? "男" : "女",
            personSortFunc = (a, b) => a.sex.CompareTo(b.sex),
            valueObjGet = x => x.sex,
            valueObjSet = (x, v) => x.sex = (int)v,
        };

        public static SortTitle SortByLoyalty = new SortTitle()
        {
            name = "忠诚",
            width = 2.00f,
            valueGetCall = (x) =>
            {
                if (x.BelongForce == null || x == x.BelongForce.Governor) return "---";
                return System.Math.Min(100, x.loyalty).ToString();
            } ,
            personSortFunc = (a, b) => a.loyalty.CompareTo(b.loyalty),
            valueObjGet = x => x.loyalty,
            valueObjSet = (x, v) => x.loyalty = (int)v,
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

        public static SortTitle SortByLevel = new SortTitle()
        {
            name = "等级",
            width = 2.00f,
            valueGetCall = x => x.Level.Name,
            personSortFunc = (a, b) => a.Level.Id.CompareTo(b.Level.Id),
            valueObjGet = x => x.Level,
            valueObjSet = null,
        };

        public static SortTitle GetSortByFeatrueId(int id)
        {
            Feature feature = Scenario.Cur.GetObject<Feature>(id);
            return new SortTitle()
            {
                name = feature.Name,
                width = 2.00f,
                valueGetCall = x => x.HasFeatrue(id) ? "○" : "✕",
                personSortFunc = (a, b) => a.HasFeatrue(id).CompareTo(b.HasFeatrue(id)),
                valueObjGet = x => x.HasFeatrue(id),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortByHasItemId(int id)
        {
            ItemType itemType = Scenario.Cur.GetObject<ItemType>(id);
            return new SortTitle()
            {
                name = itemType.Name,
                width = 2.00f,
                valueGetCall = x => x.HasItem(id) ? "○" : "✕",
                personSortFunc = (a, b) => a.HasItem(id).CompareTo(b.HasItem(id)),
                valueObjGet = x => x.HasItem(id),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortByContainsInList(string title, List<Person> list)
        {
            return new SortTitle()
            {
                name = title,
                width = 2.00f,
                valueGetCall = x => list.Contains(x) ? "○" : "✕",
                personSortFunc = (a, b) => list.Contains(a).CompareTo(list.Contains(b)),
                valueObjGet = x => list.Contains(x),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortBySearchingRecommend(List<Person> recommendList, int featureId)
        {
            return new SortTitle()
            {
                name = "军师推荐",
                width = 3.20f,
                valueGetCall = x =>
                {
                    bool isRecommend = recommendList.Contains(x);
                    if (isRecommend) return "○";
                    return "✕";
                },
                personSortFunc = (a, b) =>
                {
                    bool aRecommend = recommendList.Contains(a);
                    bool bRecommend = recommendList.Contains(b);
                    bool aFeature = a.HasFeatrue(featureId);
                    bool bFeature = b.HasFeatrue(featureId);

                    int aScore = (aRecommend ? 2 : 0) + (aFeature ? 1 : 0);
                    int bScore = (bRecommend ? 2 : 0) + (bFeature ? 1 : 0);

                    if (aScore != bScore)
                        return -aScore.CompareTo(bScore);

                    return -a.Politics.CompareTo(b.Politics);
                },
                valueObjGet = x => recommendList.Contains(x),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortByRecruitRecommend(List<Person> recommendList)
        {
            return new SortTitle()
            {
                name = "军师推荐",
                width = 3.20f,
                valueGetCall = x => recommendList.Contains(x) ? "○" : "✕",
                personSortFunc = (a, b) =>
                {
                    bool aRecommend = recommendList.Contains(a);
                    bool bRecommend = recommendList.Contains(b);
                    if (aRecommend != bRecommend)
                        return -aRecommend.CompareTo(bRecommend);
                    return -a.Glamour.CompareTo(b.Glamour);
                },
                valueObjGet = x => recommendList.Contains(x),
                valueObjSet = null,
            };
        }

        public static SortTitle GetSortByDistanceDay(City where)
        {
            return new SortTitle()
            {
                name = "期间",
                width = 2.00f,
                valueGetCall = x => $"{x.DistanceDays(where) * 10}日",
                personSortFunc = (a, b) => a.DistanceDays(where).CompareTo(b.DistanceDays(where)),
                valueObjGet = x => x.DistanceDays(where),
                valueObjSet = null,
            };
        }

        public static SortTitle SortByAction = new SortTitle()
        {
            name = "行动",
            width = 2.00f,
            valueGetCall = x => x == null ? "—" : (x.ActionOver ? "已" : "未"),
            personSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueObjGet = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        public static SortTitle SortByMissionType = new SortTitle()
        {
            name = "任务",
            width = 2.00f,
            valueGetCall = x => x == null ? "—" : (x.missionType == 0 ? "无" : x.missionType.ToString()),
            personSortFunc = (a, b) => a.missionType.CompareTo(b.missionType),
            valueObjGet = x => x.missionType,
            valueObjSet = (x, v) => x.missionType = (int)v,
        };

        public static SortTitle SortByMissionTarget = new SortTitle()
        {
            name = "目标",
            width = 2.00f,
            valueGetCall = x => x == null ? "—" : (x.missionTarget == 0 ? "无" : x.missionTarget.ToString()),
            personSortFunc = (a, b) => a.missionTarget.CompareTo(b.missionTarget),
            valueObjGet = x => x.missionTarget,
            valueObjSet = (x, v) => x.missionTarget = (int)v,
        };

        public static SortTitle SortByIsFree = new SortTitle()
        {
            name = "空闲",
            width = 2.00f,
            valueGetCall = x => x.IsFree ? "○" : "✕",
            personSortFunc = (a, b) => a.IsFree.CompareTo(b.IsFree),
            valueObjGet = x => x.IsFree,
            valueObjSet = null,
        };

        public static SortTitle SortByIsWild = new SortTitle()
        {
            name = "在野",
            width = 2.00f,
            valueGetCall = x => x.IsWild ? "○" : "✕",
            personSortFunc = (a, b) => a.IsWild.CompareTo(b.IsWild),
            valueObjGet = x => x.IsWild,
            valueObjSet = null,
        };

        public static SortTitle SortByAge = new SortTitle()
        {
            name = "年龄",
            width = 2.00f,
            valueGetCall = x => x.Age.ToString(),
            personSortFunc = (a, b) => a.Age.CompareTo(b.Age),
            valueObjGet = x => x.Age,
            valueObjSet = null,
        };

        public static SortTitle SortByBelongForce = new SortTitle()
        {
            name = "势力",
            width = 3.00f,
            valueGetCall = x => x.BelongForce?.Name ?? "",
            personSortFunc = (a, b) => SangoObject.Compare(a.BelongForce, b.BelongForce),
            valueObjGet = x => x.BelongForce,
            valueObjSet = (x, v) => x.BelongForce = (Force)v,
        };

        public static SortTitle SortByBelongCorps = new SortTitle()
        {
            name = "军团",
            width = 3.40f,
            valueGetCall = x => x.BelongCorps?.Name ?? "",
            personSortFunc = (a, b) => SangoObject.Compare(a.BelongCorps, b.BelongCorps),
            valueObjGet = x => x.BelongCorps,
            valueObjSet = (x, v) => x.BelongCorps = (Corps)v,
        };

        public static SortTitle SortByBelongTroop = new SortTitle()
        {
            name = "部队",
            width = 2.00f,
            valueGetCall = x => x.BelongTroop?.Name ?? "",
            personSortFunc = (a, b) => SangoObject.Compare(a.BelongTroop, b.BelongTroop),
            valueObjGet = x => x.BelongTroop,
            valueObjSet = (x, v) => x.BelongTroop = (Troop)v,
        };

        public static SortTitle SortByBelongCity = new SortTitle()
        {
            name = "所属",
            width = 3.40f,
            valueGetCall = x => x.BelongCity?.Name ?? "",
            personSortFunc = (a, b) => SangoObject.Compare(a.BelongCity, b.BelongCity),
            valueObjGet = x => x.BelongCity,
            valueObjSet = (x, v) => x.BelongCity = (City)v,
        };

        public static SortTitle SortByCurrentCity = new SortTitle()
        {
            name = "所在",
            width = 3.40f,
            valueGetCall = (x) => {

                if (x.BelongTroop != null)
                    return x.BelongTroop.Name;
                else
                    return x.CurrentCity?.Name ?? "";
                },
            personSortFunc = (a, b) => SangoObject.Compare(a.CurrentCity, b.CurrentCity),
            valueObjGet = x => x.CurrentCity,
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
            valueGetCall = x => x.appearance.ToString(),
            personSortFunc = (a, b) => a.appearance.CompareTo(b.appearance),
            valueObjGet = x => x.appearance,
            valueObjSet = (x, v) => x.appearance = (int)v,
        };

        public static SortTitle SortByIsValid = new SortTitle()
        {
            name = "登场",
            width = 2.00f,
            valueGetCall = x => x.IsValid ? "○" : "✕",
            personSortFunc = (a, b) => a.IsValid.CompareTo(b.IsValid),
            valueObjGet = x => x.IsValid,
            valueObjSet = null,
        };

        public static SortTitle SortByBeFinded = new SortTitle()
        {
            name = "已发现",
            width = 2.00f,
            valueGetCall = x => x.beFinded ? "○" : "✕",
            personSortFunc = (a, b) => a.beFinded.CompareTo(b.beFinded),
            valueObjGet = x => x.beFinded,
            valueObjSet = null,
        };

        public static SortTitle SortByYearBorn = new SortTitle()
        {
            name = "出生年",
            width = 2.00f,
            valueGetCall = x => x.yearBorn.ToString(),
            personSortFunc = (a, b) => a.yearBorn.CompareTo(b.yearBorn),
            valueObjGet = x => x.yearBorn,
            valueObjSet = (x, v) => x.yearBorn = (int)v,
        };

        public static SortTitle SortByYearDead = new SortTitle()
        {
            name = "死亡年",
            width = 2.00f,
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

        public static SortTitle SortByIsCityLeader = new SortTitle()
        {
            name = "太守",
            width = 2.00f,
            valueGetCall = x =>
            {
                if (x.BelongCity == null)
                    return "✕";
                return x == x.BelongCity.Leader ? "○" : "✕";
            },
            personSortFunc = (a, b) =>
            {
                bool aIsLeader = a.BelongCity != null && a == a.BelongCity.Leader;
                bool bIsLeader = b.BelongCity != null && b == b.BelongCity.Leader;
                return bIsLeader.CompareTo(aIsLeader);
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByIsCounsellor = new SortTitle()
        {
            name = "军师",
            width = 2.00f,
            valueGetCall = x =>
            {
                if (x.BelongForce == null)
                    return "✕";
                return x == x.BelongForce.Counsellor ? "○" : "✕";
            },
            personSortFunc = (a, b) =>
            {
                bool aIsCounsellor = a.BelongForce != null && a == a.BelongForce.Counsellor;
                bool bIsCounsellor = b.BelongForce != null && b == b.BelongForce.Counsellor;
                return bIsCounsellor.CompareTo(aIsCounsellor);
            },
            valueObjGet = null,
            valueObjSet = null,
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
        public static SortTitle SortByPersonality = new SortTitle()
        {
            name = "性格",
            width = 2.00f,
            valueGetCall = x => x == null || x.personality == null ? "—" : x.personality.Name,
            personSortFunc = (a, b) =>
            {
                string aName = a?.personality?.Name ?? "";
                string bName = b?.personality?.Name ?? "";
                return aName.CompareTo(bName);
            },
            valueObjGet = x => x.personality,
            valueObjSet = (x, v) => x.personality = (Personality)v,
        };

        public static SortTitle SortByOfficial = new SortTitle()
        {
            name = "官职",
            width = 3.20f,
            valueGetCall = x => x.Official.Name,
            personSortFunc = (a, b) => SangoObject.Compare(a.Official, b.Official),
            valueObjGet = x => x.Official,
            valueObjSet = (x, v) => x. Official = (Official)v,
        };

        public static SortTitle SortByCost = new SortTitle()
        {
            name = "俸禄",
            width = 3.20f,
            valueGetCall = x => (x.Official?.cost ?? 5).ToString(),
            personSortFunc = (a, b) => SangoObject.Compare(a.Official, b.Official),
            valueObjGet = x => x.Official?.cost ?? 5,
            valueObjSet = null,
        };

        public static SortTitle SortByFather = new SortTitle()
        {
            name = "父亲",
            width = 2.40f,
            valueGetCall = x => x == null || x.Father == null ? " " : x.Father.Name,
            personSortFunc = (a, b) => SangoObject.Compare(a?.Father, b?.Father),
            valueObjGet = x => x.Father,
            valueObjSet = (x, v) => x.Father = (Person)v,
        };

        public static SortTitle SortByMother = new SortTitle()
        {
            name = "母亲",
            width = 2.40f,
            valueGetCall = x => x == null || x.Mother == null ? " " : x.Mother.Name,
            personSortFunc = (a, b) => SangoObject.Compare(a?.Mother, b?.Mother),
            valueObjGet = x => x.Mother,
            valueObjSet = (x, v) => x.Mother = (Person)v,
        };

        public static SortTitle SortByBrother = new SortTitle()
        {
            name = "兄弟",
            width = 7.20f,
            valueGetCall = x =>
            {
                if (x == null) return " ";
                if (x.BrotherList == null || x.BrotherList.Count == 0) return " ";

                var names = new System.Collections.Generic.List<string>();
                foreach (Person brother in x.BrotherList)
                {
                    if (brother != null) names.Add(brother.Name);
                }
                return names.Count == 0 ? " " : string.Join("，", names);
            },
            personSortFunc = (a, b) =>
            {
                if (a.BrotherList != null && b.BrotherList != null)
                {
                    return a.BrotherList.Count.CompareTo(b.BrotherList.Count);
                }

                if (a.BrotherList != null)
                    return 1;

                if (b.BrotherList != null)
                    return -1;

                return 0;
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortBySpouse = new SortTitle()
        {
            name = "配偶",
            width = 7.20f,
            valueGetCall = x =>
            {
                if (x == null) return " ";
                if (x.SpouseList == null || x.SpouseList.Count == 0) return " ";

                var names = new System.Collections.Generic.List<string>();
                foreach (Person spouse in x.SpouseList)
                {
                    if (spouse != null) names.Add(spouse.Name);
                }
                return names.Count == 0 ? " " : string.Join("，", names);
            },
            personSortFunc = (a, b) =>
            {
                if (a.SpouseList != null && b.SpouseList != null)
                {
                    return a.SpouseList.Count.CompareTo(b.SpouseList.Count);
                }

                if (a.SpouseList != null)
                    return 1;

                if (b.SpouseList != null)
                    return -1;

                return 0;
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByWork = new SortTitle()
        {
            name = "工作",
            width = 4.50f,
            valueGetCall = x => x.workingBuilding?.Name ?? "-",
            personSortFunc = (a, b) => Building.Compare(a.workingBuilding, b.workingBuilding),
            valueObjGet = x => x.workingBuilding,
            valueObjSet = null,
        };

        public static SortTitle SortByUpgradeOffical = new SortTitle()
        {
            name = "可晋升",
            width = 2.00f,
            valueGetCall = x =>
            {
                return x.CanUpgradeOfficial ? "○" : "✕";
            },
            personSortFunc = (a, b) =>
            {
                return a.CanUpgradeOfficial.CompareTo(b.CanUpgradeOfficial);
            },
            valueObjGet = x => x.CanUpgradeOfficial,
            valueObjSet = null,
        };

        public static List<ObjectSortTitle> DefaultSortList = new List<ObjectSortTitle>
        {
            SortByName,
            SortByBelongCity,
            SortByState,
            SortByLoyalty,
            SortByMerit,
            SortByLevel,
        };

    }

}
