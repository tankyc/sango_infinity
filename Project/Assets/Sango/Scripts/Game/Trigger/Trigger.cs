using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class Trigger : IConditionDatabase
    {
        public virtual SkillInstance ActionSkill => null;
        public virtual SkillInstance TargetSkill => null;
        public virtual Person ActionPerson => null;
        public virtual Person TargetPerson => null;
        public virtual Troop ActionTroop => null;
        public virtual Troop TargetTroop => null;
        public virtual Cell ActionCell => null;
        public virtual Cell TargetCell => null;
        public virtual City ActionCity => null;
        public virtual City TargetCity => null;
        public virtual Corps ActionCorps => null;
        public virtual Corps TargetCorps => null;
        public virtual Force ActionForce => null;
        public virtual Force TargetForce => null;

        public virtual Fire ActiveFire => null;
        public virtual Fire TargetFire => null;
        public virtual object ActionObject => null;
        public virtual object TargetObject => null;
        public virtual Tools.OverrideData<int> DamageOverride => null;

        public delegate void TriggerCall(Trigger trigger);
        public TriggerCall triggerCall;
        public virtual Trigger Clone()
        {
            return null;
        }

        public virtual void Init(TriggerCall call)
        {
            triggerCall = call;
        }

        public virtual void Clear()
        {
        }

        /// <summary>
        /// 动作创建委托
        /// </summary>
        public delegate Trigger TriggerCreator();

        /// <summary>
        /// 动作创建映射
        /// </summary>
        public static Dictionary<string, TriggerCreator> CreateMap = new Dictionary<string, TriggerCreator>();
        /// <summary>
        /// 注册动作
        /// </summary>
        /// <param name="name">动作名称</param>
        /// <param name="action">动作创建器</param>
        public static void Register(string name, TriggerCreator action)
        {
            CreateMap[name] = action;
        }

        /// <summary>
        /// 创建动作处理器
        /// </summary>
        /// <typeparam name="T">动作类型</typeparam>
        /// <returns>动作实例</returns>
        public static Trigger CraeteHandle<T>() where T : Trigger, new()
        {
            return new T();
        }
        /// <summary>
        /// 创建动作
        /// </summary>
        /// <param name="name">动作名称</param>
        /// <returns>动作实例</returns>
        public static Trigger Create(string name)
        {
            TriggerCreator actionBaseCreator;
            if (CreateMap.TryGetValue(name, out actionBaseCreator))
                return actionBaseCreator();
            return null;
        }

        /// <summary>
        /// 初始化所有动作
        /// </summary>
        public static void Init()
        {
            Register("TriggerWhenSkillHitTroop", CraeteHandle<TriggerWhenSkillHitTroop>);
            Register("TriggerWhenSkillActionEnd", CraeteHandle<TriggerWhenSkillActionEnd>);
            Register("TriggerWhenSkillAfterHitTroop", CraeteHandle<TriggerWhenSkillAfterHitTroop>);

        }
    }
}
