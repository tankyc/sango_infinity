# 部队 AI 评分规则梳理与分级策略方案

> 状态：**已全量实施**（A + B + C + D3 组合，编译 0 错误）
> 相关文件：`TroopAIUtility.cs`、`Troop*.cs`（各 MissionBehaviour）、`Troop.cs`、`BattleSituation.cs`、`AIConfig.cs`

---

## 实施状态（本轮已完成）

采纳组合：**A（态势分档）+ B（角色分级）+ C（评分重构）+ D3（按档位择优）**
配置模板：`Build/Content/Data/Common/AIConfig.json`

| 阶段 | 内容 | 落地位置 |
|---|---|---|
| P0 | 统一态势评估器（方案 E） | `BattleSituation.EvaluateBalance` / `BalanceSnapshot` / `GetTier` |
| P0 | 态势分档（方案 A） | `TroopBattleTier` + `TroopTierWeights` + `AIConfig.#region 部队态势分档` |
| P1 | 评分重构（方案 C） | `SkillStatusPriority` 常数全外置；`ApplyTaskBonus` 统一任务倍率 |
| P2 | 角色分级（方案 B） | `TroopRole` + `TroopRoleWeights` + `Troop.ResolveRole` |
| P3 | 按档位择优（方案 D3） | `WeightList.RandomGetTop` + `AIPolicy.bestN` |

**新增文件**

```
Troop/TroopAITier.cs     TroopBattleTier 枚举 + TroopTierWeights + AIPolicy（档位×角色合成）
Troop/TroopAIRole.cs     TroopRole 枚举 + TroopRoleWeights
```

**关键消除的魔法数字**

| 原值 | 现状 |
|---|---|
| `+500000`（主目标）/ `=5`（非目标） | `taskPrimaryTargetWeight=5000` / `taskOtherTargetWeight=5`（倍率，%） |
| `+1000000`（原地施法）/ `+50000`（近战贴脸） | `taskStayBonus=200` / `taskMeleeCloseBonus=50`（%） |
| `+30000`（打大部队） | `taskBigTroopBonus=30`（%）+ `taskBigTroopPercent=150` |
| `150` / `200` / `30` / `15` / `5` / `10` / `4` / `15` / `8` | 全部迁至 `AIConfig.#region 部队技能评分` |
| `Random(500,1000)`（护城加成） | `protectCityNearEnemyBonusPerCell=40`（%）+ `protectCityThreatRange=5` |

---

## 一、现状：评分体系全景（改造前）

### 1.1 五层链路

```
第 1 层  技能收益    SkillStatusPriority            技能 × 目标格的基础收益
第 2 层  反击惩罚    SkillDefencePriority           贴脸打高反击单位的扣分
第 3 层  任务加成    各 Behaviour.SkillAttackPriority  目标优先级修正
第 4 层  行动选择    wightList.RandomGet()          加权随机选出本回合行动
第 5 层  任务切换    Troop.AIPrepare                全局策略（目前只有"撤退"）
```

### 1.2 逐层细节

#### 第 1 层：技能基础收益 `SkillStatusPriority`

**打部队**（`TroopAIUtility.cs:544`）

```csharp
(skill.atk + strategy_factor + (IsRange?15:0) + (IsSingle?5:0) + (HasEffect?5:0))
  * 150 / Math.Max(10, skill.costEnergy)
  * (troop.Attack - target.Defence + 200)
```

- `strategy_factor`：目标被控制（混乱/眩晕）时 +30
- 策略技能成功率 `< 75` 直接判 0 分
- 火计（Id 22）对已有火焰的格子判 0；灭火（Id 23）对无火格子判 0
- 打友军（`canDamageTeam`）返回**负分**

**打建筑**（`TroopAIUtility.cs:572`）

```csharp
(skill.atkDurability + (IsSingle?5:0) + (HasEffect?5:0)) * 4 * rangeFactor * (IsSingle?15:10)
// rangeFactor = IsRange ? 150 : 100；策略成功率高时 rangeFactor += 2*(succ-75)
```

#### 第 2 层：反击惩罚 `SkillDefencePriority`

```csharp
if (skill.IsRange() || skill.IsStrategy()) return 0;      // 远程 / 策略不吃反击
float hitBack = target.troop.GetAttackBackFactor(skill, 距离);
return -Ceil(hitBack * CalculateSkillDamage(目标→我)) * counterAttackPenaltyFactor(10);
```

兜底（`TroopAIUtility.cs:202`）：反击惩罚最多削弱攻击收益到 `counterAttackPenaltyFloorPercent`（50%），避免 AI 完全不行动。

#### 第 3 层：任务加成（三个 Behaviour 高度重复）

| Behaviour | 对主目标 | 对其它目标 | 原地施法 | 近战原地 |
|---|---|---|---|---|
| `TroopOccupyCity` | 目标城 `+500000` | 建筑 `=5` | `+1000000` | `+50000` |
| `TroopOccupyCity` | 大部队 `+30000` | — | — | — |
| `TroopDestroyTroop` | 目标部队 `+500000` | 部队 `=5` | `+1000000` | `+50000` |
| `TroopProtectCity` | 近城敌 `+距离×Random(500,1000)` | — | `+100000` | `+50000` |

#### 第 4 层：行动选择

```csharp
public static PriorityActionData PriorityAction(...) {
    ...
    return wightList.RandomGet();     // 按权重随机
}
```

#### 第 5 层：任务切换 `Troop.AIPrepare`

```
① 状态不佳且有可求援补给队  → TroopAskSupply
② 断粮 / 兵力过低 / 士气崩溃 → 就近撤退到己方据点（TroopMovetoCity）
```

**仅此两条全局策略**。

### 1.3 已有的态势感知能力（及使用情况）

| 能力 | 位置 | 实际使用者 |
|---|---|---|
| `EvaluateThreat` | `BattleSituation` | `CityAI`（城池防守） |
| `EvaluateCityThreat` | `BattleSituation` | `CityAI` |
| `EvaluateForceSnapshot` / 实力比 | `BattleSituation` | `ForceAI` |
| `IsForceLosingAt`（局部兵力占比） | `TroopSupplyTroop` | **仅补给队** |
| `EvaluateCellSafety`（格子安全度） | `TroopAIUtility` | ⚠️ `MoveToTargetSafely`（多数场景未被调用） |
| `EvaluateCityDefenceStrength` | `TroopAIUtility` | ⚠️ **无任何调用方（死代码）** |

> **关键缺口**：**作战部队（非补给队）完全没有局部态势感知** —— 它不知道自己这一片是优势还是劣势，因此无法采取不同策略。

---

## 二、问题清单

| # | 问题 | 严重度 | 具体表现 |
|---|---|---|---|
| S1 | **魔法数字量纲混乱** | 高 | `500000` / `1000000` 与 `skill.atk`（几十）直接相加，任务加成**完全压倒**技能评分 |
| S2 | **无分级策略** | 高 | 精锐与残兵、优势与劣势用同一套评分与行为 |
| S3 | **撤退是唯一策略** | 高 | 只有"状态差 → 撤"，没有"优势 → 追击""均势 → 试探" |
| S4 | **加权随机而非择优** | 中 | `RandomGet()` 可能选中明显次优行动；精锐部队也会"犯低级错误" |
| S5 | **Behaviour 评分重复** | 中 | `TroopOccupyCity` / `TroopDestroyTroop` / `TroopProtectCity` 结构几乎一致 |
| S6 | **常数硬编码** | 中 | `150`/`200`/`30`/`15`/`5`/`4`/`8` 全在代码里，无法调参 |
| S7 | **安全评估未启用** | 中 | `MoveToTargetSafely` 在进攻/防守中被注释掉，只用 `TryCloseTo` 直冲 |
| S8 | **威胁模型过简** | 低 | `(100 - 距离×30) × 兵力/1000`、2 格范围、未考虑兵种克制与我方增援 |
| S9 | **无部队角色概念** | 中 | 只有 MissionType（去哪），没有 Role（**怎么打**） |
| S10 | **死代码** | 低 | `EvaluateCityDefenceStrength` 无调用方 |

---

## 三、分级策略方案（四个可选维度）

### 方案 A：战场态势分档（Tier）

**核心**：每回合为每支部队计算一个"局部战场态势档"，档位驱动评分权重与行为倾向。

**分档定义（阈值可配）**

| 档位 | 我方兵力占比 | 含义 |
|---|---|---|
| `Decisive` 碾压 | ≥ 65% | 可强攻、可追击 |
| `Advantaged` 优势 | 45% ~ 65% | 主动进攻 |
| `Even` 均势 | 35% ~ 45% | 谨慎试探 |
| `Disadvantaged` 劣势 | 25% ~ 35% | 收缩防守 |
| `Critical` 危局 | < 25% | 保存实力、脱离 |

**档位权重表（示例，全部外置）**

| 参数 | 碾压 | 优势 | 均势 | 劣势 | 危局 |
|---|---|---|---|---|---|
| 攻击收益倍率 | 1.4 | 1.2 | 1.0 | 0.8 | 0.5 |
| 反击惩罚倍率 | 0.5 | 0.8 | 1.0 | 1.5 | 2.0 |
| 撤退触发概率 | 0 | 0 | 10% | 40% | 80% |
| 追击距离上限 | 大 | 中 | 中 | 小 | 0 |
| 站位偏好 | 贴脸 | 前压 | 正常 | 后撤 | 逃 |

**改动落点**

- 新增 `TroopBattleTier.cs`：评估器（复用 `IsForceLosingAt` 的局部兵力统计口径）
- `AIConfig` 新增 `#region 部队态势分档`：5 个阈值 + 5 组权重
- `PriorityAction` 增加一个 `tierMultiplier` 参数（默认 1.0，不影响玩家部队）
- `Troop.AIPrepare` 用档位替代固定的撤退概率

**优点**：改动集中、见效快；作战部队立刻获得态势感知；权重表可 JSON 热调。
**缺点**：档位边界需调参；未考虑兵种克制。

---

### 方案 B：部队角色分级（Role）

**核心**：给部队一个"怎么打"的定位，与 MissionType（去哪）正交。

| Role | 行为特征 | 适用 |
|---|---|---|
| `Assault` 攻坚 | 高攻低守、优先打城/大部队、敢吃反击 | 攻城队 |
| `Defender` 防守 | 守城/守要道、重视地形与规避反击 | 守城队 |
| `Harasser` 骚扰 | 专打运输队/落单部队，打完就跑 | 轻骑 |
| `Skirmisher` 游击 | 低血也敢打，靠机动找机会 | 残兵 |
| `Escort` 护卫 | 贴身保护补给队/主将 | 护卫 |
| `Guard` 守备 | 驻守据点，不主动出击 | 关卡守军 |

**Role 分配方式（三选一或组合）**

1. **由 `CityAI` 派兵时指定** —— 攻城队 = `Assault`，守城队 = `Defender`
2. **由部队属性推导** —— 武力高 + 攻击兵种 = `Assault`；统率高 + 防御兵种 = `Defender`；骑兵 = `Harasser`
3. **由 `MissionType` 映射** —— `OccupyCity` → `Assault`，`ProtectCity` → `Defender`

**每个 Role 配一组评分权重**（复用并参数化现有 `SkillAttackPriority` 结构）。

**优点**：玩法层次感强，AI 行为差异化明显。
**缺点**：需要 Role 生命周期管理（谁分配、何时切换、随任务变化）；改动面比 A 大。

---

### 方案 C：评分公式重构（去魔法数字、统一量纲）

**核心**：把常数外置，并让各层分数**同量纲可加**。

```csharp
// 现状：两套量纲直接相加
score = (skill.atk + 15 + 5 + 5) * 150 / cost * (atk - def + 200);   // 约几万
score += 500000;                                                      // 五十万 —— 压倒一切

// 重构后：分层归一化
finalScore = baseSkillScore      // 归一化到 0 ~ scoreBaseMax（默认 1000）
           * taskWeight          // 任务权重（主目标 = 10.0，其它 = 0.01）
           * tierMultiplier      // 态势倍率（方案 A）
           + positioningBonus    // 站位加成（原地 +50 / 近战贴脸 +30）
```

**优点**：可控、可解释、可调参；为方案 A/B 提供干净的挂载点。
**缺点**：会改变现有 AI 行为，需回归测试（建议先用 JSON 保持旧值做等价验证）。

---

### 方案 D：行动选择从"加权随机"改为"择优"

| 选项 | 做法 | 特点 |
|---|---|---|
| D1 | 直接取最高分 | AI 最强，但行为可预测、易被玩家利用 |
| **D2** | 取 **top-N** 加权随机（推荐） | 兼顾强度与变化；精英部队 N 小、杂兵 N 大 |
| D3 | 按态势档动态：碾压取 top-1，均势取 top-5 | 与方案 A 天然联动 |

**优点**：精锐部队不再"犯低级错误"，强度分层自然显现。
**缺点**：AI 整体变强，需配套调整其它难度参数。

---

### 方案 E：统一态势评估器（基础设施，其余方案的前置）

把 `IsForceLosingAt` 从 `TroopSupplyTroop` 提升到 `BattleSituation`：

```csharp
public struct BalanceSnapshot
{
    public int myTroops;
    public int enemyTroops;
    public int myCount;
    public int enemyCount;
    /// <summary>我方兵力占比（0~100）</summary>
    public int BalancePercent => (myTroops + enemyTroops) <= 0 ? 100
                                : myTroops * 100 / (myTroops + enemyTroops);
}

public static BalanceSnapshot EvaluateBalance(Cell center, Force force, int range, Scenario scenario);
```

**优点**：与 `BattleSituation` 既有设计目标一致（"各层 AI 共用同一套口径"）；一次遍历拿到敌我双方兵力 + 数量 + 占比。
**缺点**：需注意遍历开销（建议按回合缓存或降低调用频率）。

---

## 四、推荐组合与实施顺序

| 阶段 | 组合 | 内容 | 收益 |
|---|---|---|---|
| **P0** | E + A | 统一态势评估器 + 5 档权重表 | 作战部队获得态势感知，行为分级 |
| **P1** | C | 评分公式重构 + 常数外置 | 可调参、可解释 |
| **P2** | D2 | top-N 加权随机 | 强度分层显现 |
| **P3** | B | 角色分级（可选） | 玩法层差异 |

---

## 五、需要你抉择的四个点

1. **分级维度**：只做态势分档（A）？只做角色分级（B）？还是 A + B 都要？
2. **评分公式**：保持现状、只加一个"档位倍率"？还是完整重构去魔法数字（C）？
3. **行动选择**：保持加权随机？改为 top-N 择优（D2）？还是按档位动态（D3）？
4. **实施范围**：只做 P0？P0 + P1？还是按 P0 → P3 全量推进？

---

## 六、附：不引入分级也能先做的低成本清理

若暂时不想动评分体系，以下为**独立可做**的小改进：

| 项 | 内容 | 风险 |
|---|---|---|
| Q1 | 删死代码 `EvaluateCityDefenceStrength` | 无 |
| Q2 | 三处 `SkillAttackPriority` 抽公共辅助（如 `BonusForPrimaryTarget`） | 低 |
| Q3 | `EvaluateCellSafety` 的 `30` / `5` 外置到 `AIConfig` | 无 |
| Q4 | 启用 `MoveToTargetSafely`（替换进攻/防守中的 `TryCloseTo`） | 中（会改变行进路线，需实测） |
