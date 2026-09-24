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

        // 序列化形态保持 int[]（键名不变，老存档可读）；运行期列表由 OnScenarioPrepare 解析。
        [JsonProperty("addSkills")]
        public int[] addSkills_list;
        public SangoObjectList<Skill> addSkills = new SangoObjectList<Skill>();

        [JsonProperty("addFeatures")]
        public int[] addFeatures_list;
        public SangoObjectList<Feature> addFeatures = new SangoObjectList<Feature>();

        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            if (addSkills_list != null && addSkills_list.Length > 0 && addSkills.Count == 0)
                addSkills.FromArray(addSkills_list);
            if (addFeatures_list != null && addFeatures_list.Length > 0 && addFeatures.Count == 0)
                addFeatures.FromArray(addFeatures_list);
        }

        /// <summary>存档前回写 int[]（否则会把读档时的旧 id 存回去）。</summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            addSkills_list = addSkills != null ? addSkills.ToArray() : null;
            addFeatures_list = addFeatures != null ? addFeatures.ToArray() : null;
        }

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
