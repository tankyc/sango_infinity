using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]

    public class TroopAnimation : SangoObject
    {
        public class LinkObj : ScenarioObject<TroopAnimation>
        {
            protected override TroopAnimation Get(int id)
            {
                return Scenario.Cur.CommonData.TroopAnimations.Get(id);
            }
        }

        [JsonProperty] public string aniName;
        [JsonProperty] public string frameTexture;
        [JsonProperty] public string maskTexture;
        [JsonProperty] public byte celCount;
        [JsonProperty] public byte rowCount;
        [JsonProperty] public byte celCountMax;
        [JsonProperty] public float scale;
        [JsonProperty] public float time;
        [JsonProperty] public bool isLoop;


    }
}
