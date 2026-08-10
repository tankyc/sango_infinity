using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 剧本附加数据
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ScenarioAddon
    {
        /// <summary>
        /// 自建武将
        /// </summary>
        [JsonConverter(typeof(SangoObjectMapConverter<PersonLib>))]
        [JsonProperty] public SangoObjectMap<PersonLib> PersonAddonMap = new SangoObjectMap<PersonLib>();

        /// <summary>
        /// 容貌区间性别分类
        /// </summary>
        public enum HeadSexType
        {
            /// <summary>男性容貌</summary>
            Male = 0,
            /// <summary>女性容貌</summary>
            Female = 1,
            /// <summary>自定义容貌（新武将）</summary>
            Custom = 2
        }

        /// <summary>
        /// 容貌ID区间配置。
        /// 每个区间定义一段连续的容貌ID范围及其性别属性，
        /// 支持配置多段区间以覆盖不连续的自定义头像ID段。
        /// </summary>
        public class HeadIdRange
        {
            /// <summary>区间名称，如"标准男性"、"自定义女性"</summary>
            [Tooltip("区间名称，用于在编辑器中标识")]
            public string name;

            /// <summary>起始ID（包含）</summary>
            [Tooltip("区间起始ID，包含此ID")]
            public int startId;

            /// <summary>结束ID（包含）</summary>
            [Tooltip("区间结束ID，包含此ID")]
            public int endId;

            /// <summary>性别分类</summary>
            [Tooltip("该区间容貌的性别分类")]
            public HeadSexType sexType;
        }

        [JsonProperty]
        public List<HeadIdRange> HeadIdRanges = new List<HeadIdRange>();

        public void Load(string file)
        {
            if (File.Exists(file))
            {
                TKNewtonsoft.Json.JsonConvert.PopulateObject(File.ReadAllText(file), this);
            }
        }

        /// <summary>
        /// 所有容貌数据链表。
        /// 按区间配置顺序生成，先男后女排列。
        /// </summary>
        public List<int> headDataList = new List<int>();

        public int femaleStartIndex = -1;
        /// <summary>
        /// 当前筛选后的容貌数据链表。
        /// 根据性别Toggle筛选后的子集。
        /// </summary>
        public List<int> filteredHeadDataList = new List<int>();


        public void Init()
        {
            headDataList.Clear();
            femaleStartIndex = -1;
            foreach (var range in HeadIdRanges)
            {
                if (range == null) continue;
                if (range.sexType == HeadSexType.Male)
                {
                    for (int id = range.startId; id <= range.endId; id++)
                    {
                        headDataList.Add(id);
                    }
                }
            }
            femaleStartIndex = headDataList.Count;
            foreach (var range in HeadIdRanges)
            {
                if (range == null) continue;
                if (range.sexType == HeadSexType.Female)
                {
                    for (int id = range.startId; id <= range.endId; id++)
                    {
                        headDataList.Add(id);
                    }
                }
            }
        }
    }
}
