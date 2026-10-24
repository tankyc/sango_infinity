using Sango.UI;
using System.Collections.Generic;
using static Sango.Core.PersonSortFunction;

namespace Sango.Core.Player
{
    [GameSystem]
    public class CityTrade : CityBaseSystem
    {
        public int targetValue;

        public CityTrade()
        {
            customTitleName = "交易";
            customTitleList = new List<ObjectSortTitle>()
            {
                PersonSortFunction.SortByName,
                PersonSortFunction.SortByPolitics,
            };
            customMenuName = "都市/商人";
            customMenuOrder = 700;
            windowName = "window_city_trade";
        }

        public override bool IsValid
        {
            get
            {
                // 敌军兵临则禁止交易 —— 对齐原版 validate_trade_command 的 building_has_enemy
                if (TargetCity.HasEnemyNearby())
                    return false;
                return TargetCity.FreePersonCount > 0 &&
                    TargetCity.hasBusiness > 0 &&
                    TargetCity.CheckJobCost(CityJobType.TradeFood) &&
                    TargetCity.GetJobCounter((int)CityJobType.TradeFood) == 0 &&
                    TargetCity.BelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.TradeFood);
            }
        }

        /// <inheritdoc/>
        public override string GetInvalidReason(City city)
        {
            if (city != null)
                TargetCity = city;
            if (TargetCity.HasEnemyNearby())
                return "敌军兵临";
            if (TargetCity.FreePersonCount <= 0)
                return "无空闲武将";
            if (TargetCity.hasBusiness <= 0)
                return "城中无商人";
            if (!TargetCity.CheckJobCost(CityJobType.TradeFood))
                return "资金不足";
            if (TargetCity.GetJobCounter((int)CityJobType.TradeFood) != 0)
                return "本回合已交易过";
            if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.TradeFood))
                return "行动力不足";
            return null;
        }

        /// <summary>
        /// 打开商人窗口时，把当前粮价与灾害状态提示给玩家。
        ///
        /// 之所以走提示而不是窗口控件：<c>window_city_trade</c> 的 prefab 里没有预留这两行文本，
        /// 加控件需要改美术资源。消息提示不依赖 prefab，且商人窗口正是粮价最相关的场景
        /// （灾害会把粮价压到 30~40，丰收抬到 60~70）。
        /// </summary>
        public override void OnEnter()
        {
            base.OnEnter();

            if (TargetCity == null)
                return;

            string text = TargetCity.GetRateText();
            string disaster = TargetCity.GetDisasterText();
            if (!string.IsNullOrEmpty(disaster))
                text = text + "；" + disaster;

            PlayerMessage.AddTextMessage(text, TargetCity.BelongForce, TargetCity.x, TargetCity.y);
        }

        public override int CalculateWonderNumber()
        {
            if (personList.Count == 0)
                return 0;
            wonderNumber = GameUtility.Method_Trade(personList[0].Politics);
            return wonderNumber;
        }

        public override void RecommandPersonList()
        {
            personList.Clear();
            Person[] people = ForceAI.CounsellorRecommendTrade(TargetCity.freePersons);
            if (people != null)
            {
                for (int i = 0; i < people.Length; ++i)
                {
                    Person p = people[i];
                    if (p != null)
                        personList.Add(p);
                }
            }
        }

        public override void DoJob()
        {
            if (personList.Count <= 0)
                return;

            // 【失败必须可见】targetValue == 0 表示滑条没动（金粮数量相等），
            // 原版 exec 会走 validate 回传错误文案；这里原先直接 Done()，玩家点确认毫无反应。
            if (targetValue == 0)
            {
                PlayerMessage.AddTextMessage("交易数量为 0，请调整金粮比例后再确认。",
                    TargetCity.BelongForce, TargetCity.x, TargetCity.y);
                return;
            }

            if (!TargetCity.JobTradeFood(personList.ToArray(), targetValue))
            {
                PlayerMessage.AddTextMessage("交易未能执行：城中没有商人，或金/粮已达上限。",
                    TargetCity.BelongForce, TargetCity.x, TargetCity.y);
                return;
            }

            GameMedia.Instance.PlayDoAcitonSfx();
            Done();
        }
    }
}
