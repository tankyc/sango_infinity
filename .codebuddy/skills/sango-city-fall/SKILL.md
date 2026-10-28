---
name: sango-city-fall
description: |
  Sango Infinity 城池陷落（城陷 / 攻占）链路的知识库与改造工作流。
  当任务涉及 City.OnFall、攻城破城结算、城破后的武将撤离与俘虏判定、城内建筑存留、
  金粮兵装掠夺、城市归属改旗、势力灭亡（Force.Ruin）、港关随都市易主、异族/贼攻城，
  或城陷事件（GameEvent.OnCityFall）与破城演出（CityFallCompleteEvent）时触发。
  提供原版 kyoten_kanraku 的 18 步标准顺序、本项目逐步落点对照表、
  不可违反的时序约束、已踩坑清单与已知未对齐项。
---

# 城池陷落链路（Sango Infinity）

## 一、适用范围

**用本技能**：
- 改 `City.OnFall` 及其任意下游（俘虏、建筑、掠夺、改旗、太守、兵力/耐久归零）
- 排查"城打下后归属不对 / 太守消失 / 武将户口挂在敌城 / 势力没灭亡 / 港关没跟着换旗"
- 新增攻城来源（事件、脚本、编辑器、AI 特殊兵种），需要复用城陷收尾
- 城陷事件与演出（`GameEvent.OnCityFall`、`CityFallCompleteEvent`、`ForceFallCompleteEvent`）

**不用本技能**（改用 `open311-alignment`）：
- 泛化的归属治理、军团整编、外交、内政建造
- 与城陷无关的 Open311 对齐任务

铁律仍然生效：**先读原版 `D:\open311\s11\s11_sys_event_kanraku.cpp` 再动手**，
任何对齐代码必须带 `文件:行号` 出处注释。

## 二、链路入口

| 入口 | 位置 | 触发条件 |
|---|---|---|
| 耐久归零 | `BuildingBase.ChangeDurability` → `OnFall(atk)`（`BuildingBase.cs:357`） | 攻城/普攻把耐久打到 0 |
| 兵力归零 | `SkillInstance.cs:820` `city.OnFall(this)` | `City.ChangeTroops` 返回 false（守军清零） |
| 陷阱/火计 | `BuildingTrapEffect.cs:171` | 陷阱爆炸波及 |
| 城内部设施 | `City.OnFall` 内 `building.OnFall(atk)` | 城陷清算，**不是**新一次城陷 |

`City.OnFall(SangoObject atker)` 唯一实现：`Project/Assets/Sango/Scripts/Game/Object/City/City.cs:2262`。
`atker` 可能是 `Troop`，也可能是 `SkillInstance`（内部取 `skillInstance.master` 还原成部队），
两条分支必须先归一化再判空。

三条前置短路（不可删）：
```2268:2283:Project/Assets/Sango/Scripts/Game/Object/City/City.cs
            Troop atk = atker as Troop;
            SkillInstance skillInstance = null;
            if (atker.ObjectType == SangoObjectType.SkillInstance)
            ...
            if (atk == null) return;
            if (BelongForce == atk.BelongForce)
                return;
```

## 三、唯一权威顺序（原版 18 步 → 本项目落点）

顺序即语义。**任何改动先看这张表有没有被破坏**。

| # | 原版 `s11_sys_event_kanraku.cpp` | 作用 | 本项目落点 | 状态 |
|---|---|---|---|---|
| 1 | `:185-194` 势力城表剔除本城，空则 `set_ruined(true)` | 判定是否最后一城 | `escapeCity == null` 分支（`City.cs:2405-2469`） | ✅ |
| 2 | `:195` `kanraku_report` | 城陷播报 | `Sango.Log.Info("[城陷]…")`（`:2386`） | ✅ |
| 3 | `:196` `remove_building_fire` | 清火 | `OccupyCellList` 逐个 `fire.Clear()`（`:2300-2309`） | ✅ |
| 4 | `:197-200` `on_city_falls` / `on_gate_falls` | 城/关分流事件 | 末尾统一派 `OnCityFall`（`:2783`） | ⚠️ 不分流 |
| 5 | `:201-220` 功绩 2000/1000/500、经验 5/3 | 攻城奖励 | `SkillInstance.cs:812-818` + `GainValueConfig` | ✅ 走配置 |
| 6 | `:221-227` 关系值压到 15 地板，否则 -1 | 敌对度 | `DiplomacyRules.ApplyHostilityPenalty`（`:2289`） | ✅ |
| 7 | `:228-229` 玩家攻方 `diplomacy_ban_timer = 9` | 禁谈 | 未实现 | ❌ |
| 8 | `:230-250` 我方俘虏归顺（改军团） | 解救俘虏 | `PersonStateSystem.Recruit`（`:2743-2751`） | ✅ |
| 9 | `:251-263` `capture` + `horyo_shoguu` | 抓捕与招降 | `temp_captive_list` → `CityRecruitPersonWhenCityFallEvent`（`:2793`） | ✅ |
| 10 | `:264-277` `rate = max(5, 魅力/10)` 掠夺金粮兵装 | 掠夺 | `:2681-2710` | ✅ |
| 11 | `:278-342` 设施：完工进 dev / 未完工即毁；军事设施只拆守方同城；`shuffle`；`n = dev - min(dev, 魅力/20)` | 设施存留 | `:2542-2642` | ✅ |
| 12 | `:343-346` `building_change_district` 或 `building_neutralize` | 改旗/白城化 | `CityStateSystem.SetCorps`（`:2726-2729`） | ✅ |
| 13 | `:347-358` `unit_enter` / `update_bandits` | 入城 / 异族转进 | `:2754-2775`、`UpdateBanditsAfterFall` | ✅ |
| 14 | `:359` `building_appoint_taishu` | 指定太守 | `Leader = atk.Leader`（`:2756`） | ✅ |
| 15 | `:360-362` 耐久补到 `max_hp/2`（取大值） | 恢复耐久 | `City.ChangeDurability`（`:2134-2147`） | ✅ |
| 16 | `:363` `building_reset_targeted` | 重置以该城为目标的部队 | 未实现 | ❌ |
| 17 | `:364-376` 同区域 `Expel` / `ComBuild` 部队改 `Retreat` | 逼退 | 未实现 | ❌ |
| 18 | `:377` `move_screen` | 镜头 | 表现层，无需对齐 | — |

> **唯一偏离**：本项目把「设施清算（第 11 步）」放在「掠夺（第 10 步）」**之前**
> （`City.cs:2542` 设施 / `:2659` 掠夺）。两步数据无耦合（设施不动金粮兵装），
> 但排查日志顺序时不要误判为 bug。

细节版（含代码片段与二次分支）见 `references/pipeline.md`。

## 四、四条硬约束

1. **四大块的相对顺序固定**：设施清算 → 掠夺 → 改旗（`:343`）→ 入城（`:347`）。
   这两块绝不能挪到改旗之后：`CityStateSystem.SetCorps` 会触发表现层与账本刷新，
   提前翻色会让玩家在"掠夺保留率"播出之前就看到城已易主。
2. **`Force.Ruin` 必须早于改旗，且返回 true 时立刻 `return`**（`City.cs:2654-2655`）。
   返回 true = 达成天下一统，主循环已暂停、终局演出已广播；继续往下会改一座已终结的城的归属。
3. **`Leader` 只在"城已归攻方 + 武将已入籍"后才可信，且赋值与入城必须同块完成**。
   白城分支：先 `EnterCity`，再校验 `BelongCity == this && allPersons.Contains` 后赋值（`:2323-2329`）；
   主路径：先赋值再 `EnterCity`（`:2756-2757`），靠 `PersonStateSystem.Enter` 里的
   `city.NeedUpdateLeader()`（`PersonStateSystem.cs:179`）重算兜底。
   两条写法都不能在赋值与入城之间 `return` —— 否则 `Leader` 指向一个户口不在本城、
   也不在 `allPersons` 里的人，年度巡检判其失效并清空，表现为"刚打下的城太守凭空消失"。
4. **`escapeCity` 必须是本势力据点；最后一城时必须为 null**。
   它同时是"势力是否灭亡"的唯一判据（`CityCount == 0`），也是武将迁户口的唯一目标。

## 五、escapeCity 决策树

```
if (this == BelongForce.CapitalCity)  escapeCity = GetNearnestForceCity()
else                                  escapeCity = BelongForce.CapitalCity
if (escapeCity != null && escapeCity.BelongForce != BelongForce) escapeCity = GetNearnestForceCity()
if (escapeCity == null && BelongForce.CityCount > 0)             escapeCity = GetNearnestForceCity()
if (BelongForce.CityCount == 0)                                  escapeCity = null   // 势力灭亡
```

两道兜底不是防御性冗余，而是修 `CapitalCity` 漂移：
`CapitalCity` 是君主户口的派生值（`mGovernor?.BelongCity`）。君主出征/在途/死亡时户口留在旧城，
城一易主 `CapitalCity` 就会指向敌城或变 null。此时若不兜底，武将全被迁到敌城或被误判亡国。
对齐依据：`s11_sys_scen_person.cpp:909` 的 `get_force_id() == force_id` 过滤。

## 六、归属与人员只走治理入口

| 要做的事 | 必须调用 | 禁止 |
|---|---|---|
| 改城池归属 | `CityStateSystem.SetCorps(city, corps)` | `city.BelongCorps = …` + `city.BelongForce = …` |
| 白城化 | `CityStateSystem.SetCorps(city, null)` | 手写 `Neutralize` 后再补字段 |
| 武将入城/改户口 | `PersonStateSystem.Enter` / `ChangeBelongCity` | 直接赋 `BelongCity` |
| 武将转在野 | `PersonStateSystem.LeaveToWild` | 手写 `state = Wild` |
| 武将变俘虏 | `PersonStateSystem.ToCaptive` | `city.AddCaptive` 与状态各写一半 |
| 俘虏归顺 | `PersonStateSystem.Recruit` | 改 `state` 后手工 `AddPerson` |
| 在途者迁户 | `PersonStateSystem.RelocateHomeOnCityFall` | 跳过 `CurrentCity != this` 的人 |

`CityStateSystem.SetCorps` 内部已经做了：取消以该城为目的地的在途任务、跨势力转移技巧点、
内政设施跟随改旗（`SyncBuildings`）、新旧势力账本全量重算。绕过它就会漏其中若干项。

另外：都市易主后必须调 `CityStateSystem.SyncSubCities(this)`（`City.cs:2781`），
否则会留下"都市属 A、港口/关隘属 B"的飞地，在军团统计、AI 出兵、补给判定里持续制造脏数据。

## 七、坑清单（摘要）

| 坑 | 结论 |
|---|---|
| 边遍历边 `building.OnFall` | `OnFall` 会经 `OnBuildingDestroy` 从 `allBuildings` 移除自己 → 必须先快照（`:2559-2564`） |
| 陷阱/障碍参与城陷结算 | 原版 `is_general_type()` 直接跳过，本项目按 `bt.IsExplosive \|\| bt.IsObstacle` 跳过 |
| 军事设施按"本城辖区"判 | 原版还有 `has_builder()` 前置，本项目 `Building.Builder` 无写入点，照搬会成死分支 → 以归属判定 |
| 异族/贼按普通势力处理 | 非 normal 走白城 + 全毁设施 + 不入城 + `update_bandits`，与占领互斥 |
| 白城被异族打 | 直接 `return`，不占领（`bandit_ignores_empty_kyoten`） |
| 灭亡时清理同势力港关 | 必须排除 `c == this`，否则 `BelongCorps` 被置空，后续 `Ruin` 空引用 |
| 灭亡时清理部队 | 主将已脱编的部队 `BelongForce` 为 null，需按副将归属兜底（`:2428-2435`） |
| 掠夺算农业/商业 | 原版只算金/粮/兵/兵装，开发度不动 |

完整案例（现象/根因/修法）见 `references/pitfalls.md`。

## 八、已知未对齐项（勿擅自补）

| 项 | 未实现原因 | 风险 |
|---|---|---|
| `building_reset_targeted`（原版 `:363`） | 缺"部队目标失效重定向"的通用设施 | 城易主后，正赶去占它的部队仍保留旧目标 |
| 同区域 `Expel`/`ComBuild` 转 `Retreat`（`:364-376`） | 需要遍历全部部队并判同区域 | 驱逐/建设中的部队会在敌城里继续作业 |
| 玩家攻方 9 回合禁谈（`:228`） | 本项目 `diplomacyBanTimer` 语义是"提案被拒后的禁谈期"，复用会污染外交 | 高 |

补任何一项前，先确认没有第二套机制已覆盖，并在注释里写明原版出处。

## 九、验证流程

```
1. 改完 City.OnFall 及其下游
2. refresh_unity(mode=force, scope=all, compile=request, wait_for_ready=true)
3. read_console(types=[error], count=40, include_stacktrace=true)   ← 唯一可信的编译诊断
4. read_lints(paths=[改动文件], severity=[error])                   ← 仅辅助，会漏报
5. python scripts/verify_city_fall_order.py                          ← 时序回归
```

`verify_city_fall_order.py` 以 `BelongForce.UpdateCityCount` 为界切出主流程，
校验 8 条规则（关系值在主流程前；设施/掠夺/灭亡先于改旗；改旗先于入城；
入城先于港关同步；港关同步先于事件；太守赋值与入城同块），任一失败返回非 0。

`read_console` 拿不到时必须写"**未能确认编译**"，禁止写"编译通过"。

## 十、交付格式

每次改动回复必须包含五节，缺一不可（可精简）：

1. **改动清单**：文件 + 关键行 + 一句话
2. **原版对齐表**：`s11_sys_event_kanraku.cpp:行号` → 本项目落点 → ✅/⚠️/❌
3. **未对齐/近似项及原因**
4. **验证结果**：编译、console 输出、lint（如实）
5. **实测建议**：具体操作 + 预期日志（`[城陷]` / `[掠夺]` / `[归属]` 前缀）

## 十一、资源

- `references/pipeline.md`：18 步逐步细节、分支判定、代码出处
- `references/pitfalls.md`：踩坑案例（现象 → 根因 → 修法 → 教训）
- `scripts/verify_city_fall_order.py`：`City.OnFall` 时序回归校验
