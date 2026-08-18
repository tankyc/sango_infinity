using System;
using System.Collections.Generic;
using TKNewtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class TroopType : SangoObject
    {
        /// <summary>
        /// 类型
        /// </summary>
        [JsonProperty] public int kind;

        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 图标
        /// </summary>
        [JsonProperty] public string icon;

        /// <summary>
        /// 模型
        /// </summary>
        [JsonProperty] public string model;

        /// <summary>
        /// 动画
        /// </summary>
        [JsonProperty] public int[] aniIds;

        /// <summary>
        /// 是否战斗
        /// </summary>
        [JsonProperty] public bool isFight;

        // <summary>
        /// 是否远程
        /// </summary>
        [JsonProperty] public bool isRange;

        /// <summary>
        /// 是否是单体(器械)
        /// </summary>
        [JsonProperty] public bool isSingle;

        /// <summary>
        /// 是否为陆地
        /// </summary>
        [JsonProperty] public bool isLand;

        /// <summary>
        /// 攻击力
        /// </summary>
        [JsonProperty] public int atk;

        /// <summary>
        /// 耐久破坏力
        /// </summary>
        [JsonProperty] public int durabilityDmg;

        /// <summary>
        /// 防御力
        /// </summary>
        [JsonProperty] public int def;

        /// <summary>
        /// 移动力
        /// </summary>
        [JsonProperty] public int move;

        /// <summary>
        /// 护甲类型
        /// </summary>
        [JsonProperty] public int defType;

        /// <summary>
        /// 攻击类型
        /// </summary>
        [JsonProperty] public int atkType;

        /// <summary>
        /// 战斗力
        /// </summary>
        [JsonProperty] public int fightPower;

        /// <summary>
        /// 挂钩能力
        /// </summary>
        [JsonProperty] public int influenceAbility;

        /// <summary>
        /// 初始技能
        /// </summary>
        [JsonProperty] public int[] skills;

        /// <summary>
        /// 组建1000人所消耗的道具(兵器,战马,船等) 
        /// </summary>
        [JsonProperty] public int[] costItems;

        /// <summary>
        /// 可组建必须的兵符道具Id
        /// </summary>
        [JsonProperty] public int validItemId;

        /// <summary>
        /// 可组建必须的科技Id
        /// </summary>
        [JsonProperty] public int validTechId;

        /// <summary>
        /// 对于每种地形的移动消耗值
        /// </summary>
        [JsonProperty] public int[] moveCost;

        /// <summary>
        /// 可组建条件(兵符,特定城市等)
        /// </summary>
        //[JsonProperty] public Condition.Condition activeCondition;

        /// <summary>
        /// 粮食消耗倍率
        /// </summary>
        [JsonProperty] public float foodCostFactor = 1;

        /// <summary>
        /// 招募每兵所需额外食物
        /// </summary>
        [JsonProperty] public int costFood = 0;

        /// <summary>
        /// 招募每兵所需额外金币
        /// </summary>
        [JsonProperty] public int costGold = 0;

        /// <summary>
        /// 招募每兵所需额外兵役人口
        /// </summary>
        [JsonProperty] public int costPopulation = 0;

        /// <summary>
        /// 招募每兵所需技巧点数/兵
        /// </summary>
        [JsonProperty] public int costTechPoint = 0;

        /// <summary>
        /// 招募每兵所需对应经验点数/兵
        /// </summary>
        [JsonProperty] public int costExpPoint = 0;

        /// <summary>
        /// 适配的特性
        /// </summary>
        [JsonProperty] public int[] matchFeatures;

        /// <summary>
        /// 伏兵计略暴击率加成
        /// </summary>
        [JsonProperty] public byte ambushCriticalAdd;

        /// <summary>
        /// 对于每种地形的防御加成值
        /// </summary>
        [JsonProperty] public float[] terrainDefenceBonus;

        /// <summary>
        /// 自身处于每种地形对战法成功率加成值
        /// </summary>
        [JsonProperty] public int[] terrainIncreaseSucecessBonus;

        /// <summary>
        /// 目标处于每种地形的对成功率的降低值
        /// </summary>
        [JsonProperty] public int[] terrainDecreaseSucecessBonus;

        /// <summary>
        /// 目标处于每种地形的对攻击力的加成
        /// </summary>
        [JsonProperty] public int[] terrainIncreaseAtkBonus;

        /// <summary>
        /// 目标处于每种地形的对防御力的加成
        /// </summary>
        [JsonProperty] public int[] terrainDecreaseDefenceBonus;

        /// <summary>
        /// 行军音效
        /// </summary>
        [JsonProperty] public int moveSound;

        public int MoveCost(Cell cell)
        {
#if SANGO_DEBUG
            if (cell.TerrainType == null || cell.TerrainType.Id < 0 || cell.TerrainType.Id >= moveCost.Length)
            {
                Sango.Log.Error($"地形移动数据不存在!! => {cell.TerrainType?.Id ?? -1}");
                return 99;
            }
#endif
            return moveCost[cell.TerrainType.Id];
        }

        public bool IsValid(Force force)
        {
            // 检查科技
            if (validTechId > 0 && !force.HasTechnique(validTechId))
                return false;
            else if (validTechId < 0 && force.HasTechnique(Math.Abs(validTechId)))
                return false;
            return true;
        }

        // 获取当前可以组建的兵种
        public static void CheckActivTroopTypeList(List<Person> checkPersonList, List<TroopType> activeTroopTypes)
        {
            if (checkPersonList == null || checkPersonList.Count == 0) return;

            Force force = checkPersonList[0].mBelongForce;

            // 确定可组建特殊兵种
            Scenario.Cur.CommonData.TroopTypes.ForEach(t =>
            {
                if (!t.isFight) return;

                if (!t.IsValid(force))
                    return;

                if (t.validItemId == 0)
                {
                    activeTroopTypes.Add(t);
                    return;
                }

                // 检查武将限定道具
                for (int j = 0; j < checkPersonList.Count; j++)
                {
                    Person person = checkPersonList[j];
                    if (person != null && person.HasItem(t.validItemId))
                    {
                        activeTroopTypes.Add(t);
                        return;
                    }
                }
            });
        }

        //// 获取满足消耗的可组建兵种
        //public static void GetCostEnoughTroopTypeList(List<Person> checkPersonList, ItemStore itemStore, List<TroopType> costEnoughTroopTypes, int compareCostNum = 1)
        //{
        //    List<TroopType> activeTroopTypes = new List<TroopType>();
        //    CheckActivTroopTypeList(checkPersonList, activeTroopTypes);
        //    for (int i = 0; i < activeTroopTypes.Count; i++)
        //    {
        //        TroopType t = activeTroopTypes[i];
        //        if (t.costItems == null || t.costItems.Count == 0)
        //        {
        //            costEnoughTroopTypes.Add(t);
        //            return;
        //        }

        //        // 检查消耗是否满足
        //        bool enough = true;
        //        for (int j = 0; j < t.costItems.Count; j += 2)
        //        {
        //            int itemTypeId = t.costItems[j];
        //            int itemNum = t.costItems[j + 1];
        //            if (itemStore.GetNumber(itemTypeId) < (itemNum * compareCostNum))
        //            {
        //                enough = false;
        //                break;
        //            }
        //        }

        //        if (enough)
        //            costEnoughTroopTypes.Add(t);
        //    }
        //}

        public static void GetCostEnoughTroopTypeList(City city, List<TroopType> costEnoughTroopTypes, int compareCostNum = 1)
        {
            List<TroopType> activeTroopTypes = new List<TroopType>();
            CheckActivTroopTypeList(city.freePersons, activeTroopTypes);
            for (int i = 0; i < activeTroopTypes.Count; i++)
            {
                TroopType t = activeTroopTypes[i];
                if (t.CheckCost(city, compareCostNum))
                {
                    costEnoughTroopTypes.Add(t);
                }
            }
        }

        public bool CheckCost(City city, int troops)
        {
            if (city.food < costFood * troops) return false;
            if (city.gold < costGold * troops) return false;
            if (city.troopPopulation < costPopulation * troops) return false;
            if (city.mBelongForce.TechniquePoint < costTechPoint * troops) return false;

            return city.itemStore.CheckItemEnough(costItems, troops);
        }

        public void Cost(City city, int troops)
        {
            city.food -= costFood * troops;
            city.gold -= costGold * troops;
            city.troopPopulation -= costPopulation * troops;
            city.mBelongForce.GainTechniquePoint(-costTechPoint * troops);
            city.itemStore.Cost(costItems, troops);
        }

        public bool IsTransport()
        {
            return kind == 7;
        }

        public bool IsMachine()
        {
            return kind == 8 || kind == 9;
        }
        public bool IsHelepolis()
        {
            return kind == 8;
        }

        public static TroopType GetTransportType(Scenario scenario, Force force)
        {
            for (int i = 1; i < scenario.CommonData.TroopTypes.Count; i++)
            {
                TroopType troopType = scenario.CommonData.TroopTypes[i];
                if (troopType != null && troopType.IsTransport() && troopType.IsValid(force))
                    return troopType;
            }

            return null;
        }

    }
}
