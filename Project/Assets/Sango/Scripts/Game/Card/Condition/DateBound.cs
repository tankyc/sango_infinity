using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game.Card
{
    /// <summary>
    /// 日期范围
    /// </summary>
    public class DateBound : Condition
    {
        public ushort begingYear;
        public ushort begingMonth;
        public ushort begingDay;
        public ushort endYear;
        public ushort endMonth;
        public ushort endDay;

        public override bool Check(Scenario scenario, Player player, params object[] data)
        {
            ScenarioInfo scenarioInfo = scenario.Info;
            return scenarioInfo.year >= begingYear && scenarioInfo.month >= begingMonth && scenarioInfo.day >= endDay && scenarioInfo.year <= endYear && scenarioInfo.month <= endMonth && scenarioInfo.day <= endDay;
        }
    }
}
