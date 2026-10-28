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

        /// <summary>
        /// 开局年份（对应 Open311 的 start_year）。
        ///
        /// 用途：寿终结算有一道"开局第一年不结算"的门槛，避免剧本里本该已死的武将
        /// 在第一个月就集体去世。老存档没有这个字段 → 读入为 0，
        /// 结算时按 <see cref="year"/> 兜底（等同"当年开局"，寿终从下一年才开始）。
        /// </summary>
        [JsonProperty]
        public int startYear;
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
        /// 剧本级寿命模式（对应 Open311 的 LifeModeEnum）：
        ///   0 史实 / 1 长寿 / 2 虚构 / 3 无死亡 / **-1 未指定**。
        ///
        /// 【为什么放在剧本信息里】寿命模式是剧本的**史实设定**，不是玩家的偏好：
        /// "英雄集结"这类剧本里武将本就不该死，必须锁死"无死亡"；
        /// 而史实剧本一旦被改成"无死亡"，整个势力兴衰的节奏就崩了。
        /// 所以它属于剧本作者的决定，写在剧本信息（ScenarioInfo）里随剧本分发。
        ///
        /// 【与剧本参数的关系】取值 ≥0 时**本字段优先**，
        /// <see cref="ScenarioVariables.lifeMode"/> 在该剧本内被锁定、界面上只显示不可改；
        /// 取 -1（默认）时才回落到剧本参数，保持老剧本与老存档的既有行为。
        /// 解析入口统一在 <see cref="PersonLifeCycle.GetLifeMode"/>。
        /// </summary>
        [JsonProperty]
        public int lifeMode = -1;

        /// <summary>
        /// 是否由剧本信息锁定寿命模式（lifeMode ≥ 0）。
        /// 不是存档字段（OptIn 序列化，未标 JsonProperty）。
        /// </summary>
        public bool HasLifeMode => lifeMode >= 0;

        /// <summary>
        /// 存档时记录的已启用模组 Id 列表(按加载顺序)。
        /// 读档时用它和"当前启用的模组"做对比:不一致时只展示基础信息,并在玩家确认后才尝试加载。
        /// 老存档没有这个字段 → 为 null,表示"不知道当时用了哪些模组"。
        /// </summary>
        [JsonProperty]
        public string[] mods;

        /// <summary>
        /// 剧本参数（ScenarioVariables）预设名，空串 = 不加载预设、直接用剧本自带的 Variables。
        ///
        /// 取值是文件名（不含 .json），对应：
        ///   · 模组：{模组}/Data/ScenarioVariables/{名字}.json
        ///   · 自建：{CustomEditRootPath}/Data/ScenarioVariables/{名字}.json
        /// 进剧本参数界面时由 <c>Scenario.LoadVariables</c> 先套用一次，
        /// 详见 <see cref="ScenarioVariablesPresetManager"/>。
        /// </summary>
        [JsonProperty]
        public string variablesPreset;

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
