# 已踩过的坑与术语映射表

按"原版概念 → 本项目落点"组织。**动手前先扫一遍，能省一轮返工。**

## 1. 术语映射（搜原版词搜不到时查这里）

| 原版 | 本项目 | 位置 | 备注 |
|---|---|---|---|
| `hex.nejiro()`（根城候选位） | `MapGrid.GridState.Thief`（bit3） | `MapGrid.cs:32`、`Cell.HasGridState` | 搜 `nejiro` 全项目零命中，实际叫 Thief；数据在 `DefaultMap.bin` 里已有 190 个标记 |
| `FacilityId_Base` | `BuildingKindType.Base` = 15 | `BuildingKindType.cs` | 异族根城，耐久 5000 |
| `FacilityId_Den` | `BuildingKindType.Base2` = 16 | 同上 | 贼根城，耐久 2000 |
| `ForceType_Barbarian` / `_Bandit` | `ForceType.Barbarian` / `Bandit` | `Force.cs` | 剧本 JSON 缺 `forceType` 字段，需运行时补正 |
| `PersonId_Hizoku`(0x2e9=745) | 746「贼将」 | `PersonLibrary.json` | 本项目同段 Id 整体 +1（745 是「卫兵」） |
| `UnitStatus_Alerted` | 无 | — | 原版只写不读（`is_alerted` 零命中），不实现 |
| `get_inactive_person_count()` | 无 | — | 原版是临时武将**对象池剩余空位**，项目 `SangoObjectSet` 动态扩容，无对应物 |
| `elapsed_months` | `Scenario.TurnCount / 3` | — | 36 回合/年 ÷ 12 月 |
| 一旬 / 一周 | 一回合 | `Scenario.cs:1869` 注释已确认 | `OnTurnStart` = `on_week` |

## 2. 势力与军团：先查剧本，别急着建

`ScenarioEdit.CreateForce` 看似通用，实则强制要求「无势力君主 + 无势力都城」，无城势力复用必失败。

**先读剧本数据**：

```bash
python -c "import json,io; d=json.load(io.open(r'd:\sango_infinity\Build\Content\Scenario\Scenario.json',encoding='utf-8')); print(sorted(int(k) for k in d['forceSet'])); print(sorted(int(k) for k in d['corpsSet']))"
```

实测结论：43/44/45/46 = 羌/山越/乌丸/南蛮，47 = 贼将，**势力与第 1 军团都已存在且无城**。所以「建势力」「建军团」两步都是多余的。

## 3. 派生字段链（赋值无效的根源）

```
Person.BelongForce  => BelongCorps?.BelongForce      // Person.cs:48
Troop.BelongCity    => Leader?.BelongCity            // Troop.cs:173
Troop.BelongForce   => Leader.BelongForce
Force.IsAlive       => isAlive && mGovernor != null && mGovernor.IsAlive && !mGovernor.IsDead
Person.IsBarbarian  => type == Barbarian || barbarianTribe > 0
Person.HasLifeCycle => !IsSpecialPerson && !IsTemporary
Person.IsSpecialPerson => IsEventPerson || IsBarbarian   // 不含 IsTemporary！
```

推论：
- 想给武将挂势力 → 必须 `BelongCorps = corps; BelongCorpsId = corps.Id`
- 克隆出来的异族武将 Id 变了 → `FixupBarbarianTribe`（Id+名字双校验）匹配不上 → **必须手动抄 `barbarianTribe`**（该方法只在字段为 0 时写，不会覆盖，且该字段 `[JsonProperty]` 存盘）
- 临时武将（`type=2`）不会被 `IsSpecialPerson` 豁免 → `BelongCity` 为 null 时会被归属巡检下野

## 4. `City.allBuildings` 的三个坑（关键）

给建筑设 `BelongCity` 就会经 `Building.Init → BelongCity.OnBuildingCreate` 登记进 `city.allBuildings`，随后：

| 坑 | 位置 | 后果 |
|---|---|---|
| 城陷落遍历 `allBuildings` | `City.cs:2456-2468` | 70% 把建筑 `ChangeCorps` 给攻方，否则 `OnFall` 拆除 |
| 军团重编 | `City.cs:2188-2191` | 批量改 `allBuildings` 的军团 |
| 城池收入累加 | `ClassicsCityWorking.cs:191-207` | 不按势力过滤，遍历 `allBuildings` 累加 `foodGain` |

`City.cs:2390` 势力灭亡时还会 `allBuildings.ForEach(b => b.OnFall(atk))`。

**结论**：不属于某城的建筑（根城/异族据点）必须 `BelongCity = null`，领地城用 `CenterCell.BelongCity` 现查。

连带问题：`BuildingRender.cs:37` 原本写 `Building.BelongCity.Name`，无城建筑会 NRE → 要改成 `BelongCity?.Name ?? BelongForce?.Name ?? "据点"`。

## 5. 拆根城的完整关联链（原版）

不是 AI 命令驱动，是部队行动目标枚举的自然产物：

```
get_target_action                      s11_sys_unit_action.cpp:168
 ├─ :181 get_targetable(self, src_pos, 4)   ← 周围 4 格内所有 MilitaryUnitObject（部队 + 建筑）
 ├─ :193 target = hex.get_hex_object()
 ├─ :261 if (target != main_target)
 │    ├─ :266 is_base_in_friend_area → continue      ← 唯一根城专属过滤
 │    └─ :268 is_rockfall → continue
 ├─ :272 get_target_score
 └─ :275 add_attack_action → unit_attack_building_anim
```

`is_base_in_friend_area`（`s11_sys_unit_utils.cpp:624-637`）：
- 只对 `FacilityId_Base`（异族根城）生效，**Den 不豁免**
- 领地核心据点不是我方敌人 → 返回 true → 跳过
- 设计意图：异族窝长在你领地里，**自家部队不拆**，等别的势力打进来顺手拆

分值（`get_target_score`）：

| 项 | 分 | 出处 |
|---|---|---|
| 敌对 | +5000 | `:1694` |
| 攻城兵器打建筑 / 普通兵打部队 | +2000 | `:1701` / `:1719` |
| 任务主目标 | +10000 | `:1724` |
| Base / Den | **+200** | `:1770-1773` |

根城 5200 < 打部队 7000 → 有部队先打部队。

项目原实现把 +200 错给了 `ArrowTower / Camp`（箭楼/阵，原版没有这一项），已修成 `Base / Base2`。

## 6. 检查"这个字段有没有人读"

原版 `set_alerted(true)` 只写不读（全 s11 搜 `is_alerted` 零命中），属未完成占位。**动手实现前先搜读取点**，零读取点的状态位不值得对齐。

## 7. 无城部队的硬阻塞点

`Troop.Init`（`Troop.cs:1111-1112`）原本无条件 `BelongCity.allTroops.Add(this)`。无城部队唯一崩点就在这里，其余环节（`Clear`、`RebuildTroopLists`、`Force.OnForceTurnStart`、`TroopRender`）都已是空安全或按 `BelongForce` 遍历。

## 8. 无城势力的 AI 安全性

`ForceAIOutpost.AIForceDistricts`：君主 `BelongCity ?? CurrentCity` 都为 null → 直接 `return true`，不会重编军团。
`Corps.PrepareCityInfo` 对 0 城安全（`cityCount = 0`）；`AddActionPoint` 不会除零。
`Corps.CheckValid` 只在 `cityCount == 0 && number > 1` 时删军团 → `number = 1` 安全。
