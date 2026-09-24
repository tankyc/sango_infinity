using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 击破敌方部队时按配置倍率提高本部队获得的技巧点。
    /// </summary>
    public class TroopImproveDefeatTechniquePoint : TroopActionBase
    {
        /// <summary>
        /// 订阅本部队的技巧点结算事件并读取倍率配置。
        /// </summary>
        /// <param name="p">包含 value 百分比的 Action 配置。</param>
        /// <param name="sangoObjects">第一个对象为装配该特技的部队。</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateTechniquePoint += OnTroopCalculateTechniquePoint;
        }

        /// <summary>
        /// 解除技巧点结算事件订阅。
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopCalculateTechniquePoint -= OnTroopCalculateTechniquePoint;
        }

        /// <summary>
        /// 仅在本部队击破敌方部队时按配置倍率改写本次技巧点。
        /// </summary>
        private void OnTroopCalculateTechniquePoint(Troop troop, bool isDestroyEnemyTroop, OverrideData<int> techniquePoint)
        {
            if (Troop != troop || !isDestroyEnemyTroop)
            {
                return;
            }
            techniquePoint.Value = techniquePoint.Value * value / 100;
        }
    }
}
