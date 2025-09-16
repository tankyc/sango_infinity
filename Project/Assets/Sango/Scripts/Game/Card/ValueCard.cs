namespace Sango.Game.Card
{
    public class ValueCardBase : CardBase
    {
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
        /// <summary>
        /// 金
        /// </summary>
        public int gold;
        /// <summary>
        /// 粮食
        /// </summary>
        public int food;
        /// <summary>
        /// 士兵
        /// </summary>
        public int troops;
        /// <summary>
        /// 剑
        /// </summary>
        public int swords;
        /// <summary>
        /// 弓
        /// </summary>
        public int bow;
        /// <summary>
        /// 矛
        /// </summary>
        public int spear;
        /// <summary>
        /// 马
        /// </summary>
        public int horse;

        public override void OnGet(Scenario scenario)
        {
            base.OnGet(scenario);
            //scenario.Player.business += business * cardLevel;
            //scenario.Player.farming += farming * cardLevel;
            //scenario.Player.population += population * cardLevel;
            //scenario.Player.prosperity += prosperity * cardLevel;
            //scenario.Player.technology += technology * cardLevel;
            ////
            //scenario.Player.food += food * cardLevel;
            //scenario.Player.gold += gold * cardLevel;
            //scenario.Player.horse += horse * cardLevel;
            //scenario.Player.swords += swords * cardLevel;
            //scenario.Player.bow += bow * cardLevel;
            //scenario.Player.troops += troops * cardLevel;
            //scenario.Player.spear += spear * cardLevel;
        }

        public override void OnLost(Scenario scenario)
        {
            base.OnLost(scenario);
            //scenario.Player.business -= business * cardLevel;
            //scenario.Player.farming -= farming * cardLevel;
            //scenario.Player.population -= population * cardLevel;
            //scenario.Player.prosperity -= prosperity * cardLevel;
            //scenario.Player.technology -= technology * cardLevel;
            //scenario.Player.food -= food * cardLevel;
            //scenario.Player.gold -= gold * cardLevel;
            //scenario.Player.horse -= horse * cardLevel;
            //scenario.Player.swords -= swords * cardLevel;
            //scenario.Player.bow -= bow * cardLevel;
            //scenario.Player.troops -= troops * cardLevel;
            //scenario.Player.spear -= spear * cardLevel;
        }

        public override void OnLevelUP(Scenario scenario, byte destLevel)
        {
            OnLost(scenario);
            base.OnLevelUP(scenario, destLevel);
            OnGet(scenario);
        }
    }
}
