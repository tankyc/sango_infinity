# 委任部队 AI：物理分离方案评估

> 你的建议：`MissionType` 新增一套 `PlayerTroopXXXX`，行为类对应新增一套，各自维护。
> 结论：**方向正确，但不必复制代码**——有一个能让改动量下降一个数量级的做法。

---

## 一、先看现状：额外逻辑到底有多少

调研了全部 17 个 Troop 行为类，结论出乎意料：

> **势力 AI 的"额外逻辑"绝大部分**已经**集中在一个方法里**，而不是散落在行为类内部。

| 额外逻辑 | 位置 | 现状 |
|---|---|---|
| 态势分档与主动撤退 | `Troop.AIPrepare`（`Troop.cs:3290`） | 委任部队经 `AppointPrepare`（空实现）已完全跳过 |
| 断粮 / 兵少就近补给撤退 | `Troop.AIPrepare`（`Troop.cs:3330`） | 同上 |
| 向补给队求援 | `Troop.AIPrepare`（`Troop.cs:3246`） | 同上 |
| 随军增筑辅助建筑 | `Troop.AIPrepare` → `TryBuildFieldBuilding`（`Troop.cs:3436`） | 同上 |
| 态势权重修正（技能评分、择优池） | `Troop.GetTierWeights`（`Troop.cs:3634`） | **总闸**： `AIType==Appoint` 返回 null |
| 建完自动找下一个建址 | `TroopBuildBuilding.TryFindNextSite`（`:148`） | 单点判断 |
| 补给队"大劣就跑 / 离敌近就撤" | `TroopSupplyTroop.DoAI`（`:74`） | 单点判断 |

**行为类内部残留的 `AIType==Appoint` 判断只有 2 处**（`TroopBuildBuilding.cs:148`、`TroopSupplyTroop.cs:74`）。

其余行为类里的"额外逻辑"实际是**任务完成后的改派**（如 `TroopOccupyCity` → `TroopReturnCity`）——那属于**收尾**，不算态势逻辑，委任部队同样需要（否则部队会卡在原地）。

---

## 二、你的建议：全量物理分离的真实代价

### 需要新增的枚举（玩家实际可达的委任任务，9 项）

| 玩家入口 | 现有枚举 | 需新增 |
|---|---|---|
| `TroopInteractiveOccupyCity.cs:41` | `TroopOccupyCity` | `PlayerTroopOccupyCity` |
| `TroopInteractiveMoveToCity.cs:40` | `TroopMovetoCity` | `PlayerTroopMovetoCity` |
| `TroopInteractiveMoveToCell.cs:41` | `TroopMovetoCell` | `PlayerTroopMovetoCell` |
| `TroopInteractiveDestroyTroop.cs:47` | `TroopDestroyTroop` | `PlayerTroopDestroyTroop` |
| `TroopInteractiveDestroyBuilding.cs:41` | `TroopDestroyBuilding` | `PlayerTroopDestroyBuilding` |
| `TroopInteractiveBanishTroop.cs:50` | `TroopBanishTroop` | `PlayerTroopBanishTroop` |
| `TroopInteractiveBuildingFix.cs:40` | `TroopFixBuilding` | `PlayerTroopFixBuilding` |
| `CityTransport.cs:90` | `TroopTransformGoodsToCity` | `PlayerTroopTransformGoodsToCity` |
| `TroopActionBuild.cs:207` | `TroopFixBuilding` | （复用上面的） |
| 行为类内部改派（14 处） | `TroopReturnCity` 等 | `PlayerTroopReturnCity` 等 |

### 隐藏成本：改派矩阵翻倍

每个行为类的 `Prepare` 里都有"任务完成 → 改派"的逻辑，物理分离后每个改派点都要**判断自己是哪一套**：

```csharp
// TroopOccupyCity.cs:28 现在
Troop.SetMission(MissionType.TroopReturnCity, ...);

// 分离后必须变成
Troop.SetMission(IsAppoint ? MissionType.PlayerTroopReturnCity
                           : MissionType.TroopReturnCity, ...);
```

**这个判断会出现在 14 个改派点里**——比现在的 2 处判断还多。

### 存档风险

`missionType` 是 `[JsonProperty] int` 落盘（`Troop.cs:358`）。旧存档中玩家部队的 `missionType = 8`（`TroopOccupyCity`）读入后**不会**自动变成新枚举 → 必须加**归一化迁移**逻辑，否则老存档的委任部队会走错行为类。

---

## 三、关键洞察：派发点其实一处都不用改

如果按"改所有玩家派发点"来做，要动 9 个 `TroopInteractive*.cs` + `CityTransport` + `TroopActionBuild`。

**但可以完全不动**——把映射收敛到 `Troop.SetMission` 一处：

```csharp
public void SetMission(MissionType missionType, int missionTarget)
{
    // 【委任部队】玩家第一军团的部队自动使用 PlayerTroop 系列枚举
    if (IsPlayerControl)
        missionType = TroopMissionBehaviour.ToPlayerMission(missionType);

    this.missionType = (int)missionType;
    this.missionTarget = missionTarget;
    NeedPrepareMission();
}
```

**收益**：
- 9 个玩家派发点 + `CityTransport` + `TroopActionBuild`：**全部不用改**
- 14 个行为类内部改派点：**全部不用改**（自动被映射）
- 唯一需要显式处理的是**旧存档归一化**

这一处映射，把"物理分离"的改动面从 ~25 处压到 ~3 处。

---

## 四、三种实现路线

### 路线 A：纯复制分离

```
MissionType.PlayerTroopOccupyCity = 新增
PlayerTroopOccupyCity.cs          = 把 TroopOccupyCity 的核心逻辑复制一份
```

- ✅ 两套逻辑彻底独立
- ❌ **9 个类、每个几十行重复代码**
- ❌ 势力 AI 行为类修 bug 时，委任版要同步改一遍（容易漏）

### 路线 B：继承分离（**推荐**）

```
MissionType.PlayerTroopOccupyCity = 新增

// 薄壳：继承势力 AI 版，只覆写"准备"阶段
public class PlayerTroopOccupyCity : TroopOccupyCity
{
    public override MissionType MissionType => MissionType.PlayerTroopOccupyCity;

    // 势力 AI 的 Prepare 全是"改派 / 态势"，委任版只做目标绑定 + 最小收尾
    public override void Prepare(Troop troop, Scenario scenario) { /* 核心准备，无改派 */ }

    // DoAI 完全复用父类的核心行为（移动 / 攻击），零重复
}
```

- ✅ 物理分离（独立枚举 + 独立类）
- ✅ **核心行为零重复**（继承）
- ✅ 未来分叉时，覆写哪个方法就改哪个，互不影响
- ✅ 改动面小：~10 个薄壳类 + 1 处映射
- ⚠️ 需要约定：委任版只覆写 `Prepare`（准备），`DoAI` 优先复用

### 路线 C：保持现状（`TroopAIType` 类型化）

- ✅ 零新增、零迁移风险
- ❌ 判断仍在行为类里（现有 2 处，未来可能变多）

---

## 五、三个必须一起定的细节

### 5.1 委任部队任务完成后做什么？

| 选项 | 行为 | 评价 |
|---|---|---|
| **(a) `ClearMission()`** | 清空任务，交回玩家手动控制（部队原地待命） | **最纯粹的委任语义**。部队停在原地不动，玩家需要自己处理 |
| **(b) 沿用现有改派** | 自动 `TroopReturnCity` 回城 | 省心，但算"额外逻辑"，且会改变部队位置 |
| **(c) 可配** | 新增开关 `appointAutoReturn` | 灵活，多一项配置 |

> 注意：选 (a) 时，如果部队停在敌方城池旁，可能被消灭——这是"委任=只做这件事"的必然代价。

### 5.2 旧存档归一化

需在 `Troop.Init` 或首次 `DoAI` 时执行一次：

```csharp
// 玩家第一军团且任务为 Troop 系列 → 转成 PlayerTroop 系列
if (IsPlayerControl)
    missionType = (int)TroopMissionBehaviour.ToPlayerMission((MissionType)missionType);
```

### 5.3 委任版是否需要"任务失效"处理

比如委任"占领城池"，但该城池被友军抢先占领了。势力 AI 会改派新任务；委任版应当**直接完成并 `ClearMission()`**（告知玩家任务已无意义）。

---

## 六、改动量对照

| 维度 | A 纯复制 | **B 继承（推荐）** | C 保持现状 |
|---|---|---|---|
| 新增枚举 | ~10 | ~10 | 0 |
| 新增类 | ~10（各几十行） | ~10（各 10-30 行薄壳） | 0 |
| 派发点改动 | 0（映射收敛） | 0（映射收敛） | 0 |
| 改派点改动 | 0（映射收敛） | 0（映射收敛） | 0 |
| 重复代码 | **多** | **无** | 无 |
| 存档迁移 | 需要 | 需要 | 不需要 |
| 未来分叉成本 | 高（改两处） | 低（只改覆写的方法） | **越来越高** |
| 与现有 `TroopAIType` | 可删除 | 可删除或保留为兜底 | 保留 |

---

## 七、我的建议

**推荐路线 B**，理由：

1. 你的核心诉求（物理分开、各自维护、未来可分化）**完全满足**
2. 但**不必复制核心行为**——继承即可，避免"改一处漏一处"
3. 借助 `SetMission` 单点映射，改动面从 25 处压到 3 处
4. 现在行为类里那 2 处 `AIType` 判断可以删掉，代码更干净

需要你定的：第一、五节的三个细节（**任务完成后行为** / **旧存档归一化** / **任务失效处理**）。
