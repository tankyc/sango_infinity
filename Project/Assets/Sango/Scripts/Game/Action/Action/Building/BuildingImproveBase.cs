using TKNewtonsoft.Json.Linq;
using Sango.Core.Tools;
using System.Collections.Generic;

namespace Sango.Core.Action
{


    /// <summary>
    /// 提升范围内部队基础类
    /// value:提升值
    /// bound: 生效范围 
    /// targetType: 作用目标范围, 0己方 1敌人 2所有
    /// </summary>
    public class BuildingImproveBase : BuildingActionBase
    {
        /// <summary>
        /// 提升值(百分比)
        /// </summary>
        protected int value;

        /// <summary>
        /// 生效范围
        /// </summary>
        protected int bound;

        /// <summary>
        /// 作用目标范围, 0己方 1敌人 2所有
        /// </summary>
        protected int tartgetType;

        /// <summary>
        /// 效果唯一key
        /// </summary>
        protected string improveKey;

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

            GameEvent.OnTroopEnterCell += OnTroopEnterCell;

            value = p.Value<int>("value");
            bound = p.Value<int>("bound");
            tartgetType = p.Value<int>("tartgetType");

            Building.CenterCell.Spiral(bound, (cell) =>
            {
                if (cell.troop != null)
                {
                    Troop troop = cell.troop;
                    if (tartgetType == 0 && !troop.IsSameForce(Building))
                        return;
                    if (tartgetType == 1 && !troop.IsEnemy(Building))
                        return;

                    if (troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase buildingImproveData))
                    {
                        if (buildingImproveData != this && buildingImproveData.value < value)
                        {
                            buildingImproveData.OnLeave(troop);
                            troop.buildingImproveMap[improveKey] = this;
                            OnEnter(troop);
                        }
                    }
                    else
                    {
                        troop.buildingImproveMap.Add(improveKey, this);
                        OnEnter(troop);
                    }
                }
            });

        }

        /// <summary>
        /// 清理建筑资金产量提升动作
        /// </summary>
        public override void Clear()
        {
            GameEvent.OnTroopEnterCell -= OnTroopEnterCell;
            Building.CenterCell.Spiral(bound, (cell) =>
            {
                if (cell.troop != null)
                {
                    Troop troop = cell.troop;
                    if (tartgetType == 0 && !troop.IsSameForce(Building))
                        return;
                    if (tartgetType == 1 && !troop.IsEnemy(Building))
                        return;

                    if(troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase buildingImproveData))
                    {
                        if(buildingImproveData == this)
                        {
                            troop.buildingImproveMap.Remove(improveKey);
                            OnLeave(troop);
                        }
                    }
                }
            });
        }

        void OnTroopEnterCell(Troop troop, Cell dest, Cell last)
        {
            if (tartgetType == 0 && !troop.IsSameForce(Building))
                return;
            if (tartgetType == 1 && !troop.IsEnemy(Building))
                return;

            int destDis = dest.Distance(Building.CenterCell);
            int lastDis = last == null ? 999 : last.Distance(Building.CenterCell);
            if (lastDis > bound && destDis <= bound)
            {
                if (troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase buildingImproveData))
                {
                    if (buildingImproveData != this && buildingImproveData.value < value)
                    {
                        buildingImproveData.OnLeave(troop);
                        troop.buildingImproveMap[improveKey] = this;
                        OnEnter(troop);
                    }
                }
                else
                {
                    troop.buildingImproveMap.Add(improveKey, this);
                    OnEnter(troop);
                }
            }
            else
            {
                if (lastDis <= bound && destDis > bound)
                {
                    if (troop.buildingImproveMap.TryGetValue(improveKey, out BuildingImproveBase buildingImproveData))
                    {
                        if (buildingImproveData == this)
                        {
                            OnLeave(troop);
                            troop.buildingImproveMap.Remove(improveKey);
                        }
                    }
                }
            }
        }
        protected virtual void OnEnter(Troop troop)
        {

        }

        protected virtual void OnLeave(Troop troop)
        {

        }
    }
}
