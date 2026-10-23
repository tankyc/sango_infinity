using Newtonsoft.Json;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 联盟类型
    /// </summary>
    public enum AllianceType
    {
        /// <summary>
        /// 同盟
        /// </summary>
        Alliance = 1,
        /// <summary>
        /// 停战协议
        /// </summary>
        Truce = 2,

        // 通商协议（Trade = 3）已删除：原版没有通商命令，盟约只有"同盟"与"停战"两种。
        // 数值 3 刻意保留空缺，避免老存档里已有的 allianceType 被重新解释。
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class Alliance : SangoObject
    {

        /// <summary>
        /// 部队列表
        /// </summary>
        // 序列化形态保持 int[]（键名不变，老存档可读）；运行期列表由 OnScenarioPrepare 解析。
        [JsonProperty("ForceList")]
        public int[] ForceList_list;
        public SangoObjectList<Force> ForceList = new SangoObjectList<Force>();

        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            if (ForceList_list != null && ForceList_list.Length > 0 && ForceList.Count == 0)
                ForceList.FromArray(ForceList_list);
        }

        /// <summary>存档前回写 int[]（否则会把读档时的旧 id 存回去）。</summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            ForceList_list = ForceList != null ? ForceList.ToArray() : null;
        }

        [JsonProperty] public int leftCount;
        [JsonProperty] public AllianceType allianceType;

        public bool Contains(Force force)
        {
            return ForceList.Contains(force);
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            leftCount--;
            IsAlive = leftCount > 0;
            if(!IsAlive )
            {
#if SANGO_DEBUG
                StringBuilder stringBuilder = new StringBuilder();
#endif
                foreach (Force force in ForceList)
                {
#if SANGO_DEBUG
                    stringBuilder.Append(force.Name);
                    stringBuilder.Append(" ");
#endif
                    force.AllianceList.Remove(this);
                }

#if SANGO_DEBUG
                string allianceTypeStr = allianceType == AllianceType.Alliance ? "同盟" : "停战协议";
                Sango.Log.Info($"@外交@{stringBuilder.ToString()} 的{allianceTypeStr} {Id} 于{scenario.GetDateStr()} 结束!!");
#endif

            }
            return base.OnTurnStart(scenario);
        }
    }
}
