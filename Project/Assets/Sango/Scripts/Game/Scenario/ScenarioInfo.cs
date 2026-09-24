using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioInfo
    {
        [JsonProperty]
        public int id;
        [JsonProperty]
        public string name;
        [JsonProperty]
        public int type;
        [JsonProperty]
        public string tag;
        [JsonProperty]
        public string description;
        [JsonProperty]
        public int year;
        [JsonProperty]
        public int month;
        [JsonProperty]
        public int day;
        [JsonProperty]
        public int curForceId;
        [JsonProperty]
        public string curForceName;
        [JsonProperty]
        public string mapType;
        [JsonProperty]
        public int turnCount;
        [JsonProperty]
        public int priority;
        [JsonProperty]
        public bool isSave;
        [JsonProperty]
        public int[] playerForceList;
        [JsonProperty]
        public long dateTime;
    }
}
