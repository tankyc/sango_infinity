using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 条件基类，所有游戏中的条件判断都继承自此类
    /// </summary>
    public abstract class Condition
    {
        /// <summary>
        /// 初始化条件
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public abstract void Init(JObject p, params SangoObject[] sangoObjects);


        /// <summary>
        /// 检查条件是否满足
        /// </summary>
        /// <param name="database">检查条件所需的对象</param>
        /// <returns>条件是否满足</returns>
        public abstract bool Check(IConditionDatabase database);


        /// <summary>
        /// 条件创建委托
        /// </summary>
        public delegate Condition ConditionCreator();

        /// <summary>
        /// 条件创建映射表
        /// </summary>
        public static Dictionary<string, ConditionCreator> CreateMap = new Dictionary<string, ConditionCreator>();

        /// <summary>
        /// 注册条件
        /// </summary>
        /// <param name="name">条件名称</param>
        /// <param name="action">条件创建方法</param>
        public static void Register(string name, ConditionCreator action)
        {
            CreateMap[name] = action;
        }

        /// <summary>
        /// 创建条件实例的通用方法
        /// </summary>
        /// <typeparam name="T">条件类型</typeparam>
        /// <returns>条件实例</returns>
        public static Condition CraeteHandle<T>() where T : Condition, new()
        {
            return new T();
        }

        /// <summary>
        /// 根据名称创建条件实例
        /// </summary>
        /// <param name="name">条件名称</param>
        /// <returns>条件实例</returns>
        public static Condition Create(string name)
        {
            ConditionCreator actionBaseCreator;
            if (CreateMap.TryGetValue(name, out actionBaseCreator))
                return actionBaseCreator();
            return null;
        }

        /// <summary>
        /// 初始化所有条件
        /// </summary>
        public static void Init()
        {
            // core
            Register("and", CraeteHandle<ConditionAnd>);
            Register("or", CraeteHandle<ConditionOr>);
            Register("andList", CraeteHandle<ConditionAndList>);
            Register("orList", CraeteHandle<ConditionOrList>);
            Register("DistanceCheck", CraeteHandle<DistanceCheck>);
            Register("ProbabilityCheck", CraeteHandle<ProbabilityCheck>);
            
            // Troop
            Register("TroopAttributeCompare", CraeteHandle<TroopAttributeCompare>);
            // 【补注册】部队自身属性与常量比较（attributeType / compareTarget / operator / value）。
            // 该类早已实现（Game/Condition/Troop/TroopAttributeCheck.cs），但一直没进注册表，
            // 于是 Condition.Create("TroopAttributeCheck") 恒返回 null：
            //   · TroopTroopActionBase 会打 Error（外层条件是 andList，注册成功，所以看不到）；
            //   · ConditionAndList 会**静默丢弃**为 null 的子条件。
            // 后果：Features.json 28「不屈」的"守方兵力≤3000"这一条被整条丢掉，
            // 特技退化成"任何普攻 50% 概率把伤害改成 1"，表现为"攻击不掉血"。
            Register("TroopAttributeCheck", CraeteHandle<TroopAttributeCheck>);
            Register("TroopStatusCheck", CraeteHandle<TroopStatusCheck>);
           // Register("TroopStrengthCheck", CraeteHandle<TroopStrengthCheck>);
            Register("TroopMoraleCheck", CraeteHandle<TroopMoraleCheck>);
            Register("TroopAroundTeammateCount", CraeteHandle<TroopAroundTeammateCount>);
            // 邻接敌军数量：与友军版对称（只数紧邻一圈），支撑"被包围才生效"类特技
            Register("TroopAroundEnemyCount", CraeteHandle<TroopAroundEnemyCount>);
            // 兵种适性检查：比较部队(自己或目标)指定兵种的适性等级
            Register("TroopAbilityCheck", CraeteHandle<TroopAbilityCheck>);
            // 双方属性对比：施法方属性 op 目标方属性（骁将/步将/无双/龙胆/名将/游侠等
            // "对武力或统率低于自己的敌人"类判定，TroopAttributeCheck 只能与常量比）
            Register("TroopAttributeCompareBoth", CraeteHandle<TroopAttributeCompareBoth>);

            // Skill
            Register("SkillIsCritical", CraeteHandle<SkillIsCritical>);
            Register("SkillIsNormalSkill", CraeteHandle<SkillIsNormalSkill>);
            Register("SkillIsStrategySkill", CraeteHandle<SkillIsStrategySkill>);
            Register("SkillTargetCellCheck", CraeteHandle<SkillTargetCellCheck>);
            Register("SkillIdInList", CraeteHandle<SkillIdInList>);
            
            // Person
            Register("PersonAttributeCompare", CraeteHandle<PersonAttributeCheck>);
            Register("PersonLoyaltyCheck", CraeteHandle<PersonLoyaltyCheck>);
            Register("PersonLevelCheck", CraeteHandle<PersonLevelCheck>);
            // 武将关系检查：夫妻 / 同胞 / 义兄弟 / 亲子 / 血亲 / 亲爱 / 厌恶（需 ActionPerson 与 TargetPerson 同时在场上）
            Register("PersonRelationCheck", CraeteHandle<PersonRelationCheck>);
            // 君主身份检查：result 1 = 要求是所在势力君主，0 = 要求不是（特技「桀骜不羁」用）
            Register("PersonIsGovernor", CraeteHandle<PersonIsGovernor>);
            // 性别检查：compareTarget（self/target，缺省 target）+ sex（0 男 / 1 女）
            // 用于"只对异性生效"的特技（美人计 / 洛神 / 国色天香 / 红颜）
            Register("PersonSexCheck", CraeteHandle<PersonSexCheck>);
            // 年龄检查：compareTarget（self/target，缺省 target）+ operator（缺省 gte）+ age
            // 用于"老当益壮""少年英雄"一类按年龄生效的设计
            Register("PersonAgeCheck", CraeteHandle<PersonAgeCheck>);

            // Terrain
            Register("TerrainCheck", CraeteHandle<TerrainCheck>);

            // Weather
            Register("WeatherCheck", CraeteHandle<WeatherCheck>);

            // Faction
            Register("FactionCheck", CraeteHandle<FactionCheck>);

            // Resource
            //Register("ResourceCheck", CraeteHandle<ResourceCheck>);

            // City
            Register("CityAttributeCheck", CraeteHandle<CityAttributeCheck>);

            // Time
            //Register("TimeCheck", CraeteHandle<TimeCheck>);
        }
    }
}
