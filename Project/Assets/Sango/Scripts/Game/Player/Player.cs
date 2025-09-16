using Sango.Game.Card;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sango.Game
{
    public class Player
    {
        public Scenario scenario { get; private set; }
        public Player(Scenario scenario)
        { 
            this.scenario = scenario;
        }

        public int food;
        public int gold;
        public int horse;
        public int swords;
        public int bow;
        public int troops;
        public int morale;


        /// <summary>
        /// 商业
        /// </summary>
        public int business;
        /// <summary>
        /// 农业
        /// </summary>
        public int farming;
        /// <summary>
        /// 人口
        /// </summary>
        public int population;
        /// <summary>
        /// 繁荣
        /// </summary>
        public int prosperity;
        /// <summary>
        /// 科技
        /// </summary>
        public int technology;



        public List<CardBase> cards = new List<CardBase>();
        public List<PersonCard> heroes = new List<PersonCard>();

        public int add_food;
        public int add_gold;

        public int add_troops;

        public int decrease_food;
        public int decrease_gold;

        public void RandomLoseCard(int cardType, int num)
        {
            List<CardBase> list = cards.FindAll(x => x.cardType == cardType);
            if (list != null && list.Count > 0)
            {
                if (list.Count > num)
                {
                    int c = list.Count - num;
                    for (int i = 0; i < c; i++)
                    {
                        list.RemoveAt(UnityEngine.Random.Range(0, list.Count - 1));
                    }
                }
            }

            foreach (CardBase card in list)
            {
                cards.Remove(card);
                card.OnLost(scenario);
            }
        }

        public void OnMonthUpdate(Scenario scenario)
        {
            ScenarioInfo info = scenario.Info;
            gold += add_gold;
            troops += add_troops;
        }

        public void OnSeasonUpdate(Scenario scenario)
        {
            ScenarioInfo info = scenario.Info;
            SeasonType cur_season = GameDefine.SeasonInMonth[info.month];
            if(cur_season == SeasonType.Autumn || cur_season == SeasonType.Spring)
            {
                food += add_food;
            }
        }

        public void OnYearUpdate(Scenario scenario)
        {
        }
    }
}
