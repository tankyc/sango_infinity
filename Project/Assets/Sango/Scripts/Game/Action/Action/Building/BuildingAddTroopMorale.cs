using Newtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;
using Sango.Render;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑回合结束时，给范围内的部队恢复气力。
    ///
    /// 接入事件：OnBuildingTurnEnd（建筑回合结束，且建筑必须已完工）。
    ///
    /// 参数：
    /// value      恢复的气力值
    /// bound      生效范围（格），-1 表示使用建筑自身的 effectCells（建筑效果范围）
    /// targetType 作用对象 0 己方 / 1 敌人 / 2 所有（旧配置键名拼写为 tartgetType，仍然兼容）
    /// </summary>
    public class BuildingAddTroopMorale : BuildingActionBase
    {
        /// <summary>
        /// 恢复的气力值
        /// </summary>
        int value;

        /// <summary>
        /// 生效范围（格），-1 表示使用建筑自身的 effectCells
        /// </summary>
        int bound;

        /// <summary>
        /// 作用对象范围：0 己方 / 1 敌人 / 2 所有
        /// </summary>
        int targetType;

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
            targetType = ReadTargetType(p);
        }

        /// <summary>
        /// 清理建筑回合结束回气动作
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
                            if (targetType == 0 && !troop.IsSameForce(Building))
                                continue;
                            if (targetType == 1 && !troop.IsEnemy(Building))
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
                            if (targetType == 0 && !troop.IsSameForce(Building))
                                return;
                            if (targetType == 1 && !troop.IsEnemy(Building))
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
