using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 效果生效时立即给势力内所有据点补充耐久（只执行一次，不订阅事件）。
    ///
    /// 参数：value 补充的耐久值；kinds 限定据点类型，null 表示全部。
    /// 注意：这是"一次性加耐久"，不是"提升耐久上限"，上限请用 CityDurabilityLimit。
    /// 接入事件：无（容器触发）——由所在「触发器 + 效果」容器的触发器决定时机，单独挂载不生效
    /// </summary>
    public class CityAddDurability : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

        }

        public override void Clear()
        {
        }

        public override void Execute(Trigger trigger)
        {
            if (Force == null) return;
            Force.ForEachCityBase(x =>
            {
                x.ChangeDurability(value, null);
            });

        }
    }
}
