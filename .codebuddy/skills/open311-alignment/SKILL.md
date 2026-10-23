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
2. refresh_unity(mode=force, scope=scripts, compile=request, wait_for_ready=true)
3. read_console(types=[error], count=40, include_stacktrace=true)
4. read_lints(paths=[改动文件], severity=[error])
```

- `read_console` 超时 / "session not ready" = 编辑器在重导入，用 `read_lints` 兜底，并在回复里如实说明"未能取到 console 输出"。
- **不要**声称"已验证"而没有实际跑过编译。
- 运行时行为（移动、战斗、AI）需要玩家实跑，代码层面只能保证编译与静态一致。

## 七、交付格式

每次对齐完成后，回复里必须包含：

1. **原版证据**：`文件:行号` + 代码片段
2. **对照表**：原版步骤 → 本项目落点 → 状态（✅ / ⚠️ / ❌）
3. **差异说明**：哪些没对齐、为什么（架构差异 / 功能缺失 / 风险过高）
4. **验证结果**：编译、console、lint 的实际状态
5. **实测建议**：具体操作 + 预期日志

## 八、参考文件

- `references/pitfalls.md`：完整的踩坑记录与排查步骤（按需读取）
