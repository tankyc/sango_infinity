# 本项目已知热点账本

数据来源：**代码注释里记录的 Profiler 实测值**（不是本文档重新测的）。改动这些位置前先读对应注释，注释里保留了原写法、分配符号与等价性说明。

「状态」列含义：`已改` = 代码里已是优化后写法，但**改后数字没有留存记录**，需要时按文末方法复测；`待复核` = 脚本扫出的疑似点，尚未人工确认。

## 一、月度 / 回合刷新（单帧最贵的一类）

| 位置 | 原分配 | 手法 | 状态 |
|---|---|---|---|
| `Scenario.cs:2262-2270` `UpdateCityRate` | 单帧 **124.5 KB**（`<>c__DisplayClass158_0.<UpdateCityRate>b__0`） | ForEach 闭包 → 下标遍历 + 静态表 | 已改 |
| `City.cs:625-680` `UpdateRate` | 每城每月 **192 B**（`new int[,]` 12×3） | 常量表提成 `static readonly RatePresetTable` | 已改 |
| `City.cs:403-406` `City.OnForceTurnStart` × N | 每处 **232 B ~ 1 KB** | `freePersons(64)`/`wildPersons(32)`/`allPersons(128)`/`captiveList(16)`/`allBuildings(64)` | 已改 |
| `PersonLoyaltyRules.cs:140-143` 换季掉忠 | 1500 武将 **2.4 秒** | 每城标记位域（`City.stateFlags`）+ 热路径一次位与 | 已改 |

## 二、势力回合（`Force.UpdateTurnInfo` 一族）

| 位置 | 原分配 | 手法 | 状态 |
|---|---|---|---|
| `Force.cs:677-680` `CityList` | 6.5 KB 中 **2.0 KB**（List 翻倍） | 容器容量预设 | 已改 |
| `Force.cs:1047-1051` `UpdateCanBuildBuildingTypes` | **4.5 KB/帧**（26 类型 × ~172 B） | 两个捕获 lambda → 三条就地循环 | 已改 |
| `Force.cs:1402-1405` 技法判重 | **27.9 KB**（该路径总计 30.6 KB） | `Contains(int)` 的 Predicate → 直接遍历字典值集合（struct 枚举器） | 已改 |
| `Force.cs:3726-3731` `UpdateValidCreatedItemTypes` | **1.9 KB/帧**（`Dictionary.Resize` 440/100/20 B） | 复用 `itemTypeMapCache` + `Clear()`，去掉 `Values.ToArray()` | 已改 |
| `SangoObjectList.cs:179-195` `Contains(int)` | **20 KB+/帧** | `Find(lambda)` → 手写循环 | 已改 |
| `ForceAIOutpost.cs:400-403` `SetGuard` 评分 | `GetGuardStatScore` 是该函数调用次数最多项 | 候选人五维外层预取 | 已改 |
| `ForceAIOutpost.cs:461-463` | `SetGuard` 全部 **22.9 KB** | 循环不变量提出 | 已改 |

## 三、部队 AI 与寻路（单帧最大的两个点）

| 位置 | 原分配 | 手法 | 状态 |
|---|---|---|---|
| `TroopAIUtility.cs:2672-2675` `PriorityAction` | 单帧 **283.2 KB** | 计数数组静态复用 + `Array.Clear`；遍历改「起点 + Ring(1..3)」 | 已改 |
| `Map.cs:399-402` `GetRing` | **168 B/次**，`SkillInstance.GetSpellRange` 一次 2 环 = 336 B | 闭包 + `Action<Hex>` → `List<Hex>` 重载 + 静态 `ringHexBuffer` | 已改 |
| `Map.cs:670-673` `GetMovePath` | **152 B/次** | 捕获闭包 + `CellCheck` 委托 → 字段缓存委托 | 已改 |
| `Map.cs:757-760` `GetMinCostDirectPath` | 单次 **182.6 KB**（`Dictionary.set_Item`×3 + `HashSet.Add`×2） | 字典/集合预设容量 | 已改 |
| `Troop.cs:3911-3915` `TroopOccupoyCity.Prepare` | **11 ms / 283 KB** | 不在属性 getter 里 Prepare（隐藏副作用），移到显式时机 | 已改（架构型） |

## 四、脚本扫出的待复核项（截至 2026-11-04）

`scan_gc_hotspots.py --rule reverse-remove` 当前 **0 条**（说明已治理的循环都改对了）；剩余两类需要人工判断：

```
--rule reverse-remove-fragile   3 条   （用 i-- 补偿，正确但脆弱）
--rule reverse-remove-indirect  8 条   （调用链里可能摘元素，需追链；SangoObjectSet 的正序是合法的）
```

典型待复核点：

- `ForceAI.cs:1641` / `1672`：`city.captiveList` / `troop.captiveList` 的正序 + `i--` 补偿（`ProcessCaptives`，注释已声明前提）。
- `CityAllRecruit.cs:270`：过滤不可执行目标，`RemoveAt(i)` + `i--`。
- `PersonLifeCycle.cs:400`：遍历 `scenario.personSet`（`SangoObjectSet`，**正序正确**，属误报；看见这类命中先确认容器类型）。
- `DiplomacyActionAnnex.cs:278`：遍历的是快照 `members`，「移除」发生在 `Receiver` 名册上，安全。

结论：**脚本只是缩小范围，命中后必须确认「移除的是不是正在遍历的那个集合」以及「容器是 List 还是 Set」。**

## 五、复测方法

1. Unity Profiler（Development Build + Deep Profile 关闭，`GC Alloc` 列足够定位分配）连真机或编辑器。
2. 复现场景：进入剧本 → 推进一个**势力回合**（覆盖 `Force.UpdateTurnInfo` 一族）→ 推进到**月初**（覆盖 `UpdateCityRate` / `UpdateRate`）→ 让 AI 部队行动一轮（覆盖 `PriorityAction` / `GetRing` / `GetMovePath`）。
3. 记录目标方法的 `GC Alloc`；与上表数字对比。**注意**：`TroopAIUtility.PriorityAction` 曾在不同提交里记为 132.6 KB 与 283.2 KB，说明该值与 AI 局面强相关，比较时必须用同一存档、同一回合。
4. 若数字没降：先确认改的是否真在调用栈上（有些方法有多个重载/多条调用路径），再确认是否被别的分配掩盖。
