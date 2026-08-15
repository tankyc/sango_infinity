using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能暴击逻辑库
    /// </summary>
    public abstract class SkillCriticalMethod
    {
        public SkillInstance master;
        public virtual void Init(JObject p, SkillInstance master) { this.master = master; }
        public abstract int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell);
        public virtual void Clear() { }

        public delegate SkillCriticalMethod SkillCriticalMethodCreator();

        public static Dictionary<string, SkillCriticalMethodCreator> CreateMap = new Dictionary<string, SkillCriticalMethodCreator>();
        public static void Register(string name, SkillCriticalMethodCreator action)
        {
            CreateMap[name] = action;
        }
        public static SkillCriticalMethod CraeteHandle<T>() where T : SkillCriticalMethod, new()
        {
            return new T();
        }
        public static SkillCriticalMethod Create(string name)
        {

            if (string.IsNullOrEmpty(name)) return null;

            SkillCriticalMethodCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("CommonMethod", CraeteHandle<CommonMethod>);
            Register("FireCriticalMethod", CraeteHandle<FireCriticalMethod>);
            Register("PutOutFireCriticalMethod", CraeteHandle<PutOutFireCriticalMethod>);
            Register("FalseReportCriticalMethod", CraeteHandle<FalseReportCriticalMethod>);
            Register("DisturbCriticalMethod", CraeteHandle<DisturbCriticalMethod>);
            Register("CalmdownCriticalMethod", CraeteHandle<CalmdownCriticalMethod>);
            Register("AmbushCriticalMethod", CraeteHandle<AmbushCriticalMethod>);
            Register("InfightingCriticalMethod", CraeteHandle<InfightingCriticalMethod>);
            Register("SorceryCriticalMethod", CraeteHandle<SorceryCriticalMethod>);
            Register("ThunderboltCriticalMethod", CraeteHandle<ThunderboltCriticalMethod>);
        }

        /// <summary>
        /// 常规战法暴击率
        /// </summary>
        public class CommonMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                ScenarioVariables scenarioVariables = Scenario.Cur.Variables;
                int basCriticalRate = scenarioVariables.baseSkillCriticalRate + troop.TroopTypeLv * Scenario.Cur.Variables.skillCriticalRateAddByAbility;
                basCriticalRate += Mathf.Max(0, (troop.Strength - 60) * scenarioVariables.skillCriticalRateAddByStength / 10);
                return basCriticalRate;
            }
        }

        /*
             1)	火计：
                    火计爆击率 ＝ (A智-B智/2)*0.1
                    注：对火计而言，如果被放计对象不是部队，则“B智”值为0
             */
        public class FireCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                int B = 0;
                Troop target = spellCell.troop;
                if (target != null)
                    B = target.Intelligence;

                int V = (troop.Intelligence - B / 2) * 10 / 100;
                return V;
            }
        }

        /*
        2)	灭火：
                灭火爆击率 = 0
                注：灭火不会有爆击

         */
        public class PutOutFireCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                return 0;
            }
        }

        /*
        3)	伪报：
                伪报爆击率 ＝ (A智+C-B智/2)*0.1
                C：根据用计部队主将的性格，胆小=10 冷静=5 刚胆=0 莽撞=-5

        */
        public class FalseReportCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if(target == null) return 0;

                int C = troop.Leader.mPersonality.falseReportCriticalAdd;
                int V = (troop.Intelligence  + C - target.Intelligence / 2) * 10 / 100;

                return V;
            }
        }

        /*
        4)	扰乱
            扰乱爆击率 ＝ (A智+C-B智/2)*0.1
            C：根据用计部队主将的性格，胆小=-5 冷静=0 刚胆=5 莽撞=10
       */
        public class DisturbCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = troop.Leader.mPersonality.disturbCriticalAdd;
                int V = (troop.Intelligence + C - target.Intelligence / 2) * 10 / 100;

                return V;
            }
        }

        /*
        5)	镇静
            镇静爆击率 ＝ A智*0.1
       */
        public class CalmdownCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;
                int V = troop.Intelligence * 10 / 100;
                return V;
            }
        }

        /*
        6)	伏兵
            伏兵爆击率 ＝ (A智+D-B智/2)*0.1
            D：根据用计部队的兵科，剑=5 枪=10 戟=10 弩=5
       */
        public class AmbushCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int D = troop.TroopType.ambushCriticalAdd;
                int V = (troop.Intelligence + D - target.Intelligence / 2) * 10 / 100;
                return V;
            }
        }

        /*
        7)	内讧
            内讧爆击率 ＝ (A智+E-B智/2)*0.1
            D：根据用计部队主将的义理，容易背叛=10，无情义=5，普通=0，情理坚定=-5，不会背叛=-10
       */
        public class InfightingCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int D = troop.Leader.mArgumentation.infightingCriticalAdd;
                int V = (troop.Intelligence + D - target.Intelligence / 2) * 10 / 100;
                return V;
            }
        }

        /*
        8)	妖术
            妖术爆击率 ＝ A智*1/3*0.1
       */
        public class SorceryCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int V = troop.Intelligence / 30;
                return V;
            }
        }

        /*
        9)	落雷
            落雷爆击率 ＝ A智*1/3*0.1
       */
        public class ThunderboltCriticalMethod : SkillCriticalMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int V = troop.Intelligence / 30;
                return V;
            }
        }
    }
}
