using System.Collections;
using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 特性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Official : SangoObjectExtensionData
    {
        [JsonProperty]
        public int troopsLimit;

        [JsonProperty]
        public int cost;

        [JsonProperty]
        public int level;

        [JsonProperty]
        public int meritNeeds;

        [JsonProperty]
        public int commandAdd;

        [JsonProperty]
        public int strengthAdd;

        [JsonProperty]
        public int intelligenceAdd;

        [JsonProperty]
        public int politicsAdd;

        [JsonProperty]
        public int glamourAdd;

        [JsonProperty]
        public string effect_desc;

        [JsonProperty]
        public int commandNeed;

        [JsonProperty]
        public int strengthNeed;

        [JsonProperty]
        public int intelligenceNeed;

        [JsonProperty]
        public int politicsNeed;

        [JsonProperty]
        public int glamourNeed;

        [JsonProperty]
        public int levelNeed;

        [JsonProperty]
        [JsonConverter(typeof(SangoObjectListIDConverter<Skill>))]
        public SangoObjectList<Skill> addSkills = new SangoObjectList<Skill>();

        [JsonProperty]
        [JsonConverter(typeof(SangoObjectListIDConverter<Feature>))]
        public SangoObjectList<Feature> addFeatures = new SangoObjectList<Feature>();

        /// <summary>
        /// 技能效果
        /// </summary>
        [JsonProperty] public JArray officialEffects;

        /// <summary>
        /// 下一级官职
        /// </summary>
        Official[] _next_lvevl_officials;

        public Official[] NextOfficials
        {
            get
            {
                if (meritNeeds > 0 && _next_lvevl_officials == null)
                {
                    // 第一次访问, 缓存
                    List<Official> lvevl_officials = new List<Official>();
                    Scenario.Cur.CommonData.Officials.ForEach(o =>
                    {
                        if (o.level == level - 1)
                        {
                            lvevl_officials.Add(o);
                        }
                    });
                    _next_lvevl_officials = lvevl_officials.ToArray();
                }
                return _next_lvevl_officials;
            }
        }

        public bool CheckPerson(Person person)
        {
            if(person.Command < commandNeed) return false;
            if(person.Strength < strengthNeed) return false;
            if(person.Intelligence < intelligenceNeed) return false;
            if(person.Politics < politicsNeed) return false;
            if(person.Glamour < glamourNeed) return false;
            if(person.Level.Id < levelNeed) return false;
            return true ;
        }

        public void OnPersonAdd(Person person)
        {
            person.command.extra_value += commandAdd;
            person.strength.extra_value += strengthAdd;
            person.intelligence.extra_value += intelligenceAdd;
            person.politics.extra_value += politicsAdd;
            person.glamour.extra_value += glamourAdd;
        }

        public void OnPersonRemove(Person person)
        {
            person.command.extra_value -= commandAdd;
            person.strength.extra_value -= strengthAdd;
            person.intelligence.extra_value -= intelligenceAdd;
            person.politics.extra_value -= politicsAdd;
            person.glamour.extra_value -= glamourAdd;
        }
        protected void InitSkillEffects()
        {
            //if (skill.skillEffects == null) return;
            //if (skill.skillEffects.Count == 0) return;
            //if (effects != null) return;
            //effects = new List<SkillEffect>();
            //for (int i = 0; i < skill.skillEffects.Count; i++)
            //{
            //    JObject valus = skill.skillEffects[i] as JObject;
            //    SkillEffect eft = SkillEffect.Create(valus.Value<string>("class"));
            //    if (eft != null)
            //    {
            //        eft.Init(valus, this);
            //        effects.Add(eft);
            //    }
            //}
        }
    }
}
