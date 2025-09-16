using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class CorpsAI
    {
        public static bool AICities(Corps corps, Scenario scenario)
        {
            for (int i = 0; i < corps.allCities.Count; i++)
            {
                City city = corps.allCities[i];
                if (city != null && city.IsAlive && !city.ActionOver)
                {
                    if (!city.DoAI(scenario))
                        return false;
                }
            }
            return true;
        }

        public static bool AITransfromPerson(Corps corps, Scenario scenario)
        {
            List<Person> canTransforPersons = new List<Person>();
            for (int k = 0, kCount = corps.allCities.Count; k < kCount; ++k)
            {
                City kCity = corps.allCities[k];
                if (kCity.PersonHole < 0 && kCity.freePersons.Count > 0)
                {
                    int count = Math.Abs(kCity.PersonHole);
                    kCity.freePersons.Sort((a, b) =>
                    {
                        return -a.MilitaryAbility.CompareTo(b.MilitaryAbility);
                    });

                    int maxCount = kCity.freePersons.Count;
                    for (int i = 0; i < count; i++)
                    {
                        if (i < maxCount)
                            canTransforPersons.Add(kCity.freePersons[maxCount - 1 - i]);
                    }
                }
            }

            if (canTransforPersons.Count <= 0)
                return true;

            canTransforPersons.Sort((a, b) =>
            {
                return a.MilitaryAbility.CompareTo(b.MilitaryAbility);
            });

            for (int k = 0, kCount = corps.allCities.Count; k < kCount; ++k)
            {
                if (canTransforPersons.Count <= 0)
                    break;

                City kCity = corps.allCities[k];
                if (kCity.PersonHole > 0 && kCity.IsBorderCity)
                {
                    for (int i = 0; i < kCity.PersonHole; i++)
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

            for (int k = 0, kCount = corps.allCities.Count; k < kCount; ++k)
            {
                if (canTransforPersons.Count <= 0)
                    break;

                City kCity = corps.allCities[k];
                if (kCity.PersonHole > 0 && !kCity.IsBorderCity)
                {
                    for (int i = 0; i < kCity.PersonHole; i++)
                    {
                        if (canTransforPersons.Count > 0)
                        {
                            canTransforPersons[0].TransformToCity(kCity);
                            canTransforPersons.RemoveAt(0);
                        }
                    }
                }

            }
            return true;
        }
        public static bool AITroops(Corps corps, Scenario scenario)
        {
            for (int i = 0; i < corps.allTroops.Count; i++)
            {
                Troop troop = corps.allTroops[i];
                if (troop != null && troop.IsAlive && !troop.ActionOver)
                {
                    if (!troop.DoAI(scenario))
                        return false;
                }
            }
            return true;
        }

    }
}
