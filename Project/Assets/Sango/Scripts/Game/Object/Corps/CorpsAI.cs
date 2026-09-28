using System;
using System.Collections.Generic;

namespace Sango.Core
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
