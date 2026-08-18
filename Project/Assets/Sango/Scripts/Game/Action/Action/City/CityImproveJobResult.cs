using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action 
{ 
    /// <summary>
    /// 提升工作效率
    /// value: 百分比
    /// jobIds: 工作ID集合
    /// </summary>
    public class CityImproveJobResult : CityActionBase
    {
        /// <summary>
        /// 提升百分比
        /// </summary>
        int value;

        /// <summary>
        /// 工作ID集合
        /// </summary>
        List<int> jobIds;

        /// <summary>
        /// 初始化城市工作效率提升动作
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            value = p.Value<int>("value");
            JArray kindsArray = p.Value<JArray>("jobIds");
            if (kindsArray != null)
            {
                jobIds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    jobIds.Add(kindsArray[i].ToObject<int>());
            }
            GameEvent.OnCityJobResult += OnCityJobResult;
        }

        /// <summary>
        /// 清理城市工作效率提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnCityJobResult -= OnCityJobResult;
        }

        /// <summary>
        /// 处理城市工作结果提升
        /// </summary>
        /// <param name="city">城市对象</param>
        /// <param name="jobId">工作ID</param>
        /// <param name="persons">参与工作的人员</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnCityJobResult(City city, int jobId, Person[] persons, OverrideData<int> overrideData)
        {
            if (City != city) return;
            if (jobIds != null && !jobIds.Contains(jobId)) return;
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
