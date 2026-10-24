/*
 * 文件名：Corps.cs
 * 描述：军团类，管理游戏中的军团对象
 * 创建日期：2026-03-27
 * 最后修改：2026-03-27
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sango.Core
{
    /// <summary>
    /// 军团类，管理游戏中的军团对象
    /// 军团是势力下的组织单位，包含多个城市和武将
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Corps : SangoObjectExtensionData
    {
        /// <summary>
        /// 获取对象类型
        /// </summary>
        public override SangoObjectType ObjectType { get { return SangoObjectType.Corps; } }

        /// <summary>
        /// 是否为玩家军团
        /// </summary>
        public virtual bool IsPlayer => BelongForce?.IsPlayer ?? false;

        /// <summary>
        /// 是否为玩家控制的
        /// </summary>
        public virtual bool IsPlayerControl => IsPlayer && IsCaptainCorps;

        public bool IsCaptainCorps => number == 1;

        /// <summary>
        /// 获取是否为当前的玩家势力
        /// </summary>
        public bool IsCurPlayer => BelongForce?.IsCurPlayer ?? false;

        /// <summary>
        /// AI是否完成行动
        /// </summary>
        public virtual bool AIFinished { get; set; }

        /// <summary>
        /// AI是否准备完成
        /// </summary>
        public virtual bool AIPrepared { get; set; }

        /// <summary>
        /// 军团编号文本（索引与军团编号一致，覆盖第1~第50军团）
        /// 编号文本最多保留两位汉字:两位以上编号省略中间的"十"（如编号23显示为"二三"），便于界面紧凑展示；
        /// 索引0为"零"占位（军团编号从1开始不使用），实际可用编号为1~50；
        /// 该数组长度同时决定CorpsSystem可新建军团的最大编号(numberTxt.Length - 1)
        /// </summary>
        public static readonly string[] numberTxt =
        {
            "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
            "二一", "二二", "二三", "二四", "二五", "二六", "二七", "二八", "二九", "三十",
            "三一", "三二", "三三", "三四", "三五", "三六", "三七", "三八", "三九", "四十",
            "四一", "四二", "四三", "四四", "四五", "四六", "四七", "四八", "四九", "五十",
        };

        /// <summary>
        /// 军团名称
        /// </summary>
        public override string Name
        {
            get { return $"{BelongForce?.ColorName}第{numberTxt[number]}军团"; }
        }

        /// <summary>
        /// 军团名称
        /// </summary>
        public string ForceNumberName
        {
            get { return $"{BelongForce?.ColorName}第{numberTxt[number]}军团"; }
        }

        /// <summary>
        /// 带颜色的军团名称
        /// </summary>
        public string ColorName { get { return $"<color=#7CCADB>第{numberTxt[number]}军团</color>"; } }

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonProperty("BelongForce")]
        public int BelongForceId;

        public Force BelongForce;

        /// <summary>
        /// 军团长
        /// </summary>
        [JsonProperty]
        public int Comander;

        public Person mComander;

        /// <summary>
        /// 玩家指定的运输终点城 Id（三级回退，-1 = 未指定）。
        ///
        /// 对齐出处：Open311 `get_player_district_transport_dest`（s11_game_ai_kyoten_cmd_mil_transport.cpp:417-431）
        /// 与 `District::transport_dest_building_` / `transport_dest_district_`。
        /// <list type="number">
        /// <item>玩家在本军团直接指定的据点（<see cref="transportDestBuildingId"/>）；</item>
        /// <item>指定了目标军团时，该军团都督所在据点（<see cref="TotokuServiceId"/>）；</item>
        /// <item>中期目标城（<see cref="mid_objective_target"/>，兼容旧存档）。</item>
        /// </list>
        /// </summary>
        /// <param name="scenario">场景对象</param>
        /// <returns>终点城 Id；未指定返回 -1</returns>
        public int GetTransportDestCityId(Scenario scenario)
        {
            if (transportDestBuildingId > 0)
                return transportDestBuildingId;

            if (transportDestDistrictNumber > 0 && scenario != null && scenario.corpsSet != null)
            {
                for (int i = 0; i < scenario.corpsSet.Count; i++)
                {
                    Corps destCorps = scenario.corpsSet[i];
                    if (destCorps == null || !destCorps.IsAlive)
                        continue;
                    if (destCorps.BelongForce != BelongForce)
                        continue;
                    if (destCorps.number != transportDestDistrictNumber)
                        continue;
                    int id = destCorps.TotokuServiceId;
                    if (id > 0)
                        return id;
                    break;
                }
            }

            return mid_objective_target;
        }

        /// <summary>
        /// 都督（军团长）所在据点 Id（-1 = 都督不在任何据点）。
        ///
        /// 对齐出处：Open311 `District::get_totoku_service_id()`（s11_scen_obj_district.cpp:116-122）——
        /// 原版它是 `totoku_` 的**派生量**，不单独存字段。
        /// 消费者：
        ///   · 跨军团物资输送的目标据点（`district.cpp:50-55`）
        ///   · 军团合并时成员的落位据点（`s11_sys_scen_district.cpp:120-128`）
        /// </summary>
        public int TotokuServiceId
        {
            get
            {
                Person comander = mComander;
                if (comander == null || !comander.IsAlive)
                    return -1;
                City city = comander.BelongCity ?? comander.CurrentCity;
                return city != null ? city.Id : -1;
            }
        }

        /// <summary>
        /// 军团番号
        /// </summary>
        [JsonProperty]
        public int number;

        /// <summary>
        /// 中期目标类型
        /// </summary>
        [JsonProperty] public int mid_objective;

        /// <summary>
        /// 中期目标
        /// </summary>
        [JsonProperty] public int mid_objective_target;

        /// <summary>
        /// 本军团"出击据点"：辖下离攻略目标最近的那座城。
        ///
        /// 对齐出处：Open311 `s11_game_ai_district.cpp:143-159` 的 `outpost_` ——
        /// 玩家子军团在"都市攻略"方针下，从离目标最近的据点出兵。
        /// -1 表示未指定。
        /// </summary>
        [JsonProperty] public int districtTargetCityId = -1;

        /// <summary>
        /// 本军团"运输源据点"：辖下有富余资源的那座城。
        ///
        /// 对齐出处：Open311 `s11_game_ai_district.cpp:160-209` 的 `transport_src_`。
        /// -1 表示未指定。
        /// </summary>
        [JsonProperty] public int districtTransportSrcId = -1;

        /// <summary>
        /// 玩家指定的"运输终点据点"。
        ///
        /// 对齐出处：Open311 `District::transport_dest_building_`（s11_scen.h:1023）
        /// 与 `get_transport_dest_building_id()`（:957）。
        /// 原版这是玩家在军团设置里单独指定的一项，军团 AI 据此挑运输源；
        /// 与 `mid_objective_target`（中期目标城）是两个独立字段，不能互相顶替。
        /// -1 表示未指定。
        /// </summary>
        [JsonProperty] public int transportDestBuildingId = -1;

        /// <summary>
        /// 玩家指定的"运输终点军团编号"（跨军团定向补给）。
        ///
        /// 对齐出处：Open311 `District::transport_dest_district_`（s11_scen.h:1024）
        /// 与 `get_transport_dest_district_number()`（:959）。
        /// -1 表示未指定（只按据点走）。
        /// </summary>
        [JsonProperty] public int transportDestDistrictNumber = -1;

        // 【已合并】原 `policy` / `policy_target` 两个字段删除。
        // 它们与 `appoint` / `appoint_target` 是同一份数据的两套命名：
        //   · 全工程没有任何代码读 `policy`（只有 Force.CreateCorps 做过一次复制）；
        //   · 界面上玩家设的"攻略某势力 / 攻占某城"实际写的是 `appoint`。
        // 因此统一以 `appoint` 为准，避免"能设不生效"的静默失效。

        /// <summary>
        /// 委任（军团方针）：对应 Open311 的 DistrictPolicy
        /// （None=不指定 / DestroyForce=势力攻略 / OccupyCity=都市攻略 / Auto=委任）。
        /// </summary>
        [JsonProperty] public int appoint;

        /// <summary>
        /// 委任目标,攻略势力为势力ID, 攻占城池为城池ID
        /// </summary>
        [JsonProperty] public int appoint_target;

        public enum AppointType : int
        {
            None = 0,
            DestroyForce,
            OccupyCity,
            Auto,
        }

        public enum AppointContentType : int
        {
            MakeItem_Spear = 0,
            MakeItem_Halberd,
            MakeItem_Crossbow,
            MakeItem_Horse,
            MakeItem_Machine,
            MakeItem_Boat,
            Donot_Store_Gold,
            Store_Foood,
            Store_Troops,
            Person,
            Attack,
            Transport,
            Build,
            TransportDisable,

            /// <summary>
            /// 港关驻军：0 = 开（允许 AI 人才调度把武将调动到港 / 关），1 = 关（禁止）。
            /// 只对玩家势力的军团有意义 —— 非玩家（AI）势力以势力为边界调度，不受军团委任约束。
            /// 关闭后由 DeploymentSolver / DeploymentExecutor 共同保证"不为港关编制驻军岗位、也不向港关调人"。
            /// </summary>
            PortGateGarrison,

            /// <summary>枚举项总数（同时作为 appointSetting 数组长度）</summary>
            Max = 15
        }

        /// <summary>
        /// 政策内容
        /// </summary>
        [JsonProperty] public int[] appointSetting;

        /// <summary>
        /// 行动力点数
        /// </summary>
        [JsonProperty] public int ActionPoint { get; set; }

        /// <summary>
        /// 工作计数
        /// </summary>
        [JsonProperty]
        public Dictionary<int, int> jobCounter = new Dictionary<int, int>();


        public override void OnScenarioPrepare(Scenario scenario)
        {
            if (BelongForceId > 0)
                BelongForce = scenario.forceSet.Get(BelongForceId);
            if(Comander > 0)
                mComander = scenario.personSet.Get(Comander);
        }

        public override void OnScenarioSave(Scenario scenario)
        {
            BelongForceId = BelongForce?.Id ?? 0;
            Comander = mComander?.Id ?? 0;
        }

        /// <summary>
        /// 获取工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>工作计数</returns>
        public int GetJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                return job;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// 增加工作计数
        /// </summary>
        /// <param name="jobId">工作ID</param>
        /// <returns>增加后的工作计数</returns>
        public int AddJobCounter(int jobId)
        {
            if (jobCounter.TryGetValue(jobId, out var job))
            {
                job++;
                jobCounter[jobId] = job;
                return job;
            }
            else
            {
                jobCounter.Add(jobId, 1);
                return 1;
            }
        }

        /// <summary>
        /// 创建时候的缓存城市列表
        /// </summary>
        public List<City> inti_cities;

        /// <summary>
        /// 军团颜色表（共50个,依次对应第1~第50军团,索引=number-1）
        /// 前11个保留原有配置以兼容既有显示,其余39个由CreateCorpsColors算法填充,
        /// 任意两个颜色均不相同且辨识度较高
        /// </summary>
        static readonly Color[] colors = CreateCorpsColors();

        /// <summary>
        /// 生成50个高辨识度军团颜色
        /// 设计说明:
        /// 1. 第1~第11军团保留原有颜色,保证既有存档与显示不变化;
        /// 2. 第12~第50军团按黄金角(≈222.5°)在色环上均匀取色,相邻编号色相差大、整体色相不扎堆,
        ///    同时明度按编号每3个一组递减(0.95/0.73/0.51),即使色相接近也能靠明暗拉开辨识度;
        /// 3. 饱和度统一为0.9,保证颜色鲜艳清晰
        /// </summary>
        static Color[] CreateCorpsColors()
        {
            Color[] result = new Color[50];
            // 前11个颜色沿用历史配置,保持旧版本显示习惯
            Color[] legacy = new Color[]
            {
                Color.cyan,
                Color.red * 0.8f,
                Color.yellow * 0.8f,
                Color.green * 0.8f,
                Color.blue * 0.8f,
                Color.gray,
                Color.magenta * 0.8f,
                Color.black,
                Color.green * 0.3f,
                Color.red * 0.3f,
                Color.cyan * 0.5f,
            };
            for (int i = 0; i < legacy.Length; i++)
                result[i] = legacy[i];

            // 第12~第50军团(共39个)用黄金角填充
            int legacyCount = legacy.Length;
            for (int i = legacyCount; i < result.Length; i++)
            {
                // 起始色相0.58避开前11色的红黄绿青蓝品红主色区域,后续按黄金比例步进
                float hue = (0.58f + (i - legacyCount) * 0.618033988749895f) % 1f;
                // 明度按编号分组递减,保证任意两个编号都可区分
                float value = 0.95f - ((i - legacyCount) % 3) * 0.22f;
                float saturation = 0.9f;
                result[i] = Color.HSVToRGB(hue, saturation, value);
            }
            return result;
        }

        public Color Color => colors[number - 1];

        /// <summary>
        /// 番号
        /// </summary>
        public int Index => number;

        public City TargetCity { get; set; }
        public Force TargetForce { get; set; }


        public Queue<System.Func<Corps, Scenario, bool>> AICommandQueue = new Queue<Func<Corps, Scenario, bool>>();


        public Corps()
        {
            // 全 0 = 全部"允许"（含港关驻军默认开），长度跟随 AppointContentType.Max
            appointSetting = new int[(int)AppointContentType.Max];
        }

        public override void Init(Scenario scenario)
        {
            if (appointSetting == null)
            {
                appointSetting = new int[(int)AppointContentType.Max];
            }
            else if (appointSetting.Length < (int)AppointContentType.Max)
            {
                // 枚举新增项后，老存档里只有旧长度的数组：这里**扩容并保留玩家已有设置**，
                // 新增项留作 0（默认值）。整体重置会把玩家辛苦调好的委任方案清零。
                int[] grown = new int[(int)AppointContentType.Max];
                for (int i = 0; i < appointSetting.Length; i++)
                    grown[i] = appointSetting[i];
                appointSetting = grown;
            }
            PrepareCityInfo();
        }

        public int GetAppointValue(AppointContentType policyContentType)
        {
            if (appointSetting == null) return 0;
            return appointSetting[(int)policyContentType];
        }

        public bool CheckTargetIsAppointTarget(Force force)
        {
            if (appoint == (int)AppointType.DestroyForce)
                return appoint_target == force.Id;
            return true;
        }

        public bool CheckTargetIsAppointTarget(City city)
        {
            if (city.BelongForce == null) return true;
            if (appoint == (int)AppointType.OccupyCity)
                return appoint_target == city.Id;
            else if (appoint == (int)AppointType.DestroyForce)
                return appoint_target == city.BelongForce.Id;
            return true;
        }

        public override bool OnForceTurnStart(Scenario scenario)
        {
            jobCounter.Clear();
            AIFinished = false;
            AIPrepared = false;
            AddActionPoint(scenario);
            ActionOver = false;

            // 城市部队名单（allTroops / allAttackTroops）是运行期缓存、不存档：
            // 每回合按部队池重建一次，漏删 / 漏加的病态数据最多存活一回合。
            // 口径见 City.RebuildTroopLists。
            ForEachCity(city => city.RebuildTroopLists());

            PrepareCityInfo();
            if (needUpdateCommander)
                UpdateCommander();


            // 【已移除】玩家势力的资源调度 / 人才部署兜底已删除（原本在此处补跑
            // ResourceDispatcher.RunPlayerCorps 与 DeploymentShadow.RunPlayerCorps）。
            // 玩家下属军团的调度走与 AI 势力完全相同的一条路：
            // Corps.AIPrepare → CorpsAI.AIDistrictPrepare → AIDistrictCommand
            //   → City.DoOutpostAI → 原版据点命令队列（move / summon / transport / support 等）。
            return true;
        }

        public int cityCount;
        public int personCount;
        public int gold;
        public int troops;
        public int food;
        bool needUpdateCommander = false;
        public void UpdateWhenCityChange()
        {
            PrepareCityInfo();
            // 不能解散第一军团
            if (cityCount == 0 && number > 1)
            {
                BelongForce.DeleteCorps(this);
            }
        }

        public void CheckValid()
        {
            PrepareCityInfo();
            if(personCount == 0 && number > 1)
            {
                BelongForce.DeleteCorps(this);
            }
        }

        public void PrepareCityInfo()
        {
            cityCount = 0;
            personCount = 0;
            gold = 0;
            troops = 0;
            food = 0;
            ForEachCity(x =>
            {
                cityCount++;
                personCount += x.allPersons.Count;
                gold += x.gold;
                troops += x.troops;
                food += x.food;
            });
        }

        public void AddActionPoint(Scenario scenario)
        {
            /*
                每回合新增行动力=（君主参数+城市参数+武将参数）* 军师参数

                君主参数=40*【0.65+0.025*（能力参数-6）】
                上式中的“能力参数”为君主“统率”和“魅力”两个属性中数值较高者，然后再除以5所得的数值。这个数值取整，非四舍五入。
                另外，（能力参数-6）最小取0，不能取负数。
                比如某君主统率78，魅力99，则这个值首选选取统率和魅力两项中较高的魅力值99，然后除以5，得19.8。然后取整，得19。
                
                城市参数=10*（拥有城数的数量-1）
                也就是说，每多拥有一个城池，这个参数就多10点。
                不过需要注意的是，只有一个城池的时候，这个数值是0。而拥有6个城池的时候，达到最大值的50上限。
             
                武将参数=拥有最多所属武将数的前6个城池、港口、关卡的武将数之和
                不过，每个据点最多只计算10个，超出不计。
                比如，你只有一个城，城里有15个武将，那也只按10个计算。
                还有一点需要注意的是，必须占领所在地的主城才可以计算该参数。仅仅占领了该城附属的港口和关卡是没用的。
                比如，你没有打下来洛阳，但是打下了虎牢关，那虎牢关中的武将是不计人数的。
                另外就是武将只计算人头，和他本身的身份、属性等等都没有关系。也就是说，刘备和刘禅，在凑人头方面，没有区别。
                武将参数上限为60，即60个武将理论上有可能达到最大行动力。

                军师参数
                根据计算公式也可以看出来，军师参数是一项非常重要的参数，因为前面全是相加，到这里变成了相乘。
                军师参数=1.2-0.01*（50-智力参数）
                其中，智力参数=军师智力/2，取整，同样的，不四舍五入。
                如果没有军师的时候，军师参数取1。
                可以看到，上式中，军师的智力越高，军师参数的数值就越大。当军师智力达到100时，军师参数有最大值1.2。
                而军师的智力≤60的时候，军师参数反而小于了没有军师的1，这时候，不要军师就对了，反正一个60智力的军师，说话能靠谱才怪了。
             */

            // ============ 行动力回复公式：严格对齐 charge_district_action_point ============
            // 出处：s11_game.cpp:1252-1284。
            //
            // 原版公式（逐项对应）：
            //   n  = max(都督统率, 都督魅力) / 5            // 0..20
            //   if (n < 6) n = 6;                          // 下限 6
            //   city_ap = min(城池数, 6) * 10              // 0..60
            //   n  = n + city_ap + 10 + min(city_ap, 武将数)
            //   if (有军师) n = ((军师智力 - 60) / 2 + 100) * n / 100
            //   n += 符节台数 * 5
            //
            // 【与本项目旧实现的差异（均已修正）】
            //   · 都督项：旧版用 `40 * (0.65 + 0.025 * max(0, 统率魅力/5 - 6))`，
            //     数值约为原版的 2 倍（统率 100 时原版 20、旧版 40）；
            //   · 城池项：旧版 `10 * (城池数 - 1)`，比原版**少 10**（原版第 1 座城就计入）；
            //   · 固定基数 +10：旧版完全没有；
            //   · 武将项：旧版是"前 6 城各自 min(10, 人数) 之和"，
            //     原版是 `min(城池项, 武将总数)` —— 语义不同，城池少的军团会虚高。
            List<City> cities = new List<City>();
            int cityCount = 0;
            int totalPersonCount = 0;
            BelongForce.ForEachCityBase((c) =>
            {
                if (c.BelongCity != null && c.BelongCity.BelongForce != BelongForce)
                    return;

                if (c.IsCity()) cityCount++;

                if (c.BelongCorps == this)
                {
                    cities.Add(c);
                    totalPersonCount += c.allPersons != null ? c.allPersons.Count : 0;
                }
            });

            // ① 都督项：max(统率, 魅力) / 5，下限 6
            int n = System.Math.Max(BelongForce.mGovernor.Command, BelongForce.mGovernor.Glamour) / 5;
            if (n < 6)
                n = 6;

            // ② 城池项 + 固定基数 + 武将项
            int cityAp = System.Math.Min(cityCount, 6) * 10;
            n = n + cityAp + 10 + System.Math.Min(cityAp, totalPersonCount);

            // ③ 军师系数：((智力 - 60) / 2 + 100) 百分比
            if (BelongForce.mCounsellor != null)
                n = ((BelongForce.mCounsellor.Intelligence - 60) / 2 + 100) * n / 100;

            // ④ 符节台：每座 +5（不参与军师系数，与原版一致）
            int tallyOfficeCount = 0;
            for (int i = 0; i < cities.Count; i++)
            {
                City c = cities[i];
                if (c == null || c.allBuildings == null)
                    continue;
                for (int j = 0; j < c.allBuildings.Count; j++)
                {
                    Building b = c.allBuildings[j];
                    if (b == null || !b.IsAlive || b.BuildingType == null)
                        continue;
                    // kind 41 = 符节台（BuildingKindType.SpBuilding1）
                    if (b.BuildingType.kind == (int)BuildingKindType.SpBuilding1 && b.isComplate)
                        tallyOfficeCount++;
                }
            }
            n = n + tallyOfficeCount * 5;

            ActionPoint = System.Math.Min(Scenario.Cur.Variables.ActionPointLimit,
                ActionPoint + (int)(n * scenario.Variables.ActionPointFactor));
            ActionPoint = System.Math.Max(0, ActionPoint);


            if (IsPlayer && BelongForce == Scenario.Cur.CurRunForce)
            {
                GameEvent.OnCorpsActionPointChange?.Invoke(this);
            }

        }

        public void ReduceActionPoint(int v)
        {
            // 【据点 AI 记账钩子】
            // 据点命令队列（CityAIOutpost.OutpostCommand）需要知道"这条命令到底执行了没有"，
            // 才能像原版那样 action_point_ -= cost（见 s11_game_ai_kyoten_cmd.cpp:143 等）。
            // 本项目所有据点指令最终都会走 City 的 JobXxx → 本方法，
            // 因此在这里统一标记，避免在 20 多处 JobXxx 调用点逐个埋点。
            // 判定条件限定"正在跑据点 AI 的那个军团"，玩家手动下指令不会误触发。
            if (OutpostRuntime.corps == this && OutpostRuntime.force == BelongForce)
                OutpostRuntime.MarkExecuted();

            if (!IsPlayer)
                return;

            ActionPoint -= v;
            if (IsPlayer && BelongForce == Scenario.Cur.CurRunForce)
            {
                GameEvent.OnCorpsActionPointChange?.Invoke(this);
            }
        }

        //public City Add(City city)
        //{
        //    allCities.Add(BelongForceId.Add(city));
        //    return city;
        //}
        //public Person Add(Person person)
        //{
        //    allPersons.Add(BelongForceId.Add(person));
        //    return person;
        //}
        //public Troop Add(Troop troops)
        //{
        //    allTroops.Add(BelongForceId.Add(troops));
        //    return troops;
        //}
        //public Building Add(Building building)
        //{
        //    allBuildings.Add(BelongForceId.Add(building));
        //    return building;
        //}
        //public City Remove(City city)
        //{
        //    allCities.Remove(BelongForceId.Remove(city));
        //    return city;
        //}
        //public Person Remove(Person person)
        //{
        //    allPersons.Remove(BelongForceId.Remove(person));
        //    return person;
        //}
        //public Troop Remove(Troop troops)
        //{
        //    allTroops.Remove(BelongForceId.Remove(troops));
        //    return troops;
        //}
        //public Building Remove(Building building)
        //{
        //    allBuildings.Remove(BelongForceId.Remove(building));
        //    return building;
        //}

        public override bool Run(Scenario scenario)
        {
            if (ActionOver)
                return true;

            // 主军团永远不是委任军团,除此之外全是委任军团
            if (IsPlayer && mComander == BelongForce.mGovernor)
            {
                GameEvent.OnPlayerControl?.Invoke(this, scenario);
                return false;
            }

            if (!DoAI(scenario))
                return false;

            ActionOver = true;
            return true;
        }

        public override bool DoAI(Scenario scenario)
        {
            if (AIFinished)
                return true;

            if (!AIPrepared)
            {
                AIPrepare(scenario);
                AIPrepared = true;
            }

            while (AICommandQueue.Count > 0)
            {
                System.Func<Corps, Scenario, bool> CurrentCommand = AICommandQueue.Peek();
                if (!CurrentCommand.Invoke(this, scenario))
                    return false;

                AICommandQueue.Dequeue();
            }

            AIFinished = true;
            return true;
        }

        /// <summary>
        /// AI准备
        /// </summary>
        private void AIPrepare(Scenario scenario)
        {
            // 【Phase C】旧调人逻辑（CorpsAI.AITransfromPerson）已删除。
            // 【方案 B】人才部署由 DeploymentShadow 负责，入口在 Force 层，这里不再触发（避免重复调度）：
            //   · AI 势力：Force.AIPrepare → DeploymentShadow.Run（势力级，势力范围 == 其唯一军团）；
            //   · 玩家势力：Force.Run → DeploymentShadow.RunPlayerCorps（军团级，逐军团调度）。
            if (AIConfig.Instance.useOutpostStateOrder)
            {
                // 【行动力预算】对齐 Open311 load_district_data（s11_game_ai_district.cpp:43-64）：
                // AI 军团每回合把行动力兜底到 max(当前值, 城数*2*20)，保证不会因为"没钱"整军团停摆。
                AICommandQueue.Enqueue(CorpsAI.AIDistrictPrepare);
                // 【据点轮转】对齐 district_command（s11_game_ai_district_cmd.cpp:5-54）：
                // 按据点优先级切分行动力 → 逐据点跑命令队列 → 先跑的据点可以吃掉后面的配额。
                // 取代原来的 AICities（只是"遍历自己的城逐个 DoAI"，没有任何预算约束）。
                AICommandQueue.Enqueue(CorpsAI.AIDistrictCommand);
            }
            else
            {
                // 旧路径（useOutpostStateOrder = false 时回退）：遍历自己的城逐个 DoAI
                AICommandQueue.Enqueue(CorpsAI.AICities);
            }
            AICommandQueue.Enqueue(CorpsAI.AITroops);

            //AISections();
            //AICapital();
            //AIMakeMarriage();
            //AISelectPrince();
            //AIZhaoXian();
            //AIAppointMayor();
            //AIHouGong();

            //AILegions();
            //AITrainChildren();
        }

        public void ForEachCity(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsCity())
                {
                    action(c);
                }
            }
        }

        public void ForEachPort(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsPort())
                {
                    action(c);
                }
            }
        }

        public void ForEachGate(System.Action<City> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.citySet.Count; ++i)
            {
                var c = scenario.citySet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && c.IsGate())
                {
                    action(c);
                }
            }
        }

        public void ForEachPerson(System.Action<Person> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.personSet.Count; ++i)
            {
                var c = scenario.personSet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this && !c.IsPrisoner)
                {
                    action(c);
                }
            }
        }

        public void ForEachBuilding(System.Action<Building> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.buildingSet.Count; ++i)
            {
                var c = scenario.buildingSet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this)
                {
                    action(c);
                }
            }
        }

        public void ForEachTroop(System.Action<Troop> action)
        {
            Scenario scenario = Scenario.Cur;
            for (int i = 0; i < scenario.troopsSet.Count; ++i)
            {
                var c = scenario.troopsSet[i];
                if (c != null && c.IsAlive && c.BelongCorps == this)
                {
                    action(c);
                }
            }
        }

        public void RemoveCity(City target)
        {
            cityCount--;
            if (cityCount <= 0)
            {
                BelongForce.DeleteCorps(this);
            }
            else
            {
                if (mComander.BelongCity == target)
                {
                    mComander?.SetStateNormal();
                    mComander = null;
                    NeedUpdateCommander();
                }
            }
        }

        public void NeedUpdateCommander()
        {
            needUpdateCommander = true;
        }

        public void UpdateCommander()
        {
            needUpdateCommander = false;
            if (IsCaptainCorps) return;

            Person dest = null;
            Official higher = null;
            int commandHigher = 0;
            mComander?.SetStateNormal();
            ForEachPerson((checker) =>
            {
                if (checker.IsPrisoner) { return; }

                if (checker != null && checker.IsAlive)
                {
                    if (dest == null)
                    {
                        dest = checker;
                        higher = dest.Official;
                        commandHigher = dest.Command;
                    }
                    else
                    {
                        if (checker.Official.level > higher.level)
                        {
                            dest = checker;
                            higher = dest.Official;
                            commandHigher = dest.Command;
                        }
                        else if (checker.Official.level == higher.level)
                        {
                            if (checker.Command > commandHigher)
                            {
                                dest = checker;
                                higher = dest.Official;
                                commandHigher = dest.Command;
                            }
                        }
                    }
                }
            });

            mComander = dest;
            if (mComander != null)
            {
                mComander.SetStateCommander();
                mComander.BelongCity?.UpdateNewLeader();
                return;
            }

            // 【原版 district_appoint_totoku 兜底：无人可任 → 整军并入主军团】
            // 对齐 s11_sys_scen_district.cpp:147（district_appoint_totoku）与 `:88-133`（district_dissolve）：
            // 支军团的都督一旦空缺且军团内再也挑不出合适人选，原版会把整个军团解散、
            // 城池并回第 1 军团，而不是留下一个"无都督军团"继续挂在势力下 ——
            // 那种悬空军团会让 CorpsAI 每回合空转（没有都督就没有行动力预算来源）。
            // `DeleteCorps` 内部对 number == 1 直接返回，主军团不受影响。
            if (!IsCaptainCorps)
            {
                Sango.Log.Info($"*[军团]{BelongForce?.Name} 的第 {number} 军团无人可任都督，整军并入主军团");
                BelongForce?.DeleteCorps(this);
            }
        }
    }
}
