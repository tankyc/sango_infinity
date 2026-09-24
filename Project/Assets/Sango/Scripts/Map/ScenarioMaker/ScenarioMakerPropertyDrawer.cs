using Sango.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;


namespace Sango.ScenarioMaker
{
    /// <summary>
    /// 剧本编辑器通用属性绘制器
    /// 仅处理带有[JsonProperty]标记的成员
    /// </summary>
    public static class ScenarioMakerPropertyDrawer
    {
        /// <summary>
        /// 场景对象集合字段映射
        /// </summary>
        private static readonly Dictionary<Type, string> ScenarioSetMap = new Dictionary<Type, string>
        {
            { typeof(Person), "personSet" },
            { typeof(Force), "forceSet" },
            { typeof(Corps), "corpsSet" },
            { typeof(City), "citySet" },
            { typeof(Troop), "troopsSet" },
            { typeof(Building), "buildingSet" },
            { typeof(Fire), "fireSet" },
            { typeof(Alliance), "allianceSet" }
        };

        /// <summary>
        /// 公共数据集合属性映射
        /// </summary>
        private static readonly Dictionary<Type, string> CommonDataSetMap = new Dictionary<Type, string>
        {
            { typeof(Personality), "Personalities" },
            { typeof(Argumentation), "Argumentations" },
            { typeof(Official), "Officials" },
            { typeof(PersonLevel), "PersonLevels" },
            { typeof(Flag), "Flags" },
            { typeof(Title), "Titles" },
            { typeof(BuildingType), "BuildingTypes" },
            { typeof(CityLevelType), "CityLevelTypes" },
            { typeof(Province), "Provinces" },
            { typeof(Region), "Regions" },
            { typeof(Technique), "Techniques" },
            { typeof(Skill), "Skills" },
            { typeof(TerrainType), "TerrainTypes" },
            { typeof(TroopType), "TroopTypes" },
            { typeof(TroopAnimation), "TroopAnimations" },
            { typeof(Feature), "Features" },
            { typeof(PersonAttributeType), "PersonAttributeTypes" },
            { typeof(AttributeChangeType), "AttributeChangeTypes" },
            { typeof(ItemType), "ItemTypes" },
            { typeof(JobType), "JobTypes" },
            { typeof(Buff), "Buffs" }
        };

        /// <summary>
        /// 绘制目标对象的所有[JsonProperty]成员
        /// </summary>
        /// <param name="target">目标对象</param>
        /// <param name="scenario">当前剧本</param>
        /// <returns>是否有任意字段被修改</returns>
        public static bool DrawObject(object target, Scenario scenario)
        {
            if (target == null)
            {
                return false;
            }

            bool changed = false;
            Type type = target.GetType();
            IEnumerable<MemberInfo> members = GetJsonMembers(type);
            foreach (MemberInfo member in members)
            {
                if (DrawMember(member, target, scenario))
                {
                    changed = true;
                }
            }
            return changed;
        }

        /// <summary>
        /// 获取所有带[JsonProperty]的字段和属性，按Order排序
        /// </summary>
        private static IEnumerable<MemberInfo> GetJsonMembers(Type type)
        {
            MemberInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<JsonPropertyAttribute>() != null)
                .Cast<MemberInfo>()
                .ToArray();
            MemberInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.GetCustomAttribute<JsonPropertyAttribute>() != null)
                .Cast<MemberInfo>()
                .ToArray();
            return fields.Concat(properties).OrderBy(GetOrder);
        }

        /// <summary>
        /// 获取[JsonProperty]的Order值
        /// </summary>
        private static int GetOrder(MemberInfo member)
        {
            JsonPropertyAttribute attr = member.GetCustomAttribute<JsonPropertyAttribute>();
            return attr?.Order ?? 0;
        }

        /// <summary>
        /// 绘制单个成员
        /// </summary>
        private static bool DrawMember(MemberInfo member, object target, Scenario scenario)
        {
            Type memberType = GetMemberType(member);
            object value = GetValue(member, target);
            string name = member.Name;

            GUILayout.BeginHorizontal();
            GUILayout.Label(name, GUILayout.Width(120));
            bool changed = DrawValue(member, target, memberType, value, scenario);
            GUILayout.EndHorizontal();
            return changed;
        }

        /// <summary>
        /// 绘制成员的值控件
        /// </summary>
        private static bool DrawValue(MemberInfo member, object target, Type type, object value, Scenario scenario)
        {
            if (type == typeof(int))
            {
                int v = value == null ? 0 : (int)value;
                string text = GUILayout.TextField(v.ToString(), GUILayout.Width(120));
                if (int.TryParse(text, out int newValue) && newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(float))
            {
                float v = value == null ? 0f : (float)value;
                string text = GUILayout.TextField(v.ToString(), GUILayout.Width(120));
                if (float.TryParse(text, out float newValue) && newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(double))
            {
                double v = value == null ? 0.0 : (double)value;
                string text = GUILayout.TextField(v.ToString(), GUILayout.Width(120));
                if (double.TryParse(text, out double newValue) && newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(string))
            {
                string v = value as string ?? "";
                string newValue = GUILayout.TextField(v, GUILayout.Width(200));
                if (newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(bool))
            {
                bool v = value != null && (bool)value;
                bool newValue = GUILayout.Toggle(v, "");
                if (newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(byte))
            {
                byte v = value == null ? (byte)0 : (byte)value;
                string text = GUILayout.TextField(v.ToString(), GUILayout.Width(120));
                if (byte.TryParse(text, out byte newValue) && newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type.IsEnum)
            {
                int v = value == null ? 0 : (int)value;
                int[] values = Enum.GetValues(type).Cast<int>().ToArray();
                string[] names = Enum.GetNames(type);
                int index = System.Math.Max(0, Array.IndexOf(values, v));
                int newIndex = GUILayout.SelectionGrid(index, names, System.Math.Min(names.Length, 4));
                if (newIndex != index && newIndex >= 0 && newIndex < values.Length)
                {
                    SetValue(member, target, Enum.ToObject(type, values[newIndex]));
                    return true;
                }
                return false;
            }

            if (type == typeof(int[]))
            {
                int[] array = value as int[];
                string text = array == null ? "" : string.Join(",", array.Select(x => x.ToString()).ToArray());
                string newText = GUILayout.TextField(text, GUILayout.Width(200));
                if (newText != text)
                {
                    try
                    {
                        int[] newArray = newText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => int.Parse(x.Trim()))
                            .ToArray();
                        SetValue(member, target, newArray);
                        return true;
                    }
                    catch
                    {
                        // 输入格式错误时不更新
                    }
                }
                return false;
            }

            if (type == typeof(string[]))
            {
                string[] array = value as string[];
                string text = array == null ? "" : string.Join(",", array);
                string newText = GUILayout.TextField(text, GUILayout.Width(200));
                if (newText != text)
                {
                    SetValue(member, target, newText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray());
                    return true;
                }
                return false;
            }

            if (type == typeof(long))
            {
                long v = value == null ? 0L : (long)value;
                string text = GUILayout.TextField(v.ToString(), GUILayout.Width(120));
                if (long.TryParse(text, out long newValue) && newValue != v)
                {
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }

            if (type == typeof(PersonAttributeValue))
            {
                return DrawPersonAttributeValue(member, target, value as PersonAttributeValue);
            }

            if (typeof(SangoObject).IsAssignableFrom(type))
            {
                return DrawSangoObjectReference(member, target, type, value as SangoObject, scenario);
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(SangoObjectList<>))
            {
                object list = value;
                int count = 0;
                if (list != null)
                {
                    try
                    {
                        count = (int)type.GetProperty("Count")?.GetValue(list);
                    }
                    catch
                    {
                        count = 0;
                    }
                }
                GUILayout.Label($"列表元素数: {count}");
                return false;
            }

            GUILayout.Label($"[{type.Name}]");
            return false;
        }

        /// <summary>
        /// 绘制PersonAttributeValue的简易编辑
        /// </summary>
        private static bool DrawPersonAttributeValue(MemberInfo member, object target, PersonAttributeValue value)
        {
            bool changed = false;
            PersonAttributeValue pav = value ?? new PersonAttributeValue();

            GUILayout.BeginVertical(GUILayout.Width(240));
            GUILayout.BeginHorizontal();
            GUILayout.Label("基础值", GUILayout.Width(60));
            string text = GUILayout.TextField(pav.baseValue.ToString(), GUILayout.Width(60));
            if (int.TryParse(text, out int newBase) && newBase != pav.baseValue)
            {
                pav.baseValue = newBase;
                changed = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("经验", GUILayout.Width(60));
            text = GUILayout.TextField(pav.valueExp.ToString(), GUILayout.Width(60));
            if (int.TryParse(text, out int newExp) && newExp != pav.valueExp)
            {
                pav.valueExp = newExp;
                changed = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            if (value == null && (pav.baseValue != 0 || pav.valueExp != 0))
            {
                SetValue(member, target, pav);
                changed = true;
            }
            else if (changed)
            {
                SetValue(member, target, pav);
            }
            return changed;
        }

        /// <summary>
        /// 绘制场景对象引用，数量较少时显示下拉菜单，否则显示ID输入框
        /// </summary>
        private static bool DrawSangoObjectReference(MemberInfo member, object target, Type type, SangoObject value, Scenario scenario)
        {
            List<SangoObject> candidates = GetCandidates(type, scenario);
            int currentId = value == null ? 0 : value.Id;

            if (candidates.Count <= 30)
            {
                List<SangoObject> options = new List<SangoObject> { null };
                List<string> names = new List<string> { "无" };
                foreach (SangoObject obj in candidates)
                {
                    if (obj != null)
                    {
                        options.Add(obj);
                        names.Add($"{obj.Id}:{obj.Name}");
                    }
                }

                int index = System.Math.Max(0, options.IndexOf(value));
                int newIndex = Sango.Tools.EditorUtility.Popup(index, names.ToArray(), GUILayout.Width(120));
                if (newIndex != index && newIndex >= 0 && newIndex < options.Count)
                {
                    SetValue(member, target, options[newIndex]);
                    return true;
                }
                return false;
            }
            else
            {
                string text = GUILayout.TextField(currentId.ToString(), GUILayout.Width(80));
                if (int.TryParse(text, out int newId) && newId != currentId)
                {
                    SangoObject newValue = newId == 0 ? null : scenario.GetObject(newId, type) as SangoObject;
                    SetValue(member, target, newValue);
                    return true;
                }
                return false;
            }
        }


        /// <summary>
        /// 获取指定类型的候选对象列表
        /// </summary>
        private static List<SangoObject> GetCandidates(Type type, Scenario scenario)
        {
            List<SangoObject> result = new List<SangoObject>();
            if (scenario == null)
            {
                return result;
            }

            if (ScenarioSetMap.TryGetValue(type, out string fieldName))
            {
                FieldInfo field = typeof(Scenario).GetField(fieldName);
                if (field != null)
                {
                    IDatabase set = field.GetValue(scenario) as IDatabase;
                    if (set != null)
                    {
                        set.ForEach(obj =>
                        {
                            if (obj != null && obj is SangoObject so)
                            {
                                result.Add(so);
                            }
                        });
                    }
                    else
                    {
                        PropertyInfo propertyInfo = typeof(ScenarioCommonData).GetProperty(fieldName);
                        if (propertyInfo != null)
                        {
                            set = propertyInfo.GetValue(scenario.CommonData) as IDatabase;
                            if (set != null)
                            {
                                set.ForEach(obj =>
                                {
                                    if (obj != null && obj is SangoObject so)
                                    {
                                        result.Add(so);
                                    }
                                });
                            }
                        }
                    }
                }
                return result;
            }

            if (CommonDataSetMap.TryGetValue(type, out string commonDataFieldName))
            {
                if (scenario.CommonData != null)
                {
                    FieldInfo field = typeof(ScenarioCommonData).GetField(commonDataFieldName);
                    if (field != null)
                    {
                        IDatabase set = field.GetValue(scenario.CommonData) as IDatabase;
                        if (set != null)
                        {
                            set.ForEach(obj =>
                            {
                                if (obj != null && obj is SangoObject so)
                                {
                                    result.Add(so);
                                }
                            });
                        }
                    }
                    else
                    {
                        PropertyInfo propertyInfo = typeof(ScenarioCommonData).GetProperty(commonDataFieldName);
                        if (propertyInfo != null)
                        {
                            IDatabase set = propertyInfo.GetValue(scenario.CommonData) as IDatabase;
                            if (set != null)
                            {
                                set.ForEach(obj =>
                                {
                                    if (obj != null && obj is SangoObject so)
                                    {
                                        result.Add(so);
                                    }
                                });
                            }
                        }
                    }
                }
                return result;
            }

            return result;
        }

        /// <summary>
        /// 获取成员类型
        /// </summary>
        private static Type GetMemberType(MemberInfo member)
        {
            if (member is FieldInfo field)
            {
                return field.FieldType;
            }
            if (member is PropertyInfo property)
            {
                return property.PropertyType;
            }
            return typeof(object);
        }

        /// <summary>
        /// 读取成员值
        /// </summary>
        private static object GetValue(MemberInfo member, object target)
        {
            if (member is FieldInfo field)
            {
                return field.GetValue(target);
            }
            if (member is PropertyInfo property)
            {
                return property.GetValue(target, null);
            }
            return null;
        }

        /// <summary>
        /// 设置成员值
        /// </summary>
        private static void SetValue(MemberInfo member, object target, object value)
        {
            if (member is FieldInfo field)
            {
                field.SetValue(target, value);
            }
            else if (member is PropertyInfo property)
            {
                property.SetValue(target, value, null);
            }
        }
    }
}
