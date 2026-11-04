namespace Sango.Core
{
    public class TroopSortFunction : Singleton<TroopSortFunction>
    {
        public delegate string TroopValueStrGet(Troop troop);
        public delegate T TroopValueGet<T>(Troop troop);
        public delegate int TroopSortFunc(Troop troop1, Troop troop2);

        /// <summary>
        /// 设置Troop对象属性值的代理
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="value">新的属性值</param>
        public delegate void TroopValueObjSet(Troop troop, object value);

        public class SortTitle<T> : ObjectSortTitle
        {
            public TroopValueStrGet valueStrGetCall;
            public TroopSortFunc valueSortFunc;
            public TroopValueGet<T> valueGetCall;
            public TroopValueObjSet valueObjSet;

            public override object GetValue(SangoObject obj)
            {
                return valueGetCall.Invoke((Troop)obj);
            }

            public override void SetValue(SangoObject obj, object value)
            {
                valueObjSet?.Invoke((Troop)obj, value);
            }

            public override string GetValueStr(SangoObject obj)
            {
                return valueStrGetCall.Invoke((Troop)obj);
            }

            public override int Sort(SangoObject a, SangoObject b)
            {
                return valueSortFunc.Invoke((Troop)a, (Troop)b);
            }

            public SortTitle<T> Copy()
            {
                return new SortTitle<T>
                {
                    name = name,
                    alignment = alignment,
                    width = width,
                    isNameColumn = isNameColumn,
                    nameExtraChars = nameExtraChars,
                    valueStrGetCall = valueStrGetCall,
                    valueGetCall = valueGetCall,
                    valueSortFunc = valueSortFunc,
                    valueObjSet = valueObjSet,
                    editType = editType,
                    dataSetType = dataSetType,
                    minValue = minValue,
                    maxValue = maxValue,
                    customData = customData,
                };
            }
        }

        public static SortTitle<string> SortByName = new SortTitle<string>()
        {
            isNameColumn = true,        // 名字列：宽度按剧本参数"名字最大支持字数"计算(1.05×字数)
            name = "武将",
            width = 3.2f,
            valueGetCall = x => x.Name,
            valueStrGetCall = x => x.Name,
            valueSortFunc = (a, b) => a.Name.CompareTo(b.Name),
            valueObjSet = (x, v) => x.Name = (string)v,
            editType = DataEditType.Text,
        };

        public static SortTitle<int> SortByDefence = new SortTitle<int>()
        {
            name = "防御",
            width = 2.0f,
            valueStrGetCall = x => x.Defence.ToString(),
            valueGetCall = x => x.Defence,
            valueSortFunc = (a, b) => a.Defence.CompareTo(b.Defence),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByAttack = new SortTitle<int>()
        {
            name = "攻击",
            width = 2.0f,
            valueStrGetCall = x => x.Attack.ToString(),
            valueGetCall = x => x.Attack,
            valueSortFunc = (a, b) => a.Attack.CompareTo(b.Attack),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByMoveability = new SortTitle<int>()
        {
            name = "移动",
            width = 2.0f,
            valueStrGetCall = x => x.MoveAbility.ToString(),
            valueGetCall = x => x.MoveAbility,
            valueSortFunc = (a, b) => a.MoveAbility.CompareTo(b.MoveAbility),
            valueObjSet = null,
        };
        public static SortTitle<int> SortByBuild = new SortTitle<int>()
        {
            name = "建设",
            width = 2.0f,
            valueStrGetCall = x => x.BuildPower.ToString(),
            valueGetCall = x => x.BuildPower,
            valueSortFunc = (a, b) => a.BuildPower.CompareTo(b.BuildPower),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByIntelligence = new SortTitle<int>()
        {
            name = "智力",
            width = 2.0f,
            valueStrGetCall = x => x.Intelligence.ToString(),
            valueGetCall = x => x.Intelligence,
            valueSortFunc = (a, b) => a.Intelligence.CompareTo(b.Intelligence),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByGold = new SortTitle<int>()
        {
            name = "资金",
            width = 2.0f,
            valueStrGetCall = x => x.gold.ToString(),
            valueGetCall = x => x.gold,
            valueSortFunc = (a, b) => a.gold.CompareTo(b.gold),
            valueObjSet = (x, v) => x.gold = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByFood = new SortTitle<int>()
        {
            name = "兵粮",
            width = 2.0f,
            valueStrGetCall = x => x.food.ToString(),
            valueGetCall = x => x.food,
            valueSortFunc = (a, b) => a.food.CompareTo(b.food),
            valueObjSet = (x, v) => x.food = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMorale = new SortTitle<int>()
        {
            name = "气力",
            width = 2.0f,
            valueStrGetCall = x => x.morale.ToString(),
            valueGetCall = x => x.morale,
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMoraleByMax = new SortTitle<int>()
        {
            name = "气力",
            width = 2.0f,
            valueStrGetCall = x => $"{x.morale}/{x.MaxMorale}",
            valueGetCall = x => x.morale,
            valueSortFunc = (a, b) => a.morale.CompareTo(b.morale),
            valueObjSet = (x, v) => x.morale = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByTroops = new SortTitle<int>()
        {
            name = "士兵",
            width = 2.0f,
            valueStrGetCall = x => x.troops.ToString(),
            valueGetCall = x => x.troops,
            valueSortFunc = (a, b) => a.troops.CompareTo(b.troops),
            valueObjSet = (x, v) => x.troops = (int)v,
            editType = DataEditType.IntCalculator,
            minValue = 0,
        };

        public static SortTitle<int> SortByMember1 = new SortTitle<int>()
        {
            isNameColumn = true,        // 名字列：显示副将名，宽度按剧本参数"名字最大支持字数"计算(1.05×字数)
            name = "副将",
            width = 2.0f,
            valueStrGetCall = x => x.Member1 != null ? x.Member1.Name : "",
            valueGetCall = x => x.Member1?.Id ?? 0,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Member1, b.Member1),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByMember2 = new SortTitle<int>()
        {
            isNameColumn = true,        // 名字列：显示副将名，宽度按剧本参数"名字最大支持字数"计算(1.05×字数)
            name = "副将",
            width = 2.0f,
            valueStrGetCall = x => x.Member2 != null ? x.Member2.Name : "",
            valueGetCall = x => x.Member2?.Id ?? 0,
            valueSortFunc = (a, b) => SangoObject.Compare(a.Member2, b.Member2),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongForce = new SortTitle<int>()
        {
            isNameColumn = true,        // 名字列：显示势力名，宽度按剧本参数"名字最大支持字数"计算(1.05×字数)
            name = "势力",
            width = 2.4f,
            valueStrGetCall = x => x.BelongForce?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.BelongForce, b.BelongForce),
            valueGetCall = x => x.BelongForce?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongCorps = new SortTitle<int>()
        {
            // 名字列：显示军团名(第N军团)，宽度按剧本参数"名字最大支持字数"计算；军团名额外预留 4 字
            isNameColumn = true,
            nameExtraChars = 4,
            name = "军团",
            width = 4.0f,
            valueStrGetCall = x => x.BelongCorps?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.BelongCorps, b.BelongCorps),
            valueGetCall = x => x.BelongCorps?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByBelongCity = new SortTitle<int>()
        {
            name = "所属",
            width = 2.4f,
            valueStrGetCall = x => x.BelongCity?.Name ?? "无",
            valueSortFunc = (a, b) => SangoObject.Compare(a.BelongCity, b.BelongCity),
            valueGetCall = x => x.BelongCity?.Id ?? 0,
            valueObjSet = null,
        };

        public static SortTitle<SangoObjectList<Person>> SortByCaptiveCount = new SortTitle<SangoObjectList<Person>>()
        {
            name = "俘虏",
            width = 2.4f,
            valueStrGetCall = x => x.captiveList.Count.ToString(),
            valueSortFunc = (a, b) => a.captiveList.Count.CompareTo(b.captiveList.Count),
            valueGetCall = x => x.captiveList,
            valueObjSet = null,
        };

        public static SortTitle<bool> SortByActionOver = new SortTitle<bool>()
        {
            name = "行动",
            width = 2.4f,
            valueStrGetCall = x => x.ActionOver ? "未行动" : "行动完",
            valueSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueGetCall = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        public static SortTitle<bool> SortByState = new SortTitle<bool>()
        {
            name = "状态",
            width = 2.4f,
            valueStrGetCall = x => x.ActionOver ? "行动完" : "未行动",
            valueSortFunc = (a, b) => a.ActionOver.CompareTo(b.ActionOver),
            valueGetCall = x => x.ActionOver,
            valueObjSet = (x, v) => x.ActionOver = (bool)v,
        };

        /// <summary>
        /// 显示文本转义：把可能被 uGUI 当成富文本解析的字符换成全角，并把换行压成空格。
        ///
        /// 【为什么需要】列表单元格走的是 <c>Text.supportRichText = true</c>，
        /// 值里一旦混进 <c>&lt;color=...&gt;</c>（势力 / 特技 / 部队的 ColorName 都带标签），
        /// 轻则整列被染色，重则标签未闭合把后面几列一起吞掉。
        /// 这里只处理"显示"这一路；排序走 <c>valueSortFunc</c>，用的是原始数据，不受影响。
        /// </summary>
        /// <param name="text">原始文本</param>
        /// <returns>可直接塞进列表单元格的文本</returns>
        public static string EscapeDisplay(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";

            // 换行 / 制表符会把单行单元格撑开，统一压成空格
            text = text.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');

            // 尖括号转全角：保留字面可读性，又不会被富文本解析器当成标签
            return text.Replace('<', '＜').Replace('>', '＞');
        }

        /// <summary>
        /// 部队任务的中文名（枚举 → 显示文本）。
        ///
        /// 【为什么要转义】<c>missionType</c> 是 int 落盘的枚举，直接显示就是"13"这种数字，
        /// 玩家看不出部队在干什么。这里按 <see cref="MissionType"/> 映射成中文，
        /// 并统一走 <see cref="EscapeDisplay"/> 做显示转义。
        /// 与 <c>PersonSortFunction.GetMissionTypeName</c> 同一套做法（人员列表已这么做）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>任务名</returns>
        public static string GetMissionTypeName(Troop troop)
        {
            if (troop == null)
                return "—";
            if (troop.missionType == 0)
                return "无";

            string name;
            switch ((MissionType)troop.missionType)
            {
                // ---- 势力 AI 部队任务 ----
                case MissionType.TroopReturnCity: name = "返城"; break;
                case MissionType.TroopMovetoCity: name = "移动"; break;
                case MissionType.TroopDestroyTroop: name = "攻击部队"; break;
                case MissionType.TroopDestroyBuilding: name = "攻击建筑"; break;
                case MissionType.TroopOccupyCity: name = "占领"; break;
                case MissionType.TroopHarassCity: name = "骚扰"; break;
                case MissionType.TroopBanishTroop: name = "驱逐"; break;
                case MissionType.TroopProtectBuilding: name = "护建筑"; break;
                case MissionType.TroopProtectTroop: name = "护卫"; break;
                case MissionType.TroopProtectCity: name = "守城"; break;
                case MissionType.TroopBuildBuilding: name = "建造"; break;
                case MissionType.TroopFixBuilding: name = "修补"; break;
                case MissionType.TroopTransformGoodsToCity: name = "运输"; break;
                case MissionType.TroopStay: name = "待命"; break;
                case MissionType.TroopMovetoCell: name = "移动"; break;
                case MissionType.TroopMovetoBuild: name = "移动"; break;
                case MissionType.TroopSupplyTroop: name = "补给"; break;
                case MissionType.TroopAskSupply: name = "求援"; break;
                case MissionType.TroopBanditRazeCity: name = "烧城"; break;

                // ---- 委任部队（玩家第一军团）专用任务 ----
                // 与上面同名：对玩家而言"我的部队在做什么"只有一种说法，
                // 委任与否是控制权差异，不该多出一个"委任版任务名"。
                case MissionType.PlayerTroopReturnCity: name = "返城"; break;
                case MissionType.PlayerTroopMovetoCity: name = "移动"; break;
                case MissionType.PlayerTroopDestroyTroop: name = "攻击部队"; break;
                case MissionType.PlayerTroopDestroyBuilding: name = "攻击建筑"; break;
                case MissionType.PlayerTroopOccupyCity: name = "占领"; break;
                case MissionType.PlayerTroopBanishTroop: name = "驱逐"; break;
                case MissionType.PlayerTroopBuildBuilding: name = "建造"; break;
                case MissionType.PlayerTroopFixBuilding: name = "修补"; break;
                case MissionType.PlayerTroopTransformGoodsToCity: name = "运输"; break;
                case MissionType.PlayerTroopMovetoCell: name = "移动"; break;

                default: name = ((MissionType)troop.missionType).ToString(); break;
            }

            return EscapeDisplay(name);
        }

        /// <summary>
        /// 部队任务目标的可读名（城名 / 部队名 / 建筑名 / 建筑类型 + 坐标）。
        ///
        /// 【为什么必须按任务类型解析】<c>missionTarget</c> 只是一个 int，
        /// 它指向哪个表完全由 <c>missionType</c> 决定（各任务行为类的 Prepare 里各取各表）：
        /// <list type="bullet">
        /// <item>城类任务 → <c>scenario.citySet</c>（城 / 关 / 港统一建模）</item>
        /// <item>攻击、驱逐、补给、求援 → <c>scenario.troopsSet</c>（友军部队 Id）</item>
        /// <item>攻击建筑、修补 → <c>Building</c></item>
        /// <item>建造、移动到建筑 → <c>BuildingType</c>，真实位置在 <c>missionTargetCell</c></item>
        /// <item>移动到格子 → 坐标存在 <c>missionParams1/2</c>（x / y），<c>missionTarget</c> 恒为 0</item>
        /// </list>
        /// 直接显示 <c>missionTarget</c> 的数字对玩家没有意义（"目标 37"）。
        /// </summary>
        /// <param name="troop">部队</param>
        /// <returns>目标名</returns>
        public static string GetMissionTargetName(Troop troop)
        {
            if (troop == null || troop.missionType == 0)
                return "无";

            Scenario scenario = Scenario.Cur;
            if (scenario == null)
                return EscapeDisplay(troop.missionTarget.ToString());

            switch ((MissionType)troop.missionType)
            {
                // ---- 目标是一处据点 ----
                case MissionType.TroopReturnCity:
                case MissionType.PlayerTroopReturnCity:
                case MissionType.TroopMovetoCity:
                case MissionType.PlayerTroopMovetoCity:
                case MissionType.TroopProtectCity:
                case MissionType.TroopOccupyCity:
                case MissionType.PlayerTroopOccupyCity:
                case MissionType.TroopTransformGoodsToCity:
                case MissionType.PlayerTroopTransformGoodsToCity:
                case MissionType.TroopBanditRazeCity:
                    return EscapeDisplay(GetCityName(scenario, troop.missionTarget));

                // ---- 目标是一支部队 ----
                case MissionType.TroopDestroyTroop:
                case MissionType.PlayerTroopDestroyTroop:
                case MissionType.TroopBanishTroop:
                case MissionType.PlayerTroopBanishTroop:
                case MissionType.TroopSupplyTroop:
                case MissionType.TroopAskSupply:
                    return EscapeDisplay(GetTroopName(scenario, troop.missionTarget));

                // ---- 目标是一座建筑 ----
                case MissionType.TroopDestroyBuilding:
                case MissionType.PlayerTroopDestroyBuilding:
                case MissionType.TroopFixBuilding:
                case MissionType.PlayerTroopFixBuilding:
                    return EscapeDisplay(GetBuildingName(scenario, troop.missionTarget));

                // ---- 目标是"建筑类型 + 建址" ----
                case MissionType.TroopBuildBuilding:
                case MissionType.PlayerTroopBuildBuilding:
                case MissionType.TroopMovetoBuild:
                    {
                        BuildingType buildingType = scenario.GetObject<BuildingType>(troop.missionTarget);
                        string typeName = buildingType != null ? buildingType.Name : troop.missionTarget.ToString();
                        return EscapeDisplay(AppendCellPos(typeName, troop.missionTargetCell));
                    }

                // ---- 目标是地图坐标（missionTarget 恒为 0，坐标在 missionParams1/2）----
                case MissionType.TroopMovetoCell:
                case MissionType.PlayerTroopMovetoCell:
                    {
                        int x = troop.missionParams1;
                        int y = troop.missionParams2;
                        if (x < 0 || y < 0)
                            return "—";
                        return EscapeDisplay($"{x},{y}");
                    }

                default:
                    // 待命 / 已停用的护卫任务等：没有独立目标，只有一个可能为 0 的 Id
                    return troop.missionTarget > 0 ? EscapeDisplay(troop.missionTarget.ToString()) : "—";
            }
        }

        /// <summary>按 Id 取据点名（城 / 关 / 港统一建模），取不到时退回 Id 文本</summary>
        static string GetCityName(Scenario scenario, int id)
        {
            if (scenario == null || scenario.citySet == null)
                return id.ToString();
            City city = scenario.citySet.Get(id);
            return city != null ? city.Name : id.ToString();
        }

        /// <summary>按 Id 取部队名；刻意不用 ColorName，避免带出富文本标签</summary>
        static string GetTroopName(Scenario scenario, int id)
        {
            if (scenario == null || scenario.troopsSet == null)
                return id.ToString();
            Troop troop = scenario.troopsSet.Get(id);
            return troop != null ? troop.Name : id.ToString();
        }

        /// <summary>按 Id 取建筑名，取不到时退回 Id 文本</summary>
        static string GetBuildingName(Scenario scenario, int id)
        {
            if (scenario == null)
                return id.ToString();
            Building building = scenario.GetObject<Building>(id);
            return building != null ? building.Name : id.ToString();
        }

        /// <summary>把"名称(坐标)"拼成一段文本；没有坐标时只返回名称</summary>
        static string AppendCellPos(string name, Cell cell)
        {
            if (cell == null)
                return name;
            return $"{name}({cell.x},{cell.y})";
        }

        /// <summary>
        /// 任务列：按 <c>missionType</c> 排序，显示中文任务名。
        /// 原先 <c>UITroopInformation</c> 的"任务"栏错用了俘虏数量列，本列即为其正确落点。
        /// </summary>
        public static SortTitle<int> SortByMissionType = new SortTitle<int>()
        {
            name = "任务",
            width = 2.4f,
            valueStrGetCall = x => GetMissionTypeName(x),
            valueSortFunc = (a, b) => a.missionType.CompareTo(b.missionType),
            valueGetCall = x => x.missionType,
            valueObjSet = (x, v) => x.missionType = (int)v,
        };

        /// <summary>
        /// 任务目标列：显示目标可读名，按**显示文本**排序（目标跨城 / 部队 / 建筑三类，
        /// 按 Id 数值排没有意义）。
        /// </summary>
        public static SortTitle<string> SortByMissionTarget = new SortTitle<string>()
        {
            name = "目标",
            width = 2.8f,
            valueStrGetCall = x => GetMissionTargetName(x),
            valueSortFunc = (a, b) => string.Compare(GetMissionTargetName(a), GetMissionTargetName(b), System.StringComparison.Ordinal),
            valueGetCall = x => GetMissionTargetName(x),
            valueObjSet = null,
        };

        public static SortTitle<int> SortByTroopType = new SortTitle<int>()
        {
            name = "兵种",
            width = 2.4f,
            valueStrGetCall = x => x.TroopType.Name,
            valueSortFunc = (a, b) => a.TroopType.Id.CompareTo(b.TroopType.Id),
            valueGetCall = x => x.TroopType.Id,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByAbility = new SortTitle<int>()
        {
            name = "适应",
            width = 2.4f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.TroopTypeLv),
            valueSortFunc = (a, b) => a.TroopTypeLv.CompareTo(b.TroopTypeLv),
            valueGetCall = x => x.TroopTypeLv,
            valueObjSet = null,
        };

        public static SortTitle<int> SortByWaterAbility = new SortTitle<int>()
        {
            name = "水军",
            width = 2.4f,
            valueStrGetCall = x => Scenario.Cur.Variables.GetAbilityName(x.WaterTroopTypeLv),
            valueSortFunc = (a, b) => a.WaterTroopTypeLv.CompareTo(b.WaterTroopTypeLv),
            valueGetCall = x => x.WaterTroopTypeLv,
            valueObjSet = null,
        };
    }

}
