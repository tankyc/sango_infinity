using TKNewtonsoft.Json;
using Sango.Core.Action;
using Sango.Render;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 建筑类，继承自BuildingBase，用于管理游戏中的建筑对象
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Building : BuildingBase
    {
        /// <summary>
        /// 获取建筑的对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Building; } }

        /// <summary>
        /// 获取建筑的名称
        /// </summary>
        public override string Name { get { return BuildingType?.Name; } }

        /// <summary>
        /// 建筑的工人列表
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> Workers { get; set; }

        /// <summary>
        /// 建筑的建造者列表
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Person>))]
        [JsonProperty]
        public SangoObjectList<Person> Builder { get; set; }

        /// <summary>
        /// 剩余建造或升级的回合数
        /// </summary>
        [JsonProperty]
        public int LeftCounter { get; set; }

        /// <summary>
        /// 生产物品的ID
        /// </summary>
        [JsonProperty]
        public int ProductItemId { get; set; }

        /// <summary>
        /// 累积的产品数量
        /// </summary>
        [JsonProperty]
        public int AccumulatedProduct { get; set; }

        /// <summary>
        /// 累积的食物数量
        /// </summary>
        [JsonProperty]
        public int AccumulatedFood { get; set; }

        /// <summary>
        /// 累积的金币数量
        /// </summary>
        [JsonProperty]
        public int AccumulatedGold { get; set; }

        /// <summary>
        /// 累积的人口数量
        /// </summary>
        [JsonProperty]
        public int AccumulatedPopulation { get; set; }

        /// <summary>
        /// 地格收获的总食物数量
        /// </summary>
        public int cellHarvestTotalFood = 0;

        /// <summary>
        /// 地格收获的总金币数量
        /// </summary>
        public int cellHarvestTotalGold = 0;

        /// <summary>
        /// 建筑的行动列表
        /// </summary>
        public List<ActionBase> actionList;

        bool isInited = false;

        /// <summary>
        /// 比较两个建筑对象
        /// </summary>
        /// <param name="a">第一个建筑对象</param>
        /// <param name="b">第二个建筑对象</param>
        /// <returns>比较结果，按名称排序</returns>
        public static int Compare(Building a, Building b)
        {
            if (a != null && b != null)
            {
                return a.Name.CompareTo(b.Name);
            }

            if (a != null)
                return 1;

            if (b != null)
                return -1;

            return 0;
        }

        /// <summary>
        /// 场景准备时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);

            mBelongCity?.OnBuildingCreate(this);
            // 地格占用
            OccupyCellList = new List<Cell>();
            scenario.Map.GetSpiral(x, y, BuildingType.radius, OccupyCellList);
            foreach (Cell cell in OccupyCellList)
                cell.building = this;
            CenterCell = OccupyCellList[0];

            if (CenterCell.IsInterior)
                CenterCell.ClearInteriorModel();

            actionList = new List<ActionBase>();
            BuildingType.InitActions(actionList, this);

            // 效果范围
            effectCells = new System.Collections.Generic.List<Cell>();
            scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius + 1, BuildingType.radius + BuildingType.atkRange, effectCells);
            isInited = true;
            //Init(scenario);
        }

        /// <summary>
        /// 准备渲染时的回调方法
        /// </summary>
        public override void OnPrepareRender()
        {

            if (Render == null)
                Render = new BuildingRender(this);
        }

        /// <summary>
        /// 初始化建筑
        /// </summary>
        /// <param name="scenario">当前场景</param>
        public override void Init(Scenario scenario)
        {
            if (!isInited)
            {
                mBelongCity?.OnBuildingCreate(this);
                // 地格占用
                OccupyCellList = new List<Cell>();
                scenario.Map.GetSpiral(x, y, BuildingType.radius, OccupyCellList);
                foreach (Cell cell in OccupyCellList)
                    cell.building = this;
                CenterCell = OccupyCellList[0];

                if (CenterCell.IsInterior)
                    CenterCell.ClearInteriorModel();

                actionList = new List<ActionBase>();
                BuildingType.InitActions(actionList, this);

                // 效果范围
                effectCells = new System.Collections.Generic.List<Cell>();
                scenario.Map.GetDirectSpiral(CenterCell, BuildingType.radius + 1, BuildingType.radius + BuildingType.atkRange, effectCells);
                isInited = true;
            }

            OnPrepareRender();
        }

        /// <summary>
        /// 势力回合开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            ActionOver = false;

            // 检查人员,自动修复建造和工作关系
            if(Workers != null && Workers.Count > 0)
            {
                Workers.ForEach(x =>
                {
                    if (x.mBelongCity != mBelongCity || x.mBelongForce != mBelongForce || x.mTroop != null)
                    {
                        Workers.Remove(x);
                        if (x.workingBuilding == this)
                        {
                            x.workingBuilding = null;
                        }
                    }
                });
            }

            if (Builder != null && Builder.Count > 0)
            {
                Builder.ForEach(x =>
                {
                    if (x.mBelongCity != mBelongCity || x.mBelongForce != mBelongForce || x.mBelongCorps != mBelongCorps ||
                        x.missionType != (int)MissionType.PersonBuild || x.missionTarget != this.Id || x.mTroop != null)
                    {
                        Builder.Remove(x);
                    }
                });
            }

            if (!isComplate && Builder != null)
            {
                if (LeftCounter <= 0)
                    LeftCounter = 1;

                int totalValue = (BuildingType.durabilityLimit - durability) / LeftCounter;
                durability += totalValue;
                if (durability >= BuildingType.durabilityLimit)
                {
                    durability = BuildingType.durabilityLimit;
                    isComplate = true;
                    //CalculateHarvest();
                    SangoObjectList<Person> builder = Builder;
                    OnBuildComplate();
                    mBelongCity.OnBuildingComplete(this, builder);
                    GameEvent.OnBuildingComplete?.Invoke(this, builder);
                }
                if (LeftCounter > 0)
                    LeftCounter--;
            }
            else if (isUpgrading && Builder != null)
            {
                if (LeftCounter <= 0)
                    LeftCounter = 1;
                int totalValue = (BuildingType.durabilityLimit - durability) / LeftCounter;
                durability += totalValue;
                if (durability >= BuildingType.durabilityLimit)
                {
                    durability = BuildingType.durabilityLimit;
                    isUpgrading = false;
                    //CalculateHarvest();
                    SangoObjectList<Person> builder = Builder;
                    OnUpgradeComplate();
                    mBelongCity.OnBuildingUpgradeComplete(this, builder);
                    GameEvent.OnBuildingUpgradeComplete?.Invoke(this, builder);
                }

                if (LeftCounter > 0)
                    LeftCounter--;
            }
            else
            {
                if (LeftCounter > 0)
                    LeftCounter--;
                ActionOver = false;
            }

            if (IsIntorBuilding() && isComplate && !isUpgrading)
            {
                if(mBelongCity != null && !mBelongCity.IsEnemiesRound(6))
                {
                    // 耐久自修复
                    if (durability < DurabilityLimit)
                    {
                        ChangeDurability(mBelongCity.Leader?.BaseBuildAbility ?? 50, null);
                    }
                }
            }

            GameEvent.OnBuildingTurnStart?.Invoke(this, scenario);

            if (Render != null)
                Render.UpdateRender();

            return base.OnForceTurnStart(scenario);
        }

        /// <summary>
        /// 势力回合结束时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            GameEvent.OnBuildingTurnEnd?.Invoke(this, scenario);
            return base.OnForceTurnEnd(scenario);
        }

        /// <summary>
        /// 建筑完成时的回调方法
        /// </summary>
        /// <param name="builder">建造者</param>
        public override void OnComplate(SangoObject builder)
        {
            Troop atk = builder as Troop;
            if (atk == null) return;

            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Build;
            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            atk.ForEachPerson(person =>
            {
                person.merit += meritGain;
                person.GainExp(meritGain);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
            });

#if SANGO_DEBUG
            Sango.Log.Info($"[{mBelongCity.Name}]{stringBuilder}完成{Name}建造!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(mBelongCity, jobId, Workers.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            mBelongForce.GainTechniquePoint(techniquePointGain);
            Render.UpdateRender();

        }

        /// <summary>
        /// 建筑建造完成时的回调方法
        /// </summary>
        public virtual void OnBuildComplate()
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Build;
            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < Builder.Count; i++)
            {
                Person person = Builder[i];
                person.merit += meritGain;
                person.GainExp(meritGain);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                if (person.missionType == (int)MissionType.PersonBuild && person.missionTarget == this.Id)
                {
                    person.ClearMission();
                }
                person.ActionOver = false;
            }
#if SANGO_DEBUG
            Sango.Log.Info($"[{mBelongCity.Name}]{stringBuilder}完成{Name}建造!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(mBelongCity, jobId, Builder.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            mBelongForce.GainTechniquePoint(techniquePointGain);
            Builder = null;
        }

        /// <summary>
        /// 建筑升级完成时的回调方法
        /// </summary>
        public void OnUpgradeComplate()
        {
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Build;
            int meritGain = JobType.GetJobLimit(jobId);
            int techniquePointGain = JobType.GetJobTPGain(jobId);

            BuildingType nextBuildingType = scenario.GetObject<BuildingType>(BuildingType.nextId);
            BuildingType = nextBuildingType;

            durability = DurabilityLimit;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < Builder.Count; i++)
            {
                Person person = Builder[i];
                person.merit += meritGain;
                person.GainExp(meritGain);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
                if (person.missionType == (int)MissionType.PersonBuild && person.missionTarget == this.Id)
                {
                    person.ClearMission();
                }
            }
#if SANGO_DEBUG
            Sango.Log.Info($"[{mBelongCity.Name}]{stringBuilder}完成{Name}升级!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(mBelongCity, jobId, Builder.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            mBelongForce.GainTechniquePoint(techniquePointGain);
            Builder = null;
        }

        /// <summary>
        /// 改变建筑所属的城市
        /// </summary>
        /// <param name="dest">目标城市</param>
        public void ChangeCity(City dest)
        {
            if (!isComplate)
            {
                Sango.Log.Error("不允许转换一个未建好的建筑!!");
                OnFall(null);
                return;
            }

            mBelongCity.allBuildings.Remove(this);
            dest.allBuildings.Add(this);

            mBelongCorps = dest.mBelongCorps;
            mBelongForce = dest.mBelongForce;

            Render?.UpdateRender();
        }

        /// <summary>
        /// 改变建筑所属的军团
        /// </summary>
        /// <param name="corps">目标军团</param>
        /// <returns>之前所属的军团</returns>
        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;
            if (!isComplate && mBelongForce != corps.mBelongForce)
            {
                //Sango.Log.Error("不允许转换一个未建好的建筑!!");
                OnFall(null);
                return last;
            }

            if (mBelongCorps != corps)
            {
                last = mBelongCorps;
                mBelongCorps = corps;

                if (corps.mBelongForce != mBelongForce)
                {
                    mBelongForce = corps.mBelongForce;
                }

                Render?.UpdateRender();
            }
            return last;
        }

        /// <summary>
        /// 清理建筑资源
        /// </summary>
        public override void Clear()
        {
            IsAlive = false;

            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
                actionList = null;
            }

            Scenario.Cur.buildingSet.Remove(this);


            if (Builder != null)
            {
                for (int i = 0; i < Builder.Count; i++)
                {
                    Person person = Builder[i];
                    if(person.missionType == (int)MissionType.PersonBuild && person.missionTarget == this.Id)
                    {
                        person.ClearMission();
                    }
                }
                Builder = null;
            }

            //if (Workers != null)
            //{
            //    for (int i = 0; i < Workers.Count; i++)
            //    {
            //        Person person = Workers[i];
            //        person.ClearMission();
            //    }
            //    Workers = null;
            //}

            if (effectCells != null)
                effectCells.Clear();
            if (CenterCell != null)
            {
                CenterCell.building = null;

                if (CenterCell.IsInterior)
                    CenterCell.CreateInteriorModel();

                CenterCell = null;
            }
            if (Render != null)
            {
                Render.Clear();
                Render = null;
            }
        }

        /// <summary>
        /// 获取技能方法可用的部队数量
        /// </summary>
        /// <returns>可用的部队数量</returns>
        public override int GetSkillMethodAvaliabledTroops()
        {
            return 4000 + 4000 * durability / DurabilityLimit;
        }

        /// <summary>
        /// 建筑被摧毁时的回调方法
        /// </summary>
        /// <param name="atk">攻击者</param>
        public override void OnFall(SangoObject atk)
        {
            RemoveAllWorkers();
            mBelongCity?.OnBuildingDestroy(this);
            Clear();
        }

        /// <summary>
        /// 移除工人
        /// </summary>
        /// <param name="person">要移除的工人</param>
        public void RemoveWorker(Person person)
        {
            if (Workers == null) return;
            if (person.workingBuilding == this)
            {
                Workers.Remove(person);
                person.workingBuilding = null;
            }
        }

        /// <summary>
        /// 移除所有工人
        /// </summary>
        public void RemoveAllWorkers()
        {
            if (Workers == null) return;
            Workers.ForEach(worker =>
            {
                worker.workingBuilding = null;
            });
            Workers.Clear();
        }

        /// <summary>
        /// 添加工人
        /// </summary>
        /// <param name="person">要添加的工人</param>
        public void AddWorker(Person person)
        {
            if (Workers == null) Workers = new SangoObjectList<Person>();
            if (person.workingBuilding != null)
            {
                Building workingBuilding = person.workingBuilding;
                workingBuilding.RemoveWorker(person);
                workingBuilding.Render?.UpdateRender();
            }

            Workers.Add(person);
            person.workingBuilding = this;
        }

        /// <summary>
        /// 获取指定索引的工人
        /// </summary>
        /// <param name="index">工人索引</param>
        /// <returns>工人对象</returns>
        public Person GetWorker(int index)
        {
            if (Workers == null || index >= Workers.Count) return null;
            return Workers[index];
        }
    }
}
