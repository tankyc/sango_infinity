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
        public Force BelongForce;

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

        public virtual int DurabilityLimit => BuildingType.durabilityLimit;

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

        public bool IsCity()
        {
            return BuildingType.kind == (int)BuildingKindType.City || BuildingType.kind == (int)BuildingKindType.LittleCity;
        }
        public bool IsPort()
        {
            return BuildingType.kind == (int)BuildingKindType.Port;
        }
        public bool IsGate()
        {
            return BuildingType.kind == (int)BuildingKindType.Gate;
        }
        public bool IsIntorBuilding()
        {
            return BuildingType.IsIntrior;
        }

        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (BelongForceId > 0)
                BelongForce = scenario.forceSet.Get(BelongForceId);
            if (BelongCityId > 0)
                BelongCity = scenario.citySet.Get(BelongCityId);


            if (BelongCorpsId > 0)
                BelongCorps = scenario.corpsSet.Get(BelongCorpsId);

            effectCells = new List<Cell>();
            //BelongForceId = scenario.forceSet.Get(_belongForceId);
            //BelongCorpsId = scenario.corpsSet.Get(_belongCorpsId);
            //BuildingType = scenario.CommonData.BuildingTypes.Get(_buildingTypeId);
        }

        public override void OnScenarioSave(Scenario scenario)
        {
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
            if (showDamage)
            {
                if (Render != null)
                    Render.ShowInfo(num, (int)InfoType.Durability);
            }

            if (num < 0)
            {
                // 【新增】记录攻击本势力城池 / 建筑的敌方部队,供 AI 主动驱逐
                if (atk is Troop attacker && attacker.IsAlive && !attacker.IsSameForce(this))
                {
                    BelongForce?.MarkThreatTroop(attacker);
                }

                if (Render != null && Render.IsVisible())
                {
                    if (atk != null && atk.ObjectType == SangoObjectType.Troops)
                    {
                        GameMedia.Instance.PlaySfx(93);
                    }
                }
            }

            durability = durability + num;

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
                }
            }
            else
            {
                bool isAlive = durability > 0;
                if (!isAlive)
                {
                    durability = 0;
                    Render?.UpdateRender();
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
            // 暂时写死
            if (isComplate && BuildingType.atk > 0 && BuildingType.atkRange > 0)
            {
                for (int i = 1; i < effectCells.Count; i++)
                {
                    Cell cell = effectCells[i];
                    if (cell.troop != null && IsEnemy(cell.troop))
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
