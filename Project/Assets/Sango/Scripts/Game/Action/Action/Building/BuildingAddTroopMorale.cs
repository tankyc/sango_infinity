using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;
using Sango.Render;

namespace Sango.Core.Action
{
    /// <summary>
    /// 增加范围内部队的气力
    /// value:提升值 bound: 生效范围 targetType: 作用目标范围, 0己方 1敌人 2所有
    /// </summary>
    public class BuildingAddTroopMorale : BuildingActionBase
    {
        /// <summary>
        /// 提升值
        /// </summary>
        int value;

        /// <summary>
        /// 生效范围
        /// </summary>
        int bound;

        /// <summary>
        /// 作用目标范围, 0己方 1敌人 2所有
        /// </summary>
        int tartgetType;

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            if (Building == null)
                return;

            GameEvent.OnBuildingTurnEnd += OnBuildingTurnEnd;

            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            tartgetType = p.Value<int>("tartgetType");
        }

        /// <summary>
        /// 清理建筑资金产量提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnBuildingTurnEnd -= OnBuildingTurnEnd;
        }

        static List<Troop> target_list = new List<Troop>();
        void OnBuildingTurnEnd(Building building, Scenario scenario)
        {
            if (building == Building && building.isComplate)
            {
                target_list.Clear();
                if (bound == -1)
                {
                    for (int i = 1; i < Building.effectCells.Count; i++)
                    {
                        Cell cell = Building.effectCells[i];
                        if (cell.troop != null)
                        {
                            Troop troop = cell.troop;
                            if (tartgetType == 0 && !troop.IsSameForce(Building))
                                continue;
                            if (tartgetType == 1 && !troop.IsEnemy(Building))
                                continue;

                            target_list.Add(troop);
                        }
                    }


                }
                else
                {
                    Building.CenterCell.Spiral(bound, (cell) =>
                    {
                        if (cell.troop != null)
                        {
                            Troop troop = cell.troop;
                            if (tartgetType == 0 && !troop.IsSameForce(Building))
                                return;
                            if (tartgetType == 1 && !troop.IsEnemy(Building))
                                return;

                            target_list.Add(troop);
                        }
                    });
                }

                if (target_list.Count > 0)
                {

                    BuildingAddTroopMoraleEvent buildingAddTroopMoraleEvent = RenderEvent.Instance.Create<BuildingAddTroopMoraleEvent>();
                    buildingAddTroopMoraleEvent.Init(building, target_list, value);
                    RenderEvent.Instance.Add(buildingAddTroopMoraleEvent);
                }
            }
        }
    }
}
