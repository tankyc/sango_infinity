using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class SkillInstance : SangoObject
    {
        /// <summary>
        /// 技能
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Skill>))]
        [JsonProperty]
        public Skill Skill { get; set; }

        /// <summary>
        /// 当前剩余冷却
        /// </summary>
        [JsonProperty]
        public int CDCount { get; set; }

    }
}
