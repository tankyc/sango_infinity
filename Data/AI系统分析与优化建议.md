# Sango Infinity（三国志11 PK 版）AI 系统梳理与优化建议

> 分析范围：`Project/Assets/Sango/Scripts/Game/` 下全部 AI 相关代码
> 分析时间：2026-09-19

---

## 一、AI 系统总览

项目的 AI 采用 **四层指挥链**，全部基于统一的 `SangoObject.DoAI(Scenario)` 入口 + **命令队列（策略模式 / 责任链）** 驱动，且每层都遵守「逐帧推进」协议（返回 `false` 表示本帧未完成，下一帧继续）。

```
Force(势力)
 └─ Run() → DoAI()
     └─ AICommandList : Func<Force,Scenario,bool>
         ├─ ForceAI.AICaptives        俘虏招降/释放/赎回
         ├─ ForceAI.AITechniques      科技研发
         ├─ ForceAI.AISetOfficial     官职任命
         ├─ ForceAI.AITransfromPerson 跨城人事调动
         └─ (ForceAI.AIDiplomacy)     外交 —— 已被注释停用
     └─ 遍历 Corps
         └─ Corps(军团) → Run() → DoAI()
             └─ AICommandQueue
                 ├─ CorpsAI.AITransfromPerson  一级军团向其他军团派将
                 ├─ CorpsAI.AICities           遍历城市 City.DoAI()
                 └─ CorpsAI.AITroops           遍历部队 Troop.DoAI()

City(城市)  [CityAI]
 └─ AIPrepare()  触发 GameEvent.OnCityAIPrepare
 └─ AICommandList : Func<City,Scenario,bool>   ← 由多个系统在事件中填充
     ├─ CityAI.AIRewardPerson / AIAttack / AITradeFood / AIIntrior
     ├─ CityAI.AITransfrom / AITrainTroop / AIRecruitTroop / AICreateItems
     ├─ CityAI.AISecurity / AISearching / AIRecruitPerson
     ├─ TechniqueResearch.AIResearch        （独立系统注入）
     └─ AITransfromToBelongCity             （Port/Gate 注入）

Troop(部队)  [TroopMissionBehaviour 策略模式]
 └─ DoAI() → TroopMissionBehaviour.Create(missionType)
     ├─ TroopOccupyCity   攻占城池
     ├─ TroopDestroyTroop 歼灭敌军
     ├─ TroopDestroyBuilding 破坏建筑
     ├─ TroopBuildBuilding / TroopFixBuilding 建造/修复
     ├─ TroopMovetoCity / TroopMovetoCell / TroopMovetoBuild 行军
     ├─ TroopReturnCity / TroopTransformGoodsToCity 返城/运输
     ├─ TroopProtectCity / TroopProtectTroop / TroopProtectBuilding 护卫
     └─ TroopBanishTroop / TroopStay
     ↑ 评分核心：TroopAIUtility.PriorityAction()
```

### 关键文件清单

| 文件 | 职责 |
|---|---|
| `Game/Object/Force/ForceAI.cs` (91.6KB) | 势力决策、个性系统、外交、俘虏、科技、**军师推荐算法** |
| `Game/Object/Force/Force.cs` | 势力回合调度、`AICommandList` 装配 |
| `Game/Object/Corps/CorpsAI.cs` | 军团级城市/部队调度、人事派遣 |
| `Game/Object/City/CityAI.cs` (64KB) | 城市内政、军事、运输、造兵全部逻辑 |
| `Game/Object/Troop/TroopAIUtility.cs` | **技能/目标评分、安全评估、寻路辅助** |
| `Game/Object/Troop/TroopMissionBehaviour.cs` | 部队任务基类与工厂 |
| `Game/Object/Troop/Troop*.cs` | 各部队任务行为 |
| `Game/System/Game/CityAIResourceAddSystem.cs` | 电脑城池每回合资源补充 |
| `Game/System/Game/PlayerDoTroopAI.cs` | 玩家「委任部队行动」 |
| `Game/Debate/DebateAI.cs`、`Game/Duel/DuelAI.cs` | 辩论 / 单挑 AI |
| `Game/GameAIDebug.cs` | AI 可视化调试（移动范围、路径、逐帧） |

### 两层「个性」体系

1. **`ForceAI.AIPersonalityType`**：`Aggressive / Defensive / Diplomatic / Economic / Balanced`，由君主 `Personality` 的四项倾向值择优得出。
2. 已实际接入决策的只有：外交成功率/投入、科技权重、攻击兵力阈值。**城市内政、部队战术基本未使用个性**。

---

## 二、AI 运行机制要点

### 1. 部队 AI 的评分核心 `TroopAIUtility.PriorityAction`

对「技能 × 可达格 × 施法范围格 × 命中格」做四重遍历，逐格累加攻防评分，最后以权重随机返回一个 `PriorityActionData`：

- `SkillStatusPriority`：伤害/策略成功率/技能消耗/射程/单体加成 的乘积式评分；
- `SkillDefencePriority`：**目前恒返回 0（TODO 未完成）**，等于 AI 完全忽略「被反击风险」；
- 评分结果为负（如可能误伤友军）直接丢弃；多目标技能命中不足 2 个单位则分数减半。

### 2. 城市 AI 的装配方式

`City.AIPrepare()` 只做通用准备，具体命令由 **事件订阅者** 注入：

- `BuildingWorking`（工作制）→ `OnCityAIPrepare`
- `ClassicsCityWorking`（经典内政）→ `OnCityAIPrepare`
- `TechniqueResearch` → `city.AICommandList.Add(AIResearch)`
- `Port` / `Gate.AIPrepare()` → `AIAttack` + `AITransfromToBelongCity`

内政模式由 `BuildingWorking.selectedWorkingType`（0=工作制 / 1=经典内政）二选一切换，并随存档保存。

### 3. 势力 AI 的个性取值

`GetAIPersonality` 取四项倾向的最大值；**并列时按 war→defense→diplomacy→economic 的顺序短路**，因此平局必定判为 `Aggressive`。

---

## 三、发现的问题（按类别）

### A. 死代码 / 停用功能

| 位置 | 问题 | 影响 |
|---|---|---|
| `TroopAI.cs` 全文（377 行） | 整个文件被 `//` 注释，功能已被 `TroopMissionBehaviour` 取代 | 冗余文件，易误导 |
| `ForceAI.AIDiplomacy`（约 300 行） | 完整实现但被 `Force.cs:644` 注释停用 | **AI 势力完全不会主动外交**（结盟/停战/送礼/通商/和亲都不会做） |
| `TroopAIUtility.SkillAttackPriority` | 整段注释 | 被反击评估缺失 |
| `TroopAIUtility.SkillDefencePriority` | `return 0` + TODO | AI 不考虑走位挨打风险 |
| `City.AIPrepare` 内约 30 行 | 命令列表写死又被注释 | 逻辑来源混乱 |
| `CityAI.AIIntrior` | `AISearching/AIRecruitPerson/AITrainTroop` 等调用被注释 | 仅剩 `AIBuilding` 生效，靠事件列表补 |
| `ForceAI.AITransfromPerson` 尾部 ~50 行 | 旧实现注释 | 污染阅读 |
| `Troop.cs AIPrepare` | 「永不退缩」逻辑注释 | AI 不会因低士气/断粮主动撤退 |
| `CityAI.AITroopLevelUp/Merge/Train` | 空方法注释 | 部队等级/合并/训练 AI 缺失 |

### B. 潜在缺陷与健壮性风险

1. **空引用风险（未判空数组首元素）**
   - `CityAI.AITransfrom`：`Person leader = persons[0];`（`persons` 可能 null）
   - `CityAI.AIMakeTransportTroop`：`Person leader = persons[0];`
   - `CityAI.AIMakeTroop`：`troop.Leader = people[0];`
   - `TroopMovetoCity.IsMissionComplete` / `Prepare`：`TargetCity` 可能为 null 即调用 `IsSameForce`

2. **静态共享可变量**
   `TroopAIUtility` 的 `spellRangeCells / attackCells / tempTargets / checkList / wightList` 与 `TroopDestroyTroop.wightList` 均为 `static`。当前单线程串行「准备-执行」尚可，但：
   - 一旦引入并行/重入（如城市 AI 处理部队 A 未结束时又处理部队 B），**容器会被清空复用**；
   - `priorityActionData` 跨帧持有引用，若未来把中间列表改为对象池回收，将出现悬垂数据。

3. **魔法数字硬编码**
   - `SkillStatusPriority` 用 `skill.Id == 22 / 23` 判断「火计 / 灭火」；
   - 攻击兵力阈值 `20000 / 8000 / 12000 / 10000`、`money 2000/3000/5000`、`loyalty > 70`、`succ < 75` 等散落各处，无统一配置表。

4. **随机性过强导致行为不稳定**
   - `CityAI.AIAttack`：`city.AttackTroopsCount < GameRandom.Range(8, 30)` 每次判定门槛都变；
   - 大量 `GameRandom.Chance(...)` 直接决定是否行动，缺少「至少执行一次保底」。

5. **个性平局短路**
   `GetAIPersonality` 平局必判 `Aggressive`，会让「无明显倾向」的君主偏侵略。

6. **`AISetOfficial` 随机任命**
   `match_officials[GameRandom.Range(count)]` 纯随机，高能力武将可能被安排到不匹配官位。

7. **内政模式事件订阅互斥脆弱**
   `BuildingWorking` 与 `ClassicsCityWorking` 都订阅 `OnCityAIPrepare`，靠 `selectedWorkingType` 在 `OnScenarioInit` 中手动 `ScenarioClear/ScenarioInit` 切换。若存档读取顺序或异常路径漏掉一次切换，会出现**命令重复注入 → 同一行动被执行多次**。

### C. 性能问题

1. **`PriorityAction` 四重循环是全项目 AI 最大热点**
   O(技能数 × 可达格 × 施法范围 × 命中格)，且内层 `checkList.Find` 为线性扫描，近似 O(n²)。
   - 建议：按「技能 → 命中目标集合」建哈希去重；可达格中只保留「朝向目标方向的子集」；对同一 (skill, targets) 复用评分。

2. **重复全量遍历**
   - `ForceAI.CalculateForcePower` 连续 3 次 `ForEachCity`（每次都遍历整个 `citySet`）；
   - `ForceAI.AIDiplomacy` 连续 6 次遍历 `NeighborForceList`；
   - `Corps.ForEachCity/Person/Building` 每次都全表扫描 `scenario.citySet/personSet`，未走索引。

3. **即时分配**
   - `EvaluateCityDefenceStrength` 每次 `new List<Cell>()`；
   - `EvaluateCellSafety` 每次使用 lambda 闭包（`SpiralAction`）；
   - `Troop.TryMoveToCell` 每次 `new PriorityQueue<Cell>()`。

4. **逐帧推进的渲染耦合**
   部队 AI 移动/施法依赖 `RenderEvent` 完成才继续，部队数量大时单个 AI 回合时间线性膨胀。

### D. 架构 / 可维护性

1. **两种内政系统并存**，职责与事件订阅重叠，后续新增 AI 行为时容易「加错订阅者」。
2. **命令队列用 `List` + `RemoveAt(0)`**（O(n) 搬移），应改 `Queue<T>` 或索引游标。
3. **`city.TroopMissionType`（internal 字段）与 `MissionType` 枚举**混用，城市 AI 与部队任务耦合。
4. **AI 参数无集中配置**，不可通过 Mod / JSON 调参，与项目「外部数据用 JSON」的规则不符。
5. **个性未贯穿决策**：`AIPersonalityType` 只影响少数几处，城市内政/部队战术仍是「一刀切」。
6. **威胁评估模型分散**：`virtualFightPower`、`FightPower`、`EnemyCount` 各自为政，缺少统一「战场态势」对象。
7. **命名不一致**：`missionType`（int）与 `MissionType`（enum）并存；`TroopAIUtility` 部分方法缺中文注释。

---

## 四、优化建议（按优先级）

### P0 — 正确性 / 稳定性（建议立即处理）

1. **补全空引用防护**
   对 `CounsellorRecommendTransportTroop / MakeTroop / RecommendBuild` 等返回值统一判空，或让这些推荐函数在无解时**返回空数组而非 null**，从源头上杜绝 `[0]` 越界。
2. **删除 `TroopAI.cs` 死文件**（含 `.meta`），避免误引用。
3. **明确 `AIDiplomacy` 的取舍**：要么在 `Force.AIPrepare` 恢复加入（推荐，否则 AI 缺乏外交博弈），要么彻底删除实现并移除 `DiplomacyImmunityTime` 等只为它服务的字段。
4. **修复 `GetAIPersonality` 平局**：四项倾向都 `<= 0` 或全相等时返回 `Balanced`，避免全判 `Aggressive`。
5. **`TroopMovetoCity` 增加 `TargetCity == null` 兜底**（切回 `TroopReturnCity`）。

### P1 — 性能优化

6. **重构 `PriorityAction`**：
   - 用 `Dictionary<(skill, targetHash), PriorityActionData>` 去重，去掉 `checkList.Find` 线性扫描；
   - 只评估「移动后能进入施法范围」的可行格（预剪枝），而非全部可达格；
   - 把 `static` 容器改为 `[ThreadStatic]` 或方法内局部（配合容量复用），消除重入隐患。
7. **合并重复遍历**：
   - `CalculateForcePower` 三次 `ForEachCity` → 一次循环同时累计城市数/兵力/金钱；
   - `AIDiplomacy` 六次 `NeighborForceList` → 一次遍历 + 决策优先级数组。
8. **缓存与池化**：
   - `EvaluateCityDefenceStrength` 复用静态 `List<Cell>`；
   - `TryMoveToCell` 复用 `PriorityQueue`；
   - `SpiralAction` 传对象/委托缓存替代闭包。
9. **势力/军团遍历改订阅式索引**：维护 `force.cities / corps.cities` 增量列表，替代全表 `citySet` 扫描。

### P2 — AI 智能与体验

10. **完成 `SkillDefencePriority`**：把 `Troop.GetAttackBackFactor` 的被反击期望伤害纳入评分，使 AI 会规避「贴脸打高反击单位」。
11. **引入集中式 AI 参数配置（JSON）**
    新建 `AIConfig.json`：攻击兵力阈值、内政优先级权重、外交期望值、技能评分系数等，运行时加载，支持 Mod 调参（符合项目 JSON 约定）。
12. **让个性贯穿全链**：为每类 AI 行为定义「个性权重组」，如 `Aggressive` 提高攻击/募兵权重、`Economic` 提高农商/运输权重，城市与部队决策都读同一 `AIPersonalityType`。
13. **加入撤退/保存实力逻辑**：恢复被注释的「低士气/断粮撤退」判断，并考虑兵力比过劣时主动回城。
14. **`AISetOfficial` 改为能力匹配**：按官职需求属性（武力/统率/智力）做加权最优分配，而非随机。
15. **消除随机门槛抖动**：把 `GameRandom.Range(8,30)` 改为「基于敌我兵力比的确定性曲线 + 少量随机扰动」，保证行为可预测、可复现。
16. **统一战场态势模型**：新增 `BattleSituation`（我方/敌方兵力、距离、援军、补给）供 Force/Corps/City/Troop 复用，减少各层重复计算。

### P3 — 架构与可维护性

17. **内政系统事件订阅显式互斥**：由 `BuildingWorking` 统一持有模式开关，`ClassicsCityWorking` 不再各自订阅，改为被 `BuildingWorking` 显式调用，杜绝重复注入。
18. **命令队列统一改 `Queue<T>`**（`Force/Corps/City` 三处一致），并抽出公共基类 `AIController`。
19. **清理注释死代码**：`ForceAI.AITransfromPerson` 尾部、`City.AIPrepare`、`CityAI.AIIntrior`、`TroopAIUtility` 中的注释块统一删除（Git 已有历史可回溯）。
20. **补全中文注释**：`TroopAIUtility` 部分公开方法、`Port/Gate` 的 AI 方法需补齐，达到项目 30% 注释要求。
21. **消除魔法数字**：`skill.Id == 22/23` 改为 `SkillType.Fire / ExtinguishFire` 语义常量。
22. **统一命名**：`missionType`(int) 逐步收敛到 `MissionType` 枚举访问器。

---

## 五、快速收益清单（建议优先落地）

| 序号 | 事项 | 预计收益 | 风险 |
|---|---|---|---|
| 1 | 推荐函数统一非 null 返回 + 判空 | 消除偶发崩溃 | 低 |
| 2 | 删除 `TroopAI.cs` 与各注释死代码 | 可读性↑ | 低 |
| 3 | 重构 `PriorityAction` 去重+剪枝 | AI 回合耗时显著↓ | 中 |
| 4 | 合并重复遍历 | CPU↓ | 低 |
| 5 | 完成 `SkillDefencePriority` | 部队走位更合理 | 中 |
| 6 | 引入 `AIConfig.json` | 可调参、可 Mod | 低 |
| 7 | 个性贯穿决策 | 势力行为差异化 | 中 |
| 8 | 恢复/删除 `AIDiplomacy` | AI 外交完整性 | 低 |

---

## 六、结论

整体架构方向正确：**四层指挥链 + 命令队列 + 任务策略模式** 是清晰且可扩展的设计，`TroopMissionBehaviour` 的策略化重构（取代旧 `TroopAI`）是良性演进。

当前主要短板集中在三块：
1. **历史注释残留多**，功能开关（尤其 `AIDiplomacy`）处于「半成品」状态；
2. **`PriorityAction` 评分算法是性能与智能的双重瓶颈**（缺少被反击评估、四重循环）；
3. **AI 参数硬编码、个性未贯穿**，导致行为趋同且难以调优。

建议按 **P0 稳定性 → P1 性能 → P2 智能 → P3 架构** 的顺序推进，其中「推荐函数判空」与「PriorityAction 重构」投入产出比最高。
