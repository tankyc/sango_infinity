using Sango.Core.Player;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

using Sango.Core; namespace Sango.UI
{
    public class UICityTrade : UGUIWindow
    {
        public Text windiwTitle;

        public UIPersonItem targetPersonItems;
        public UIStatusItem targetStatusItem;
        public UITextField targetEffectValue;
        public UITextField targetGoldLabel;
        public UITextField targetFoodLabel;
        public UITextField targetPercent;
        public Slider tradeSlider;

        public UITextField action_value;

        City TargetCity;
        CityTrade currentSystem;
        // 滑条顶端对应的粮食总量（城内现有粮 + 用尽金币能买到的粮）
        double totalFood = 0;
        // 滑条当前位置：0 = 粮食清空（全部卖出），totalFood 对应滑条 1
        double currentP = 0;
        public Button sureButton;

        public override void OnOpen()
        {
            currentSystem = GameSystem.GetSystem<CityTrade>();
            TargetCity = currentSystem.TargetCity;
            windiwTitle.text = currentSystem.customTitleName;

            // 滑条范围按"结算公式"重算，界面显示的每一个数字都必须能真的成交
            CalcTradeRange();
            tradeSlider.SetValueWithoutNotify((float)currentP);

            action_value.text = $"{JobType.GetJobCostAP((int)CityJobType.TradeFood)}/{TargetCity.BelongCorps.ActionPoint}";
            UpdatePerson();
            UpdateContent();
        }

        /// <summary>
        /// 重建滑条取值范围：
        ///   0      = 把城里的粮食全部卖出；
        ///   顶端   = 城内现有粮 + 用尽金币（且放得进粮仓）能买到的粮；
        ///   初始值 = 现有粮食量，也就是"不交易"。
        ///
        /// 【为什么不用 hasBusiness 估算】hasBusiness 是商人兑换比例（剧本固定值），
        /// 与结算用的粮价 rate 不是同一个量。界面按它算会给出买不到的数量（显示 16 万、实际 12 万）。
        /// </summary>
        void CalcTradeRange()
        {
            Person[] persons = currentSystem.personList.ToArray();

            int foodRoom = System.Math.Max(TargetCity.foodLimit - TargetCity.food, 0);
            int maxBuyFood = TargetCity.PreviewTradeFood(persons, TargetCity.gold);
            if (maxBuyFood > foodRoom)
                maxBuyFood = foodRoom;
            if (maxBuyFood < 0)
                maxBuyFood = 0;

            totalFood = TargetCity.food + maxBuyFood;
            currentP = totalFood > 0 ? TargetCity.food / totalFood : 0;
        }

        void UpdatePerson()
        {
            if (currentSystem.personList.Count > 0)
            {
                Person person = currentSystem.personList[0];
                targetPersonItems.SetPerson(person);
                targetStatusItem.SetPerson(person);
            }
            else
            {
                targetStatusItem.SetPerson(null);
                targetPersonItems.SetPerson(null);
            }

            targetEffectValue.text = $"{currentSystem.wonderNumber}%";
            UpdatePercentText();
        }

        /// <summary>
        /// 刷新"1 金 : X 粮"的汇率文案。
        /// 按结算同一条公式（粮价 rate + 执行武将政治力）折算，
        /// 以前写的 <c>1 : hasBusiness</c> 是商人系数，与真实兑换比无关。
        /// </summary>
        void UpdatePercentText()
        {
            if (targetPercent == null)
                return;

            int politics = (currentSystem.personList.Count > 0 && currentSystem.personList[0] != null)
                ? currentSystem.personList[0].Politics : 50;

            // 用 100 金做样本，避免样本太小被整数取整放大误差
            const int sampleGold = 100;
            int sampleFood = City.CalcTradeFoodByGold(sampleGold, TargetCity.rate, politics);
            targetPercent.text = sampleFood > 0
                ? $"1 : {(float)sampleFood / sampleGold:0.0##}"
                : "无法交易";
        }

        public void UpdateContent()
        {
            Person[] persons = currentSystem.personList.ToArray();
            int currentFood = TargetCity.food;
            int currentGold = TargetCity.gold;

            int targetFood = (int)(totalFood * currentP);
            int delta = targetFood - currentFood;
            int showGold = currentGold;

            if (delta > 0)
            {
                // 买入：先把"目标粮食"反算成金币（向上取整），再用结算同一公式正算一次实际到手量。
                // 这样显示的就是成交结果（粮价、政治、性格效率、粮仓上限全部计入）。
                int politics = (persons.Length > 0 && persons[0] != null) ? persons[0].Politics : 50;
                int needGold = City.CalcTradeGoldNeedForFood(delta, TargetCity.rate, politics);
                // 滑条拉到最右时直接用光金币：反算有取整，差一两金会让玩家觉得"没买满"
                if (currentP >= 0.999)
                    needGold = currentGold;
                if (needGold > currentGold)
                    needGold = currentGold;
                if (needGold < 0)
                    needGold = 0;

                int realFood = needGold > 0 ? TargetCity.PreviewTradeFood(persons, needGold) : 0;
                if (realFood <= 0)
                {
                    // 一粒都买不到（粮仓已满 / 没有有效武将）时不能给下单金额，否则确认后会白扣钱
                    needGold = 0;
                    targetFood = currentFood;
                }
                else
                {
                    targetFood = currentFood + realFood;
                }

                showGold = currentGold - needGold;
                currentSystem.targetValue = needGold;
            }
            else if (delta < 0)
            {
                // 卖出：结算参数是"负数粮数"，金库满时按与结算相同的口径截断
                int sellFood = -delta;
                int gainGold = TargetCity.PreviewTradeGold(persons, sellFood);
                if (gainGold <= 0)
                {
                    // 换不到钱就不卖，避免"粮食被扣、金没加"
                    sellFood = 0;
                    targetFood = currentFood;
                }
                else
                {
                    targetFood = currentFood - sellFood;
                    showGold = currentGold + gainGold;
                }

                currentSystem.targetValue = -sellFood;
            }
            else
            {
                currentSystem.targetValue = 0;
            }

            targetGoldLabel.text = $"{currentGold}→{showGold}";
            targetFoodLabel.text = $"{currentFood}→{targetFood}";
        }

        public void OnSure()
        {
            currentSystem.DoJob();
        }

        public void OnCancel()
        {
            currentSystem.Exit();
        }

        public void OnSelectPerson()
        {
            GameSystem.GetSystem<PersonSelectSystem>().Start(currentSystem.TargetCity.freePersons,
               currentSystem.personList, 1, OnPersonChange, currentSystem.customTitleList, currentSystem.customTitleName);
        }

        public virtual void OnPersonChange(List<Person> personList)
        {
            currentSystem.personList = (personList);
            currentSystem.UpdateJobValue();
            // 换武将后政治力变了，汇率和可买量都要按新公式重算
            CalcTradeRange();
            tradeSlider.SetValueWithoutNotify((float)currentP);
            UpdatePerson();
            UpdateContent();
        }

        public void OnSelectFood()
        {

        }

        public void OnTradeSliderValueChanged(float p)
        {
            currentP = p;
            UpdateContent();
        }
    }
}
