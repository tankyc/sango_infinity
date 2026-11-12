using Newtonsoft.Json;
using Sango.Render;
using System.Collections.Generic;
using System;

namespace Sango.Core
{

    public abstract class BuildingBase : SangoObjectExtensionData
    {
        public virtual string ColorName => $"<color=#93C86D>{Name}</color>";

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty("BelongForce")]
        public int BelongForceId;
        public virtual Force BelongForce { get; set; }

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty("BelongCorps")]
        public int BelongCorpsId;
        public Corps BelongCorps;

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonProperty("BelongCity")]
        public int BelongCityId;
        public City BelongCity;

        /// <summary>
        /// 建筑类型的 id（存档数值），读取 BuildingType 时按需解析
        /// </summary>
        [JsonProperty("BuildingType")]
        public int BuildingTypeId;

        BuildingType mBuildingType;
        /// <summary>
        /// 建筑类型；写入时自动同步 BuildingTypeId
        /// </summary>
        public BuildingType BuildingType
        {
            get
            {
                if (mBuildingType == null && BuildingTypeId > 0) mBuildingType = IdRef.Resolve<BuildingType>(BuildingTypeId);
                return mBuildingType;
            }
            set { mBuildingType = value; BuildingTypeId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 当前耐久
        /// </summary>
        [JsonProperty] public int durability;

        /// <summary>
        /// 地图坐标
        /// </summary>
        public MapCoords coords;
        [JsonProperty] public int x;
        [JsonProperty] public int y;

        /// <summary>
        /// 旋转值
        /// </summary>
        [JsonProperty] public float rot;

        /// <summary>
        /// 高度偏移
        /// </summary>
        [JsonProperty] public float heightOffset;

        [JsonProperty]
        public string model;

        /// <summary>
        /// 是否建造完成
        /// </summary>
        [JsonProperty] public bool isComplate;

        /// <summary>
        /// 是否升级中
        /// </summary>
        [JsonProperty] public bool isUpgrading;

        /// <summary>
        /// 是否工作中
        /// </summary>
        [JsonProperty] public bool isWorking;

        /// <summary>
        /// 耐久上限。
        ///
        /// 【必须判空】BuildingType 是按 id 懒解析的可空引用（见本文件 44-52 行）：
        /// 设施表里查不到该 id（Mod 精简了 BuildingTypes.json、存档来自缺该设施的版本、
        /// 剧本编辑器直接摆放）时它就是 null。
        /// 实测崩溃栈：BuildingRender.OnModelLoaded → UIBuildingHeadbar.UpdateState
        /// → BuildingBase.get_DurabilityLimit（地图建筑血条一显示就空引用）。
        ///
        /// 【缺省值取 1 而不是 0】耐久 0 会被 UI 与战斗公式当成"已损毁"，
        /// 血条还要按 0 做除法；1 是"类型未知"的安全下限，既画得出血条也不会被判成废墟。
        /// 原版不存在这种状态：建筑类型由 building_id 取值区间决定
        ///（utils::is_city_building_id 等，s11_db.cpp:1432/1451/1470），没有"查表失败"。
        /// </summary>
        public virtual int DurabilityLimit => BuildingType?.durabilityLimit ?? 1;

        /// <summary>
        /// 中心Cell
        /// </summary>
        public virtual Cell CenterCell { get; set; }

        /// <summary>
        /// 占用的cell
        /// </summary>
        public List<Cell> OccupyCellList { get; set; }

        /// <summary>
        /// 渲染器
        /// </summary>
        public ObjectRender Render { get; set; }

        public override ObjectRender GetRender() { return Render; }

        public bool IsPlayer => BelongForce?.IsPlayer ?? false;

        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => BelongCorps?.IsPlayerControl ?? false;

        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// 作用范围
        /// </summary>
        public List<Cell> effectCells;// = new List<Cell>();

        public bool IsCityBase()
        {
            return IsCity() || IsPort() || IsGate();
        }

        /// <summary>
        /// 是否为城市（都市 / 小城）
        /// </summary>
        /// <remarks>
        /// 【必须判空 BuildingType】见本文件 44-52 行，BuildingType 是按 id 懒解析的可空引用：
        /// BuildingTypeId &lt;= 0，或设施表里查不到该 id（Mod 精简了 BuildingTypes.json、
        /// 存档来自缺该设施的版本、剧本编辑器直接摆放）时它就是 null。
        /// 这类"孤儿建筑"仍会被 <see cref="Building.OnScenarioPrepare"/> 挂到
        /// <c>CenterCell.building</c> 上（Building.cs:148-157），于是地图上任何遍历地格的地方都会撞上它。
        /// 实测崩溃栈：TroopReturnCity.Prepare (TroopReturnCity.cs:87) → BuildingBase.IsCity()。
        /// 原版的城市 / 港口 / 关隘判定基于 building_id 的取值区间，不查表、不存在"类型解析失败"这一态：
        /// utils::is_city_building_id（s11_db.cpp:1432）、is_port_building_id（s11_db.cpp:1470）、
        /// is_gate_building_id（s11_db.cpp:1451）都是 <c>in_range(...)</c>。
        /// 故此处把"类型缺失"归为非据点，与原版语义等价，且不会误判任何正常建筑。
        /// </remarks>
        public bool IsCity()
        {
            BuildingType type = BuildingType;
            return type != null
                && (type.kind == (int)BuildingKindType.City || type.kind == (int)BuildingKindType.LittleCity);
        }

        /// <summary>是否为港口（类型缺失时返回 false，见 <see cref="IsCity"/> 的说明）</summary>
        public bool IsPort()
        {
            BuildingType type = BuildingType;
            return type != null && type.kind == (int)BuildingKindType.Port;
        }

        /// <summary>是否为关隘（类型缺失时返回 false，见 <see cref="IsCity"/> 的说明）</summary>
        public bool IsGate()
        {
            BuildingType type = BuildingType;
            return type != null && type.kind == (int)BuildingKindType.Gate;
        }

        /// <summary>
        /// 是否为"港关"（港口 / 关隘）。
        ///
        /// 用途：入城时的"容量溢出（吞兵）"检查只对港关生效。
        /// 城池不检查容量 —— 城兵满时若还拦着，部队就永远回不了城。
        /// 见 <see cref="Troop.EnterCity"/> 与 <see cref="TroopAIFallback.TryRerouteForOverflow"/>。
        /// </summary>
        /// <returns>港口或关隘返回 true</returns>
        public bool IsPortOrGate()
        {
            return IsPort() || IsGate();
        }
        /// <summary>
        /// 是否为"内政设施"（内政地建筑）。
        /// 【必须判空】BuildingType 可为 null（孤儿建筑），详见 <see cref="IsCity"/> 的说明：
        /// 调用点遍布 UI 头bar 刷新（UIBuildingBaseHeadbar.UpdateState），不拦会每帧抛。
        /// </summary>
        public bool IsIntorBuilding()
        {
            BuildingType type = BuildingType;
            return type != null && type.IsIntrior;
        }

        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (BelongForceId > 0)
                BelongForce = scenario.forceSet.Get(BelongForceId);
            if (BelongCityId > 0)
                BelongCity = scenario.citySet.Get(BelongCityId);


            if (BelongCorpsId > 0)
                BelongCorps = scenario.corpsSet.Get(BelongCorpsId);

            // 【归属治理 · 与原版对齐】据点的势力由所挂军团派生（Open311: Force <- Corps <- City）。
            // 存档里可能出现"势力=A、军团=B 势力的军团"的历史脏数据，
            // 这里以军团为锚点纠正，避免读档后整局带着不一致继续跑。
            // 地图建筑（Building）不参与：它的势力是独立记录的 owner_force，
            // 内政设施则跟随所属都市，都不是军团派生。
            if (this is City && BelongCorps != null && BelongForceId != BelongCorps.BelongForceId)
            {
                Sango.Log.Warning($"*[归属]{Name} 读档势力 {BelongForce?.Name ?? "无"} 与军团 {BelongCorps.Name} 的势力 {BelongCorps.BelongForce?.Name ?? "无"} 不一致，已以军团为准");
                BelongForceId = BelongCorps.BelongForceId;
                BelongForce = scenario.forceSet.Get(BelongForceId);

            }

            effectCells = new List<Cell>();
            //BelongForceId = scenario.forceSet.Get(_belongForceId);
            //BelongCorpsId = scenario.corpsSet.Get(_belongCorpsId);
            //BuildingType = scenario.CommonData.BuildingTypes.Get(_buildingTypeId);

            // 陷阱 / 障碍在存档里可能残留归属（旧存档或编辑器直接赋值）。
            // 读档时按"建成即中立"纠正一次（对齐 Open311 set_completed 的副作用）。
            // BuildingType 是懒解析，此处若尚未就绪会返回 null，
            // OnCompleted 内部会安全跳过，由 GovernanceValidator 的年度巡检兜底。
            if (this is Building building && building.isComplate)
                building.OnCompleted();
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            // 【归属治理】据点的势力永远与军团保持一致，避免把不一致写进存档。
            // 存档是脏数据扩散的关键节点：一旦写进文件，读档只能靠局部兜底捞回。
            if (this is City && BelongCorps != null)
                BelongForce = BelongCorps.BelongForce;

            BelongForceId = BelongForce?.Id ?? 0;
            BelongCorpsId = BelongCorps?.Id ?? 0;
            BelongCityId = BelongCity?.Id ?? 0;
        }

        public override void Init(Scenario scenario)
        {
            base.Init(scenario);
            OnPrepareRender();
        }

        public virtual void OnPrepareRender()
        {

        }

        public bool IsAlliance(BuildingBase other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(BuildingBase other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(BuildingBase other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsAlliance(Troop other)
        {
            return IsAlliance(BelongForce, other.BelongForce);
        }

        public bool IsEnemy(Troop other)
        {
            return IsEnemy(BelongForce, other.BelongForce);
        }

        public bool IsSameForce(Troop other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }
        public bool IsSameForce(Person other)
        {
            return IsSameForce(BelongForce, other.BelongForce);
        }

        public bool IsBeSurrounded()
        {
            List<Cell> cells = new List<Cell>();
            Scenario.Cur.Map.GetRing(x, y, BuildingType.radius + 1, cells);
            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                if (cell == null) continue;
                if (cell.troop == null && cell.building == null) return false;
                if (cell.troop != null && !cell.troop.IsEnemy(this)) return false;
            }
            return true;
        }
        /// <summary>
        /// 判断城池 / 建筑周围的出兵通道是否被完全封锁。
        ///
        /// 【修复】原实现把"己方建筑"也视为封锁(只有空地才算通行),
        /// 导致周边建筑密集的大城会被误判为"无法出兵",从而永远不派兵防守。
        /// 现在只有"敌方部队"才构成封锁:空地、建筑以及友军 / 中立部队均视为可通行。
        /// </summary>
        /// <returns>是否被敌军完全封锁</returns>
        public bool IsRoadBlocked()
        {
            List<Cell> cells = new List<Cell>();
            Scenario.Cur.Map.GetRing(x, y, BuildingType.radius + 1, cells);
            for (int i = 0; i < cells.Count; i++)
            {
                Cell cell = cells[i];
                if (cell == null) continue;
                // 没有部队(空地或仅有建筑) → 可以出兵
                if (cell.troop == null) return false;
                // 非敌方部队(友军 / 中立) → 可以出兵
                if (!cell.troop.IsEnemy(this)) return false;
            }
            // 四周一圈全部被敌方部队占据,才算真正无法出兵
            return true;
        }

        public virtual bool ChangeDurability(int num, SangoObject atk, bool showDamage = true)
        {
            // 【统一解析】把攻击来源归一成部队：普攻 / 建筑反击 / 陷阱传的是 Troop，
            // 战法（含普攻经 Skill 结算）传的是 SkillInstance，施法部队在其 master 上。
            // 早先各处写 `atk is Troop`，导致战法破城 / 破设施时整条判定链被跳过。
            Troop atkTroop = atk?.AsTroop();

            if (showDamage)
            {
                if (Render != null)
                    Render.ShowInfo(num, (int)InfoType.Durability);
            }

            if (num < 0)
            {
                // 【新增】记录攻击本势力城池 / 建筑的敌方部队,供 AI 主动驱逐
                if (atkTroop != null && atkTroop.IsAlive && !atkTroop.IsSameForce(this))
                {
                    BelongForce?.MarkThreatTroop(atkTroop);
                }

                // 【命中即爆】陷阱类设施被攻击命中的瞬间就引爆，并由下面的通用流程销毁。
                // 对齐 Open311：火球 / 落石在被攻击命中时立即结算范围伤害并自毁。
                //
                // 返回 true 时**跳过**常规耐久累加：否则 OnHit 里已置 0 的 durability
                // 又被加一次 num，中间值变成负数（日志/UI 会显示 -500 之类的脏值）。
                if (OnHit(atk))
                {
                    durability = 0;
                    // 直接进入下方的"耐久 <= 0 → 销毁"判定
                }
                else
                {
                    if (Render != null && Render.IsVisible())
                    {
                        // 部队打出来的命中音效；战法同样算"部队打的"，用解析后的 atkTroop 判断
                        if (atkTroop != null)
                        {
                            GameMedia.Instance.PlaySfx(93);
                        }
                    }

                    durability = durability + num;
                }
            }
            else
            {
                durability = durability + num;
            }

            // 火焰不能破城
            if (num < 0 && IsCityBase() && atk != null && atk.ObjectType == SangoObjectType.Fire)
                durability = Math.Max(durability, 1);

            if (num > 0 && durability >= DurabilityLimit)
            {
                durability = DurabilityLimit;
                if (!isComplate)
                {
                    isComplate = true;
                    OnComplate(atk);
                    OnCompleted();
                }
            }
            else
            {
                bool isAlive = durability > 0;
                if (!isAlive)
                {
                    durability = 0;
                    Render?.UpdateRender();

                    // 【关系值：设施被摧毁】压到 15 地板
                    // 对齐 s11_sys_scen_building.cpp:372-382（与城池陷落、部队击破同构）。
                    // 城池（City）不在这里处理 —— 它自己的 `OnFall` 已扣过一次，
                    // 重复调用会把"压到 15"变成"再 -1"，语义错位。
                    // 战法摧毁设施同样要扣敌意值，所以这里用解析后的 atkTroop 而不是原始 atk
                    if (atkTroop != null && !(this is City))
                        DiplomacyRules.ApplyHostilityPenalty(atkTroop.BelongForce, BelongForce);

                    OnFall(atk);
                    return true;
                }
            }
            Render?.UpdateRender();
            return false;
        }

        public virtual void OnFall(SangoObject atk)
        {

        }
        public virtual void OnComplate(SangoObject atk)
        {

        }

        /// <summary>
        /// 被攻击命中时的钩子（耐久扣减之前调用）。
        ///
        /// 返回 true 表示本设施已在钩子内自行销毁，<see cref="ChangeDurability"/>
        /// 会立即返回 true 且不再结算耐久。陷阱（火球 / 落石）靠这个实现"命中即爆"。
        /// </summary>
        /// <param name="atk">攻击者</param>
        /// <returns>是否已自行销毁</returns>
        public virtual bool OnHit(SangoObject atk)
        {
            return false;
        }

        /// <summary>
        /// 建造完成状态翻到 true 之后的统一收尾钩子。
        ///
        /// 与 <see cref="OnComplate"/> 的分工：
        ///   · OnComplate 处理"建造者拿到功绩/技巧点"这类带上下文的结算；
        ///   · 本方法处理"设施完成后的归属 / 状态归一"，不依赖建造者是谁。
        ///
        /// 对应 Open311 的 Building::set_completed 里的副作用
        /// （陷阱 / 障碍建成后清 owner_force）。放在钩子里是因为它有两条触发路径：
        /// 耐久被打满（ChangeDurability）与建造进度推进（Building.Attach）。
        /// </summary>
        public virtual void OnCompleted()
        {

        }

        /// <summary>
        /// 执行建筑行为
        /// </summary>
        /// <param name="scenario"></param>
        public virtual bool DoBuildingBehaviour(Scenario scenario)
        {
            return true;
        }


        //public virtual int GetFoodHarvest(Cell cell)
        //{
        //    return (int)((cell.TerrainType.foodDeposit + BuildingType.foodGain) * cell.Fertility);
        //}
        //public virtual int GetGoldHarvest(Cell cell)
        //{
        //    return (int)((cell.TerrainType.goldDeposit + BuildingType.goldGain) * cell.Prosperity);
        //}

        public virtual int GetAttack() { return BuildingType.atk; }
        public virtual int GetAttackBack() { return BuildingType.atkBack; }
        public virtual int GetDefence() { return 50; }
        public float GetAttackBackFactor(SkillInstance skill, int distance)
        {
            if (skill.IsRange() && skill.IsNormal() && distance > 1)
                return 0.7f;
            else if (!skill.IsRange() && distance == 1)
                return 0.9f;
            return 0;
        }

        public virtual int GetSkillMethodAvaliabledTroops()
        {
            return DurabilityLimit;
        }

        public BuildingType GetBuiildingKindType()
        {
            if (BuildingType.Id == BuildingType.kind)
                return BuildingType;
            else
                return Scenario.Cur.GetObject<BuildingType>(BuildingType.kind);
        }

        public override bool OnForceTurnStart(Scenario scenario)
        {
            // 原版只有阵/砦/城塞/箭楼/连弩楼/投石台会主动攻击部队（calc_troops_damage 表）。
            //
            // 【本项目扩展：据点也自主攻击】城池 / 关所 / 港口按 attackMaxRange 射击范围内的敌军，
            // 这是 0.0.75「城池,港,关,阵增加攻击效果」引入的玩法（原版没有，属自建设计）。
            // 对齐原版时判定被收窄成 troopAtk > 0，而据点的 troopAtk 数据里恒为 0，
            // 于是这条行为被整体关掉 —— 这里把据点重新纳入，伤害见
            // CombatFormula.CalcBuildingTroopsDamage 的据点分支。
            bool isKyoten = this is City;
            bool canAutoAttack = BuildingType.troopAtk > 0 || isKyoten;

            if (isComplate && canAutoAttack && BuildingType.attackMaxRange > 0)
            {
                for (int i = 1; i < effectCells.Count; i++)
                {
                    Cell cell = effectCells[i];
                    // 【必须有势力】无主据点（城陷后中立化 / 势力灭亡清场）不攻击任何人：
                    // SangoObject.IsEnemy 对 null 势力返回 true，不拦的话无主城会朝周围所有部队开火。
                    if (cell.troop != null && BelongForce != null && IsEnemy(cell.troop))
                    {
                        BuildingAttackEvent @event = RenderEvent.Instance.Create<BuildingAttackEvent>();
                        @event.Init(this, cell);
                        RenderEvent.Instance.Add(@event);
                    }
                }
            }

            return base.OnForceTurnStart(scenario);
        }
    }
}
