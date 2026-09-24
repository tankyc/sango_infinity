using System.Collections.Generic;
using System.IO;
using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using TKNewtonsoft.Json.Serialization;
using Sango.Core.Action;
using Sango.Render;
using UnityEngine;
using System.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 势力类，继承自SangoObject，用于管理游戏中的势力对象
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Force : SangoObjectExtensionData
    {
        /// <summary>
        /// 获取势力的对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Force; } }

        /// <summary>
        /// 获取或设置AI是否完成
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// 获取或设置AI是否准备就绪
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 获取或设置是否为玩家势力
        /// </summary>
        public virtual bool IsPlayer { get; set; }

        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => IsPlayer && this == Scenario.Cur.CurRunForce;

        private bool isAlive;

        /// <summary>
        /// 获取或设置势力是否存活
        /// </summary>
        public override bool IsAlive
        {
            get
            {
                return isAlive && mGovernor != null && mGovernor.BelongCity != null;
            }
            set
            {
                isAlive = value;
            }
        }

        /// <summary>
        /// 获取势力的名称
        /// </summary>
        public override string Name => mGovernor?.Name;

        /// <summary>
        /// 获取势力的颜色名称
        /// </summary>
        public virtual string ColorName => mGovernor?.ColorName;

        /// <summary>
        /// 首都, 必须是君主所在城市,无论港关
        /// </summary>
        public City CapitalCity => mGovernor?.BelongCity;

        /// <summary>
        /// 第一军团
        /// </summary>
        public Corps CapitalCorps => mGovernor?.BelongCorps;

        /// <summary>
        /// 主公
        /// </summary>
        [JsonProperty]
        public int Governor;

        public Person mGovernor;

        /// <summary>
        /// 军师
        /// </summary>
        [JsonProperty]
        public int Counsellor;
        public Person mCounsellor;

        /// <summary>
        /// 旗帜
        /// </summary>
        [JsonProperty]
        public int Flag;

        public Flag mFlag { get; set; }

        /// <summary>
        /// 爵位的 id（存档数值），读取 Title 时按需解析
        /// </summary>
        [JsonProperty("Title")]
        public int TitleId;

        Title mTitle;
        /// <summary>
        /// 爵位；写入时自动同步 TitleId
        /// </summary>
        public Title Title
        {
            get
            {
                if (mTitle == null && TitleId > 0) mTitle = IdRef.Resolve<Title>(TitleId);
                return mTitle;
            }
            set { mTitle = value; TitleId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 联盟信息
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Alliance>))]
        [JsonProperty]
        public SangoObjectList<Alliance> AllianceList = new SangoObjectList<Alliance>();

        /// <summary>
        /// 势力独有的初始化科技信息,以此为入口,且可以为势力设置独特的科技树
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Technique>))]
        [JsonProperty]
        public SangoObjectList<Technique> InitTechniques = new SangoObjectList<Technique>();

        /// <summary>
        /// 已完成的科技信息
        /// </summary>
        [JsonConverter(typeof(SangoObjectListIDConverter<Technique>))]
        [JsonProperty]
        public SangoObjectList<Technique> Techniques = new SangoObjectList<Technique>();

        /// <summary>
        /// 本国被俘虏
        /// </summary>
        public List<Person> BeCaptiveList = new List<Person>();

        /// <summary>
        /// 技巧点数
        /// </summary>
        [JsonProperty] public int TechniquePoint { get; private set; }

        /// <summary>
        /// 霸业点数
        /// </summary>
        [JsonProperty] public int HegemonyPoint { get; set; }

        /// <summary>
        /// 势力方针
        /// </summary>
        [JsonProperty] public int PolicyType { get; set; }

        /// <summary>
        /// 国库
        /// </summary>
        public ItemStore Stroe = new ItemStore();

        /// <summary>
        /// 当前研究的技术
        /// </summary>
        [JsonProperty]
        public int ResearchTechnique { get; set; }

        /// <summary>
        /// 当前研究的技术剩余回合
        /// </summary>
        [JsonProperty]
        public int ResearchLeftCounter { get; set; }

        /// <summary>
        /// AI指令集
        /// </summary>
        public List<System.Func<Force, Scenario, bool>> AICommandList = new List<System.Func<Force, Scenario, bool>>();

        /// <summary>
        /// 威胁过本势力的敌方部队ID列表(用于 AI 主动驱逐)。
        /// 敌方部队攻击本势力的城池 / 建筑 / 部队时会被记录,只要其仍存活且位于本势力领地内,
        /// 就会由距离最近的城市派兵歼灭。
        /// </summary>
        [JsonProperty]
        public List<int> threatTroopIds = new List<int>();

        /// <summary>
        /// 记录一个对本势力实施过攻击的敌方部队。
        /// </summary>
        /// <param name="troop">实施攻击的敌方部队</param>
        public void MarkThreatTroop(Troop troop)
        {
            if (troop == null || !troop.IsAlive)
                return;
            if (threatTroopIds == null)
                threatTroopIds = new List<int>();
            if (threatTroopIds.Contains(troop.Id))
                return;
            threatTroopIds.Add(troop.Id);
        }

        /// <summary>
        /// 清理已阵亡或不存在的威胁部队记录。
        /// </summary>
        public void CleanupThreatTroops()
        {
            if (threatTroopIds == null || threatTroopIds.Count == 0)
                return;
            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return;

            for (int i = threatTroopIds.Count - 1; i >= 0; i--)
            {
                Troop troop = scenario.troopsSet.Get(threatTroopIds[i]);
                if (troop == null || !troop.IsAlive)
                    threatTroopIds.RemoveAt(i);
            }
        }

        /// <summary>
        /// 相邻势力
        /// </summary>
        public List<Force> NeighborForceList = new List<Force>();

        /// <summary>
        /// 相邻非本势力的城市
        /// </summary>
        public List<City> NeighborCityList = new List<City>();

        /// <summary>
        /// 本势力的城市
        /// </summary>
        public List<City> CityList = new List<City>();

        /// <summary>
        /// 国力值
        /// </summary>
        public int FightPower;

        /// <summary>
        /// 执行建筑行为的建筑列表(建筑攻击等)
        /// </summary>
        Queue<BuildingBase> buildingBaseList = new Queue<BuildingBase>();

        /// <summary>
        /// 当前可以研发的科技列表
        /// </summary>
        public List<Technique> canResearchTechniqueList = new List<Technique>();

        /// <summary>
        /// 国家科技树
        /// </summary>
        public List<ForceTechnique> techniqueTree = new List<ForceTechnique>();

        /// <summary>
        /// 科技树最大等级
        /// </summary>
        public int techniqueMaxLevel = 0;

        /// <summary>
        /// 科技树最大行数
        /// </summary>
        public int techniqueMaxRow = 0;

        /// <summary>
        /// 可创建的物品类型列表
        /// </summary>
        public List<ItemType> createdItemTypes = new List<ItemType>();

        /// <summary>
        /// 判断是否与另一势力为敌对关系
        /// </summary>
        /// <param name="force">另一势力</param>
        /// <returns>是否为敌对关系</returns>
        public bool IsEnemy(Force force) { return IsEnemy(this, force); }

        /// <summary>
        /// 势力拥有的武将数量
        /// </summary>
        public int PersonCount { get; set; }

        /// <summary>
        /// 势力拥有的城市数量
        /// </summary>
        public int CityCount { get; set; }

        /// <summary>
        /// 势力拥有的城寨数量
        /// </summary>
        public int CityBaseCount { get; set; }
        public int BorderCityCount { get; set; }

        /// <summary>
        /// 势力的颜色
        /// </summary>
        public Color Color => mFlag.color;

        /// <summary>
        /// 当前运行的军团
        /// </summary>
        public Corps CurRunCorps { get; set; }

        /// <summary>
        /// 势力的行动列表
        /// </summary>
        public List<ActionBase> actionList;

        /// <summary>
        /// 外交失败次数记录 (key: 目标势力ID, value: 失败次数)
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> DiplomacyFailCount = new Dictionary<int, int>();

        /// <summary>
        /// 外交免疫时间记录 (key: 目标势力ID, value: 免疫结束时间)
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> DiplomacyImmunityTime = new Dictionary<int, int>();

        /// <summary>
        /// 能够建造的建筑集合
        /// </summary>
        public List<BuildingType> canBuildMilitaryBuildingType = new List<BuildingType>();


        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (Governor > 0)
                mGovernor = scenario.personSet.Get(Governor);
            if (Counsellor > 0)
                mCounsellor = scenario.personSet.Get(Counsellor);
            mFlag = scenario.CommonData.Flags.Get(Flag);
            if (mFlag == null)
                mFlag = scenario.CommonData.Flags.Get(0);
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            Governor = mGovernor?.Id ?? 0;
            Counsellor = mCounsellor?.Id ?? 0;
        }

        /// <summary>
        /// 初始化势力
        /// </summary>
        /// <param name="scenario">当前场景</param>
        public override void Init(Scenario scenario)
        {
            if (mGovernor == null)
            {
                IsAlive = false;
                return;
            }

            actionList = new List<ActionBase>();
            Techniques.ForEach(x =>
            {
                x.InitActions(actionList, this);
            });
            InitTechniquesTree(scenario);
            UpdateTurnInfo(scenario);
        }

        public override void Clear()
        {
            base.Clear();

            if (actionList != null)
            {
                for (int i = 0; i < actionList.Count; i++)
                    actionList[i].Clear();

                actionList.Clear();
                actionList = null;
            }


        }

        public void UpdateCanBuildBuildingTypes()
        {
            canBuildMilitaryBuildingType.Clear();
            Scenario.Cur.CommonData.BuildingTypes.ForEach(x =>
            {
                if (!x.IsIntrior && x.IsValid(this) && x.canBuild)
                {
                    if (x.level > 1)
                    {
                        canBuildMilitaryBuildingType.RemoveAll(b => b.kind == x.kind && b.level < x.level);
                        canBuildMilitaryBuildingType.Add(x);
                    }
                    else
                    {
                        // 如果有level比这个大的 则不添加
                        if (!canBuildMilitaryBuildingType.Exists(b => b.kind == x.kind && x.level > 1))
                            canBuildMilitaryBuildingType.Add(x);
                    }
                }
            });
        }

        void InitTechniquesTree(Scenario scenario)
        {
            techniqueMaxLevel = 0;

            // 在这里初始化科技树
            techniqueTree.Clear();
            InitTechniques.ForEach(x =>
            {
                ForceTechnique forceTechnique = new ForceTechnique()
                {
                    technique = x,
                    y = 0,
                };
                techniqueMaxLevel = Mathf.Max(techniqueMaxLevel, forceTechnique.FillChildren(scenario.CommonData.Techniques));
                techniqueTree.Add(forceTechnique);
            });
            techniqueTree.Sort((a, b) => a.technique.Id.CompareTo(b.technique.Id));
            techniqueMaxLevel++;

            for (int i = 0; i < techniqueTree.Count; i++)
            {
                int upY = 0, downY = 0;
                techniqueTree[i].UpdateY(ref upY, ref downY);

            }

            int startY = 0;
            for (int i = 0; i < techniqueTree.Count; i++)
            {
                ForceTechnique forceTechnique = techniqueTree[i];
                int minY = 0, maxY = 0;
                forceTechnique.GetMinMaxY(ref minY, ref maxY);
                int total = maxY - minY;
                forceTechnique.y = startY - minY;
                startY += total + 1;
            }
            techniqueMaxRow = startY + 1;
        }

        /// <summary>
        /// 准备科技列表
        /// </summary>
        /// <param name="scenario">当前场景</param>
        void prepareTechniqueList(Scenario scenario)
        {
            // 初始化可研究的科技
            canResearchTechniqueList.Clear();
            scenario.CommonData.Techniques.ForEach(x =>
            {
                if (x.Id != ResearchTechnique)
                {
                    if (x.CanResearch(this))
                    {
                        canResearchTechniqueList.Add(x);
                    }
                }
            });
        }

        /// <summary>
        /// 判断是否与另一势力为同盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>是否为同盟关系</returns>
        public bool IsAlliance(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other) && alliance.allianceType == AllianceType.Alliance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断是否与另一势力有贸易协议
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>是否有贸易协议</returns>
        public bool HasActiveTradeAgreement(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other) && alliance.allianceType == AllianceType.Trade)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断是否与另一势力有任何协议
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>是否有任何协议</returns>
        public bool HasActiveAgreement(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.IsAlive && alliance.leftCount > 0 && alliance.Contains(other))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 检查与另一势力的联盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <returns>联盟对象</returns>
        public Alliance CheckAlliance(Force other)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.Contains(other))
                    return alliance;
            }
            return null;
        }

        /// <summary>
        /// 检查与另一势力的特定类型联盟关系
        /// </summary>
        /// <param name="other">另一势力</param>
        /// <param name="allianceType">联盟类型</param>
        /// <returns>联盟对象</returns>
        public Alliance CheckAlliance(Force other, AllianceType allianceType)
        {
            for (int i = 0; i < AllianceList.Count; ++i)
            {
                Alliance alliance = AllianceList[i];
                if (alliance.IsAlive && alliance.leftCount > 0 && alliance.Contains(other) && alliance.allianceType == allianceType)
                    return alliance;
            }
            return null;
        }

        public bool IsTruce(Force other)
        {
            return CheckAlliance(other, AllianceType.Truce) != null;
        }

        //public Corps Add(Corps corps)
        //{
        //    allCorps.Add(Scenario.Cur.Add(corps));
        //    return corps;
        //}
        //public City Add(City city)
        //{
        //    allCities.Add(Scenario.Cur.Add(city));
        //    return city;
        //}
        //public Person Add(Person person)
        //{
        //    allPersons.Add(Scenario.Cur.Add(person));
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(Scenario.Cur.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(Scenario.Cur.Add(building));
        //    return building;
        //}
        //public Corps Remove(Corps corps)
        //{
        //    allCorps.Remove(Scenario.Cur.Remove(corps));
        //    return corps;
        //}
        //public City Remove(City city)
        //{
        //    allCities.Remove(Scenario.Cur.Remove(city));
        //    return city;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(Scenario.Cur.Remove(person));
        //    return person;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(Scenario.Cur.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(Scenario.Cur.Remove(building));
        //    return building;
        //}

        /// <summary>
        /// 运行势力逻辑
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool Run(Scenario scenario)
        {
            if (ActionOver)
                return true;

            if (!DoBuildingBehaviour(scenario))
                return false;

            // 非玩家才执行AI
            if (!IsPlayer && !DoAI(scenario))
                return false;

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                Corps corps = scenario.corpsSet[i];
                if (corps != null && corps.IsAlive && corps.BelongForce == this && !corps.ActionOver)
                {
                    CurRunCorps = corps;
                    if (!corps.Run(scenario))
                        return false;
                }
            }

            ActionOver = true;
            return true;
        }

        /// <summary>
        /// 执行AI逻辑
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                GameEvent.OnForceAIStart?.Invoke(this, scenario);
                AIPrepared = true;
            }

            while (AICommandList.Count > 0)
            {
                System.Func<Force, Scenario, bool> CurrentCommand = AICommandList[0];
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandList.RemoveAt(0);
            }

            GameEvent.OnForceAIEnd?.Invoke(this, scenario);
            AIFinished = true;
            return true;
        }


        /// <summary>
        /// 执行建筑行为
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public bool DoBuildingBehaviour(Scenario scenario)
        {
            if (buildingBaseList.Count <= 0)
                return true;

            while (buildingBaseList.Count > 0)
            {
                BuildingBase currentBuilding = buildingBaseList.Peek();
                if (!currentBuilding.DoBuildingBehaviour(scenario))
                    return false;

                buildingBaseList.Dequeue();
            }

            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            // 添加外交AI
            // 【暂时屏蔽】AI 势力之间的外交。如需恢复,取消下一行注释即可。
            //AICommandList.Add(ForceAI.AIDiplomacy);
            AICommandList.Add(ForceAI.AICaptives);
            AICommandList.Add(ForceAI.AITechniques);
            AICommandList.Add(ForceAI.AISetOfficial);
            AICommandList.Add(ForceAI.AITransfromPerson);

            GameEvent.OnForceAIPrepare?.Invoke(this, scenario);
        }

        /// <summary>
        /// 势力回合开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnStart(Scenario scenario)
        {
            buildingBaseList.Clear();
            AIFinished = false;
            AIPrepared = false;
            FightPower = 0;
            PersonCount = 0;
            CityCount = 0;
            CityBaseCount = 0;
            // 清理已阵亡的威胁部队记录
            CleanupThreatTroops();
            Sango.Log.Info($"==={Name} 回合===");

            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                    buildingBaseList.Enqueue(c);
                }
            }

            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    PersonCount++;
                    c.OnForceTurnStart(scenario);
                }
            }

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                }
            }



            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnStart(scenario);
                }
            }

            UpdateTurnInfo(scenario);

            // 检查敌方新建部队是否有占领我方城池的任务
            if (IsPlayer)
            {
                List<City> checkedCity = null;
                foreach (Troop troop in scenario.troopsSet)
                {
                    if (troop != null && troop.IsAlive && troop.BelongForce != this && troop.IsNewTroop && troop.missionType == (int)MissionType.TroopOccupyCity)
                    {
                        // 检查任务目标是否为我方城池
                        var targetCity = scenario.GetObject<City>(troop.missionTarget);
                        if (targetCity != null && targetCity.BelongForce == this)
                        {
                            // 根据军师智力计算发现概率
                            int baseProbability = scenario.Variables.discoverEnemyTroopBaseProbability;
                            int intelligenceFactor = 0;
                            if (mCounsellor != null)
                            {
                                intelligenceFactor = mCounsellor.Intelligence * scenario.Variables.discoverEnemyTroopIntelligenceFactor;
                            }
                            int totalProbability = baseProbability + intelligenceFactor;

                            // 概率命中后生成相机移动事件
                            if (GameRandom.Chance(totalProbability, 10000))
                            {
                                // 获取部队所属城市的位置
                                var troopCity = troop.BelongCity;
                                if (troopCity != null)
                                {
                                    if (checkedCity == null)
                                        checkedCity = new List<City>();
                                    else
                                    {
                                        if (checkedCity.Contains(troopCity))
                                            continue;
                                    }

                                    checkedCity.Add(troopCity);

                                    // 创建相机移动事件
                                    CameraMoveEvent cameraMoveEvent = RenderEvent.Instance.Create<CameraMoveEvent>();
                                    cameraMoveEvent.Init(troop.cell.Position, 0.5f,
                                        GameDialog.DialogStyle.ClickPersonSay, $"{ColorName}大人，\n我军细作传来消息,有敌军正在往我方{targetCity.ColorName}靠近!!。", mCounsellor, null, null).donotReturn = true;
                                    RenderEvent.Instance.Add(cameraMoveEvent);

                                    // 触发发现敌方部队事件
                                    GameEvent.OnDiscoverEnemyTroop?.Invoke(this, targetCity, troop, mCounsellor);
                                }
                            }
                        }
                    }
                }

                if (checkedCity != null)
                {
                    CameraMoveEvent cameraMoveEvent = RenderEvent.Instance.Create<CameraMoveEvent>();
                    cameraMoveEvent.Init(MapRender.Instance.GetCameraPos(), 0.5f);
                    RenderEvent.Instance.Add(cameraMoveEvent);
                }

            }

            PrepareCityPersonHole(scenario);

            return base.OnForceTurnStart(scenario);
        }

        void UpdateTurnInfo(Scenario scenario)
        {
            prepareTechniqueList(scenario);
            UpdateValidCreatedItemTypes();
            UpdateCanBuildBuildingTypes();

            bool hasNoCheckBorder = false;
            NeighborForceList.Clear();
            NeighborCityList.Clear();
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {

                    c.OnForceTurnStart(scenario);
                    FightPower += c.FightPower;
                    buildingBaseList.Enqueue(c);
                    CityBaseCount++;

                    if (c.IsCity())
                    {
                        CityCount++;

                        c.borderLine = -1;
                        // 计算相邻势力
                        foreach (City neighbor in c.NeighborList)
                        {
                            if (!neighbor.IsSameForce(c))
                            {
                                c.borderLine = 0;
                                if (neighbor.BelongForce != null)
                                {
                                    if (!NeighborForceList.Contains(neighbor.BelongForce))
                                    {
                                        NeighborForceList.Add(neighbor.BelongForce);
                                    }
                                }

                                if (!NeighborCityList.Contains(neighbor))
                                    NeighborCityList.Add(neighbor);
                            }
                        }
                        if (c.borderLine == -1)
                            hasNoCheckBorder = true;
                    }
                }
            }

            while (hasNoCheckBorder)
            {
                for (int i = 0; i < scenario.citySet.Count; ++i)
                {
                    var c = scenario.citySet[i];
                    if (c != null && c.IsAlive && c.BelongForce == this && c.borderLine < 0)
                    {
                        int minBorder = 99;
                        // 计算相邻势力
                        foreach (City neighbor in c.NeighborList)
                        {
                            if (neighbor.borderLine >= 0)
                                minBorder = Mathf.Min(minBorder, neighbor.borderLine);
                        }
                        if (minBorder >= 0)
                        {
                            c.borderLine = minBorder + 1;
                        }
                        hasNoCheckBorder = c.borderLine == -1;
                    }
                }
            }



        }


        /// <summary>
        /// 势力回合结束时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnForceTurnEnd(Scenario scenario)
        {
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    c.OnForceTurnEnd(scenario);
                }
            }

            return base.OnForceTurnEnd(scenario);
        }

        /// <summary>
        /// 月份开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnMonthStart(Scenario scenario)
        {
            // 原版：俘虏每月都可能掉忠（在仕武将只有换季才结算，见 OnSeasonStart）
            ForceCaptiveLoyaltyChange();
            return base.OnMonthStart(scenario);
        }

        /// <summary>
        /// 季节开始时的回调方法
        /// </summary>
        /// <param name="scenario">当前场景</param>
        /// <returns>是否成功执行</returns>
        public override bool OnSeasonStart(Scenario scenario)
        {
            if (RollLoyaltyChangeSettlement())
            {
                ForcePersonLoyaltyChange();
            }

            return base.OnSeasonStart(scenario);
        }


        static int[] loyaltyWeight = new int[5] { 2, 3, 2, 1, 1 };

        /// <summary>
        /// 势力武将换季掉忠计算。
        ///
        /// 顺序：
        ///   1) 原版"必不掉忠"资格判定（君主 / 亲爱 / 夫妻 / 义兄弟 / 亲子 / 相性与义理野心 / 城内有仁政），
        ///      见 PersonLoyaltyRules.CanLoseLoyalty —— 不通过就直接跳过；
        ///   2) 通过者每人各掷一次掉忠值（原来只掷一次、全势力共用）；
        ///   3) 扣忠之前再发 GameEvent.OnForcePersonLoyaltyChange 征询监听者：
        ///      把 shouldLoseLoyalty 置 false 即可否决该武将本次扣忠
        ///     （例如"阻止本城武将掉忠"的 Action，见 CityPreventPersonLoyaltyLoss）。
        /// </summary>
        public void ForcePersonLoyaltyChange()
        {
            // TODO: 武将换季掉忠（掉忠量本身仍是占位公式）
            ForEachPerson(person =>
            {
                // 原版免掉忠条件：命中则本季不进入掉忠结算
                if (!PersonLoyaltyRules.CanLoseLoyalty(this, person)) return;

                // 每人各掷一次：本次该掉多少
                int v = GameRandom.RandomWeightIndex(loyaltyWeight, 9);

                // 先问监听者能否否决本次扣忠（默认 true = 照扣）
                Tools.OverrideData<bool> shouldLoseLoyalty = Tools.OverrideData<bool>.Create(true);
                GameEvent.OnForcePersonLoyaltyChange?.Invoke(this, person, shouldLoseLoyalty);
                if (!shouldLoseLoyalty.Value) return;

                person.loyalty -= v;
                Sango.Log.Info($"势力：{Name}, 武将：{person.Name}, 忠诚度下降: {v}, 现有忠诚度:{person.loyalty}");
            });
        }

        /// <summary>
        /// 本势力是否要结算一次掉忠。
        /// 原版：掌握人心科技有 2/3 概率让武将不进入掉忠计算 —— 数据里给该科技挂一个
        /// value=33 的 ForcePersonLoyaltyChange（它把基础值 100 压成 33），事件一来即生效；
        /// 没有该科技时基础值仍是 100，等于必定结算。
        /// </summary>
        private bool RollLoyaltyChangeSettlement()
        {
            Tools.OverrideData<int> overrideData = Tools.OverrideData<int>.Create(100);
            GameEvent.OnForcePersonLoyaltyChangeProbability?.Invoke(this, overrideData);
            return GameRandom.Chance(overrideData.ValueAndRecycle);
        }

        /// <summary>
        /// 本势力被俘武将的月度掉忠（原版：俘虏每月都可能掉忠）。
        ///
        /// 与换季结算（ForcePersonLoyaltyChange）的区别：
        ///   · 频率：每月（换季是每 3 个月）；
        ///   · 免掉忠条件只有三条（亲爱君主 / 夫妻或义兄弟 / 子女或父母），不受「仁政」与
        ///     "相性差 ≤ 25"保护，见 PersonLoyaltyRules.CanCaptiveLoseLoyalty；
        ///   · 与原君主相性差 ≥ 25 会额外多掉，义理低掉得更快、义理高掉得慢；
        ///   · 掌握人心科技：每人各掷一次，本人有 2/3 概率跳过。
        ///
        /// 比较对象是被俘势力的君主（原君主）—— 俘虏的本势力归属与 Force.BeCaptiveList
        /// 都保留在原势力上，因此这里遍历 BeCaptiveList。
        /// </summary>
        public void ForceCaptiveLoyaltyChange()
        {
            if (BeCaptiveList == null || BeCaptiveList.Count == 0) return;

            Person governor = mGovernor;
            for (int i = BeCaptiveList.Count - 1; i >= 0; --i)
            {
                Person person = BeCaptiveList[i];
                if (person == null || !person.IsAlive || !person.IsPrisoner) continue;
                if (!PersonLoyaltyRules.CanCaptiveLoseLoyalty(person, governor)) continue;
                if (!RollLoyaltyChangeSettlement()) continue;      // 掌握人心：每人各掷一次

                int v = PersonLoyaltyRules.CalcCaptiveLoyaltyLoss(person, governor);
                person.loyalty = System.Math.Max(0, person.loyalty - v);
                Sango.Log.Info($"势力：{Name}, 俘虏：{person.Name}, 忠诚度下降: {v}, 现有忠诚度:{person.loyalty}");
            }
        }

        /// <summary>
        /// 对所有城寨执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCityBase(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有城市执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCity(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsCity())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有关卡执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachGate(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsGate())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有港口执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachPort(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongForce == this && c.IsPort())
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有武将执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachPerson(System.Action<Person> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有军团执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachCorps(System.Action<Corps> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.corpsSet.Count; ++i)
            {
                var c = scenario.corpsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有建筑执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachBuilding(System.Action<Building> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }

        /// <summary>
        /// 对所有部队执行操作
        /// </summary>
        /// <param name="action">要执行的操作</param>
        public void ForEachTroop(System.Action<Troop> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongForce == this)
                {
                    action(c);
                }
            }
        }


        /// <summary>
        /// 获得技巧点数
        /// </summary>
        /// <param name="value">获得的技巧点数</param>
        public void GainTechniquePoint(int value)
        {
            if (value == 0) return;

            TechniquePoint += value;

            if (IsPlayer && value > 0)
            {
                GameMedia.Instance.PlaySfx(61);
                GameMedia.Instance.PlayDelayedSfx(60, 1);
            }

            GameEvent.OnForceGainTechniquePoint?.Invoke(this, value);
        }

        /// <summary>
        /// 获得霸业点数
        /// </summary>
        /// <param name="value">获得的霸业点数</param>
        public void GainHegemonyPoint(int value)
        {
            HegemonyPoint += value;
            GameEvent.OnForceGainHegemonyPoint?.Invoke(this, value);
        }

        /// <summary>
        /// 检查是否拥有指定科技
        /// </summary>
        /// <param name="techId">科技ID</param>
        /// <returns>是否拥有该科技</returns>
        public bool HasTechnique(int techId)
        {
            return Techniques.Contains(techId);
        }

        /// <summary>
        /// 添加科技
        /// </summary>
        /// <param name="techId">科技ID</param>
        /// <returns>添加的科技对象</returns>
        public Technique AddTechnique(int techId)
        {
            if (ResearchTechnique == techId)
            {
                ResearchTechnique = 0;
                ResearchLeftCounter = 0;
            }

            Technique technique = Scenario.Cur.GetObject<Technique>(techId);
            if (technique == null) return null;
            Techniques.Add(technique);
            technique.InitActions(actionList, this);
            technique.DoActiveAction(this);
            UpdateCanBuildBuildingTypes();
            UpdateValidCreatedItemTypes();

            ForEachCityBase(x =>
            {
                x.UpdateCalculate();
            });

            ForEachTroop(x =>
            {
                x.CalculateAttribute(Scenario.Cur);
            });

            return technique;
        }

        /// <summary>
        /// 创建军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        public void CreateCorps(int number, Person commander, List<City> cities)
        {
            Corps corps = new Corps();
            corps.number = number;
            corps.BelongForce = this;
            corps.mComander = commander;
            Scenario.Cur.Add(corps);
            corps.Init(Scenario.Cur);
            foreach (var city in cities)
            {
                city.BelongCorps = corps;
                foreach (Person person in city.allPersons)
                {
                    person.BelongCorps = corps;

                }
                foreach (Building building in city.allBuildings)
                {
                    building.BelongCorps = corps;
                }
            }
            GameEvent.OnCorpsCreate?.Invoke(corps, Scenario.Cur);
        }

        public void CreateCorps(Corps corps)
        {
            corps.BelongForce = this;
            Scenario.Cur.Add(corps);
            corps.Init(Scenario.Cur);
            corps.mComander.state = (int)PersonStateType.Commander;
            foreach (var city in corps.inti_cities)
            {
                city.BelongCorps = corps;
                foreach (Person person in city.allPersons)
                {
                    person.BelongCorps = corps;

                }
                foreach (Building building in city.allBuildings)
                {
                    building.BelongCorps = corps;
                }
            }

            // 处理港关
            Scenario.Cur.citySet.ForEach(x =>
            {
                if (x.IsAlive && !x.IsCity() && x.BelongForce == this && x != corps.BelongForce.CapitalCity && corps.inti_cities.Contains(x.BelongCity))
                {
                    x.BelongCorps = corps;
                    x.UpdateCorps();
                }
            });

            corps.inti_cities = null;
            corps.mComander.BelongCity.UpdateNewLeader();
            corps.PrepareCityInfo();
            GameEvent.OnCorpsCreate?.Invoke(corps, Scenario.Cur);
        }

        public void ResetCorps(Corps corps, Corps copy)
        {
            DeleteCorps(corps);
            CreateCorps(copy);
        }

        /// <summary>
        /// 解散军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        public void DeleteCorps(int number)
        {
            if (number == 1) return;
            Corps corps = Scenario.Cur.corpsSet.Find((x) =>
            {
                return x.BelongForce == this && x.number == number;
            });
            DeleteCorps(corps);
        }

        /// <summary>
        /// 解散军团
        /// </summary>
        /// <param name="number"></param>
        /// <param name="commander"></param>
        /// <param name="cities"></param>
        public void DeleteCorps(Corps corps)
        {
            if (corps.BelongForce == null)
            {
                Scenario.Cur.corpsSet.Remove(corps);
                return;
            }

            if (corps == CapitalCorps)
                return;

            Corps governorCorps = CapitalCorps;
            Scenario scenario = Scenario.Cur;
            scenario.citySet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    x.BelongCorps = governorCorps;
                }
            });

            scenario.personSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    x.BelongCorps = governorCorps;
                }
            });

            scenario.buildingSet.ForEach(x =>
            {
                if (x != null && x.IsAlive && x.BelongCorps == corps)
                {
                    x.BelongCorps = governorCorps;
                }
            });
            if (corps.mComander != null && corps.mComander.state == (int)PersonStateType.Commander)
                corps.mComander.state = (int)PersonStateType.Normal;

            Scenario.Cur.corpsSet.Remove(corps);
            GameEvent.OnCorpsDelete?.Invoke(corps, Scenario.Cur);
        }

        /// <summary>
        /// 更换军师
        /// </summary>
        /// <param name="dest">新军师</param>
        public void ChangeCounsellor(Person dest)
        {
            Person old = mCounsellor;
            mCounsellor = dest;
            GameEvent.OnForceChangeCounsellor?.Invoke(this, old);
        }

        /// <summary>
        /// 更新有效的可创建物品类型
        /// </summary>
        public void UpdateValidCreatedItemTypes()
        {
            createdItemTypes.Clear();
            Scenario scenario = Scenario.Cur;
            Dictionary<int, ItemType> itemMap = new Dictionary<int, ItemType>();
            scenario.CommonData.ItemTypes.ForEach(it =>
            {
                if (it.cost > 0 && it.IsValid(this))
                {
                    ItemType itemType;
                    if (itemMap.TryGetValue(it.storeKind, out itemType))
                    {
                        if (it.Id > itemType.Id)
                        {
                            itemMap[it.storeKind] = it;
                        }
                    }
                    else
                    {
                        itemMap[it.storeKind] = it;
                    }
                }
            });

            createdItemTypes.AddRange(itemMap.Values.ToArray());
            createdItemTypes.Sort(SangoObject.Compare);
        }


        /// <summary>
        /// 准备人才缺口
        /// </summary>
        public void PrepareCityPersonHole(Scenario scenario)
        {
            int cityCount = 0;
            BorderCityCount = 0;
            int personCount = 0;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.BelongForce == this && c.IsCity())
                {
                    cityCount++;
                    if (c.IsBorderCity)
                        BorderCityCount++;
                    c.PersonHole = 0;
                    personCount += c.allPersons.Count;
                }
            }

            if (cityCount <= 1) return;
            if (BorderCityCount == 0)
                return;

            int noBoderSeat = 3;
            int avarageTotalSeat = personCount - cityCount * noBoderSeat;
            if (avarageTotalSeat <= 0)
            {
                noBoderSeat = 1;
                avarageTotalSeat = personCount - cityCount * noBoderSeat;
                if (avarageTotalSeat <= 0)
                {
                    avarageTotalSeat = personCount;
                }
            }
            int boderSeat = avarageTotalSeat / BorderCityCount + noBoderSeat;
            int upSeat = boderSeat - 15;

            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.BelongForce == this && c.IsCity())
                {
                    if (c.IsBorderCity)
                    {
                        c.PersonHole = boderSeat - c.allPersons.Count;
                    }
                    else if(upSeat > 0)
                    {
                        if(c.borderLine == 1)
                        {
                            c.PersonHole = (noBoderSeat + upSeat) - c.allPersons.Count;
                        }
                    }
                    else
                    {
                        c.PersonHole = noBoderSeat - c.allPersons.Count;
                    }
                }
            }
        }
    }
}
