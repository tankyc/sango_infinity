using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 某兵种"对据点守军兵力伤害"的额外增益（百分比）。
    ///
    /// value：增加值（百分比，40 表示 ×1.4）
    /// kinds：兵种种类（1 剑 / 2 枪 / 3 戟 / 4 弩 / 5 骑 / 6 水军 / 7 输送 / 8 冲车木兽 / 9 井阑投石）
    /// 接入事件：OnTroopCalculateAttribute（累加到 <see cref="Troop.DamageBuildingTroopExtraFactor"/>）
    ///
    /// 【与 TroopAddDamageBuildingExtraFactor 的区别】
    ///   那个改的是打建筑的**耐久**伤害（calc_hp_damage），
    ///   这个改的是打据点（城 / 关 / 港）**守军兵力**的伤害（calc_troops_damage 的据点分支）。
    ///   原版「云梯」两段都 ×1.4（s11_sys_damage.cpp calc_attack_damage 的 target_building 分支），
    ///   所以 Techniques.json #20 同时配了这两个 Action。
    /// </summary>
    public class TroopAddDamageBuildingTroopExtraFactor : ForceTroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnTroopCalculateAttribute += OnTroopCalculateAttribute;
        }

        public override void Clear()
        {
            GameEvent.OnTroopCalculateAttribute -= OnTroopCalculateAttribute;
        }

        void OnTroopCalculateAttribute(Troop troop, Scenario scenario)
        {
            if (Force != troop.BelongForce) return;
            float factor = value / 100f;
            if (kinds == null)
            {
                troop.landDamageBuildingTroopExtraFactor += factor;
                troop.waterDamageBuildingTroopExtraFactor += factor;
            }
            else
            {
                if (kinds.Contains(troop.LandTroopType.kind))
                    troop.landDamageBuildingTroopExtraFactor += factor;
                if (kinds.Contains(troop.WaterTroopType.kind))
                    troop.waterDamageBuildingTroopExtraFactor += factor;
            }
        }
    }
}
