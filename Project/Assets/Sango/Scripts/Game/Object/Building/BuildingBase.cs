using TKNewtonsoft.Json;
using Sango.Render;
using System.Collections.Generic;

namespace Sango.Core
{

    public abstract class BuildingBase : SangoObject
    {
        public virtual string ColorName => $"<color=#93C86D>{Name}</color>";

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        [JsonProperty]
        public Force BelongForce;

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Corps>))]
        [JsonProperty]
        public Corps BelongCorps;

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<City>))]
        [JsonProperty]
        public City BelongCity;

        /// <summary>
        /// 建筑类型
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<BuildingType>))]
        [JsonProperty]
        public BuildingType BuildingType;

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
            return BuildingType.kind == (int)BuildingKindType.City;
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
            effectCells = new List<Cell>();
            //BelongForce = scenario.forceSet.Get(_belongForceId);
            //BelongCorps = scenario.corpsSet.Get(_belongCorpsId);
            //BuildingType = scenario.CommonData.BuildingTypes.Get(_buildingTypeId);
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
        public bool IsRoadBlocked()
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

        public virtual bool ChangeDurability(int num, SangoObject atk, bool showDamage = true)
        {
            if (showDamage)
            {
                if (Render != null)
                    Render.ShowInfo(num, (int)InfoType.Durability);
            }

            if (num < 0)
            {
                if (Render != null && Render.IsVisible())
                {
                    if (atk != null && atk.ObjectType == SangoObjectType.Troops)
                    {
                        GameMedia.Instance.PlaySfx(93);
                    }
                }
            }

            durability = durability + num;
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
