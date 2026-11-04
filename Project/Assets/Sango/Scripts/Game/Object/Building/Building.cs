using Newtonsoft.Json;
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
        // 序列化形态保持 int[]（键名不变，老存档可读）。
        // 解析写在**本类原有的** OnScenarioPrepare（见 :137）里，避免重复定义（CS0111）。
        [JsonProperty("Workers")]
        public int[] Workers_list;
        public SangoObjectList<Person> Workers { get; set; } = new SangoObjectList<Person>();

        /// <summary>存档前回写 int[]（否则会把读档时的旧 id 存回去）。</summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            Workers_list = Workers != null ? Workers.ToArray() : null;
            Builder_list = Builder != null ? Builder.ToArray() : null;
        }

        /// <summary>
        /// 建筑的建造者列表
        /// </summary>
        // 序列化形态保持 int[]（键名不变，老存档可读）。
        [JsonProperty("Builder")]
        public int[] Builder_list;
        public SangoObjectList<Person> Builder { get; set; } = new SangoObjectList<Person>();

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

            // 【int[] → 对象】本类序列化列表的解析（统一放在原有的 OnScenarioPrepare 里）
            if (Workers_list != null && Workers_list.Length > 0 && Workers.Count == 0)
                Workers.FromArray(Workers_list);
            if (Builder_list != null && Builder_list.Length > 0 && Builder.Count == 0)
                Builder.FromArray(Builder_list);

            // 【设施类型缺失】BuildingType 是按 id 懒解析的可空引用（BuildingBase.cs:44-52）：
            // BuildingTypeId <= 0，或设施表里查不到这个 id（Mod 覆盖精简了 BuildingTypes.json、
            // 存档来自缺该设施的版本、剧本编辑器直接摆放）时，它就是 null。
            // 下面的 OnBuildingCreate / GetSpiral / InitActions / attackMaxRange 全都要解引用它，
            // 直接往下走会抛 NullReferenceException，而且发生在世界初始化的 Prepare 阶段，
            // 会把整个开局或读档一起带崩（实战崩在 Scenario.Prepare → Building.OnScenarioPrepare）。
            // 处理策略：只补"中心格"—— 后续逻辑（渲染、选中、失效判断）最常访问的就是它；
            // 其余依赖设施类型的初始化整段跳过；isInited 置 true，
            // 免得到时候 Building.Init 再走一遍同样必崩的分支。
            if (BuildingType == null)
            {
                Sango.Log.Error($"*[设施]{Id} 找不到设施类型（BuildingTypeId={BuildingTypeId}），已跳过初始化，请检查数据或 Mod 是否缺少该设施");
                OccupyCellList = new List<Cell>();
                CenterCell = scenario.Map.GetCell(x, y);
                if (CenterCell != null)
                    CenterCell.building = this;
                isInited = true;
                return;
            }

            BelongCity?.OnBuildingCreate(this);
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

            // 攻击范围（attackMinRange / attackMaxRange，0 表示不攻击）
            effectCells = new System.Collections.Generic.List<Cell>();
            if (BuildingType.attackMaxRange > 0)
                scenario.Map.GetDirectSpiral(CenterCell,
                    BuildingType.radius + BuildingType.attackMinRange,
                    BuildingType.radius + BuildingType.attackMaxRange, effectCells);
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
                // 与 OnScenarioPrepare 同一原因：设施类型缺失时不能占地、不能建动作表，
                // 否则 GetSpiral / InitActions / attackMaxRange 全都会空引用。
                // 正常情况下 OnScenarioPrepare 已经把 isInited 置 true，这里主要是防
                // "建筑在运行期才创建、没走过 Prepare"的路径。
                if (BuildingType == null)
                {
                    isInited = true;
                    OnPrepareRender();
                    return;
                }

                BelongCity?.OnBuildingCreate(this);
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

                // 攻击范围（attackMinRange / attackMaxRange，0 表示不攻击）
                effectCells = new System.Collections.Generic.List<Cell>();
                if (BuildingType.attackMaxRange > 0)
                    scenario.Map.GetDirectSpiral(CenterCell,
                        BuildingType.radius + BuildingType.attackMinRange,
                        BuildingType.radius + BuildingType.attackMaxRange, effectCells);
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
            if (Workers != null && Workers.Count > 0)
            {
                // 【性能】原 ForEach(lambda) 捕获 this，改为按下标遍历。
                // 必须**倒序**：SangoObjectList.ForEach 本身就是倒序（Count-1 → 0），
                // 这里在循环体内 Remove，正序会跳过元素。
                for (int i = Workers.Count - 1; i >= 0; i--)
                {
                    Person x = Workers[i];
                    if (x == null)
                        continue;
                    if (x.BelongCity != BelongCity || x.BelongForce != BelongForce || x.mBelongTroop != null)
                    {
                        Workers.Remove(x);
                        if (x.workingBuilding == this)
                        {
                            x.workingBuilding = null;
                        }
                    }
                }
            }

            if (Builder != null && Builder.Count > 0)
            {
                // 【性能】同上方 Workers：SangoObjectList.ForEach 为倒序遍历，且循环体内 Remove，
                // 改成 for 时必须保持倒序，否则会跳过元素。
                for (int i = Builder.Count - 1; i >= 0; i--)
                {
                    Person x = Builder[i];
                    if (x == null)
                        continue;
                    if (x.BelongCity != BelongCity || x.BelongForce != BelongForce || x.BelongCorps != BelongCorps ||
                        x.missionType != (int)MissionType.PersonBuild || x.missionTarget != this.Id || x.mBelongTroop != null)
                    {
                        Builder.Remove(x);
                    }
                }
            }

            if (!isComplate && Builder != null && Builder.Count > 0)
            {
                if (LeftCounter <= 0)
                    LeftCounter = 1;

                int totalValue = (BuildingType.durabilityLimit - durability) / LeftCounter;
                durability += totalValue;
                if (durability >= BuildingType.durabilityLimit)
                {
                    durability = BuildingType.durabilityLimit;
                    isComplate = true;
                    OnCompleted();
                    //CalculateHarvest();
                    SangoObjectList<Person> builder = Builder;
                    OnBuildComplate();
                    BelongCity.OnBuildingComplete(this, builder);
                    GameEvent.OnBuildingComplete?.Invoke(this, builder);
                }
                if (LeftCounter > 0)
                    LeftCounter--;
            }
            else if (isUpgrading && Builder != null && Builder.Count > 0)
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
                    BelongCity.OnBuildingUpgradeComplete(this, builder);
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
                if (BelongCity != null && !BelongCity.IsEnemiesRound(6))
                {
                    // 耐久自修复
                    if (durability < DurabilityLimit)
                    {
                        ChangeDurability(BelongCity.Leader?.BaseBuildAbility ?? 50, null);
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
            int meritGain = JobType.GetJobMeritGain(jobId);
            // 【原版对齐】建筑完成 → 设施表 tech_point
            //（s11_sys_person_order_city.cpp:228 `force_add_tech_point(force, facility->get_tech_point(), building)`）。
            // 本项目对应 BuildingType.techGain；走统一入口后会被减半 + 上限夹取。
            int techniquePointGain = BuildingType != null ? BuildingType.techGain : 0;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            atk.ForEachPerson(person =>
            {
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 建造 → 政治经验
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(" ");
#endif
            });

#if SANGO_DEBUG
            Sango.Log.Info($"[{BelongCity.Name}]{stringBuilder}完成{Name}建造!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(BelongCity, jobId, Workers.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
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
            int meritGain = JobType.GetJobMeritGain(jobId);
            // 【原版对齐】建筑完成 → 设施表 tech_point（s11_sys_person_order_city.cpp:228）
            int techniquePointGain = BuildingType != null ? BuildingType.techGain : 0;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < Builder.Count; i++)
            {
                Person person = Builder[i];
                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);      // 建造/升级 → 政治经验
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
            Sango.Log.Info($"[{BelongCity.Name}]{stringBuilder}完成{Name}建造!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(BelongCity, jobId, Builder.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
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
            int meritGain = JobType.GetJobMeritGain(jobId);

            BuildingType nextBuildingType = scenario.GetObject<BuildingType>(BuildingType.nextId);

            // 【原版对齐】升级按**新设施**的 tech_point 给（原版 on_build_completed 用的就是完成后的 facility，
            // s11_sys_person_order_city.cpp:215-228）。所以这里必须取 nextBuildingType，不能取旧 BuildingType。
            int techniquePointGain = nextBuildingType != null ? nextBuildingType.techGain : 0;
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
                person.GainJobAttributeExp(jobId);      // 建造/升级 → 政治经验
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
            Sango.Log.Info($"[{BelongCity.Name}]{stringBuilder}完成{Name}升级!!");
#endif
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(techniquePointGain);
            GameEvent.OnCityJobGainTechniquePoint?.Invoke(BelongCity, jobId, Builder.objects.ToArray(), overrideData);
            techniquePointGain = overrideData.ValueAndRecycle;

            BelongForce.GainTechniquePoint(techniquePointGain);
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

            BelongCity.allBuildings.Remove(this);
            dest.allBuildings.Add(this);

            BelongCorps = dest.BelongCorps;
            BelongForce = dest.BelongForce;

            Render?.UpdateRender();
        }

        /// <summary>
        /// 建造完成后的归属归一（对齐 Open311 的 Building::set_completed）。
        ///
        /// 陷阱（Explosive）与障碍（Obstacle）一旦建成就不再属于任何势力：
        ///   · 它们的效果对敌我一致（谁踩都触发），挂在建造方名下会出现
        ///     "敌方踩雷算我方战果""设施归属面板显示我方却处在中立区域"之类的错乱；
        ///   · 原版因此在 set_completed 里无条件把 owner_force 清成 -1，
        ///     而 has_builder() 仍为 true → get_force_id() 返回 -1 = 中立。
        ///
        /// 原版例外：堤防（Levee）虽归在陷阱类型，但 is_trap_type() 显式排除它，
        /// 不参与中立化。本项目目前没有堤防设施；若后续加入，需在这里按 kind 排除。
        /// </summary>
        public override void OnCompleted()
        {
            base.OnCompleted();

            BuildingType type = BuildingType;
            if (type == null)
                return;

            if (type.IsExplosive || type.IsObstacle)
            {
                // 原版例外：堤防（Levee）虽然归在陷阱类型里，但 is_trap_type() 显式排除它。
                // 它需要长期归属修复方（谁修的堤谁负责），不能中立化。
                if (type.kind == (int)BuildingKindType.Levee)
                    return;

                BelongForce = null;
                BelongCorps = null;
            }
        }

        /// <summary>
        /// 改变建筑所属的军团
        /// </summary>
        /// <param name="corps">目标军团</param>
        /// <returns>之前所属的军团</returns>
        public Corps ChangeCorps(Corps corps)
        {
            Corps last = null;

            // 军团可以为 null（城变无主 / 中立化），此时势力也应清空。
            // 原版 building_set_district_id 对 district 为 -1 是合法输入，
            // 直接解引用 corps.BelongForce 会在这里空引用崩溃。
            Force newForce = corps != null ? corps.BelongForce : null;

            // 【同一军团：只纠正势力字段，绝不拆除】
            // `CityStateSystem.SyncBuildings` 会遍历城内的内政设施、对"军团与城不一致"的逐个调本方法。
            // 若此时军团其实没变、只是 `BelongForce` 缓存脏了，旧逻辑会命中下面的
            // "未完工 → OnFall" 分支，把在建设施静默清掉 ——
            // 表现为"最后一个空格建了没建好就没了、且没有任何拆除日志"。
            if (BelongCorps == corps)
            {
                if (corps != null && BelongForce != newForce)
                    BelongForce = newForce;
                return last;
            }

            // 未完工 / 升级中的建筑**跨势力**易手 = 直接拆除
            // （对齐原版：建造中的设施不留给敌方）
            if ((!isComplate || isUpgrading) && BelongForce != newForce)
            {
                // 【必须打日志】这条路径此前完全静默，是"建筑凭空消失却查不到原因"的主要来源之一
                Sango.Log.Info($"*[建筑]{Name} 未完工且随城易主，拆除"
                    + $"（{BelongForce?.Name ?? "无势力"} → {newForce?.Name ?? "无势力"}）");
                OnFall(null);
                return last;
            }

            if (BelongCorps != corps)
            {
                last = BelongCorps;
                BelongCorps = corps;

                if (newForce != BelongForce)
                {
                    BelongForce = newForce;
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
                    if (person.missionType == (int)MissionType.PersonBuild && person.missionTarget == this.Id)
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
            // 陷阱的爆发已改挂在 OnHit（命中即爆），这里不再触发。
            // 走到 OnFall 说明是"非攻击性销毁"（势力灭亡清场、中立化、未完工拆除、
            // 编辑器删除），这些场景原版也是直接移除、不引爆。
            RemoveAllWorkers();
            BelongCity?.OnBuildingDestroy(this);
            Clear();
        }

        /// <summary>
        /// 被攻击命中：陷阱立即引爆（对齐 Open311 的火球 / 落石）。
        ///
        /// 只负责"结算效果"，销毁仍走 ChangeDurability 的通用流程
        /// （耐久归零 → OnFall → 摘工人 / 通知所属城 / 归还槽位）。
        /// 早期版本在这里直接调 OnFall，会让对象在调用方读 Name / 结算战功之前
        /// 就归还对象池，日志与功绩都会读到已被复用的脏数据。
        /// </summary>
        /// <param name="atk">攻击者</param>
        /// <returns>true = 本次命中已特殊处理，ChangeDurability 会把耐久直接归零</returns>
        public override bool OnHit(SangoObject atk)
        {
            if (!BuildingTrapEffect.CanTrigger(this))
                return false;

            // 结算爆发（此时 CenterCell / BuildingType 仍有效）
            BuildingTrapEffect.Trigger(this, atk);

            return true;
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
            // 【性能】原 ForEach(lambda) 改为按下标遍历（顺序无关，保持倒序与原实现一致）。
            for (int i = Workers.Count - 1; i >= 0; i--)
            {
                Person worker = Workers[i];
                if (worker != null)
                    worker.workingBuilding = null;
            }
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
