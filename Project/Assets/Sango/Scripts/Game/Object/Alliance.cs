using Newtonsoft.Json;
using System.Text;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Alliance : SangoObject
    {

        /// <summary>
        /// 部队列表
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Force>))]
        [JsonProperty]
        public SangoObjectList<Force> ForceList = new SangoObjectList<Force>();

        [JsonProperty] public int leftCount;
        [JsonProperty] public int allianceType;

        public bool Contains(Force force)
        {
            return ForceList.Contains(force);
        }

        public override bool OnNewTurn(Scenario scenario)
        {
            leftCount--;
            IsAlive = leftCount > 0;
            if(!IsAlive )
            {
#if SANGO_DEBUG
                StringBuilder stringBuilder = new StringBuilder();
#endif
                foreach (Force force in ForceList)
                {
#if SANGO_DEBUG
                    stringBuilder.Append(force.Name);
                    stringBuilder.Append(" ");
#endif
                    force.AllianceList.Remove(this);
                }

#if SANGO_DEBUG

                Sango.Log.Print($"@外交@{stringBuilder.ToString()} 同盟 {Id} 于{scenario.GetDateStr()} 与 结束!!");
#endif

            }
            return base.OnNewTurn(scenario);
        }
    }
}
