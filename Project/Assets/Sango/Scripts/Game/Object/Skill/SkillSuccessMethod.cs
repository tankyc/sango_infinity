using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 技能成功率逻辑库
    /// </summary>
    public abstract class SkillSuccessMethod
    {
        public SkillInstance master;
        public virtual void Init(JObject p, SkillInstance master) { this.master = master; }
        public abstract int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell);
        public virtual void Clear() { }

        public delegate SkillSuccessMethod SkillSuccessMethodCreator();

        public static Dictionary<string, SkillSuccessMethodCreator> CreateMap = new Dictionary<string, SkillSuccessMethodCreator>();
        public static void Register(string name, SkillSuccessMethodCreator action)
        {
            CreateMap[name] = action;
        }
        public static SkillSuccessMethod CraeteHandle<T>() where T : SkillSuccessMethod, new()
        {
            return new T();
        }
        public static SkillSuccessMethod Create(string name)
        {
            if(string.IsNullOrEmpty(name)) return null;
            SkillSuccessMethodCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {
            Register("CommonMethod", CraeteHandle<CommonMethod>);
            Register("FireSuccessMethod", CraeteHandle<FireSuccessMethod>);
            Register("PutOutFireSuccessMethod", CraeteHandle<PutOutFireSuccessMethod>);
            Register("FalseReportSuccessMethod", CraeteHandle<FalseReportSuccessMethod>);
            Register("DisturbSuccessMethod", CraeteHandle<DisturbSuccessMethod>);
            Register("CalmdownSuccessMethod", CraeteHandle<CalmdownSuccessMethod>);
            Register("AmbushSuccessMethod", CraeteHandle<AmbushSuccessMethod>);
            Register("SorcerySuccessMethod", CraeteHandle<SorcerySuccessMethod>);
            Register("ThunderboltSuccessMethod", CraeteHandle<ThunderboltSuccessMethod>);
        }

        /// <summary>
        /// 常规战法成功率
        /// </summary>
        public class CommonMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                int V = skillInstance.successRate + Mathf.Max(0, troop.TroopTypeLv - 1) * Scenario.Cur.Variables.skillSuccessRateAddByAbility;

                // 地形影响
                if (troop.TroopType.terrainIncreaseSucecessBonus != null && troop.cell.TerrainType != null)
                {
                    int terrainId = troop.cell.TerrainType.Id;
                    if (terrainId < troop.TroopType.terrainIncreaseSucecessBonus.Length)
                    {
                        V += troop.TroopType.terrainIncreaseSucecessBonus[terrainId];
                    }
                }

                if(spellCell.troop != null && spellCell.troop.TroopType.terrainDecreaseSucecessBonus != null && spellCell.TerrainType != null)
                {
                    int terrainId = spellCell.TerrainType.Id;
                    if (terrainId < spellCell.troop.TroopType.terrainIncreaseSucecessBonus.Length)
                    {
                        V -= spellCell.troop.TroopType.terrainIncreaseSucecessBonus[terrainId];
                    }
                }

                return V;
            }
        }


        /*
             1)	火计：
                    V1 = (A智*0.3-B智*0.2)+55+C+G
                    V2 = (A智*A智*(100-B智*0.9)*100/(A智*A智+B智*B智))*1/55-(100-A智)*0.1+F-D+C-5
                    火计成功率 = MIN(V1,V2)
                    注：对火计而言，如果被放计对象不是部队，则“B智”值为0
                    C：如果被用计部队是异常状态则为10，否则为0
                    D：如果被用计方智力高于用计方则为 = (B智-A智)/3，否则为0
                    F：不同地形的修正值，草地=20 土=20 沙地=20 湿地=5 毒泉=5 森林=25 荒地=17 主径=15 栈道=7 渡所=10 浅滩=5 小径=5 其它地形(川、河、海、岸、崖、港、关、城市)=0
                    G：如果放火对象不是部队则为10，否则为0

             
             */
        public class FireSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                int G = 10;
                int B = 0;
                int C = 0;
                int D = 0;
                Troop target = spellCell.troop;
                if (target != null)
                {
                    G = 0;
                    B = target.Intelligence;
                    if (target.HasControlBuff())
                        C = 10;
                    D = B > troop.Intelligence ? (B - troop.Intelligence) / 3 : 0;
                }
                int F = spellCell.TerrainType.fireRate;
                int V1 = (troop.Intelligence * 30 - B * 20) / 100 + 55 + C + G;
                int V2 = (troop.Intelligence * troop.Intelligence * (100 - B * 90 / 100) * 100 /
                    (troop.Intelligence * troop.Intelligence + B * B)) / 55 - (100 - troop.Intelligence) * 10 / 100 + F - D + C - 5;

                return Mathf.Min(V1, V2);
            }
        }

        /*
        2)	灭火：
                灭火成功率 = sqrt(A智-5)*11
                注：sqrt意为开根号,如果(A智-5)的结果小于10，则取10
         */
        public class PutOutFireSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                int V = (int)(Mathf.Sqrt(Mathf.Max(10, troop.Intelligence - 5)) * 11);
                return V;
            }
        }

        /*
        3)	伪报：
                V1 = (A智*0.3-B智*0.2)+A魅*0.05 +E+70+C
                V2 = (A智*A智*(100-B智*0.9)*100/(A智*A智+B智*B智))*1/55-(100-A智)*0.1+A魅*0.05+E-D+C
                伪报成功率 = MIN(V1,V2)
                C：如果被用计部队是异常状态则为10，否则为0
                D：如果被用计方智力高于用计方则为 = (B智-A智)/6，否则为0
                E：根据被用计部队的主将性格，胆小=3，冷静=1，刚胆=0，莽撞=-2
        */
        public class FalseReportSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
                int D = target.Intelligence > troop.Intelligence ? (target.Intelligence - troop.Intelligence) / 6 : 0;
                int E = target.Leader.mPersonality.falseReportSuccessAdd;

                int V1 = (troop.Intelligence * 30 - target.Intelligence * 20) / 100 + troop.Glamour * 5 / 100 + E + 70 + C;
                int V2 = (troop.Intelligence * troop.Intelligence * (100 - target.Intelligence * 90 / 100) * 100 /
                    (troop.Intelligence * troop.Intelligence + target.Intelligence * target.Intelligence)) / 55 - (100 - troop.Intelligence) * 10 / 100 + troop.Glamour * 5 / 100 + E - D + C;

                return Mathf.Min(V1, V2);
            }
        }

        /*
        4)	扰乱
            V1 = (A智*0.3-B智*0.2)+B防*0.05+E+70+C
            V2 = [A智*A智*(100-B智*0.9)*100/(A智*A智+B智*B智)]*1/45-(100-A智)*0.1+B防*0.05+E-D+C
            扰乱成功率 = MIN(V1,V2)
            C：如果被用计部队是异常状态则为10，否则为0
            D：如果被用计方智力高于用计方则为 (B智-A智)/6，否则为0
            E：根据被用计部队的主将性格，胆小=-2，冷静=0，刚胆=1，莽撞=3

       */
        public class DisturbSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
                int D = target.Intelligence > troop.Intelligence ? (target.Intelligence - troop.Intelligence) / 6 : 0;
                int E = target.Leader.mPersonality.disturbSuccessAdd;

                int V1 = (troop.Intelligence * 30 - target.Intelligence * 20) / 100 + target.Defence * 5 / 100 + E + 70 + C;
                int V2 = (troop.Intelligence * troop.Intelligence * (100 - target.Intelligence * 90 / 100) * 100 /
                    (troop.Intelligence * troop.Intelligence + target.Intelligence * target.Intelligence)) / 55 - (100 - troop.Intelligence) * 10 / 100 + target.Defence * 5 / 100 + E - D + C;

                return Mathf.Min(V1, V2);
            }
        }

        /*
        5)	镇静
            镇静成功率 = sqrt(A智-5)*11+E
            注：sqrt意为开根号,如果(A智-5)的结果小于10，则取10
            E：根据被用计部队的主将性格，胆小=-5，冷静=5，刚胆=0，莽撞=-10
       */
        public class CalmdownSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int E = target.Leader.mPersonality.calmdownSuccessAdd;
                int V = (int)(Mathf.Sqrt(Mathf.Max(10, troop.Intelligence - 5)) * 11) + E;
                return V;
            }
        }

        /*
        6)	伏兵
            V1 = (A智*0.3-B智*1/3)+(A攻-B防)*0.1+E+80+C
            V2 = [A智*A智*(100-B智*0.9)*100/(A智*A智+B智*B智)]*1/50-(100-A智)*0.1+(A攻-B防)*0.1+E-D+C
            伏兵成功率 = MIN(V1,V2)
            C：如果被用计部队是异常状态则为10，否则为0
            D：如果被用计方智力高于用计方则为 (B智-A智)/6，否则为0
            E：根据被用计部队的主将性格，胆小=3，冷静=-2，刚胆=0，莽撞=1
       */
        public class AmbushSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
                int D = target.Intelligence > troop.Intelligence ? (target.Intelligence - troop.Intelligence) / 6 : 0;
                int E = target.Leader.mPersonality.ambushSuccessAdd;

                int V1 = (troop.Intelligence * 30 - target.Intelligence * 33) / 100 + (troop.Attack - target.Defence) * 10 / 100 + E + 80 + C;
                int V2 = (troop.Intelligence * troop.Intelligence * (100 - target.Intelligence * 90 / 100) * 100 /
                    (troop.Intelligence * troop.Intelligence + target.Intelligence * target.Intelligence)) / 50 - (100 - troop.Intelligence) * 10 / 100 + (troop.Attack - target.Defence) * 10 / 100 + E - D + C;

                return Mathf.Min(V1, V2);
            }
        }

        /*
        7)	内讧
            V1 = (A智*0.3-B智*0.4)+E+80+C
            V2 = [A智*A智*(100-B智*0.9)*100/(A智*A智+B智*B智)]*1/55-(100-A智)*0.1+E-D+C
            内讧成功率 = MIN(V1,V2)
            C：如果被用计部队是异常状态则为10，否则为0
            D：如果被用计方智力高于用计方则为 (B智-A智)/6，否则为0
            E：根据被用计部队的主将性格，胆小=-2，冷静=0，刚胆=1，莽撞=3
        */
        public class InfightingSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
                int D = target.Intelligence > troop.Intelligence ? (target.Intelligence - troop.Intelligence) / 6 : 0;
                int E = target.Leader.mPersonality.infightingSuccessAdd;

                int V1 = (troop.Intelligence * 30 - target.Intelligence * 40) / 100 + E + 80 + C;
                int V2 = (troop.Intelligence * troop.Intelligence * (100 - target.Intelligence * 90 / 100) * 100 /
                    (troop.Intelligence * troop.Intelligence + target.Intelligence * target.Intelligence)) / 55 - (100 - troop.Intelligence) * 10 / 100 + E - D + C;

                return Mathf.Min(V1, V2);
            }
        }

        /*
        8)	妖术
            V1 = (100-B智*0.9)+A智*0.1+E+C
            V2 = (100-B智*0.9)+(100-A智)*0.1-5+E+C
            妖术成功率 = (A智>=B智)? V1：V2（即在A智不低于B智的情况下用V1，否则用V2）
            C：如果被用计部队是异常状态则为10，否则为0
            E：根据被用计部队的主将性格，胆小=-2，冷静=0，刚胆=1，莽撞=3

       */
        public class SorcerySuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
                int E = target.Leader.mPersonality.sorcerySuccessAdd;

                if (troop.Intelligence >= target.Intelligence)
                {
                    return (100 - target.Intelligence * 90) / 100 + troop.Intelligence * 10 / 100 + E + C;
                }
                else
                {
                    return (100 - target.Intelligence * 90) / 100 + (100 - troop.Intelligence) * 10 / 100 - 5 + E + C;
                }
            }
        }

        /*
        9)	落雷
            落雷成功率 = A智*0.6+15+C
            C：如果被用计部队是异常状态则为10，否则为0
       */
        public class ThunderboltSuccessMethod : SkillSuccessMethod
        {
            public override int Calculate(SkillInstance skillInstance, Troop troop, Cell spellCell)
            {
                Troop target = spellCell.troop;
                if (target == null) return 0;

                int C = 0;
                if (target.HasControlBuff())
                    C = 10;
              
                return troop.Intelligence * 60 / 100 + 15 + C;
            }
        }
    }
}
