using Sango.Game.Render;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Sango.Game
{
    public abstract class BuildingBase : SangoObject
    {
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
        /// <summary>
        /// 是否建造完成
        /// </summary>
        [JsonProperty] public bool isComplte;


        public virtual Cell CenterCell { get; set; }

        /// <summary>
        /// 渲染器
        /// </summary>
        public ObjectRender Render { get; set; }


        public List<Cell> effectCells = new List<Cell>();

        public override void OnScenarioPrepare(Scenario scenario)
        {
            //BelongForce = scenario.forceSet.Get(_belongForceId);
            //BelongCorps = scenario.corpsSet.Get(_belongCorpsId);
            //BuildingType = scenario.CommonData.BuildingTypes.Get(_buildingTypeId);
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

        public virtual bool ChangeDurability(int num, Troop atk)
        {
            durability = durability + num;
            bool isAlive = durability > 0;
            if (!isAlive)
            {
                durability = 0;
                OnFall(atk);
                return true;
            }
            return false;
        }

        public virtual void OnFall(Troop atk)
        {

        }

        //public virtual int GetFoodHarvest(Cell cell)
        //{
        //    return (int)((cell.TerrainType.foodDeposit + BuildingType.foodGain) * cell.Fertility);
        //}
        //public virtual int GetGoldHarvest(Cell cell)
        //{
        //    return (int)((cell.TerrainType.goldDeposit + BuildingType.goldGain) * cell.Prosperity);
        //}

        public virtual int GetBaseDamage() { return 0; }
        public virtual int GetBaseCommand() { return 0; }
        public float GetAttackBackFactor(Skill skill, int distance)
        {
            if (skill.IsRange() && distance > 1)
                return 0;
            else if (!skill.IsRange() && distance == 1)
                return 0.5f;
            return 0;
        }
    }
}
