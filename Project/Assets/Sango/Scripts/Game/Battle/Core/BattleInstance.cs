using System;
using System.Collections;
using System.Collections.Generic;

namespace Sango.Game.Battle.Core
{
    public class BattleInstance
    {
        public BattleEvent @event;
        public BattleLog log;
        public List<BattlePerson> battlePersonList;
        public int round_count = 0;
        public BattleRandom randomGen;
        public BattleTroops[] troops;
        public BattleFormation[] formations;
        public int result
        {
            internal set;
            get;
        }

        public BattleInstance(BattleTroops a, BattleTroops b, int seed)
        {
            _Init(a, b, seed);
        }

        public BattleInstance(BattleTroops a, BattleTroops b)
        {
            _Init(a, b);
        }

        void _Init(BattleTroops a, BattleTroops b, int seed = 0)
        {
            troops = new BattleTroops[2] { a, b };
            if (seed == 0)
                randomGen = new BattleRandom();
            else
                randomGen = new BattleRandom(seed);

            formations = new BattleFormation[2] { new BattleFormation(this, a), new BattleFormation(this, b) };
            for (int i = 0; i < formations.Length; i++)
                formations[i].GetPersons(battlePersonList);
        }

        public int CheckResult()
        {
            for (int i = 0; i < formations.Length; i++)
            {
                BattlePerson[] battlePeople = formations[i].GetPersons();
                bool hasAlive = false;
                for (int j = 0; j < battlePeople.Length; j++)
                {
                    BattlePerson person = battlePeople[j];
                    if (person != null && person.IsAlive)
                    {
                        hasAlive = true;
                        break;
                    }
                }

                if (!hasAlive)
                {
                    result = i;

                    return i;
                }
            }
            return -1;
        }

        public IEnumerator Prepare()
        {
            log.Add("====进入准备阶段!====");
            battlePersonList.Sort(BattleUtility.SortHeroByOrder);
            for (int i = 0; i < battlePersonList.Count; i++)
            {
                yield return @event.OnPrepareStart?.Invoke(this, battlePersonList[i]);
            }

            for (int i = 0; i < battlePersonList.Count; i++)
            {
                yield return battlePersonList[i].PrepareAttribute();
            }

            //---技能的准备顺序和技能类型有关
            int maxType = (int)BattleDefine.SkillType.Command;
            for (int j = 0; j < maxType; ++j)
            {
                for (int i = 0; i < battlePersonList.Count; i++)
                {
                    yield return battlePersonList[i].PrepareSkill((BattleDefine.SkillType)j);
                }
            }

            for (int i = 0; i < battlePersonList.Count; i++)
            {
                yield return @event.OnPrepareEnd?.Invoke(this, battlePersonList[i]);
            }

            yield break;
        }

        public IEnumerator Round(int count)
        {
            round_count = count;
            log.Add($"====第{count}回合开始!====");
            battlePersonList.Sort(BattleUtility.SortHeroByOrder);
            for (int i = 0; i < battlePersonList.Count; i++)
            {
                BattlePerson person = battlePersonList[i];
                if (person.IsAlive)
                {
                    yield return @event.OnRoundStart?.Invoke(this, battlePersonList[i], count);
                    yield return person.Run(this, count);
                    yield return @event.OnRoundEnd?.Invoke(this, battlePersonList[i], count);
                }
            }
            result = CheckResult();
        }

        public IEnumerator ShowResult()
        {
            if (result < 0)
                log.Add($"====平手!====");
            else
                log.Add($"====失败者 {result}!====");
            yield break;
        }

        public IEnumerator Run()
        {
            log.Add("====战斗开始!====");
            result = -1;
            yield return Prepare();
            for (int i = 1; i <= BattleDefine.ACTION_MAX_COUNT; ++i)
            {
                round_count = i;
                yield return Round(i);
                if (result >= 0)
                    break;
            }
            yield return ShowResult();
        }

        public int Random()
        {
            return randomGen.Gen();
        }

        public int Random(int lhs, int rhs)
        {
            return randomGen.RandInt(lhs, rhs);
        }

        public bool RandomCheck(int prob)
        {
            return randomGen.Gen() < prob;
        }

        public BattlePerson FindPerson(Predicate<BattlePerson> comparer)
        {
            return battlePersonList.Find(comparer);
        }
        public List<BattlePerson> FindPersons(Predicate<BattlePerson> comparer)
        {
            return battlePersonList.FindAll(comparer);
        }

        public BattlePerson[] GetTargets(ushort targetType, BattlePerson who, byte num = 1)
        {
            BattleDefine.TargetType destTargetType = (BattleDefine.TargetType)targetType;
            switch (destTargetType)
            {
                case BattleDefine.TargetType.Self:
                    return new BattlePerson[1] { who };
                case BattleDefine.TargetType.RandomEnemy:
                    {
                        // 嘲讽
                        if (who.HasState(BattleDefine.PersonState.Taunt))
                            return new BattlePerson[1] { who.GetTauntTarget() };

                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                if (x.HasState(BattleDefine.PersonState.Chaos))
                                    return true;
                                else
                                    return x.IsEnemy(who);
                            }
                            return false;
                        });

                        targets = BattleUtility.WeightRandom(this, targets, num);
                        return targets != null ? targets.ToArray() : null;
                    }
                case BattleDefine.TargetType.RandomTeammate:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                if (x.HasState(BattleDefine.PersonState.Chaos))
                                    return true;
                                else
                                    return !x.IsEnemy(who);
                            }
                            return false;
                        });

                        targets = BattleUtility.ListRandom(this, targets, num);
                        return targets != null ? targets.ToArray() : null;
                    }
                case BattleDefine.TargetType.RandomAll:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                                return true;
                            return false;
                        });

                        targets = BattleUtility.ListRandom(this, targets, num);
                        return targets != null ? targets.ToArray() : null;
                    }
                case BattleDefine.TargetType.Teammate_MaxTroops:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                return !x.IsEnemy(who);
                            }
                            return false;
                        });

                        if (targets != null)
                        {
                            targets.Sort(BattleUtility.SortPersonByTroops);
                            return new BattlePerson[1] { targets[targets.Count - 1] };
                        }
                        return null;
                    }
                case BattleDefine.TargetType.Teammate_MinTroops:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                return !x.IsEnemy(who);
                            }
                            return false;
                        });

                        if (targets != null)
                        {
                            targets.Sort(BattleUtility.SortPersonByTroops);
                            return new BattlePerson[1] { targets[0] };
                        }
                        return null;
                    }
                case BattleDefine.TargetType.Enemy_MaxTroops:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                return x.IsEnemy(who);
                            }
                            return false;
                        });

                        if (targets != null)
                        {
                            targets.Sort(BattleUtility.SortPersonByTroops);


                            return new BattlePerson[1] { targets[targets.Count - 1] };
                        }
                        return null;
                    }
                case BattleDefine.TargetType.Enemy_MinTroops:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                return x.IsEnemy(who);
                            }
                            return false;
                        });

                        if (targets != null)
                        {
                            targets.Sort(BattleUtility.SortPersonByTroops);

                            return new BattlePerson[1] { targets[0] };
                        }
                        return null;
                    }
                case BattleDefine.TargetType.Teammate:
                    {
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                                return !x.IsEnemy(who);
                            return false;
                        });
                        return targets != null ? targets.ToArray() : null;
                    }
                case BattleDefine.TargetType.SeatEnemy:
                    {
                        // 嘲讽
                        if (who.HasState(BattleDefine.PersonState.Taunt))
                            return new BattlePerson[1] { who.GetTauntTarget() };

                        BattlePerson dst = FindPerson(x =>
                        {
                            if (x != who && x.IsAlive && x.IsEnemy(who) && x.seat.index == who.seat.index)
                                return true;
                            return false;
                        });

                        if (dst != null)
                            return new BattlePerson[] { dst };

                        // 优先级, 随机敌人
                        List<BattlePerson> targets = FindPersons(x =>
                        {
                            if (x != who && x.IsAlive)
                            {
                                if (x.HasState(BattleDefine.PersonState.Chaos))
                                    return true;
                                else
                                    return x.IsEnemy(who);
                            }
                            return false;
                        });

                        targets = BattleUtility.WeightRandom(this, targets, num);
                        return targets != null ? targets.ToArray() : null;
                    }
                default:
                    return null;
            }
        }
    }
}
