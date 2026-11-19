using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 官职类型。对齐 Open311 <c>rank_type_t</c>（s11_enum_ui.h:RankTypeEnum）：
    /// <c>RankType_Special = 0 / RankType_Military = 1 / RankType_Civil = 2</c>。
    ///
    /// 用途：官职"自动设定"时按类型选用不同的能力评分公式
    /// （见 <see cref="Official.GetAutoSetScore"/>，原版 s11_sys_ui_rank_dialog.cpp:193-262）。
    /// </summary>
    public enum OfficialRankType
    {
        /// <summary>特殊（三公 / 丞相级）：要求统率 + 智力双高</summary>
        Special = 0,

        /// <summary>军事（将军 / 校尉 / 都督）：要求统率、武力</summary>
        Military = 1,

        /// <summary>文官（九卿 / 尚书 / 掾史）：要求智力、政治</summary>
        Civil = 2,
    }

    /// <summary>
    /// 官职
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Official : SangoObjectExtensionData
    {
        /// <summary>
        /// 官职类型（数据表字段，缺省为 <see cref="OfficialRankType.Civil"/>）。
        /// 数据标注见 Officers.json 的 <c>rankType</c>；对齐原版 <c>RankDialog::get_comp_type</c>
        /// （s11_sys_ui_rank_dialog.cpp:134-186）的分类口径。
        /// </summary>
        [JsonProperty]
        public OfficialRankType rankType = OfficialRankType.Civil;

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

        /// <summary>
        /// 效果短描述（列表里显示，5 个字以内，例如"武+3"）。
        /// 完整说明仍放在 <see cref="effect_desc"/>，由界面在选中该官职时显示到说明栏
        /// （window_official_set_org/root/official_list/offical_effect）。
        /// 老数据没填时由 <see cref="EffectShort"/> 退回详细描述截断，保证列表不会出现空白。
        /// </summary>
        [JsonProperty]
        public string effect_short;

        /// <summary>完整效果说明（多行，含任职条件 / 加成明细）</summary>
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
                    // 【性能】原 ForEach(lambda) 捕获 this / lvevl_officials，改为按下标遍历底层数组。
                    Official[] officialArray = Scenario.Cur.CommonData.Officials.objects;
                    for (int i = 1; i < officialArray.Length; i++)
                    {
                        Official o = officialArray[i];
                        if (o != null && o.level == level - 1)
                            lvevl_officials.Add(o);
                    }
                    _next_lvevl_officials = lvevl_officials.ToArray();
                }
                return _next_lvevl_officials;
            }
        }

        /// <summary>
        /// 担任该官职所需的功绩门槛（缓存）。
        ///
        /// 【对齐说明】Open311 的官职门槛不是官职表字段，而是由"需要的势力称号"换算：
        /// <c>int Database::get_rank_required_kouseki(rank_id): return max(4000 * (TitleId_None - rank->get_required_title_id()), 0);</c>
        /// （s11_db.cpp:787-793）。本项目 <see cref="Official"/> 没有 required_title_id 字段，
        /// 直接用表里的 <see cref="meritNeeds"/> 作为门槛 —— 属数据模型差异。
        ///
        /// 【为什么还要回退】历史数据里 <c>meritNeeds == 0</c> 曾被当作"它不是晋升起点"
        /// （三公 / 大都督等顶级官职只能由 lv1 官职晋升而来，见 <see cref="NextOfficials"/> 的构建条件），
        /// 若直接拿 0 当门槛，这些顶级官职会变成零门槛、人人可当。
        /// 2026-11 重排门槛后所有有效官职都填了正数（顶级 = 32000，对齐原版最高档），
        /// 这里仅作老存档 / 未更新数据的兜底：回退到"下一级（level + 1）官职中的最低门槛"。
        /// </summary>
        public int RequiredMerit
        {
            get
            {
                if (meritNeeds > 0)
                    return meritNeeds;
                if (cachedRequiredMerit >= 0)
                    return cachedRequiredMerit;

                Scenario scenario = Scenario.Cur;
                if (scenario == null || scenario.CommonData == null || scenario.CommonData.Officials == null)
                    return 0;   // 剧本上下文未就绪时不缓存

                int best = int.MaxValue;
                Official[] array = scenario.CommonData.Officials.objects;
                for (int i = 1; i < array.Length; i++)
                {
                    Official o = array[i];
                    if (o == null || o == this)
                        continue;
                    if (o.level == level + 1 && o.meritNeeds > 0 && o.meritNeeds < best)
                        best = o.meritNeeds;
                }

                cachedRequiredMerit = best == int.MaxValue ? 0 : best;
                return cachedRequiredMerit;
            }
        }

        int cachedRequiredMerit = -1;

        /// <summary>
        /// 列表用的效果短描述（5 个字以内）。
        /// 优先取数据里的 <see cref="effect_short"/>；没填时退回 <see cref="effect_desc"/> 的前 5 个字，
        /// 这样即使跑的是没更新过的官职表（Build/Content 下那份老数据），列表也不会整列空白。
        /// </summary>
        public string EffectShort
        {
            get
            {
                if (!string.IsNullOrEmpty(effect_short))
                    return effect_short;
                if (string.IsNullOrEmpty(effect_desc))
                    return "";
                return effect_desc.Length > 5 ? effect_desc.Substring(0, 5) : effect_desc;
            }
        }

        public bool CheckPerson(Person person)
        {
            // 【防御】Level 是"武将等级"对象引用，由 OnScenarioPrepare 兜底赋值（Person.cs:1866-1867），
            // 但等级表为空或武将未经准备流程时可能为 null。调用方（官职界面的 CanAssign、
            // 自动设定的 GetAutoSetScore）都只期望"合格/不合格"，这里按"不合格"处理，
            // 避免 AI 每 3 回合的全量封官因为一个数据缺口抛 NullReferenceException。
            if (person == null || person.Level == null) return false;
            if(person.Command < commandNeed) return false;
            if(person.Strength < strengthNeed) return false;
            if(person.Intelligence < intelligenceNeed) return false;
            if(person.Politics < politicsNeed) return false;
            if(person.Glamour < glamourNeed) return false;
            if(person.Level.Id < levelNeed) return false;
            return true ;
        }

        /// <summary>
        /// 自动设定评分：某人担任某官职的适配分（越大越合适）。
        /// 返回 <see cref="int.MinValue"/> 表示"不参与"（不合格或不该被自动选中）。
        ///
        /// 【出处】Open311 <c>RankDialog::find_best_ranker</c>（s11_sys_ui_rank_dialog.cpp:193-262），
        /// 逐分支照搬：
        /// <code>
        /// if (person->get_loyalty() >= 90) {                 // 203 行：忠诚门槛
        ///     if (strict) {
        ///         Special : stat = 统率 + 智力;   stat >= 150 → score = stat + min(loyalty-90,10)*9
        ///         Military: stat = max(统率,武力); stat >= 60  → score = 统率 + 武力 + min(loyalty-90,10)*9
        ///         Civil   : stat = max(智力,政治); stat >= 70  → score = stat + min(loyalty-90,10)*9
        ///     } else {
        ///         a = max(统率,武力); b = max(智力,政治);
        ///         Military: a &lt; b → 不选，否则 score = a
        ///         default : b &lt;= a → 不选，否则 score = b     // Special / Civil 都走 default
        ///     }
        /// }
        /// </code>
        ///
        /// 【为什么非 strict 分支"不满足就返回 MinValue"】原版把 score 停在 INT_MIN，
        /// 后续 <c>if (best_score &lt; score)</c> 永远不会成立，等价于"这个人不参与第二轮"。
        /// 换句话说第二轮是**严格互补**的：军事官职只给武将、文官/三公只给文臣，
        /// 而不是"随便找个人填坑"。这里用 MinValue 显式表达，避免误当成 0 分参与比较。
        /// </summary>
        /// <param name="person">候选武将</param>
        /// <param name="official">目标官职</param>
        /// <param name="strict">true = 第一轮（要求能力下限），false = 第二轮（只按互补性挑选）</param>
        public static int GetAutoSetScore(Person person, Official official, bool strict)
        {
            if (person == null || official == null)
                return int.MinValue;
            if (!person.IsAlive)
                return int.MinValue;
            // 原版 203 行：忠诚 < 90 的人不参与自动设定
            if (person.loyalty < 90)
                return int.MinValue;
            // 原版 200 行的 can_set：官职有效性 + 功绩门槛 + 属性门槛
            if (official.Id == 0)
                return int.MinValue;
            if (person.merit < official.RequiredMerit)
                return int.MinValue;
            if (!official.CheckPerson(person))
                return int.MinValue;

            int command = person.Command;
            int strength = person.Strength;
            int intelligence = person.Intelligence;
            int politics = person.Politics;
            // 原版 215/223/231 行：min(loyalty - 90, 10)，取值 0..10
            int loyaltyBonus = System.Math.Min(person.loyalty - 90, 10);

            if (strict)
            {
                switch (official.rankType)
                {
                    case OfficialRankType.Special:
                        {
                            int stat = command + intelligence;
                            if (stat >= 150)
                                return stat + loyaltyBonus * 9;
                            return int.MinValue;
                        }
                    case OfficialRankType.Military:
                        {
                            int stat = System.Math.Max(command, strength);
                            if (stat >= 60)
                                return command + strength + loyaltyBonus * 9;
                            return int.MinValue;
                        }
                    default:
                        {
                            int stat = System.Math.Max(intelligence, politics);
                            if (stat >= 70)
                                return stat + loyaltyBonus * 9;
                            return int.MinValue;
                        }
                }
            }

            int a = System.Math.Max(command, strength);
            int b = System.Math.Max(intelligence, politics);
            if (official.rankType == OfficialRankType.Military)
            {
                // 原版 243-247 行：武不如文 → 不选
                if (a < b)
                    return int.MinValue;
                return a;
            }

            // 原版 248-251 行的 default：文不高于武 → 不选（Special 与 Civil 都走这里）
            if (b <= a)
                return int.MinValue;
            return b;
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
