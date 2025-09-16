using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class ForceAI
    {
        
        
        /// <summary>
        /// AI外交
        /// </summary>
        public static bool AIDiplomacy(Force force, Scenario scenario)
        {
            return true;
            //if (this.Leader.Status == PersonStatus.Captive) return;

            //foreach (Faction f in Session.Current.Scenario.PlayerFactions)
            //{
            //    if (!this.adjacentTo(f)) continue;
            //    if (this.IsFriendly(f)) continue;
            //    if (this == f) continue;
            //    if (GameObject.Random(1000) < Session.Parameters.AIEncirclePlayerRate && GameObject.Chance(f.ArchitectureCount))
            //    {
            //        if (GetEncircleFactionList(f, true) == null) continue;
            //        foreach (Architecture a in this.Architectures)
            //        {
            //            if (a.Fund > 120000 + a.AbundantFund)
            //            {
            //                Encircle(a, f);
            //                return;
            //            }
            //        }
            //    }
            //}

            //if (GameObject.Random(180 * Math.Max(1, 5 - this.Leader.Ambition)) == 0 && GameObject.Chance(100 - Session.Parameters.AIEncirclePlayerRate))
            //{
            //    GameObjectList factions = this.GetAdjecentHostileFactions();
            //    if (factions.Count == 0) return;

            //    factions.PropertyName = "Power";
            //    factions.IsNumber = true;
            //    factions.SmallToBig = false;
            //    factions.ReSort();

            //    int rank = Session.Parameters.AIEncircleRank + GameObject.Random(Session.Parameters.AIEncircleVar * 2) - Session.Parameters.AIEncircleVar;
            //    rank = Math.Min(rank, 100);
            //    rank = Math.Max(rank, 0);
            //    Faction target = (Faction)factions[(factions.Count - 1) * rank / 100];
            //    int rel = Session.Current.Scenario.GetDiplomaticRelation(this.ID, target.ID);
            //    if (target != this && rel < 0 && GetEncircleFactionList(target, true) != null)
            //    {
            //        if (GameObject.Chance(Math.Abs(rel) / 10))
            //        {
            //            foreach (Architecture a in this.Architectures)
            //            {
            //                if (a.Fund > 120000 + a.AbundantFund)
            //                {
            //                    Encircle(a, target);
            //                    return;
            //                }
            //            }
            //        }
            //    }
            //}
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

    }
}
