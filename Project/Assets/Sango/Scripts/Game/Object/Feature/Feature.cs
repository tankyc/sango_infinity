using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using Sango.Core.Action;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 特性
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Feature : SangoObject
    {
        private string _name;
        public override string Name
        {
            get
            {
                if (actionEntities == null)
                    return $"<color=#ff1111>{_name}</color>";
                else
                    return _name;
            }

            set
            {
                _name = value;
            }
        }

        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty] public int kind;

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty] public int level;

        /// <summary>
        /// 效果实体集合
        /// </summary>
        [JsonProperty]
        public TKNewtonsoft.Json.Linq.JArray actionEntities;

        public void InitActions(List<ActionBase> list, params SangoObject[] sangoObjects)
        {
            if (actionEntities == null) return;
            for (int i = 0; i < actionEntities.Count; i++)
            {
                JObject valus = actionEntities[i] as JObject;
                ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                if (action != null)
                {
                    action.Init(valus, sangoObjects);
                    list.Add(action);
                }
            }
        }

    }
}
