---
name: open311-alignment
description: |
  Sango Infinity 与 Open311（三国志11 引擎复刻）逻辑对齐工作流。
  当任务涉及武将/城池/势力/军团/部队/建筑/地图的归属、生命周期、移动、战斗结算等业务逻辑的实现、修复或重构时触发。
  该技能强制"先读原版源码再写代码"，并提供字段映射、调用方审查方法、已踩过的坑与验证流程。
---

# Open311 逻辑对齐工作流

## 一、铁律

1. **先找原版再动手**。任何"实现 / 修复 / 对齐 / 为什么这样"的任务，第一步是去 `D:\open311\s11` 检索是否有相同逻辑。
2. **找到就必须对齐**，不是"参考"。语义、顺序、边界条件、异常分支都要照搬。
3. **找不到才自主设计**，并在代码注释里显式写明"原版无对应实现"及理由。
4. **注释必须引用原版出处**：`文件:行号` + 关键代码片段。没有出处的对齐等于没对齐。

## 二、原版源码位置与检索

根目录：`D:\open311\s11`

| 用途 | 文件 |
|---|---|
| 数据结构 / 方法声明 | `s11_scen.h` |
| 枚举（Mibun / Facility / LifeMode 等） | `s11_enum_scen.h`、`s11_enum_preset.h` |
| 武将治理 | `s11_sys_scen_person.cpp` |
| 城池 / 建筑治理 | `s11_sys_scen_building.cpp` |
| 势力治理 | `s11_sys_scen_force.cpp` |
| 军团治理 | `s11_sys_scen_district.cpp` |
| 部队治理 | `s11_sys_scen_unit.cpp` |
| 武将命令（移动/返回等） | `s11_sys_person_order_*.cpp` |
| 军团命令（新设/重编） | `s11_sys_person_order_kunshu.cpp` |
| 战斗结算 | `s11_sys_unit_battle.cpp`、`s11_sys_unit_action.cpp` |
| 年度 / 月度结算 | `s11_game_year.cpp`、`s11_game_month.cpp` |
| 城陷 / 捕获事件 | `s11_sys_event_kanraku.cpp`、`s11_sys_event_capture.cpp` |
| 地图 Hex | `s11_scen_map_hex.cpp` |

检索优先级：**函数名**（`System::person_enter`）> **字段名**（`district_`）> **枚举值**（`Mibun_Taishu`）。

## 三、字段映射表

| Open311 | Sango |
|---|---|
| `Person` / `mibun_` | `Person` / `state`（`PersonStateType`） |
| `service_`（所属据点） | `Person.BelongCity` |
| `location_`（所在地点） | `Person.CurrentCity` |
| `district_`（军团） | `Person.BelongCorps` |
| 派生势力（`district_->force_`） | `Person.BelongForce` |
| `Force` / `kunshu_` | `Force` / `mGovernor` |
| `District` / `number` | `Corps` / `number` |
| `Building`（含 City/Gate/Port 统一建模） | `City`（`IsCity/IsGate/IsPort`）+ `Building`（内政/军事设施） |
| `Unit` | `Troop` |
| `actors_`（在途武将表） | `Scenario.Actors` |
| `person_change_order` | `Person.SetMission` |
| `person_change_district` | `PersonStateSystem.SetCorps` / `Recruit` |
| `person_enter` / `person_leave` | `PersonStateSystem.Enter` / `OnWillChangeToCity` |
| `person_set_zaiya` | `PersonStateSystem.LeaveToWild` |
| `building_set_district_id` | `CityStateSystem.SetCorps` |
| `building_change_district` | `CityStateSystem.ChangeCorps` |
| `building_neutralize` | `CityStateSystem.Neutralize` |
| `building_appoint_taishu` | `City.UpdateNewLeader` / `CityStateSystem.AppointTaishu` |
| `district_appoint_totoku` | `CityStateSystem.AppointTotoku` → `Corps.UpdateCommander` |
| `district_dissolve` | `Force.MergeCorps(from, to)` / `Force.DeleteCorps` |
| `force_ruin` | `Force.Ruin(by, fallCity, byObject)` |
| `force_koukei` | `Force.Succeed(successor, oldCapitalCorps)` |
| `select_successor` | `Force.SelectSuccessor(oldGovernor)` |
| `force_surrender_all_buildings` | `Force.SurrenderAllBuildings(winner)` |
| `set_completed` 的副作用 | `Building.OnCompleted()` |

## 四、调用方审查法（必做）

改一个函数前，按顺序回答三个问题：

1. **谁调用它？** 逐个列出调用点，确认每个调用点在**调用时刻**满足新加的前提。
2. **参数从哪来？** 被调函数内部逻辑 ≠ 完整语义。参数常常由调用方构建。
3. **改完这个判定后，谁负责维护它依赖的字段？**

### 三个真实翻车案例

| 案例 | 错误做法 | 正确做法 |
|---|---|---|
| 军团新设是否带港关 | 只读 `create_district` 内部（只补都督城），结论"不带港关" | 追到 `exec_district_new_command`，它用 `city_list[building->get_area_city_id()]` 把港关塞进 `kyoten_list` —— **会带**，但排除君主驻地（`kunshu->get_service_id() == i`） |
| 陷阱命中即爆 | 只在 `Building.OnHit` 里 `durability = 0`，没看 `ChangeDurability` 后续 | `OnHit(atk)` 之后还有 `durability = durability + num`，导致 `0 + (-500) = -500`。`OnHit` 返回 true 时必须在 `ChangeDurability` 里跳过累加 |
| 君主死亡继承 | 把 `Force.IsAlive` 改成"要求君主存活"，没检查谁换君主 | 全工程 `mGovernor` 只在剧本编辑器写，运行期零处更新 → 君主一死势力凭空消失。必须补 `SelectSuccessor` + `Succeed`，并在 `Person.Dead()` 里触发 |

## 五、高频陷阱清单

| 陷阱 | 说明 |
|---|---|
| **死代码分支** | `person_leave` 里"君主改城军团"前置条件是 `move == true`，而所有调用点传 `false` —— 该分支永不执行，不必实现 |
| **被调函数只做一半** | `create_district` 只管"传入列表"，列表构建在调用方 |
| **生命周期时序** | `OnHit` 里不能直接 `OnFall`（对象会提前归还池，调用方随后读 `Name` 得到脏数据）；只能"清零耐久 + 返回"，让标准流程收尾 |
| **存储字段 vs 派生** | 原版 `get_force_id()` 是 `district_->force_` 派生；Sango 把 `BelongForce` 存成字段 → 必须在所有改军团的地方同步势力 |
| **合法空值** | 港关 `district_ == -1`（无主）、`type_ == -1`（未分类）都是合法状态，巡检不能当脏数据 |
| **双向名册** | 改归属必须同时维护对象列表与 Id 数组（`SpouseList` / `SpouseListId`） |
| **集合重复** | `SangoObjectList.Add` 重复时**打 Error 但仍插入**，调用点必须自己 `Contains` 判断 |
| **循环引用** | `City.AddCaptive` 与 `PersonStateSystem.ToCaptive` 不能互相调用，只能单向委托 |

## 六、验证流程

```
1. 改完代码
2. refresh_unity(mode=force, scope=all, compile=request, wait_for_ready=true)
3. read_console(types=[error], count=40, include_stacktrace=true)   ← 唯一可信的编译诊断
4. read_lints(paths=[改动文件], severity=[error])                   ← 仅作辅助
```

### 诊断手段的可信度（血泪排序）

| 手段 | 可信度 | 说明 |
|---|---|---|
| `read_console(types=[error])` | ✅ **唯一可信** | 直接读 Unity 编译器输出 |
| `read_console` 返回 0 条 | ✅ 可信 | 说明编译干净 |
| `refresh_unity` 返回 success | ⚠️ **不代表编译通过** | 只说明"刷新完成" |
| `read_lints` | ❌ **会漏报，不可作依据** | 已两次返回 0 error 而实际编译失败：<br>· `CS0111` 重复定义 `City.GetJobCost`<br>· `CS1061` `Scenario.Month` 不存在 |

**铁律**：
- `read_lints` 报 0 error 但 `read_console` 拿不到时，回复里必须写**"未能确认编译"**，禁止写"编译通过"。
- 用户贴报错是最高优先级信息，比任何工具结论都可信。

### `read_console` 超时的应对

MCP 的 `read_console` 会因编辑器重导入返回 `ping not answered` / `timed out`。超时期间：

1. **不要继续堆改动**（错误会累积，定位变难）
2. 回复里列出**本轮动了哪些文件**，方便用户对照 Console 自查
3. 主动请用户贴报错
4. 恢复响应后**立刻**跑一次 `read_console` 把积压错误清掉

## 六.五、每轮收尾自检（改完必做）

| 检查 | 动作 |
|---|---|
| **新增方法是否与已有同名** | 全库搜一次方法名（`City.GetJobCost` 的教训） |
| **基类是否真有该成员** | `SangoObject` 这类弱基类不含 `BelongForce` 等子类字段，需 `atk is Troop t` 显式取 |
| **门禁与扣费是否同源** | 见第九节 |
| **是否引入了新的静默路径** | 新增 `OnFall` / `Clear` / 直接写字段，要么打日志要么加注释说明为何静默 |

## 七、门禁与扣费必须同源（高频事故）

**凡是"菜单能不能点（`IsValid`）"与"点下去扣多少"分别计算的命令，都要逐项核对。**

一次全量审查查出 **15 处**不一致，可分五类：

| 类型 | 典型案例 | 表现 |
|---|---|---|
| **门禁折后价、扣费原价** | 出阵：门禁 `GetJobCostAP`（军事府 5）、扣费 `JobType.GetJobCostAP`（10） | 显示 5、实扣 10，**还多扣** |
| **门禁查 A、扣费扣 B** | 能力研究：门禁用 `ResearchGoldCost * 2`（**金币常量**）当 AP 门槛 | 门槛被放大，按钮长期灰 |
| **门禁写死金额** | 亲善 `gold >= 1000`，实际扣 `GetJobCost()`（外交府减半后 500） | 钱够却灰，或反过来 |
| **门禁查、扣费不扣** | 八项外交从不 `ReduceActionPoint`；摒弃/交换要 1000 金却免费 | 可无限刷 |
| **门禁不查、扣费会失败** | 技巧研究不查金/技巧点，执行时 `return null` | 点了没反应 |

### 通用做法

1. **收敛成城级入口**：`City.GetJobCostAP(CityJobType)` / `City.GetJobCost(CityJobType)`，内部按建筑/科技打折
2. **门禁与执行都走它**，禁止一边用 `JobType.` 原价、一边用 `City.` 折后价
3. **扣费前二次复核**：门禁通过后玩家还可能换目标（如建筑列表里换成更贵的），
   执行方法**开头**必须再判一次资源（注意：必须放在"注册对象 / 摘除武将 / 置状态位"之前，否则会留下孤儿对象）
4. **批量操作要算总价**：褒赏按人数累加、建造按所选建筑，门禁只按 1 份算就会扣成负数
5. **0 成本的工作**（`JobTypes.json` 里 `cost = 0`）会让 `CheckJobCost` 恒真，不能拿它当资金门禁

## 八、尺度过切换（raw ↔ 物理值）

**现象**：同一"关系值"存在两套刻度 —— 存储 raw（-5000~5000）、业务 like（0~100），
换算藏在 `GetLike` 里，调用点混用。

**后果举例**：

| 调用 | 写的值 | 实际效果 |
|---|---|---|
| 停战成功 | `AddRelation(+300 raw)` | ≈ +3 like（原版**不改**） |
| 宣战 | `ReduceRelation(-1000 raw)` | ≈ -10 like（原版**没有**这条） |
| 外交事件 | `AddRelation(+50 raw)` | `+50 ÷ 100 = 0` → **完全无效** |
| 调略失败 | `cityStrategyFailedPenalty = 200 raw` | ≈ 2 like（原版 -8） |

### 处理原则

1. **统一到原版刻度**（关系值就是 0~100），存储层也改成同尺度
2. **旧档迁移用值域探测**（而非版本号），因为**"直接加载原版剧本"时原版值天然在范围内，不会误触发迁移**
3. 迁移时**超范围修正**（钳制），避免脏值把 UI 撑爆
4. 迁移之后，**所有按 raw 设计的变量默认值必须 ÷100**，包括散落在 `ScenarioVariables` 里的十数个
5. 旧档兼容兜底：关键计算处加 `if (v > 100) v /= 100;`

## 九、原版的"多命令解耦"不要合并

**案例**：内政建造与拆除。

| 原版 | 本项目（错误做法） |
|---|---|
| `kyoten_destroy`：建满时只拆**废设施**（铸币厂无市场 / 谷仓无农田 / 优先级表），**拆完即止** | `TryReplaceBuilding`：规划器算"拆 A 建 B"，**同一函数里先拆后建** |
| `dev_ai`：下次执行发现**空地**才建 | —— |

**为什么不能合并**：原版靠"空地"这个中间状态解耦，两条命令各有独立判定；
合并后绕过了拆除侧的全部门槛（"只拆废设施"），产生大量非预期拆除。

**教训**：看到原版有两条相邻命令作用在同一对象上，先确认它们之间是不是用**状态字段**解耦的。

## 十、不要把单点规则套到全体

**案例**：`FindUnnecessaryBuilding`。

| 原版 | 本项目（错误做法） |
|---|---|
| `get_unnecessary_facility_list(city, FacilityId_Mint)`：**只判铸币厂**，看它周边的**市场**数 | 对**所有内政设施**判"周边同类 < 3 即冗余" |
| `FacilityId_Garner`：**只判谷仓**，看周边**农田**数 | 比较的是**自己这种**设施 |

**后果**：一座孤立的农场（周边没有别的农场）被判为冗余 → 拆掉 → AI 再建 → 再拆，形成死循环。

**教训**：原版函数名里的参数（`FacilityId_Mint`）就是"适用范围"，不是"举例"。

## 十一、交付格式

每次对齐完成后，回复里必须包含：

1. **原版证据**：`文件:行号` + 代码片段
2. **对照表**：原版步骤 → 本项目落点 → 状态（✅ / ⚠️ / ❌）
3. **差异说明**：哪些没对齐、为什么（架构差异 / 功能缺失 / 风险过高）
4. **验证结果**：编译、console、lint 的实际状态
5. **实测建议**：具体操作 + 预期日志

## 十二、参考文件

- `references/pitfalls.md`：完整的踩坑记录与排查步骤（按需读取）
- `references/diagnostics.md`：编译诊断与静默清理路径的排查手册
