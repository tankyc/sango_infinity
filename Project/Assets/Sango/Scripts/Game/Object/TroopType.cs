using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 器械定位。对应原版攻城兵器的两组专属分支，**具体哪些兵种属于哪一组由 TroopTypes.json 的
    /// siegeRole 配置决定，代码里不写死任何兵种 Id**。
    /// </summary>
    public enum SiegeRoleType
    {
        /// <summary>普通兵种：守军走"兵力×75% + 城/关/港倍率"，耐久走通用公式 + 建筑类型倍率</summary>
        None = 0,
        /// <summary>冲车型（原版 EquipmentId_Shousha / EquipmentId_Mokujuu）：耐久走冲车公式并跳过建筑类型倍率</summary>
        Ram = 1,
        /// <summary>井阑型（原版 EquipmentId_Seiran / EquipmentId_Touseki）：守军吃完整兵力并乘配置倍率，不乘城/关/港倍率</summary>
        Artillery = 2,
    }

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
        /// 耐久伤害微调（百分比，100 = 原版数值）。
        ///
        /// 【用途】只调"拆建筑有多快"，不改变公式形状、不影响打士兵。
        ///
        /// 【重要】原版耐久公式（calc_hp_damage，D:\open311\s11\s11_sys_damage.cpp:1531-1553）
        /// 里**没有**任何兵种系数，兵装差异只通过 Attack 体现，所以 **100 才是"对齐原版"的值**。
        /// 填 30 / 50 这类值属于有意偏离原版的平衡调整（历史上剑兵/运输曾配 30、井阑配 50，
        /// 已统一回 100），改动前请确认不是误改。
        /// </summary>
        [JsonProperty] public int durabilityDmg = 100;

        /// <summary>
        /// 士兵伤害微调（百分比，100 = 原版数值）。
        ///
        /// 【用途】只调"打部队 / 打据点守军有多疼"，不影响建筑耐久拆除速度。
        /// 会对**两条兵力伤害通道**各乘一次：部队→部队、部队→据点守军。
        ///
        /// 【性质】纯平衡旋钮，原版没有对应项；默认 100 时与 Open311 公式逐位一致。
        /// 与 <see cref="siegeRole"/> 的 buildingTroopDamageFactor 的区别：
        ///   · buildingTroopDamageFactor 是**原版对齐值**（井阑 180 / 投石 120），改它 = 偏离原版；
        ///   · roleDmg 是**调参值**，改它 = 有意调平衡。
        /// </summary>
        [JsonProperty] public int roleDmg = 100;

        /// <summary>
        /// 器械定位（0=普通 / 1=冲车型 / 2=井阑型），见 <see cref="SiegeRoleType"/>。
        ///
        /// 【原版依据】
        ///   · 1 冲车型：耐久改走 calc_shousha_hp_damage，且**跳过**建筑类型耐久倍率
        ///     （D:\open311\s11\s11_sys_damage.cpp:553-558、833-834，对应原版冲车/木兽）；
        ///   · 2 井阑型：打据点守军改走"完整兵力 × 倍率"，且**不乘**城/关/港倍率
        ///     （同文件 442-465，对应原版井阑/投石）。
        /// 默认 0 = 普通兵种，行为与改造前完全一致；具体兵种取值由 TroopTypes.json 配置。
        /// </summary>
        [JsonProperty] public int siegeRole;

        /// <summary>
        /// 对据点守军的伤害倍率（百分比，100 = 原样）。
        /// 对应原版 ×1.8（井阑）/ ×1.2（投石），仅 <see cref="siegeRole"/> == 2 时生效。
        /// </summary>
        [JsonProperty] public int buildingTroopDamageFactor = 100;

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

            Force force = checkPersonList[0].BelongForce;

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
            if (city.BelongForce.TechniquePoint < costTechPoint * troops) return false;

            return city.itemStore.CheckItemEnough(costItems, troops);
        }

        public void Cost(City city, int troops)
        {
            city.food -= costFood * troops;
            city.gold -= costGold * troops;
            city.troopPopulation -= costPopulation * troops;
            city.BelongForce.GainTechniquePoint(-costTechPoint * troops);
            city.itemStore.Cost(costItems, troops);
        }

        /// <summary>
        /// 是否剑兵(kind == 1)。
        /// 剑兵物理孱弱且只带"近战普攻"，AI 在技能判定时会倾向于用计略弥补战力短板。
        /// </summary>
        /// <returns>true = 剑兵</returns>
        public bool IsSword()
        {
            return kind == 1;
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
