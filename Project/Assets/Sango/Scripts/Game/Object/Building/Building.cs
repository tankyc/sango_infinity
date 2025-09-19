﻿using Newtonsoft.Json;
using Sango.Game.Render;

namespace Sango.Game
{
    [JsonObject(MemberSerialization.OptIn)]
    public class Building : BuildingBase
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Building; } }

        public override string Name { get { return BuildingType.Name; } }
        public Person Builder { get; set; }
        public int cellHarvestTotalFood = 0;
        public int cellHarvestTotalGold = 0;
        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<City>))]
        [JsonProperty]
        public City BelongCity;

        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            Init(scenario);
        }

        public override void Init(Scenario scenario)
        {
            if (BelongCity != null)
            {
                BelongCity.allBuildings.Add(this);
                if (BuildingType.isIntrior)
                    BelongCity.allIntriorBuildings.Add(this);
            }

            //UnityEngine.Debug.LogError($"{BelongCity.Name}-><{x},{y}>");
            CenterCell = scenario.Map.GetCell(x, y);
            CenterCell.building = this;
            effectCells.Clear();
            scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius, effectCells);
            //CalculateHarvest();
            Render = new BuildingRender(this);
        }

        public override bool OnTurnStart(Scenario scenario)
        {
            if (!isComplte)
            {
                durability += Builder.BaseBuildAbility;
                if (durability >= BuildingType.durabilityLimit)
                {
                    durability = BuildingType.durabilityLimit;
                    isComplte = true;
                    //CalculateHarvest();
                    OnBuildComplate();
                    BelongCity.OnBuildingComplete(this);
                }
            }
            if (Render != null)
                Render.UpdateRender();
            return base.OnTurnStart(scenario);
        }

        public virtual void OnBuildComplate()
        {
            Sango.Log.Print($"[{Builder.BelongCity.Name}]{Builder.Name}完成{Name}建造!!");
            Builder.missionType = 0;
            Builder.missionTarget = 0;
            Builder.missionCounter = 0;
            Builder = null;
        }

        public void ChangeCity(City dest)
        {
            if (!isComplte)
            {
                Sango.Log.Error("不允许转换一个未建好的建筑!!");
                return;
            }

            BelongCity.allBuildings.Remove(this);

            dest.allBuildings.Add(this);

            BelongCorps = dest.BelongCorps;
            BelongForce = dest.BelongForce;

            Render?.UpdateRender();
        }

        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (!isComplte)
            {
                Sango.Log.Error("不允许转换一个未建好的建筑!!");
                return last;
            }

            if (BelongCorps != corps)
            {
                last = BelongCorps;
                BelongCorps = corps;

                if (corps.BelongForce != BelongForce)
                {
                    BelongForce = corps.BelongForce;
                }

                Render?.UpdateRender();
            }
            return last;
        }

        public void Destroy()
        {
            if (BelongCity != null)
            {
                if (BuildingType.isIntrior)
                    BelongCity.allIntriorBuildings.Remove(this);

                BelongCity.villageList.Remove(this);
                BelongCity.allBuildings.Remove(this);
            }

            Scenario.Cur.buildingSet.Remove(this);

            if (Builder != null)
            {
                Builder.missionType = 0;
                Builder.missionTarget = 0;
                Builder.missionCounter = 0;
            }

            effectCells.Clear();
            CenterCell.building = null;
            CenterCell = null;
            Render.Clear();
            Render = null;
        }

        public override void OnFall(Troop atk)
        {
            BelongCity.OnBuildingDestroy(this);
            Destroy();
        }
    }
}
