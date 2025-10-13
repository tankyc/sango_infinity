using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.VisualScripting;

namespace Sango.Game
{
    public class ForceAI
    {
        /// <summary>
        /// AI外交
        /// </summary>
        public static bool AIDiplomacy(Force force, Scenario scenario)
        {
            if (force.Governor == null) return true;
            if (force.Governor.BelongCity == null) return true;

            City centerCity = force.Governor.BelongCity;
            if (centerCity.freePersons.Count == 0)
                return true;

            if (centerCity.gold < 3000)
                return true;

            // 找到
            foreach (Force neighbor in force.NeighborForceList)
            {
                if (neighbor.IsAlliance(force)) continue;

                int neighborRelation = scenario.GetRelation(neighbor, force);
                if (neighborRelation > 0) continue;
                // 敌人的敌人 就是朋友
                foreach (Force enemysenemy in neighbor.NeighborForceList)
                {
                    if (enemysenemy != force && !enemysenemy.IsAlliance(neighbor) && !force.NeighborForceList.Contains(enemysenemy) && !enemysenemy.IsAlliance(force))
                    {
                        int enemysenemy_relation = scenario.GetRelation(enemysenemy, force);
                        if (enemysenemy_relation > 2000)
                        {
                            if (centerCity.gold > 3000)
                            {
                                // 派遣结盟
                                if (GameRandom.Changce(enemysenemy_relation, 10000))
                                {
                                    centerCity.gold -= 1000;
                                    Alliance alliance = new Alliance()
                                    {
                                        ForceList = new SangoObjectList<Force>(),
                                        leftCount = 36,
                                        allianceType = 1,
                                        IsAlive = true,
                                    };
                                    alliance.ForceList.Add(force);
                                    alliance.ForceList.Add(enemysenemy);

                                    scenario.Add(alliance);

                                    force.AllianceList.Add(alliance);
                                    enemysenemy.AllianceList.Add(alliance);
#if SANGO_DEBUG
                                    Sango.Log.Print($"@外交@{force.Name} 于{scenario.GetDateStr()} 与 {enemysenemy.Name} 达成了12个月的结盟 Id={alliance.Id}!!");
#endif
                                }
                            }
                        }
                        else
                        {
                            if (centerCity.gold > 3000)
                            { // 结交
                                scenario.AddRelation(enemysenemy, force, 1000);
                                centerCity.gold -= 1000;
#if SANGO_DEBUG
                                Sango.Log.Print($"@外交@{force.Name} 与 {enemysenemy.Name} 亲密接触,关系到达了{scenario.GetRelation(enemysenemy, force)}!!");
#endif
                                return true;
                            }
                        }
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// AI-俘虏
        /// </summary>
        public static bool AICaptives(Force force, Scenario scenario)
        {
            return true;
            // 释放或者招降俘虏
            // 赎回俘虏
        }
        /// <summary>
        /// AI-科技研发
        /// </summary>

        public static bool AITechniques(Force force, Scenario scenario)
        {
            return true;
            //if ((this.ArchitectureCount != 0) && (this.UpgradingTechnique < 0))
            //{
            //    if (this.PlanTechnique == null)
            //    {
            //        if (this.PreferredTechniqueKinds.Count > 0)
            //        {
            //            Dictionary<Technique, float> list = new Dictionary<Technique, float>();
            //            float preferredTechniqueComplition = this.GetPreferredTechniqueComplition();
            //            foreach (Technique technique in Session.Current.Scenario.GameCommonData.AllTechniques.Techniques.Values)
            //            {
            //                if (!this.IsTechniqueUpgradable(technique))
            //                {
            //                    continue;
            //                }
            //                if (this.GetTechniqueUsefulness(technique) <= 0) continue;

            //                float weight = 1;
            //                foreach (KeyValuePair<Condition, float> c in technique.AIConditionWeight)
            //                {
            //                    if (c.Key.CheckCondition(this))
            //                    {
            //                        weight *= c.Value;
            //                    }
            //                }

            //                if (preferredTechniqueComplition < 0.5f)
            //                {
            //                    if (this.PreferredTechniqueKinds.IndexOf(technique.Kind) >= 0)
            //                    {
            //                        list.Add(technique, weight);
            //                    }
            //                }
            //                else if (preferredTechniqueComplition < 0.75f)
            //                {
            //                    if ((this.PreferredTechniqueKinds.IndexOf(technique.Kind) >= 0) || GameObject.Chance(0x19))
            //                    {
            //                        list.Add(technique, weight);
            //                    }
            //                }
            //                else if (preferredTechniqueComplition < 1f)
            //                {
            //                    if ((this.PreferredTechniqueKinds.IndexOf(technique.Kind) >= 0) || GameObject.Chance(50))
            //                    {
            //                        list.Add(technique, weight);
            //                    }
            //                }
            //                else if ((this.PreferredTechniqueKinds.IndexOf(technique.Kind) >= 0) || GameObject.Chance(0x4b))
            //                {
            //                    list.Add(technique, weight);
            //                }
            //            }
            //            if (list.Count > 0)
            //            {
            //                this.PlanTechnique = GameObject.WeightedRandom(list);
            //            }
            //            else
            //            {
            //                this.PlanTechnique = this.GetRandomTechnique();
            //            }
            //        }
            //        else
            //        {
            //            this.PlanTechnique = this.GetRandomTechnique();
            //        }
            //    }
            //    if (this.PlanTechnique != null)
            //    {
            //        if (((this.TechniquePoint + this.TechniquePointForTechnique) >= this.getTechniqueActualPointCost(this.PlanTechnique)) && (this.Reputation >= this.getTechniqueActualReputation(this.PlanTechnique)))
            //        {
            //            if (this.ArchitectureCount > 1)
            //            {
            //                this.Architectures.PropertyName = "Fund";
            //                this.Architectures.IsNumber = true;
            //                this.Architectures.ReSort();
            //            }
            //            Architecture a = this.Architectures[0] as Architecture;
            //            if (a.IsFundEnough)
            //            {
            //                this.PlanTechniqueArchitecture = this.Architectures[0] as Architecture;
            //                if (this.PlanTechniqueArchitecture.Fund >= this.getTechniqueActualFundCost(this.PlanTechnique))
            //                {
            //                    this.DepositTechniquePointForTechnique(this.TechniquePointForTechnique);
            //                    this.UpgradeTechnique(this.PlanTechnique, this.PlanTechniqueArchitecture);
            //                    this.PlanTechniqueArchitecture = null;
            //                    this.PlanTechnique = null;
            //                }
            //            }
            //            else
            //            {
            //                this.PlanTechniqueArchitecture = null;
            //                this.PlanTechnique = null;
            //            }
            //        }
            //        else if ((this.Reputation >= this.getTechniqueActualReputation(this.PlanTechnique)) && GameObject.Chance(0x21))
            //        {
            //            this.SaveTechniquePointForTechnique(this.getTechniqueActualPointCost(this.PlanTechnique) / this.PlanTechnique.Days);
            //        }
            //        else if (GameObject.Chance(10))
            //        {
            //            this.PlanTechniqueArchitecture = null;
            //            this.PlanTechnique = null;
            //        }
            //    }
            //}
        }

        /// <summary>
        /// 军师建造推荐
        /// </summary>
        public List<Person> CounsellorRecommendBuild(List<Person> sortedFreePersons, BuildingType buildingType)
        {
            sortedFreePersons.RemoveAll(x => x.ActionOver);
            int count = sortedFreePersons.Count;
            if (count <= 0)
                return null;

            int maxPersonCount = Scenario.Cur.Variables.jobMaxPersonCount[(int)CityJobType.Build];
            List<Person> list = new List<Person>();
            int buildAbility = 0;
            int buildCount = 999;
            for (int k = 0; k < maxPersonCount; k++)
            {
                for (int i = 0; i < count; i++)
                {
                    Person person = sortedFreePersons[0];
                    if (list.Contains(person))
                        continue;

                    int addValue = buildAbility + person.BaseBuildAbility;
                    int new_buildCount = buildingType.durabilityLimit / addValue;
                    if (buildingType.durabilityLimit % addValue > 0) new_buildCount++;

                    if (new_buildCount < buildCount)
                    {
                        list.Add(person);
                        buildAbility = addValue;
                        break;
                    }
                }
            }
            return list;
        }

    }
}
