using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action 
{ 
    /// <summary>
    /// 缩短城市工作工期（value 为百分比，jobIds 限定生效的工作类型）。
    ///
    /// 接入事件：OnCityJobCounterResult（城市工作工期计数结算时）。
    /// </summary>
    public class CityImproveJobCounterResult : CityActionBase
    {
        int value;
        List<int> jobIds;

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
            GameEvent.OnCityJobCounterResult += OnCityJobCounterResult;
        }

        public override void Clear()
        {
            GameEvent.OnCityJobCounterResult -= OnCityJobCounterResult;
        }

        void OnCityJobCounterResult(City city, int jobId, Person[] persons, OverrideData<int> overrideData)
        {
            if (City != city) return;
            if (jobIds != null && !jobIds.Contains(jobId)) return;
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
