using Sango.UI;
using System;
using System.Collections.Generic;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTransport : CityBaseSystem
    {
        public CityTransport()
        {
            customTitleName = "运输";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByLevel,
                PersonSortFunction.SortByTroopsLimit,
                PersonSortFunction.SortByCommand,
                PersonSortFunction.SortByStrength,
                PersonSortFunction.SortByIntelligence,
                PersonSortFunction.SortByPolitics,
                PersonSortFunction.SortByGlamour,
                PersonSortFunction.SortBySpearLv,
                PersonSortFunction.SortByHalberdLv,
                PersonSortFunction.SortByCrossbowLv,
                PersonSortFunction.SortByRideLv,
                PersonSortFunction.SortByWaterLv,
                PersonSortFunction.SortByMachineLv,
                PersonSortFunction.SortByFeatureList,
            };
            customMenuName = "军事/运输";
            customMenuOrder = 1200;
            windowName = "window_city_create_transport";
        }

        public Troop TargetTroop { get; set; }

        protected override bool MenuCanShow()
        {
            return true;
        }

        public override bool IsValid
        {
            get
            {
                // 出阵位已满则不可组建运输队 —— 同出征，对齐 has_empty_place
                // （s11_sys_person_order_military.cpp:272-281）
                if (!TargetCity.HasEmptyCellForTroop())
                    return false;
                return TargetCity.troops > 0 && TargetCity.food > 0 && TargetCity.freePersons.Count > 0 &&
                    TargetCity.BelongCorps.ActionPoint >= TargetCity.GetJobCostAP(CityJobType.MakeTansport);
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (!TargetCity.HasEmptyCellForTroop())
                return "出阵位已满";
            if (TargetCity.troops <= 0)
                return "城中无兵";
            if (TargetCity.food <= 0)
                return "城中无粮";
            if (TargetCity.freePersons.Count <= 0)
                return "无空闲武将";
            // 与 IsValid 同源（运输本身无折扣建筑，但统一口径便于将来扩展）
            if (TargetCity.BelongCorps.ActionPoint < TargetCity.GetJobCostAP(CityJobType.MakeTansport))
                return "行动力不足";
            return null;
        }
        public override void UpdateJobValue()
        {
            if (personList.Count == 0) return;

            TargetTroop.Leader = personList[0];

            if (personList.Count > 1) TargetTroop.Member1 = personList[1];
            if (personList.Count > 2) TargetTroop.Member2 = personList[2];

            TargetTroop.CalculateAttribute(Scenario.Cur);
        }

        /// <summary>初始化运输部队，并在场景数据就绪后创建搬运特性排序列。</summary>
        public override void OnEnter()
        {
            // 特性标题依赖 Scenario.Cur，必须延迟到进入运输命令后再生成。
            customTitleList[1] = CreateTransportPersonSortTitle();
            personList.Clear();
            TargetTroop = new Troop();
            Scenario scenario = Scenario.Cur;
            // 默认主将与手动选择器均按搬运优先规则确定。
            Person[] persons = GetRecommendedTransportPersons();
            Person leader = persons.Length > 0 ? persons[0] : null;

            if (leader != null)
                personList.Add(leader);

            TargetTroop.morale = TargetCity.morale;
            TargetTroop.energy = TargetCity.energy;
            TargetTroop.Leader = leader;
            TargetTroop.Member1 = null;
            TargetTroop.Member2 = null;
            TargetTroop.LandTroopType = TroopType.GetTransportType(scenario, TargetCity.BelongForce);
            TargetTroop.WaterTroopType = scenario.GetObject<TroopType>(8);
            if (TargetTroop.troops == 0)
            {
                TargetTroop.troops = 1;
                TargetTroop.food = Math.Min(20, TargetCity.food);
            }
            // 【不要在这里派任务】玩家组建的运输队是纯手动部队：由玩家自己开到目标城，
            // 抵达后由 Troop.EnterCity 弹"返回原地 / 留在本城"。
            //
            // 以前这里写死 missionType = TroopTransformGoodsToCity，却没有（也无法）写 missionTarget ——
            // 于是 Troop.AIType 判成 Appoint，委任 AI 一进场就以 "TargetCity == null → 任务已完成" 收尾并
            // ClearMission，部队退化成无任务的散兵，入城询问也就再没触发过。
            // 势力 AI 的运输队走 CityAI / Troop 的 AI 部署分支派任务，与本入口无关。
            TargetTroop.CalculateAttribute(Scenario.Cur);
            Window.Instance.Open(windowName);
        }

        /// <summary>创建运输专用排序列：搬运优先，同组内按运输能力倒序。</summary>
        private static PersonSortFunction.SortTitle CreateTransportPersonSortTitle()
        {
            PersonSortFunction.SortTitle sortTitle = PersonSortFunction.GetSortByFeatrueId(8);
            // SortTitle 的标准比较器字段为 valueSortFunc，按搬运特性与运输能力排序。
            sortTitle.valueSortFunc = (a, b) =>
            {
                // 搬运特性提高输送部队移动力，应优先于基础运输能力。
                int featureCompare = b.HasFeatrue(8).CompareTo(a.HasFeatrue(8));
                return featureCompare != 0 ? featureCompare : b.MilitaryAbility.CompareTo(a.MilitaryAbility);
            };
            return sortTitle;
        }

        /// <summary>按运输选择器的排序规则推荐最多三名武将。</summary>
        private Person[] GetRecommendedTransportPersons()
        {
            List<Person> persons = new List<Person>(TargetCity.freePersons);
            // 复用展示排序，保证自动推荐与玩家手动选择的优先级一致。
            persons.Sort((a, b) => CreateTransportPersonSortTitle().Sort(a, b));
            if (persons.Count > 3) persons.RemoveRange(3, persons.Count - 3);
            return persons.ToArray();
        }

        public void MakeTroop()
        {
            // 【日志】原先三个前置判定都是静默 return，玩家点"确定"后没反应时无从判断原因。
            if (TargetTroop.troops <= 0)
            {
                Sango.Log.Warning($"*[运输]{TargetCity?.Name} 组建运输队取消：携带兵力为 0");
                return;
            }
            if (TargetTroop.food <= 0)
            {
                Sango.Log.Warning($"*[运输]{TargetCity?.Name} 组建运输队取消：携带军粮为 0");
                return;
            }
            if (personList.Count == 0)
            {
                Sango.Log.Warning($"*[运输]{TargetCity?.Name} 组建运输队取消：未选择押运武将");
                return;
            }

            ContextMenu.CloseAll();
            TargetTroop.ActionOver = false;
            TargetTroop.IsAlive = true;

            TargetCity.troops -= TargetTroop.troops;
            TargetCity.food -= TargetTroop.food;

            // 【经济守恒】同 CityExpedition.DoJob：资金走 AddGold（内含下限夹取），
            // 且"部队携带额 = 实际扣款额"。裸减法会把城内金扣成负数，
            // 负数在下次 AddGold 时被夹成 0（凭空抹账），回城再 AddGold(gold) 即净增。
            int payGold = TargetTroop.gold;
            if (payGold < 0) payGold = 0;
            if (payGold > TargetCity.gold) payGold = TargetCity.gold;
            TargetTroop.gold = payGold;
            TargetCity.AddGold(-payGold);

            TargetCity.itemStore.Remove(TargetTroop.itemStore);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Remove(person);
            });
            TargetCity.Render?.UpdateRender();
            TargetCity.EnsureTroop(TargetTroop, Scenario.Cur);
            TargetTroop.BelongCorps.ReduceActionPoint(JobType.GetJobCostAP((int)CityJobType.MakeTansport));
            Window.Instance.SetVisible(windowName, false);
            Sango.Log.Info($"*[运输]{TargetCity.Name} 玩家组建运输队「{TargetTroop.Name}」出城" +
                           $"（{TargetTroop.Leader?.Name} 押运，兵{TargetTroop.troops} 粮{TargetTroop.food} 金{TargetTroop.gold}）");
            GameSystem.GetSystem<TroopSystem>().Start(TargetTroop);
        }

        public override void OnBack(ICommandEvent whoGone)
        {
            base.OnBack(whoGone);
            if (whoGone is ObjectSelectSystem) return;
            Window.Instance.SetVisible(windowName, true);
            TargetTroop.BelongCorps.ReduceActionPoint(-JobType.GetJobCostAP((int)CityJobType.MakeTansport));
            // 取消组建的回滚：部队尚未出发，不做改道
            TargetTroop.EnterCity(TargetCity, false);
            TargetTroop.ForEachPerson(person =>
            {
                TargetCity.freePersons.Add(person);
                person.ActionOver = false;
            });
        }
    }
}
