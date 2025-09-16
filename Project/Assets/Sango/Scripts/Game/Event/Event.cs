using Sango.Game.Card;
using Sango.Game;
using System.Collections;
using System.Collections.Generic;

namespace Sango.Game
{
    public class Event : EventBase
    {
        public static EventDelegate<int, int> OnGameStateEnter;
        public static EventDelegate<int, int> OnGameStateExit;
       
        
        /// <summary>
        /// 剧本加载开始
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioLoadStart;
        /// <summary>
        /// 剧本加载结束
        /// </summary>
        public static EventDelegate<Scenario> OnScenarioLoadEnd;
        public static EventDelegate<Scenario> OnWorldLoadStart;
        public static EventDelegate<Scenario> OnWorldLoadEnd;
        public static EventDelegate<Scenario> OnScenarioStart;
        public static EventDelegate<Scenario> OnScenarioPrepare;
        public static EventDelegate<Scenario> OnScenarioEnd;
        public static EventDelegate<Scenario, float> OnScenarioTick;

        public static EventDelegate<string, Window.WindowInterface> OnWindowCreate;

        public static CoEventDelegate<Scenario, TroopsData[]> OnSelectBattle;


    }
}
