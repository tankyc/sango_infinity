# 城陷链路逐步细节

原版权威：`D:\open311\s11\s11_sys_event_kanraku.cpp` → `System::kyoten_kanraku`（`:154-378`）。
本项目落点：`Project/Assets/Sango/Scripts/Game/Object/City/City.cs` → `City.OnFall`（`:2262-2807`）。
下文行号如无特别说明，均指这两个文件。

> **章节按原版顺序编排**，但本项目在两处与实际代码顺序不同：
> 1. 第 9 节（掠夺）与第 10 节（设施）在 `City.OnFall` 里是**设施在前、掠夺在后**
>    （`City.cs:2542` / `:2659`）。两步数据无耦合，不影响结果，但看日志会先看到设施结算。
> 2. 第 12 节（灭亡）在代码里紧跟设施之后（`:2644-2656`），早于掠夺。

---

## 0. 前置归一化与短路

```2268:2283:Project/Assets/Sango/Scripts/Game/Object/City/City.cs
            Troop atk = atker as Troop;
            SkillInstance skillInstance = null;
            if (atker.ObjectType == SangoObjectType.SkillInstance)
            {
                skillInstance = (atker as SkillInstance);
                atk = skillInstance.master;
            }
            ...
            if (atk == null) return;
            if (BelongForce == atk.BelongForce)
                return;
```

- `atker` 有两种形态：`Troop` 与 `SkillInstance`。技能攻城走 `SkillInstance.cs:820`，
  必须能还原出 `master` 部队，否则后面取 `atk.Leader` / `atk.BelongCorps` 全部空引用。
- `BelongForce == atk.BelongForce` 直接返回：友军无法"攻陷"自家城。
  白城（`BelongForce == null`）不在此短路内，走第 11 步的白城分支。

对应原版 `:156-159` 的 `is_alive(self)` / `is_alive(by)` 前置检查。

---

## 1. 势力最后一城判定（原版 `:185-194`）

```185:194:s11_sys_event_kanraku.cpp
    force_id_t force_id = self->get_force_id();
    Force* force = get_force(force_id);
    City* city = self->get_city();
    if (utils::is_alive(force))
    {
        std::vector<City*> city_list = get_force_city_list(force);
        std::erase(city_list, city);
        if (not city_list.size())
            force->set_ruined(true);
    }
```

本项目不在这里置灭亡位，而是在算完 `escapeCity` 后用 `BelongForce.CityCount == 0` 判定
（`:2370-2373`），再到 `:2405` 才写 `IsAlive = false`。
**原因**：`IsAlive = false` 会让 `BelongForce.CityList` 等账本立刻不可用，
而第 11 步的港关清理、部队清理还需要读它们。

## 2. 播报（原版 `:195` `kanraku_report`）

`:2386-2388` 一条带上下文的日志，明确三种信息：城名、旧势力、攻方、残部去向（或"最后一座城"）。
后续每个武将的结果都挂在它下面，方便按城排查。

## 3. 清火（原版 `:196`）

`:2300-2309` 遍历 `OccupyCellList`，`cell.fire.Clear(); cell.fire = null;`。
必须在改旗前做，否则新势力接手时城内还在烧。

## 4. 事件（原版 `:197-200`）

原版按 `facility_id` 分流 `on_city_falls` / `on_gate_falls`。
本项目只有一条 `GameEvent.OnCityFall`（`:2783`），下游自行用 `IsCity/IsGate/IsPort` 判断。
**位置**：必须在改旗、入城、港关同步全部完成之后派发，否则订阅方读到的 `BelongForce` 还是旧值。
白城分支（`:2333`）另有一次派发，是"占领白城"的独立路径，不要合并。

## 5. 奖励（原版 `:201-220`）

原版常量：城 2000 功绩 / 5 经验，关 1000 / 3，港 500 / 3。
本项目不在城陷里发，而在打崩守军的调用点发：
`SkillInstance.cs:812-818` 按 `IsCity/IsGate/IsPort` 取 `GainValueConfig.Merit(...)`，
再 `troop.GainEP(ep, false, true)`。
**新增攻城来源时必须自己发奖励**，`City.OnFall` 内部不发。

## 6. 关系值与禁谈（原版 `:221-229`）

```221:229:s11_sys_event_kanraku.cpp
    if (is_alive(by_force) and is_alive(force))
    {
        if (force->get_like(by_force_id) > 15)
            force_set_like(force->get_id(), by_force_id, 15);
        else
            force_add_like(force->get_id(), by_force_id, -1);
    }
    if (is_alive(by_force) and by_force->is_player() and by_force->get_diplomacy_ban_timer() < 9)
        force_set_diplomacy_ban_timer(by_force, 9);
```

- 关系值：`DiplomacyRules.ApplyHostilityPenalty`（`:2289`）已覆盖"压到 15 地板 / 否则 -1"。
  语义是"打完还能再谈"，一场战争最多压到 15。
- 禁谈 9 回合：**未实现**。本项目 `Force.diplomacyBanTimer` 语义是"我方提案被 AI 拒绝后的禁谈期"
  （`DiplomacyActionTruce.cs:217`、`DiplomacyActionAlliance.cs:227` 写入），
  复用到城陷会污染外交状态，属高风险改造。

## 7. 我方俘虏归顺（原版 `:230-250`）

原版只处理 `location_id == self->get_location_id()` 的 `Mibun_Horyo`，
本方（`person->get_force_id() == by_force_id`）→ `Mibun_Ippan` + 改军团 + `cancel_all_order`；
其余 `person_release`。

本项目 `:2743-2751`：
```2743:2751:Project/Assets/Sango/Scripts/Game/Object/City/City.cs
            captiveList.ForEach(person =>
            {
                if (person != null && person.IsSameForce(atk))
                {
                    PersonStateSystem.Recruit(person, this, PersonChangeReason.Released);
                    if (!freePersons.Contains(person))
                        freePersons.Add(person);
                }
            });
```
`city.captiveList` 本身已经按据点过滤，所以不需要再判 `location_id`。

## 8. 抓捕与招降（原版 `:251-263`）

```251:263:s11_sys_event_kanraku.cpp
    auto [captured, all] = capture(self, by, indirected, -1, -1, false);
    all.append_range(captured);
    person_list = get_building_member_list(self, { Mibun_Kunshu, Mibun_Totoku, Mibun_Taishu, Mibun_Ippan });
    ...
    horyo_shoguu(all, captured, self, by);
```

本项目拆成两段：
- **判定段**（`:2474-2538`）：遍历 `allPersons`，先处理"物理不在本城"的在途/出征者
  （`PersonStateSystem.RelocateHomeOnCityFall`），再对留在城里的人掷 `captureChangceWhenCityFall`。
  豁免条件见 `IsCaptureImmune`（`:2832-2843`）：名马（`EquippedHorse != null`）或强运（特技 #33）。
  君主（`person == personForce.mGovernor`）不参与被俘判定。
  最后一城时**全员必俘、不掷骰**（对齐原版 `:452-456` 的 `not node.remaining`）。
- **招降段**（`:2793-2795`）：把 `temp_captive_list` 交给
  `CityRecruitPersonWhenCityFallEvent`，按 `PersonRecruitType.OnCityFall` / `OnForceFall` 走 UI 流程。

捕捉率配置项：`ScenarioVariables.captureChangceWhenCityFall`（只此一档）。
历史上曾配过"最后一城抓捕率"，唯一使用点在 `escapeCity != null` 分支内，永远读不到，已移除。

## 9. 掠夺（原版 `:264-277`）

```264:277:s11_sys_event_kanraku.cpp
    int rate = 5;
    if (utils::is_alive(by_leader))
        rate = std::max(rate, by_leader->get_stat(PersonStatType_Charisma) / 10);
    int gold = self->get_gold() * rate / 100;
    building_set_gold(self, gold);
    ...
```

本项目 `:2681-2710`，要点：
- 保留率 `keepPercent = max(5, Leader.Glamour / 10)`，**不是随机数**。
- 只算金 / 粮 / 兵力 / 兵装（`itemStore` 逐项 `Set(n * keepPercent / 100)`）；
  **农业与商业开发值不动** —— 原版城陷只毁设施，开发度保留。
- 兵装用 `Set` 精确赋值而非 `Split` 折算，避免"按拆走量取整"与原版"按保留量取整"的 1 点误差。
- 异族/贼攻破：`gold/food/troops=0`、`itemStore.Clear()`，同样不清开发度
  （对应 `building_neutralize` → `building_empty`，`s11_sys_scen_building.cpp:935-955`）。

## 10. 设施存留（原版 `:278-342`）

判定顺序（照抄，不要重排）：
1. `is_general_type()` → 跳过（陷阱 / 爆炸物 / 障碍，本项 `bt.IsExplosive || bt.IsObstacle`）。
2. 内政设施（`bt.IsIntrior`）：只取 `area_city_id == 本城`。
   - 已完工 → 进 `dev` 候选表（原版额外排除铜雀台，本项目无此设施）。
   - 未完工 → 立即 `building.OnFall(atk)`。
3. 军事设施（`bt.IsMilitary`）：只拆 `force_id == 守方` 且 `area_city_id == 本城` 的。
   原版 `has_builder()` 前置在本项目无写入点（`Building.Builder` 恒空），照搬会成为死分支。
4. `charisma = max(20, 攻方主将魅力)`；`shuffle(dev)`；
   `n = devCount - min(devCount, charisma / 20)`；非 normal 势力 → `n = devCount`（全毁）。
5. 前 `n` 个 `OnFall`，其余 `ChangeCorps(atk.BelongCorps)` 并清空 `Builder` / `Workers`。

**必须用快照遍历**：`OnFall` 会经 `City.OnBuildingDestroy` 从 `allBuildings` 里移除自己，
直接 `ForEach` 会漏元素（`:2557-2564` 已踩过）。

魅力 100 的主将最多保下 5 座内政设施，其余全烧 —— 这是"打大城很赚"之外的平衡点。

## 11. 白城分支（原版无对应实现）

`BelongCorps == null`（无主据点）时走 `:2311-2335`：
- 异族/贼：直接 `return`，不占领（`bandit_ignores_empty_kyoten`，`s11_sys_unit_action.cpp:222-230`）。
- 正常势力：直接改 `BelongCorps/BelongForce`，若 `skillInstance` 非范围技能则
  **先 `atk.EnterCity(this)` 再指定 `Leader`**（`:2323-2329` 的注释解释了反序的后果）。
- 早退：不掠夺、不处理设施、不清财政，因为白城本来就没有资源与守军。

## 12. 灭亡分支（原版 `:185-194` + `Force.Ruin`）

`escapeCity == null` 时（`:2405-2469`）：
1. `BelongForce.IsAlive = false; BelongCorps.IsAlive = false;`
2. 清同势力全部部队：`troop.BelongForce == 本势力` 直接 `Clear()`；
   主将为 null 时按副将归属兜底（否则残留部队永远拿不到任务，冻结原地）。
3. 清同势力港关：排除 `c == this`，逐个 `LeaveToWild` + 建筑 `OnFall` + 俘虏 `Escape`。
4. 所有人（含在途）`LeaveToWild` 并进 `temp_captive_list`。
5. `BelongForce.Ruin(atk.BelongForce, this, atk)`（`:2654`）：
   **返回 true 表示天下一统，必须立刻 `return`**，不得继续改旗与指定太守。

## 13. 改旗（原版 `:343-346`）

```343:346:s11_sys_event_kanraku.cpp
    if (is_alive(by_force) and by_force->is_normal() and is_alive(by_district))
        building_change_district(self, by_district, BuildingDistrictChangeReason_0);
    else
        building_neutralize(self);
```

本项目 `:2726-2729` → `CityStateSystem.SetCorps(this, corps)` / `SetCorps(this, null)`。
`SetCorps` 负责：势力与军团同步、账本全量重算、内政设施跟随、取消以该城为目的地的在途任务、
跨势力技巧点转移。

**位置必须在掠夺与设施之后**（`:2712-2725` 有完整理由注释）。

## 14. 入城 / 异族转进（原版 `:347-358`）

```347:358:s11_sys_event_kanraku.cpp
    if (is_alive(by_unit))
    {
        if (by_force->is_normal())
        {
            if (map_.range_contains(self->get_pos(), ... , by_unit->get_pos()))
                unit_enter(by_unit, self);
        }
        else
        {
            update_bandits(self, by_unit);
        }
    }
```

本项目 `:2754-2758`：`Leader = atk.Leader; atk.EnterCity(this);`（仅非异族且非范围技能）。
范围技能（`skillInstance.IsRange()`）不触发入城 —— 弓/弩/火计隔着格子烧掉的城，部队并不在城下。

异族走 `UpdateBanditsAfterFall`（`:2857-2903`）：
- 城被攻陷 → 该目标的异族部队 `Clear()` 自毁；
- 港关被攻陷 → `GetBanditNextTarget` 找同城区下一个正常势力据点，找不到则解散。

## 15. 太守与后处理（原版 `:359-362`）

- `building_appoint_taishu`：本项目直接用攻方主将（`Leader = atk.Leader`），
  没有原版"从城内武将里挑一个"的候选逻辑。
- 耐久补半在 `City.ChangeDurability` 里做（`:2134-2147`），不在 `OnFall` 里，
  因为两条入口（耐久归零 / 兵力归零）都要走到。
- 港关同步：`CityStateSystem.SyncSubCities(this)`（`:2781`），原版无对应调用
  （原版港关归属是动态派生，本项目存成字段，必须显式同步）。

## 16. 演出（本项目独有）

```2785:2804:Project/Assets/Sango/Scripts/Game/Object/City/City.cs
            if (atk.BelongCorps != null && atk.BelongCorps.IsPlayer)
            {
                RenderEvent.Instance.Add(new Render.CityFallCompleteEvent() { city = this });
            }
            ...
            RenderEvent.Instance.Add(new Render.ForceFallCompleteEvent() { force = destroyedForce });
```

`CityFallCompleteEvent` 只在**玩家势力**攻陷时播；`ForceFallCompleteEvent` 只在势力灭亡时播。
招降演出 `CityRecruitPersonWhenCityFallEvent` 无条件加，由它自己判空列表。

---

## 调用方检查清单（改 `OnFall` 前逐条确认）

1. 我改的字段，还有谁在读？`BelongForce` / `BelongCorps` / `Leader` / `escapeCity` 的派生关系。
2. 新加的 `return` 会不会跳过 `GameEvent.OnCityFall` → UI 不刷新？
3. 新加的清空操作会不会在别人遍历时改集合？（`allPersons` / `allBuildings` / `troopsSet`）
4. 新加的 `OnFall` 递归调用会不会回到 `City.OnFall`？（`Building.OnFall` 不会，`City.OnFall` 会）
5. 新加的日志用了哪种级别？城陷必须 `Info`，异常 `Warning/Error`，禁止 `Debug`。
