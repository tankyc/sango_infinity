using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 电脑控制城池每回合资源补充系统。
    /// </summary>
    [GameSystem(order = 101)]
    public class CityAIResourceAddSystem : GameSystem
    {
        /// <summary>缓存的兵器类道具类型</summary>
        List<ItemType> weaponItemTypes = new List<ItemType>();

        public override void Init()
        {
            GameEvent.OnCityTurnStart += OnCityTurnStart;
            GameEvent.OnScenarioLoadEnd += OnScenarioLoadEnd;
        }

        public override void Clear()
        {
            GameEvent.OnCityTurnStart -= OnCityTurnStart;
            GameEvent.OnScenarioLoadEnd -= OnScenarioLoadEnd;
            weaponItemTypes.Clear();
        }

        /// <summary>
        /// 城市回合开始时给电脑控制的城池补充资源。
        /// </summary>
        /// <param name="city">当前城市</param>
        /// <param name="scenario">当前剧本</param>
        void OnCityTurnStart(City city, Scenario scenario)
        {
            if (scenario == null || city == null)
                return;

            // 只给正式的城池补充；空城、港关、关卡不补充
            if (!city.IsCity())
                return;
            if (city.mBelongForce == null)
                return;
            if (city.IsPlayer)
                return;

            ScenarioVariables variables = scenario.Variables;
            if (variables == null)
                return;

            variables.GetAIAddValues(out int gold, out int food, out int troops, out int arms);

            if (gold != 0)
            {
                city.AddGold(gold);
                city.Render?.ShowInfo(gold, (int)InfoType.Gold);
            }
            if (food != 0)
            {
                city.AddFood(food);
                city.Render?.ShowInfo(food, (int)InfoType.Food);
            }
            if (troops != 0)
            {
                city.AddTroops(troops);
                city.Render?.ShowInfo(troops, (int)InfoType.Troop);
            }
            if (arms != 0)
            {
                AddArmsToCity(city, scenario, arms);
            }
        }

        /// <summary>
        /// 给城市添加兵装（兵器类 + 战马，不包含器械和船）。
        /// </summary>
        /// <param name="city">目标城市</param>
        /// <param name="scenario">当前剧本</param>
        /// <param name="arms">兵装数量</param>
        void AddArmsToCity(City city, Scenario scenario, int arms)
        {
            CacheWeaponItemTypes(scenario);
            foreach (ItemType itemType in weaponItemTypes)
            {
                if (itemType == null)
                    continue;
                city.itemStore.Add(itemType, arms);
            }
        }

        /// <summary>
        /// 缓存所有"可直接装备部队"的道具类型：兵器类（枪 / 戟 / 弩 / 剑）+ 战马。
        ///
        /// 注意：战马（<see cref="ItemKindType.Horse"/>）不属于 <see cref="ItemType.IsWeapon"/>，
        /// 必须单独纳入 —— 否则电脑城池每回合补兵装时永远补不到马，
        /// 导致其无法组建骑兵、也无法为运输队 / 骑兵补充装备。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void CacheWeaponItemTypes(Scenario scenario)
        {
            if (weaponItemTypes.Count > 0)
                return;
            if (scenario.CommonData?.ItemTypes == null)
                return;

            scenario.CommonData.ItemTypes.ForEach(item =>
            {
                if (item is ItemType itemType && (itemType.IsWeapon() || itemType.IsHorse()))
                    weaponItemTypes.Add(itemType);
            });
        }

        /// <summary>
        /// 剧本加载完成后清空缓存，避免跨剧本数据残留。
        /// </summary>
        /// <param name="scenario">新加载的剧本</param>
        void OnScenarioLoadEnd(Scenario scenario)
        {
            weaponItemTypes.Clear();
        }
    }
}
