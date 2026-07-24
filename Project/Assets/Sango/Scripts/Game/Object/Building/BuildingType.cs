using System.IO;
using TKNewtonsoft.Json;
using System.Xml;
using System.Collections.Generic;
using TKNewtonsoft.Json.Linq;
using Sango.Core.Action;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BuildingType : SangoObject
    {
        [JsonProperty] public string desc;
        [JsonProperty] public string limitDesc;
        [JsonProperty] public byte majorType;
        [JsonProperty] public byte kind;
        [JsonProperty] public int nextId;
        [JsonProperty] public int level;
        [JsonProperty] public string icon;
        [JsonProperty] public int durabilityLimit;
        [JsonProperty] public int buildNumLimit;
        [JsonProperty] public int goldGain;
        [JsonProperty] public int foodGain;
        [JsonProperty] public int emptyProduct;
        [JsonProperty] public int product;
        [JsonProperty] public int productCost;

        /// <summary>
        /// 限制类型 0无限制 1限制一座 2港口限制一座 3大都市限制一座 4全地图限制一座
        /// </summary>
        [JsonProperty] public int limitType;

        /// <summary>
        /// 工作类型
        /// </summary>
        [JsonProperty] public int jobId;


        [JsonProperty] public int populationGain;
        [JsonProperty] public int cost;
        [JsonProperty] public byte radius;
        //[JsonProperty] public bool IsIntrior;
        //[JsonProperty] public bool IsOutside;
        [JsonProperty] public string model;
        [JsonProperty] public string modelBroken;
        [JsonProperty] public string modelCreate;
        [JsonProperty] public bool canFire;
        [JsonProperty] public short techGain;
        [JsonProperty] public int needTech;
        [JsonProperty] public bool canBuild;

        [JsonProperty] public int workerLimit;
        [JsonProperty] public int[] productItems;


        /// <summary>
        /// 受哪种能力影响
        /// </summary>
        [JsonProperty] public int effectAttrType;

        /// <summary>
        /// 反击攻击力
        /// </summary>
        [JsonProperty] public int atkBack;
        /// <summary>
        /// 攻击力
        /// </summary>
        [JsonProperty] public int atk;
        /// <summary>
        /// 攻击范围
        /// </summary>
        [JsonProperty] public int atkRange;

        /// <summary>
        /// 被伤害倍率
        /// </summary>
        [JsonProperty] public float damageBounds;

        /// <summary>
        /// 效果实体集合
        /// </summary>
        [JsonProperty]
        public TKNewtonsoft.Json.Linq.JArray actionEntities;

        public void InitActions(List<ActionBase> list, params SangoObject[] sangoObjects)
        {
            if (actionEntities == null) return;
            for (int i = 0; i < actionEntities.Count; i++)
            {
                JObject valus = actionEntities[i] as JObject;
                ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                if (action != null)
                {
                    action.Init(valus, sangoObjects);
                    list.Add(action);
                }
            }
        }

        public bool CanBuildToHere(Cell cell)
        {
            if (cell.IsInterior)
                return IsIntrior;
            else
            {
                if (!cell.CanBuild)
                {
                    return false;
                }

                if (IsObstacle)
                {
                    // 障碍物不能建在港关城周围
                    int buildSpace = Scenario.Cur.Variables.BuildingSpace;
                    if (cell.SpiralHasBuilding(buildSpace, (b) =>
                    {
                        return b.BuildingType.majorType == 0;
                    }))
                    {
                        return false;
                    }
                }
                else
                {
                    // 只有军事建筑和爆炸物需要判断距离限制
                    int buildSpace = Scenario.Cur.Variables.BuildingSpace;
                    if (cell.SpiralHasBuilding(buildSpace, (b) =>
                    {
                        return !b.BuildingType.IsObstacle;
                    }))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public bool IsValid(Force force)
        {
            if (force == null) return false;
            if (needTech > 0)
                return force.HasTechnique(needTech);
            return true;
        }

        public bool IsValid(City city)
        {
            if (!IsValid(city.BelongForce))
                return false;

            /// 限制类型 0无限制 1限制一座 2港口限制一座 3大都市限制一座 4全地图限制一座
            switch (limitType)
            {
                case 1:
                    return city.GetBuildingNumber(kind) < 1;
                case 2:
                    return city.GetBuildingNumber(kind) < 1 && city.portList.Count > 0;
                case 3:
                    return city.GetBuildingNumber(kind) < 1 && city.CityLevelType.Id > 2;
                case 4:
                    return false;
            }
            return true;
        }

        public bool IsIntrior => majorType == (int)BuildingMajorType.Interior;
        public bool IsOutside => IsMilitary || IsExplosive || IsObstacle;
        public bool IsMilitary => majorType == (int)BuildingMajorType.Military;
        public bool IsExplosive => majorType == (int)BuildingMajorType.Explosive;
        public bool IsObstacle => majorType == (int)BuildingMajorType.Obstacle;

        public Cell GetBestPlace(City city)
        {
            return city.GetEmptyInteriorCell();
        }

    }
}
