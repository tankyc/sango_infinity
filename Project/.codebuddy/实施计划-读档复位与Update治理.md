# 实施计划：读档复位 + Update 分配治理

> 生成日期：2026-09-24
> 已拍板：② 读档**不做** `Window.DestroyAll`；③ Part 2 **直接开改**（不先做 Watchdog）；④ 本文档入库。
>
> ① GameEvent 策略**已升级**：原定 C（保守对称退订 + 诊断兜底，不做自动清空）→
> 现改为 **"开局前基线 + 收尾整体还原"**（见第 ⑩ 节）。
> 原因：C 方案本质是"抓漏"，每出现一个漏子就要定位一个类（本会话已连续处理 3 轮），成本会一直涨；
> 基线方案是"兜底"，一次性覆盖所有现在与将来的漏子。

---

## 0. 先纠正三处误判（已被源码复核推翻，勿按原样施工）

| 原报告说法 | 实际 | 证据 |
|---|---|---|
| "`LayerMask.GetMask` 每帧调用" | 是**字段初始化器**，只在构造时执行一次 | `GameController.cs:213` |
| "FPS 文本每帧字符串插值" | 由 `InvokeRepeating("UpdateFPS", 1f, 1f)` 驱动，**每秒一次** | `UIGame.cs:178`、`:579-583` |
| "日志字符串需要条件化" | `Log.Info/Warning` 带 `[Conditional("SANGO_DEBUG")]`，正式包**连实参求值一起被编译掉** | `Log.cs:71/80/99/108` |

→ 第 2 项真正的抓手见 Part 2。

---

## Part 1 读档复位

### 1.1 现状（已核实）

读档路径 `Player.Load` / `LoadAutoFile`（`Player.cs:151-159`、`:173-181`）：

```cs
Window.Instance.CloseAll();
Window.Instance.Open("window_loading");
Quit();                                   // = Scenario.Cur?.OnGameShutdown()
Scenario.CurSelected = new Scenario(fileName);
Scenario.StartScenario(Scenario.CurSelected);
```

`Scenario.Clear()`（`Scenario.cs:1054-1087`）只清 `prepareList` 各对象 + 退订 3 个 GameEvent + 退订 `MapRender.OnMapLoaded` + 清 8 个集合。

### 1.2 泄漏面清单（均带证据）

| # | 残留物 | 证据 | 后果 |
|---|---|---|---|
| a | `RenderEvent` 两条队列 + `CurEvent` + 事件池**无清空** | `RenderEvent.cs:9-13`、`:76-90` | 旧事件被"续播"，目标是旧剧本对象 |
| b | `GameSystemManager` 活动系统栈不出栈 | `Done()` 仅 `UIGameSaveLoad.cs:216` 调用；`Player.Load` 不调 | 系统 `OnDestroy` 不执行 → 对称退订全丢（`PlayerMessage` 30 个订阅、`TroopInformation`/`CityInformation` 各 8 个、`CellSelector` 6 个…） |
| c | `GameDialog` 队列/状态未复位 | `GameDialog.cs` | 旧对话被新剧本继续播放；`Enabled` 可能停在 false |
| d | `AIConfig.Reset()` **零调用** | `AIConfig.cs:42` | AI 参数跨剧本不刷新（Mod 覆盖失效） |
| e | `GameController` 输入残留 | `GameController.cs:23-48/252-279` | 读档后首次点击异常 |
| f | `Player.currentTurnCount` 残留 | `Player.cs:86` | 自动存档节奏错乱 |
| g | `Path.AddSearchPath` 无去重且每次打日志 | `Path.cs:85` | 重复加载时搜索路径累积 |

**窗口实例不是泄漏源**（已拍板不做 `DestroyAll`）：`CloseAll` 关闭但保留实例（`Window.cs:391-401`），而 `UGUIWindow.OnClose` 是**对称退订**的（例 `UIGame.cs:94-99`）；`DestroyAll` 只在回主菜单用（`Player.cs:192`）。

### 1.3 方案

新增 `Game/System/ScenarioLifecycle.cs`（只做编排，不持有状态）：

```cs
public static class ScenarioLifecycle
{
    public static void BeginShutdown()
    {
        RenderEvent.Instance.Reset();                  // 1 表现层队列
        GameSystemManager.Instance.Done();             // 2 系统出栈 → 对称退订
        GameDialog.Instance.Reset();                   // 3 对话队列 / Enabled
        PlayerMessage.Instance?.ClearMessages();        // 4 消息数据
        GameController.Instance.ResetInputState();      // 5 输入残留
        Scenario.Cur?.OnGameShutdown();                 // 6 既有：End + Clear + Cur=null
        AIConfig.Reset();                              // 7 AI 参数重载
        Player.currentTurnCount = 0;                    // 8 自动存档计数
        GameEventDiagnostics.Snapshot("shutdown");      // 9 诊断快照
    }
}
```

调用点（3 处各 1 行）：`Player.Load`、`Player.LoadAutoFile`（`Quit()` → `BeginShutdown()`）、`Player.QuitToMainMenu`。

Mod 幂等（只加保护，不改加载顺序）：`Path.AddSearchPath` 去重；`ModManager.InitMods` 加 `inited` 守卫。

### 1.4 GameEvent 策略：C（保守 + 诊断）

`GameEvent` 约 145 个静态委托（`GameEvent.cs:18-814`），混着两类订阅者：
- **每剧本**：`PlayerMessage`、`TroopInformation`、`CityInformation`、`CellSelector`、各 `GameSystem` —— 应清（清了会重订）
- **应用级**：`GameMedia`、`AudioManager`、`ModManager` 等，只在 `Game.Init` 订一次 —— **不能清**（清了第二次开局静默丢功能）

→ 本计划**不做自动清空**，改为：靠 1.2-b 的对称退订 + `GameEventDiagnostics` 快照 diff 找残留，等诊断证明还剩多少再决定是否上登记制（B）。

`GameEventDiagnostics`：反射遍历 `GameEvent` 静态委托字段，取 `Delegate.GetInvocationList().Length`，输出"事件名 → 计数"；在 `ScenarioLifecycle.BeginShutdown`（`shutdown`）与 `Scenario.StartScenario`（`scenario-start`）各打一次，并与**上一次同标签快照** diff（同相位才可比）。差异非空即失败。

### 1.5 验证方案

1. 订阅不变量：进主菜单快照 → 读档 → 回主菜单快照，两次 `shutdown` 快照必须一致，`Diff()` 为空。
2. 队列：读档后 `RenderEvent.Instance.Dump()` → 队列空、`CurEvent == null`。
3. 系统栈：读档后 `GameSystemManager.Instance.Dump()` → `CurrentCommand == null`、`commads` 空。
4. 对话/输入：读档后 `GameDialog` 无残留、`GameController.Enabled == true`、`controlType == None`。
5. 连读 3 次：消息条数不翻倍、数据与存档一致、AI 参数按新剧本生效。
6. 真机：读档→存档→读档 ×3，无"行为翻倍 / 事件播两遍"。

### 1.6 风险与回滚

- `ScenarioLifecycle` 为新增文件，`Player` 三行调用可退回 `Quit()`。
- 行为变化：读档时 `GameSystemManager.Done()` 会关闭当前活动界面（新剧本本就重开界面，预期无感）。若需"读档后恢复同位置"，另立项（UI 状态记录），**不在本计划**。
- 分 3 次提交：① 四个 Reset 方法；② `ScenarioLifecycle` + 接线 + 幂等；③ `GameEventDiagnostics`。

---

## Part 2 Update 分配治理

### 2.1 已知可核实的三类（按收益排序，直接开改）

| 优先级 | 项 | 证据 | 动作 |
|---|---|---|---|
| P1 | 日志出口不统一：53 处 `Debug.Log` 直连；`Log.Error` 非条件 | `Log.cs:86/94`；`MapRender.cs:340`、`MapGrid.cs:193`、`MapData.cs:305`、`UIScenarioAddonMenu.cs:63/109/261` 等 | 运行期代码统一走 `Sango.Log`；`Error` 保持必定输出 |
| P2 | UI 文本赋值可缓存/脏标记 | `UIGame.cs:576/582`；`UITroopHeadbar.cs:87` 自注 TODO | 静态前缀缓存 + 值未变不写 `text`（`text` setter 会触发 Mesh 重建） |
| P3 | `Camera.main` 每次调用 | `GameController.cs:240` | 缓存到字段 |

**不建议动**：每帧 `Physics.Raycast`（`GameController.cs:289`，1 条射线属正常开销，改间隔破坏悬停手感）；`GameSystemManager.debug_StringBuilder`（已包在 `if (debug)` 内，正式包不执行）。

### 2.2 与分配耦合的治本项（单独评估）

1. `RenderEvent` 队列 `RemoveAt(0)` → 索引游标 + 批量清理（`RenderEvent.cs:87`）
2. 恢复事件池复用（`:88-89` 被注释）：改为"帧末统一回收本帧处理完的事件"
3. AI 全量遍历分帧（`CorpsAI` / `Force.OnForceTurnStart` / `CityAI`，每帧预算 N 个对象）

---

## 三、实施顺序

| 步 | 内容 | 状态 |
|---|---|---|
| D1 | Part 2 的 P1（日志出口统一） | **已完成**（见进展记录） |
| D2 | Part 1 步骤①：`RenderEvent.Reset`、`GameDialog.Reset`、`GameController.ResetInputState`、`PlayerMessage.ClearMessages` | **已完成** |
| D3 | Part 1 步骤②：`ScenarioLifecycle` + `Player` 接线 | **已完成** |
| D4 | Part 1 步骤③：`GameEventDiagnostics` + 快照 diff | **已完成** |
| D5 | Part 2 的 P2/P3 | **已完成** |

每步独立可编译；Part 1 与 Part 2 互不阻塞。

---

## 四、进展记录（2026-09-24）

### 已完成

- **新增** `Game/System/ScenarioLifecycle.cs`：`BeginShutdown()` 九步收尾清单。
  顺序：表现层队列 → 系统出栈（`Done()` 触发对称退订）→ 对话队列 → 消息数据 → 输入残留 → 剧本自身 → `AIConfig.Reset()` → `currentTurnCount` → 诊断快照。
- **新增** `Game/System/GameEventDiagnostics.cs`：反射遍历 `GameEvent` 静态委托，`Capture()`/`Diff()`/`Snapshot(tag)`。
- `Player.Quit()` 改走 `ScenarioLifecycle.BeginShutdown()`，`QuitToMainMenu` 复用它 →
  **`Scenario.OnGameShutdown()` 全项目只剩 1 个调用点**（绕过路径清零）。
- `RenderEvent.Reset()` + `IsIdle`；`GameDialog.Reset()`；`GameController.ResetInputState()`；`PlayerMessage.ClearMessages()`。
- `GameController.Camera.main` 改为缓存字段；`UIGame` FPS 文本静态前缀 + 值未变不写；`UITroopHeadbar` 脏标记（兵力/士气/缺粮未变不写控件）。
- `Path.AddSearchPath`：日志移到去重之后并改走 `Sango.Log`。
- `Log.cs`：修正类注释中写反的描述，明确 **Error 进包、Info/Warning 仅 SANGO_DEBUG**。
- `GameStart`：`OnApplicationPause(false)` 全平台复位输入；新增 `OnApplicationFocus(true)` 但**仅移动端**（桌面端焦点变化频繁，会把正在进行的拖拽打断）。
- 运行期 `Debug.Log` 统一为 `Sango.Log`：`Game/` 下 **13 处全部清零**（`UI/` 无存活、`Map/` 的 2 处在编辑器笔刷中，按计划不动）。

### 与原计划的偏离（两处）

1. `GameEventDiagnostics` 放 `Game/System/` 而不是 `Framework/Diagnostics/`：`Framework` 是独立程序集 `Sango.Framework`，看不到 `Assembly-CSharp` 里的 `GameEvent`（编译直接 CS0246）。
2. **没有**给 `ModManager.InitMods` 加 `inited` 守卫：它会从 Mod 界面被**故意重复调用**（改完 Mod 列表重新应用），加守卫会破坏该功能；而幂等诉求本身已满足（`Path.AddSearchPath` 有 `IndexOf` 去重），只需修刷屏日志。

### 尚未做

- `Framework/` 下约 30 处存活 `Debug.Log`（`AssetBundleManager` 11、`AudioLoader` 8、`AudioUtility` 4、`PlaneLineIntersection` 4、`CursorManager` 2、`ModelLoader` 2、`Utility`/`BmpLoader`/`TextureLoader` 各 1；另有 `AudioExample.cs` 14 处属示例文件）。
  价值偏低：都是加载失败/参数错误的单次日志，与 `Debug.LogError` 一样都会进控制台，只是格式与开关口径不同。
- 治本三项的处理结论：
  - **① `RenderEvent` 队列索引游标：已完成**（见下）。
  - **② 恢复事件池复用：搁置**。根因是事件引用被别处长期持有（`Game/Troop/TroopSkillBack.cs:47` 把事件写进 `skillInstance.master.skillRenderEvent`），同帧回收再复用会串到别的目标——正是 `RenderEvent.cs:88` 注释里的坑；加上事件对象小、数量由玩法驱动，收益 < 风险。
  - **③ AI 全量遍历分帧：搁置**。复核后原假设不成立：`Map.Distance` 是立方坐标 O(1) 减法（`Game/Map/Map.cs:247-250`）；`IsNeedAskSupply` 有 `lowTroops || lowFood` 早退（`Game/Object/Troop/Troop.cs:418-430`）；AI 本来就是"对象级让出"（`Force.cs:621-631`、`CorpsAI.cs:15/118` 返回 false 即下帧继续）。**注意这条更正了评估报告里的过度结论。**
- **分配看板（AllocWatchdog）：已决定不做**（评估人决定）。
- 回归验证：登录游戏后连读档 3 次，核对 `[GameEvent诊断]` 快照差异（C 策略闭环）。

### ⑥ 诊断日志可见性修复（2026-09-24）

**背景坑**：`ProjectSettings.asset:756-771` 显示**所有平台都没有定义 `SANGO_DEBUG`**
（Android 仅 `UNITY_POST_PROCESSING_STACK_V2;LUAC_5_3;FAIRYGUI_TOLUA`，Standalone 也没有）→
`Log.Info/Warning`（带 `[Conditional("SANGO_DEBUG")]`）**被编译期整体删除**，项目里所有信息类日志都不输出；
只有 `Log.Error`（不带 Conditional）会进控制台 —— 与"Error 要进包"的决策正好一致。

**修法**：诊断工具**不再走 `Sango.Log`**，直接用 `UnityEngine.Debug`，并自门控
`Application.isEditor || Debug.isDebugBuild`（编辑器 / 开发包可见，正式玩家包安静且省掉反射遍历）。

**顺带增强**：快照点从 1 个扩成 2 个 —— `shutdown-begin`（收尾前全景）与 `shutdown-end`（收尾后），
同标签跨次对比即可定位"上一次收尾漏退了什么"；首次快照也会打印一行，便于确认工具确实在跑。

**已验证**：编辑器内连续 4 次 probe 快照按预期出现在控制台（首次 / 一致 / `0 → 1` 检出 / `1 → 0` 检出）。

**待评估（需拍板）**：是否给 Standalone（编辑器所在平台）补上 `SANGO_DEBUG` 以恢复全项目 Info/Warning 日志。
代价是日志量会显著变大；好处是本次统一过出口的那些日志才真正有价值。

### ⑦ 实证泄漏与修复：城市 Action 未清理（2026-09-24）

**实测差异**（`shutdown-end` 跨次对比，订阅数逐轮递增）：

```
OnCityHeadbarShowInfoChange: 6 → 7
OnCityTurnStart:             3 → 4
OnCityCalculateGoldHarvest:  2 → 4
OnCityCalculateFoodHarvest:  1 → 2
OnCityCalculateFoodCost:     1 → 3
OnCityGainGoldHarvest:       1 → 2
```

**根因（其中 5 个事件）**：`City` 的 Feature/Action 在拆剧本时没有被清理。

- 城市按武将特技装配 Action：`Game/Object/City/City.cs:1300` `InitPersonAction()` → `feature.InitActions(actionList, this, x)`
- 这些 Action 订阅了全局事件：`Game/Action/Action/City/CityImproveGoldHarvest.cs:27`、
  `CityImproveFoodHarvest.cs:27`、`CityGoldHarvestEveryTurn.cs:27-28`（后者同时订 `OnCityTurnStart`）
- 与差异逐条对得上：`OnCityCalculateGoldHarvest +2`、`OnCityCalculateFoodHarvest +1`、
  `OnCityCalculateFoodCost +2`、`OnCityGainGoldHarvest +1`、`OnCityTurnStart +1`
- **关键证据是"缺席"**：`actionList[i].Clear()` 在 `Troop`（`Troop.cs:2773-2780`）、
  `Person`（`Person.cs:2175-2187`）、`Building`（`Building.cs:481-492`）、`Force`（`Force.cs:373-384`）
  里都有，**唯独 `City` 没有**（`actionList` 在 `City.cs` 仅出现于声明 `:514` 与 `InitPersonAction` `:1300-1305`）

**修复**：给 `City.cs` 补 `public override void Clear()`，逐个 `actionList[i].Clear()` 后清空列表；
刻意不置 `null`（工程里可能仍有别处直接读 `city.actionList`，且 `InitPersonAction` 无 null 判断）。

**另一条独立成因（`OnCityHeadbarShowInfoChange +1`）**：唯一订阅方
`UI/UICityHeadbar.cs:56-63` 的 `OnEnable`/`OnDisable` **是对称的**，所以这不是订阅失衡，
而是**城市血条对象本身在泄漏**（旧实例未销毁 / 新实例创建时旧的没销毁）。待查其实例化与销毁点。

**验证状态**：编译 0 错误、lint 0；**待复跑** `shutdown-end` 跨次对比确认前 5 项不再增长
（注意：`snapshots` 是静态字典，重启编辑器/Play 会清空基线，需要在新会话里至少走两次收尾才有对比）。

### ⑧ 第二轮差异：真泄漏 1 项 + 测量读数修正（2026-09-24）

**重要：`shutdown-end` 的读数偏早，跨次对比应该看 `shutdown-begin`。**

调用栈显示 `BeginShutdown` 是**同步**跑在 UI 回调里的
（`UIDialog.OnSure → GameBackToMain → Player.QuitToMainMenu → Player.Quit → ScenarioLifecycle.BeginShutdown`），
而 `QuitToMainMenu` 里的 `Window.CloseAll()` / `DestroyAll()` 是**之后**才执行，且 Unity 的 `Destroy`
是**帧末延迟销毁** → 这些 UI 对象的 `OnDestroy()`（退订点）在拍快照时尚未发生。
**正确读法：比较 `shutdown-begin` 的跨次差异** —— 下一次收尾开始时，上一次的窗口/实体已经销毁完毕，
读数才是"上一次收尾真正残留了什么"。

**第二轮差异里的真泄漏：状态（Buff）未清理**

- `Stun.cs:14`、`Escape.cs:15` 等 `BuffEffect` 订阅 `OnTroopTurnStart`，只在各自 `Clear()` 里退订（`Stun.cs:29-34`）
- `BuffInstance.Clear()` → `effects[i].Clear()`（`BuffInstance.cs:102-113`）本身没问题；
  常规移除（`RemoveBuff`/`RemoveBuffByKind`/`TurnUpdate` 到期）也都会 Clear
- 但 **`BuffManager` 没有整表 Clear，`Troop` 收尾时也不清它**（`Troop.cs` 里 `buffManager` 只出现于
  声明、加/解 Buff、`Init`、`OnForceTurnStart`）→ 拆剧本时仍挂着的状态把订阅带到了下一次开局
- 与差异吻合：`OnTroopTurnStart: 0 → 6`（≈ 拆剧本时仍挂着的状态实例数）
- **修复**：新增 `BuffManager.Clear()`（逐个 `BuffInstance.Clear()` 退订 + `ClearAsset()` 回收表现对象 +
  清 `assetRef` + `Master = null`），并在 `Troop.ClearWithBelongCity` 清完技能后调用 `buffManager?.Clear()`

**第二轮差异里的疑为假阳性（待用 `shutdown-begin` 复测判定）**：`UIGame`（`UI/UIGame.cs:157-169` 订阅 /
`OnDestroy:215-231` 退订）、`UIMiniMap`（`UI/UIMiniMap.cs:94-103` / `:136-150`）、
`UIPlayerInfoPanel`（`UI/UIPlayerInfoPanel.cs:33-42` / `:44-52`）—— 三者覆盖了差异里的绝大多数事件，
且**订阅数 = 当前实体数**（血条 2、部队 6），是典型的"UI 在销毁时才退订"。

### ⑨ 第三轮差异：部队 Action 在收尾后仍被装配（2026-09-24）

**诊断输出（订阅者点名）**：

```
OnTroopCalculateAttribute: 3 → 6
  当前订阅者(6): TroopAddMoveAbility×4; TroopAddSkill×2;
OnTroopChangeTroops: 6 → 9
  当前订阅者(9): TroopChangeDamage×9;
```

**根因**：`ClearWithBelongCity` 开头有 `if (isCleared) return;`（`Troop.cs:2764`），
而装配 Action（并订阅全局事件）的入口只有 `Troop.Init → InitActionList` 与
`ResetActionAndStatus → InitActionList`。后者的调用点包括：

- `Person.cs:1814`（武将从部队离开）
- `Troop.cs:2933`（部队转投势力）
- `UI/GameEdit/UIPersonEdit.cs:256`（编辑武将窗口）

这些路径完全可能发生在部队**已收尾之后**（阵亡 / 入城 / 解散 → `isCleared = true`）：
此时 `Clear()` 会直接 return，而新建的 Action 订阅
（`TroopAddMoveAbility`/`TroopAddSkill` → `OnTroopCalculateAttribute`；
`TroopChangeDamage` → `OnTroopChangeTroops`）**再也退不掉**，逐轮累积。

**修复**：在唯一装配入口 `InitActionList()` 顶部加 `if (isCleared) return;`，
并在 `ResetActionAndStatus()` 同样提前返回（省掉对已收尾部队的无用属性重算）。
`Troop.Init` 会先把 `isCleared` 置 false，所以正常路径不受影响。

**验证**：编译 0 错误、lint 0；
反射断言——`isCleared=true` 时 `InitActionList()` 不建列表（提前返回）、`ResetActionAndStatus()` 不抛异常；
`isCleared=false` 时 `InitActionList()` 正常建列表（守卫不误伤活部队）。

**同时观察到（上一轮修复已生效）**：

- `OnTroopTurnStart` 从差异中**消失** → `BuffManager.Clear()` 生效 ✓
- `OnScenarioInit` 从差异中**消失** → `UIScenarioForceSelect` 补的 `OnDestroy` 退订生效 ✓
- `OnCityHeadbarShowInfoChange` 本轮也**未出现** —— 但我并没有改过它，其数量可能随"当前可见血条数"波动，
  **属未结案**，后续复测若再出现需按订阅者类型继续定位。

### ⑩ 结构化方案：开局前基线 + 收尾整体还原（落地）

**结论：不再逐个类抓漏**，改为"按开局前状态还原 GameEvent 静态订阅"。

实现（4 个文件）：

- 新增 `Game/System/GameEventBaseline.cs`
  - `CaptureIfNeeded()`：**第一次开局前**拍下全部事件字段的调用列表 —— 那正是"开局前状态"，
    天然包含 `Game.Init` 里订的应用级订阅（GameMedia / AudioManager / ModManager…），
    所以还原不会打掉它们；运行期**不重拍**（重拍会把当前剧本对象钉进基线，反而制造新泄漏）。
  - `Restore()`：把每个委托**整体替换回基线** —— 基线里有的保留、没有的一律移除。
    因为替换的是整个调用列表，**重复订阅也会自动回到 1 份**（天然去重）。
  - 多重集比较按"同一实例 + 同一方法"、与订阅顺序无关；字段级 try/catch 隔离，单字段出错不影响整体。
- `Scenario.StartScenario`（两个入口 `Scenario.cs:857` / `:892`）→ 增加 `CaptureIfNeeded()`
- `ScenarioLifecycle.BeginShutdown` **第 9 步**（最后一步，放在各系统正常退订之后）→ `GameEventBaseline.Restore()`
  - 第 10 步的 `shutdown-end` 快照因此反映"还原后"状态：**下次 `shutdown-begin` 应与本次 `shutdown-end` 完全一致**（自验证闭环）
- `GameEventDiagnostics.LogEnabled`：`static` → `internal static`（复用日志开关）

验证（编辑器内反射，6 项断言全过）：

| # | 断言 | 结果 |
|---|---|---|
| 1 | `GameEventBaseline` 已编译、含 `CaptureIfNeeded`/`Restore` | ✓ |
| 2 | 开局前该字段为空，`Capture()` 后 `HasBaseline=true` | ✓ |
| 3 | 场景中新增 1 个订阅 → `Restore()` 移除 1 个、字段回到 null | ✓ |
| 4 | 基线 `[A]` + 泄漏 `[A,B,A]` → 移除 2 个（B + 重复 A），剩余恰好 `[A]` | ✓ |
| 5 | 已一致时再 `Restore()` 移除 0 个（幂等） | ✓ |
| 6 | 测试字段复原、基线清理 | ✓ |

覆盖范围与已知边界：

- 覆盖 `GameEvent`（静态委托字段，全项目 140+ 事件）。
- **不覆盖**常驻单例上的**实例事件**（如 `MapRender.Instance.OnMapLoaded`）—— 那类由持有者自身对称退订
  （`Scenario.Clear` 里有）。若出现此类泄漏，在 `GameEventBaseline.hubTypes` 加一项即可。
- 首次运行会一次性清掉此前累积的残留（日志里的"移除 N"可能偏大），之后应稳定接近 0。

「GameSystem / 单例」还原现状（`ScenarioLifecycle` 清单）：

| 对象 | 复位方式 | 状态 |
|---|---|---|
| `GameSystem` 栈 | `GameSystemManager.Instance.Done()`（第 2 步，触发各系统 `OnDestroy` 对称退订） | 已有 ✓ |
| `RenderEvent` 表现队列 | `Reset()`（第 1 步） | 已有 ✓ |
| `GameDialog` | `Reset()`（第 3 步，含 `Enabled` 复位） | 已有 ✓ |
| `PlayerMessage` | `ClearMessages()`（第 4 步） | 已有 ✓ |
| 输入状态 | `GameController.ResetInputState()`（第 5 步） | 已有 ✓ |
| `Scenario` 自身 | `OnGameShutdown()` → `End + Clear + Cur = null`（第 6 步） | 已有 ✓ |
| `AIConfig` | `Reset()`（第 7 步） | 已有 ✓ |
| `Player.currentTurnCount` | 归零（第 8 步） | 已有 ✓ |
| **GameEvent 全部静态订阅** | **基线还原（第 9 步）** | **本次新增 ✓** |
| `Window` | `QuitToMainMenu` 里的 `CloseAll + DestroyAll` | 已有 ✓ |
| `ModManager` | 未复位（应用级内容，跨剧本本应保留；仅"重载幂等"待做） | 待做 |

### ⑪ 仍未做（Part 2）
- `RenderEvent` 事件池复用 + 队列 `RemoveAt(0)` → 索引游标（P0#1）。
- `ModManager` 重载幂等。

### ⑤ `RenderEvent` 队列索引游标（2026-09-24 完成）

- 背景：原来每处理完一个事件就 `eventQueue.RemoveAt(0)`，整队是 O(n²) 次数组搬移；而移动演出是"路径每格排一个事件"（各 `TroopAction*` 共 7 处），队列轻易上百。
- 改法：新增 `int queueHead` 游标，`Update` 主循环只前进游标，全部处理完再 `RemoveRange(0, queueHead)` 一次性搬移。
- 同步改的三处（缺一即出 bug）：
  1. `Reset()` 里 `queueHead = 0`（否则读档后残留游标会跳过新事件）；
  2. `EventCount` 与 `Update` 里的 `evCount` 都改成 `eventQueue.Count - queueHead`（后者用于"有待播 depends 事件时不推进回合"的判定）；
  3. `IsIdle` 用待处理数判断；`Dump()` 从 `queueHead` 开始列举。
- 验证：编译 0 错误；合成队列（`DelayEvent`）四项断言通过——游标只前进不搬移（长仍为 2、游标 1）、处理完批量清理（游标 0、长 0）、非零游标时 `Reset` 归零。
