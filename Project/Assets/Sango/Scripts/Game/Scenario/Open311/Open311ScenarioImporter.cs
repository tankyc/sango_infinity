using System;
using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 把原版剧本数据灌进 Sango 的内存 <see cref="Scenario"/>。
    ///
    /// 【用法】
    ///   var data = new Open311ScenarioData();
    ///   Open311ScenarioParser.ParseScen(scenPath, data, out err);
    ///   Open311ScenarioParser.ParseScenario(scenarioPath, data, out err);
    ///   Open311ScenarioImporter.Apply(data, scenario, out err);
    ///   Scenario.StartScenario(scenario);      // 走现有开局流程
    ///
    /// 【前提】scenario 已由 Scenario.json 反序列化（地图 / 坐标 / 通用数据沿用骨架），
    /// 本类只覆盖"数值与归属"。
    ///
    /// 【Id 偏移】原版 0 起，Sango 1 起，统一 +1。
    /// 【坐标偏移】Sango 地图 256×256、原版 200×200，原版坐标 -28 后居中落位。
    ///
    /// 【字段对应关系（已核对 Person 的 96 个字段）】
    ///   ketsuen_            → consanguinity
    ///   kanshitsu_（漢室）  → kanshitsu
    ///   giri_（义理 0~4）   → ideal（霸道/王道/我道/割據/義俠，同为 5 档）
    ///   local_affiliation_  → barbarianTribe（异民族）
    ///   voice_ / tone_      → voice / tone（同名同义）
    ///   mibun_ → type、rank_ → OfficialId、kouseki_ → merit、wadai_ → wadai
    ///
    ///   generation_         → generation（血缘世代，Person 已补字段）
    ///   strategic_tendency_ → strategic_tendency（战略倾向，Person 已补字段）
    ///   promotion_          → promotion（晋升意向，Person 已补字段）
    ///
    /// 【3D 相关，丢弃】old_age_ / skeleton_ / body[8] / left_weapon_ / right_weapon_ / horse_
    ///   —— Sango 用 Prefab 表现，无逐人模型部件字段。
    /// </summary>
    public static class Open311ScenarioImporter
    {
        /// <summary>
        /// 坐标偏移：Sango 坐标 = 原版坐标 + MapOffset。
        ///
        /// 原版地图 200×200，Sango 用 DefaultMap 256×256，居中放置 →
        /// 四周各留 (256-200)/2 = 28 的边距，所以是 **+28**。
        /// （原版坐标取值范围 0~199，+28 后 28~227，完整落在 0~255 内。）
        /// </summary>
        public const int MapOffset = 28;

        /// <summary>
        /// 势力默认开放的科技树根节点。
        ///
        /// 与 <see cref="Scenario.ApplyShortScenario"/> 里给自建势力用的完全一致
        /// （`{ 1, 5, 9, 13, 17, 21, 25, 29, 33 }`）。
        /// 骨架剧本的 forceSet 没有配 `InitTechniques_list`（全是 null），
        /// 而 `Force.InitTechniquesTree` 完全依赖它 —— 空数组会让科技树面板为空、无法研发。
        /// </summary>
        static readonly int[] DefaultInitTechniques = { 1, 5, 9, 13, 17, 21, 25, 29, 33 };

        /// <summary>
        /// 关所 / 港口朝向的额外角度偏移（弧度）。
        ///
        /// 自动算法算的是"朝向地形"的世界角度，但模型 Prefab 的正面基准不一定朝世界 +Z。
        /// 若实测发现模型侧对/背对，调这个常量即可（例如 Mathf.PI * 0.5f）。
        /// </summary>
        public static float GatePortRotationOffset = 0f;

        public static int SangoId(int open311Id) { return open311Id + 1; }
        public static int MapX(int x) { return x + MapOffset; }
        public static int MapY(int y) { return y + MapOffset; }

        /// <summary>把原版数据写入 scenario（势力 → 军团 → 城池 → 武将）</summary>
        public static bool Apply(Open311ScenarioData data, Scenario scenario, out string error)
        {
            error = null;
            if (data == null) { error = "原版数据为空"; return false; }
            if (scenario == null) { error = "目标剧本为空"; return false; }

            // 骨架 JSON 的集合 Id **不连续**（corpsSet 缺 22/39~42，forceSet 缺 22/31/36~38），
            // 而导入时按「0 起递增」分配 Id，一旦落到空洞就会拿到 null。
            // 先补占位对象：Id 正确、其余字段默认，后续引用就不会解析成 null。
            if (scenario.corpsSet != null && scenario.corpsSet.objects != null)
            {
                for (int hi = 1; hi < scenario.corpsSet.objects.Length; hi++)
                {
                    if (scenario.corpsSet.objects[hi] != null) continue;
                    Corps holeCorps = new Corps();
                    holeCorps.Id = hi;
                    scenario.corpsSet.objects[hi] = holeCorps;
                }
            }
            if (scenario.forceSet != null && scenario.forceSet.objects != null)
            {
                for (int hi = 1; hi < scenario.forceSet.objects.Length; hi++)
                {
                    if (scenario.forceSet.objects[hi] != null) continue;
                    Force holeForce = new Force();
                    holeForce.Id = hi;
                    scenario.forceSet.objects[hi] = holeForce;
                }
            }

            // 原版 District（0 起）→ Sango Corps Id（1 起）；-1 = 该军团无效
            int[] districtToCorps = BuildDistrictToCorps(data);
            // 原版 District → 所属势力（0 起；-1 = 无）
            int[] districtToForce = BuildDistrictToForce(data);

            ApplyInfo(data, scenario);
            ApplyForces(data, scenario);
            ApplyCorps(data, scenario, districtToCorps, districtToForce);
            ApplyCities(data, scenario, districtToCorps, districtToForce);
            FixGatePortRotation(data, scenario);      // 依赖 Map.CellSet（LoadContent 已加载）
            ApplyPersons(data, scenario, districtToCorps, districtToForce);
            ApplyItems(data, scenario);
            return true;
        }

        // =================================================================================
        // 宝物
        // =================================================================================
        /// <summary>
        /// 原版 Item Id → Sango ItemTypes Id 的偏移。
        ///
        /// Sango 的 ItemTypes.json 前 24 条是兵装（1~12 剑/枪/戟/弓/军马/冲车/木兽/井阑/投石/走舸/楼船/斗舰）
        /// 与兵符（13~24），第 25 条起才是宝物，且顺序与原版 Item 完全一致：
        ///   25 赤兔马、26 的卢、27 爪黄飞电、28 绝影、29~31 大宛马、32 七星宝刀、33 倚天剑 …
        /// 原版 0 赤兔馬 / 1 的盧 / 2 爪黃飛電 / 3 絕影 / 4~6 大宛馬 / 7 七星寶刀 —— 逐项吻合。
        /// </summary>
        const int ItemIdOffset = 25;

        static void ApplyItems(Open311ScenarioData data, Scenario scenario)
        {
            if (scenario.CommonData == null || scenario.CommonData.ItemTypes == null) return;

            for (int i = 0; i < data.Items.Count; i++)
            {
                Open311ItemData src = data.Items[i];
                if (string.IsNullOrEmpty(src.Name)) continue;      // 空槽位

                int sangoItemId = i + ItemIdOffset;
                // `ItemTypes` 已重构为 SangoObjectMap（按 Id 索引），不再是 SangoObjectSet，
                // 所以不能用 `Id2Object<T>(SangoObjectSet<T>, int)`（会 CS0411 推断失败）。
                ItemType type = scenario.CommonData.ItemTypes.Get(sangoItemId);
                if (type == null) continue;                        // Sango 没有对应宝物，跳过

                if (src.OwnerPerson >= 0)
                {
                    // 已装备 / 赐予某武将
                    Person owner = SafeGet(scenario.personSet, SangoId(src.OwnerPerson));
                    if (owner != null && owner.itemStore != null)
                        owner.itemStore.Add(sangoItemId, 1);
                }
                else if (src.LocationCity >= 0)
                {
                    // 存放在某据点
                    City city = SafeGet(scenario.citySet, SangoId(src.LocationCity));
                    if (city != null && city.itemStore != null)
                        city.itemStore.Add(sangoItemId, 1);
                }
            }
        }

        // =================================================================================
        // 剧本头
        // =================================================================================
        static void ApplyInfo(Open311ScenarioData data, Scenario scenario)
        {
            if (scenario.Info == null) return;
            scenario.Info.name = data.ScenName;
            scenario.Info.description = data.ScenDesc;
            scenario.Info.year = data.StartYear;
            scenario.Info.month = data.StartMonth;
            scenario.Info.day = data.StartDay;
        }

        // =================================================================================
        // 势力
        // =================================================================================
        static void ApplyForces(Open311ScenarioData data, Scenario scenario)
        {
            for (int i = 0; i < data.Forces.Count; i++)
            {
                Open311ForceData src = data.Forces[i];
                if (src.Kunshu < 0) continue;                       // 空势力

                Force dst = SafeGet(scenario.forceSet, SangoId(i));
                if (dst == null) continue;

                dst.Governor = SangoId(src.Kunshu);
                dst.Counsellor = src.Gunshi >= 0 ? SangoId(src.Gunshi) : -1;
                dst.Flag = src.Color;
                dst.TitleId = src.Title >= 0 ? src.Title + 1 : 0;   // 原版 TitleId_None = 9
                // 国号：Sango Nations.json 共 84 条，Id 1~84，名称与原版 Kokugou 表逐一对应
                // （1 魏 / 2 吴 / 3 蜀 / 4 晋 / 5 成 / 6 黄巾 / 7 汉 …）
                dst.NationId = src.Kokugou >= 0 ? src.Kokugou + 1 : 0;

                // 科技树：**不导原版位图**（原版 tech_[2] 是 64 位 bitset，Id 空间与 Sango 的
                // Techniques 不保证一一对应）；改用与自建势力完全一致的默认根节点，
                // 否则 InitTechniques 为空 → Force.InitTechniquesTree 生成空科技树 → 无法研发。
                if (dst.InitTechniques_list == null || dst.InitTechniques_list.Length == 0)
                    dst.InitTechniques_list = DefaultInitTechniques;

                // 同盟：同样不导原版 ally_[2] 位图（forceSet 存在空洞 Id，会解析出 null
                // 并触发 "不能添加null元素"）。开局无同盟是原版剧本一的正确状态。
            }
        }

        /// <summary>
        /// 把两段 32 位 bitset 解成 Id 数组（bit0 → Id 1）。
        ///
        /// 【为什么不能只做上限截断】Sango 的表 Id **不连续** ——
        /// 本地骨架 `Scenario.json` 的 forceSet 是
        /// `1~21, 23~30, 32~35, 39~47`（缺 22/31/36/37/38）。
        /// 原版位图一旦命中空洞 Id，`Database.FromArray` 会解析出 null，
        /// 触发 `SangoObjectList.Add(null)` 的 "不能添加null元素" 错误并中断 Prepare。
        ///
        /// 所以这里要求调用方给出 `exists` 判定，逐个过滤。
        /// </summary>
        static int[] UnpackBits(uint lo, uint hi, Func<int, bool> exists)
        {
            List<int> list = new List<int>();
            if (exists == null) return list.ToArray();

            for (int b = 0; b < 32; b++)
            {
                if ((lo & (1u << b)) == 0) continue;
                int id = b + 1;
                if (exists(id)) list.Add(id);
            }
            for (int b = 0; b < 32; b++)
            {
                if ((hi & (1u << b)) == 0) continue;
                int id = 32 + b + 1;
                if (exists(id)) list.Add(id);
            }
            return list.ToArray();
        }

        // =================================================================================
        // 军团（原版 District）
        // =================================================================================
        static int[] BuildDistrictToCorps(Open311ScenarioData data)
        {
            int[] map = new int[data.Districts.Count];
            int next = 1;
            for (int i = 0; i < data.Districts.Count; i++)
                map[i] = data.Districts[i].Force >= 0 ? next++ : -1;
            return map;
        }

        static int[] BuildDistrictToForce(Open311ScenarioData data)
        {
            int[] map = new int[data.Districts.Count];
            for (int i = 0; i < data.Districts.Count; i++)
                map[i] = data.Districts[i].Force;
            return map;
        }

        static void ApplyCorps(Open311ScenarioData data, Scenario scenario, int[] districtToCorps, int[] districtToForce)
        {
            for (int i = 0; i < data.Districts.Count; i++)
            {
                int corpsId = districtToCorps[i];
                if (corpsId < 0) continue;

                Open311DistrictData src = data.Districts[i];
                Corps dst = SafeGet(scenario.corpsSet, corpsId);
                if (dst == null) continue;

                dst.BelongForceId = SangoId(src.Force);
                dst.Comander = src.Totoku >= 0 ? SangoId(src.Totoku) : -1;
                dst.number = src.Number;
                dst.mid_objective = src.MidObjective;
                dst.mid_objective_target = src.MidObjectiveTarget;
            }
        }

        // =================================================================================
        // 城池（城 / 关 / 港）
        // =================================================================================
        static void ApplyCities(Open311ScenarioData data, Scenario scenario, int[] districtToCorps, int[] districtToForce)
        {
            for (int i = 0; i < data.Cities.Count; i++)
            {
                Open311CityData s = data.Cities[i];
                ApplyCityCore(scenario, SangoId(i), s.District, districtToCorps, districtToForce,
                    s.Troops, s.Gold, s.Food, s.BaseMaxHp, s.Energy, s.PublicOrder, s.BaseMaxTroops, s.Equipment,
                    s.Rate);
            }

            int gateBase = data.Cities.Count;
            for (int i = 0; i < data.Gates.Count; i++)
            {
                Open311GatePortData s = data.Gates[i];
                ApplyCityCore(scenario, SangoId(gateBase + i), s.District, districtToCorps, districtToForce,
                    s.Troops, s.Gold, s.Food, s.BaseMaxHp, s.Energy, 0, 0, s.Equipment, 0);
            }

            int portBase = gateBase + data.Gates.Count;
            for (int i = 0; i < data.Ports.Count; i++)
            {
                Open311GatePortData s = data.Ports[i];
                ApplyCityCore(scenario, SangoId(portBase + i), s.District, districtToCorps, districtToForce,
                    s.Troops, s.Gold, s.Food, s.BaseMaxHp, s.Energy, 0, 0, s.Equipment, 0);
            }

            // 实例数据：坐标 / 朝向 / 当前耐久 / 建成状态 / 所属州 / 邻接
            // （BuildingTypes 等静态表已由 Sango 自己维护，这里只同步"这一局的这一格"）
            for (int i = 0; i < data.Buildings.Count; i++)
            {
                City dst = SafeGet(scenario.citySet, SangoId(i));
                if (dst == null) continue;

                Open311BuildingData b = data.Buildings[i];
                dst.x = MapX(b.PosX);
                dst.y = MapY(b.PosY);
                // 注意：原版 dir_ 是 0~5 的六向索引，Sango 的 rot 是弧度，**不能直接赋值**。
                // 城的朝向沿用骨架 JSON 里的值；关所 / 港口稍后由 FixGatePortRotation 按地形精修。
                dst.durability = b.Hp;
                dst.isComplate = b.Completed != 0;

                // 静态表只有 42 个城有（州 + 6 个邻接）；关/港没有该段
                if (i >= data.CityInfos.Count) continue;

                Open311KyotenInfo info = data.CityInfos[i];
                dst.ProvinceId = info.Province >= 0 ? info.Province + 1 : 0;

                List<int> neighbors = new List<int>(info.Neighbors.Length);
                for (int k = 0; k < info.Neighbors.Length; k++)
                    if (info.Neighbors[k] >= 0) neighbors.Add(SangoId(info.Neighbors[k]));

                if (neighbors.Count > 0)
                    dst.NeighborList_list = neighbors.ToArray();
            }
        }

        static void ApplyCityCore(Scenario scenario, int sangoCityId, int districtId,
                                  int[] districtToCorps, int[] districtToForce,
                                  uint troops, uint gold, uint food, int durabilityLimit,
                                  int energy, int publicOrder, uint troopsLimit, uint[] equipment,
                                  int rate)
        {
            City dst = SafeGet(scenario.citySet, sangoCityId);
            if (dst == null) return;

            dst.troops = (int)troops;
            dst.gold = (int)gold;
            dst.food = (int)food;
            if (durabilityLimit > 0) dst.durabilityLimit = durabilityLimit;
            if (energy > 0) dst.energy = energy;
            if (publicOrder > 0) dst.security = publicOrder;
            if (troopsLimit > 0) dst.troopsLimit = (int)troopsLimit;
            if (rate > 0) dst.rate = rate;                       // 原版 City.rate_（规模/等级系数）

            if (districtId >= 0 && districtId < districtToCorps.Length)
            {
                int corpsId = districtToCorps[districtId];
                int forceId = districtToForce[districtId];
                if (corpsId > 0) dst.BelongCorpsId = corpsId;
                if (forceId >= 0) dst.BelongForceId = SangoId(forceId);
            }

            // 兵装库存：itemStore 是「id, 数量」交替
            if (dst.itemStore != null && equipment != null)
            {
                dst.itemStore.Clear();
                for (int k = 0; k < equipment.Length; k++)
                {
                    if (equipment[k] == 0) continue;
                    dst.itemStore.Add(k, (int)equipment[k]);
                }
            }
        }

        // =================================================================================
        // 关所 / 港口 的朝向修正
        // =================================================================================
        /// <summary>
        /// 按地形自动修正关所 / 港口的朝向（<see cref="City.rot"/>，绕 Y 轴弧度）。
        ///
        /// 【为什么必须修正】
        ///   原版 `Building.dir_` 是 0~5 的**六向索引**，而 Sango 的 `rot` 是**弧度**，
        ///   直接 `rot = dir` 会让模型几乎不旋转（0~5 弧度 ≈ 0°~286°，但方向完全对不上）。
        ///
        /// 【规则】
        ///   · **港口**：正面朝水、背对陆地 → 取"水域最多"的方向；
        ///   · **关所**：左右两侧不能是可通行地格，否则形同虚设 → 取"可通行陆地最多"的
        ///     方向作为朝向（即通路方向），这样模型左右（rot ± 60°）落在山/水上形成卡口。
        ///
        /// 【评分】第 1 圈邻居权重 3、第 2 圈权重 1，避免单格噪声导致朝向抖动。
        /// </summary>
        public static void FixGatePortRotation(Open311ScenarioData data, Scenario scenario)
        {
            if (data == null || scenario == null) return;

            Map map = scenario.Map;
            if (map == null || map.CellSet == null) return;     // 地图未加载时跳过

            int cityCount = data.Cities.Count;                  // 42
            int gateCount = data.Gates.Count;                   // 10

            for (int i = cityCount; i < data.Buildings.Count; i++)
            {
                City city = SafeGet(scenario.citySet, SangoId(i));
                if (city == null) continue;

                Cell cell = map.GetCell(city.x, city.y);
                if (cell == null) continue;

                bool isPort = i >= cityCount + gateCount;
                city.rot = CalcGatePortRotation(cell, isPort) + GatePortRotationOffset;
            }
        }

        /// <summary>
        /// 算出某个地格最适合关所 / 港口的朝向（弧度）。
        /// 6 个方向逐一评分，取最高分方向的世界角度。
        /// </summary>
        static float CalcGatePortRotation(Cell cell, bool isPort)
        {
            if (cell == null) return 0f;

            UnityEngine.Vector3 self = cell.Position;
            float bestAngle = 0f;
            int bestScore = int.MinValue;
            bool found = false;

            for (int d = 0; d < 6; d++)
            {
                Cell first = cell.GetNeighbor(d);
                if (first == null) continue;                    // 地图边界

                // 评分：第 1 圈权重 3、第 2 圈权重 1
                int score = 0;
                Cell cur = first;
                for (int r = 1; r <= 2 && cur != null; r++)
                {
                    int weight = r == 1 ? 3 : 1;

                    if (isPort)
                    {
                        // 港口：水域越多越好（正面朝水）
                        if (cur.IsWater) score += weight * 2;
                    }
                    else
                    {
                        // 关所：只统计"可通行陆地"—— 该方向越通畅，越说明这里是通路，
                        // 模型朝向通路方向后，左右两侧（±60°）自然落在山/水上形成卡口。
                        if (!cur.IsWater && cur.moveAble) score += weight;
                    }

                    cur = r == 1 ? cur.GetNeighbor(d) : null;
                }

                if (score <= bestScore) continue;

                UnityEngine.Vector3 delta = first.Position - self;
                if (delta.sqrMagnitude <= 0.0001f) continue;

                bestScore = score;
                bestAngle = UnityEngine.Mathf.Atan2(delta.x, delta.z);
                found = true;
            }

            return found ? bestAngle : 0f;
        }

        // =================================================================================
        // 武将
        // =================================================================================
        static void ApplyPersons(Open311ScenarioData data, Scenario scenario, int[] districtToCorps, int[] districtToForce)
        {
            for (int i = 0; i < data.Persons.Count; i++)
            {
                Open311PersonData src = data.Persons[i];
                Person dst = SafeGet(scenario.personSet, SangoId(i));
                if (dst == null) continue;

                // ---- 姓名 ----
                dst.familyName = src.FamilyName;
                dst.giveName = src.GivenName;
                dst.nickName = src.NickName;

                // ---- 五维 ----
                SetAttribute(dst.command, src.BaseStat[0]);
                SetAttribute(dst.strength, src.BaseStat[1]);
                SetAttribute(dst.intelligence, src.BaseStat[2]);
                SetAttribute(dst.politics, src.BaseStat[3]);
                SetAttribute(dst.glamour, src.BaseStat[4]);

                // ---- 兵种适性 ----
                SetAbility(dst.spearLv, src.Tekisei[0]);
                SetAbility(dst.halberdLv, src.Tekisei[1]);
                SetAbility(dst.crossbowLv, src.Tekisei[2]);
                SetAbility(dst.rideLv, src.Tekisei[3]);
                SetAbility(dst.machineLv, src.Tekisei[4]);
                SetAbility(dst.waterLv, src.Tekisei[5]);

                // ---- 基本属性（只映射 Sango 运行时类上真实存在的字段）----
                dst.compatibility = src.Aishou;
                dst.consanguinity = src.Ketsuen;          // 原版 ketsuen_（血缘组）
                dst.ambition = src.Ambition;
                dst.wadai = src.Wadai;
                dst.birthplace = src.Birthplace;
                dst.merit = src.Kouseki;
                dst.loyalty = src.Loyalty;
                // 身分：原版 MibunEnum 与 Sango PersonStateType **编号不同**，必须逐项映射
                // （原版 0=君主, 1=都督, 2=太守, 3=一般, 4=在野, 5=俘虏, 6/7=未登场/未发现, 8=死亡）
                dst.state = ToPersonState(src.Mibun);
                dst.OfficialId = src.Rank >= 0 ? src.Rank + 1 : -1;   // 原版 rank_（官职）
                dst.yearBorn = src.Birth;
                dst.yearDead = src.Death;
                dst.appearance = src.Appearance;
                dst.deathType = src.DeathType;
                dst.sex = src.Gender;

                // 汉室态度（原版 kanshitsu_）
                dst.kanshitsu = src.Kanshitsu;

                // 理想（原版 giri_ 义理 0~4 → Sango「霸道/王道/我道/割據/義俠」五档，取值域一致）
                dst.ideal = src.Giri;

                // 异民族（原版 local_affiliation_ 地方情缘 → Sango 的异族标记/归属）
                dst.barbarianTribe = src.LocalAffiliation;

                // 血缘世代 / 战略倾向 / 晋升意向（Person 已补齐这三个字段）
                dst.generation = src.Generation;
                dst.strategic_tendency = src.StrategicTendency;
                dst.promotion = src.Promotion;

                // 语音 / 语气（原版 voice_ / tone_ 与 Sango 同名字段语义一致，直接对应）
                dst.voice = src.Voice;
                dst.tone = src.Tone;

                // ---- 图像 ----
                dst.headIconID = src.Portrait;

                // ---- 性格（原版 0~3 → Sango 性格 Id 1~4）----
                if (src.Seikaku >= 0) dst.PersonalityId = src.Seikaku + 1;

                // ---- 亲爱 / 厌恶（原版 personId，Sango 也是 personId +1）----
                dst.LikePersonListId = ToSangoIds(src.Like);
                dst.HatePersonListId = ToSangoIds(src.Hate);

                // ---- 血缘 ----
                // ketsuen 组已在上方写入 consanguinity。
                //
                // father_ / mother_ 需要**逐条过滤**后再写入：
                //   Person.OnScenarioPrepare 会无条件执行 `Father.sonList.Add(this)` /
                //   `Mother.sonList.Add(this)`，而 SangoObjectList.Add 对重复项会报错。
                // 只有下面两类异常会造成"同一 child 加两次到同一 parent"：
                //   ① father == mother（父母指向同一人）
                //   ② father/mother == 自己（自引用）
                // 互指（A 是 B 父、B 是 A 父）不会重复 —— 两个 sonList 各自独立。
                if (src.Father >= 0 && src.Father != i)
                    dst.FatherId = SangoId(src.Father);
                if (src.Mother >= 0 && src.Mother != i && src.Mother != src.Father)
                    dst.MotherId = SangoId(src.Mother);

                // ---- 所属 ----
                // service_ / location_ 是 building_id（0~86），Sango 是 cityId（1~87）
                if (src.Service >= 0)
                {
                    dst.BelongCityId = SangoId(src.Service);
                    dst.CurrentCityId = dst.BelongCityId;
                }

                if (src.DistrictId >= 0 && src.DistrictId < districtToCorps.Length)
                {
                    int corpsId = districtToCorps[src.DistrictId];
                    int forceId = districtToForce[src.DistrictId];
                    if (corpsId > 0) dst.BelongCorpsId = corpsId;
                    if (forceId >= 0) dst.BelongForceId = SangoId(forceId);
                }

                // 保底：君主(1) / 都督(2) 在 Prepare 阶段会读 BelongCorps.BelongForceId，
                // 若军团 Id 不可用会空引用。宁可降级成「一般」，也不让整局开局中断。
                if ((dst.state == 1 || dst.state == 2) && dst.BelongCorpsId <= 0)
                    dst.state = 4;
            }
        }

        /// <summary>
        /// 原版 MibunEnum → Sango PersonStateType。
        ///
        /// | 原版 | 值 | Sango | 值 |
        /// |---|---|---|---|
        /// | Kunshu 君主 | 0 | Governor | 1 |
        /// | Totoku 都督 | 1 | Commander | 2 |
        /// | Taishu 太守 | 2 | Leader | 3 |
        /// | Ippan 一般 | 3 | Normal | 4 |
        /// | Zaiya 在野 | 4 | Unemployed | 5 |
        /// | Horyo 俘虏 | 5 | Prisoner | 6 |
        /// | Mitoujou 未登场 | 6 | Invisible | 8 |
        /// | Mihakken 未发现 | 7 | Invisible | 8 |
        /// | Shibou 死亡 | 8 | Dead | 9 |
        ///
        /// 【为什么必须映射】两边编号错位（原版 1=都督，Sango 1=君主），
        /// 直接赋值会让大量普通武将误走 Governor 分支而空引用。
        /// </summary>
        static int ToPersonState(int mibun)
        {
            switch (mibun)
            {
                case 0: return 1;    // 君主
                case 1: return 2;    // 都督
                case 2: return 3;    // 太守
                case 3: return 4;    // 一般
                case 4: return 5;    // 在野
                case 5: return 6;    // 俘虏
                // 【原版 6 = 未登场，必须落 Invalid(7)，不能并到 Invisible】
                // Person.OnTurnStart 的登场入口是 `state == PersonStateType.Invalid`
                // （见 Person.cs 登场分支），靠 appearance 年份分批把人放出来。
                // 早先这里和 case 7 共用 `return 8`，等于把"未登场"直接标成"未发现"：
                // 他们永远进不了登场流程，只能等玩家探索碰运气，
                // 表现为"剧本里明明有的武将，到年份了也不出现"。
                case 6: return 7;    // 未登场 → Invalid
                case 7: return 8;    // 未发现 → Invisible
                case 8: return 9;    // 死亡
                default: return 4;   // 未知按「一般」处理，避免落进任何特权分支
            }
        }

        /// <summary>
        /// 原版 personId 数组 → Sango Id 数组（+1），并**去重**。
        ///
        /// 【必须去重】原版 like_[5] / hate_[5] 是固定 5 槽，未填满时会出现重复值；
        /// `Person.OnScenarioPrepare` 会用 `FromArray` 逐个 Add，而
        /// `SangoObjectList.Add` 对重复项会报 "不能重复添加" 并中断 Prepare。
        /// </summary>
        static int[] ToSangoIds(int[] src)
        {
            if (src == null) return new int[0];

            HashSet<int> seen = new HashSet<int>();
            List<int> list = new List<int>(src.Length);
            for (int i = 0; i < src.Length; i++)
            {
                if (src[i] < 0) continue;
                int id = SangoId(src[i]);
                if (!seen.Add(id)) continue;
                list.Add(id);
            }
            return list.ToArray();
        }

        /// <summary>
        /// 安全取集合元素。
        ///
        /// `SangoObjectSet&lt;T&gt;` 的索引器直接访问内部 `objects[]`，不做长度检查；
        /// 骨架剧本的集合可能只扩到某个长度（或 Id 有空洞），越界会抛
        /// IndexOutOfRangeException。这里统一兜底，越界视为"该对象不存在"。
        /// </summary>
        static T SafeGet<T>(SangoObjectSet<T> set, int id) where T : SangoObject, new()
        {
            if (set == null) return null;
            T[] arr = set.objects;
            if (arr == null || id < 0 || id >= arr.Length) return null;
            return arr[id];
        }

        static void SetAttribute(PersonAttributeValue v, int value)
        {
            if (v == null) return;
            v.baseValue = value;
            v.UpdateNoAge();
        }

        static void SetAbility(PersonAbilityValue v, int level)
        {
            if (v == null) return;
            v.baseValue = level;
            v.valueExp = 0;
            v.value = level;
        }
    }
}
