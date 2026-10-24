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
            // 【动态排序】启用时 AIResearch 已由 CityAIOrderPlanner 统一纳入排序,
            // 此处不再重复注入,避免同一命令被执行两次。
            if (AIConfig.Instance.useDynamicCityOrder)
                return;

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
            if (city.BelongForce.ResearchTechnique > 0) return true;
            if (city.BelongForce.TechniquePoint < 1000) return true;
            if (city.gold < 2000) return true;
            if (city.IsEnemiesRound(9))
                return true;

            Force force = city.BelongForce;
            for (int i = 0; i < force.canResearchTechniqueList.Count; i++)
            {
                Technique technique = force.canResearchTechniqueList[i];
                if (technique == null) continue;
                if (technique.goldCost <= city.gold && technique.techPointCost <= city.BelongForce.TechniquePoint)
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

            // 【人才府】研究工期 -2
            // 对齐原版 calc_research_tech_time_cost（s11_sys_person_order_city.cpp:1903-1927）：
            //   有人才府的城用 `1 + time_cost - stat`，没有则 `3 + time_cost - stat` —— 相差 2 回合。
            // 放在 isTest 之前，保证"界面预估工期"与"实际执行工期"一致。
            if (values != null && values.Length > 2
                && CityFacilityRequirement.HasGovFacility(city, InteriorFacilityKind.PersonnelOffice))
            {
                values[2] = System.Math.Max(1, values[2] - 2);
            }

            if (isTest)
            {
                return values;
            }
            int goldNeed = values[0];
            int tpNeed = values[1];
            int turnCount = values[2];
            if (city.gold < goldNeed || city.BelongForce.TechniquePoint < tpNeed)
            {
                return null;
            }

            city.gold -= goldNeed;
            city.BelongForce.GainTechniquePoint(-tpNeed);

            // 功绩 = 该技巧的科技点成本 / 2 —— 对齐 on_research_tech_result
            // （s11_sys_person_order_city.cpp:1802-1810：`kouseki = tech->get_tech_point_cost() / 2`）。
            // 原实现取 Data 表里写死的 1000，与技巧贵贱无关；改为按实际消耗折算。
            int meritGain = technique.techPointCost / 2;

#if SANGO_DEBUG
            StringBuilder stringBuilder = new StringBuilder();
#endif
            for (int i = 0; i < personList.Length; i++)
            {
                Person person = personList[i];
                if (person == null) continue;

                person.merit += meritGain;
                person.GainExp(meritGain);
                person.GainJobAttributeExp(jobId);          // 研究 → 智力经验
                person.SetMission(MissionType.PersonResearch, technique, turnCount);
                city.freePersons.Remove(person);
#if SANGO_DEBUG
                stringBuilder.Append(person.Name);
                stringBuilder.Append(",");
#endif
                person.ActionOver = true;
            }

            city.BelongForce.ResearchTechnique = technique.Id;
            city.BelongForce.ResearchLeftCounter = turnCount;

            city.BelongCorps.ReduceActionPoint(JobType.GetJobCostAP(jobId));

#if SANGO_DEBUG
            Sango.Log.Info($"@内政@[{city.BelongForce.Name}]{stringBuilder}在<{city.Name}>开始研究科技: [{technique.Name}], 研究需要{turnCount}回合!");
#endif

            city.ClearJobFeature();
            return null;
        }

        public bool IsValid
        {
            get
            {
                // 兵临城下禁止研究技巧 —— 对齐 can_research_tech_command
                // （s11_sys_person_order_city.cpp:1580-1590 的 building_has_enemy 判定）
                if (TargetCity.HasEnemyNearby())
                    return false;

                Force force = TargetCity.BelongForce;
                if (force == null || force.ResearchTechnique > 0)
                    return false;
                if (TargetCity.freePersons.Count <= 0)
                    return false;
                if (TargetCity.BelongCorps.ActionPoint < JobType.GetJobCostAP((int)CityJobType.Research))
                    return false;

                // 【必须至少有一个"负担得起"的技巧】
                // 原实现完全不查金与技巧点，按钮亮着可点，点进去选完技巧才在
                // `JobResearch` 里 `gold < goldNeed || TP < tpNeed` 静默返回 null —— 玩家毫无反馈。
                // 这里按"存在任一可负担项"放行，与执行侧口径一致。
                if (force.canResearchTechniqueList == null || force.canResearchTechniqueList.Count == 0)
                    return false;
                for (int i = 0; i < force.canResearchTechniqueList.Count; i++)
                {
                    Technique tech = force.canResearchTechniqueList[i];
                    if (tech == null)
                        continue;
                    if (TargetCity.gold >= tech.goldCost
                        && force.TechniquePoint >= tech.techPointCost)
                        return true;
                }
                return false;
            }
        }

        public void DoResearch()
        {
            if (TargetTechnique == null || !TargetTechnique.CanResearch(TargetCity.BelongForce))
                return;

            TechniqueResearch.JobResearch(TargetCity, personList.ToArray(), TargetTechnique, false);
            Done();
        }

        void OnCityContextMenuShow(IContextMenuData menuData, City city)
        {
            TargetCity = city;
            if (city.IsCityBase() && city.BelongForce != null && city.BelongForce.IsPlayer && city.BelongForce == Scenario.Cur.CurRunForce)
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
                            Back();
                        break;
                    }
            }
        }

    }
}
