# 诊断手册：编译验证与静默路径排查

## 一、编译诊断的可信度

| 手段 | 可信度 | 说明 |
|---|---|---|
| `read_console(types=["error"], count=40, include_stacktrace=true)` | ✅ **唯一可信** | 直接读 Unity 编译器输出 |
| `read_console` 返回 0 条 | ✅ 可信 | 说明编译干净 |
| `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)` | ⚠️ **不代表编译通过** | 只说明刷新完成 |
| `read_lints(paths=[...], severity=["error"])` | ❌ **会漏报** | 读的是 IDE 缓存，Unity 重编译后可能不失效 |

### 已证伪的案例

| 实际错误 | `read_lints` |
|---|---|
| `CS0111: Type 'City' already defines a member called 'GetJobCost'` | 0 error |
| `CS1061: 'Scenario' does not contain a definition for 'Month'` | 0 error |
| `CS1061: 'SangoObject' does not contain a definition for 'BelongForce'` | 0 error |

### 铁律

- `read_lints` 干净 **不能** 在回复里写"编译通过"
- `read_console` 拿不到时，必须写 **"未能确认编译"**
- 用户贴的报错优先级高于任何工具结论

## 二、`read_console` 超时的处理

**表现**：`ping not answered within 2.0s` / `session not ready` / `fetch failed`

**原因**：Unity 正在重导入脚本或编译，MCP 桥接被阻塞。

**处理顺序**：

1. **停止堆改动** —— 错误会累积，后面定位更难
2. 回复里列出本轮动过的**全部文件清单**（用户可对照 Console 自查）
3. 主动请用户贴报错
4. 等编辑器空闲后，**立刻** 跑一次 `read_console` 把积压错误清掉
5. 若超时期间必须继续，只做**互不相关的小步改动**

**注**：`read_console(action="clear")` 后再 `refresh_unity` 再 `get`，可拿到干净的一轮诊断。

## 三、静默清理路径排查

### 现象特征

玩家反馈 **"某个对象凭空消失了，但日志里查不到"**。

### 排查方法

**不要只信日志**，要把销毁入口穷举一遍。

```bash
# 1. 建筑 / 据点销毁
grep -rn "\.OnFall(" Scripts/

# 2. 集合移除
grep -rn "allBuildings.Remove\|RemoveBuilding(" Scripts/

# 3. 部队解散（与"正常进城"共用，尤其容易误判）
grep -rn "IsAlive = false" Scripts/Game/Object/Troop/

# 4. 关系/资源被改（同理会静默）
grep -rn "AddRelation(\|ReduceRelation(" Scripts/
```

### 已发现的静默入口（本项目）

| 入口 | 触发条件 | 是否已有日志 |
|---|---|---|
| `TryReplaceBuilding` | 内政地建满 + 规划器认为需换配置 | ✅ 已补（且调用已切断） |
| `City.OnYearStart` 清黑市 | 每年 1 月 | ✅ 已补 |
| `Building.ChangeCorps` 未完工 | `BelongCorps != corps` 且势力变化 | ✅ 已补 + 已修误触发 |
| `ChangeCity` 未完工 | 无调用方（死代码） | 有 `Log.Error` |

### `ChangeCorps` 的坑（重点）

```csharp
// 错误写法：只看势力字段
if ((!isComplate || isUpgrading) && BelongForce != newForce)
    OnFall(null);
```

`CityStateSystem.SyncBuildings` 只比较 `BelongCorps`：
- 军团一致、`BelongForce` 脏了 → 仍会进 `ChangeCorps` → 命中拆除 → **在建设施被清掉**

**正确写法**：

```csharp
// 同一军团：只纠正字段，绝不拆除
if (BelongCorps == corps)
{
    if (corps != null && BelongForce != newForce)
        BelongForce = newForce;
    return last;
}
// 真的跨势力才拆，并且必须打日志
if ((!isComplate || isUpgrading) && BelongForce != newForce)
{
    Log.Info($"...未完工且随城易主，拆除（{旧} → {新}）");
    OnFall(null);
    return last;
}
```

## 四、数值失真的排查

**症状**：某个效果"看起来应该生效但完全没效果"。

**优先怀疑整数除法的取整归零**：

| 现象 | 原因 |
|---|---|
| 外交事件"关系改善"无效果 | `+50 raw ÷ 100 = 0` |
| 同盟关系月增长为 0 | `+50 raw` 经 like 换算后取整为 0 |
| 一次性操作"没有变化" | 值太小，被 `Math.Max(x, 1)` 之前就归零 |

**排查手法**：把"写入值 → 单位换算 → 钳制"三段分别打日志验证。

## 五、一轮改动后的自检清单

- [ ] 新增方法名是否与已有成员重名（`grep "public .* 方法名"`）
- [ ] 基类是否真有该字段/属性（`SangoObject` 是弱基类）
- [ ] 门禁与扣费是否走同一个入口
- [ ] 扣费复核是否放在"注册对象 / 摘武将 / 置状态位"**之前**
- [ ] 新增的静默操作是否打了日志或写了注释
- [ ] `refresh_unity` + `read_console(errors)` 是否真的跑过
