# 六类高频分配源：识别、改法、本项目实例

单位说明：`B/次` 是单次调用分配量，`KB/帧` 是一帧累计（频次放大后的结果）。数据全部来自本项目代码注释里记录的 Profiler 实测。

---

## 1. 闭包捕获（最高频）

### 识别

- Profile 符号：`<>c__DisplayClass{id}_{n}` 类、`<方法名>b__{n}` 方法
- 代码特征：`xxx.ForEach(v => { ... })`、`xxx.Find(v => ...)`，且 lambda 里用到了外部变量（`this`、方法参数、局部变量）

### 代价

捕获任意一个变量，编译器就生成一个闭包类实例（约 24–40 B）+ 一个委托实例（约 32 B）。**捕获的变量越多不会更贵，但调用次数会乘上去**。

### 改法

把 `ForEach(lambda)` 换成按下标 `for`，捕获变量改为局部变量或参数。集合本身选对方向（`SangoObjectList` 倒序、`SangoObjectSet` 正序）。

```csharp
// ❌ 每次调用分配闭包 + 委托
scenario.citySet.ForEach(city => { city.UpdateRate(month); });

// ✅ 零分配（正序 1..Length 与 SangoObjectSet.ForEach 一致）
City[] cityArray = scenario.citySet.objects;
for (int i = 1; i < cityArray.Length; i++)
{
    City city = cityArray[i];
    if (city != null) city.UpdateRate(month);
}
```

实例：

| 位置 | 数据 | 说明 |
|---|---|---|
| `Scenario.cs:2262-2270` | `Scenario.UpdateCityRate` 单帧 **124.5 KB** | 捕获 `month`，每月每城一次；样本 `<>c__DisplayClass158_0.<UpdateCityRate>b__0` |
| `Map.cs:399-402` | `Map.GetRing` **168 B/次** → `TroopAIUtility.PriorityAction` 单帧 **132.6 KB** | lambda 捕获 `this/radius/checkMoveAble/cellList`；AI 目标扫描一次要 2 个环 |
| `Force.cs:1047-1051` | `Force.UpdateCanBuildBuildingTypes` **4.5 KB/帧**（26 个建筑类型 × ~172 B） | 两个 lambda 捕获循环变量 `x` |
| `City.cs:1985-1988` | 拆除黑市 | 同时是「必须倒序」现场 |

### 陷阱

`ForEach` 的语义是「回调里可以删元素」（倒序实现）。改写成 `for` 之后，`return` 要改成 `continue`，方向按下节第 1 条。

---

## 2. 委托 / Predicate 分配（藏在基础容器里）

### 识别

`List<T>.Find / FindAll / Exists / RemoveAll / Sort(Comparison)`、`Enumerable.Any/Where/Select/OrderBy`。这些 API 都吃 `Predicate<T>` / `Func<T,bool>`。

### 代价

即使 lambda 不捕获任何东西，非静态 lambda 仍要分配委托（静态缓存只在编译器能证明不捕获时生效，不要把优化押在这上面）。捕获时再加一个闭包。

### 改法

手写 `for` + 直接比较；`Sort` 用 `IComparer<T>` 实例或 `Comparison` 缓存的静态委托。

```csharp
// ❌ 每次调用 ≈56 B（闭包 + 委托）
return mObjects.Find(x => x != null && x.Id == id) != null;

// ✅ 零分配，语义完全一致
for (int i = 0; i < mObjects.Count; i++)
{
    T obj = mObjects[i];
    if (obj != null && obj.Id == id) return true;
}
return false;
```

实例：`SangoObjectList.cs:179-195` —— `Contains(int)` 是 `HasTechnique / HasFeatrue` 一类高频判重的地基，`Force.prepareTechniqueList` 每势力回合每技法调两次，单帧堆出 **20 KB+**（Profiler: `Force.UpdateTurnInfo`）。

---

## 3. 容器扩容

### 识别

Profile 符号：`List.Resize`、`Dictionary.Resize`、`HashSet.Add`、`Array.Copy`（在 GC.Alloc 列里连续出现）。
代码特征：`new List<T>()` / `new Dictionary<K,V>()` 不传容量，且元素数已知会到几十上百。

### 代价

`List` 容量 4→8→16→32…，每次翻倍都新分配数组并 `Array.Copy`；`Dictionary` 从 3→7→17…，还要重建桶。单次远距离寻路就能翻出上百 KB。

### 改法

1. **构造时给容量**：按常见规模给足，整个对局不再扩容（宁可浪费一点常驻内存）。
2. **复用字段**：`Dictionary` 放成 `readonly` 字段，每次 `Clear()`（保留容量）而不是 `new`。
3. **热点容器**在 `SangoObjectList<T>` 上提供了带容量构造：`new SangoObjectList<Person>(128)`。

```csharp
// ✅ SangoObjectList 新增的容量构造（无参构造必须保留，反序列化要用）
public SangoObjectList(int capacity) { mObjects = new List<T>(capacity > 0 ? capacity : 0); }
```

实例：

| 位置 | 数据 | 手法 |
|---|---|---|
| `Map.cs:757-760` | `GetMinCostDirectPath` 单次 **182.6 KB**（`Dictionary.set_Item`×3 + `HashSet.Add`×2） | 寻路用字典/集合预设容量 |
| `City.cs:403-406` | `City.OnForceTurnStart` × N 每处 **232 B ~ 1 KB** | `freePersons(64)` / `wildPersons(32)` / `allPersons(128)` / `captiveList(16)` / `allBuildings(64)` |
| `Force.cs:677-680` | `Force.UpdateTurnInfo` 单帧 6.5 KB 中 **2.0 KB** 来自 `CityList` 翻倍 | 势力容器容量预设 |
| `Force.cs:3726-3731`、`3767-3770` | `UpdateValidCreatedItemTypes` 单帧 **1.9 KB**（`Dictionary.Resize` 440/100/20 B） | 字典提成 `readonly itemTypeMapCache`，`Clear()` 复用 |

---

## 4. 函数内建表 / 建数组

### 识别

方法体里出现 `new int[,] {...}`、`new int[] {...}`、`new[]{...}`，内容是**常量**。
Profile 里表现为该方法本身有固定的小额分配（如 192 B），与参数无关。

### 改法

提成 `static readonly` 字段（或 `static readonly` 数组），只分配一次。

```csharp
// ❌ 每次调用 new 一个 12×3 的 int[,]（约 192 B），每月每城一次
int[,] table = { { 50, 10, 2 }, ... };

// ✅ 提成静态只读字段
static readonly int[,] RatePresetTable = { { 50, 10, 2 }, ... };
```

实例：`City.cs:625-680` —— `City.UpdateRate` 的粮价预设表，由 `Scenario.UpdateCityRate` 每月每城调用，Profiler 中该路径单帧 **124.5 KB**（与第 1 类的闭包叠加）。

注意：`int[,]` 在 C# 里是**多维数组**，索引器较慢。若表很小且访问不密集可接受；热路径建议改一维数组 + `index * stride`，但那是第二步优化，先别在同一个提交里做两件事。

---

## 5. 临时数组（`ToArray` / `ToList`）

### 识别

`.ToArray()`、`.ToList()`、`dict.Values.ToArray()`、`list.ToArray()`，出现在每次刷新都会走的方法里。

### 改法

- 只是遍历 → 直接遍历原集合（`Dictionary` 的值集合枚举器是 struct，零分配）。
- 只是传给需要 `IList` 的方法 → 看能否改成接收 `List<T>` / `IReadOnlyList<T>`；不能改就复用缓冲 List。
- 需要「快照防并发修改」→ 那是语义需求（见 `reverse-traversal.md` 的「快照」），保留，但确认是否真在热路径。

实例：`Force.cs:3726-3731` 原实现 `new Dictionary` + `ForEach` 闭包 + `Values.ToArray()` 三件套，全部改为零分配循环。

---

## 6. 重复计算（不是分配，但常与分配一起出现）

### 识别

双层循环里对内层每个元素重复读同一批属性、重复查同一个表。Profile 里表现为 CPU 占用高而非 GC。

### 改法

外层预取到局部数组；或将「每帧不变」的结果缓存到字段。

实例：

| 位置 | 数据 | 手法 |
|---|---|---|
| `ForceAIOutpost.cs:400-403` | `GetGuardStatScore` 是 `SetGuard` 里调用次数最多的一项 | 外层预取候选人五维到 `candidateScores`（`new List<int>(candidates.Count)` 一次） |
| `ForceAIOutpost.cs:461-463` | `SetGuard` 全部 **22.9 KB GC** | `maxStat` 提到循环外 |
| `TroopAIUtility.cs:2672-2675` | `PriorityAction` 单帧 **283.2 KB** | 计数数组改静态复用，每次 `Array.Clear`；遍历用「起点 + Ring(1..3)」 |
| `PersonLoyaltyRules.cs:140-143` | 1500 武将换季结算 **2.4 秒** | 遍历判定改为位域，热路径只剩一次位与 |
| `Troop.cs:3911-3915` | `TroopOccupoyCity.Prepare` 单帧 **11 ms / 283 KB** | 不要在属性 getter 里做 `Prepare`（隐藏副作用），改到显式时机 |

---

## 静态复用字段的三条纪律

`static` 缓冲/数组能干掉分配，但会引入状态，必须遵守：

1. **使用前 `Clear()` / `Array.Clear`**，否则上一轮的残值会被读进结果。
2. **注释里写明单线程前提**（本项目是回合制单线程；若将来引入并行，这些字段全部要改）。
3. **不要在可能与自身重入的路径上使用**（递归、回调里再次调用同一方法）——重入会让外层缓冲被内层清空。
