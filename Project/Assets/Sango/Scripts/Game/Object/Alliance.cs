using Newtonsoft.Json;

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
            IsAlive = leftCount <= 0;
            if(!IsAlive )
            {
                foreach (Force force in ForceList)
                    force.AllianceList.Remove(this);
                scenario.Remove(this);
            }
            return base.OnNewTurn(scenario);
        }
    }
}
