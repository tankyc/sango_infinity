using TKNewtonsoft.Json.Linq;
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
            Register("TroopStatusCheck", CraeteHandle<TroopStatusCheck>);
           // Register("TroopStrengthCheck", CraeteHandle<TroopStrengthCheck>);
            Register("TroopMoraleCheck", CraeteHandle<TroopMoraleCheck>);
            Register("TroopAroundTeammateCount", CraeteHandle<TroopAroundTeammateCount>);

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
