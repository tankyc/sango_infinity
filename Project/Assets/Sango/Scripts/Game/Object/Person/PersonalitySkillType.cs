/*
 * 文件名：PersonalitySkillType.cs
 * 描述：性格加成所对应的计略类型。
 *       用于把"性格对计略的抗性 / 专精"与 Skills.json 里的计略技能 Id 建立映射，
 *       使新增计略时只需在此处补一项映射，而不必散落硬编码。
 */

namespace Sango.Core
{
    /// <summary>
    /// 性格加成对应的计略类型。
    ///
    /// 与 <c>Skills.json</c> 中 <c>kind == 3</c> 的计略技能一一对应：
    /// <code>
    ///   Id 22 火计 / 23 灭火  —— 不属于性格表
    ///   Id 24 伪报      → FalseReport
    ///   Id 25 扰乱      → Disturb
    ///   Id 26 镇静      → Calmdown
    ///   Id 27 伏兵      → Ambush
    ///   Id -- 妖术      → Sorcery     （技能表暂缺，保留占位）
    ///   Id 28 内讧      → Infighting
    /// </code>
    /// </summary>
    public enum PersonalitySkillType : int
    {
        /// <summary>未知 / 不参与性格表</summary>
        None = -1,
        /// <summary>伪报</summary>
        FalseReport = 0,
        /// <summary>扰乱</summary>
        Disturb = 1,
        /// <summary>镇静</summary>
        Calmdown = 2,
        /// <summary>伏兵</summary>
        Ambush = 3,
        /// <summary>妖术（技能表暂缺，保留占位）</summary>
        Sorcery = 4,
        /// <summary>内讧</summary>
        Infighting = 5,
    }

    /// <summary>
    /// 计略技能与 <see cref="PersonalitySkillType"/> 的映射。
    ///
    /// 【数据驱动】归属关系由 <c>Skills.json</c> 的 <c>personalitySkillType</c> 字段声明，
    /// 代码里**不再出现任何技能 Id**。新增计略时只需在技能表里填上分类值，
    /// 性格的抗性 / 专精自动生效。
    /// </summary>
    public static class PersonalitySkillMap
    {
        /// <summary>
        /// 把技能 Id 转换为性格计略类型。
        /// </summary>
        /// <param name="skillId">技能 Id</param>
        /// <returns>对应类型；未配置或不属于性格表的技能返回 <see cref="PersonalitySkillType.None"/></returns>
        public static PersonalitySkillType ToSkillType(int skillId)
        {
            if (skillId < 0) return PersonalitySkillType.None;

            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.CommonData == null || scenario.CommonData.Skills == null)
                return PersonalitySkillType.None;

            Skill skill = scenario.Id2Object(scenario.CommonData.Skills, skillId);
            if (skill == null || skill.personalitySkillType < 0)
                return PersonalitySkillType.None;

            return (PersonalitySkillType)skill.personalitySkillType;
        }

        /// <summary>
        /// 直接按技能对象取性格计略类型（已有 <see cref="Skill"/> 引用时优先用这个，少一次查表）。
        /// </summary>
        public static PersonalitySkillType ToSkillType(Skill skill)
        {
            if (skill == null || skill.personalitySkillType < 0)
                return PersonalitySkillType.None;
            return (PersonalitySkillType)skill.personalitySkillType;
        }

        /// <summary>
        /// 安全取得某武将性格对指定计略的**抗性加成**（作为目标时）。
        /// </summary>
        /// <param name="person">武将（可为 null）</param>
        /// <param name="type">计略类型</param>
        /// <returns>成功率加成；武将或性格缺失时返回 0</returns>
        public static int GetResistAdd(Person person, PersonalitySkillType type)
        {
            if (person == null || person.mPersonality == null)
                return 0;
            return person.mPersonality.GetSkillResistAdd(type);
        }

        /// <summary>
        /// 安全取得某武将性格对指定计略的**专精加成**（作为施法者时）。
        /// </summary>
        /// <param name="person">武将（可为 null）</param>
        /// <param name="type">计略类型</param>
        /// <returns>暴击率加成；武将或性格缺失时返回 0</returns>
        public static int GetMasteryAdd(Person person, PersonalitySkillType type)
        {
            if (person == null || person.mPersonality == null)
                return 0;
            return person.mPersonality.GetSkillMasteryAdd(type);
        }
    }
}
