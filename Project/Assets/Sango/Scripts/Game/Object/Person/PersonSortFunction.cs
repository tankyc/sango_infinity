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
        /// 判断武将是否允许修改该属性的委托（编辑前拦截，如君主身份不可修改）
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <returns>是否允许修改</returns>
        public delegate bool PersonCanSetCall(Person person);

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
            public PersonValueStrGet valueStrGetCall;
            public PersonSortFunc valueSortFunc;
            public PersonValueObjGet valueObjGet;
            public PersonValueObjSet valueObjSet;

            /// <summary>
            /// 是否允许修改该武将属性的委托（为空表示默认允许）
            /// </summary>
            public PersonCanSetCall canSetCall;

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
                return valueStrGetCall.Invoke((Person)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Person)a, (Person)b);
            }

            /// <summary>
            /// 是否允许修改目标武将的该属性（君主身份等特殊属性不可修改）
            /// </summary>
            /// <param name="obj">目标对象</param>
            /// <returns>是否允许修改</returns>
            public override bool CanSetValue(SangoObject obj)
            {
                Person person = obj as Person;
                if (person == null)
                {
                    return base.CanSetValue(obj);
                }
                if (canSetCall != null)
                {
                    return canSetCall(person);
                }
                return true;
            }

            public SortTitle Copy()
            {
                return new SortTitle
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    valueStrGetCall = valueStrGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjGet = valueObjGet,
                    valueObjSet = valueObjSet,
                    canSetCall = canSetCall,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        /// <summary>
        /// 从通用object值中收集目标类型的对象列表
        /// 兼容List&lt;T&gt;/SangoObjectList&lt;T&gt;/单个对象/null等输入，并自动去重
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">通用值</param>
        /// <returns>收集到的对象列表</returns>
        private static List<T> CollectObjectList<T>(object value) where T : SangoObject
        {
            List<T> list = new List<T>();
            if (value is T)
            {
                list.Add((T)value);
                return list;
            }
            if (value is System.Collections.IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    if (item is T && !list.Contains((T)item))
                    {
                        list.Add((T)item);
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// 设置武将的特技列表（特殊数据修改接口：整体替换特技集合）
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="value">新的特技列表</param>
        public static void SetPersonFeatureList(Person person, object value)
        {
            if (person == null) return;
            List<Feature> newList = CollectObjectList<Feature>(value);
            if (person.mFeatureList == null)
            {
                person.mFeatureList = new SangoObjectList<Feature>();
            }
            person.mFeatureList.Clear();
            for (int i = 0; i < newList.Count; i++)
            {
                if (newList[i] != null)
                {
                    person.mFeatureList.Add(newList[i]);
                }
            }
        }

        /// <summary>
        /// 设置武将的配偶列表（特殊数据修改接口）
        /// 1.先解除目标武将与其原配偶的双向登记关系；
        /// 2.再与每个新配偶建立双向登记关系；
        /// 3.维持唯一约束：一个武将只能被一个其他武将登记为配偶（已在他人配偶中的武将只能归属一个“A”）
        /// </summary>
        /// <param name="person">目标武将（A）</param>
        /// <param name="value">新的配偶列表（B集合）</param>
        public static void SetPersonSpouseList(Person person, object value)
        {
            if (person == null) return;
            List<Person> newList = CollectObjectList<Person>(value);
            if (person.mSpouseList == null)
            {
                Sango.Log.Warning("武将:" + person.Name + " 的配偶列表尚未初始化,无法修改");
                return;
            }

            // 1.解除旧关系：移除未保留的旧配偶，同时解除对方列表中对自己的登记
            List<Person> oldList = new List<Person>();
            foreach (Person old in person.mSpouseList)
            {
                if (old != null) oldList.Add(old);
            }
            person.mSpouseList.Clear();
            for (int i = 0; i < oldList.Count; i++)
            {
                Person oldSpouse = oldList[i];
                if (newList.Contains(oldSpouse)) continue;
                if (oldSpouse.mSpouseList != null && oldSpouse.mSpouseList.Contains(person))
                {
                    oldSpouse.mSpouseList.Remove(person);
                }
            }

            // 2.建立新关系：把配偶登记进目标武将列表，并让每个配偶只归属于目标武将一人
            for (int i = 0; i < newList.Count; i++)
            {
                Person spouse = newList[i];
                if (spouse == null || spouse == person) continue;
                if (!person.mSpouseList.Contains(spouse))
                {
                    person.mSpouseList.Add(spouse);
                }
                if (spouse.mSpouseList == null) continue;
                // 解除配偶与其原有配偶的双向登记，确保其只对应一个“A”
                List<Person> otherSpouses = new List<Person>();
                foreach (Person other in spouse.mSpouseList)
                {
                    if (other != null && other != person) otherSpouses.Add(other);
                }
                for (int j = 0; j < otherSpouses.Count; j++)
                {
                    Person other = otherSpouses[j];
                    spouse.mSpouseList.Remove(other);
                    if (other.mSpouseList != null && other.mSpouseList.Contains(spouse))
                    {
                        other.mSpouseList.Remove(spouse);
                    }
                }
                if (!spouse.mSpouseList.Contains(person))
                {
                    spouse.mSpouseList.Add(person);
                }
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
        public static SortTitle SortById = new SortTitle()
        {
            name = "编号",
            width = 2.5f,
            valueStrGetCall = x => x.Id.ToString(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = x => x.Id,
            valueObjSet = (x, v) => x.Id = (int)v,
        };

        public static SortTitle SortByName = new SortTitle()
        {
            name = "武将",
            width = 4.20f,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjGet = x => x.Name,
            valueObjSet = null// (x, v) => x.Name = (string)v,
            //editType = DataEditType.Text,
        };

        public static SortTitle SortByTroopsLimit = new SortTitle()
        {
            name = "指挥",
            width = 2.80f,
            valueStrGetCall = x => x.TroopsLimit.ToString(),
            valueSortFunc = (a, b) => a.TroopsLimit.CompareTo(b.TroopsLimit),
            valueObjGet = x => x.TroopsLimit,
            valueObjSet = null,
        };

        public static SortTitle SortByCommand = new SortTitle()
        {
            name = "统率",
            width = 2.00f,
            valueStrGetCall = x => x.Command.ToString(),
            valueSortFunc = (a, b) => a.Command.CompareTo(b.Command),
            valueObjGet = x => x.Command,
            valueObjSet = null,
        };

        public static SortTitle SortByStrength = new SortTitle()
        {
            name = "武力",
            width = 2.00f,
            valueStrGetCall = x => x.Strength.ToString(),
            valueSortFunc = (a, b) => a.Strength.CompareTo(b.Strength),
            valueObjGet = x => x.Strength,
            valueObjSet = null,
        };

        public static SortTitle SortByIntelligence = new SortTitle()
        {
            name = "智力",
            width = 2.00f,
            valueStrGetCall = x => x.Intelligence.ToString(),
            valueSortFunc = (a, b) => -a.Intelligence.CompareTo(b.Intelligence),
            valueObjGet = x => x.Intelligence,
            valueObjSet = null,
        };

        public static SortTitle SortByPolitics = new SortTitle()
        {
            name = "政治",
            width = 2.00f,
            valueStrGetCall = x => x.Politics.ToString(),
            valueSortFunc = (a, b) => b.Politics.CompareTo(a.Politics),
            valueObjGet = x => x.Politics,
            valueObjSet = null,
        };

        public static SortTitle SortByGlamour = new SortTitle()
        {
            name = "魅力",
            width = 2.00f,
            valueStrGetCall = x => x.Glamour.ToString(),
            valueSortFunc = (a, b) => a.Glamour.CompareTo(b.Glamour),
            valueObjGet = x => x.Glamour,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseCommand = new SortTitle()
        {
            name = "统率",
            width = 2.00f,
            valueStrGetCall = x => x.command.baseValue.ToString(),
            valueSortFunc = (a, b) => a.command.baseValue.CompareTo(b.command.baseValue),
            valueObjGet = x => x.command.baseValue,
            valueObjSet = (x, v) => {
                x.command.baseValue = (int)v;
                x.command.UpdateNoAge();
            }
            ,
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 100,
        };

        public static SortTitle SortByBaseStrength = new SortTitle()
        {
            name = "武力",
            width = 2.00f,
            valueStrGetCall = x => x.strength.baseValue.ToString(),
            valueSortFunc = (a, b) => a.strength.baseValue.CompareTo(b.strength.baseValue),
            valueObjGet = x => x.strength.baseValue,
            valueObjSet = (x, v) => {
                x.strength.baseValue = (int)v;
                x.strength.UpdateNoAge();
            },
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 100,
        };

        public static SortTitle SortByBaseIntelligence = new SortTitle()
        {
            name = "智力",
            width = 2.00f,
            valueStrGetCall = x => x.intelligence.baseValue.ToString(),
            valueSortFunc = (a, b) => -a.intelligence.baseValue.CompareTo(b.intelligence.baseValue),
            valueObjGet = x => x.intelligence.baseValue,
            valueObjSet = (x, v) => {
                x.intelligence.baseValue = (int)v;
                x.intelligence.UpdateNoAge();
            },
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 100,
        };

        public static SortTitle SortByBasePolitics = new SortTitle()
        {
            name = "政治",
            width = 2.00f,
            valueStrGetCall = x => x.politics.baseValue.ToString(),
            valueSortFunc = (a, b) => b.politics.baseValue.CompareTo(a.politics.baseValue),
            valueObjGet = x => x.politics.baseValue,
            valueObjSet = (x, v) => {
                x.politics.baseValue = (int)v;
                x.politics.UpdateNoAge();
            },
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 100,

        };

        public static SortTitle SortByBaseGlamour = new SortTitle()
        {
            name = "魅力",
            width = 2.00f,
            valueStrGetCall = x => x.glamour.baseValue.ToString(),
            valueSortFunc = (a, b) => a.glamour.baseValue.CompareTo(b.glamour.baseValue),
            valueObjGet = x => x.glamour.baseValue,
            valueObjSet = (x, v) => {
                x.glamour.baseValue = (int)v;
                x.glamour.UpdateNoAge();
            },
            editType = DataEditType.IntCalculator,
            minValue = 1,
            maxValue = 100,
        };


        public static SortTitle SortByCommandChangeType = new SortTitle()
        {
            name = "成长",
            width = 3.00f,
            valueStrGetCall = x => x.command.changeType.ToString(),
            valueSortFunc = (a, b) => a.command.changeType.Id.CompareTo(b.command.changeType.Id),
            valueObjGet = x => x.command.changeType,
            valueObjSet = (x, v) => x.command.changeType = (AttributeChangeType)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.AttributeChangeType
        };

        public static SortTitle SortByStrengthChangeType = new SortTitle()
        {
            name = "成长",
            width = 3.00f,
            valueStrGetCall = x => x.strength.changeType.ToString(),
            valueSortFunc = (a, b) => a.strength.changeType.Id.CompareTo(b.strength.changeType.Id),
            valueObjGet = x => x.strength.changeType,
            valueObjSet = (x, v) => x.strength.changeType = (AttributeChangeType)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.AttributeChangeType
        };

        public static SortTitle SortByIntelligenceChangeType = new SortTitle()
        {
            name = "成长",
            width = 3.00f,
            valueStrGetCall = x => x.intelligence.changeType.ToString(),
            valueSortFunc = (a, b) => a.intelligence.changeType.Id.CompareTo(b.intelligence.changeType.Id),
            valueObjGet = x => x.strength.changeType,
            valueObjSet = (x, v) => x.strength.changeType = (AttributeChangeType)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.AttributeChangeType
        };

        public static SortTitle SortByPoliticsChangeType = new SortTitle()
        {
            name = "成长",
            width = 3.00f,
            valueStrGetCall = x => x.politics.changeType.ToString(),
            valueSortFunc = (a, b) => a.politics.changeType.Id.CompareTo(b.politics.changeType.Id),
            valueObjGet = x => x.politics.changeType,
            valueObjSet = (x, v) => x.politics.changeType = (AttributeChangeType)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.AttributeChangeType

        };

        public static SortTitle SortByGlamourChangeType = new SortTitle()
        {
            name = "成长",
            width = 3.00f,
            valueStrGetCall = x => x.glamour.changeType.ToString(),
            valueSortFunc = (a, b) => a.glamour.changeType.Id.CompareTo(b.glamour.changeType.Id),
            valueObjGet = x => x.glamour.changeType,
            valueObjSet = (x, v) => x.glamour.changeType = (AttributeChangeType)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.AttributeChangeType
        };

        public static SortTitle SortByMilitaryAbility = new SortTitle()
        {
            name = "军事",
            width = 2.00f,
            valueStrGetCall = x => x.MilitaryAbility.ToString(),
            valueSortFunc = (a, b) => a.MilitaryAbility.CompareTo(b.MilitaryAbility),
            valueObjGet = x => x.MilitaryAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseCommerceAbility = new SortTitle()
        {
            name = "商业",
            width = 2.00f,
            valueStrGetCall = x => x.BaseCommerceAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseCommerceAbility.CompareTo(b.BaseCommerceAbility),
            valueObjGet = x => x.BaseCommerceAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseSecurityAbility = new SortTitle()
        {
            name = "治安",
            width = 2.00f,
            valueStrGetCall = x => x.BaseSecurityAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseSecurityAbility.CompareTo(b.BaseSecurityAbility),
            valueObjGet = x => x.BaseSecurityAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseTrainTroopAbility = new SortTitle()
        {
            name = "训练",
            width = 2.00f,
            valueStrGetCall = x => x.BaseTrainTroopAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseTrainTroopAbility.CompareTo(b.BaseTrainTroopAbility),
            valueObjGet = x => x.BaseTrainTroopAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseAgricultureAbility = new SortTitle()
        {
            name = "农业",
            width = 2.00f,
            valueStrGetCall = x => x.BaseAgricultureAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseAgricultureAbility.CompareTo(b.BaseAgricultureAbility),
            valueObjGet = x => x.BaseAgricultureAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseBuildAbility = new SortTitle()
        {
            name = "建设",
            width = 2.00f,
            valueStrGetCall = x => x.BaseBuildAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseBuildAbility.CompareTo(b.BaseBuildAbility),
            valueObjGet = x => x.BaseBuildAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseCreativeAbility = new SortTitle()
        {
            name = "生产",
            width = 2.00f,
            valueStrGetCall = x => x.BaseCreativeAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseCreativeAbility.CompareTo(b.BaseCreativeAbility),
            valueObjGet = x => x.BaseCreativeAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseSearchingAbility = new SortTitle()
        {
            name = "搜寻",
            width = 2.00f,
            valueStrGetCall = x => x.BaseSearchingAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseSearchingAbility.CompareTo(b.BaseSearchingAbility),
            valueObjGet = x => x.BaseSearchingAbility,
            valueObjSet = null,
        };

        public static SortTitle SortByBaseRecruitmentAbility = new SortTitle()
        {
            name = "招募",
            width = 2.00f,
            valueStrGetCall = x => x.BaseRecruitmentAbility.ToString(),
            valueSortFunc = (a, b) => a.BaseRecruitmentAbility.CompareTo(b.BaseRecruitmentAbility),
            valueObjGet = x => x.BaseRecruitmentAbility,
            valueObjSet = null,
        };

        public static SortTitle SortBySpearLv = new SortTitle()
        {
            name = "枪兵",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.SpearLv),
            valueSortFunc = (a, b) => a.SpearLv.CompareTo(b.SpearLv),
            valueObjGet = x => x.SpearLv,
            valueObjSet = null,
        };

        public static SortTitle SortByHalberdLv = new SortTitle()
        {
            name = "戟兵",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.HalberdLv),
            valueSortFunc = (a, b) => a.HalberdLv.CompareTo(b.HalberdLv),
            valueObjGet = x => x.HalberdLv,
            valueObjSet = null,
        };

        public static SortTitle SortByCrossbowLv = new SortTitle()
        {
            name = "弓兵",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.CrossbowLv),
            valueSortFunc = (a, b) => a.CrossbowLv.CompareTo(b.CrossbowLv),
            valueObjGet = x => x.CrossbowLv,
            valueObjSet = null,
        };

        public static SortTitle SortByRideLv = new SortTitle()
        {
            name = "骑兵",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.RideLv),
            valueSortFunc = (a, b) => a.RideLv.CompareTo(b.RideLv),
            valueObjGet = x => x.RideLv,
            valueObjSet = null,
        };

        public static SortTitle SortByWaterLv = new SortTitle()
        {
            name = "水军",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.WaterLv),
            valueSortFunc = (a, b) => a.WaterLv.CompareTo(b.WaterLv),
            valueObjGet = x => x.WaterLv,
            valueObjSet = null,
        };

        public static SortTitle SortByMachineLv = new SortTitle()
        {
            name = "兵器",
            width = 2.00f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.MachineLv),
            valueSortFunc = (a, b) => a.MachineLv.CompareTo(b.MachineLv),
            valueObjGet = x => x.MachineLv,
            valueObjSet = null,
        };

        public static SortTitle SortByFeatureList = new SortTitle()
        {
            name = "特技",
            width = 6.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x =>
            {
                StringBuilder sb = new StringBuilder();
                if (x.mFeatureList != null)
                {
                    for (int i = 0; i < x.mFeatureList.Count; i++)
                    {
                        sb.Append(x.mFeatureList[i].Name);
                        if (i < x.mFeatureList.Count - 1)
                            sb.Append(", ");
                    }
                }
                return sb.ToString();
            },
            valueSortFunc = (a, b) =>
            {
                if (a.mFeatureList == null && b.mFeatureList == null)
                    return 0;
                if (a.mFeatureList != null && b.mFeatureList == null)
                    return -1;
                if (a.mFeatureList == null && b.mFeatureList != null)
                    return 1;
                return a.mFeatureList.Count.CompareTo(b.mFeatureList.Count);
            },
            valueObjGet = x => x.mFeatureList,
            valueObjSet = (x, v) => SetPersonFeatureList(x, v),
            editType = DataEditType.FeatureList,
            dataSetType = DataSetType.Feature,
        };

        public static SortTitle SortByFeatureDesc = new SortTitle()
        {
            name = "说明",
            width = 30.00f,
            valueStrGetCall = x =>
            {
                if (x.mFeatureList == null || x.mFeatureList.Count == 0)
                    return string.Empty;

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < x.mFeatureList.Count; i++)
                {
                    var feat = x.mFeatureList[i];
                    sb.Append(feat.desc ?? string.Empty);
                    if (i < x.mFeatureList.Count - 1)
                        sb.Append("\n");
                }
                return sb.ToString();
            },
            valueSortFunc = (a, b) => 0,
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortBySex = new SortTitle()
        {
            name = "性别",
            width = 2.00f,
            valueStrGetCall = x => x.sex == 0 ? "男" : "女",
            valueSortFunc = (a, b) => a.sex.CompareTo(b.sex),
            valueObjGet = x => x.sex,
            valueObjSet = (x, v) => x.sex = (int)v,
            editType = DataEditType.IntDropdown,
            customData = DataEditPresetOptions.SexOptions,
        };

        public static SortTitle SortByLoyalty = new SortTitle()
        {
            name = "忠诚",
            width = 2.00f,
            valueStrGetCall = (x) =>
            {
                if (x.mBelongForce == null || x == x.mBelongForce.mGovernor) return "---";
                return System.Math.Min(100, x.loyalty).ToString();
            },
            valueSortFunc = (a, b) => a.loyalty.CompareTo(b.loyalty),
            valueObjGet = x => x.loyalty,
            valueObjSet = (x, v) => x.loyalty = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 250,
        };

        public static SortTitle SortByMerit = new SortTitle()
        {
            name = "功绩",
            width = 4.00f,
            valueStrGetCall = x => x.merit.ToString(),
            valueSortFunc = (a, b) => a.merit.CompareTo(b.merit),
            valueObjGet = x => x.merit,
            valueObjSet = (x, v) => x.merit = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 100000,
        };

        public static SortTitle SortByExp = new SortTitle()
        {
            name = "经验",
            width = 2.00f,
            valueStrGetCall = x => x.Exp.ToString(),
            valueSortFunc = (a, b) => a.Exp.CompareTo(b.Exp),
            valueObjGet = x => x.Exp,
            valueObjSet = null,
        };

        public static SortTitle SortByLevel = new SortTitle()
        {
            name = "等级",
            width = 2.00f,
            valueStrGetCall = x => x.Level.Name,
            valueSortFunc = (a, b) => a.Level.Id.CompareTo(b.Level.Id),
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
                valueStrGetCall = x => x.HasFeatrue(id) ? "○" : "✕",
                valueSortFunc = (a, b) => a.HasFeatrue(id).CompareTo(b.HasFeatrue(id)),
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
                valueStrGetCall = x => x.HasItem(id) ? "○" : "✕",
                valueSortFunc = (a, b) => a.HasItem(id).CompareTo(b.HasItem(id)),
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
                valueStrGetCall = x => list.Contains(x) ? "○" : "✕",
                valueSortFunc = (a, b) => list.Contains(a).CompareTo(list.Contains(b)),
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
                valueStrGetCall = x =>
                {
                    bool isRecommend = recommendList.Contains(x);
                    if (isRecommend) return "○";
                    return "✕";
                },
                valueSortFunc = (a, b) =>
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
                valueStrGetCall = x => recommendList.Contains(x) ? "○" : "✕",
                valueSortFunc = (a, b) =>
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
                valueStrGetCall = x => $"{x.DistanceDays(where) * 10}日",
                valueSortFunc = (a, b) => a.DistanceDays(where).CompareTo(b.DistanceDays(where)),
                valueObjGet = x => x.DistanceDays(where),
                valueObjSet = null,
            };
        }

        public static SortTitle SortByAction = new SortTitle()
        {
            name = "行动",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : (x.ActionOver ? "已" : "未"),
            valueSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueObjGet = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        public static SortTitle SortByMissionType = new SortTitle()
        {
            name = "任务",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : (x.missionType == 0 ? "无" : x.missionType.ToString()),
            valueSortFunc = (a, b) => a.missionType.CompareTo(b.missionType),
            valueObjGet = x => x.missionType,
            valueObjSet = (x, v) => x.missionType = (int)v,
        };

        public static SortTitle SortByMissionTarget = new SortTitle()
        {
            name = "目标",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : (x.missionTarget == 0 ? "无" : x.missionTarget.ToString()),
            valueSortFunc = (a, b) => a.missionTarget.CompareTo(b.missionTarget),
            valueObjGet = x => x.missionTarget,
            valueObjSet = (x, v) => x.missionTarget = (int)v,
        };

        public static SortTitle SortByIsFree = new SortTitle()
        {
            name = "空闲",
            width = 2.00f,
            valueStrGetCall = x => x.IsFree ? "○" : "✕",
            valueSortFunc = (a, b) => a.IsFree.CompareTo(b.IsFree),
            valueObjGet = x => x.IsFree,
            valueObjSet = null,
        };

        public static SortTitle SortByIsWild = new SortTitle()
        {
            name = "在野",
            width = 2.00f,
            valueStrGetCall = x => x.IsWild ? "○" : "✕",
            valueSortFunc = (a, b) => a.IsWild.CompareTo(b.IsWild),
            valueObjGet = x => x.IsWild,
            valueObjSet = null,
        };

        public static SortTitle SortByAge = new SortTitle()
        {
            name = "年龄",
            width = 2.00f,
            valueStrGetCall = x => x.Age.ToString(),
            valueSortFunc = (a, b) => a.Age.CompareTo(b.Age),
            valueObjGet = x => x.Age,
            valueObjSet = null,
        };

        public static SortTitle SortByBelongForce = new SortTitle()
        {
            name = "势力",
            width = 4.20f,
            valueStrGetCall = x => x.mBelongForce?.Name ?? "",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongForce, b.mBelongForce),
            valueObjGet = x => x.mBelongForce,
            valueObjSet = (x, v) => x.mBelongForce = (Force)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Force,
        };

        public static SortTitle SortByBelongCorps = new SortTitle()
        {
            name = "军团",
            width = 6.40f,
            valueStrGetCall = x => x.mBelongCorps?.ForceNumberName ?? "",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCorps, b.mBelongCorps),
            valueObjGet = x => x.mBelongCorps,
            valueObjSet = (x, v) => x.mBelongCorps = (Corps)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Corps,
        };

        public static SortTitle SortByBelongTroop = new SortTitle()
        {
            name = "部队",
            width = 2.00f,
            valueStrGetCall = x => x.mTroop?.Name ?? "",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mTroop, b.mTroop),
            valueObjGet = x => x.mTroop,
            valueObjSet = (x, v) => x.mTroop = (Troop)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Troop,
        };

        public static SortTitle SortByBelongCity = new SortTitle()
        {
            name = "所属",
            width = 3.40f,
            valueStrGetCall = x => x.mBelongCity?.Name ?? "",
            valueSortFunc = (a, b) => SangoObject.Compare(a.mBelongCity, b.mBelongCity),
            valueObjGet = x => x.mBelongCity,
            valueObjSet = (x, v) => x.mBelongCity = (City)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.City,
        };

        public static SortTitle SortByCurrentCity = new SortTitle()
        {
            name = "所在",
            width = 3.40f,
            valueStrGetCall = (x) =>
            {

                if (x.mTroop != null)
                    return x.mTroop.Name;
                else
                    return x.mCurrentCity?.Name ?? "";
            },
            valueSortFunc = (a, b) => SangoObject.Compare(a.mCurrentCity, b.mCurrentCity),
            valueObjGet = x => x.mCurrentCity,
            valueObjSet = null,
        };

        public static SortTitle SortByDescription = new SortTitle()
        {
            name = "身平",
            width = 2.00f,
            valueStrGetCall = x => x.GetDescription(),
            valueSortFunc = (a, b) => a.Id.CompareTo(b.Id),
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByFamilyName = new SortTitle()
        {
            name = "姓",
            width = 2.00f,
            valueStrGetCall = x => x.familyName,
            valueSortFunc = (a, b) => a.familyName.CompareTo(b.familyName),
            valueObjGet = x => x.familyName,
            valueObjSet = (x, v) => x.familyName = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByGiveName = new SortTitle()
        {
            name = "名",
            width = 2.00f,
            valueStrGetCall = x => x.giveName,
            valueSortFunc = (a, b) => a.giveName.CompareTo(b.giveName),
            valueObjGet = x => x.giveName,
            valueObjSet = (x, v) => x.giveName = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByNickName = new SortTitle()
        {
            name = "字",
            width = 2.00f,
            valueStrGetCall = x => x.nickName,
            valueSortFunc = (a, b) => a.nickName.CompareTo(b.nickName),
            valueObjGet = x => x.nickName,
            valueObjSet = (x, v) => x.nickName = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle SortByYearAvailable = new SortTitle()
        {
            name = "登场年",
            width = 2.00f,
            valueStrGetCall = x => x.appearance.ToString(),
            valueSortFunc = (a, b) => a.appearance.CompareTo(b.appearance),
            valueObjGet = x => x.appearance,
            valueObjSet = (x, v) => x.appearance = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByIsValid = new SortTitle()
        {
            name = "登场",
            width = 2.00f,
            valueStrGetCall = x => x.IsValid ? "○" : "✕",
            valueSortFunc = (a, b) => a.IsValid.CompareTo(b.IsValid),
            valueObjGet = x => x.IsValid,
            valueObjSet = null,
        };

        public static SortTitle SortByBeFinded = new SortTitle()
        {
            name = "已发现",
            width = 2.00f,
            valueStrGetCall = x => x.beFinded ? "○" : "✕",
            valueSortFunc = (a, b) => a.beFinded.CompareTo(b.beFinded),
            valueObjGet = x => x.beFinded,
            valueObjSet = null,
        };

        public static SortTitle SortByYearBorn = new SortTitle()
        {
            name = "出生年",
            width = 2.00f,
            valueStrGetCall = x => x.yearBorn.ToString(),
            valueSortFunc = (a, b) => a.yearBorn.CompareTo(b.yearBorn),
            valueObjGet = x => x.yearBorn,
            valueObjSet = (x, v) => x.yearBorn = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByYearDead = new SortTitle()
        {
            name = "死亡年",
            width = 2.00f,
            valueStrGetCall = x => x.yearDead.ToString(),
            valueSortFunc = (a, b) => a.yearDead.CompareTo(b.yearDead),
            valueObjGet = x => x.yearDead,
            valueObjSet = (x, v) => x.yearDead = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle SortByCompatibility = new SortTitle()
        {
            name = "相性",
            width = 2.00f,
            valueStrGetCall = x => x.compatibility.ToString(),
            valueSortFunc = (a, b) => a.compatibility.CompareTo(b.compatibility),
            valueObjGet = x => x.compatibility,
            valueObjSet = (x, v) => x.compatibility = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
            maxValue = 255,
        };

        public static SortTitle SortByState = new SortTitle()
        {
            name = "身份",
            width = 2.80f,
            valueStrGetCall = x =>
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
            valueSortFunc = (a, b) => a.state.CompareTo(b.state),
            valueObjGet = x => x.state,
            // 主公（君主）身份不允许直接修改，需先在势力编辑中删除对应势力
            valueObjSet = (x, v) =>
            {
                if (x.IsGovernor)
                {
                    Sango.Log.Warning("君主身份不可修改,需先在势力页删除其势力");
                    return;
                }
                x.state = (int)v;
            },
            // 编辑前拦截：君主不允许修改身份
            canSetCall = x => x != null && !x.IsGovernor,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Custom,
            customData = DataEditPresetOptions.PersonStateOptions,
        };

        public static SortTitle SortByIsCityLeader = new SortTitle()
        {
            name = "太守",
            width = 2.00f,
            valueStrGetCall = x =>
            {
                if (x.mBelongCity == null)
                    return "✕";
                return x == x.mBelongCity.Leader ? "○" : "✕";
            },
            valueSortFunc = (a, b) =>
            {
                bool aIsLeader = a.mBelongCity != null && a == a.mBelongCity.Leader;
                bool bIsLeader = b.mBelongCity != null && b == b.mBelongCity.Leader;
                return bIsLeader.CompareTo(aIsLeader);
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByIsCounsellor = new SortTitle()
        {
            name = "军师",
            width = 2.00f,
            valueStrGetCall = x =>
            {
                if (x.mBelongForce == null)
                    return "✕";
                return x == x.mBelongForce.mCounsellor ? "○" : "✕";
            },
            valueSortFunc = (a, b) =>
            {
                bool aIsCounsellor = a.mBelongForce != null && a == a.mBelongForce.mCounsellor;
                bool bIsCounsellor = b.mBelongForce != null && b == b.mBelongForce.mCounsellor;
                return bIsCounsellor.CompareTo(aIsCounsellor);
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        public static SortTitle SortByStamina = new SortTitle()
        {
            name = "体力",
            width = 2.00f,
            valueStrGetCall = x => x.stamina.ToString(),
            valueSortFunc = (a, b) => a.stamina.CompareTo(b.stamina),
            valueObjGet = x => x.stamina,
            valueObjSet = (x, v) => x.stamina = (int)v,
        };

        // 性格（小写！！！）
        public static SortTitle SortByPersonality = new SortTitle()
        {
            name = "性格",
            width = 2.00f,
            valueStrGetCall = x => x == null || x.mPersonality == null ? "—" : x.mPersonality.Name,
            valueSortFunc = (a, b) =>
            {
                string aName = a?.mPersonality?.Name ?? "";
                string bName = b?.mPersonality?.Name ?? "";
                return aName.CompareTo(bName);
            },
            valueObjGet = x => x.mPersonality,
            valueObjSet = (x, v) => x.mPersonality = (Personality)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Personality,
        };

        public static SortTitle SortByOfficial = new SortTitle()
        {
            name = "官职",
            width = 3.20f,
            valueStrGetCall = x => x.Official.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Official, b.Official),
            valueObjGet = x => x.Official,
            valueObjSet = (x, v) => x.Official = (Official)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Official,
        };

        public static SortTitle SortByCost = new SortTitle()
        {
            name = "俸禄",
            width = 3.20f,
            valueStrGetCall = x => (x.Official?.cost ?? 5).ToString(),
            valueSortFunc = (a, b) => SangoObject.Compare(a.Official, b.Official),
            valueObjGet = x => x.Official?.cost ?? 5,
            valueObjSet = null,
        };

        public static SortTitle SortByFather = new SortTitle()
        {
            name = "父亲",
            width = 2.40f,
            valueStrGetCall = x => x == null || x.mFather == null ? " " : x.mFather.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a?.mFather, b?.mFather),
            valueObjGet = x => x.mFather,
            valueObjSet = (x, v) => x.mFather = (Person)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Person,
        };

        public static SortTitle SortByMother = new SortTitle()
        {
            name = "母亲",
            width = 2.40f,
            valueStrGetCall = x => x == null || x.mMother == null ? " " : x.mMother.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a?.mMother, b?.mMother),
            valueObjGet = x => x.mMother,
            valueObjSet = (x, v) => x.mMother = (Person)v,
            editType = DataEditType.Object,
            dataSetType = DataSetType.Person,
        };

        public static SortTitle SortByBrother = new SortTitle()
        {
            name = "兄弟",
            width = 7.20f,
            valueStrGetCall = x =>
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
            valueSortFunc = (a, b) =>
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
            valueStrGetCall = x =>
            {
                if (x == null) return " ";
                if (x.mSpouseList == null || x.mSpouseList.Count == 0) return " ";

                var names = new System.Collections.Generic.List<string>();
                foreach (Person spouse in x.mSpouseList)
                {
                    if (spouse != null) names.Add(spouse.Name);
                }
                return names.Count == 0 ? " " : string.Join("，", names);
            },
            valueSortFunc = (a, b) =>
            {
                if (a.mSpouseList != null && b.mSpouseList != null)
                {
                    return a.mSpouseList.Count.CompareTo(b.mSpouseList.Count);
                }

                if (a.mSpouseList != null)
                    return 1;

                if (b.mSpouseList != null)
                    return -1;

                return 0;
            },
            valueObjGet = x => x.mSpouseList,
            // 配偶修改走特殊接口：先解除原配偶关系，再建立新关系（维持一人最多被一人登记的约束）
            valueObjSet = (x, v) => SetPersonSpouseList(x, v),
            editType = DataEditType.SpouseList,
            dataSetType = DataSetType.Person,
        };

        public static SortTitle SortByWork = new SortTitle()
        {
            name = "工作",
            width = 4.50f,
            valueStrGetCall = x => x.workingBuilding?.Name ?? "-",
            valueSortFunc = (a, b) => Building.Compare(a.workingBuilding, b.workingBuilding),
            valueObjGet = x => x.workingBuilding,
            valueObjSet = null,
        };

        public static SortTitle SortByUpgradeOffical = new SortTitle()
        {
            name = "可晋升",
            width = 2.00f,
            valueStrGetCall = x =>
            {
                return x.CanUpgradeOfficial ? "○" : "✕";
            },
            valueSortFunc = (a, b) =>
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

        /// <summary>
        /// 义理排序标题（按武将的义理对象显示与排序，下拉从剧本义理集合中选值修改）
        /// </summary>
        public static SortTitle SortByArgumentation = new SortTitle()
        {
            name = "义理",
            width = 2.00f,
            valueStrGetCall = x => x == null || x.mArgumentation == null ? "—" : x.mArgumentation.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a == null ? null : a.mArgumentation, b == null ? null : b.mArgumentation),
            valueObjGet = x => x == null ? null : x.mArgumentation,
            valueObjSet = (x, v) => x.mArgumentation = v as Argumentation,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Argumentation,
        };

        /// <summary>
        /// 出生州排序标题（出生州保存州Id，显示与下拉均通过当前剧本的州集合解析）
        /// </summary>
        public static SortTitle SortByBirthplace = new SortTitle()
        {
            name = "出生州",
            width = 3.00f,
            valueStrGetCall = x => GetBirthplaceProvinceName(x),
            valueSortFunc = (a, b) =>
            {
                int aId = a == null ? 0 : a.birthplace;
                int bId = b == null ? 0 : b.birthplace;
                return aId.CompareTo(bId);
            },
            valueObjGet = x => GetBirthplaceProvince(x),
            valueObjSet = (x, v) => x.birthplace = v is Province ? ((Province)v).Id : 0,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Province,
        };

        /// <summary>
        /// 厌恶武将排序标题（列表显示姓名，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortBymHatePerson = new SortTitle()
        {
            name = "厌恶武将",
            width = 8.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetPersonListText(x == null ? null : x.mHatePersonList),
            valueSortFunc = (a, b) => CompareListCount(a == null ? null : a.mHatePersonList, b == null ? null : b.mHatePersonList),
            valueObjGet = null,
            valueObjSet = null,
        };

        /// <summary>
        /// 喜爱武将排序标题（列表显示姓名，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByLikePerson = new SortTitle()
        {
            name = "喜爱武将",
            width = 8.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x => GetPersonListText(x == null ? null : x.mLikePersonList),
            valueSortFunc = (a, b) => CompareListCount(a == null ? null : a.mLikePersonList, b == null ? null : b.mLikePersonList),
            valueObjGet = null,
            valueObjSet = null,
        };

        /// <summary>
        /// 道具排序标题（按道具栏总数量排序，显示每类道具名x数量，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByItem = new SortTitle()
        {
            name = "道具",
            width = 8.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x => x == null ? "—" : GetItemStoreText(x.itemStore),
            valueSortFunc = (a, b) =>
            {
                int aCount = a == null || a.itemStore == null ? 0 : a.itemStore.TotalNumber;
                int bCount = b == null || b.itemStore == null ? 0 : b.itemStore.TotalNumber;
                return aCount.CompareTo(bCount);
            },
            valueObjGet = null,
            valueObjSet = null,
        };

        /// <summary>
        /// 武器排序标题（显示装备的武器名，装备修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByEquippedWeapon = new SortTitle()
        {
            name = "武器",
            width = 2.60f,
            valueStrGetCall = x => x == null || x.EquippedWeapon == null ? "—" : x.EquippedWeapon.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a == null ? null : a.EquippedWeapon, b == null ? null : b.EquippedWeapon),
            valueObjGet = x => x == null ? null : x.EquippedWeapon,
            valueObjSet = null,
        };

        /// <summary>
        /// 马匹排序标题（显示装备的马名，装备修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByEquippedHorse = new SortTitle()
        {
            name = "马匹",
            width = 2.60f,
            valueStrGetCall = x => x == null || x.EquippedHorse == null ? "—" : x.EquippedHorse.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a == null ? null : a.EquippedHorse, b == null ? null : b.EquippedHorse),
            valueObjGet = x => x == null ? null : x.EquippedHorse,
            valueObjSet = null,
        };

        /// <summary>
        /// 铠甲排序标题（显示装备的铠甲名，装备修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByEquippedArmor = new SortTitle()
        {
            name = "铠甲",
            width = 2.60f,
            valueStrGetCall = x => x == null || x.EquippedArmor == null ? "—" : x.EquippedArmor.Name,
            valueSortFunc = (a, b) => SangoObject.Compare(a == null ? null : a.EquippedArmor, b == null ? null : b.EquippedArmor),
            valueObjGet = x => x == null ? null : x.EquippedArmor,
            valueObjSet = null,
        };

        /// <summary>
        /// 理想排序标题（按理想值显示数字并排序，选项文本映射未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByIdeal = new SortTitle()
        {
            name = "理想",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : x.ideal.ToString(),
            valueSortFunc = (a, b) => (a == null ? 0 : a.ideal).CompareTo(b == null ? 0 : b.ideal),
            valueObjGet = x => x == null ? 0 : x.ideal,
            valueObjSet = null,
        };

        /// <summary>
        /// 才干排序标题（按才干值显示数字并排序，选项文本映射未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByTalent = new SortTitle()
        {
            name = "才干",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : x.talent.ToString(),
            valueSortFunc = (a, b) => (a == null ? 0 : a.talent).CompareTo(b == null ? 0 : b.talent),
            valueObjGet = x => x == null ? 0 : x.talent,
            valueObjSet = null,
        };

        /// <summary>
        /// 语气排序标题（按语气值显示数字并排序，选项文本映射未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByTone = new SortTitle()
        {
            name = "语气",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : x.tone.ToString(),
            valueSortFunc = (a, b) => (a == null ? 0 : a.tone).CompareTo(b == null ? 0 : b.tone),
            valueObjGet = x => x == null ? 0 : x.tone,
            valueObjSet = null,
        };

        /// <summary>
        /// 声音排序标题（按音声值显示对应语音名，下拉按音声选项修改）
        /// </summary>
        public static SortTitle SortByVoice = new SortTitle()
        {
            name = "声音",
            width = 3.00f,
            valueStrGetCall = x => x == null ? "—" : GetVoiceName(x.voice),
            valueSortFunc = (a, b) => (a == null ? 0 : a.voice).CompareTo(b == null ? 0 : b.voice),
            valueObjGet = x => x == null ? 0 : x.voice,
            valueObjSet = (x, v) => x.voice = (int)v,
            editType = DataEditType.IntDropdown,
            dataSetType = DataSetType.Custom,
            customData = DataEditPresetOptions.VoiceOptions,
        };

        /// <summary>
        /// 立绘排序标题（显示立绘id字符串，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByImage = new SortTitle()
        {
            name = "立绘",
            width = 8.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x => x == null || string.IsNullOrEmpty(x.image) ? "—" : x.image,
            valueSortFunc = (a, b) =>
            {
                string aStr = a == null ? string.Empty : (a.image ?? string.Empty);
                string bStr = b == null ? string.Empty : (b.image ?? string.Empty);
                return aStr.CompareTo(bStr);
            },
            valueObjGet = x => x == null ? null : x.image,
            valueObjSet = (x, v) => x.image = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 老年立绘排序标题（显示老年立绘id字符串，修改方式未定，暂不支持编辑）
        /// </summary>
        public static SortTitle SortByImageOld = new SortTitle()
        {
            name = "老年立绘",
            width = 8.00f,
            alignment = (int)TextAnchor.MiddleLeft,
            valueStrGetCall = x => x == null || string.IsNullOrEmpty(x.image_old) ? "—" : x.image_old,
            valueSortFunc = (a, b) =>
            {
                string aStr = a == null ? string.Empty : (a.image_old ?? string.Empty);
                string bStr = b == null ? string.Empty : (b.image_old ?? string.Empty);
                return aStr.CompareTo(bStr);
            },
            valueObjGet = x => x == null ? null : x.image_old,
            valueObjSet = (x, v) => x.image_old = (string)v,
            editType = DataEditType.Text,
        };

        /// <summary>
        /// 头像排序标题（按头像id显示并排序，点击后通过头像选择窗口修改）
        /// </summary>
        public static SortTitle SortByHeadIconID = new SortTitle()
        {
            name = "头像",
            width = 2.00f,
            valueStrGetCall = x => x == null ? "—" : x.headIconID.ToString(),
            valueSortFunc = (a, b) => (a == null ? 0 : a.headIconID).CompareTo(b == null ? 0 : b.headIconID),
            valueObjGet = x => x == null ? 0 : x.headIconID,
            valueObjSet = (x, v) => x.headIconID = (int)v,
            editType = DataEditType.HeadIcon,
        };

        // ==================== 排序标题辅助方法 ====================

        /// <summary>
        /// 获取武将出生州（州）对象
        /// 出生州字段保存州Id，读取时从当前剧本CommonData的州集合按Id获取
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>州对象，未设置或获取失败时为null</returns>
        private static Province GetBirthplaceProvince(Person person)
        {
            if (person == null || person.birthplace <= 0) return null;
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null) return null;
            return scenario.CommonData.Provinces.Get(person.birthplace);
        }

        /// <summary>
        /// 获取武将出生州的显示名称
        /// </summary>
        /// <param name="person">武将对象</param>
        /// <returns>州名；未设置时返回—，解析失败时返回州Id数字</returns>
        private static string GetBirthplaceProvinceName(Person person)
        {
            if (person == null || person.birthplace <= 0) return "—";
            Province province = GetBirthplaceProvince(person);
            if (province != null) return province.Name;
            return person.birthplace.ToString();
        }

        /// <summary>
        /// 生成人物列表（喜爱/厌恶武将等）的显示文本，名称以顿号分隔
        /// </summary>
        /// <param name="list">人物对象列表</param>
        /// <returns>显示文本，空列表返回—</returns>
        private static string GetPersonListText(SangoObjectList<Person> list)
        {
            if (list == null || list.Count == 0) return "—";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                Person p = list[i];
                if (p == null) continue;
                if (sb.Length > 0) sb.Append("，");
                sb.Append(p.Name);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 比较两个人物列表的长度（用于列表列排序）
        /// </summary>
        /// <param name="a">列表a</param>
        /// <param name="b">列表b</param>
        /// <returns>比较结果</returns>
        private static int CompareListCount(SangoObjectList<Person> a, SangoObjectList<Person> b)
        {
            int aCount = a == null ? 0 : a.Count;
            int bCount = b == null ? 0 : b.Count;
            return aCount.CompareTo(bCount);
        }

        /// <summary>
        /// 生成道具栏显示文本（每类道具名x数量，顿号分隔）
        /// 道具栏按道具类型的storeKind存储数量，名称需到当前剧本CommonData的道具类型集中按storeKind匹配
        /// </summary>
        /// <param name="itemStore">道具栏</param>
        /// <returns>显示文本，空道具栏返回—</returns>
        private static string GetItemStoreText(ItemStore itemStore)
        {
            if (itemStore == null || itemStore.Items == null || itemStore.Items.Count == 0)
                return "—";
            Scenario scenario = Scenario.Cur;
            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<int, int> kv in itemStore.Items)
            {
                int number = kv.Value;
                if (number <= 0) continue;
                string itemName = null;
                if (scenario != null && scenario.CommonData != null)
                {
                    ItemType itemType = scenario.CommonData.ItemTypes.Find(t => t != null && t.storeKind == kv.Key);
                    if (itemType != null) itemName = itemType.Name;
                }
                if (string.IsNullOrEmpty(itemName)) itemName = kv.Key.ToString();
                if (sb.Length > 0) sb.Append("，");
                sb.Append(itemName);
                if (number > 1) sb.Append("x").Append(number);
            }
            return sb.Length == 0 ? "—" : sb.ToString();
        }

        /// <summary>
        /// 获取音声值对应的显示名称
        /// 取值与GameMedia.PlayPersonSay中的语音映射保持一致：
        /// 0男鲁莽、1男刚胆、2男冷静、3男小心、4女刚胆、5女冷静、6吕布、7诸葛亮
        /// </summary>
        /// <param name="voice">音声值</param>
        /// <returns>音声显示名称，未知值直接返回数字</returns>
        private static string GetVoiceName(int voice)
        {
            switch (voice)
            {
                case 0: return "男鲁莽";
                case 1: return "男刚胆";
                case 2: return "男冷静";
                case 3: return "男小心";
                case 4: return "女刚胆";
                case 5: return "女冷静";
                case 6: return "吕布";
                case 7: return "诸葛亮";
                default: return voice.ToString();
            }
        }


    }

}
