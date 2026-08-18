using System.Collections.Generic;
using System.Text;

namespace Sango.Core
{
    /// <summary>
    /// 科技研究模块
    /// </summary>
    [GameSystem]
    public class TechniqueResearch : GameSystem
    {
        public City TargetCity { get; set; }
        public Cell TargetCell { get; set; }
        public IMapManageObject SelectBuildingObject { get; set; }
        public List<Cell> buildRangeCell = new List<Cell>();
        public List<BuildingType> canBuildBuildingType = new List<BuildingType>();

        public List<Person> personList = new List<Person>();
        public int wonderBuildCounter = 0;

        public string customTitleName = "开发";
        public List<ObjectSortTitle> customTitleList = new List<ObjectSortTitle>()
        {
            PersonSortFunction.SortByName,
            PersonSortFunction.SortByPolitics,
        };

        public Technique TargetTechnique { get; set; }
        public int goldCost;
        public int tpCost;
        public int counter;

        public override void Init()
        {
            GameEvent.OnForceTurnStart += OnForceTurnStart;
            GameEvent.OnCityAIPrepare += OnCityAIPrepare;
            GameEvent.OnCityContextMenuShow += OnCityContextMenuShow;
        }
        public override void Clear()
        {

            GameEvent.OnForceTurnStart -= OnForceTurnStart;
            GameEvent.OnCityAIPrepare -= OnCityAIPrepare;
            GameEvent.OnCityContextMenuShow -= OnCityContextMenuShow;

        }

        void OnForceTurnStart(Force force, Scenario scenario)
        {
            if (force.ResearchTechnique > 0)
            {
                force.ResearchLeftCounter--;
                if (force.ResearchLeftCounter <= 0)
                {
                    Technique technique = force.AddTechnique(force.ResearchTechnique);
                    force.ResearchTechnique = 0;

                    // 刷新部队属性
                    force.ForEachTroop(t => t.CalculateAttribute(scenario));

                    GameEvent.OnForceResearchComplete?.Invoke(force, technique);
                    if (force.IsPlayer)
                    {
                        Render.WindowEvent windowEvent = Render.RenderEvent.Instance.Create<Render.WindowEvent>();
                        windowEvent.Init("window_technique_complete", new object[] { technique });
                        Render.RenderEvent.Instance.Add(windowEvent);
                    }
                }
            }
        }

        void OnCityAIPrepare(City city, Scenario scenario)
        {
            city.AICommandList.Add(AIResearch);
        }

        /// <summary>
        /// AI进行研究的逻辑
        /// </summary>
        /// <param name="city"></param>
        /// <param name="scenario"></param>
        /// <returns></returns>
        public static bool AIResearch(City city, Scenario scenario)
        {
            if (!city.IsCity()) return true;
            if (city.IsPlayer) return true;
            if (city.freePersons.Count < 3) return true;
            if (city.mBelongForce.ResearchTechnique > 0) return true;
            if (city.mBelongForce.TechniquePoint < 1000) return true;
            if (city.gold < 2000) return true;
            if (city.IsEnemiesRound(9))
                return true;

            Force force = city.mBelongForce;
            for (int i = 0; i < force.canResearchTechniqueList.Count; i++)
            {
                Technique technique = force.canResearchTechniqueList[i];
                if (technique == null) continue;
                if (technique.goldCost <= city.gold && technique.techPointCost <= city.mBelongForce.TechniquePoint)
                {
                    Person[] ps = ForceAI.CounsellorRecommendResearch(city.freePersons, technique);
                    if (ps != null)
                    {
                        JobResearch(city, ps, technique, false);
                        break;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 研究结果
        /// </summary>
        /// <param name="personList"></param>
        /// <returns></returns>
        public static int[] JobResearch(City city, Person[] personList, Technique technique, bool isTest)
        {
            if (personList == null || personList.Length == 0 || technique == null) return null;
            Scenario scenario = Scenario.Cur;
            ScenarioVariables variables = scenario.Variables;
            int jobId = (int)CityJobType.Research;

            int[] values = technique.GetCost(personList, city);
            if (isTest)
            {
                return values;
            }
            int goldNeed = values[0];
            int tpNeed = values[1];
            int turnCount = values[2];
            if (city.gold < goldNeed || city.mBelongForce.TechniquePoint < tpNeed)
            {
                return null;
            }

            city.gold -= goldNeed;
            city.mBelongForce.GainTechniquePoint(-tpNeed);
            int meritGain = JobType.GetJobMeritGain(jobId);

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.SetMission(MissionType.PersonResearch, technique, turnCount);
                city.freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            city.mBelongForce.ResearchTechnique = technique.Id;
            city.mBelongForce.ResearchLeftCounter = turnCount;

            city.mBelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{city.mBelongForce.Name}]{stringBuilder}在<{city.Name}>开始研究科技: [{technique.Name}], 研究需要{turnCount}回合!");
#endif

            city.ClearJobFeature();
            return null;
        }

        public bool IsValid
        {
            get
            {
                return TargetCity.mBelongForce.ResearchTechnique <= 0 &&
                     TargetCity.freePersons.Count > 0 &&
                     TargetCity.mBelongCorps.ActionPoint >= JobType.GetJobCostAP((int)CityJobType.Research);
            }
        }

        public void DoResearch()
        {
            if (TargetTechnique == null || !TargetTechnique.CanResearch(TargetCity.mBelongForce))
                return;

            TechniqueResearch.JobResearch(TargetCity, personList.ToArray(), TargetTechnique, false);
            Done();
        }

        void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            TargetCity = city;
            if (city.IsCity() && city.mBelongForce != null && city.mBelongForce.IsPlayer && city.mBelongForce == Scenario.Cur.CurRunForce)
                menuData.Add("都市/研究技巧", 2000, city, OnClickMenuItem, IsValid);
        }

        void OnClickMenuItem(IContextMenuItem contextMenuItem)
        {
            TargetCity = contextMenuItem.CustomData as City;
            GameSystemManager.Instance.Push(this);
        }

        public override void OnEnter()
        {
            personList.Clear();
            Window.Instance.Open("window_technique");
        }

        public override void OnDestroy()
        {
            Window.Instance.Close("window_technique");
        }

        public void SelectTechnique(Technique tech)
        {
            TargetTechnique = tech;
            Person[] ps = ForceAI.CounsellorRecommendResearch(TargetCity.freePersons, TargetTechnique);
            if (ps != null)
            {
                personList.Clear();
                foreach (Person person in ps)
                {
                    personList.Add(person);
                }
            }
            UpdateJobValue();
        }

        public void UpdateJobValue()
        {
            if (personList.Count <= 0)
                return;

            int[] valus = TechniqueResearch.JobResearch(TargetCity, personList.ToArray(), TargetTechnique, true);
            if (valus == null) return;

            goldCost = valus[0];
            tpCost = valus[1];
            counter = valus[2];
        }

        public override void HandleEvent(CommandEventType eventType, Cell cell, UnityEngine.Vector3 clickPosition, bool isOverUI)
        {

            switch (eventType)
            {
                case CommandEventType.Cancel:
                case CommandEventType.RClick:
                    {
                        if (SelectBuildingObject == null)
                            GameSystemManager.Instance.Back();
                        break;
                    }
            }
        }

    }
}
