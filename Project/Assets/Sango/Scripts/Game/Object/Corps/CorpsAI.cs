using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class CorpsAI
    {
        public static bool AICities(Corps corps, Scenario scenario)
        {
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps && !c.ActionOver)
                {
                    if (!c.DoAI(scenario))
                        return false;
                }
            }
            return true;
        }

        public static bool AITransfromPerson(Corps corps, Scenario scenario)
        {
            List<Person> canTransforPersons = new List<Person>();

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps)
                {
                    City kCity = c;
                    if (kCity.PersonHole < 0 && kCity.freePersons.Count > 0)
                    {
                        int count = Math.Abs(kCity.PersonHole);
                        kCity.freePersons.Sort((a, b) =>
                        {
                            return -a.MilitaryAbility.CompareTo(b.MilitaryAbility);
                        });

                        int maxCount = kCity.freePersons.Count;
                        for (int k = 0; k < count; k++)
                        {
                            if (k < maxCount)
                                canTransforPersons.Add(kCity.freePersons[maxCount - 1 - k]);
                        }
                    }
                }
            }

            if (canTransforPersons.Count <= 0)
                return true;

            canTransforPersons.Sort((a, b) =>
            {
                return a.MilitaryAbility.CompareTo(b.MilitaryAbility);
            });

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps)
                {
                    City kCity = c;
                    if (canTransforPersons.Count <= 0)
                        break;

                    if (kCity.PersonHole > 0 && kCity.IsBorderCity)
                    {
                        for (int k = 0; k < kCity.PersonHole; k++)
                        {
                            if (canTransforPersons.Count > 0)
                            {
                                canTransforPersons[0].TransformToCity(kCity);
                                canTransforPersons.RemoveAt(0);
                            }
                        }
                    }

                    if (canTransforPersons.Count <= 0)
                        break;
                }
            }

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps)
                {
                    City kCity = c;
                    if (canTransforPersons.Count <= 0)
                        break;
                    if (kCity.PersonHole > 0 && !kCity.IsBorderCity)
                    {
                        for (int k = 0; k < kCity.PersonHole; k++)
                        {
                            if (canTransforPersons.Count > 0)
                            {
                                canTransforPersons[0].TransformToCity(kCity);
                                canTransforPersons.RemoveAt(0);
                            }
                        }
                    }
                }
            }
            return true;
        }
        public static bool AITroops(Corps corps, Scenario scenario)
        {
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongCorps == corps && !c.ActionOver)
                {
                    if (!c.DoAI(scenario))
                        return false;
                }
            }
            return true;
        }

    }
}
