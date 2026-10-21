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
        /// 存档时记录的已启用模组 Id 列表(按加载顺序)。
        /// 读档时用它和"当前启用的模组"做对比:不一致时只展示基础信息,并在玩家确认后才尝试加载。
        /// 老存档没有这个字段 → 为 null,表示"不知道当时用了哪些模组"。
        /// </summary>
        [JsonProperty]
        public string[] mods;

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
            if (mods != null)
                copy.mods = (string[])mods.Clone();
            return copy;
        }
    }
}
