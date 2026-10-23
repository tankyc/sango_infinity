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
