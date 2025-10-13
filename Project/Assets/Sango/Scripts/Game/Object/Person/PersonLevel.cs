using Newtonsoft.Json;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PersonLevel : SangoObject
    {
        [JsonProperty] public int exp;
        [JsonProperty] public int troops;

        public PersonLevel Next => Scenario.Cur.GetObject<PersonLevel>(Id + 1);

        public PersonLevel Prev => Scenario.Cur.GetObject<PersonLevel>(Id - 1);
    }
}
