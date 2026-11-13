using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 每回合城池耐久自然回复量修正（技巧「培训工兵」）。
    ///
    /// value：百分比（250 表示回复量 ×2.5，100 表示不变）
    /// kinds：建筑类型（1 都市 / 2 关 / 3 港）
    /// 接入事件：OnCityCalculateDurabilityRecover（由 City.OnForceTurnStart 的耐久自修复段派发）
    ///
    /// 【原版出处】s11_game_week.cpp:943-953
    ///   持有 TechId_Kouheiikusei 时，据点周回复量 = clamp(政治×9/2, 150, 450)；
    ///   未持有时 = 同一 clamp 值 ×2/5（60~180），即技巧效果为「回复量 ×2.5」。
    ///   原版每周结算一次；本项目在势力回合开始（每回合一次）结算，频率口径一致。
    /// </summary>
    public class CityDurabilityRecover : ForceBuildingActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnCityCalculateDurabilityRecover += OnCityCalculateDurabilityRecover;
        }

        public override void Clear()
        {
            GameEvent.OnCityCalculateDurabilityRecover -= OnCityCalculateDurabilityRecover;
        }

        void OnCityCalculateDurabilityRecover(City city, OverrideData<int> overrideData)
        {
            if (!CheckForceBuilding(city)) return;
            // 百分比修正：value=250 → 回复量 ×2.5
            overrideData.Value = overrideData.Value * value / 100;
        }
    }
}
