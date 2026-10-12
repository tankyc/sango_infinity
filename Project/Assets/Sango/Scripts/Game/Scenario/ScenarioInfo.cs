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

        /// <summary>
        /// 生成一份浅拷贝，供"存档列表快照"使用。
        ///
        /// 存档列表里的 ShortScenario 需要一份**当时**的 Info（日期 / 回合数等）：
        /// 若直接引用正在进行的剧本 Info，玩家存档后继续游戏，列表上显示的日期会跟着变，等于显示错存档。
        /// 字段全是值类型 / string，MemberwiseClone 即可；playerForceList 是数组，单独克隆避免共享。
        /// </summary>
        public ScenarioInfo Clone()
        {
            ScenarioInfo copy = (ScenarioInfo)MemberwiseClone();
            if (playerForceList != null)
                copy.playerForceList = (int[])playerForceList.Clone();
            return copy;
        }
    }
}
