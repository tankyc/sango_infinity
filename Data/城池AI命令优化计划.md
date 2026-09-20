# 城池 AI 命令代码优化计划

> 现状：17 条城池 AI 命令已通过 `CityCommandRegistry` 统一注册、`CityOrderWeights` 统一配置权重。
> 本文档针对**每个命令方法自身的代码质量**给出优化计划。

---

## 实施状态（本轮已完成，编译 0 错误 0 警告）

| 组 | 项目 | 状态 | 落地内容 |
|---|---|---|---|
| P0 | G1 空引用判空 | ✅ | `AITransfrom`、`AIMakeTransportTroop`、`AIMakeTroop` 全部补判空 |
| P0 | G4 概率越界 | ✅ | `AISecurity`（原可达 400%）、`AITrainTroop` 概率统一 `Min(100, …)` |
| P0 | G5 返回值语义 | ✅ | `AIBuilding` 使用子调用返回值提前结束循环 |
| P0 | G6 死代码 | ✅ | 删除整文件注释的 `TroopAI.cs`；清理 `AIIntrior` 注释块 |
| P1 | G2 硬编码阈值 | ✅ | 新增 `AIConfig.#region 城市内政`（14 项阈值可 JSON 覆盖） |
| P1 | G4 随机门槛 | ✅ | `AISearching` / `AIRewardPerson` 改为确定性判定 |
| P1 | 单回合上限 | ✅ | 新增 `rewardMaxPersonPerTurn` / `recruitPersonMaxPerTurn` |
| P2 | G3 建部队样板 | ✅ | 新增 `CityTroopFactory.EmitTroop()`，替换 7 处重复样板 |
| P2 | AIAttack 拆分 | ✅ | 拆为 `ContinueMilitaryMission` + `DispatchOccupyTroop` / `DispatchDefenseTroop` / `DispatchDriveOutTroop` / `DecideAttackTarget` |
| P2 | G7 重复代码 | ✅ | 抽 `IsPathClear()`；`AITrainTroop` 双分支合并；`AIMakeTroop` 兵种选择改"最优适应性" |

**新增文件**：`CityTroopFactory.cs`
**新增配置区**：`AIConfig.#region 城市内政`

---

---

## 一、跨命令的通用问题（7 项）

| # | 问题 | 影响 | 建议 |
|---|---|---|---|
| G1 | **空引用风险**：`persons[0]` / `people[0]` 多处直接取首元素 | 偶发 NullReferenceException | 抽 `TryGetFirst()` 统一判空 |
| G2 | **硬编码阈值遍地**：`gold < 500`、`foodLine = 20000`、`troops < 10000` 等 | 无法调参 | 统一收进 `AIConfig` 新增「城市内政阈值」region |
| G3 | **重复样板代码**：`EnsureTroop + CurActiveTroop + Render.UpdateRender + SANGO_DEBUG 日志` 出现 8+ 次 | 维护成本高 | 抽 `CityTroopFactory.Emit(city, troop, logTag)` |
| G4 | **随机门槛导致抖动**：`Chance(80)`、`Chance((100-security)*4)`、`Chance((95-morale)*3/2)` | 行为不可预测、概率可能越界 | 改为确定性评分（概率与态势线性映射并 `Clamp(0,100)`） |
| G5 | **返回值语义混乱**：多数命令恒 `return true`，内部子调用返回值被忽略 | 无法表达"是否做了事" | 统一约定：返回"本命令是否完成本轮决策"，并让内部子调用参与判断 |
| G6 | **死代码残留**：`AIIntrior` 内大段注释调用、`TroopAI.cs` 整文件注释 | 可读性 | 清理 |
| G7 | **重复遍历**：同一集合在一处方法内遍历 2 次（如兵装统计） | 性能 | 一次遍历缓存 |

---

## 二、逐命令优化清单

### 1. `AIRewardPerson`（褒奖）
| 问题 | 位置 | 建议 |
|---|---|---|
| 全局 `Chance(80)` 随机决定是否褒奖 | 约 495 行 | 改为「存在低忠诚武将」时确定触发 |
| `city.gold > 500`、`x.loyalty <= 90` 硬编码 | 499 行 | 入配置 `rewardGoldKeep` / `rewardLoyaltyThreshold` |
| `ForEach` 内对**每个**低忠诚武将都 `JobRewardPerson` | 499-502 行 | 限制每回合 1 人，避免耗尽金钱 |

### 2. `AIAttack`（军事总入口，含 `AICanAttack` / `AICanDefense` / `AIMakeTroop`）
| 问题 | 位置 | 建议 |
|---|---|---|
| **单方法承担 4 类决策**（进攻 / 防守 / 驱逐 / 夺回港关），超长难维护 | 18-160 行 | 拆为 `AIDecideDefense` / `AIDecideDriveOut` / `AIDecideRetakeSubCity` / `AIDecideAttack` |
| `minEquipNeed = 5000/1500`、`maxPersonCount` 三分支魔法数 | `AIMakeTroop` | 入配置 |
| `spType` 兜底 `GameRandom.Range(0, count)` 随机选兵种 | `AIMakeTroop` | 改为按主将适应性排序取最优 |
| `... Find(x => x.validItemId > 0)` 与 while 移除逻辑晦涩 | `AIMakeTroop` | 抽 `SelectBestTroopType()` |
| `persons[0]` 未判空 | `AIMakeTroop` | 判空 |

### 3. `AIReinforce`（支援）
| 问题 | 位置 | 建议 |
|---|---|---|
| `IsEnemiesRound(15)`、`troops < 10000`、`food < 8000`、`freePersons < 2` 硬编码 | 约 265-280 行 | 入配置 `reinforce*` |
| `reinforceCandidates` 为 `static readonly List` | 类字段 | 改为方法内局部或加注释说明单线程前提 |
| `Math.Max(3, defenseEnemyCount + 2)` 魔法数 | `AIAttack` 防守分支 | 入配置 |

### 4. `AITradeFood`（买粮）
| 问题 | 位置 | 建议 |
|---|---|---|
| `city.gold <= 2000`、`expectationFood = troops * mult` 硬编码 | 1370-1384 行 | 入配置 |
| `if (city.JobTradeFood(...)) { }` 空 if 分支 | 1389 行 | 删除空分支 |

### 5. `AIIntrior` / `AIBuilding`（内政建设）
| 问题 | 位置 | 建议 |
|---|---|---|
| **注释掉的死代码块**（`AIResearch` / `AISearching` / `AICreateBoat` 等） | 448-460 行 | 清理 |
| `AIBuilding` 循环调用 `AIBuildIntriore` / `AIBuildingLevelUp` **忽略返回值** | 828-831 行 | 利用返回值提前结束 |
| `count = 1 + (freePersons - 3) / 3` 魔法数 | 824-826 行 | 入配置或注释说明 |
| `buildingFlag = new int[InteriorCellCount]` 每次分配 | `AIBuildingTemplate` 1190 行 | 复用缓存数组 |
| `GameRandom.Range(Market, MilitaryGarrison)` 随机兜底建筑 | 1238 行 | 明确默认建筑类型 |
| `gold < 500`、`buildCount <= 6` 硬编码 | 1143 / 1296 行 | 入配置 |

### 6. `AISecurity`（治安）
| 问题 | 位置 | 建议 |
|---|---|---|
| `Chance((100 - security) * 4)` —— security=0 时概率 400%，**越界** | 1546 行 | `Clamp(0,100)` 或改确定性 |
| `freePersons < 2`、`gold < 400` 硬编码 | 1539 行 | 入配置 |

### 7. `AITrainTroop`（训练）
| 问题 | 位置 | 建议 |
|---|---|---|
| 两套分支都调用 `CounsellorRecommendTrainTroops + JobTrainTroops`，**代码重复** | 1567-1581 行 | 合并为「先算概率，再统一执行」 |
| `morale < 50`、`Chance((95-morale)*3/2)` 硬编码 | 1567 / 1575 行 | 入配置 |

### 8. `AIRecruitTroop`（募兵）
| 问题 | 位置 | 建议 |
|---|---|---|
| 兵装统计 `for (2..5)` 与后续判断**可能重复遍历** | 1332-1342 行 | 一次循环缓存 `totalNum` |
| `Chance(80)` / `Chance(60)` 委任随机门槛 | 1322-1328 行 | 改确定性（"轻视士兵"→ 直接降优先级，由排序体现） |
| `expectationTroops = Max(food/2, totalNum * mult / 2)` 公式晦涩 | 1342 行 | 加注释 + 入配置 |
| `city.troops > city.food` 判定可疑（量纲不同） | 1351 行 | 复核业务意图后改为 `troops > food * ratio` |

### 9. `AICreateItems`（造兵装）
| 问题 | 位置 | 建议 |
|---|---|---|
| `GetFreeBuilding` 对每类建筑各调一次 | 约 1600 行 | 抽 `GetFreeBuildings()` 一次返回 |
| `for itemTypeId 2..5` 重复出现 | 约 1595 / 1620 行 | 抽常量区间 |

### 10. `AICreateBoat` / `AICreateMachine`（造船 / 造器械）
| 问题 | 位置 | 建议 |
|---|---|---|
| 军团委任开关判断 + `IsValid(force)` 兜底逻辑重复 | 1686-1760 行 | 抽 `TryGetCreateItemType()` |
| `monsterNum/towerNum` 对比挑选逻辑晦涩 | `AICreateMachine` | 加注释或改评分 |

### 11. `AISearching`（搜索）
| 问题 | 位置 | 建议 |
|---|---|---|
| `(invisible>0 && free>0 && Chance(80)) \|\| Chance(20)` 语义混乱 | 476 行 | 改为单一明确概率（如 `Chance(60)`） |
| `recommandSearchingFeatrues` 静态数组 | 464 行 | 保持（无问题），仅加注释 |

### 12. `AIRecruitPerson`（招募武将）
| 问题 | 位置 | 建议 |
|---|---|---|
| 倒序循环中**对每个在野武将**都调用 `JobRecruitPerson` | 516-527 行 | 限制每回合 1-2 人（受空闲武将数约束） |
| `//TODO: 招募其他势力的武将` 遗留 | 528 行 | 保留或移除 |

### 13. `AITransfrom` / `AITransfromToBelongCity` / `AIMakeTransportTroop`（运输）
| 问题 | 位置 | 建议 |
|---|---|---|
| **`persons[0]` 未判空** | 621-622 行 | 判空 |
| `goldLine = 2500` / `foodLine = 20000` / `troops < 500` 硬编码 | 708-742 行 | 入配置 |
| `IsEnemiesRound()`（无参 = 任意距离）语义过宽 | 686 行 | 明确距离阈值 |
| 「检查通路」代码在两个方法中**重复** | 732-739 / `TroopMovetoCity` | 抽 `IsPathClear(from, to)` |
| `95/100`、`9/10` 等百分比魔法数 | 690 / 726 行 | 入配置 |

### 14. `AIMakeSupplyTroop`（补给队）
| 问题 | 位置 | 建议 |
|---|---|---|
| 已大部分配置化（`supply*`） | — | 保持 |
| `city.freePersons.Remove(leader)` 前未判空 | 约 `AIMakeSupplyTroop` | 判空 |
| `carryTroops * 20` 已改为 `baseFoodCostInTroop * turnCount` | — | 已完成 |

### 15. `AIBuildIntriore` / `AIBuildingTemplate` / `AIBuildingLevelUp`（建设子流程）
| 问题 | 位置 | 建议 |
|---|---|---|
| `gold < 500` 判断重复 3 次 | 1143 / 1170 / 1270 行 | 抽 `CanAffordBuild(city)` |
| `buildingFlag` 每次分配 | 1190 行 | 复用静态缓存 |
| `buildCount <= 6` 魔法数 | 1296 行 | 入配置 |

---

## 三、建议实施顺序

### P0 — 稳定性（低风险、高收益）
1. **G1 全部 `persons[0]` / `people[0]` 判空**（`AITransfrom:622`、`AIMakeTroop`、`AIMakeTransportTroop`、`AIMakeSupplyTroop`）
2. **`AISecurity` / `AITrainTroop` 概率越界 clamp**（`Chance((100-security)*4)` 可达 400%）
3. **`AIBuilding` 使用子调用返回值**，避免"装作做了事"
4. **清理死代码**（`AIIntrior` 注释块、`TroopAI.cs`）

### P1 — 可调性（中风险）
5. **G2 内政阈值全部入 `AIConfig`**：新增 `#region 城市内政阈值`，把 `gold<500`、`foodLine`、`troops<500`、`buildCount<=6` 等收拢
6. **G4 去掉随机门槛**，改为确定性评分（由新的优先级系统承担"倾向"表达）
7. **G5 统一返回值语义**：命令返回"本轮是否产生决策"，便于调度

### P2 — 结构（重构）
8. **G3 抽 `CityTroopFactory.Emit()`**，消除 8+ 处建部队样板
9. **拆分 `AIAttack`** 为 4 个决策子方法
10. **G7 消除重复遍历**（兵装统计、建筑查找）
11. **抽 `IsPathClear()`** 消除运输/移动的通路检查重复

---

## 四、可复用的既有基础设施

以下能力已经具备，优化时应**优先复用**而非重复造轮子：

| 能力 | 位置 |
|---|---|
| 城池态势快照 | `CitySituation.Collect(city, scenario)` |
| 威胁评估 | `BattleSituation.EvaluateCityThreat(city)` / `EvaluateThreat(cell, range, ...)` |
| 敌人信息 | `city.EnemyCount` / `EnemyTroops` / `NearestEnemyDistance` / `CheckEnemiesIfAlive()` |
| 配置集中 | `AIConfig.Instance` + `CityOrderWeights` |
| 命令注册 | `CityCommandRegistry`（新增命令只需注册一处） |
| 命令排序 | `CityAIOrderPlanner.Plan(city, scenario, allowIds)` |
| 就近据点查找 | `Troop.FindNearestFriendlyCity(scenario)` |

---

## 五、预期收益

| 维度 | 现状 | 优化后 |
|---|---|---|
| 崩溃风险 | 4+ 处未判空 | 消除 |
| 可调参 | 约 30 个硬编码阈值 | 全部可 JSON 调整 |
| 行为可预测 | 随机门槛抖动 | 确定性 |
| 代码复用 | 建部队样板 ×8 | 1 个辅助方法 |
| 单方法长度 | `AIAttack` >140 行 | 拆为 4 个 <40 行 |
| 新增命令成本 | 改 4 处 | 注册 1 处 |
