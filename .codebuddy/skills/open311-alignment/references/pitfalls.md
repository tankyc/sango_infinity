# 对齐踩坑记录

按发现顺序记录，每条包含：现象 → 根因 → 修法 → 可复用的教训。

## 1. 港关是否随都市改军团

**现象**：为实现"军团新设"功能，判断"新建军团时是否把都市的附庸港关一起带过去"。

**第一次结论（错）**：只读了 `create_district` 内部。

```22:25:s11/s11_sys_scen_district.cpp
    std::vector<Building*> building_list = kyoten_list;
    Building* city_building = city->get_building();
    if (not std::ranges::contains(building_list, city_building))
        building_list.push_back(city_building);
```

它只补"都督所在城"，看不到任何港关处理 → 误判为"不带港关"。

**真相**：参数 `kyoten_list` 由调用方 `exec_district_new_command` 构建：

```268:279:s11/s11_sys_person_order_kunshu.cpp
    std::vector<Building*> building_list;
    for (int i = 0; i < KyotenId_ExMax; i++)
    {
        Building* building = system_->get_building(system_->get_kyoten_building_id(i));
        if (not utils::is_alive(building) or building->get_force_id() != force->get_id()
            or kunshu->get_service_id() == i or center->get_building_id() == i)
            continue;
        District* district = system_->get_district(building->get_district_id());
        if (utils::is_alive(district) and district->get_number() != 1)
            continue;
        if (city_list[building->get_area_city_id()])
            building_list.push_back(building);
    }
```

`get_area_city_id()` 对港关返回**所属都市**的 id → 会带上。排除条件：
- `kunshu->get_service_id() == i` —— 君主驻守的据点不参与（这正是"主公在港关"的安全阀）
- `center->get_building_id() == i` —— 都督城由 `create_district` 补回
- `district->get_number() != 1` —— 新建时只从第一军团拆出去

**教训**：被调函数只处理"给定列表"，列表怎么来必须追调用方。

## 2. 陷阱命中即爆导致耐久为负

**现象**：改陷阱为"命中即爆"后，`durability` 中间值变成 `-500`。

**根因**：只在 `Building.OnHit` 里设 `durability = 0` 并返回 `false`，但 `ChangeDurability` 在 `OnHit` 之后还有：

```316:s11_sys_scen_building.cpp（对应 C# 的 BuildingBase.ChangeDurability）
            durability = durability + num;
```

`0 + (-500) = -500`。

**修法**：`OnHit` 返回 `true` 表示"本次命中已特殊处理"，`ChangeDurability` 里跳过累加直接进入销毁判定。

**教训**：改一个钩子的返回值语义，必须把它所在函数的**后续语句**全部读完。

## 3. 君主死亡后势力凭空消失

**现象**：把 `Force.IsAlive` 由 `mGovernor.BelongCity != null` 改成 `mGovernor.IsAlive` 后，君主一死势力就从 UI 与 AI 循环里消失，但城、军团、武将都还在。

**根因**：改了一个**依赖君主存活**的判定，却没检查"君主死亡时谁负责换人"。

全工程搜索 `mGovernor =` 只有剧本编辑器三处，运行期零处更新。原版有 `person_die → force_succeed → select_successor → force_koukei` 整条链，本项目完全没有。

**修法**：
- `Person.Dead()` 里缓存 `dyingForce` / `dyingCorps`（必须在 `SetState(Dead)` 之前，改完 state 后 `IsGovernor` 立刻为 false，末尾还会把 `BelongForce` / `BelongCorps` 置空）
- `Force.SelectSuccessor`（血缘 > 官职 > 统率）
- `Force.Succeed`（旧君主降级 → 新君主上位 → 解军师 → 迁都 → 军团收编 → 太守重算）
- 无继任者走 `Force.Ruin`

**配套坑**：`Ruin` 与 `Succeed` 开头都用属性 `IsAlive` 判存活 → 君主刚死、继承未执行时属性返回 false，导致 `Ruin` 永远走不进去。内部方法必须用**字段** `isAlive`。

**教训**：改变一个判定条件时，先问"这个条件依赖的字段由谁维护、什么时候更新"。

## 4. 军团继承的合并方向

**现象**：实现君主继承时把"旧第一军团"并入"继任者所在军团"。

**原版**：

```1427:1448:s11/s11_sys_scen_force.cpp
    District* old_district = get_district(successor->get_district_id());   // 继任者原军团
    District* new_district = get_district(self->get_district_id());        // 势力第 1 军团
    ...
            district_dissolve(old_district, new_district);   // 第一参数被解散
            district_set_totoku_id(new_district, successor_id);
```

`district_dissolve(self, merge_dest)` 的 `self` 是被解散方 → **保留第 1 军团**，把继任者军团并进去。

**修法**：新增 `Force.MergeCorps(from, to)` 显式指定接收方。不能用 `DeleteCorps` —— 它硬编码接收方为 `CapitalCorps`，而 `Succeed` 已把 `mGovernor` 换成继任者，会形成自合并。

**教训**：函数参数名（`old_district` / `new_district`）里的 "old/new" 指的是**相对继任者**，不是相对君主。读命名要结合赋值来源，不能只看名字。

## 5. 死代码分支不要实现

`person_leave` 里有一段"君主迁城时把目标城改挂君主军团"：

```1038:1039:s11/s11_sys_scen_person.cpp
    if (move and self->get_force_id() == dest->get_force_id() and self->is_kunshu() and self->get_district_id() != dest->get_district_id())
        building_change_district(dest, get_district(self->get_district_id()), BuildingDistrictChangeReason_0);
```

前置条件 `move == true`，但 `exec_move_command` / `exec_summon_command` / 外交 / 计略 / 录用 **全部传 `false`/`0`** → 该分支永不执行。

真正生效的是 `person_enter` 里的同一逻辑（到达时才改）。若在出发时也实现一遍，会出现"人还在路上，城已经换军团"的中间态，途中任务取消后城会永久留在错误军团。

**教训**：看到原版逻辑先确认它**有没有被走到**。搜索所有调用点，看参数是否满足前置条件。

## 6. 港关的"无主"是合法状态

巡检把 `sub.BelongCorps == null` 判成"军团失效"并挂到都市军团，日志刷屏 `港口xx 的军团已失效（无），跟随所属都市`。

**原版**：`building_neutralize` 会把 `district_` 置 `-1`，此时 `get_force_id()` 返回 `-1` = 中立。港关被单独攻占、长期属第三方都是正常玩法。

**修法**：无主直接放行；只有"军团引用还在但已解散/势力已灭亡"才算脏数据，且修复方式是**清成无主**，不是挂给都市。

**教训**：`null` / `-1` 在不同模型里的语义不同，判定前先查原版该字段的取值范围。

## 7. 俘虏可以在部队里

巡检把"俘虏的 `mBelongTroop != null`"判成脏数据并强行 `RemovePerson`，还把随军俘虏所在的部队解散了。

**原版**：俘虏必然挂在某个 location 上，城内或部队内都是合法形态。

**修法**：
- 城/部队二选一的关押位置
- 随军俘虏登记 `troop.captiveList`，户口为空，保留原势力
- `Release` 必须同时摘 `city.captiveList` 与 `troop.captiveList`
- `ToCaptive` 从原部队摘除时按来源区分：`captiveList.Contains` → `RemoveCaptive`，否则 → `RemovePerson`（否则会触发"成员摘光即解散"）

**教训**：判定"非法状态"前，先把原版里所有合法形态穷举一遍。

## 8. `SangoObjectList.Add` 重复仍插入

`Troop.EnterCity` 迁移随军俘虏入城时 `city.captiveList.Add(p)` 触发 `不能重复添加` 错误日志 —— 但它**仍然插入**了，产生真正的重复元素。

**修法**：所有 `SangoObjectList.Add` 调用点自己 `Contains` 判断；`List<Person>`（如 `Force.BeCaptiveList`）重复不报错但会导致重复结算（同一俘虏掷两次掉忠骰），同样要去重。

**教训**：不要依赖容器内部去重，尤其是"报错但继续"的实现。

## 9. 归属锚点必须是军团而不是城

原版：

```cpp
force_id_t MilitaryObject::get_force_id() const
{
    const District* district = scenario_->get_district(get_district_id());
    return is_alive(district) ? district->get_force_id() : -1;
}
district_id_t Person::get_district_id() const { return district_; }   // 自己的字段
```

武将势力 = **军团**派生，与 `service_`（所属城）无关。

Sango 用 `city.BelongForce` 做锚点会踩这个场景：在途武将的 `BelongCity` 仍指向出发城，而出发城可能已被敌方攻陷（城陷时原版用 `reason_0` 跳过成员迁移，在途者的 `service_` 保持指向陷落城）→ 用城校正会把还在路上的己方武将瞬间改成敌方势力。

**修法**：`Person.ChangeCorps` / `ChangeBelongCity` / `SyncFromCity` / `Recruit` / 巡检，全部改为"军团优先，军团为空才退回城"。

**教训**：给"存储字段"做纠错时，锚点必须与原版派生路径一致。

## 10. 城易主后内政设施没换势力

原版内政设施的势力不落盘，`get_force_id()` 每次读所属都市。Sango 存成字段，只靠 `City.UpdateCorps()` 同步，而 `CityStateSystem.SetCorps`（城易主底层入口）没调它 → "城已属 A、城里的农田还属 B"。

**修法**：新增 `CityStateSystem.SyncBuildings(city)`，只同步内政设施（军事设施/陷阱/障碍在原版有独立 `owner_force`），在 `SetCorps` 末尾调用。

**为什么不能直接调 `UpdateCorps`**：它会连在籍武将一起改，而城陷路径里武将已由调用方处理完毕，在途武将更不该被改成新势力军团。

**教训**：原版"动态继承"的字段，在改成"存储字段"时必须找出**所有**写入路径补齐。

## 11. `read_lints` 会漏报编译错误

**现象**：连续两轮改动后 `read_lints` 都返回 `0 error`，但 Unity 实际编译失败：

| 轮次 | 实际错误 | `read_lints` 结果 |
|---|---|---|
| 1 | `CS0111: Type 'City' already defines a member called 'GetJobCost' with the same parameter types` | **0 error** |
| 2 | `CS1061: 'Scenario' does not contain a definition for 'Month'` | **0 error** |

**根因**：`read_lints` 读的是 IDE 诊断缓存，Unity 重新编译后缓存未及时失效。

**修法**：
- **以 `read_console(types=[error])` 为唯一可信来源**
- `refresh_unity` 返回 success 只代表"刷新完成"，**不代表编译通过**
- `read_lints` 只作辅助，永远不能据此写"编译通过"

**配套**：`read_console` 会因编辑器重导入返回 `ping not answered`。超时期间禁止继续堆改动（错误会累积），应：
1. 只做小步改动
2. 回复里列出本轮动过的**全部文件清单**，方便用户对照 Console
3. 请用户手动贴报错

**教训**：工具报告"干净"不等于真的干净。诊断链上有缓存时，要选**离编译器最近**的那个。

## 12. 门禁与扣费不同源（15 处事故）

**现象**：玩家看到"按钮亮着可点，点下去资金/行动力不够"，或反过来"按钮灰着、其实够"。

**五类根因**：

| 类型 | 案例 |
|---|---|
| 门禁折后价、扣费原价 | 出阵：门禁 `GetJobCostAP`（军事府 5）、扣费 `JobType.GetJobCostAP`（10）→ 多扣 5 |
| 门禁查 A、扣费扣 B | 能力研究：`ActionPoint < ResearchGoldCost * 2`（**金币常量**当 AP 门槛） |
| 门禁写死金额 | 亲善 `gold >= 1000`，扣费 `GetJobCost()`（外交府减半 500） |
| 门禁查、扣费不扣 | 八项外交从不 `ReduceActionPoint`；摒弃/交换要 1000 金却免费 |
| 门禁不查、扣费失败 | 技巧研究不查金/技巧点，执行时 `return null` 静默失败 |

**修法**：
1. 收敛城级入口 `City.GetJobCostAP` / `City.GetJobCost`，门禁与执行**都走它**
2. 执行方法**开头**二次复核资源（必须放在"注册对象 / 摘武将 / 置状态位"**之前**，否则留下孤儿对象）
3. 批量操作按**总价**判（褒赏按人数、建造按所选建筑）
4. `cost = 0` 的工作不能用 `CheckJobCost` 当资金门禁（恒真）

**教训**：一对"能不能点 / 扣多少"只要分开写，就一定会漂移。收敛成一个入口。

## 13. 双尺度并存导致数值失真

**现象**：同一"关系值"两套刻度 —— 存储 raw（-5000~5000）、业务 like（0~100）。

| 调用 | 写的值 | 实际 | 原版 |
|---|---|---|---|
| 停战 | `AddRelation(+300 raw)` | +3 like | **不改** |
| 宣战 | `ReduceRelation(-1000 raw)` | -10 like | **无此规则** |
| 外交事件 | `AddRelation(+50 raw)` | **+0**（整数除法） | —— |
| 计略失败 | `200 raw` | ≈ 2 like | -8 |

**修法**：存储也改成原版 0~100；旧档用**值域探测**迁移（不是版本号）——
因为"直接加载原版剧本"时原版值天然在 0~100 内，不会误触发。

**迁移代码要点**：
```csharp
bool legacy = false;
for (...) if (row[j] < 0 || row[j] > 100) { legacy = true; break; }
if (legacy) row[j] = Clamp(50 + raw / 100, 0, 100);   // 换算 + 超范围修正
```

**连带**：迁移后所有按 raw 设计的 `ScenarioVariables` 默认值必须 ÷100（本次 15 个）。

**教训**：两套刻度的换算藏在 getter 里时，写入口一定会漏。

## 14. 把单点规则套到全体对象

**现象**：AI 反复"建农场 → 拆农场 → 再建"，同一块地永远空着。

**根因**：`FindUnnecessaryBuilding` 的实现

| 原版 | 本项目 |
|---|---|
| `get_unnecessary_facility_list(city, FacilityId_Mint)`：**只判铸币厂**，看周边**市场**数 | 对**所有**内政设施判"周边**同类** < 3 即冗余" |

后果：孤立农场（周边没有别的农场）→ 判冗余 → 拆掉 → AI 再建 → 死循环。

**教训**：原版函数名的**参数**（`FacilityId_Mint`）就是适用范围，不是"举例"。

## 15. "没有日志但对象消失了" = 存在静默清理路径

**现象**：玩家反馈"建筑凭空少一座，日志里查不到"。

**全库搜的结果**：7 条拆除路径里有 **2 条完全静默**：

| 路径 | 是否打日志 |
|---|---|
| `AIDestroy` Battle 分支 | ✅ |
| `AIDestroy` 冗余判定 | ✅ |
| `TryReplaceBuilding` | ❌ 静默 |
| `City.OnYearStart` 清黑市 | ❌ 静默 |
| **`Building.ChangeCorps` 未完工** | ❌ **静默且每次同步都触发** |
| `ChangeCity` 未完工 | 有 Error |

**最阴的一条**：`ChangeCorps` 里 `if ((!isComplate || isUpgrading) && BelongForce != newForce) OnFall()`，
而 `CityStateSystem.SyncBuildings` 只比较 `BelongCorps` —— 当"军团一致、势力字段脏了"时也会命中，
导致**每次军团同步都把在建设施清掉**。

**修法**：
1. 一条路径一条路径搜（`OnFall` / `Clear()` / `Remove`），**逐个加日志**
2. `ChangeCorps` 先判 `BelongCorps == corps` 就直接纠正字段返回，不进入拆除判定

**教训**：排查"对象莫名消失"时，**不要只信日志**，要把销毁入口穷举一遍。

## 16. 新增方法前必须搜同名

**现象**：`CS0111` —— `City` 已有 `GetJobCost(CityJobType)`，我又加了一个同签名方法。

**根因**：为了加"外交府亲善减半"直接新写方法，没先搜 `GetJobCost`。

**修法**：把新逻辑**合并进已有方法**（放在 `OnCityCheckJobCost` override 之后），
并让 `CheckJobCost` 改为调用 `GetJobCost`（它原本复制了一份实现，会二次分叉）。

**教训**：给已有类加"城级封装"前，先 `grep "public .* GetJobCost"`。

## 17. `SangoObject` 是弱基类，不保证有子类字段

**现象**：`CS1061: 'SangoObject' does not contain a definition for 'BelongForce'`。

**场景**：`Troop.ChangeTroops(int num, SangoObject atk, int atkBack)` —— 参数是基类，
但要取"攻击方势力"。

**修法**：按实际类型取，且注意**战法伤害的攻方挂在 `SkillInstance.master`**：

```csharp
Force atkForce = null;
if (atk is Troop atkTroop) atkForce = atkTroop.BelongForce;
else if (atk is SkillInstance si && si.master != null) atkForce = si.master.BelongForce;
```

**教训**：拿 `SangoObject` 当参数的方法里，任何子类字段都要先 `is` 判定。

## 18. 一价多做：一次 AP 干 N 件事

**现象**：AI 城的建造速度快得离谱，行动力像用不完。

**根因**：`AIBuilding` 内部按武将人数放大 `count`：

```csharp
int count = 1;
if (city.freePersons.Count > 6) count += (city.freePersons.Count - 3) / 3;
for (i < count) AIBuildIntriore(...);      // 建 N 座
for (i < count) AIBuildingLevelUp(...);    // 升 N 座
AIBuildMilitaryBuilding(...);              // 再建 1 座
```

而 `OutpostCommand` 只扣**一次** `GetJobCostAP(Build)` → 20 个武将的城用一次代价完成 13 次建造。

**原版**：`kyoten_develop` 一次只建**一座**、扣**一次** AP（`:141-145`）。

**修法**：拆分——建造 1 座、升级走 `AIOrderId.Merge`、军事建筑走 `AIOrderId.Build`，各自独立记账。

**教训**：`count` 这类"按规模放大"的循环，必须对应"按规模扣费"，否则就是作弊入口。

## 19. AI 军团"建→拆→建"死循环的三个独立成因

同一个现象（城里建筑反复消失/重建）追出三层，每层单独都能造成循环：

| # | 成因 | 修法 |
|---|---|---|
| 1 | **拆旧建新**（`TryReplaceBuilding`）是自创逻辑，原版没有 | 切断调用，建满时直接返回 false |
| 2 | **冗余判定套到全体设施** + 比较"同类"而非"配套" | 只判铸币厂/谷仓，看市场/农田 |
| 3 | **升级中的建筑被当成已完工拆掉**（`JobUpgradeBuilding` 只置 `isUpgrading`，`isComplate` 仍 true） | `FindUnnecessaryBuilding` 补 `isUpgrading` 排除 |

**补充**：原版"建满"判据是 `设施数量 >= 内政地数量`，因为**吸收合并会让数量下降**、之后还能继续发展；
本项目没有吸收合并，数量永不下降 → 必须改用**总等级**判据（每座建筑沿 `nextId` 链推算最终等级）。

**教训**：一个"循环"现象背后常有多条独立回路，修一条不会好，要逐条穷举。
