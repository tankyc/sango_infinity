using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 计算建筑的资金产量，并提升系数  
    /// value:提升系数（百分比） kinds：有效kind类型合集 bound: 生效范围 0：该城市全局 1：周围一圈
    /// </summary>
    public class BuildingImproveGoldGain : BuildingActionBase
    {
        /// <summary>
        /// 提升系数（百分比）
        /// </summary>
        int value;

        /// <summary>
        /// 有效kind类型合集
        /// </summary>
        List<int> kinds;

        /// <summary>
        /// 生效范围 0：该城市全局 1：周围一圈
        /// </summary>
        int bound;

        /// <summary>
        /// 初始化建筑资金产量提升动作
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            if (Building == null)
                return;
            GameEvent.OnBuildingCalculateGoldGain += OnBuildingCalculateGoldGain;
            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            JArray kindsArray = p.Value<JArray>("kinds");
            if (kindsArray != null)
            {
                kinds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    kinds.Add(kindsArray[i].ToObject<int>());
            }
        }

        /// <summary>
        /// 清理建筑资金产量提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnBuildingCalculateGoldGain -= OnBuildingCalculateGoldGain;
        }

        /// <summary>
        /// 计算建筑资金产量提升
        /// </summary>
        /// <param name="buildingBase">建筑基础对象</param>
        /// <param name="overrideData">覆盖数据对象</param>
        void OnBuildingCalculateGoldGain(BuildingBase buildingBase, OverrideData<int> overrideData)
        {
            if (Force != null && buildingBase.BelongForce != Force) return;
            if (Building != null && Building != buildingBase) return;

            if (bound == 0)
            {
                int gold = 0;
                Building.BelongCity.allBuildings.ForEach(b =>
                {
                    if (!b.IsIntorBuilding()) return;

                    // 只对农田有效
                    if (kinds != null && !kinds.Contains(b.BuildingType.kind))
                        return;

                    gold += b.BuildingType.goldGain * value;

                });
                overrideData.Value += gold / 100;
            }
            else
            {

                int gold = 0;
                for (int i = 0; i < Building.CenterCell.Neighbors.Length; ++i)
                {
                    Cell neighbor = Building.CenterCell.Neighbors[i];
                    if (neighbor == null || !neighbor.IsInterior || neighbor.building == null) continue;

                    BuildingBase target = neighbor.building;
                    // 只对农田有效
                    if (kinds != null && !kinds.Contains(target.BuildingType.kind))
                        continue;

                    gold += target.BuildingType.goldGain * value;
                }
                overrideData.Value += gold / 100;
            }
        }
    }
}
