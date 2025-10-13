using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;

namespace Sango.Game
{
    public class ScenarioEvent : EventBase
    {
        /// <summary>
        /// 剧本加载开始
        /// </summary>
        public EventDelegate<Scenario> OnLoadStart;
        /// <summary>
        /// 剧本加载结束
        /// </summary>
        public EventDelegate<Scenario> OnLoadEnd;
        public EventDelegate<Scenario> OnWorldLoadStart;
        public EventDelegate<Scenario> OnWorldLoadEnd;
        public EventDelegate<Scenario> OnStart;
        public EventDelegate<Scenario> OnEnd;
        public EventDelegate<Scenario, float> OnTick;
        public EventDelegate<Scenario> OnPrepare;

        /// <summary>
        /// 新天开始
        /// </summary>
        public EventDelegate<Scenario> OnDayUpdate;
        /// <summary>
        /// 新月开始
        /// </summary>
        public EventDelegate<Scenario> OnMonthUpdate;
        /// <summary>
        /// 新年开始
        /// </summary>
        public EventDelegate<Scenario> OnYearUpdate;
        /// <summary>
        /// 新季节开始
        /// </summary>
        public EventDelegate<Scenario> OnSeasonUpdate;
        /// <summary>
        /// 回合开始
        /// </summary>
        public EventDelegate<Scenario> OnTurnStart;
        /// <summary>
        /// 回合结束
        /// </summary>
        public EventDelegate<Scenario> OnTurnEnd;
        /// <summary>
        /// 势力逻辑开始
        /// </summary>
        public EventDelegate<Force, Scenario> OnForceStart;
        /// <summary>
        /// 势力逻辑结束
        /// </summary>
        public EventDelegate<Force, Scenario> OnForceEnd;
        /// <summary>
        /// 玩家控制势力
        /// </summary>
        public EventDelegate<Corps, Scenario> OnPlayerControl;
        /// <summary>
        /// 势力AI
        /// </summary>
        public EventDelegate<Force, Scenario> OnForceAIPrepare;
        public EventDelegate<Force, Scenario> OnForceAIStart;
        public EventDelegate<Force, Scenario> OnForceAIEnd;
        /// <summary>
        /// 城池AI
        /// </summary>
        public EventDelegate<City, Scenario> OnCityAIPrepare;
        public EventDelegate<City, Scenario> OnCityAIStart;
        public EventDelegate<City, Scenario> OnCityAIEnd;
        /// <summary>
        /// 部队AI
        /// </summary>
        public EventDelegate<Troop, Scenario> OnTroopAIStart;
        public EventDelegate<Troop, Scenario> OnTroopAIPrepare;
        public EventDelegate<Troop, Scenario> OnTroopAIEnd;
        public EventDelegate<Cell, Cell> OnTroopLeaveCell;
        public EventDelegate<Cell, Cell> OnTroopEnterCell;

        public EventDelegate<Troop, Scenario> OnTroopCreated;
        public EventDelegate<Troop, Scenario> OnTroopDestroyed;
        public EventDelegate<Troop, Scenario> OnTroopCalculateAttribute;

        public EventDelegate<City, Troop, Scenario> OnCityFall;

        /// <summary>
        /// 可监听改写工作花费
        /// City, JobType, PersonList, PersonCount, OverrideFunc
        /// </summary>
        public EventDelegate<City, int, List<Person>, int, Action<int>> OnCityCheckJobCost;
        /// <summary>
        /// 可监听改写工作成果
        /// City, JobType, PersonList, PersonCount, OverrideFunc
        /// </summary>
        public EventDelegate<City, int, List<Person>, int, Action<int>> OnCityJobResult;
        /// <summary>
        /// 可监听改写工作获取的技巧
        /// City, JobType, PersonList, PersonCount, OverrideFunc
        /// </summary>
        public EventDelegate<City, int, List<Person>, int, Action<int>> OnCityJobGainTechniquePoint;
        /// <summary>
        /// 可监听改写发现人才的几率
        /// City, JobType, PersonList, PersonCount, OverrideFunc
        /// </summary>
        public EventDelegate<City, int, List<Person>, int, Action<int>> OnCityJobSearchingWild;
        /// <summary>
        /// 可监听改写发现人才的几率
        /// City, JobType, PersonList, PersonCount, OverrideFunc
        /// </summary>
        public EventDelegate<City, Troop, Action<int>> OnTroopCalculateMaxTroops;
        /// <summary>
        /// 武将升级事件
        /// </summary>
        public EventDelegate<Person> OnPersonLevelUp;


        public static ScenarioEvent Event { get { return Scenario.Cur.Event; } }

    }
}
