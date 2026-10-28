using System.IO;
using Newtonsoft.Json;
using System.Xml;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class BuildingType : SangoObjectExtensionData
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
        /// **建筑主动攻击部队**的基础攻击值。对应原版 calc_troops_damage(Building*, Unit*)
        /// 里按设施类型的 <c>atk</c>（s11_sys_damage.cpp:1446-1472）：
        ///   阵 400 / 砦 600 / 城塞 800 / 箭楼 300 / 连弩楼 500 / 投石台 900
        /// 其余设施（含城/关/港）原版不在表里 → **0 = 不主动攻击**。
        /// </summary>
        [JsonProperty] public int troopAtk;

        /// <summary>
        /// 建筑打部队的**随机浮动上限**（原版 <c>bonus</c>）：
        /// 最终伤害 = 固定部分 + rand(0, troopAtkBonus)。
        /// 阵/砦/城塞/箭楼 300、连弩楼 400、投石台 600。
        /// </summary>
        [JsonProperty] public int troopAtkBonus;
        /// <summary>
        /// **攻击范围**下限（含），默认 1。配合 <see cref="attackMaxRange"/> 使用。
        ///
        /// 【原版出处】**不是** Facility 表里的字段，而是 building_attack_anim 的硬编码参数
        /// （s11_game.cpp:859-870）：
        ///     箭楼   building_attack_anim(building, 1, 2)
        ///     连弩楼 building_attack_anim(building, 1, 3)
        ///     投石台 building_attack_anim(building, 2, 3)   ← 不能打相邻格
        ///
        /// 所以攻击范围与 <see cref="minRange"/> / <see cref="maxRange"/>（阵地评分用）是**两套值**。
        /// </summary>
        [JsonProperty] public int attackMinRange = 1;

        /// <summary>
        /// **攻击范围**上限（含）。**0 表示该设施不攻击**（内政设施、障碍物等）。
        ///
        /// 箭楼 2 / 连弩楼 3 / 投石台 3。
        /// </summary>
        [JsonProperty] public int attackMaxRange;

        /// <summary>
        /// **AI 阵地评分 / 削弱范围**下限（含），默认 1 = 不含自身格。
        ///
        /// 【原版出处】Facility 表的 <c>min_range_</c>（s11_scen.h:2036）：
        ///   · 阵地评分 get_pos_around_facilities_score 的距离下限（s11_sys_unit_action.cpp:1485）
        ///   · 阵/砦/城塞 对周围部队的削弱判定也用它（配合 maxRange）
        ///
        /// 与攻击范围（<see cref="attackMinRange"/>）无关。
        /// </summary>
        [JsonProperty] public int minRange = 1;

        /// <summary>
        /// **AI 阵地评分 / 削弱范围**上限（含），默认 3。
        ///
        /// 【原版出处】Facility 表的 <c>max_range_</c>（s11_scen.h:2037），
        /// 用于阵地评分上限、建筑打部队的 debuff 判定（s11_sys_damage.cpp:318）、
        /// 以及"范围内异常状态每回合 -2"（s11_game.cpp:834）。
        /// </summary>
        [JsonProperty] public int maxRange = 3;

        /// <summary>
        /// 支援攻击削弱范围（Facility 表的 <c>range_</c>，s11_scen.h:2035 注释即"지원 공격 디버프 범위"）。
        ///
        /// 部队打部队时判定"防守方是否在敌方阵/砦/城塞的削弱范围内"
        /// （s11_sys_damage.cpp:665-671）。0 = 不提供削弱。
        /// </summary>
        [JsonProperty] public int debuffRange;

        /// <summary>
        /// 处于本设施削弱范围内的**敌方部队**受到的兵力伤害系数（百分比，100 = 不削弱）。
        ///
        /// 【原版出处】s11_sys_damage.cpp:792-798：
        ///     阵（Camp）    × 0.85  → 85
        ///     砦（Outpost） × 0.75  → 75
        ///     城塞（Fort）  × 0.65  → 65
        /// 与"给己方加防"（BuildingImproveTroopDefence）是两回事：
        /// 那是己方增益，这是**敌方在阵地范围内被压制**。
        /// </summary>
        [JsonProperty] public int enemyDamageFactor = 100;

        /// <summary>
        /// 被伤害倍率
        /// </summary>
        [JsonProperty] public float damageBounds;

        /// <summary>
        /// AI 位置评分的设施分类（对应原版 get_pos_around_facilities_score 的 k 值，
        /// s11_sys_unit_action.cpp:1487-1527）。**代码不认设施 Id，只认这个档位。**
        ///
        ///   0 = 不计分（火种 / 火球 / 陷阱 / 障碍 / 内政设施，原版同样不计入评分）
        ///   1 = 据点：城 / 关 / 港 / 小城（原版 k=0）
        ///   2 = 阵 / 砦 / 城塞（原版 k=1）
        ///   3 = 箭楼 / 连弩楼（原版 k=2）
        ///   4 = 投石台（原版 k=3）
        ///   5 = 太鼓台（原版 k=4）
        ///   6 = 军乐台（原版 k=5）
        ///   7 = 石兵（原版 k=6）
        ///
        /// 原版对"敌方设施"是 k+7，本项目改为按建筑所属势力实时判断，效果等价。
        /// 分值见 AIConfig.o311Pos*。
        /// </summary>
        [JsonProperty] public int posScoreKind;

        /// <summary>
        /// 火计火势系数（对应原版 get_fire_facility_coef，s11_sys_anim_unit_strategy.cpp:493-562）。
        ///
        /// 原版分组：
        ///   **10** 城 / 关 / 城塞 / 石墙 / 石兵 / 鸟落台
        ///   **30** 港 / 连弩楼 / 投石台 / 太鼓台 / 军乐台 / 根据地 / 遗迹 / 庙 / 农场系 / 工房 / 造船厂 / 造币厂 / 谷仓 / 军屯农
        ///   **35** 阵                        ← 最高（营地最易燃）
        ///   **25** 砦
        ///   **40** 箭楼 / 市场系 / 兵舍系 / 锻冶厂系 / 厩舍系 / 各府
        ///   **20** 土垒 / 堤防
        ///   **100** 火种 / 火焰种 / 火球 / 火焰球 / 火船 / 业火球 / 业火种（已是火 → 必中）
        ///
        /// 本项目按建筑逐个配置（**不能按 kind 查表**：阵/砦/城塞三者 kind 都是 4，
        /// 但原版系数分别是 35 / 25 / 10）。
        /// </summary>
        [JsonProperty] public int fireCoef;

        /// <summary>
        /// 效果实体集合
        /// </summary>
        [JsonProperty]
        public Newtonsoft.Json.Linq.JArray actionEntities;

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
                // 【陷阱的地形约束】火船只能建在水面，落石只能建在陆地。
                // 若不拦，玩家花钱建出来的火船在岸上永远不会引爆
                //（BuildingTrapEffect 会判"不在水面"直接返回），等于造了个废设施。
                bool isWaterTrap = IsExplosive && kind == (int)BuildingKindType.FireShip;

                if (isWaterTrap)
                {
                    // 水域的 terrain.canBuild 通常为 false（不允许常规军事设施），
                    // 因此火船跳过该检查，只要求"水面 + 空格"
                    if (!cell.IsWater || cell.building != null || cell.troop != null)
                        return false;
                }
                else
                {
                    if (!cell.CanBuild)
                        return false;

                    // 落石需要陆地
                    if (IsExplosive && kind == (int)BuildingKindType.Rockfall && cell.IsWater)
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
                    // 可以紧挨内政建筑
                    int buildSpace = Scenario.Cur.Variables.BuildingSpace;
                    if (cell.SpiralHasBuilding(buildSpace, (b) =>
                    {
                        return !b.BuildingType.IsObstacle && !b.BuildingType.IsIntrior;
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

        /// <summary>
        /// 该建筑能否作为**内政设施**建在指定城市（AI 与玩家同一口径）。
        ///
        /// 过滤条件与玩家侧可建列表逐条一致（见 <c>CityBuildBuilding.InitCanBuildingTypes</c>）：
        ///   · <see cref="IsIntrior"/> —— 只取内政设施（majorType = Interior）；
        ///   · <c>level == 1</c> —— 只取一级形态：升级是沿 <see cref="nextId"/> 推进的，
        ///     二级/三级形态不应出现在"新建"候选里；
        ///   · <see cref="canBuild"/> —— 逐建筑的可建开关。**内政方式**（工作制 / 经典内政）
        ///     正是靠它翻转 军事府(42) / 人才府(43) 来开关的
        ///     （见 <c>BuildingWorking.ScenarioInit</c> / <c>ScenarioClear</c>），
        ///     所以读它即可自动适配当前模式，无需另行判断内政方式；
        ///   · <see cref="IsValid(City)"/> —— 科技前置（needTech）与数量限制（limitType / buildNumLimit）。
        /// </summary>
        /// <param name="city">目标城市</param>
        /// <returns>可建返回 true</returns>
        public bool CanBuildInteriorAt(City city)
        {
            if (city == null)
                return false;

            return IsIntrior && level == 1 && canBuild && IsValid(city);
        }

    }
}
