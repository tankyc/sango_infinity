using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能成功率逻辑库
    /// </summary>
    public abstract class SkillSpellConditionMethod
    {
        public SkillInstance master;
        public virtual void Init(JObject p, SkillInstance master) { this.master = master; }
        public abstract bool Check(SkillInstance skillInstance, Troop troop, Cell where);
        public virtual void Clear() { }

        public delegate SkillSpellConditionMethod SkillSpellConditionMethodCreator();

        public static Dictionary<string, SkillSpellConditionMethodCreator> CreateMap = new Dictionary<string, SkillSpellConditionMethodCreator>();
        public static void Register(string name, SkillSpellConditionMethodCreator action)
        {
            CreateMap[name] = action;
        }
        public static SkillSpellConditionMethod CraeteHandle<T>() where T : SkillSpellConditionMethod, new()
        {
            return new T();
        }
        public static SkillSpellConditionMethod Create(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;

            SkillSpellConditionMethodCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("CheckDebuffTroop", CraeteHandle<CheckDebuffTroop>);
            Register("CheckFire", CraeteHandle<CheckFire>);

        }

        /// <summary>
        /// 需要Q,R,S,一个相等
        /// </summary>
        public class CheckDebuffTroop : SkillSpellConditionMethod
        {
            public override bool Check(SkillInstance skillInstance, Troop troop, Cell where)
            {
                if (where.troop != null && where.troop.HasControlBuff())
                    return true;

                return false;
            }
        }
        /// <summary>
        /// 需要Q,R,S,一个相等
        /// </summary>
        public class CheckFire : SkillSpellConditionMethod
        {
            public override bool Check(SkillInstance skillInstance, Troop troop, Cell where)
            {
                if (where.fire != null)
                    return true;

                return false;
            }
        }

    }
}
